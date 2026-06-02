using UnityEngine;
using System.Collections.Generic;

namespace PBS2D
{
    public class SplatManager : Singleton<SplatManager>
    {
        [SerializeField]
        GameObject _splatPrefab;

        [Header("Grid Settings")]
        [SerializeField]
        float _cellSize = 0.25f;

        [SerializeField]
        float _minSplatSize = 0.15f;

        [SerializeField]
        float _maxSplatSize = 0.2f;

        [SerializeField]
        float _elongation = .4f;

        protected override void Awake()
        {
            base.Awake();
            SplatGrid.Init(_cellSize);
        }

        public bool PlaceBloodSplat(Vector2 position, bool horizontal)
        {
            float size = Random.Range(_minSplatSize, _maxSplatSize);

            if (!SplatGrid.IsFree(position, size * SplatGrid.OVERLAP_CHECK_LOOSENESS))
                return false;

            var splat = ObjectPoolManager.SpawnObject(_splatPrefab, position, Quaternion.identity, PoolType.Effect);
            splat.transform.localScale = new Vector2(size, size * _elongation);
            if (!horizontal)
                splat.transform.rotation = Quaternion.Euler(0, 0, 90);

            SplatGrid.Add(position, size / 2, horizontal, splat.transform);

            return true;
        }
    }

    public static class SplatGrid
    {
        const float GROWTH_STEP = 0.02f;
        const float GROWTH_CAP_MULTIPLIER = 3f;
        const float LOGICAL_GROWTH_FACTOR = 0.5f;
        const float VERTICAL_GROWTH_FACTOR = 0.2f;
        public const float OVERLAP_CHECK_LOOSENESS = 1f / 3f;
        
        public static float CellSize { get; private set; }
        public static float MaxStoredRadius { get; private set; }
        static readonly Dictionary<Vector2Int, List<Splat>> grid = new();

        struct Splat
        {
            public Vector2 Position;
            public float Radius;
            public float OriginalRadius;
            public bool Horizontal;
            public Transform Transform;
        }

        public static void Init(float cellSize)
        {
            CellSize = cellSize;
            grid.Clear();
            MaxStoredRadius = 0f;
        }

        static Vector2Int GetCell(Vector2 position) =>
            new(Mathf.FloorToInt(position.x / CellSize), Mathf.FloorToInt(position.y / CellSize));

        public static bool IsFree(Vector2 position, float radius)
        {
            var cell = GetCell(position);
            int searchRange = Mathf.CeilToInt((radius + MaxStoredRadius) / CellSize) + 1;

            for (int y = -searchRange; y <= searchRange; y++)
                for (int x = -searchRange; x <= searchRange; x++)
                {
                    var neighborCell = new Vector2Int(cell.x + x, cell.y + y);
                    if (!grid.TryGetValue(neighborCell, out var splats))
                        continue;

                    for (int i = splats.Count - 1; i >= 0; i--)
                    {
                        var existingSplat = splats[i];

                        if (existingSplat.Transform == null)
                        {
                            splats.RemoveAt(i);
                            continue;
                        }

                        float combinedRadius = existingSplat.Radius + radius;

                        if ((existingSplat.Position - position).sqrMagnitude < combinedRadius * combinedRadius)
                        {
                            float maxRadius = existingSplat.OriginalRadius * GROWTH_CAP_MULTIPLIER;
                            float remaining = maxRadius - existingSplat.Radius;
                            if (remaining > 0f)
                            {
                                float desiredLogicalStep = GROWTH_STEP * LOGICAL_GROWTH_FACTOR;
                                float deltaRadius = Mathf.Min(desiredLogicalStep, remaining);
                                existingSplat.Radius += deltaRadius;

                                float growthRatio = deltaRadius / desiredLogicalStep;
                                var scale = existingSplat.Transform.localScale;
                                scale.x += GROWTH_STEP * growthRatio;
                                scale.y += (GROWTH_STEP * VERTICAL_GROWTH_FACTOR) * growthRatio;
                                existingSplat.Transform.localScale = scale;

                                splats[i] = existingSplat;
                                if (existingSplat.Radius > MaxStoredRadius) MaxStoredRadius = existingSplat.Radius;
                            }
                            return false;
                        }
                    }
                }
            return true;
        }

        public static void Add(Vector2 position, float radius, bool horizontal, Transform transform)
        {
            var cell = GetCell(position);
            if (!grid.TryGetValue(cell, out var splats))
                grid[cell] = splats = new List<Splat>(4);

            splats.Add(new Splat
            {
                Position = position,
                Radius = radius,
                OriginalRadius = radius,
                Horizontal = horizontal,
                Transform = transform
            });

            if (radius > MaxStoredRadius) MaxStoredRadius = radius;
        }
    }
}
