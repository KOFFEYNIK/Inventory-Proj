/// <summary>
/// Game-specific интерфейс для эффектов, которые лечат / восстанавливают части тела.
/// Регистрируется в <see cref="IItemEffectContext"/> (обычно — реализуется на HealthSystem).
///
/// Этот интерфейс ЖИВЁТ В ИГРЕ (не в плагине), потому что система здоровья по частям тела —
/// игровая механика, не часть инвентаря. Плагин про него не знает.
/// </summary>
public interface IHealthTarget
{
    bool IsAlive { get; }

    /// <summary>Восстановить первую разрушенную часть тела на restoreHp HP. False, если разрушенных нет.</summary>
    bool TryRestoreFirstDestroyed(float restoreHp);

    /// <summary>Лечить первую раненую (живую, но не на 100%) часть на amount HP. False, если нечего лечить.</summary>
    bool TryHealAnyWoundedLimb(float amount);

    /// <summary>Дефолтное значение восстановления хирургического набора (если эффект не задаёт собственное).</summary>
    float DefaultSurgicalKitRestoreHp { get; }
}
