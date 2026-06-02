using UnityEngine;

/// <summary>
/// 2D-вариант статичного мирового контейнера (ящик/сундук). Требует <see cref="Collider2D"/>
/// (не-триггер) для наведения через <see cref="HoverPickup2D"/> (OverlapPoint/OverlapCircle).
/// Подсказка лежит в плоскости экрана — ориентация не требуется.
///
/// Создаётся в дизайн-тайме через утилиту <c>Tools/Inventory/Make Container From Selected — 2D</c>
/// либо вручную: повесь компонент на статичный GameObject с Collider2D.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class WorldContainer2D : WorldContainerBase
{
    protected override void EnsureCollider()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = false;
    }

    // 2D: подсказка уже в плоскости сцены, билборд не нужен.
    protected override void OrientPrompt() { }
}
