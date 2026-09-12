#region license

// Copyright (c) 2021, andreakarasho
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
// 1. Redistributions of source code must retain the above copyright
//    notice, this list of conditions and the following disclaimer.
// 2. Redistributions in binary form must reproduce the above copyright
//    notice, this list of conditions and the following disclaimer in the
//    documentation and/or other materials provided with the distribution.
// 3. All advertising materials mentioning features or use of this software
//    must display the following acknowledgement:
//    This product includes software developed by andreakarasho - https://github.com/andreakarasho
// 4. Neither the name of the copyright holder nor the
//    names of its contributors may be used to endorse or promote products
//    derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS ''AS IS'' AND ANY
// EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

#endregion

using System;
using System.Xml;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Network;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Lightweight diagnostic overlay: FPS, ping, packet rate, GC count.
    /// Refresh 500ms so it doesn't itself become a perf cost.
    /// </summary>
    internal class PerfHudGump : Gump
    {
        private const int WIDTH = 290;
        private const int HEIGHT = 174;
        private const int HISTORY = 120;
        private const int GRAPH_HEIGHT = 45;
        private const ushort HUE_GOOD = 0x0044;
        private const ushort HUE_WARNING = 0x0035;
        private const ushort HUE_BAD = 0x0021;

        private readonly AlphaBlendControl _bg;
        private readonly Label _fpsLabel, _pingLabel, _netLabel, _queueLabel, _gcLabel, _stallLabel;
        private readonly uint[] _pingHistory = new uint[HISTORY];
        private int _pingNext, _pingCount;
        private long _refreshTime;
        private uint _lastBytesIn, _lastBytesOut;
        private int _lastG0, _lastG1, _lastG2;

        public PerfHudGump() : this(120, 80) { }

        public PerfHudGump(int x, int y) : base(0, 0)
        {
            X = x;
            Y = y;
            Width = WIDTH;
            Height = HEIGHT;
            CanMove = true;
            AcceptMouseInput = true;
            CanCloseWithRightClick = true;
            WantUpdateSize = false;
            LayerOrder = UILayer.Over;

            Add(_bg = new AlphaBlendControl(0.6f) { Width = WIDTH, Height = HEIGHT });
            CustomGumpThemeManager.ApplyDataSurface(_bg, 0.6f);
            Add(new Label("Perf HUD", true, 0x0481, font: 1) { X = 6, Y = 4 });
            Add(_fpsLabel = new Label("FPS: -", true, 0x03B2, font: 1) { X = 6, Y = 22 });
            Add(_pingLabel = new Label("Ping: -", true, 0x03B2, font: 1) { X = 6, Y = 38 });
            Add(_netLabel = new Label("Net: -", true, 0x03B2, font: 1) { X = 6, Y = 54 });
            Add(_queueLabel = new Label("Queue: -", true, 0x03B2, font: 1) { X = 6, Y = 70 });
            Add(_gcLabel = new Label("GC: -", true, 0x03B2, font: 1) { X = 6, Y = 86 });
            Add(_stallLabel = new Label("Worst: none", true, 0x03B2, WIDTH - 12, font: 1) { X = 6, Y = 102 });
            _lastG0 = GC.CollectionCount(0);
            _lastG1 = GC.CollectionCount(1);
            _lastG2 = GC.CollectionCount(2);
        }

        public override GumpType GumpType => GumpType.PerfHud;

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;
            if (Time.Ticks < _refreshTime) return;
            _refreshTime = (long)Time.Ticks + 500;

            int fps = (int)CUOEnviroment.CurrentRefreshRate;
            FrameTimingMetrics.Snapshot(out double frameMs, out int lowFps);
            _fpsLabel.Text = $"FPS: {fps}  Frame: {frameMs:0.0} ms  1% low: {lowFps}";
            _fpsLabel.Hue = frameMs <= 17 && lowFps >= 50
                ? HUE_GOOD
                : frameMs <= 34 && lowFps >= 25 ? HUE_WARNING : HUE_BAD;

            uint ping = NetClient.Socket?.Statistics?.Ping ?? 0;
            AddPing(ping);
            double jitter = CalculateJitter();
            _pingLabel.Text = $"Ping: {ping} ms  Jitter: {jitter:0.0} ms";
            _pingLabel.Hue = GetLatencyHue(ping, jitter);

            uint inBytes = NetClient.Socket?.Statistics?.TotalBytesReceived ?? 0;
            uint outBytes = NetClient.Socket?.Statistics?.TotalBytesSent ?? 0;
            uint dIn = inBytes >= _lastBytesIn ? inBytes - _lastBytesIn : inBytes;
            uint dOut = outBytes >= _lastBytesOut ? outBytes - _lastBytesOut : outBytes;
            _lastBytesIn = inBytes;
            _lastBytesOut = outBytes;
            _netLabel.Text = $"Net: ↓{dIn} ↑{dOut} B/0.5s";

            AsyncNetClient socket = AsyncNetClient.Socket;
            _netLabel.Hue = socket.IsConnected ? HUE_GOOD : HUE_BAD;
            _queueLabel.Text = $"Queue: {socket.IncomingMessageCount} chunks / {socket.IncomingBytes} B / {socket.OldestIncomingMessageAgeMilliseconds} ms";
            _queueLabel.Hue = GetQueueHue(
                socket.IncomingMessageCount,
                socket.IncomingBytes,
                socket.OldestIncomingMessageAgeMilliseconds
            );

            int g0 = GC.CollectionCount(0);
            int g1 = GC.CollectionCount(1);
            int g2 = GC.CollectionCount(2);
            _gcLabel.Text = $"GC delta: {g0 - _lastG0}/{g1 - _lastG1}/{g2 - _lastG2}";
            _gcLabel.Hue = g2 > _lastG2
                ? HUE_BAD
                : g1 > _lastG1 || g0 - _lastG0 > 2 ? HUE_WARNING : HUE_GOOD;
            _lastG0 = g0; _lastG1 = g1; _lastG2 = g2;
            _stallLabel.Text = "Worst: " + MainThreadHangDiagnostics.LastWorstFrameSummary;
            long worstFrameMs = MainThreadHangDiagnostics.LastWorstFrameMilliseconds;
            _stallLabel.Hue = worstFrameMs == 0
                ? HUE_GOOD
                : worstFrameMs <= 100 ? HUE_WARNING : HUE_BAD;

            int requiredHeight = _stallLabel.Y + _stallLabel.Height + 6 + GRAPH_HEIGHT + 7;
            Height = Math.Max(HEIGHT, requiredHeight);
            _bg.Height = Height;
        }

        private static ushort GetLatencyHue(uint ping, double jitter)
        {
            if (ping == 0 || ping > 200 || jitter > 50)
                return HUE_BAD;
            if (ping > 150 || jitter > 20)
                return HUE_WARNING;
            return HUE_GOOD;
        }

        private static ushort GetQueueHue(int chunks, long bytes, long ageMilliseconds)
        {
            if (ageMilliseconds > 250 || bytes > 1024 * 1024 || chunks > 256)
                return HUE_BAD;
            if (ageMilliseconds > 50 || bytes > 64 * 1024 || chunks > 32)
                return HUE_WARNING;
            return HUE_GOOD;
        }

        private static Color GetPingColor(uint ping)
        {
            if (ping > 200)
                return Color.Red;
            if (ping > 150)
                return Color.Yellow;
            return Color.LimeGreen;
        }

        private void AddPing(uint ping)
        {
            if (ping == 0) return;
            _pingHistory[_pingNext] = ping;
            _pingNext = (_pingNext + 1) % HISTORY;
            if (_pingCount < HISTORY) _pingCount++;
        }

        private double CalculateJitter()
        {
            if (_pingCount < 2) return 0;
            long total = 0;
            int first = (_pingNext - _pingCount + HISTORY) % HISTORY;
            uint previous = _pingHistory[first];
            for (int i = 1; i < _pingCount; i++)
            {
                uint current = _pingHistory[(first + i) % HISTORY];
                total += Math.Abs((long)current - previous);
                previous = current;
            }
            return (double)total / (_pingCount - 1);
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            bool result = base.Draw(batcher, x, y);
            if (_pingCount < 2) return result;

            int graphY = _stallLabel.Y + _stallLabel.Height + 6;
            int first = (_pingNext - _pingCount + HISTORY) % HISTORY;
            uint max = 1;
            for (int i = 0; i < _pingCount; i++) max = Math.Max(max, _pingHistory[(first + i) % HISTORY]);
            Vector3 hue = ShaderHueTranslator.GetHueVector(0, false, 1f);
            for (int i = 0; i < _pingCount; i++)
            {
                uint value = _pingHistory[(first + i) % HISTORY];
                int height = Math.Max(1, (int)(value * GRAPH_HEIGHT / max));
                batcher.Draw(SolidColorTextureCache.GetTexture(GetPingColor(value)),
                    new Rectangle(x + 6 + i * 2, y + graphY + GRAPH_HEIGHT - height, 2, height), hue);
            }
            return result;
        }

        public override void Save(XmlTextWriter writer)
        {
            base.Save(writer);
        }

        public override void Restore(XmlElement xml)
        {
            base.Restore(xml);
        }
    }
}
