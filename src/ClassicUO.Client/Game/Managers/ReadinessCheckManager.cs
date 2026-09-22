// TazUO addition: shared readiness evaluation for restock and equipment checks.

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
                if (durability.MaxDurabilty <= 0)
                {
                    continue;
                }

                result.DurabilityItems++;

                if (result.LowestDurability == null
                    || durability.Durabilty < result.LowestDurability.Durabilty)
                {
                    result.LowestDurability = durability;
                }

                if (durability.Durabilty < DurabilityManager.CriticalDurability)
                {
                    result.CriticalDurabilityItems++;
                }
            }

            result.DurabilityReady = result.CriticalDurabilityItems == 0;

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

            PlayerMobile player = World.Player;
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
                             && result.BackpackReady;
            return result;
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
        internal int CriticalDurabilityItems { get; set; }
        internal DurabiltyProp LowestDurability { get; set; }
        internal bool EquipmentReady { get; set; }
        internal int EquipmentBaselineCount { get; set; }
        internal List<Layer> MissingEquipmentLayers { get; } = new List<Layer>();
        internal bool BackpackAvailable { get; set; }
        internal bool BackpackReady { get; set; }
        internal bool WeightReady { get; set; } = true;
        internal int Weight { get; set; }
        internal int WeightMax { get; set; }
        internal bool BackpackSpaceReady { get; set; }
        internal int BackpackItems { get; set; }
    }
}
