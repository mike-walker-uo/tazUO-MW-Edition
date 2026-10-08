using System.Collections.Concurrent;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Network;

namespace ClassicUO.Game.Managers
{
    public class MoveItemQueue
    {
        public static MoveItemQueue Instance { get; private set; }

        public bool IsEmpty => _isEmpty;

        private bool _isEmpty = true;
        private readonly ConcurrentQueue<MoveRequest> _queue = new();

        public MoveItemQueue()
        {
            Instance = this;
        }

        public void Enqueue(uint serial, uint destination, ushort amt = 0, int x = 0xFFFF, int y = 0xFFFF, int z = 0) =>
            EnqueueTracked(serial, destination, amt, x, y, z, null);

        internal void EnqueueTracked(uint serial, uint destination, ushort amt, int x, int y, int z, QueuedOperation operation)
        {
            if (operation?.Cancelled == true) return;
            if (operation != null) operation.Total++;
            if(amt == 0)
            {
                Item i = World.Items.Get(serial);

                if (i != null)
                    amt = i.Amount;
                else
                    amt = 1;
            }

            _queue.Enqueue(new MoveRequest(serial, destination, amt, x, y, z, operation: operation));
            _isEmpty = false;
        }

        public void EnqueueQuick(Item item)
        {
            Item backpack = World.Player.FindItemByLayer(Layer.Backpack);

            if (backpack == null)
            {
                return;
            }

            uint bag = ProfileManager.CurrentProfile.GrabBagSerial == 0 ? backpack.Serial : ProfileManager.CurrentProfile.GrabBagSerial;

            Enqueue(item.Serial, bag, item.Amount, 0xFFFF, 0xFFFF);
        }

        public void EnqueueEquipSingle(uint serial, Layer layer)
        {
            Item i = World.Items.Get(serial);

            if (i == null) return;

            _queue.Enqueue(new MoveRequest(serial, uint.MaxValue, 1, 0xFFFF, 0xFFFF, 0, layer));
            _isEmpty = false;
        }

        public void EnqueueQuick(uint serial)
        {
            Item i = World.Items.Get(serial);

            if(i != null)
                EnqueueQuick(i);
        }

        public void ProcessQueue()
        {
            if (_isEmpty) return;
            int discarded = 0;
            while (_queue.TryPeek(out var cancelled) && cancelled.Operation?.Cancelled == true && discarded < 256)
            { _queue.TryDequeue(out _); discarded++; }
            _isEmpty = _queue.IsEmpty;
            if (_isEmpty || discarded == 256) return;

            if (GlobalActionCooldown.IsOnCooldown)
            { SetWaiting("Waiting for object delay"); return; }

            if (Client.Game.GameCursor.ItemHold.Enabled)
            { SetWaiting("Waiting: cursor is holding an item"); return; }

            if (!_queue.TryDequeue(out var request))
                return;

            if (request.Operation?.Cancelled == true)
            { _isEmpty = _queue.IsEmpty; return; }
            Item live = World.Items.Get(request.Serial);
            if (request.Operation != null && (live == null || live.IsDestroyed))
            { request.Operation.Skipped++; _isEmpty = _queue.IsEmpty; return; }
            AsyncNetClient.Socket.Send_PickUpRequest(request.Serial, request.Amount);

            if(request.Destination != uint.MaxValue)
            {
                GameActions.DropItem(request.Serial, request.X, request.Y, request.Z, request.Destination, true);
            }
            else
            {
                AsyncNetClient.Socket.Send_EquipRequest(request.Serial, request.Layer, World.Player);
            }

            if (request.Operation != null)
            { request.Operation.Sent++; request.Operation.Detail = "Request sent; waiting for next slot"; }
            GlobalActionCooldown.BeginCooldown();
            _isEmpty = _queue.IsEmpty;
        }

        private void SetWaiting(string reason)
        {
            if (_queue.TryPeek(out var request) && request.Operation != null && !request.Operation.Cancelled)
                request.Operation.Detail = reason;
        }

        public void Clear()
        {
            while (_queue.TryDequeue(out var request)) request.Operation?.Cancel();
            _isEmpty = true;
        }

        private readonly struct MoveRequest(uint serial, uint destination, ushort amount, int x, int y, int z, Layer layer = Layer.Invalid, QueuedOperation operation = null)
        {
            public uint Serial { get; } = serial;
            public uint Destination { get; } = destination;
            public ushort Amount { get; } = amount;
            public int X { get; } = x;
            public int Y { get; } = y;
            public int Z { get; } = z;

            public Layer Layer { get; } = layer;
            internal QueuedOperation Operation { get; } = operation;
        }
    }
}
