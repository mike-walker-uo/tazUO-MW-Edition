// TazUO addition: shared readiness evaluation for restock and equipment checks.

using System;
using System.Collections.Generic;
using System.Linq;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;

namespace ClassicUO.Game.Managers
{
    internal static class ReadinessCheckManager
    {
        internal const int BackpackItemLimit = 125;
        internal const int MinimumFreeItems = 5;
        internal const int MinimumFreeWeight = 10;

        internal static ReadinessSnapshot Evaluate()
        {
            var result = new ReadinessSnapshot();

            foreach (RestockEntry entry in RestockAgentManager.Settings.Items)
            {
                if (entry.DesiredAmount == 0)
                {
                    continue;
                }

                result.ActiveSupplyTargets++;

                if (RestockAgentManager.CountInTarget(entry) >= entry.DesiredAmount)
                {
                    result.ReadySupplyTargets++;
                }
            }

            result.SuppliesReady = result.ActiveSupplyTargets > 0
                                   && result.ReadySupplyTargets == result.ActiveSupplyTargets;

            IEnumerable<DurabiltyProp> durabilities = World.DurabilityManager?.Durabilities
                                                       ?? Enumerable.Empty<DurabiltyProp>();

            foreach (DurabiltyProp durability in durabilities)
            {
                Item item = World.Items.Get((uint)durability.Serial);
                RecordDurability(result, durability, item != null && item.ItemData.IsWeapon);
            }

            PlayerMobile player = World.Player;
            CheckUntrackedWeapon(result, player?.FindItemByLayer(Layer.OneHanded));
            CheckUntrackedWeapon(result, player?.FindItemByLayer(Layer.TwoHanded));
            result.DurabilityReady = result.BelowMinimumDurabilityItems == 0
                                     && result.UnverifiedWeaponItems == 0;

            List<byte> requiredLayers = RestockAgentManager.Settings.RequiredEquipmentLayers;
            result.EquipmentBaselineCount = requiredLayers.Count;

            if (World.Player != null)
            {
                foreach (byte value in requiredLayers)
                {
                    Layer layer = (Layer)value;

                    if (World.Player.FindItemByLayer(layer) == null)
                    {
                        result.MissingEquipmentLayers.Add(layer);
                    }
                }
            }

            result.EquipmentReady = result.MissingEquipmentLayers.Count == 0;

            if (player != null)
            {
                for (LinkedObject node = player.Items; node != null; node = node.Next)
                {
                    Item item = (Item)node;
                    Layer layer = item.Layer;
                    if (layer <= Layer.Invalid || layer >= Layer.Mount
                        || layer == Layer.Hair || layer == Layer.Beard || layer == Layer.Backpack)
                        continue;

                    result.InsuranceItems++;
                    if (!World.OPL.TryGetNameAndData(item.Serial, out _, out _))
                    {
                        World.OPL.Contains(item.Serial);
                        result.UnverifiedInsuranceLayers.Add(layer);
                        continue;
                    }

                    var properties = new ItemPropertiesData(item);
                    bool protectedItem = properties.singlePropertyData.Any(property =>
                        string.Equals(property.Name, "Insured", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(property.Name, "Blessed", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(property.Name, "Cursed", StringComparison.OrdinalIgnoreCase));
                    if (!protectedItem)
                        result.UninsuredEquipmentLayers.Add(layer);
                }
            }

            result.InsuranceReady = result.UninsuredEquipmentLayers.Count == 0
                                    && result.UnverifiedInsuranceLayers.Count == 0;

            Item backpack = player?.FindItemByLayer(Layer.Backpack);
            result.BackpackAvailable = backpack != null;

            if (player != null)
            {
                result.Weight = player.Weight;
                result.WeightMax = player.WeightMax;
                result.WeightReady = player.WeightMax == 0
                                     || (int)player.WeightMax - player.Weight >= MinimumFreeWeight;
            }

            result.BackpackItems = RestockAgentManager.CountBackpackItems();
            result.BackpackSpaceReady = backpack != null
                                        && result.BackpackItems <= BackpackItemLimit - MinimumFreeItems;
            result.BackpackReady = result.BackpackAvailable
                                   && result.WeightReady
                                   && result.BackpackSpaceReady;

            result.IsReady = World.Player != null
                             && result.SuppliesReady
                             && result.DurabilityReady
                             && result.EquipmentReady
                             && result.InsuranceReady
                             && result.BackpackReady;
            return result;
        }

        private static void CheckUntrackedWeapon(ReadinessSnapshot result, Item item)
        {
            DurabilityManager manager = World.DurabilityManager;
            if (item == null || !item.ItemData.IsWeapon
                || manager != null && manager.TryGetDurability(item.Serial, out _))
                return;

            if (DurabilityManager.TryGetItemDurability(item, out int current, out int maximum))
                RecordDurability(result, new DurabiltyProp((int)item.Serial, current, maximum), true);
            else
                result.UnverifiedWeaponItems++;
        }

        private static void RecordDurability(ReadinessSnapshot result, DurabiltyProp durability, bool isWeapon)
        {
            if (durability.MaxDurabilty <= 0)
                return;

            result.DurabilityItems++;
            if (result.LowestDurability == null
                || durability.Durabilty < result.LowestDurability.Durabilty)
                result.LowestDurability = durability;
            if (!DurabilityManager.MeetsReadinessMinimum(durability.Durabilty, isWeapon))
            {
                result.BelowMinimumDurabilityItems++;
                if (result.LowestBelowMinimumDurability == null
                    || durability.Durabilty < result.LowestBelowMinimumDurability.Durabilty)
                    result.LowestBelowMinimumDurability = durability;
            }
        }
    }

    internal sealed class ReadinessSnapshot
    {
        internal bool IsReady { get; set; }
        internal bool SuppliesReady { get; set; }
        internal int ActiveSupplyTargets { get; set; }
        internal int ReadySupplyTargets { get; set; }
        internal bool DurabilityReady { get; set; }
        internal int DurabilityItems { get; set; }
        internal int BelowMinimumDurabilityItems { get; set; }
        internal int UnverifiedWeaponItems { get; set; }
        internal DurabiltyProp LowestDurability { get; set; }
        internal DurabiltyProp LowestBelowMinimumDurability { get; set; }
        internal bool EquipmentReady { get; set; }
        internal int EquipmentBaselineCount { get; set; }
        internal List<Layer> MissingEquipmentLayers { get; } = new List<Layer>();
        internal bool InsuranceReady { get; set; }
        internal int InsuranceItems { get; set; }
        internal List<Layer> UninsuredEquipmentLayers { get; } = new List<Layer>();
        internal List<Layer> UnverifiedInsuranceLayers { get; } = new List<Layer>();
        internal bool BackpackAvailable { get; set; }
        internal bool BackpackReady { get; set; }
        internal bool WeightReady { get; set; } = true;
        internal int Weight { get; set; }
        internal int WeightMax { get; set; }
        internal bool BackpackSpaceReady { get; set; }
        internal int BackpackItems { get; set; }
    }
}
