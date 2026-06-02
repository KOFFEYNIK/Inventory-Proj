using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// 3D-вариант мирового пикапа. Требует <see cref="Collider"/>. Подсказка ориентируется
/// к <see cref="Camera.main"/> (билборд). Подбор выполняется через HoverPickup на камере.
///
/// Раньше этот класс назывался просто <c>WorldItem</c> в Assembly-CSharp. <see cref="MovedFromAttribute"/>
/// гарантирует, что существующие сцены/префабы продолжат привязываться к компоненту.
///
/// Для создания из кода — используй статические <see cref="Spawn(ItemDefinition, int, Vector3, Quaternion)"/>.
/// </summary>
[RequireComponent(typeof(Collider))]
[MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "WorldItem")]
public class WorldItem3D : WorldItemBase
{
    /// <summary>
    /// Хук для проекта: подписаться, чтобы убрать game-specific компоненты из мирового
    /// префаба перед спавном (например, FPS-скрипты, anim-controllers, recoil — те, что
    /// в Awake обращаются к иерархии игрока).
    /// </summary>
    public static event System.Action<GameObject> OnPrefabSanitized;

    // ── WorldItemBase overrides ──────────────────────────────────────────────

    protected override void EnsureCollider()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = false;
    }

    protected override void OrientPrompt()
    {
        if (Camera.main != null)
            promptUI.transform.forward = Camera.main.transform.forward;
    }

    // ── Static factory (для выброса из инвентаря) ─────────────────────────────

    /// <summary>Спавн предмета без явной скорости — лёгкий случайный подбросок вверх.</summary>
    public static WorldItem3D Spawn(ItemDefinition definition, int count, Vector3 position, Quaternion rotation = default)
        => SpawnInternal(definition, count, position, rotation, null, null);

    /// <summary>Спавн выброшенного из инвентаря предмета (сохраняет nested-контейнер с содержимым).</summary>
    public static WorldItem3D Spawn(ItemInstance instance, Vector3 position, Quaternion rotation = default)
    {
        if (instance == null || instance.definition == null) return null;
        return SpawnInternal(instance.definition, instance.stackCount, position, rotation, instance, null);
    }

    /// <summary>Детерминированный бросок с заданной скоростью (например, «вперёд от игрока»).</summary>
    public static WorldItem3D SpawnWithVelocity(ItemInstance instance, Vector3 position, Vector3 initialVelocity,
                                                Quaternion rotation = default)
    {
        if (instance == null || instance.definition == null) return null;
        return SpawnInternal(instance.definition, instance.stackCount, position, rotation, instance, initialVelocity);
    }

    private static WorldItem3D SpawnInternal(ItemDefinition definition, int count, Vector3 position,
                                             Quaternion rotation, ItemInstance preserved,
                                             Vector3? initialVelocity)
    {
        if (definition == null) return null;

        GameObject go = null;
        if (definition.worldPrefab3D != null)
        {
            try
            {
                go = Object.Instantiate(definition.worldPrefab3D, position,
                                        rotation == default ? Quaternion.identity : rotation);
                go.name = definition.displayName;
                SanitizeWorldPrefab(go);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WorldItem3D] Instantiate worldPrefab '{definition.worldPrefab3D?.name}' " +
                               $"для '{definition.displayName}' упал: {ex.Message}. Падение на fallback-куб.");
                if (go != null) Object.Destroy(go);
                go = null;
            }
        }
        if (go == null)
        {
            go = CreateFallbackCube(definition);
            go.transform.position = position;
            go.transform.rotation = rotation == default ? Quaternion.identity : rotation;
        }

        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.linearVelocity  = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (initialVelocity.HasValue)
            rb.linearVelocity = initialVelocity.Value;
        else
            rb.AddForce(Vector3.up * 2f + Random.insideUnitSphere * 0.4f, ForceMode.VelocityChange);

        var rootCol = go.GetComponent<Collider>();
        if (rootCol == null) rootCol = go.AddComponent<BoxCollider>();
        rootCol.isTrigger = false;

        var wi = go.GetComponent<WorldItem3D>();
        if (wi == null) wi = go.AddComponent<WorldItem3D>();
        wi.definition        = definition;
        wi.stackCount        = count;
        wi.preservedInstance = preserved;

        return wi;
    }

    /// <summary>
    /// Чистка свежеспавненного префаба от того, что мешает ему лежать в мире:
    ///   • Вложенные Rigidbody — мы вешаем единственный на корень.
    ///   • Non-convex MeshCollider — конфликтует с динамическим Rigidbody.
    /// Дополнительная игровая чистка (FPS-скрипты и т.п.) — через event <see cref="OnPrefabSanitized"/>.
    /// </summary>
    private static void SanitizeWorldPrefab(GameObject root)
    {
        if (root == null) return;

        var nestedBodies = root.GetComponentsInChildren<Rigidbody>(true);
        foreach (var rb in nestedBodies)
            if (rb != null && rb.gameObject != root) Object.Destroy(rb);

        var meshCols = root.GetComponentsInChildren<MeshCollider>(true);
        foreach (var mc in meshCols)
            if (mc != null) mc.convex = true;

        OnPrefabSanitized?.Invoke(root);
    }

    private static GameObject CreateFallbackCube(ItemDefinition definition)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = definition.displayName;
        go.transform.localScale = new Vector3(
            0.1f * definition.width,
            0.1f,
            0.1f * definition.height);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            // В URP/HDRP new Material(Shader.Find(...)) даёт material БЕЗ keywords/textures
            // и рендерится magenta. Копируем дефолтный material активного pipeline.
            var rp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            Material baseMat = rp != null ? rp.defaultMaterial : null;
            if (baseMat == null) baseMat = renderer.sharedMaterial;
            Material mat = baseMat != null
                ? new Material(baseMat)
                : new Material(Shader.Find("Standard"));

            var color = definition.isContainer
                ? new Color(0.3f, 0.5f, 0.7f)
                : new Color(0.4f, 0.7f, 0.4f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     color);
            renderer.material = mat;
        }
        return go;
    }
}
