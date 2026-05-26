using UnityEngine;

/// <summary>
/// 2D-вариант мирового пикапа. Требует <see cref="Collider2D"/>. Подсказка не билбордится —
/// она в той же плоскости XY, что и предмет.
///
/// Подбор: HoverPickup2D наводит курсор / точка позиции игрока в зоне триггера — вызывает
/// <see cref="WorldItemBase.TryPickup"/>. (HoverPickup2D ещё не реализован — добавится в следующей итерации.)
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldItem2D : WorldItemBase
{
    protected override void EnsureCollider()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = false;
    }

    /// <summary>В 2D подсказка остаётся в плоскости — оборачивать её к камере не нужно.</summary>
    protected override void OrientPrompt() { /* no-op */ }

    // ── Static factory ───────────────────────────────────────────────────────

    /// <summary>Спавн предмета в мире с лёгким импульсом вверх.</summary>
    public static WorldItem2D Spawn(ItemDefinition definition, int count, Vector3 position, Quaternion rotation = default)
        => SpawnInternal(definition, count, position, rotation, null, null);

    /// <summary>Спавн выброшенного из инвентаря предмета (сохраняет nested-контейнер).</summary>
    public static WorldItem2D Spawn(ItemInstance instance, Vector3 position, Quaternion rotation = default)
    {
        if (instance == null || instance.definition == null) return null;
        return SpawnInternal(instance.definition, instance.stackCount, position, rotation, instance, null);
    }

    /// <summary>Детерминированный бросок с заданной 2D-скоростью.</summary>
    public static WorldItem2D SpawnWithVelocity(ItemInstance instance, Vector3 position, Vector2 initialVelocity,
                                                Quaternion rotation = default)
    {
        if (instance == null || instance.definition == null) return null;
        return SpawnInternal(instance.definition, instance.stackCount, position, rotation, instance, initialVelocity);
    }

    private static WorldItem2D SpawnInternal(ItemDefinition definition, int count, Vector3 position,
                                             Quaternion rotation, ItemInstance preserved,
                                             Vector2? initialVelocity)
    {
        if (definition == null) return null;

        GameObject go = null;
        if (definition.worldPrefab != null)
        {
            try
            {
                go = Object.Instantiate(definition.worldPrefab, position,
                                        rotation == default ? Quaternion.identity : rotation);
                go.name = definition.displayName;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[WorldItem2D] Instantiate worldPrefab '{definition.worldPrefab?.name}' " +
                               $"для '{definition.displayName}' упал: {ex.Message}. Падение на fallback-sprite.");
                if (go != null) Object.Destroy(go);
                go = null;
            }
        }
        if (go == null)
        {
            go = CreateFallbackSprite(definition);
            go.transform.position = position;
            go.transform.rotation = rotation == default ? Quaternion.identity : rotation;
        }

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null) rb = go.AddComponent<Rigidbody2D>();
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (initialVelocity.HasValue)
            rb.linearVelocity = initialVelocity.Value;
        else
            rb.AddForce(Vector2.up * 2f + Random.insideUnitCircle * 0.4f, ForceMode2D.Impulse);

        var rootCol = go.GetComponent<Collider2D>();
        if (rootCol == null) rootCol = go.AddComponent<BoxCollider2D>();
        rootCol.isTrigger = false;

        var wi = go.GetComponent<WorldItem2D>();
        if (wi == null) wi = go.AddComponent<WorldItem2D>();
        wi.definition        = definition;
        wi.stackCount        = count;
        wi.preservedInstance = preserved;

        return wi;
    }

    private static GameObject CreateFallbackSprite(ItemDefinition definition)
    {
        var go = new GameObject(definition.displayName);
        go.transform.localScale = new Vector3(definition.width, definition.height, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        if (definition.icon != null) sr.sprite = definition.icon;
        sr.color = definition.isContainer
            ? new Color(0.3f, 0.5f, 0.7f)
            : new Color(0.4f, 0.7f, 0.4f);
        return go;
    }
}
