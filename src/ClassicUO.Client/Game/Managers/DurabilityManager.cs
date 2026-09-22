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

using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Assets;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.Managers
{
    public class DurabilityManager : IDisposable
    {
        internal const int CriticalDurability = 10;
        internal const ushort CriticalHue = 0x04E6;
        private const long WARNING_INTERVAL_MS = 10 * 60 * 1000;

        private readonly Dictionary<uint, DurabiltyProp> _itemLayerSlots = new();
        private long _nextWarningAt;
        private static Regex _durabilityRegex;
        private static string _durabilityTemplate;

        private static readonly Layer[] _equipLayers =
        {
            Layer.Cloak, Layer.Shirt, Layer.Pants, Layer.Shoes, Layer.Legs, Layer.Arms, Layer.Torso, Layer.Tunic, Layer.Ring, Layer.Bracelet, Layer.Gloves, Layer.Skirt,
            Layer.Robe, Layer.Waist, Layer.Necklace, Layer.Beard, Layer.Earrings, Layer.Helmet, Layer.OneHanded, Layer.TwoHanded, Layer.Talisman
        };

        public IEnumerable<DurabiltyProp> Durabilities => _itemLayerSlots.Values;

        public static bool HasDurabilityData { get; private set; }

        public DurabilityManager()
        {
            EventSink.OPLOnReceive += OnOPLReceive;
            EventSink.OnItemUpdated += OnItemUpdated;
            EventSink.OnDisconnected += OnDisconnected;
        }

        public bool TryGetDurability(uint serial, out DurabiltyProp durability)
        {
            return _itemLayerSlots.TryGetValue(serial, out durability);
        }

        public void Tick()
        {
            if (World.Player == null || !World.InGame || Time.Ticks < _nextWarningAt)
                return;

            DurabiltyProp lowest = null;
            Item lowestItem = null;
            foreach (DurabiltyProp durability in _itemLayerSlots.Values)
            {
                if (durability.MaxDurabilty <= 0 || durability.Durabilty >= CriticalDurability ||
                    (lowest != null && durability.Durabilty >= lowest.Durabilty))
                    continue;

                Item item = World.Items.Get((uint)durability.Serial);
                if (item == null || item.IsDestroyed)
                    continue;

                lowest = durability;
                lowestItem = item;
            }

            if (lowest == null)
                return;

            string name = string.IsNullOrWhiteSpace(lowestItem.Name) ? lowestItem.Layer.ToString() : lowestItem.Name;
            ToastManager.ShowPersistent("low-durability", $"Low durability: {name} ({lowest.Durabilty}/{lowest.MaxDurabilty})",
                0x21, new Color(255, 160, 168), AlertCategory.Equipment, AlertSeverity.Warning);
            _nextWarningAt = (long)Time.Ticks + WARNING_INTERVAL_MS;
        }

        private void OnOPLReceive(object s, OPLEventArgs e)
        {
            if (!SerialHelper.IsItem(e.Serial))
                return;

            if (!World.Items.TryGetValue(e.Serial, out var item) || item.IsDestroyed)
                return;

            UpdateItem(item, e.Data);
        }

        private void OnItemUpdated(object sender, EventArgs e)
        {
            if (!(sender is Item item))
                return;

            if (World.OPL.TryGetNameAndData(item.Serial, out _, out string data))
            {
                UpdateItem(item, data);
            }
            else if (_itemLayerSlots.Remove(item.Serial))
            {
                NotifyChanged();
            }
        }

        private void OnDisconnected(object sender, EventArgs e)
        {
            _nextWarningAt = 0;
            if (_itemLayerSlots.Count == 0)
                return;

            _itemLayerSlots.Clear();
            NotifyChanged();
        }

        public void Clear()
        {
            OnDisconnected(null, EventArgs.Empty);
        }

        public void Remove(uint serial)
        {
            if (_itemLayerSlots.Remove(serial))
                NotifyChanged();
        }

        private void UpdateItem(Item item, string data)
        {
            bool equipped = World.Player != null
                            && !item.IsDestroyed
                            && item.Container == World.Player.Serial
                            && _equipLayers.Contains(item.Layer);
            bool changed;

            if (equipped)
            {
                DurabiltyProp durability = ParseDurability((int)item.Serial, data);
                if (durability.Serial != 0)
                {
                    _itemLayerSlots.TryGetValue(item.Serial, out DurabiltyProp previous);
                    changed = previous == null || previous.Durabilty != durability.Durabilty
                              || previous.MaxDurabilty != durability.MaxDurabilty;
                    if (durability.Durabilty < CriticalDurability)
                        durability.CriticalPulseStartedAt = previous != null && previous.Durabilty < CriticalDurability
                            ? previous.CriticalPulseStartedAt
                            : (long)Time.Ticks;
                    _itemLayerSlots[item.Serial] = durability;
                }
                else
                {
                    changed = _itemLayerSlots.Remove(item.Serial);
                }
            }
            else
            {
                changed = _itemLayerSlots.Remove(item.Serial);
            }

            if (changed)
                NotifyChanged();
        }

        private void NotifyChanged()
        {
            UIManager.GetGump<DurabilitysGump>()?.RequestUpdateContents();
            UIManager.GetGump<ModernPaperdoll>()?.RequestUpdateContents();

            HasDurabilityData = _itemLayerSlots.Count > 0;
        }

        private static DurabiltyProp ParseDurability(int serial, string data)
        {
            if (string.IsNullOrEmpty(data))
                return new DurabiltyProp();

            string template = ClilocLoader.Instance.GetString(1060639, "Durability ~1_val~ / ~2_val~");
            return TryParseDurabilityValues(template, data, out int min, out int max)
                ? new DurabiltyProp(serial, min, max)
                : new DurabiltyProp();
        }

        internal static bool TryParseDurabilityValues(string template, string data, out int current, out int maximum)
        {
            current = maximum = 0;
            if (string.IsNullOrEmpty(template) || string.IsNullOrEmpty(data))
                return false;

            if (_durabilityRegex == null || !string.Equals(_durabilityTemplate, template, StringComparison.Ordinal))
            {
                string pattern = Regex.Replace(Regex.Escape(template), @"~\d+_[^~]+~", @"(\d+)");
                _durabilityRegex = new Regex(pattern, RegexOptions.IgnoreCase);
                _durabilityTemplate = template;
            }

            Match match = _durabilityRegex.Match(data);
            return match.Success
                   && int.TryParse(match.Groups[1].Value, out current)
                   && int.TryParse(match.Groups[2].Value, out maximum)
                   && current >= 0
                   && maximum > 0
                   && current <= maximum;
        }

        public void Dispose()
        {
            EventSink.OPLOnReceive -= OnOPLReceive;
            EventSink.OnItemUpdated -= OnItemUpdated;
            EventSink.OnDisconnected -= OnDisconnected;
            _itemLayerSlots.Clear();
            HasDurabilityData = false;
            _nextWarningAt = 0;
        }
    }

    public class DurabiltyProp
    {
        public int Serial { get; set; }
        public int Durabilty { get; set; }
        public int MaxDurabilty { get; set; }
        internal long CriticalPulseStartedAt { get; set; }

        public float Percentage => MaxDurabilty > 0 ? ((float)Durabilty / (float)MaxDurabilty) : 0;

        public DurabiltyProp(int serial, int current, int max)
        {
            Serial = serial;
            Durabilty = current;
            MaxDurabilty = max;
        }

        public DurabiltyProp() : this(0, 0, 0)
        {
        }
    }
}
