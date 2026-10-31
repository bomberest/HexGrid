using NUnit.Framework;
using Unity.Mathematics;

namespace Anvelop.HexGrid.Tests
{
    public class HexGeometryTests
    {
        [Test]
        public void CubeImportChecksPlaneWithoutOverflow()
        {
            Assert.That(HexCoord.TryFromCube(new int3(3, -8, 5), out HexCoord coordinate), Is.True);
            Assert.That(coordinate, Is.EqualTo(new HexCoord(3, -8)));
            Assert.That(coordinate.Cube, Is.EqualTo(new int3(3, -8, 5)));
            Assert.That(HexCoord.TryFromCube(new int3(3, -8, 4), out _), Is.False);
            Assert.That(HexCoord.TryFromCube(new int3(int.MaxValue, int.MaxValue, 2), out _), Is.False);
        }

        [Test]
        public void NeighborsAndDiagonalsHaveExpectedDistancesAndOpposites()
        {
            HexCoord origin = new HexCoord(-12, 7);
            for (int direction = -12; direction <= 12; direction++)
            {
                HexCoord neighbor = origin.Neighbor(direction);
                Assert.That(origin.DistanceTo(neighbor), Is.EqualTo(1));
                Assert.That(origin.EuclideanDistanceTo(neighbor), Is.EqualTo(1f).Within(0.00001f));
                Assert.That(neighbor.Neighbor(direction + 3), Is.EqualTo(origin));
                Assert.That(origin.DistanceTo(origin.DiagonalNeighbor(direction)), Is.EqualTo(2));
                Assert.That(HexCoord.DiagonalDirection(direction),
                    Is.EqualTo(HexCoord.Direction(direction) + HexCoord.Direction(direction + 1)));
            }
            Assert.That(HexCoord.Direction(0), Is.EqualTo(new HexCoord(1, 0)));
            Assert.That(HexCoord.Direction(1), Is.EqualTo(new HexCoord(1, -1)));
        }

        [Test]
        public void DistanceMatchesKnownExamplesAndTriangleInequality()
        {
            Assert.That(new HexCoord(3, -7).Length, Is.EqualTo(7));
            Assert.That(new HexCoord(1, -3).DistanceTo(new HexCoord(5, -1)), Is.EqualTo(6));
            Assert.That(new HexCoord(100000000, -100000000).EuclideanDistanceTo(new HexCoord(100000001, -100000000)), Is.EqualTo(1f));
            HexCoord a = new HexCoord(4, -7);
            HexCoord b = new HexCoord(-9, 2);
            HexCoord c = new HexCoord(11, 12);
            Assert.That(a.DistanceTo(b), Is.EqualTo(b.DistanceTo(a)));
            Assert.That(a.DistanceTo(c), Is.LessThanOrEqualTo(a.DistanceTo(b) + b.DistanceTo(c)));
            Assert.That((new HexCoord(2, -4) / 2f).Round(), Is.EqualTo(new HexCoord(1, -2)));
        }

        [Test]
        public void RotationsAndReflectionsPreserveDistanceAndAreReversible()
        {
            HexCoord coordinate = new HexCoord(3, -8);
            HexCoord center = new HexCoord(-7, 2);
            Assert.That(coordinate.RotateClockwise(), Is.EqualTo(new HexCoord(8, -5)));
            Assert.That(coordinate.RotateCounterClockwise(), Is.EqualTo(new HexCoord(-5, -3)));
            for (int steps = -18; steps <= 18; steps++)
            {
                Assert.That(coordinate.RotateClockwise(steps).RotateCounterClockwise(steps), Is.EqualTo(coordinate));
                Assert.That(coordinate.RotateClockwise(steps).Length, Is.EqualTo(coordinate.Length));
                Assert.That(coordinate.RotateAround(center, steps).DistanceTo(center), Is.EqualTo(coordinate.DistanceTo(center)));
            }
            Assert.That(coordinate.ReflectQ().ReflectQ(), Is.EqualTo(coordinate));
            Assert.That(coordinate.ReflectR().ReflectR(), Is.EqualTo(coordinate));
            Assert.That(coordinate.ReflectS().ReflectS(), Is.EqualTo(coordinate));
            Assert.That(coordinate.ReflectQ().Q, Is.EqualTo(coordinate.Q));
            Assert.That(coordinate.ReflectR().R, Is.EqualTo(coordinate.R));
            Assert.That(coordinate.ReflectS().S, Is.EqualTo(coordinate.S));
            Assert.That(coordinate.ReflectQ(center).ReflectQ(center), Is.EqualTo(coordinate));
            Assert.That(coordinate.ReflectR(center).ReflectR(center), Is.EqualTo(coordinate));
            Assert.That(coordinate.ReflectS(center).ReflectS(center), Is.EqualTo(coordinate));
        }

