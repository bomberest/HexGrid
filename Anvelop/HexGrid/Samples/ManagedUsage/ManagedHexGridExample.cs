using Unity.Mathematics;
using UnityEngine;

namespace Anvelop.HexGrid.Samples
{
    /// <summary>Add to an empty GameObject and enter Play mode. No meshes or rendering are created.</summary>
    public sealed class ManagedHexGridExample : MonoBehaviour
    {
        [SerializeField, Range(2, 32)] private int radius = 4;
        private ManagedHexMap map;

        // Numeric snapshots are visible in the Inspector and can be read from other managed components.
        [field: SerializeField]
        public int LastPathLength { get; private set; }
        [field: SerializeField]
        public long LastPathCost { get; private set; }
        [field: SerializeField]
        public int LastReachableCount { get; private set; }
        [field: SerializeField]
        public int LastStepRangeCount { get; private set; }
        [field: SerializeField]
        public int LastVisibleCount { get; private set; }
        public HexCoord PickedCell { get; private set; }
        [field: SerializeField]
        public Vector2Int PickedAxial { get; private set; }
        [field: SerializeField]
        public bool TargetIsVisible { get; private set; }

        private void OnEnable()
        {
            if (map != null) return;
            map = new ManagedHexMap(math.max(2, radius));
            map.SetCost(new HexCoord(1, 0), -1); // Wall: paths go around it; rays cannot cross it.
            map.SetCost(new HexCoord(0, 1), 3);  // Traversable, but more expensive to enter.
            DemonstrateQueries();
        }

        /// <summary>May be called repeatedly from ordinary C# without scheduling a job.</summary>
        [ContextMenu("Run managed hex examples")]
        public void DemonstrateQueries()
        {
            if (map == null) return;
            var start = new HexCoord(0, 0);
            var goal = new HexCoord(2, 0);
            HexSearchResult path = map.FindPath(start, goal);
            LastPathLength = path.Status == HexSearchStatus.Success ? path.Count : 0;
            LastPathCost = path.Status == HexSearchStatus.Success ? path.Cost : 0;
            for (int i = 0; i < map.ResultCount; i++)
            {
                HexCoord routeCell = map.GetResultCell(i);
                // Use the cell immediately, for example to move an existing unit.
                // No ToArray, List or logging is needed in the query loop.
                PickedCell = routeCell;
            }

            HexSearchResult movement = map.Reachable(start, budget: 3);
            LastReachableCount = movement.Status == HexSearchStatus.Success ? movement.Count : 0;
            HexSearchResult steps = map.ReachableSteps(start, maxSteps: 2);
            LastStepRangeCount = steps.Status == HexSearchStatus.Success ? steps.Count : 0;
            HexSearchResult visible = map.FieldOfView(start, radius: 3);
            LastVisibleCount = visible.Status == HexSearchStatus.Success ? visible.Count : 0;
            TargetIsVisible = map.HasLineOfSight(start, goal);

            // Convert a world-space hit into the grid's XZ plane. Supply an actual ray hit in a game.
            PickedCell = ManagedHexGeometry.PickXZ(ManagedHexGeometry.ToWorldXZ(goal));
            PickedAxial = new Vector2Int(PickedCell.Q, PickedCell.R);
        }

        private void OnDisable()
        {
            map?.Dispose();
            map = null;
        }
    }
}
