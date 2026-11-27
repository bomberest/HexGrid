using Unity.Mathematics;

namespace Anvelop.HexGrid
{
    /// <summary>Intersection of inclusive cube-axis intervals, on q+r+s=0.</summary>
    public readonly struct HexRegion
    {
        public readonly int QMin, QMax, RMin, RMax, SMin, SMax;

        public HexRegion(int qMin, int qMax, int rMin, int rMax, int sMin, int sMax)
        {
            QMin = math.max(qMin, -rMax - sMax);
            QMax = math.min(qMax, -rMin - sMin);
            RMin = rMin; RMax = rMax; SMin = sMin; SMax = sMax;
        }

        public static HexRegion Around(HexCoord center, int radius) => radius < 0
            ? new HexRegion(1, 0, 1, 0, 1, 0)
            : new HexRegion(center.Q - radius, center.Q + radius,
                center.R - radius, center.R + radius, center.S - radius, center.S + radius);

        public HexRegion Intersect(HexRegion other) => new HexRegion(
            math.max(QMin, other.QMin), math.min(QMax, other.QMax),
            math.max(RMin, other.RMin), math.min(RMax, other.RMax),
            math.max(SMin, other.SMin), math.min(SMax, other.SMax));

        public bool Contains(HexCoord hex) => hex.Q >= QMin && hex.Q <= QMax
            && hex.R >= RMin && hex.R <= RMax && hex.S >= SMin && hex.S <= SMax;

        public int Count
        {
            get
            {
                int count = 0;
                if (RMin > RMax || SMin > SMax) return 0;
                for (int q = QMin; q <= QMax; q++)
                    count += math.max(0, math.min(RMax, -q - SMin) - math.max(RMin, -q - SMax) + 1);
                return count;
            }
        }

        public Enumerator GetEnumerator() => new Enumerator(this);

        public struct Enumerator
        {
            private readonly HexRegion region;
            private int q, r, rMax;
            private bool started, finished;
            public HexCoord Current => new HexCoord(q, r);
            internal Enumerator(HexRegion region)
            {
                this.region = region;
                q = region.QMin; r = 0; rMax = -1; started = false;
                finished = region.QMin > region.QMax || region.RMin > region.RMax || region.SMin > region.SMax;
            }
            public bool MoveNext()
            {
                if (finished) return false;
                if (started && r < rMax) { r++; return true; }
                if (started) q++;
                started = true;
                while (q <= region.QMax)
                {
                    r = math.max(region.RMin, -q - region.SMax);
                    rMax = math.min(region.RMax, -q - region.SMin);
                    if (r <= rMax) return true;
                    q++;
                }
                finished = true;
                return false;
            }
        }
    }

    /// <summary>One ring, starting at direction 4. Radius zero yields the center once.</summary>
    public readonly struct HexRing
    {
        public readonly HexCoord Center;
        public readonly int Radius;
        public HexRing(HexCoord center, int radius) { Center = center; Radius = radius; }
        public int Count => Radius < 0 ? 0 : Radius == 0 ? 1 : 6 * Radius;
        public Enumerator GetEnumerator() => new Enumerator(Center, Radius);
        public struct Enumerator
        {
            private readonly int radius;
            private HexCoord current;
            private int index, side, step;
            public HexCoord Current => current;
            internal Enumerator(HexCoord center, int radius)
            {
                this.radius = radius; current = radius > 0 ? center + HexCoord.Direction(4) * radius : center;
                index = -1; side = 0; step = 0;
            }
            public bool MoveNext()
            {
                if (radius < 0 || (radius == 0 ? index >= 0 : index >= 6 * radius - 1)) return false;
                if (index >= 0)
                {
                    current = current.Neighbor(side);
                    if (++step == radius) { step = 0; side++; }
                }
                index++;
                return true;
            }
        }
    }

    /// <summary>Center followed by successive rings. Pattern enumeration creates no objects.</summary>
    public readonly struct HexSpiral
    {
        public readonly HexCoord Center;
        public readonly int Radius;
        public HexSpiral(HexCoord center, int radius) { Center = center; Radius = radius; }
        public int Count => Radius < 0 ? 0 : 1 + 3 * Radius * (Radius + 1);
        public Enumerator GetEnumerator() => new Enumerator(Center, Radius);
        public struct Enumerator
        {
            private readonly HexCoord center;
            private readonly int radius;
            private int ring;
            private HexRing.Enumerator enumerator;
            public HexCoord Current => enumerator.Current;
            internal Enumerator(HexCoord center, int radius)
            {
                this.center = center; this.radius = radius; ring = 0;
                enumerator = new HexRing(center, radius < 0 ? -1 : 0).GetEnumerator();
            }
            public bool MoveNext()
            {
                if (enumerator.MoveNext()) return true;
                if (ring >= radius) return false;
                enumerator = new HexRing(center, ++ring).GetEnumerator();
                return enumerator.MoveNext();
            }
        }
    }

    /// <summary>Nearest-cell line including both endpoints; deterministic epsilon breaks edge ties.</summary>
    public readonly struct HexLine
    {
        public readonly HexCoord From, To;
        public HexLine(HexCoord from, HexCoord to) { From = from; To = to; }
        public int Count => From.DistanceTo(To) + 1;
        public Enumerator GetEnumerator() => new Enumerator(From, To);
        public struct Enumerator
        {
            private readonly HexCoord from;
            private readonly int distance;
            private readonly double3 delta;
            private readonly bool reversed;
            private int index;
            private HexCoord current;
            public HexCoord Current => current;
            internal Enumerator(HexCoord from, HexCoord to)
            {
                reversed = from.Q > to.Q || (from.Q == to.Q && from.R > to.R);
                if (reversed) { HexCoord swap = from; from = to; to = swap; }
                this.from = from; distance = from.DistanceTo(to);
                delta = new double3(to.Q - from.Q, to.R - from.R, to.S - from.S);
                index = -1; current = default;
            }
            public bool MoveNext()
            {
                if (index >= distance) return false;
                index++;
                // Work relative to the first cell, preserving precision when world coordinates are large.
                int canonicalIndex = reversed ? distance - index : index;
                double3 value = delta * (distance == 0 ? 0.0 : (double)canonicalIndex / distance)
                    + new double3(0.000001, 0.000001, -0.000002);
                int3 rounded = (int3)math.round(value);
                double3 error = math.abs((double3)rounded - value);
                if (error.x > error.y && error.x > error.z) rounded.x = -rounded.y - rounded.z;
                else if (error.y > error.z) rounded.y = -rounded.x - rounded.z;
                current = from + new HexCoord(rounded.x, rounded.y);
                return true;
            }
        }
    }
}
