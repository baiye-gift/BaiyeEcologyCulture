using System.Collections.Generic;
using UnityEngine;

namespace Baiye.EcologyCulture
{
    // Native BuildingConduitEndpoints registers only BuildingDef's main ports.
    // ISecondaryInput/Output supplies port markers, not network endpoints.
    public sealed class CultureSecondaryEndpoints : KMonoBehaviour
    {
        private readonly List<FlowUtilityNetwork.NetworkItem> endpoints =
            new List<FlowUtilityNetwork.NetworkItem>();

        protected override void OnSpawn()
        {
            base.OnSpawn();
            RegisterEndpoints();
        }

        internal void RegisterEndpoints()
        {
            RemoveEndpoints();
            Building building = GetComponent<Building>();
            if (building == null) return;
            foreach (ISecondaryInput input in GetComponents<ISecondaryInput>())
                foreach (ConduitType type in new[] { ConduitType.Gas, ConduitType.Liquid })
                    if (input.HasSecondaryConduitType(type))
                        Register(type, Endpoint.Sink,
                            Grid.OffsetCell(building.NaturalBuildingCell(), input.GetSecondaryConduitOffset(type)));
            foreach (ISecondaryOutput output in GetComponents<ISecondaryOutput>())
                foreach (ConduitType type in new[] { ConduitType.Gas, ConduitType.Liquid })
                    if (output.HasSecondaryConduitType(type))
                        Register(type, Endpoint.Source,
                            Grid.OffsetCell(building.NaturalBuildingCell(), output.GetSecondaryConduitOffset(type)));
        }

        private void Register(ConduitType type, Endpoint direction, int cell)
        {
            if (!Grid.IsValidCell(cell)) return;
            foreach (FlowUtilityNetwork.NetworkItem existing in endpoints)
                if (existing.ConduitType == type && existing.EndpointType == direction && existing.Cell == cell)
                    return;
            var item = new FlowUtilityNetwork.NetworkItem(type, direction, cell, gameObject);
            Conduit.GetNetworkManager(type).AddToNetworks(cell, item, is_endpoint: true);
            endpoints.Add(item);
            Debug.Log("[BaiyeEcologyCulture] Secondary endpoint registered: " + type + " " + direction + " cell=" + cell);
        }

        private void RemoveEndpoints()
        {
            foreach (FlowUtilityNetwork.NetworkItem item in endpoints)
                Conduit.GetNetworkManager(item.ConduitType).RemoveFromNetworks(item.Cell, item, is_endpoint: true);
            endpoints.Clear();
        }

        protected override void OnCleanUp()
        {
            RemoveEndpoints();
            base.OnCleanUp();
        }
    }
}
