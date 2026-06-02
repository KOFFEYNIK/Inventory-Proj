using UnityEngine;

/// <summary>
/// Применение эффектов предметов. Бизнес-логика конкретного эффекта живёт в
/// <see cref="ItemEffect.TryApply"/> (плагин-абстракция); этот сервис только:
///   1) строит <see cref="IItemEffectContext"/> с актуальными game-side таргетами;
///   2) вызывает TryApply;
///   3) при успехе и <see cref="ItemEffect.consumeOnUse"/> = true уменьшает стак / удаляет.
///
/// Когда InventoryManager переедет в плагин, ItemUseService туда тоже переедет — он generic.
/// </summary>
public static class ItemUseService
{
    public static bool TryUse(ItemInstance item, GridContainer container, EquipmentSlotType? slotType)
    {
        if (item == null || item.definition == null) return false;
        var def    = item.definition;
        var effect = def.consumable;
        if (effect == null)
        {
            Debug.Log($"[ItemUse] {def.displayName} — нечего использовать (нет эффекта)");
            return false;
        }

        var ctx = BuildContext();
        if (!effect.TryApply(item, ctx)) return false;

        if (effect.consumeOnUse) ConsumeOne(item, container, slotType);
        return true;
    }

    private static IItemEffectContext BuildContext()
    {
        var ctx = new ItemEffectContext();
        var mgr = PlayerModuleManager.Instance;
        if (mgr != null)
        {
            // Единая точка lookup'а — любой player-модуль, реализующий IHungerTarget/
            // IThirstTarget/IHealthTarget (или будущие интерфейсы), подцепится сюда
            // автоматически.
            var hunger = mgr.Get<IHungerTarget>(); if (hunger != null) ctx.Register(hunger);
            var thirst = mgr.Get<IThirstTarget>(); if (thirst != null) ctx.Register(thirst);
            var health = mgr.Get<IHealthTarget>(); if (health != null) ctx.Register(health);
        }
        else
        {
            // Fallback: сцена без PlayerModuleManager (например, старая или собранная вручную).
            if (HungerSystem.Instance != null) ctx.Register<IHungerTarget>(HungerSystem.Instance);
            if (ThirstSystem.Instance != null) ctx.Register<IThirstTarget>(ThirstSystem.Instance);
            if (HealthSystem.Instance != null) ctx.Register<IHealthTarget>(HealthSystem.Instance);
        }
        return ctx;
    }

    private static void ConsumeOne(ItemInstance item, GridContainer container, EquipmentSlotType? slotType)
    {
        var mgr = InventoryManager.Instance;
        if (mgr == null) return;

        item.stackCount--;
        if (item.stackCount <= 0)
        {
            if (container != null) mgr.RemoveItem(item, container);
            else if (slotType.HasValue) mgr.Unequip(slotType.Value);
            else if (mgr.TryLocateItem(item, out var foundContainer, out _))
                mgr.RemoveItem(item, foundContainer);
            else
            {
                var foundSlot = mgr.FindSlotByEquippedItem(item);
                if (foundSlot.HasValue) mgr.Unequip(foundSlot.Value);
            }
        }
        else
        {
            mgr.NotifyChanged();
        }
    }
}
