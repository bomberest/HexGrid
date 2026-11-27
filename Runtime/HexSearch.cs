using Unity.Collections;
using Unity.Mathematics;

namespace Anvelop.HexGrid
{
    /// <summary>Borrowed sparse grid view. The caller owns and disposes all containers.</summary>
    /// <remarks>Cells must be unique; Indices must map every coordinate to its exact array index.
    /// Costs are destination-entry costs: zero is allowed, negative values block movement and vision.
    /// Containers must not be mutated while a search or visibility operation is running.</remarks>
    public readonly struct HexMap
    {
        public readonly NativeArray<HexCoord> Cells;
        public readonly NativeArray<int> Costs;
        public readonly NativeParallelHashMap<HexCoord, int> Indices;

        public HexMap(NativeArray<HexCoord> cells, NativeArray<int> costs,
            NativeParallelHashMap<HexCoord, int> indices)
        {
            Cells = cells;
            Costs = costs;
            Indices = indices;
        }

        public bool IsCreated => Cells.IsCreated && Costs.IsCreated && Indices.IsCreated;
        public bool IsValid => IsCreated && Costs.Length == Cells.Length;

        public bool TryGetIndex(HexCoord cell, out int index)
        {
            if (Indices.TryGetValue(cell, out index) && (uint)index < (uint)Cells.Length && Cells[index] == cell)
                return true;
            index = -1;
            return false;
        }
    }

    public enum HexSearchStatus : byte
    {
        Success,
        NoPath,
        BufferTooSmall,
        InvalidInput
    }

    /// <summary>Count is the required output size, including on an output-capacity failure.
    /// Count is zero if scratch capacity is insufficient. Cost is the successful route's entry-cost sum.</summary>
    public readonly struct HexSearchResult
    {
        public readonly HexSearchStatus Status;
        public readonly int Count;
        public readonly long Cost;

        public HexSearchResult(HexSearchStatus status, int count = 0, long cost = 0)
        {
            Status = status;
            Count = count;
            Cost = cost;
        }
    }

    /// <summary>Scratch storage. Allocate one entry per map cell and reuse between sequential searches.</summary>
    public struct HexSearchNode
    {
        public long Cost;
        public long Priority;
        public int Parent;
        public int HeapIndex;
        public byte State;
    }

    /// <summary>Allocation-free graph and center-ray visibility operations over a borrowed sparse map.</summary>
    public static class HexSearch
    {
        /// <summary>A* with an automatically admissible minimum-entry-cost heuristic.
        /// The route includes start and goal. A zero minimum cost reduces the search to Dijkstra.</summary>
        /// <remarks>nodes and frontier each require map.Cells.Length entries. Output is untouched on failure.</remarks>
        public static HexSearchResult FindPath(HexMap map, HexCoord start, HexCoord goal,
            NativeArray<HexSearchNode> nodes, NativeArray<int> frontier, NativeArray<HexCoord> path)
        {
            HexSearchStatus status = Validate(map, start, nodes, frontier, out int startIndex);
            if (status != HexSearchStatus.Success) return new HexSearchResult(status);
            if (!map.TryGetIndex(goal, out int goalIndex)) return new HexSearchResult(HexSearchStatus.InvalidInput);
            if (map.Costs[goalIndex] < 0) return new HexSearchResult(HexSearchStatus.NoPath);
            if (!path.IsCreated) return new HexSearchResult(HexSearchStatus.InvalidInput);

            int minimumCost = Initialize(map, nodes);
            int heapCount = 0;
            SetStart(nodes, startIndex, (long)minimumCost * start.DistanceTo(goal));
            Push(nodes, frontier, ref heapCount, startIndex);
            while (heapCount > 0)
            {
                int current = Pop(nodes, frontier, ref heapCount);
                HexSearchNode currentNode = nodes[current];
                currentNode.State = 2;
                nodes[current] = currentNode;
                if (current == goalIndex)
                    return WritePath(map, nodes, startIndex, goalIndex, path);

                for (int direction = 0; direction < 6; direction++)
                {
                    HexCoord next = map.Cells[current].Neighbor(direction);
                    if (!map.TryGetIndex(next, out int nextIndex) || map.Costs[nextIndex] < 0) continue;
                    long cost = currentNode.Cost + map.Costs[nextIndex];
                    HexSearchNode nextNode = nodes[nextIndex];
                    if (nextNode.State != 0 && cost >= nextNode.Cost) continue;
                    byte previousState = nextNode.State;
                    nextNode.Cost = cost;
                    nextNode.Priority = cost + (long)minimumCost * next.DistanceTo(goal);
                    nextNode.Parent = current;
                    nextNode.State = 1;
                    nodes[nextIndex] = nextNode;
                    if (previousState == 1) SiftUp(nodes, frontier, nextNode.HeapIndex);
                    else Push(nodes, frontier, ref heapCount, nextIndex);
                }
            }
            return new HexSearchResult(HexSearchStatus.NoPath);
        }