        [TestCase(HexOffsetLayout.OddR)]
        [TestCase(HexOffsetLayout.EvenR)]
        [TestCase(HexOffsetLayout.OddQ)]
        [TestCase(HexOffsetLayout.EvenQ)]
        public void OffsetRoundtripsBothParitiesAndNegativeCoordinates(HexOffsetLayout layout)
        {
            for (int q = -12; q <= 12; q++)
            for (int r = -12; r <= 12; r++)
            {
                HexCoord coordinate = new HexCoord(q, r);
                HexOffset offset = HexCoordinates.ToOffset(coordinate, layout);
                Assert.That(HexCoordinates.FromOffset(offset, layout), Is.EqualTo(coordinate));
                for (int direction = 0; direction < 6; direction++)
                {
                    HexOffset neighbor = HexCoordinates.OffsetNeighbor(offset, layout, direction);
                    Assert.That(HexCoordinates.FromOffset(neighbor, layout), Is.EqualTo(coordinate.Neighbor(direction)));
                    Assert.That(HexCoordinates.OffsetDistance(offset, neighbor, layout), Is.EqualTo(1));
                }
            }
        }

        [Test]
        public void OffsetKnownNegativeValuesUseFloorParity()
        {
            Assert.That(HexCoordinates.ToOffset(new HexCoord(0, -1), HexOffsetLayout.OddR), Is.EqualTo(new HexOffset(-1, -1)));
            Assert.That(HexCoordinates.ToOffset(new HexCoord(0, -1), HexOffsetLayout.EvenR), Is.EqualTo(new HexOffset(0, -1)));
            Assert.That(HexCoordinates.ToOffset(new HexCoord(-1, 0), HexOffsetLayout.OddQ), Is.EqualTo(new HexOffset(-1, -1)));
            Assert.That(HexCoordinates.ToOffset(new HexCoord(-1, 0), HexOffsetLayout.EvenQ), Is.EqualTo(new HexOffset(-1, 0)));
        }

        [TestCase(HexDoubledLayout.DoubleWidth)]
        [TestCase(HexDoubledLayout.DoubleHeight)]
        public void DoubledRoundtripsAndDirectDistancesMatchAxial(HexDoubledLayout layout)
        {
            HexCoord reference = new HexCoord(4, -7);
            HexDoubled doubledReference = HexCoordinates.ToDoubled(reference, layout);
            for (int q = -12; q <= 12; q++)
            for (int r = -12; r <= 12; r++)
            {
                HexCoord coordinate = new HexCoord(q, r);
                HexDoubled doubled = HexCoordinates.ToDoubled(coordinate, layout);
                Assert.That(doubled.IsValid, Is.True);
                Assert.That(HexCoordinates.TryFromDoubled(doubled, layout, out HexCoord result), Is.True);
                Assert.That(result, Is.EqualTo(coordinate));
                Assert.That(HexCoordinates.DoubledDistance(doubled, doubledReference, layout), Is.EqualTo(coordinate.DistanceTo(reference)));
                for (int direction = 0; direction < 6; direction++)
                    Assert.That(HexCoordinates.FromDoubled(HexCoordinates.DoubledNeighbor(doubled, layout, direction), layout), Is.EqualTo(coordinate.Neighbor(direction)));
            }
            Assert.That(HexCoordinates.TryFromDoubled(new HexDoubled(-2, 1), layout, out _), Is.False);
            Assert.That(HexCoordinates.TryFromDoubled(new HexDoubled(0, 0), (HexDoubledLayout)99, out _), Is.False);
        }

