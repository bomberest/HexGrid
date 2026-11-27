---
name: anvelop-hexgrid
description: Use the com.anvelop.hexgrid Unity package to implement or extend hex-grid math, coordinate conversions, map queries, movement, visibility and pathfinding with Burst-compatible allocation-free runtime code.
---

# Anvelop HexGrid

Use the installed `com.anvelop.hexgrid` package rather than introducing a parallel hex coordinate system. Locate it at the HexGrid repository root, through the dependency in `Packages/manifest.json`, in Unity's resolved package cache, or at `Assets/Anvelop/HexGrid` for a legacy import. Read its `README.md` and the relevant `Runtime/*.cs` declarations before writing calls; source is authoritative for overloads and buffer sizes. Read `Documentation~/coverage.md` when deciding which operation matches a request.

## Coordinate and layout rules

- `Anvelop.HexGrid.HexCoord` stores axial `Q, R`; cube `S` is derived as `-Q-R`. Use it for vector operations, equality, distance, neighbors and search keys.
- Convert offset or doubled inputs at boundaries. Offset coordinates cannot be added or subtracted as hex vectors; preserve the selected odd/even row/column layout, including negative coordinates. Doubled coordinates require even `Column + Row`.
- Keep the six direction indices and layout conventions consistent with `HexCoord.Direction` and `HexLayout`. Clockwise names follow the package's coordinate convention; mapping the second Cartesian component to Unity `Y` or `Z` can change how the result appears on screen.
- Keep pointy/flat orientation, nonzero origin and independent axis scales in one `HexLayout`. Convert a mouse/ray hit to the chosen 2D grid plane before pixel-to-hex conversion. Corners are numeric geometry; rendering and mesh construction belong to the consuming project.
- Respect the documented coordinate, radius, capacity and floating-point precision bounds. Do not assume unchecked integer arithmetic supports all `int` values, or that float picking is exact arbitrarily far from the origin.

## Allocation and ownership rules

Runtime queries must allocate neither managed memory nor native storage. Create and size `NativeArray` and native hash-map storage during setup or explicit resizing, before calling queries or scheduling jobs; dispose it from its owner after dependent jobs complete.

Use the package's concrete struct enumerators directly in `foreach`; do not wrap them in `IEnumerable`, LINQ, `yield`, delegates or managed collections. A returned view or copied native-container struct still aliases its owner's storage. Do not dispose borrowed views. Do not mutate buffers used by an outstanding job.

For pathfinding and movement, use caller-owned graph data, scratch and result buffers required by `HexSearch`. Check returned status and count; only the reported output prefix is valid. Distinguish insufficient capacity from unreachable destinations. Validate cell uniqueness, index consistency, costs and configuration when building a map, outside the hot path. Reuse scratch after a query finishes; simultaneous jobs need independent writable scratch/output storage. Scratch and output must not overlap each other or any map input, including reinterpreted views; overlap is not checked by runtime queries.

Keep Burst entry points in unmanaged jobs or compiled function pointers and use `Unity.Mathematics` types. Avoid strings, boxing, managed callbacks, exception-based control flow and NativeContainer allocation inside jobs. Preserve explicit capacity checks and deterministic tie-breaking when modifying search code.

## Ordinary C# and MonoBehaviour use

Direct calls to runtime APIs are supported without Jobs; they do not automatically execute Burst-compiled code. Geometry and `HexStorage` also work with ordinary preallocated managed arrays. Read `Documentation~/managed-usage.md` for this integration path and import the package's **Managed usage** sample when its owning wrapper is useful. `ManagedHexMap` is a managed `IDisposable` owner that allocates native storage once and reuses it for synchronous queries; do not create it per query. Consume its shared output before the next result-producing query or successful cost change, and dispose it on owner shutdown. The sample wrapper is not a Burst job data type. Caller-created `List`/`Dictionary` storage is allowed in ordinary C#, but its creation, growth and enumeration behavior remain the caller's allocation responsibility.

## Choosing and validating operations

- Use geometric ranges for coordinate distance; use movement search when walls or costs matter.
- `FindPath` chooses an admissible A* heuristic from the minimum traversable enter cost; a zero minimum reduces it to Dijkstra. `Reachable` uses weighted cost, whereas `ReachableSteps` ignores cost magnitude and counts steps.
- Read visibility's endpoint and missing-cell policy before integrating fog of war. Its center-line result is a specific visibility model, not polygon coverage or a universal supercover.
- Wrapping must match the map shape and coordinate convention. A wrapped position does not automatically make an unwrapped graph's neighbors wrap.

Run relevant package EditMode tests after changes. Include negative-coordinate conversion roundtrips, radius-zero/boundary cases and capacity failures for affected APIs. When changing runtime code intended for jobs, verify actual synchronous Burst compilation/execution through package tests or an equivalent job probe; ordinary C# compilation alone does not establish Burst compatibility. Measure allocations after warmup with setup outside the measured region. Report exactly which Unity/Burst tests ran and any environment limits.