        /// <summary>Dijkstra movement range, including start at cost zero. Output follows map cell order.</summary>
        /// <remarks>Cost is charged when entering a neighbor. Zero-cost routes may extend beyond the geometric radius.
        /// Scratch arrays each require map.Cells.Length entries. Output is untouched on capacity failure.</remarks>
        public static HexSearchResult Reachable(HexMap map, HexCoord start, long budget,
            NativeArray<HexSearchNode> nodes, NativeArray<int> frontier, NativeArray<HexCoord> output)
        {
            if (budget < 0 || !output.IsCreated) return new HexSearchResult(HexSearchStatus.InvalidInput);
            HexSearchStatus status = Validate(map, start, nodes, frontier, out int startIndex);
            if (status != HexSearchStatus.Success) return new HexSearchResult(status);
            Initialize(map, nodes);
            SetStart(nodes, startIndex, 0);
            int heapCount = 0;
            Push(nodes, frontier, ref heapCount, startIndex);
            while (heapCount > 0)
            {
                int current = Pop(nodes, frontier, ref heapCount);
                HexSearchNode currentNode = nodes[current];
                currentNode.State = 2;
                nodes[current] = currentNode;
                for (int direction = 0; direction < 6; direction++)
                {
                    if (!map.TryGetIndex(map.Cells[current].Neighbor(direction), out int nextIndex) ||
                        map.Costs[nextIndex] < 0) continue;
                    long cost = currentNode.Cost + map.Costs[nextIndex];
                    if (cost > budget) continue;
                    HexSearchNode nextNode = nodes[nextIndex];
                    if (nextNode.State != 0 && cost >= nextNode.Cost) continue;
                    byte previousState = nextNode.State;
                    nextNode.Cost = cost;
                    nextNode.Priority = cost;
                    nextNode.Parent = current;
                    nextNode.State = 1;
                    nodes[nextIndex] = nextNode;
                    if (previousState == 1) SiftUp(nodes, frontier, nextNode.HeapIndex);
                    else Push(nodes, frontier, ref heapCount, nextIndex);
                }
            }
            return WriteReachable(map, nodes, output);
        }

