using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Anvelop.HexGrid
{
    public enum HexOffsetLayout { OddR, EvenR, OddQ, EvenQ }
    public enum HexDoubledLayout { DoubleWidth, DoubleHeight }

    /// <summary>An offset cell address. Its layout is supplied when converting; offset addresses are not vectors.</summary>
    public readonly struct HexOffset : IEquatable<HexOffset>
    {
        public readonly int Column;
        public readonly int Row;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexOffset(int column, int row) { Column = column; Row = row; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(HexOffset other) { return Column == other.Column && Row == other.Row; }

        public override bool Equals(object obj) { return obj is HexOffset other && Equals(other); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() { return unchecked((int)math.hash(new int2(Column, Row))); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(HexOffset a, HexOffset b) { return a.Equals(b); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(HexOffset a, HexOffset b) { return !a.Equals(b); }
    }

    /// <summary>A doubled cell address. Column and Row must have equal parity, including for negative coordinates.</summary>
    public readonly struct HexDoubled : IEquatable<HexDoubled>
    {
        public readonly int Column;
        public readonly int Row;
        public bool IsValid { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return ((Column ^ Row) & 1) == 0; } }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexDoubled(int column, int row) { Column = column; Row = row; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(HexDoubled other) { return Column == other.Column && Row == other.Row; }

        public override bool Equals(object obj) { return obj is HexDoubled other && Equals(other); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() { return unchecked((int)math.hash(new int2(Column, Row))); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(HexDoubled a, HexDoubled b) { return a.Equals(b); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(HexDoubled a, HexDoubled b) { return !a.Equals(b); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexDoubled operator +(HexDoubled a, HexDoubled b) { return new HexDoubled(a.Column + b.Column, a.Row + b.Row); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexDoubled operator -(HexDoubled a, HexDoubled b) { return new HexDoubled(a.Column - b.Column, a.Row - b.Row); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexDoubled operator *(HexDoubled value, int scale) { return new HexDoubled(value.Column * scale, value.Row * scale); }
    }

    /// <summary>Allocation-free adapters between axial/cube, four offset layouts, and two doubled layouts.</summary>
    public static class HexCoordinates
    {
        /// <summary>Layout enum values must be defined. Intermediate arithmetic must fit Int32.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexOffset ToOffset(HexCoord coordinate, HexOffsetLayout layout)
        {
            switch (layout)
            {
                case HexOffsetLayout.OddR: return new HexOffset(coordinate.Q + (coordinate.R - (coordinate.R & 1)) / 2, coordinate.R);
                case HexOffsetLayout.EvenR: return new HexOffset(coordinate.Q + (coordinate.R + (coordinate.R & 1)) / 2, coordinate.R);
                case HexOffsetLayout.OddQ: return new HexOffset(coordinate.Q, coordinate.R + (coordinate.Q - (coordinate.Q & 1)) / 2);
                default: return new HexOffset(coordinate.Q, coordinate.R + (coordinate.Q + (coordinate.Q & 1)) / 2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexCoord FromOffset(HexOffset coordinate, HexOffsetLayout layout)
        {
            switch (layout)
            {
                case HexOffsetLayout.OddR: return new HexCoord(coordinate.Column - (coordinate.Row - (coordinate.Row & 1)) / 2, coordinate.Row);
                case HexOffsetLayout.EvenR: return new HexCoord(coordinate.Column - (coordinate.Row + (coordinate.Row & 1)) / 2, coordinate.Row);
                case HexOffsetLayout.OddQ: return new HexCoord(coordinate.Column, coordinate.Row - (coordinate.Column - (coordinate.Column & 1)) / 2);
                default: return new HexCoord(coordinate.Column, coordinate.Row - (coordinate.Column + (coordinate.Column & 1)) / 2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexDoubled ToDoubled(HexCoord coordinate, HexDoubledLayout layout)
        {
            return layout == HexDoubledLayout.DoubleWidth
                ? new HexDoubled(2 * coordinate.Q + coordinate.R, coordinate.R)
                : new HexDoubled(coordinate.Q, 2 * coordinate.R + coordinate.Q);
        }

        /// <summary>Requires coordinate.IsValid and a defined layout enum value.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexCoord FromDoubled(HexDoubled coordinate, HexDoubledLayout layout)
        {
            return layout == HexDoubledLayout.DoubleWidth
                ? new HexCoord((coordinate.Column - coordinate.Row) / 2, coordinate.Row)
                : new HexCoord(coordinate.Column, (coordinate.Row - coordinate.Column) / 2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFromDoubled(HexDoubled coordinate, HexDoubledLayout layout, out HexCoord result)
        {
            if (!coordinate.IsValid || (uint)layout > (uint)HexDoubledLayout.DoubleHeight)
            {
                result = default;
                return false;
            }
            result = FromDoubled(coordinate, layout);
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexOffset OffsetNeighbor(HexOffset coordinate, HexOffsetLayout layout, int direction)
        {
            return ToOffset(FromOffset(coordinate, layout).Neighbor(direction), layout);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexDoubled DoubledNeighbor(HexDoubled coordinate, HexDoubledLayout layout, int direction)
        {
            return ToDoubled(FromDoubled(coordinate, layout).Neighbor(direction), layout);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int OffsetDistance(HexOffset a, HexOffset b, HexOffsetLayout layout)
        {
            return FromOffset(a, layout).DistanceTo(FromOffset(b, layout));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int DoubledDistance(HexDoubled a, HexDoubled b, HexDoubledLayout layout)
        {
            int dc = math.abs(a.Column - b.Column);
            int dr = math.abs(a.Row - b.Row);
            return layout == HexDoubledLayout.DoubleWidth
                ? dr + math.max(0, (dc - dr) / 2)
                : dc + math.max(0, (dr - dc) / 2);
        }
    }
}
