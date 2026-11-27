using System;
using Unity.Mathematics;

namespace Anvelop.HexGrid.Samples
{
    /// <summary>Direct managed calls. Geometry needs neither jobs nor native containers.</summary>
    public static class ManagedHexGeometry
    {
        public static HexCoord PickXZ(float3 worldPoint)
        {
            var layout = new HexLayout(HexOrientation.Pointy, new float2(1f, 1f));
            return layout.FromWorldXZ(worldPoint);
        }

        public static float3 ToWorldXZ(HexCoord cell)
        {
            var layout = new HexLayout(HexOrientation.Pointy, new float2(1f, 1f));
            return layout.ToWorldXZ(cell);
        }

        public static int SumDistancesInRange(HexCoord center, int radius)
        {
            int total = 0;
            foreach (HexCoord cell in HexRegion.Around(center, radius)) total += center.DistanceTo(cell);
            return total;
        }

        public static int SumLineDistances(HexCoord from, HexCoord to)
        {
            int total = 0;
            foreach (HexCoord cell in new HexLine(from, to)) total += from.DistanceTo(cell);
            return total;
        }

        /// <summary>Setup-only example: ordinary managed array storage, allocated once.</summary>
        public static int[] CreateTerrainCosts(int radius)
        {
            if (radius < 0 || (long)radius * (radius + 1L) > (int.MaxValue - 1L) / 3)
                throw new ArgumentOutOfRangeException(nameof(radius));
            var storage = HexStorage.Hexagon(radius);
            var terrainCosts = new int[storage.Count];
            for (int i = 0; i < terrainCosts.Length; i++) terrainCosts[i] = 1;
            // Address the center through the same storage descriptor used for subsequent reads.
            storage.TryGetIndex(default, out int centerIndex);
            terrainCosts[centerIndex] = 5;
            return terrainCosts;
        }

        /// <summary>Reading existing managed storage performs no allocation.</summary>
        public static bool TryReadTerrainCost(int[] terrainCosts, int radius, HexCoord cell, out int cost)
        {
            cost = 0;
            if (terrainCosts == null || radius < 0 ||
                (long)radius * (radius + 1L) > (int.MaxValue - 1L) / 3) return false;
            var storage = HexStorage.Hexagon(radius);
            if (terrainCosts.Length != storage.Count || !storage.TryGetIndex(cell, out int index)) return false;
            cost = terrainCosts[index];
            return true;
        }
    }
}
