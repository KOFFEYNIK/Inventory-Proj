using UnityEngine;

/// <summary>
/// Game-specific эффект расходника: восстанавливает голод (<see cref="IHungerTarget"/>),
/// жажду (<see cref="IThirstTarget"/>) и/или лечит конечности (<see cref="IHealthTarget"/>).
/// Каждый таргет опционален: если соответствующий модуль не подключён к игроку, эта часть
/// эффекта молча пропускается.
///
/// Привязывается к <see cref="ItemDefinition.consumable"/>. Активируется при выборе
/// "Использовать" в контекстном меню или хотбаре. Поля <see cref="ItemEffect.consumeOnUse"/>
/// и <see cref="ItemEffect.verb"/> унаследованы от базового класса плагина.
///
/// В будущем сюда можно добавить: бафы/дебафы, восстановление HP, яд, скорость и т.д.
/// </summary>
[CreateAssetMenu(fileName = "NewConsumable", menuName = "Inventory/Consumable Effect")]
public class ConsumableEffect : ItemEffect
{
    [Header("Restore (Hunger / Thirst)")]
    [Tooltip("Сколько голода восстанавливает за одно использование.")]
    public float restoreHunger = 0f;

    [Tooltip("Сколько жажды восстанавливает за одно использование.")]
    public float restoreThirst = 0f;

    [Header("Health (Body parts)")]
    [Tooltip("Если true — этот предмет работает как хирургический набор: восстанавливает первую " +
             "разрушенную (чёрную) часть тела. surgicalKitRestoreHp — на сколько HP оживить часть. " +
             "Если 0 — берётся IHealthTarget.DefaultSurgicalKitRestoreHp.")]
    public bool  isSurgicalKit         = false;
    public float surgicalKitRestoreHp  = 0f;

    [Tooltip("Если > 0 — лечит первую раненую (живую, но не на 100%) часть тела на это значение HP. " +
             "Не работает на разрушенных частях (для них нужен хирургический набор).")]
    public float healLimbAmount = 0f;

    public override bool TryApply(ItemInstance item, IItemEffectContext ctx)
    {
        if (ctx == null) return false;
        bool applied = false;
        var def = item != null ? item.definition : null;
        string displayName = def != null ? def.displayName : "<unknown>";

        // ── Hunger ──────────────────────────────────────────────────────────
        if (restoreHunger > 0f)
        {
            var hunger = ctx.Get<IHungerTarget>();
            if (hunger != null) { hunger.AddHunger(restoreHunger); applied = true; }
        }

        // ── Thirst ──────────────────────────────────────────────────────────
        if (restoreThirst > 0f)
        {
            var thirst = ctx.Get<IThirstTarget>();
            if (thirst != null) { thirst.AddThirst(restoreThirst); applied = true; }
        }

        // ── Health ──────────────────────────────────────────────────────────
        var health = ctx.Get<IHealthTarget>();
        if (health != null && health.IsAlive)
        {
            if (isSurgicalKit)
            {
                float restore = surgicalKitRestoreHp > 0f
                    ? surgicalKitRestoreHp
                    : health.DefaultSurgicalKitRestoreHp;
                if (restore <= 0f) restore = 30f;
                if (health.TryRestoreFirstDestroyed(restore))
                {
                    Debug.Log($"[ConsumableEffect] {displayName} → восстановлена разрушенная часть (+{restore} HP)");
                    applied = true;
                }
            }

            if (healLimbAmount > 0f && health.TryHealAnyWoundedLimb(healLimbAmount))
            {
                Debug.Log($"[ConsumableEffect] {displayName} → +{healLimbAmount} HP к раненой части");
                applied = true;
            }
        }

        if (!applied)
            Debug.Log($"[ConsumableEffect] {displayName} — нечего применять (нет цели или эффекта)");

        return applied;
    }
}
