using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ClassicUO.Game.Managers
{
    // Observe accepted outgoing requests without modifying them. Plugin threads enqueue; only the main thread reads World.
    internal static class ClientInteractionObserver
    {
        private readonly struct Request
        {
            internal readonly int Kind;
            internal readonly uint Value;
            internal readonly uint Sender, Server;
            internal Request(int kind, uint value, uint sender = 0, uint server = 0) { Kind = kind; Value = value; Sender = sender; Server = server; }
        }
        private static readonly ConcurrentQueue<Request> Requests = new ConcurrentQueue<Request>();
        private static int _overflow;

        internal static bool Decode(ReadOnlySpan<byte> packet, out int kind, out uint value)
        {
            kind = 0; value = 0;
            if (packet.Length == 0) return false;
            if (packet[0] == 0x06 && packet.Length >= 5) { kind = 6; value = U32(packet, 1) & 0x7FFFFFFF; return true; }
            if (packet[0] == 0x6C && packet.Length >= 19 && packet[1] == 0)
            { kind = 2; value = U32(packet, 7); return value != 0; }
            if (packet[0] == 0xB1 && packet.Length >= 15) { kind = 4; value = U32(packet, 11); return true; }
            if (packet[0] == 0xBF && packet.Length >= 5)
            {
                int sub = packet[3] * 256 + packet[4];
                if (sub == 0x1C && packet.Length >= 9) { kind = 1; value = (uint)(packet[7] * 256 + packet[8]); return true; }
                if (sub == 0x2D && packet.Length >= 11) { kind = 5; value = (uint)(packet[5] * 256 + packet[6]); return true; }
                if ((sub == 0x13 || sub == 0x15) && packet.Length >= 9) { kind = 3; value = U32(packet, 5); return true; }
            }
            if (packet[0] == 0x12 && packet.Length >= 5 && (packet[3] == 0x56 || packet[3] == 0x27))
            {
                int i = 4;
                while (i < packet.Length && packet[i] >= '0' && packet[i] <= '9')
                { if (value > 10000) return false; value = value * 10 + (uint)(packet[i++] - '0'); }
                if (i == 4) return false;
                kind = 1; return true;
            }
            return false;
        }
        private static uint U32(ReadOnlySpan<byte> p, int offset) => (uint)p[offset] << 24 | (uint)p[offset + 1] << 16 | (uint)p[offset + 2] << 8 | p[offset + 3];

        internal static void Observe(ReadOnlySpan<byte> packet)
        {
            if (!Decode(packet, out int kind, out uint value)) return;
            if (Requests.Count >= 128) { Interlocked.Exchange(ref _overflow, 1); return; }
            Requests.Enqueue(kind == 4 ? new Request(kind, value, U32(packet, 3), U32(packet, 7))
                : kind == 5 ? new Request(kind, value, U32(packet, 7)) : new Request(kind, value));
        }
        internal static void Pump()
        {
            if (Interlocked.Exchange(ref _overflow, 0) != 0) { Reset(); TravelLocationManager.Reset(); return; }
            for (int i = 0; i < 128 && Requests.TryDequeue(out var request); i++)
            {
                if (!World.InGame) continue;
                if (request.Kind == 1) TravelLocationManager.ObserveCast((int)request.Value);
                else if (request.Kind == 2) TravelLocationManager.ObserveRuneTarget(World.Get(request.Value));
                else if (request.Kind == 3) VendorLedgerManager.ObserveInteraction(request.Value);
                else if (request.Kind == 6) { TravelLocationManager.ObserveBookOpen(request.Value); VendorLedgerManager.ObserveInteraction(request.Value); }
                else if (request.Kind == 5) { TravelLocationManager.ObserveCast((int)request.Value); TravelLocationManager.ObserveRuneTarget(World.Get(request.Sender)); }
                else TravelLocationManager.ObserveBookReply(request.Sender, request.Server, unchecked((int)request.Value));
            }
        }
        internal static void Reset() { while (Requests.TryDequeue(out _)) { } Interlocked.Exchange(ref _overflow, 0); }
    }
}
