using UnityEngine;

/// <summary>
/// Единая точка спавна выброшенного из инвентаря предмета. Сама смотрит на
/// <see cref="ItemDefinition.worldMode"/> и зовёт нужный конкретный спавнер —
/// <see cref="WorldItem3D"/> или <see cref="WorldItem2D"/>.
///
/// <para>
/// Это нужно, чтобы дроп не был «всегда 3D» (как раньше): 2D-предмет в 2D-сцене
/// получает спрайтовый префаб + Rigidbody2D, а не 3D-куб.
/// </para>
///
/// Использование:
/// <code>
/// WorldItemSpawner.SpawnDropped(item, playerPos + forward * 0.35f, forward * 3f + Vector3.up * 1.2f);
/// </code>
/// </summary>
public static class WorldItemSpawner
{
    /// <summary>Спавн выброшенного из инвентаря предмета. Выбирает 2D / 3D-вариант по
    /// <see cref="ItemDefinition.worldMode"/>. В 2D-режиме Z-координата position отбрасывается,
    /// для скорости используется только (x, y).</summary>
    public static void SpawnDropped(ItemInstance instance, Vector3 position, Vector3 velocity, Quaternion rotation = default)
    {
        if (instance == null || instance.definition == null) return;

        switch (instance.definition.worldMode)
        {
            case WorldMode.TwoD:
                WorldItem2D.SpawnWithVelocity(instance, position, new Vector2(velocity.x, velocity.y), rotation);
                return;
            case WorldMode.ThreeD:
            default:
                WorldItem3D.SpawnWithVelocity(instance, position, velocity, rotation);
                return;
        }
    }
}
