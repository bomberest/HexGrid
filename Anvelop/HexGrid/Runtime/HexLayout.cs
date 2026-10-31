using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Anvelop.HexGrid
{
    public enum HexOrientation { Pointy, Flat }

    /// <summary>
    /// Maps hex coordinates to a two-dimensional plane. Size is the independent X/Y circumradius scale.
    /// Size components must be finite and nonzero; Origin must be finite. A negative component mirrors the plane.
    /// Positive second-axis values follow the article's screen-Y convention. XY/XZ helpers map it directly to Y/Z.
    /// </summary>
    public readonly struct HexLayout
    {
        private const float Sqrt3 = 1.7320508075688772935f;
        public readonly HexOrientation Orientation;
        public readonly float2 Size;
        public readonly float2 Origin;

        /// <summary>Axis-aligned cell width/height; always positive even for mirrored layouts.</summary>
        public float2 Dimensions
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return math.abs(Size) * (Orientation == HexOrientation.Pointy ? new float2(Sqrt3, 2f) : new float2(2f, Sqrt3)); }
        }

        /// <summary>Absolute spacing between columns and rows in the matching offset layout.</summary>
        public float2 Spacing
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return math.abs(Size) * (Orientation == HexOrientation.Pointy ? new float2(Sqrt3, 1.5f) : new float2(1.5f, Sqrt3)); }
        }

        public bool IsValid
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get { return (uint)Orientation <= (uint)HexOrientation.Flat && math.all(math.isfinite(Size)) && math.all(Size != 0f) && math.all(math.isfinite(Origin)); }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexLayout(HexOrientation orientation, float2 size, float2 origin = default)
        {
            Orientation = orientation;
            Size = size;
            Origin = origin;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 ToPixel(HexCoord coordinate) { return ToPixel(new HexFractional(coordinate)); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 ToPixel(HexFractional coordinate)
        {
            float2 point = Orientation == HexOrientation.Pointy
                ? new float2(Sqrt3 * (coordinate.Q + 0.5f * coordinate.R), 1.5f * coordinate.R)
                : new float2(1.5f * coordinate.Q, Sqrt3 * (0.5f * coordinate.Q + coordinate.R));
            return point * Size + Origin;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 ToPixel(HexOffset coordinate, HexOffsetLayout layout) { return ToPixel(HexCoordinates.FromOffset(coordinate, layout)); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 ToPixel(HexDoubled coordinate, HexDoubledLayout layout) { return ToPixel(HexCoordinates.FromDoubled(coordinate, layout)); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexFractional ToFractional(float2 pixel)
        {
            float2 point = (pixel - Origin) / Size;
            return Orientation == HexOrientation.Pointy
                ? new HexFractional((Sqrt3 / 3f) * point.x - point.y / 3f, (2f / 3f) * point.y)
                : new HexFractional((2f / 3f) * point.x, -point.x / 3f + (Sqrt3 / 3f) * point.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord FromPixel(float2 pixel) { return ToFractional(pixel).Round(); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexOffset PixelToOffset(float2 pixel, HexOffsetLayout layout) { return HexCoordinates.ToOffset(FromPixel(pixel), layout); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexDoubled PixelToDoubled(float2 pixel, HexDoubledLayout layout) { return HexCoordinates.ToDoubled(FromPixel(pixel), layout); }

        /// <summary>Returns one corner's displacement from the center; corner indices wrap modulo six.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 CornerOffset(int corner)
        {
            // Exact tabulated angles avoid trigonometry and make opposite corners symmetric.
            int index = HexCoord.NormalizeDirection(corner);
            float2 result;
            if (Orientation == HexOrientation.Pointy)
            {
                switch (index)
                {
                    case 0: result = new float2(Sqrt3 * 0.5f, 0.5f); break;
                    case 1: result = new float2(0f, 1f); break;
                    case 2: result = new float2(-Sqrt3 * 0.5f, 0.5f); break;
                    case 3: result = new float2(-Sqrt3 * 0.5f, -0.5f); break;
                    case 4: result = new float2(0f, -1f); break;
                    default: result = new float2(Sqrt3 * 0.5f, -0.5f); break;
                }
            }
            else
            {
                switch (index)
                {
                    case 0: result = new float2(1f, 0f); break;
                    case 1: result = new float2(0.5f, Sqrt3 * 0.5f); break;
                    case 2: result = new float2(-0.5f, Sqrt3 * 0.5f); break;
                    case 3: result = new float2(-1f, 0f); break;
                    case 4: result = new float2(-0.5f, -Sqrt3 * 0.5f); break;
                    default: result = new float2(0.5f, -Sqrt3 * 0.5f); break;
                }
            }
            return result * Size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float2 Corner(HexCoord coordinate, int corner) { return ToPixel(coordinate) + CornerOffset(corner); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 ToWorldXZ(HexCoord coordinate, float height = 0f)
        {
            float2 point = ToPixel(coordinate);
            return new float3(point.x, height, point.y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord FromWorldXZ(float3 position) { return FromPixel(position.xz); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float3 ToWorldXY(HexCoord coordinate, float depth = 0f)
        {
            float2 point = ToPixel(coordinate);
            return new float3(point.x, point.y, depth);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord FromWorldXY(float3 position) { return FromPixel(position.xy); }
    }
}
