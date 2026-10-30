using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Network;

namespace ClassicUO.Game.Managers
{
    // Uses only read-only actions found in the server's actual vendor menu.
    internal static class VendorRefreshManager
    {
        private const int ResponseTimeoutMs = 5000, BagTimeoutMs = 2000, ActionDelayMs = 1000;
        private static readonly Queue<VendorLedgerRecord> _queue = new Queue<VendorLedgerRecord>();
        private static readonly HashSet<uint> _opened = new HashSet<uint>();
        private static Profile _profile;
        private static VendorLedgerRecord _current;
        private static Gump _gump;
        private static int _step, _button, _updated, _skipped;
        private static long _deadline, _next;
        private static uint _waitingBag, _propertyBag;
        private static bool _partial;
        private static string _lastSkip = "";
        internal static bool Active => _step != 0;
        internal static string Status { get; private set; } = "";
        internal static bool OwnsHistory(Gump gump) => Active && _step == 4 && ReferenceEquals(_gump, gump);

        internal static void Start(VendorLedgerRecord only = null)
        {
            if (!World.InGame || Active) return;
            Reset(); _profile = ProfileManager.CurrentProfile;
            foreach (var record in VendorLedgerManager.Records.Where(r => r.VendorSerial != 0 && (only == null || ReferenceEquals(r, only))))
            {
                if (Nearby(record)) _queue.Enqueue(record); else _skipped++;
            }
            NextVendor();
        }

        private static bool Nearby(VendorLedgerRecord record)
        {
            var mobile = World.Mobiles.Get(record.VendorSerial);
            return mobile != null && mobile != World.Player && !mobile.IsDestroyed
                && Math.Abs(mobile.X - World.Player.X) <= 3 && Math.Abs(mobile.Y - World.Player.Y) <= 3;
        }

        private static void Wait(int step, string text)
        { _step = step; _deadline = (long)Time.Ticks + ResponseTimeoutMs; _next = (long)Time.Ticks + ActionDelayMs; Status = text; }

        private static void Skip(string reason)
        { _lastSkip = _current.ShopName + ": " + reason; _skipped++; NextVendor(); }

        private static void NextVendor()
        {
            RestoreHistory();
            _gump = null; _opened.Clear(); _waitingBag = _propertyBag = 0; _partial = false;
            if (_queue.Count == 0)
            {
                _step = 0; _current = null;
                Status = "Updated " + _updated + " vendors; " + _skipped + " skipped / incomplete. Move within 3 tiles to update distant vendors.";
                if (_lastSkip.Length > 0) Status += " Last skipped: " + _lastSkip;
                GameActions.Print(Status, 0x35); return;
            }
            _current = _queue.Dequeue();
            if (!Nearby(_current)) { Skip("vendor is not within 3 tiles"); return; }
            Wait(1, "Opening vendor menu: " + _current.ShopName);
            // GameActions.DoubleClick attacks mobiles in war mode; this is always a read-only vendor use.
            AsyncNetClient.Socket.Send_DoubleClick(_current.VendorSerial);
        }

        // Find labelled actions geometrically; never guess shard-specific button IDs.
        internal static Button Action(Gump gump, params string[] names)
        {
            var labels = gump.Children.Where(c => c.IsFromServer && !c.IsDisposed
                && names.Any(n => VendorLedgerManager.Text(c).Equals(n, StringComparison.OrdinalIgnoreCase))).ToArray();
            // ID 0 is a valid Cancel reply; excluding it can match the adjacent sales-history button instead.
            return gump.Children.OfType<Button>().Where(b => b.IsFromServer && !b.IsDisposed && b.ButtonAction == ButtonAction.Activate)
                .Select(b => new { Button = b, Distance = labels.Length == 0 ? int.MaxValue : labels.Min(l =>
                    Math.Abs(b.Y - l.Y) > 12 ? int.MaxValue : Math.Abs(b.Y - l.Y) * 4
                    + Math.Min(Math.Abs(b.X + b.Width - l.X), Math.Abs(b.X - l.X - l.Width))) })
                .Where(p => p.Distance < 100).OrderBy(p => p.Distance).Select(p => p.Button).FirstOrDefault();
        }

