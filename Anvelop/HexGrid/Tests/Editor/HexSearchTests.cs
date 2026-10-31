using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace Anvelop.HexGrid.Tests
{
    public class HexSearchTests
    {
        private NativeArray<HexCoord> cells;
        private NativeArray<int> costs;
        private NativeParallelHashMap<HexCoord, int> indices;
        private NativeArray<HexSearchNode> nodes;
        private NativeArray<int> frontier;
        private NativeArray<HexCoord> output;
        private HexMap Map => new HexMap(cells, costs, indices);

        [TearDown]
        public void Dispose()
        {
            if (cells.IsCreated) cells.Dispose();
            if (costs.IsCreated) costs.Dispose();
            if (indices.IsCreated) indices.Dispose();
            if (nodes.IsCreated) nodes.Dispose();
            if (frontier.IsCreated) frontier.Dispose();
            if (output.IsCreated) output.Dispose();
        }

        private void CreateMap(HexCoord[] coordinates, int[] weights)
        {
            cells = new NativeArray<HexCoord>(coordinates, Allocator.TempJob);
            costs = new NativeArray<int>(weights, Allocator.TempJob);
            indices = new NativeParallelHashMap<HexCoord, int>(coordinates.Length, Allocator.TempJob);
            nodes = new NativeArray<HexSearchNode>(coordinates.Length, Allocator.TempJob);
            frontier = new NativeArray<int>(coordinates.Length, Allocator.TempJob);
            output = new NativeArray<HexCoord>(coordinates.Length, Allocator.TempJob);
            for (int i = 0; i < coordinates.Length; i++) indices.Add(coordinates[i], i);
        }

        private void CreateDetour()
        {
            CreateMap(new[] { new HexCoord(0, 0), new HexCoord(1, 0), new HexCoord(2, 0),
                new HexCoord(0, 1), new HexCoord(1, 1) }, new[] { 99, 20, 1, 1, 1 });
        }

        [Test]
        public void WeightedPathChoosesCheaperLongerRouteAndOmitsStartCost()
        {
            CreateDetour();
            HexSearchResult result = HexSearch.FindPath(Map, cells[0], cells[2], nodes, frontier, output);
            Assert.That(result.Status, Is.EqualTo(HexSearchStatus.Success));
            Assert.That(result.Cost, Is.EqualTo(3));
            Assert.That(result.Count, Is.EqualTo(4));
            Assert.That(output[0], Is.EqualTo(cells[0]));
            Assert.That(output[3], Is.EqualTo(cells[2]));
            for (int i = 1; i < result.Count; i++) Assert.That(output[i - 1].DistanceTo(output[i]), Is.EqualTo(1));
        }

        [Test]
        public void ReachableDistinguishesEntryBudgetFromUniformSteps()
        {
            CreateDetour();
            HexSearchResult weighted = HexSearch.Reachable(Map, cells[0], 2, nodes, frontier, output);
            Assert.That(weighted.Status, Is.EqualTo(HexSearchStatus.Success));
            Assert.That(weighted.Count, Is.EqualTo(3));
            Assert.That(nodes[1].State, Is.Zero);
            Assert.That(nodes[4].Cost, Is.EqualTo(2));
            HexSearchResult steps = HexSearch.ReachableSteps(Map, cells[0], 1, nodes, frontier, output);
            Assert.That(steps.Status, Is.EqualTo(HexSearchStatus.Success));
            Assert.That(steps.Count, Is.EqualTo(3));
            Assert.That(nodes[1].Cost, Is.EqualTo(1));
            Assert.That(nodes[4].State, Is.Zero);
        }

        [Test]
        public void ZeroCostMovesWorkWithZeroBudget()
        {
            CreateMap(new[] { new HexCoord(0, 0), new HexCoord(1, 0), new HexCoord(2, 0) }, new[] { 5, 0, 0 });
            HexSearchResult result = HexSearch.Reachable(Map, cells[0], 0, nodes, frontier, output);
            Assert.That(result.Count, Is.EqualTo(3));
            result = HexSearch.FindPath(Map, cells[0], cells[2], nodes, frontier, output);
            Assert.That(result.Cost, Is.Zero);
            Assert.That(result.Count, Is.EqualTo(3));
        }

        [Test]
        public void LargeWeightsUseLongCosts()
        {
            CreateMap(new[] { new HexCoord(0, 0), new HexCoord(1, 0), new HexCoord(2, 0) },
                new[] { int.MaxValue, int.MaxValue, int.MaxValue });
            HexSearchResult result = HexSearch.FindPath(Map, cells[0], cells[2], nodes, frontier, output);
            Assert.That(result.Status, Is.EqualTo(HexSearchStatus.Success));
            Assert.That(result.Cost, Is.EqualTo(2L * int.MaxValue));
        }

        [Test]
        public void WallsAndHolesPreventPaths()
        {
            CreateMap(new[] { new HexCoord(0, 0), new HexCoord(1, 0), new HexCoord(2, 0), new HexCoord(8, 0) },
                new[] { 1, -1, 1, 1 });
            Assert.That(HexSearch.FindPath(Map, cells[0], cells[2], nodes, frontier, output).Status,
                Is.EqualTo(HexSearchStatus.NoPath));
            Assert.That(HexSearch.FindPath(Map, cells[2], cells[3], nodes, frontier, output).Status,
                Is.EqualTo(HexSearchStatus.NoPath));
            Assert.That(HexSearch.FindPath(Map, cells[1], cells[2], nodes, frontier, output).Status,
                Is.EqualTo(HexSearchStatus.NoPath));
        }

        [Test]
        public void ShortOutputReportsRequiredCapacityWithoutWriting()
        {
            CreateDetour();
            using (var shortOutput = new NativeArray<HexCoord>(1, Allocator.Temp))
            {
                var sentinel = new HexCoord(37, -5);
                var writableOutput = shortOutput;
                writableOutput[0] = sentinel;
                HexSearchResult path = HexSearch.FindPath(Map, cells[0], cells[2], nodes, frontier, shortOutput);
                Assert.That(path.Status, Is.EqualTo(HexSearchStatus.BufferTooSmall));
                Assert.That(path.Count, Is.EqualTo(4));
                Assert.That(shortOutput[0], Is.EqualTo(sentinel));
                HexSearchResult range = HexSearch.Reachable(Map, cells[0], 2, nodes, frontier, shortOutput);
                Assert.That(range.Status, Is.EqualTo(HexSearchStatus.BufferTooSmall));
                Assert.That(range.Count, Is.EqualTo(3));
                Assert.That(shortOutput[0], Is.EqualTo(sentinel));
                HexSearchResult view = HexSearch.FieldOfView(Map, cells[0], 3, shortOutput);
                Assert.That(view.Status, Is.EqualTo(HexSearchStatus.BufferTooSmall));
                Assert.That(view.Count, Is.EqualTo(5));
                Assert.That(shortOutput[0], Is.EqualTo(sentinel));
            }
        }

        [Test]
        public void InvalidInputAndShortScratchAreReported()
        {
            CreateDetour();
            Assert.That(HexSearch.FindPath(default, cells[0], cells[2], nodes, frontier, output).Status,
                Is.EqualTo(HexSearchStatus.InvalidInput));
            Assert.That(HexSearch.Reachable(Map, cells[0], -1, nodes, frontier, output).Status,
                Is.EqualTo(HexSearchStatus.InvalidInput));
            Assert.That(HexSearch.FindPath(Map, cells[0], new HexCoord(900, 0), nodes, frontier, output).Status,
                Is.EqualTo(HexSearchStatus.InvalidInput));
            using (var shortFrontier = new NativeArray<int>(1, Allocator.Temp))
                Assert.That(HexSearch.FindPath(Map, cells[0], cells[2], nodes, shortFrontier, output).Status,
                    Is.EqualTo(HexSearchStatus.BufferTooSmall));
        }

        [Test]
        public void SameStartAndGoalHasOneCellAndNoCost()
        {
            CreateDetour();
            HexSearchResult result = HexSearch.FindPath(Map, cells[0], cells[0], nodes, frontier, output);
            Assert.That(result.Status, Is.EqualTo(HexSearchStatus.Success));
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result.Cost, Is.Zero);
        }

        [Test]
        public void VisibilityIncludesFirstWallAndOccludesBeyondIt()
        {
            CreateMap(new[] { new HexCoord(0, 0), new HexCoord(1, 0), new HexCoord(2, 0) }, new[] { 1, -1, 1 });
            Assert.That(HexSearch.HasLineOfSight(Map, cells[0], cells[1]), Is.True);
            Assert.That(HexSearch.HasLineOfSight(Map, cells[0], cells[1], false), Is.False);
            Assert.That(HexSearch.HasLineOfSight(Map, cells[0], cells[2]), Is.False);
            HexSearchResult result = HexSearch.FieldOfView(Map, cells[0], 2, output);
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(output[0], Is.EqualTo(cells[0]));
            Assert.That(output[1], Is.EqualTo(cells[1]));
            result = HexSearch.FieldOfView(Map, cells[0], 2, output, false);
            Assert.That(result.Count, Is.EqualTo(1));
        }

        [Test]
        public void MapHolesOccludeVisibility()
        {
            CreateMap(new[] { new HexCoord(0, 0), new HexCoord(2, 0) }, new[] { 1, 1 });
            Assert.That(HexSearch.HasLineOfSight(Map, cells[0], cells[1]), Is.False);
            Assert.That(HexSearch.FieldOfView(Map, cells[0], 5, output).Count, Is.EqualTo(1));
        }

        [Test]
        public void VisibilitySharesLineSamplingAndIsSymmetricAtLargeOrigins()
        {
            var coordinates = new HexCoord[81];
            var weights = new int[81];
            var center = new HexCoord(100000000, -100000000);
            for (int row = 0; row < 9; row++)
                for (int column = 0; column < 9; column++)
                {
                    int index = row * 9 + column;
                    coordinates[index] = center + new HexCoord(column - 4, row - 4);
                    weights[index] = 1;
                }
            CreateMap(coordinates, weights);
            HexCoord from = center + new HexCoord(-3, -1);
            HexCoord to = center + new HexCoord(3, 1);
            Assert.That(HexSearch.HasLineOfSight(Map, from, to), Is.True);
            Assert.That(HexSearch.HasLineOfSight(Map, to, from), Is.True);
            foreach (HexCoord cell in new HexLine(from, to))
            {
                if (cell == from || cell == to) continue;
                Assert.That(Map.TryGetIndex(cell, out int index), Is.True);
                costs[index] = -1;
                Assert.That(HexSearch.HasLineOfSight(Map, from, to), Is.False);
                Assert.That(HexSearch.HasLineOfSight(Map, to, from), Is.False);
                costs[index] = 1;
            }
        }

        [Test]
        public void AStarMatchesIndependentRelaxationOracleAcrossWeightedMaps()
        {
            var coordinates = new HexCoord[49];
            var weights = new int[49];
            for (int row = 0; row < 7; row++)
                for (int column = 0; column < 7; column++)
                    coordinates[row * 7 + column] = new HexCoord(column - 3, row - 3);
            CreateMap(coordinates, weights);
            var random = new Random(7319);
            var expected = new long[49];
            for (int trial = 0; trial < 30; trial++)
            {
                for (int i = 0; i < costs.Length; i++) costs[i] = random.Next(-1, 12);
                costs[0] = 1;
                costs[48] = 1;
                for (int i = 0; i < expected.Length; i++) expected[i] = long.MaxValue;
                expected[0] = 0;
                // Bellman-Ford-style edge relaxation has no dependency on the production heap or heuristic.
                for (int pass = 0; pass < 48; pass++)
                {
                    bool changed = false;
                    for (int from = 0; from < cells.Length; from++)
                    {
                        if (expected[from] == long.MaxValue || costs[from] < 0) continue;
                        for (int to = 0; to < cells.Length; to++)
                        {
                            if (costs[to] < 0 || cells[from].DistanceTo(cells[to]) != 1) continue;
                            long candidate = expected[from] + costs[to];
                            if (candidate >= expected[to]) continue;
                            expected[to] = candidate;
                            changed = true;
                        }
                    }
                    if (!changed) break;
                }
                HexSearchResult actual = HexSearch.FindPath(Map, cells[0], cells[48], nodes, frontier, output);
                if (expected[48] == long.MaxValue)
                    Assert.That(actual.Status, Is.EqualTo(HexSearchStatus.NoPath));
                else
                {
                    Assert.That(actual.Status, Is.EqualTo(HexSearchStatus.Success));
                    Assert.That(actual.Cost, Is.EqualTo(expected[48]));
                }
                HexSearchResult range = HexSearch.Reachable(Map, cells[0], 9, nodes, frontier, output);
                int expectedCount = 0;
                for (int i = 0; i < expected.Length; i++) if (expected[i] <= 9) expectedCount++;
                Assert.That(range.Count, Is.EqualTo(expectedCount));
            }
        }

        [Test]
        public void RepeatedOperationsCreateNoManagedAllocations()
        {
            CreateDetour();
            for (int i = 0; i < 10; i++) RunOperations();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) RunOperations();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }

        private void RunOperations()
        {
            HexSearch.FindPath(Map, cells[0], cells[2], nodes, frontier, output);
            HexSearch.Reachable(Map, cells[0], 3, nodes, frontier, output);
            HexSearch.ReachableSteps(Map, cells[0], 2, nodes, frontier, output);
            HexSearch.HasLineOfSight(Map, cells[0], cells[2]);
            HexSearch.FieldOfView(Map, cells[0], 3, output);
        }

        [Test]
        public void SearchAndVisibilityExecuteInBurstJob()
        {
            CreateDetour();
            Assert.That(BurstCompiler.IsEnabled, Is.True, "Enable Burst compilation to validate this contract.");
            using (var result = new NativeArray<int>(6, Allocator.TempJob))
            {
                new SearchJob { Map = Map, Nodes = nodes, Frontier = frontier, Output = output, Results = result }
                    .Schedule().Complete();
                Assert.That(result[0], Is.EqualTo(3));
                Assert.That(result[1], Is.EqualTo(4));
                Assert.That(result[2], Is.EqualTo(3));
                Assert.That(result[3], Is.EqualTo(1));
                Assert.That(result[4], Is.EqualTo(5));
                Assert.That(result[5], Is.EqualTo(1), "The job must run compiled Burst code, not managed fallback.");
            }
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct SearchJob : IJob
        {
            [ReadOnly] public HexMap Map;
            public NativeArray<HexSearchNode> Nodes;
            public NativeArray<int> Frontier;
            public NativeArray<HexCoord> Output;
            public NativeArray<int> Results;

            public void Execute()
            {
                int burstMarker = 1;
                MarkManaged(ref burstMarker);
                Results[5] = burstMarker;
                HexSearchResult path = HexSearch.FindPath(Map, Map.Cells[0], Map.Cells[2], Nodes, Frontier, Output);
                Results[0] = (int)path.Cost;
                Results[1] = path.Count;
                Results[2] = HexSearch.Reachable(Map, Map.Cells[0], 2, Nodes, Frontier, Output).Count;
                Results[3] = HexSearch.HasLineOfSight(Map, Map.Cells[0], Map.Cells[2]) ? 1 : 0;
                Results[4] = HexSearch.FieldOfView(Map, Map.Cells[0], 3, Output).Count;
                HexSearch.ReachableSteps(Map, Map.Cells[0], 2, Nodes, Frontier, Output);
            }

            [BurstDiscard]
            private static void MarkManaged(ref int marker) { marker = 0; }
        }
    }
}
