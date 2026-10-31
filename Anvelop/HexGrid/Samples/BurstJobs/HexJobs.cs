using Anvelop.HexGrid;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Anvelop.HexGrid.Samples
{
    /// <summary>Schedule after allocating Cells and Distances once; the caller disposes them after completion.</summary>
    [BurstCompile]
    public struct HexDistancesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<HexCoord> Cells;
        public HexCoord Origin;
        [WriteOnly] public NativeArray<int> Distances;
        public void Execute(int index) => Distances[index] = Origin.DistanceTo(Cells[index]);
    }

    /// <summary>Allocate the five arrays and index before scheduling; none of them may alias.</summary>
    [BurstCompile]
    public struct HexPathJob : IJob
    {
        [ReadOnly] public NativeArray<HexCoord> Cells;
        [ReadOnly] public NativeArray<int> Costs;
        [ReadOnly] public NativeParallelHashMap<HexCoord, int> Indices;
        public HexCoord Start, Goal;
        public NativeArray<HexSearchNode> Nodes;
        public NativeArray<int> Frontier;
        [WriteOnly] public NativeArray<HexCoord> Path;
        [WriteOnly] public NativeArray<HexSearchResult> Result;

        public void Execute()
        {
            Result[0] = HexSearch.FindPath(new HexMap(Cells, Costs, Indices), Start, Goal, Nodes, Frontier, Path);
        }
    }
}