        [Test]
        public void RoundingSelectsNearestCellAndLerpPreservesEndpoints()
        {
            Assert.That(new HexFractional(0.1f, 0.1f).Round(), Is.EqualTo(new HexCoord(0, 0)));
            Assert.That(new HexFractional(0.6f, -0.2f).Round(), Is.EqualTo(new HexCoord(1, 0)));
            Assert.That(new HexFractional(0.4f, 0.4f).Round(), Is.EqualTo(new HexCoord(0, 1)));
            Assert.That(new HexFractional(-0.6f, 0.2f).Round(), Is.EqualTo(new HexCoord(-1, 0)));
            HexCoord a = new HexCoord(-12, 4);
            HexCoord b = new HexCoord(9, -7);
            Assert.That(HexFractional.Lerp(a, b, 0f).Round(), Is.EqualTo(a));
            Assert.That(HexFractional.Lerp(a, b, 1f).Round(), Is.EqualTo(b));
            for (int q = -5; q <= 5; q++)
            for (int r = -5; r <= 5; r++)
            {
                HexFractional sample = new HexFractional(q * 0.31f, r * 0.27f);
                HexCoord rounded = sample.Round();
                float best = SquaredMetric(sample, rounded);
                for (int direction = 0; direction < 6; direction++)
                    Assert.That(best, Is.LessThanOrEqualTo(SquaredMetric(sample, rounded.Neighbor(direction)) + 0.00001f));
            }
        }

        [TestCase(HexOrientation.Pointy)]
        [TestCase(HexOrientation.Flat)]
        public void LayoutRoundtripsWithOriginNonuniformScaleAndMirroring(HexOrientation orientation)
        {
            HexLayout layout = new HexLayout(orientation, new float2(-7f, 11f), new float2(123f, -341f));
            Assert.That(layout.IsValid, Is.True);
            for (int q = -15; q <= 15; q++)
            for (int r = -15; r <= 15; r++)
            {
                HexCoord coordinate = new HexCoord(q, r);
                Assert.That(layout.FromPixel(layout.ToPixel(coordinate)), Is.EqualTo(coordinate));
                Assert.That(layout.FromWorldXY(layout.ToWorldXY(coordinate, 7f)), Is.EqualTo(coordinate));
                Assert.That(layout.FromWorldXZ(layout.ToWorldXZ(coordinate, -4f)), Is.EqualTo(coordinate));
                HexOffset offset = HexCoordinates.ToOffset(coordinate, HexOffsetLayout.EvenR);
                HexDoubled doubled = HexCoordinates.ToDoubled(coordinate, HexDoubledLayout.DoubleHeight);
                Assert.That(math.distance(layout.ToPixel(offset, HexOffsetLayout.EvenR), layout.ToPixel(coordinate)), Is.LessThan(0.00001f));
                Assert.That(math.distance(layout.ToPixel(doubled, HexDoubledLayout.DoubleHeight), layout.ToPixel(coordinate)), Is.LessThan(0.00001f));
                Assert.That(layout.PixelToOffset(layout.ToPixel(coordinate), HexOffsetLayout.EvenR), Is.EqualTo(offset));
                Assert.That(layout.PixelToDoubled(layout.ToPixel(coordinate), HexDoubledLayout.DoubleHeight), Is.EqualTo(doubled));
            }
        }

        [TestCase(HexOrientation.Pointy)]
        [TestCase(HexOrientation.Flat)]
        public void GeometryCornersHaveUnitRadiusAndExpectedBoundingBox(HexOrientation orientation)
        {
            HexLayout layout = new HexLayout(orientation, new float2(1f));
            float2 minimum = new float2(float.PositiveInfinity);
            float2 maximum = new float2(float.NegativeInfinity);
            HexCoord coordinate = new HexCoord(2, -3);
            for (int corner = 0; corner < 6; corner++)
            {
                float2 offset = layout.CornerOffset(corner);
                Assert.That(math.length(offset), Is.EqualTo(1f).Within(0.00001f));
                Assert.That(math.length(offset + layout.CornerOffset(corner + 3)), Is.LessThan(0.00001f));
                Assert.That(math.distance(layout.Corner(coordinate, corner), layout.ToPixel(coordinate) + offset), Is.LessThan(0.00001f));
                minimum = math.min(minimum, offset);
                maximum = math.max(maximum, offset);
            }
            Assert.That(math.distance(maximum - minimum, layout.Dimensions), Is.LessThan(0.00001f));
            Assert.That(new HexLayout(orientation, new float2(0f, 1f)).IsValid, Is.False);
            Assert.That(default(HexLayout).IsValid, Is.False);
        }

        private static float SquaredMetric(HexFractional sample, HexCoord candidate)
        {
            float dq = sample.Q - candidate.Q;
            float dr = sample.R - candidate.R;
            return dq * dq + dr * dr + dq * dr;
        }
    }
}
