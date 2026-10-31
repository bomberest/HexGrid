using System;
using Unity.Collections;

namespace Anvelop.HexGrid.Samples
{
    /// <summary>
    /// Ordinary C# owner for reusable native buffers. Queries run synchronously on the calling thread.
    /// Construct once, consume each result before the next query, and dispose when the owner stops.
    /// </summary>
    /// <remarks>This sample is not thread-safe. Valid-input queries allocate no managed or native memory.
    /// Construction allocates the owner object and its buffers; error exceptions can also allocate.</remarks>
    public sealed class ManagedHexMap : IDisposable
    {
        private NativeArray<HexCoord> cells;
        private NativeArray<int> costs;
        private NativeParallelHashMap<HexCoord, int> indices;
        private NativeArray<HexSearchNode> nodes;
        private NativeArray<int> frontier;
        private NativeArray<HexCoord> output;
        private bool disposed;

        /// <summary>Valid output prefix; each search/range/FOV query replaces it. No result array is copied.</summary>
        public int ResultCount { get; private set; }

        public int CellCount
        {
            get { ThrowIfDisposed(); return cells.Length; }
        }

        /// <summary>Create a hexagonal map. Costs are charged when entering a cell; negative costs block it.</summary>
        public ManagedHexMap(int radius, int defaultCost = 1, HexCoord center = default)
        {
            if (radius < 0 || (long)radius * (radius + 1L) > (int.MaxValue - 1L) / 3)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (defaultCost < 0) throw new ArgumentOutOfRangeException(nameof(defaultCost));

            var storage = HexStorage.Hexagon(radius, center);
            int count = storage.Count;
            try
            {
                cells = new NativeArray<HexCoord>(count, Allocator.Persistent);
                costs = new NativeArray<int>(count, Allocator.Persistent);
                indices = new NativeParallelHashMap<HexCoord, int>(count, Allocator.Persistent);
                nodes = new NativeArray<HexSearchNode>(count, Allocator.Persistent);
                frontier = new NativeArray<int>(count, Allocator.Persistent);
                output = new NativeArray<HexCoord>(count, Allocator.Persistent);
                for (int i = 0; i < count; i++)
                {
                    storage.TryGetCoordinate(i, out HexCoord cell);
                    cells[i] = cell;
                    costs[i] = defaultCost;
                    indices.Add(cell, i);
                }
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>Change terrain cost outside an active query. A successful change invalidates the previous result.</summary>
        public bool SetCost(HexCoord cell, int cost)
        {
            ThrowIfDisposed();
            if (!indices.TryGetValue(cell, out int index)) return false;
            costs[index] = cost;
            ResultCount = 0;
            return true;
        }

        public HexSearchResult FindPath(HexCoord start, HexCoord goal)
        {
            ThrowIfDisposed();
            return SaveResult(HexSearch.FindPath(Map, start, goal, nodes, frontier, output));
        }

        public HexSearchResult Reachable(HexCoord start, long budget)
        {
            ThrowIfDisposed();
            return SaveResult(HexSearch.Reachable(Map, start, budget, nodes, frontier, output));
        }

        public HexSearchResult ReachableSteps(HexCoord start, int maxSteps)
        {
            ThrowIfDisposed();
            return SaveResult(HexSearch.ReachableSteps(Map, start, maxSteps, nodes, frontier, output));
        }

        public HexSearchResult FieldOfView(HexCoord origin, int radius, bool includeBlockingTargets = true)
        {
            ThrowIfDisposed();
            return SaveResult(HexSearch.FieldOfView(Map, origin, radius, output, includeBlockingTargets));
        }

        public bool HasLineOfSight(HexCoord from, HexCoord to, bool includeBlockingTarget = true)
        {
            ThrowIfDisposed();
            return HexSearch.HasLineOfSight(Map, from, to, includeBlockingTarget);
        }

        public HexCoord GetResultCell(int index)
        {
            ThrowIfDisposed();
            if ((uint)index >= (uint)ResultCount) throw new ArgumentOutOfRangeException(nameof(index));
            return output[index];
        }

        public void Dispose()
        {
            if (disposed) return;
            if (output.IsCreated) output.Dispose();
            if (frontier.IsCreated) frontier.Dispose();
            if (nodes.IsCreated) nodes.Dispose();
            if (indices.IsCreated) indices.Dispose();
            if (costs.IsCreated) costs.Dispose();
            if (cells.IsCreated) cells.Dispose();
            ResultCount = 0;
            disposed = true;
        }

        private HexMap Map => new HexMap(cells, costs, indices);

        private HexSearchResult SaveResult(HexSearchResult result)
        {
            ResultCount = result.Status == HexSearchStatus.Success ? result.Count : 0;
            return result;
        }

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(ManagedHexMap));
        }
    }
}