        internal static void ObserveGump(Gump gump, VendorGumpKind kind, VendorLedgerRecord record)
        {
            if (!Active || record == null || record != _current) return;
            if (kind == VendorGumpKind.Owner && (_step == 1 || _step == 5))
            {
                bool history = _step == 1;
                var button = history ? Action(gump, "View Sales History") : Action(gump, "See goods");
                if (button == null && history)
                { _partial = true; history = false; button = Action(gump, "See goods"); }
                if (button == null) { Skip("requested sales / goods action is unavailable"); return; }
                _gump = gump; _button = button.ButtonID;
                Wait(history ? 2 : 6, "Updating " + _current.ShopName);
            }
            else if (kind == VendorGumpKind.History && _step == 3)
            { _gump = gump; Wait(4, "Sales captured: " + _current.ShopName); }
        }

        internal static void ObserveContainer(uint serial, VendorLedgerRecord record)
        {
            if (!Active || record != _current) return;
            if (_step == 7 && serial == record.GoodsSerial) Wait(8, "Loading vendor bags: " + record.ShopName);
            if (_step == 8 && serial == _waitingBag) { _waitingBag = 0; _next = (long)Time.Ticks + 1500; }
        }

        internal static void Update()
        {
            if (!Active) return;
            if (!World.InGame || _profile != ProfileManager.CurrentProfile) { Reset(); return; }
            if (!Nearby(_current)) { Skip("vendor moved out of range"); return; }
            if (Time.Ticks > _deadline)
            {
                if (_step == 3) { _partial = true; OpenGoods(); }
                else Skip("timed out: " + Status);
                return;
            }
            if (Time.Ticks < _next) return;
            if (_step == 2 || _step == 6)
            {
                if (_gump == null || _gump.IsDisposed) { Skip("vendor menu was closed"); return; }
                var gump = _gump; int button = _button;
                Wait(_step == 2 ? 3 : 7, "Waiting for server: " + _current.ShopName);
                gump.OnButtonClick(button);
            }
            else if (_step == 4)
                OpenGoods();
            else if (_step == 8)
            {
                if (_waitingBag != 0)
                {
                    // _next already includes the action delay: bound total bag response time to two seconds.
                    if (Time.Ticks < _next + BagTimeoutMs - ActionDelayMs) return;
                    _waitingBag = 0; _partial = true;
                }
                var root = World.Items.Get(_current.GoodsSerial);
                Item bag = FindBag(root, new HashSet<uint>());
                if (bag != null && _opened.Count < 64)
                {
                    // Do not use a sale lot while its price / display-bag properties are still unknown.
                    if (World.ClientFeatures.TooltipsEnabled && !World.OPL.TryGetNameAndData(bag.Serial, out _, out _))
                    {
                        if (_propertyBag != bag.Serial)
                        {
                            _propertyBag = bag.Serial;
                            PacketHandlers.AddMegaClilocRequest(bag.Serial);
                            _next = (long)Time.Ticks + BagTimeoutMs; _deadline = _next + ResponseTimeoutMs;
                        }
                        else { _opened.Add(bag.Serial); _propertyBag = 0; _partial = true; }
                        return;
                    }
                    _propertyBag = 0;
                    _opened.Add(bag.Serial); _waitingBag = bag.Serial;
                    _deadline = (long)Time.Ticks + ResponseTimeoutMs; _next = (long)Time.Ticks + ActionDelayMs;
                    GameActions.DoubleClick(bag.Serial); return;
                }
                if (bag != null || _partial) { Skip("sales history or one or more vendor bags were unavailable; received data kept"); return; }
                _updated++;
                NextVendor();
            }
        }

        private static void OpenGoods()
        {
            if (_gump != null && !_gump.IsDisposed) _gump.OnButtonClick(0);
            Wait(5, "Opening goods: " + _current.ShopName);
            AsyncNetClient.Socket.Send_DoubleClick(_current.VendorSerial);
        }

        private static Item FindBag(Item container, HashSet<uint> seen)
        {
            if (container == null || !seen.Add(container.Serial)) return null;
            for (var node = container.Items; node != null; node = node.Next)
                if (node is Item item && !item.IsDestroyed && VendorLedgerManager.StockContainer(item))
                {
                    if (!_opened.Contains(item.Serial)) return item;
                    var child = FindBag(item, seen); if (child != null) return child;
                }
            return null;
        }

        internal static void Stop() { Reset(); Status = "Vendor update stopped. Received data remains saved."; }
        private static void RestoreHistory()
        {
            if (_gump != null && !_gump.IsDisposed && _gump.SuppressPersistence)
            { _gump.IsVisible = true; _gump.SuppressPersistence = false; }
        }
        internal static void Reset()
        { RestoreHistory(); _step = 0; _profile = null; _current = null; _gump = null; _queue.Clear(); _opened.Clear(); _waitingBag = _propertyBag = 0; _partial = false; _updated = _skipped = 0; _lastSkip = ""; Status = ""; }
    }
}
