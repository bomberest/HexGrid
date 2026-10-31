using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Anvelop.HexGrid.Tests
{
    public class HexBurstTests
    {
        [Test]
        public void GeometryRegionsStorageAndWrappingRunInBurst()
        {
            Assert.That(BurstCompiler.IsEnabled, Is.True);
            using (var result = new NativeArray<int>(2, Allocator.TempJob))
            {
                new GeometryJob { Result = result }.Schedule().Complete();
                Assert.That(result[0], Is.EqualTo(Calculate()));
                Assert.That(result[1], Is.EqualTo(1), "BurstDiscard must prove native execution.");
            }
        }

        [Test]
        public void RepeatedGeometryQueriesAllocateNoManagedMemory()
        {
            int sum = Calculate();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) sum += Calculate();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(sum, Is.Not.Zero);
        }

        private static int Calculate()
        {
            var hex = new HexCoord(7, -3);
            int checksum = hex.DistanceTo(hex.Neighbor(2)) + hex.DiagonalNeighbor(1).Length;
            checksum += hex.RotateClockwise(2).RotateCounterClockwise(2).Length;
            checksum += hex.RotateAround(new HexCoord(2, 1), 3).Length;
            checksum += hex.ReflectQ().ReflectR().ReflectS().Length;
            checksum += (int)hex.EuclideanDistanceTo(default);
            HexCoord.TryFromCube(hex.Cube, out var cube);
            checksum += cube.GetHashCode() & 127;
            for (int layout = 0; layout < 4; layout++)
                checksum += HexCoordinates.FromOffset(HexCoordinates.ToOffset(hex, (HexOffsetLayout)layout), (HexOffsetLayout)layout).Length;
            for (int layout = 0; layout < 2; layout++)
                checksum += HexCoordinates.FromDoubled(HexCoordinates.ToDoubled(hex, (HexDoubledLayout)layout), (HexDoubledLayout)layout).Length;
            checksum += HexFractional.Lerp(new HexFractional(hex), new HexFractional(default(HexCoord)), 0.5f).Round().Length;
            for (int orientation = 0; orientation < 2; orientation++)
            {
                var layout = new HexLayout((HexOrientation)orientation, new float2(2f, 3f), new float2(4f, -2f));
                checksum += layout.FromPixel(layout.ToPixel(hex)).Length;
                checksum += layout.FromWorldXY(layout.ToWorldXY(hex)).Length;
                checksum += layout.FromWorldXZ(layout.ToWorldXZ(hex)).Length;
                checksum += (int)(layout.Corner(hex, 1).x + layout.Dimensions.x + layout.Spacing.y);
            }
            foreach (HexCoord cell in HexRegion.Around(default, 4).Intersect(HexRegion.Around(hex, 5))) checksum += cell.Length;
            foreach (HexCoord cell in new HexRing(default, 4)) checksum += cell.Length;
            foreach (HexCoord cell in new HexSpiral(default, 4)) checksum += cell.Length;
            foreach (HexCoord cell in new HexLine(hex, -hex)) checksum += cell.Length;
            for (int shape = 0; shape < 5; shape++)
            {
                HexStorage storage;
                switch (shape)
                {
                    case 0: storage = HexStorage.Hexagon(4, hex); break;
                    case 1: storage = HexStorage.Rhombus(4, 5, hex); break;
                    case 2: storage = HexStorage.Rectangle(4, 5, HexOffsetLayout.OddR); break;
                    default: storage = HexStorage.Triangle(5, shape == 3, hex); break;
                }
                for (int i = 0; i < storage.Count; i++)
                {
                    storage.TryGetCoordinate(i, out var coordinate);
                    storage.TryGetIndex(coordinate, out int index);
                    checksum += index;
                }
            }
            checksum += HexWrap.Rectangle(hex, 5, 7, HexOffsetLayout.OddQ).Length;
            checksum += HexWrap.Hexagon(hex + HexWrap.MirrorCenter(4, 2) * 1000, 4).Length;
            return checksum;
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct GeometryJob : IJob
        {
            public NativeArray<int> Result;
            public void Execute()
            {
                Result[0] = Calculate();
                int isBurst = 1;
                MarkManaged(ref isBurst);
                Result[1] = isBurst;
            }
            [BurstDiscard] private static void MarkManaged(ref int value) { value = 0; }
        }
    }
}