        /// <summary>Breadth-first movement range. Each traversable step costs one regardless of tile weight.</summary>
        public static HexSearchResult ReachableSteps(HexMap map, HexCoord start, int maxSteps,
            NativeArray<HexSearchNode> nodes, NativeArray<int> frontier, NativeArray<HexCoord> output)
        {
            if (maxSteps < 0 || !output.IsCreated) return new HexSearchResult(HexSearchStatus.InvalidInput);
            HexSearchStatus status = Validate(map, start, nodes, frontier, out int startIndex);
            if (status != HexSearchStatus.Success) return new HexSearchResult(status);
            Initialize(map, nodes);
            SetStart(nodes, startIndex, 0);
            int head = 0;
            int tail = 1;
            frontier[0] = startIndex;
            while (head < tail)
            {
                int current = frontier[head++];
                if (nodes[current].Cost >= maxSteps) continue;
                for (int direction = 0; direction < 6; direction++)
                {
                    if (!map.TryGetIndex(map.Cells[current].Neighbor(direction), out int nextIndex) ||
                        map.Costs[nextIndex] < 0 || nodes[nextIndex].State != 0) continue;
                    nodes[nextIndex] = new HexSearchNode
                    {
                        Cost = nodes[current].Cost + 1,
                        Parent = current,
                        HeapIndex = -1,
                        State = 1
                    };
                    frontier[tail++] = nextIndex;
                }
            }
            return WriteReachable(map, nodes, output);
        }

        /// <summary>Center-to-center nudged ray. Missing cells occlude. The first wall is visible by default.</summary>
        /// <remarks>This samples one cell per line step; it is not a supercover or partial-cell visibility test.</remarks>
        public static bool HasLineOfSight(HexMap map, HexCoord from, HexCoord to,
            bool includeBlockingTarget = true)
        {
            if (!map.IsValid || !map.TryGetIndex(from, out _) || !map.TryGetIndex(to, out int target))
                return false;
            if (from == to) return true;
            if (!includeBlockingTarget && map.Costs[target] < 0) return false;
            foreach (HexCoord cell in new HexLine(from, to))
            {
                if (cell == from || cell == to) continue;
                if (!map.TryGetIndex(cell, out int index) || map.Costs[index] < 0) return false;
            }
            return true;
        }

        /// <summary>Simple ray-to-each-cell FOV, in map order. The origin is included and missing cells occlude.</summary>
        /// <remarks>Output is untouched on insufficient capacity. Complexity is O(cells * radius).
        /// A full-map-sized output avoids a second ray pass used for short-buffer preflight.</remarks>
        public static HexSearchResult FieldOfView(HexMap map, HexCoord origin, int radius,
            NativeArray<HexCoord> output, bool includeBlockingTargets = true)
        {
            if (!map.IsValid || radius < 0 || !output.IsCreated || !map.TryGetIndex(origin, out _))
                return new HexSearchResult(HexSearchStatus.InvalidInput);
            if (output.Length < map.Cells.Length)
            {
                int required = 0;
                for (int i = 0; i < map.Cells.Length; i++)
                    if (origin.DistanceTo(map.Cells[i]) <= radius &&
                        HasLineOfSight(map, origin, map.Cells[i], includeBlockingTargets)) required++;
                if (output.Length < required) return new HexSearchResult(HexSearchStatus.BufferTooSmall, required);
            }
            int count = 0;
            for (int i = 0; i < map.Cells.Length; i++)
                if (origin.DistanceTo(map.Cells[i]) <= radius &&
                    HasLineOfSight(map, origin, map.Cells[i], includeBlockingTargets)) output[count++] = map.Cells[i];
            return new HexSearchResult(HexSearchStatus.Success, count);
        }

        private static HexSearchStatus Validate(HexMap map, HexCoord start,
            NativeArray<HexSearchNode> nodes, NativeArray<int> frontier, out int startIndex)
        {
            startIndex = -1;
            if (!map.IsValid || !nodes.IsCreated || !frontier.IsCreated ||
                !map.TryGetIndex(start, out startIndex)) return HexSearchStatus.InvalidInput;
            if (nodes.Length < map.Cells.Length || frontier.Length < map.Cells.Length)
                return HexSearchStatus.BufferTooSmall;
            return map.Costs[startIndex] < 0 ? HexSearchStatus.NoPath : HexSearchStatus.Success;
        }

        private static int Initialize(HexMap map, NativeArray<HexSearchNode> nodes)
        {
            int minimum = int.MaxValue;
            for (int i = 0; i < map.Cells.Length; i++)
            {
                nodes[i] = new HexSearchNode { Parent = -1, HeapIndex = -1 };
                if (map.Costs[i] >= 0) minimum = math.min(minimum, map.Costs[i]);
            }
            return minimum == int.MaxValue ? 0 : minimum;
        }

