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
using ClassicUO.Game.UI.Controls;
using ClassicUO.Network;
using ClassicUO.Renderer;

namespace ClassicUO.Game.UI.Gumps
{
    /// <summary>
    /// Lightweight diagnostic overlay: FPS, ping, packet rate, GC count.
    /// Refresh 500ms so it doesn't itself become a perf cost.
    /// </summary>
    internal class PerfHudGump : Gump
    {
        private const int WIDTH = 200;
        private const int HEIGHT = 96;

        private readonly AlphaBlendControl _bg;
        private readonly Label _fpsLabel, _pingLabel, _netLabel, _gcLabel;
        private long _refreshTime;
        private uint _lastBytesIn, _lastBytesOut;

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
            Add(_gcLabel = new Label("GC: -", true, 0x03B2, font: 1) { X = 6, Y = 70 });
        }

        public override GumpType GumpType => GumpType.PerfHud;

        public override void Update()
        {
            base.Update();
            if (IsDisposed) return;
            if (Time.Ticks < _refreshTime) return;
            _refreshTime = (long)Time.Ticks + 500;

            int fps = (int)CUOEnviroment.CurrentRefreshRate;
            _fpsLabel.Text = $"FPS: {fps}";

            uint ping = NetClient.Socket?.Statistics?.Ping ?? 0;
            _pingLabel.Text = $"Ping: {ping} ms";

            uint inBytes = NetClient.Socket?.Statistics?.TotalBytesReceived ?? 0;
            uint outBytes = NetClient.Socket?.Statistics?.TotalBytesSent ?? 0;
            uint dIn = inBytes - _lastBytesIn;
            uint dOut = outBytes - _lastBytesOut;
            _lastBytesIn = inBytes;
            _lastBytesOut = outBytes;
            _netLabel.Text = $"Net: ↓{dIn} ↑{dOut} B/0.5s";

            int g0 = GC.CollectionCount(0);
            int g1 = GC.CollectionCount(1);
            int g2 = GC.CollectionCount(2);
            _gcLabel.Text = $"GC: {g0}/{g1}/{g2}";
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
