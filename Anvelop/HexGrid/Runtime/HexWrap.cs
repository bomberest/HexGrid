namespace Anvelop.HexGrid
{
    /// <summary>Wrapping is a map topology operation, independent of pixel orientation.</summary>
    public static class HexWrap
    {
        /// <summary>Wrap in offset space; either dimension can remain unwrapped.</summary>
        public static HexCoord Rectangle(HexCoord coordinate, int width, int height, HexOffsetLayout layout,
            HexOffset origin = default, bool wrapColumns = true, bool wrapRows = true)
        {
            HexOffset offset = HexCoordinates.ToOffset(coordinate, layout);
            int column = offset.Column, row = offset.Row;
            if (wrapColumns && width > 0) column = origin.Column + Mod(column - origin.Column, width);
            if (wrapRows && height > 0) row = origin.Row + Mod(row - origin.Row, height);
            return HexCoordinates.FromOffset(new HexOffset(column, row), layout);
        }

        /// <summary>One of six mirror translations of a radius-N hexagonal map.</summary>
        public static HexCoord MirrorCenter(int radius, int direction)
            => new HexCoord(2 * radius + 1, -radius).RotateClockwise(direction);

        /// <summary>Reduce by nearest mirror centers until within radius. Negative radius leaves input unchanged.</summary>
        public static HexCoord Hexagon(HexCoord coordinate, int radius, HexCoord center = default)
        {
            if (radius < 0) return coordinate;
            if (radius == 0) return center;
            HexCoord local = coordinate - center;
            // First remove whole lattice periods. This keeps work bounded for distant coordinates.
            long n = radius;
            long determinant = 3 * n * (n + 1) + 1;
            long a = DivideNearest((n + 1) * local.Q - n * local.R, determinant);
            long b = DivideNearest(n * local.Q + (2 * n + 1) * local.R, determinant);
            local = new HexCoord((int)(local.Q - a * (2 * n + 1) - b * n),
                (int)(local.R + a * n - b * (n + 1)));
            while (local.Length > radius)
            {
                HexCoord best = local;
                // Squared Euclidean length resolves equal hex-distance plateaus.
                long bestLength = SquaredLength(local);
                for (int i = 0; i < 6; i++)
                {
                    HexCoord candidate = local - MirrorCenter(radius, i);
                    long length = SquaredLength(candidate);
                    if (length < bestLength) { best = candidate; bestLength = length; }
                }
                local = best;
            }
            return center + local;
        }

        private static long SquaredLength(HexCoord h) => (long)h.Q * h.Q + (long)h.R * h.R + (long)h.S * h.S;
        private static long DivideNearest(long value, long divisor) => value >= 0
            ? (value + divisor / 2) / divisor : -((-value + divisor / 2) / divisor);
        private static int Mod(int value, int modulus) { int remainder = value % modulus; return remainder < 0 ? remainder + modulus : remainder; }
    }
}
