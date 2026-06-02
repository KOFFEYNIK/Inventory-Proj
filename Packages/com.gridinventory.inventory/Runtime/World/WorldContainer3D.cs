using UnityEngine;

/// <summary>
/// 3D-вариант статичного мирового контейнера (ящик/сундук). Требует <see cref="Collider"/>
/// (не-триггер) для наведения камерой. Подсказка ориентируется к <see cref="Camera.main"/>.
///
/// Создаётся в дизайн-тайме через утилиту <c>Tools/Inventory/Make Container From Selected — 3D</c>
/// либо вручную: повесь компонент на статичный GameObject с коллайдером.
/// </summary>
[RequireComponent(typeof(Collider))]
public class WorldContainer3D : WorldContainerBase
{
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
}
