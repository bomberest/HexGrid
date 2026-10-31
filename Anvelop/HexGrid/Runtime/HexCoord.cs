using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;

namespace Anvelop.HexGrid
{
    /// <summary>
    /// An axial coordinate with the implicit cube component S = -Q-R.
    /// All intermediate integer results must fit Int32. No allocation occurs in typed operations.
    /// Directions follow Red Blob Games: (1,0), (1,-1), (0,-1), (-1,0), (-1,1), (0,1).
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>
    {
        public readonly int Q;
        public readonly int R;
        public int S { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return -Q - R; } }
        public int3 Cube { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return new int3(Q, R, S); } }
        public int Length { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return math.max(math.abs(Q), math.max(math.abs(R), math.abs(S))); } }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord(int q, int r) { Q = q; R = r; }

        /// <summary>Returns false for a cube coordinate outside the Q+R+S=0 plane.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryFromCube(int3 cube, out HexCoord coordinate)
        {
            coordinate = new HexCoord(cube.x, cube.y);
            return (long)cube.x + cube.y + cube.z == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int DistanceTo(HexCoord other) { return (this - other).Length; }

        /// <summary>Euclidean distance in units of adjacent cell-center spacing, before layout scaling.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float EuclideanDistanceTo(HexCoord other)
        {
            float dq = Q - other.Q;
            float dr = R - other.R;
            return math.sqrt(dq * dq + dr * dr + dq * dr);
        }

        /// <summary>Direction indices wrap modulo six, including negative indices.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexCoord Direction(int direction)
        {
            switch (NormalizeDirection(direction))
            {
                case 0: return new HexCoord(1, 0);
                case 1: return new HexCoord(1, -1);
                case 2: return new HexCoord(0, -1);
                case 3: return new HexCoord(-1, 0);
                case 4: return new HexCoord(-1, 1);
                default: return new HexCoord(0, 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexCoord DiagonalDirection(int direction)
        {
            switch (NormalizeDirection(direction))
            {
                case 0: return new HexCoord(2, -1);
                case 1: return new HexCoord(1, -2);
                case 2: return new HexCoord(-1, -1);
                case 3: return new HexCoord(-2, 1);
                case 4: return new HexCoord(-1, 2);
                default: return new HexCoord(1, 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord Neighbor(int direction) { return this + Direction(direction); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord DiagonalNeighbor(int direction) { return this + DiagonalDirection(direction); }

        /// <summary>Rotates in 60-degree steps using the article's screen-coordinate convention (positive Y down).</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord RotateClockwise(int steps = 1)
        {
            switch (NormalizeDirection(steps))
            {
                case 0: return this;
                case 1: return new HexCoord(-R, -S);
                case 2: return new HexCoord(S, Q);
                case 3: return new HexCoord(-Q, -R);
                case 4: return new HexCoord(R, S);
                default: return new HexCoord(-S, -Q);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord RotateCounterClockwise(int steps = 1) { return RotateClockwise(-NormalizeDirection(steps)); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord RotateAround(HexCoord center, int clockwiseSteps) { return center + (this - center).RotateClockwise(clockwiseSteps); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord ReflectQ() { return new HexCoord(Q, S); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord ReflectR() { return new HexCoord(S, R); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord ReflectS() { return new HexCoord(R, Q); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord ReflectQ(HexCoord center) { return center + (this - center).ReflectQ(); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord ReflectR(HexCoord center) { return center + (this - center).ReflectR(); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord ReflectS(HexCoord center) { return center + (this - center).ReflectS(); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(HexCoord other) { return Q == other.Q && R == other.R; }

        public override bool Equals(object obj) { return obj is HexCoord other && Equals(other); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() { return unchecked((int)math.hash(new int2(Q, R))); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(HexCoord left, HexCoord right) { return left.Equals(right); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(HexCoord left, HexCoord right) { return !left.Equals(right); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexCoord operator +(HexCoord left, HexCoord right) { return new HexCoord(left.Q + right.Q, left.R + right.R); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexCoord operator -(HexCoord left, HexCoord right) { return new HexCoord(left.Q - right.Q, left.R - right.R); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexCoord operator -(HexCoord value) { return new HexCoord(-value.Q, -value.R); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexCoord operator *(HexCoord value, int scale) { return new HexCoord(value.Q * scale, value.R * scale); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexCoord operator *(int scale, HexCoord value) { return value * scale; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexFractional operator /(HexCoord value, float divisor) { return new HexFractional(value.Q / divisor, value.R / divisor); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int NormalizeDirection(int direction) { int n = direction % 6; return n < 0 ? n + 6 : n; }
    }

    /// <summary>Fractional axial coordinates; inputs to Round must be finite and fit Int32 after rounding.</summary>
    public readonly struct HexFractional
    {
        public readonly float Q;
        public readonly float R;
        public float S { [MethodImpl(MethodImplOptions.AggressiveInlining)] get { return -Q - R; } }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexFractional(float q, float r) { Q = q; R = r; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexFractional(HexCoord coordinate) { Q = coordinate.Q; R = coordinate.R; }

        /// <summary>Nearest-cell rounding with ties to even, followed by cube-plane correction.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public HexCoord Round()
        {
            float q = math.round(Q);
            float r = math.round(R);
            float s = math.round(S);
            float dq = math.abs(q - Q);
            float dr = math.abs(r - R);
            float ds = math.abs(s - S);
            if (dq > dr && dq > ds) q = -r - s;
            else if (dr > ds) r = -q - s;
            return new HexCoord((int)q, (int)r);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexFractional Lerp(HexFractional a, HexFractional b, float t)
        {
            return new HexFractional(math.lerp(a.Q, b.Q, t), math.lerp(a.R, b.R, t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexFractional Lerp(HexCoord a, HexCoord b, float t)
        {
            return Lerp(new HexFractional(a), new HexFractional(b), t);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexFractional operator +(HexFractional a, HexFractional b) { return new HexFractional(a.Q + b.Q, a.R + b.R); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexFractional operator -(HexFractional a, HexFractional b) { return new HexFractional(a.Q - b.Q, a.R - b.R); }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static HexFractional operator *(HexFractional a, float scale) { return new HexFractional(a.Q * scale, a.R * scale); }
    }
}