        private static void SetStart(NativeArray<HexSearchNode> nodes, int start, long priority)
        {
            nodes[start] = new HexSearchNode { Parent = -1, HeapIndex = -1, Priority = priority, State = 1 };
        }

        private static HexSearchResult WritePath(HexMap map, NativeArray<HexSearchNode> nodes,
            int start, int goal, NativeArray<HexCoord> path)
        {
            int count = 1;
            for (int current = goal; current != start; current = nodes[current].Parent) count++;
            if (path.Length < count) return new HexSearchResult(HexSearchStatus.BufferTooSmall, count);
            int index = count - 1;
            for (int current = goal; current != start; current = nodes[current].Parent)
                path[index--] = map.Cells[current];
            path[0] = map.Cells[start];
            return new HexSearchResult(HexSearchStatus.Success, count, nodes[goal].Cost);
        }

        private static HexSearchResult WriteReachable(HexMap map, NativeArray<HexSearchNode> nodes,
            NativeArray<HexCoord> output)
        {
            int required = 0;
            for (int i = 0; i < map.Cells.Length; i++) if (nodes[i].State != 0) required++;
            if (output.Length < required) return new HexSearchResult(HexSearchStatus.BufferTooSmall, required);
            int count = 0;
            for (int i = 0; i < map.Cells.Length; i++) if (nodes[i].State != 0) output[count++] = map.Cells[i];
            return new HexSearchResult(HexSearchStatus.Success, count);
        }

        private static bool Before(NativeArray<HexSearchNode> nodes, int a, int b)
        {
            long pa = nodes[a].Priority;
            long pb = nodes[b].Priority;
            return pa < pb || (pa == pb && a < b);
        }

        private static void Push(NativeArray<HexSearchNode> nodes, NativeArray<int> heap, ref int count, int cell)
        {
            heap[count] = cell;
            HexSearchNode node = nodes[cell];
            node.HeapIndex = count;
            nodes[cell] = node;
            SiftUp(nodes, heap, count++);
        }

        private static void SiftUp(NativeArray<HexSearchNode> nodes, NativeArray<int> heap, int position)
        {
            while (position > 0)
            {
                int parent = (position - 1) >> 1;
                if (!Before(nodes, heap[position], heap[parent])) break;
                Swap(nodes, heap, position, parent);
                position = parent;
            }
        }

        private static int Pop(NativeArray<HexSearchNode> nodes, NativeArray<int> heap, ref int count)
        {
            int result = heap[0];
            count--;
            if (count > 0)
            {
                heap[0] = heap[count];
                HexSearchNode moved = nodes[heap[0]];
                moved.HeapIndex = 0;
                nodes[heap[0]] = moved;
                int position = 0;
                while (position < count / 2)
                {
                    int left = position * 2 + 1;
                    if (left >= count) break;
                    int right = left + 1;
                    int best = right < count && Before(nodes, heap[right], heap[left]) ? right : left;
                    if (!Before(nodes, heap[best], heap[position])) break;
                    Swap(nodes, heap, position, best);
                    position = best;
                }
            }
            HexSearchNode node = nodes[result];
            node.HeapIndex = -1;
            nodes[result] = node;
            return result;
        }

        private static void Swap(NativeArray<HexSearchNode> nodes, NativeArray<int> heap, int a, int b)
        {
            int cellA = heap[a];
            int cellB = heap[b];
            heap[a] = cellB;
            heap[b] = cellA;
            HexSearchNode nodeA = nodes[cellA];
            HexSearchNode nodeB = nodes[cellB];
            nodeA.HeapIndex = b;
            nodeB.HeapIndex = a;
            nodes[cellA] = nodeA;
            nodes[cellB] = nodeB;
        }
    }
}
