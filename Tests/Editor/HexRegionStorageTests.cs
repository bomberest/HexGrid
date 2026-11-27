using System.Collections.Generic;
using NUnit.Framework;

namespace Anvelop.HexGrid.Tests
{
    public class HexRegionStorageTests
    {
        [Test]
        public void RegionsEnumerateExactlyTheirIntersection()
        {
            HexCoord centerA = new HexCoord(-5, 4);
            HexCoord centerB = new HexCoord(-3, 0);
            HexCoord centerC = new HexCoord(0, 2);
            HexRegion a = HexRegion.Around(centerA, 5);
            HexRegion b = HexRegion.Around(centerB, 4);
            HexRegion c = HexRegion.Around(centerC, 5);
            HexRegion intersection = a.Intersect(b).Intersect(c);
            var actual = new HashSet<HexCoord>();
            foreach (HexCoord cell in intersection)
            {
                Assert.That(actual.Add(cell), Is.True);
                Assert.That(intersection.Contains(cell), Is.True);
            }
            int expectedCount = 0;
            for (int q = -15; q <= 15; q++)
            for (int r = -15; r <= 15; r++)
            {
                HexCoord cell = new HexCoord(q, r);
                bool expected = cell.DistanceTo(centerA) <= 5 && cell.DistanceTo(centerB) <= 4 && cell.DistanceTo(centerC) <= 5;
                Assert.That(intersection.Contains(cell), Is.EqualTo(expected));
                Assert.That(actual.Contains(cell), Is.EqualTo(expected));
                if (expected) expectedCount++;
            }
            Assert.That(intersection.Count, Is.EqualTo(expectedCount));
            Assert.That(a.Intersect(b).Count, Is.EqualTo(b.Intersect(a).Count));
            Assert.That(HexRegion.Around(default, -1).Count, Is.Zero);
            Assert.That(a.Intersect(HexRegion.Around(new HexCoord(100, 100), 2)).Count, Is.Zero);
            Assert.That(new HexRegion(4, 3, 0, 1, 0, 1).Count, Is.Zero);
            Assert.That(new HexRegion(0, 1, 4, 3, 0, 1).Count, Is.Zero);
            Assert.That(new HexRegion(0, 1, 0, 1, 4, 3).Count, Is.Zero);
        }

        [Test]
        public void HexagonalStorageIsDenseAndMatchesDistanceRegion()
        {
            HexCoord center = new HexCoord(-17, 9);
            for (int radius = -1; radius <= 10; radius++)
            {
                HexStorage storage = HexStorage.Hexagon(radius, center);
                int expectedCount = radius < 0 ? 0 : 1 + 3 * radius * (radius + 1);
                Assert.That(storage.Count, Is.EqualTo(expectedCount));
                HashSet<HexCoord> cells = AssertStorageBijection(storage);
                for (int q = -12; q <= 12; q++)
                for (int r = -12; r <= 12; r++)
                {
                    HexCoord cell = center + new HexCoord(q, r);
                    bool expected = radius >= 0 && cell.DistanceTo(center) <= radius;
                    Assert.That(storage.TryGetIndex(cell, out _), Is.EqualTo(expected));
                    Assert.That(cells.Contains(cell), Is.EqualTo(expected));
                }
            }
        }

