namespace Anvelop.HexGrid
{
    public enum HexMapShape { Hexagon, Rhombus, Rectangle, TriangleUp, TriangleDown }

    /// <summary>Contiguous row-major indexing for standard map shapes, without allocating storage.</summary>
    public readonly struct HexStorage
    {
        public readonly HexMapShape Shape;
        public readonly int Width, Height;
        public readonly HexCoord Origin;
        public readonly HexOffsetLayout OffsetLayout;
        public int Radius => Width;
        public int Count
        {
            get
            {
                if (Width < 0 || Height < 0) return 0;
                switch (Shape)
                {
                    case HexMapShape.Hexagon: return (int)(1L + 3L * Width * (Width + 1L));
                    case HexMapShape.TriangleUp:
                    case HexMapShape.TriangleDown: return (int)((long)Width * (Width + 1L) / 2);
                    default: return Width * Height;
                }
            }
        }
        private HexStorage(HexMapShape shape, int width, int height, HexCoord origin, HexOffsetLayout layout)
        { Shape = shape; Width = width; Height = height; Origin = origin; OffsetLayout = layout; }
        public static HexStorage Hexagon(int radius, HexCoord center = default) => new HexStorage(HexMapShape.Hexagon, radius, radius, center, default);
        public static HexStorage Rhombus(int width, int height, HexCoord origin = default) => new HexStorage(HexMapShape.Rhombus, width, height, origin, default);
        public static HexStorage Rectangle(int width, int height, HexOffsetLayout layout, HexOffset origin = default)
            => new HexStorage(HexMapShape.Rectangle, width, height, HexCoordinates.FromOffset(origin, layout), layout);
        /// <param name="side">Number of cells along an edge; zero is empty.</param>
        public static HexStorage Triangle(int side, bool up = false, HexCoord origin = default)
            => new HexStorage(up ? HexMapShape.TriangleUp : HexMapShape.TriangleDown, side, side, origin, default);

        public bool TryGetIndex(HexCoord coordinate, out int index)
        {
            index = -1;
            if (Count == 0) return false;
            HexCoord local = coordinate - Origin;
            int q = local.Q, r = local.R;
            switch (Shape)
            {
                case HexMapShape.Hexagon:
                    if (local.Length > Radius) return false;
                    int first = System.Math.Max(-Radius, -r - Radius);
                    index = RowStart(r + Radius) + q - first;
                    return true;
                case HexMapShape.Rectangle:
                    HexOffset offset = HexCoordinates.ToOffset(coordinate, OffsetLayout);
                    HexOffset origin = HexCoordinates.ToOffset(Origin, OffsetLayout);
                    q = offset.Column - origin.Column; r = offset.Row - origin.Row;
                    if (q < 0 || q >= Width || r < 0 || r >= Height) return false;
                    index = r * Width + q; return true;
                case HexMapShape.Rhombus:
                    if (q < 0 || q >= Width || r < 0 || r >= Height) return false;
                    index = r * Width + q; return true;
                default:
                    if (r < 0 || r >= Width) return false;
                    int qMin = Shape == HexMapShape.TriangleUp ? Width - 1 - r : 0;
                    int length = Shape == HexMapShape.TriangleUp ? r + 1 : Width - r;
                    if (q < qMin || q >= qMin + length) return false;
                    index = RowStart(r) + q - qMin; return true;
            }
        }

        public bool TryGetCoordinate(int index, out HexCoord coordinate)
        {
            coordinate = default;
            if (index < 0 || index >= Count) return false;
            if (Shape == HexMapShape.Rhombus)
            { coordinate = Origin + new HexCoord(index % Width, index / Width); return true; }
            if (Shape == HexMapShape.Rectangle)
            {
                HexOffset origin = HexCoordinates.ToOffset(Origin, OffsetLayout);
                coordinate = HexCoordinates.FromOffset(new HexOffset(origin.Column + index % Width, origin.Row + index / Width), OffsetLayout);
                return true;
            }
            int lo = 0, hi = Shape == HexMapShape.Hexagon ? Radius * 2 : Width - 1;
            while (lo < hi)
            {
                int mid = lo + (hi - lo + 1) / 2;
                if (RowStart(mid) <= index) lo = mid; else hi = mid - 1;
            }
            int r = Shape == HexMapShape.Hexagon ? lo - Radius : lo;
            int qMin = Shape == HexMapShape.Hexagon ? System.Math.Max(-Radius, -r - Radius)
                : Shape == HexMapShape.TriangleUp ? Width - 1 - r : 0;
            coordinate = Origin + new HexCoord(qMin + index - RowStart(lo), r);
            return true;
        }

        private int RowStart(int row)
        {
            long t = row, n = Width;
            switch (Shape)
            {
                case HexMapShape.Hexagon:
                    if (row <= Radius) return (int)(t * (n + 1) + t * (t - 1) / 2);
                    long remaining = 2 * n + 1 - t;
                    return Count - (int)(remaining * (n + 1) + remaining * (remaining - 1) / 2);
                case HexMapShape.TriangleUp: return (int)(t * (t + 1) / 2);
                default: return (int)(t * n - t * (t - 1) / 2);
            }
        }
    }
}
