# Operation coverage

Scope: the operations described on [Red Blob Games — Hexagonal Grids](https://www.redblobgames.com/grids/hexagons/), reviewed on 2026-10-26. The package computes coordinates and numeric geometry; it does not create models, meshes, renderers or GameObjects.

| Article topic | Package entry points |
| --- | --- |
| Geometry: spacing, dimensions and corners | `HexLayout.Dimensions`, `Spacing`, `CornerOffset`, `Corner`; pointy and flat orientations |
| Cube and axial coordinates; vectors | `HexCoord(Q, R)`, derived `S`, `Cube`, `TryFromCube`; arithmetic and `HexFractional` |
| Odd/even row/column offset conversion | `HexCoordinates.ToOffset`, `FromOffset`; `OddR`, `EvenR`, `OddQ`, `EvenQ` |
| Doubled coordinate conversion and vectors | `ToDoubled`, `FromDoubled`, `TryFromDoubled`; `DoubleWidth`, `DoubleHeight`; `HexDoubled` arithmetic |
| Neighbors and diagonals | `HexCoord.Direction`, `Neighbor`, `DiagonalDirection`, `DiagonalNeighbor`; offset/doubled neighbor adapters |
| Distances | `Length`, `DistanceTo`, `EuclideanDistanceTo`; `OffsetDistance`, `DoubledDistance` |
| Line drawing | `HexLine`; inclusive endpoints and a deterministic boundary nudge |
| Coordinate range and range intersection | `HexRegion.Around`, `Intersect`, `Contains`; cube-axis bounds and concrete struct traversal |
| Obstacle-limited movement | `HexSearch.ReachableSteps` for BFS; `Reachable` for weighted movement |
| Rotation and reflection | `RotateClockwise`, `RotateCounterClockwise`, `RotateAround`, `ReflectQ/R/S`; reflection overloads around a reference point |
| Rings and spiral traversal | `HexRing`, `HexSpiral`; radius zero includes the center |
| Visibility | `HasLineOfSight`, `FieldOfView`; center rays with explicit blocking-target policy |
| Forward Cartesian mapping | `HexLayout.ToPixel`; axial, offset and doubled inputs |
| Inverse Cartesian mapping | `ToFractional`, `FromPixel`, `PixelToOffset`, `PixelToDoubled` |
| Origin and nonuniform scale | `HexLayout.Origin`, `Size`; inverse operations undo translation and scaling |
| Fractional rounding | `HexFractional.Round`, `Lerp`; cube correction after component rounding |
| Dense and sparse map storage | `HexStorage.Hexagon`, `Rhombus`, `Rectangle`, `Triangle`; `TryGetIndex`, `TryGetCoordinate`; caller-owned `NativeArray<T>`, `NativeParallelHashMap<HexCoord,T>`, `HexMap` |
| Rectangular and hexagonal wrapping | `HexWrap.Rectangle`, `Hexagon`, `MirrorCenter` |
| Pathfinding | `HexSearch.FindPath`: admissible A*, reducing to Dijkstra when the minimum enter cost is zero |

## Behavioral boundaries

- Coordinate operations accept valid enums and values whose intermediate integer arithmetic fits its declared `Int32`/`Int64` type. Fractional operations require finite values in range. `HexLayout.IsValid` and `Try` conversion APIs support setup-time validation.
- `HexCoord` is the common algorithm representation. Offset and doubled systems can access the same algorithms by converting at their boundaries; they do not each require duplicate search implementations.
- Line and visibility queries select one deterministic ray. They do not enumerate every touched boundary cell or model partial-cell obstacles. Missing sparse-map cells occlude. The first wall is visible by default.
- Map storage helpers calculate addresses; callers choose and own value storage. Search operates on the `HexMap` index and six ordinary neighbors. Coordinate wrapping does not add wrapped edges to search.
- The article's “Others” and “More” sections link to alternative coordinate research and additional articles without defining a complete operation set for them. Recursive ternary/Gosper systems and algorithms from those external publications are outside this package's operation contract.
- Geometric corner coordinates are included. Mesh/model generation and presentation remain the consuming project's responsibility.

See the package [README](../README.md) for ownership, capacity, numeric and Burst integration requirements, and `Tests/Editor` for executable examples and edge cases.