        [Test]
        public void RhombusStorageIsDenseWithNegativeOriginsAndEmptyDimensions()
        {
            HexCoord origin = new HexCoord(-13, -8);
            for (int width = -1; width <= 7; width++)
            for (int height = -1; height <= 5; height++)
            {
                HexStorage storage = HexStorage.Rhombus(width, height, origin);
                Assert.That(storage.Count, Is.EqualTo(width > 0 && height > 0 ? width * height : 0));
                HashSet<HexCoord> cells = AssertStorageBijection(storage);
                for (int q = -1; q <= 8; q++)
                for (int r = -1; r <= 6; r++)
                {
                    HexCoord cell = origin + new HexCoord(q, r);
                    bool expected = q >= 0 && q < width && r >= 0 && r < height;
                    Assert.That(storage.TryGetIndex(cell, out _), Is.EqualTo(expected));
                    Assert.That(cells.Contains(cell), Is.EqualTo(expected));
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TriangleStorageIsDenseAndMatchesItsHalfPlanes(bool up)
        {
            HexCoord origin = new HexCoord(-13, -8);
            for (int side = -1; side <= 12; side++)
            {
                HexStorage storage = HexStorage.Triangle(side, up, origin);
                Assert.That(storage.Count, Is.EqualTo(side < 0 ? 0 : side * (side + 1) / 2));
                HashSet<HexCoord> cells = AssertStorageBijection(storage);
                for (int q = -1; q <= 13; q++)
                for (int r = -1; r <= 13; r++)
                {
                    HexCoord cell = origin + new HexCoord(q, r);
                    bool expected = q >= 0 && q < side && r >= 0 && r < side
                        && (up ? q + r >= side - 1 : q + r < side);
                    Assert.That(storage.TryGetIndex(cell, out _), Is.EqualTo(expected));
                    Assert.That(cells.Contains(cell), Is.EqualTo(expected));
                }
            }
        }

        [TestCase(HexOffsetLayout.OddR)]
        [TestCase(HexOffsetLayout.EvenR)]
        [TestCase(HexOffsetLayout.OddQ)]
        [TestCase(HexOffsetLayout.EvenQ)]
        public void RectangleStorageIsDenseAcrossNegativeOffsetParities(HexOffsetLayout layout)
        {
            for (int parity = 0; parity < 2; parity++)
            {
                HexOffset origin = new HexOffset(-9 + parity, -8 + parity);
                HexStorage storage = HexStorage.Rectangle(13, 9, layout, origin);
                Assert.That(storage.Count, Is.EqualTo(117));
                HashSet<HexCoord> cells = AssertStorageBijection(storage);
                for (int column = -1; column <= 13; column++)
                for (int row = -1; row <= 9; row++)
                {
                    HexCoord cell = HexCoordinates.FromOffset(new HexOffset(origin.Column + column, origin.Row + row), layout);
                    bool expected = column >= 0 && column < 13 && row >= 0 && row < 9;
                    bool found = storage.TryGetIndex(cell, out int index);
                    Assert.That(found, Is.EqualTo(expected));
                    Assert.That(cells.Contains(cell), Is.EqualTo(expected));
                    if (found) Assert.That(index, Is.EqualTo(row * 13 + column));
                }
            }
            Assert.That(HexStorage.Rectangle(0, 9, layout).Count, Is.Zero);
            Assert.That(HexStorage.Rectangle(13, 0, layout).Count, Is.Zero);
        }

        [Test]
        public void LinesIncludeEndpointsStayAdjacentAndReverseExactly()
        {
            var endpoints = new[]
            {
                new HexCoord(0, 0), new HexCoord(1, 0), new HexCoord(-50, -50),
                new HexCoord(100, 100), new HexCoord(200, 100), new HexCoord(-7, 12),
                new HexCoord(13, -5), new HexCoord(-19, 8)
            };
            foreach (HexCoord from in endpoints)
            foreach (HexCoord to in endpoints)
                AssertLine(from, to);
            HexCoord highOrigin = new HexCoord(100000000, -100000000);
            AssertLine(highOrigin, highOrigin + new HexCoord(100, 100));
            AssertLine(highOrigin + new HexCoord(-7, 12), highOrigin + new HexCoord(13, -5));
        }

        [Test]
        public void RingsAndSpiralsHaveUniqueCellsIncludingZeroAndNegativeRadii()
        {
            HexCoord center = new HexCoord(-17, 9);
            for (int radius = -1; radius <= 12; radius++)
            {
                HexRing ring = new HexRing(center, radius);
                HexSpiral spiral = new HexSpiral(center, radius);
                var ringCells = new HashSet<HexCoord>();
                var spiralCells = new HashSet<HexCoord>();
                HexCoord first = default, previous = default;
                int index = 0;
                foreach (HexCoord cell in ring)
                {
                    if (index++ == 0) first = cell;
                    else Assert.That(previous.DistanceTo(cell), Is.EqualTo(1));
                    previous = cell;
                    Assert.That(center.DistanceTo(cell), Is.EqualTo(radius));
                    Assert.That(ringCells.Add(cell), Is.True);
                }
                if (radius > 0) Assert.That(previous.DistanceTo(first), Is.EqualTo(1));
                Assert.That(ringCells.Count, Is.EqualTo(ring.Count));
                foreach (HexCoord cell in spiral)
                {
                    Assert.That(center.DistanceTo(cell), Is.LessThanOrEqualTo(radius));
                    Assert.That(spiralCells.Add(cell), Is.True);
                }
                Assert.That(spiralCells.Count, Is.EqualTo(spiral.Count));
                Assert.That(spiralCells.Count, Is.EqualTo(HexRegion.Around(center, radius).Count));
                foreach (HexCoord cell in HexRegion.Around(center, radius))
                    Assert.That(spiralCells.Contains(cell), Is.True);
            }
        }

        [Test]
        public void HexagonWrappingIsPeriodicForAllMirrorDirectionsAndDistantCoordinates()
        {
            HexCoord center = new HexCoord(-17, 23);
            int[] periods = { -100000, -7, -1, 0, 1, 11, 100000 };
            for (int radius = 0; radius <= 10; radius++)
            {
                foreach (HexCoord cell in HexRegion.Around(center, radius))
                for (int direction = 0; direction < 6; direction++)
                foreach (int period in periods)
                {
                    HexCoord translated = cell + HexWrap.MirrorCenter(radius, direction) * period;
                    Assert.That(HexWrap.Hexagon(translated, radius, center), Is.EqualTo(cell));
                }
                for (int q = -5; q <= 5; q++)
                for (int r = -5; r <= 5; r++)
                {
                    HexCoord distant = new HexCoord(q * 173811 + 61, r * 193813 - 97);
                    HexCoord wrapped = HexWrap.Hexagon(distant, radius, center);
                    Assert.That(wrapped.DistanceTo(center), Is.LessThanOrEqualTo(radius));
                    Assert.That(HexWrap.Hexagon(wrapped, radius, center), Is.EqualTo(wrapped));
                }
            }
            HexCoord untouched = new HexCoord(-41, 17);
            Assert.That(HexWrap.Hexagon(untouched, -1, center), Is.EqualTo(untouched));
        }

        [TestCase(HexOffsetLayout.OddR)]
        [TestCase(HexOffsetLayout.EvenR)]
        [TestCase(HexOffsetLayout.OddQ)]
        [TestCase(HexOffsetLayout.EvenQ)]
        public void RectangleWrappingWorksIndependentlyOnEachAxis(HexOffsetLayout layout)
        {
            HexOffset origin = new HexOffset(-9, -8);
            for (int column = -21; column <= 21; column++)
            for (int row = -19; row <= 19; row++)
            {
                HexCoord coordinate = HexCoordinates.FromOffset(new HexOffset(origin.Column + column, origin.Row + row), layout);
                HexCoord wrapped = HexWrap.Rectangle(coordinate, 7, 5, layout, origin);
                HexOffset offset = HexCoordinates.ToOffset(wrapped, layout);
                int expectedColumn = origin.Column + ((column % 7) + 7) % 7;
                int expectedRow = origin.Row + ((row % 5) + 5) % 5;
                Assert.That(offset, Is.EqualTo(new HexOffset(expectedColumn, expectedRow)));
                HexOffset horizontal = HexCoordinates.ToOffset(HexWrap.Rectangle(coordinate, 7, 5, layout, origin, true, false), layout);
                Assert.That(horizontal, Is.EqualTo(new HexOffset(expectedColumn, origin.Row + row)));
                HexOffset vertical = HexCoordinates.ToOffset(HexWrap.Rectangle(coordinate, 7, 5, layout, origin, false, true), layout);
                Assert.That(vertical, Is.EqualTo(new HexOffset(origin.Column + column, expectedRow)));
                Assert.That(HexWrap.Rectangle(coordinate, 7, 5, layout, origin, false, false), Is.EqualTo(coordinate));
            }
        }

        private static HashSet<HexCoord> AssertStorageBijection(HexStorage storage)
        {
            var cells = new HashSet<HexCoord>();
            Assert.That(storage.TryGetCoordinate(-1, out _), Is.False);
            Assert.That(storage.TryGetCoordinate(storage.Count, out _), Is.False);
            for (int index = 0; index < storage.Count; index++)
            {
                Assert.That(storage.TryGetCoordinate(index, out HexCoord coordinate), Is.True);
                Assert.That(cells.Add(coordinate), Is.True);
                Assert.That(storage.TryGetIndex(coordinate, out int roundtrip), Is.True);
                Assert.That(roundtrip, Is.EqualTo(index));
            }
            Assert.That(cells.Count, Is.EqualTo(storage.Count));
            return cells;
        }

        private static void AssertLine(HexCoord from, HexCoord to)
        {
            var forward = new List<HexCoord>();
            var backward = new List<HexCoord>();
            HexLine line = new HexLine(from, to);
            foreach (HexCoord cell in line) forward.Add(cell);
            foreach (HexCoord cell in new HexLine(to, from)) backward.Add(cell);
            Assert.That(forward.Count, Is.EqualTo(line.Count));
            Assert.That(forward[0], Is.EqualTo(from));
            Assert.That(forward[forward.Count - 1], Is.EqualTo(to));
            backward.Reverse();
            CollectionAssert.AreEqual(forward, backward);
            for (int index = 0; index < forward.Count; index++)
            {
                Assert.That(from.DistanceTo(forward[index]), Is.EqualTo(index));
                if (index > 0) Assert.That(forward[index - 1].DistanceTo(forward[index]), Is.EqualTo(1));
            }
        }
    }
}
