using UnityEngine;

/// <summary>
/// База для всех "эффектов" предметов (расходники, бафы, активируемые предметы).
/// Конкретные эффекты — наследники с собственными полями (например, ConsumableEffect
/// с restoreHunger/restoreThirst, или MagicEffect с manaCost/spellId).
///
/// Эффект ссылается на <see cref="ItemDefinition.consumable"/> и активируется через
/// "Использовать" в инвентаре или хотбар. Конкретная игровая логика (что значит "восстановить
/// голод") вынесена за плагин — её предоставляют сервисы через <see cref="IItemEffectContext"/>.
/// </summary>
public abstract class ItemEffect : ScriptableObject
{
    [Header("Common")]
    [Tooltip("Уменьшать стак на 1 при успешном применении. Если false — предмет остаётся в инвентаре.")]
    public bool consumeOnUse = true;

    [Tooltip("Опциональная подпись действия в контекстном меню (например, 'Съесть', 'Выпить'). " +
             "Если пусто — используется 'Использовать'.")]
    public string verb = "";

    /// <summary>
    /// Применить эффект к контексту. Возвращает true, если хотя бы что-то применилось
    /// (тогда стак будет уменьшен, если <see cref="consumeOnUse"/> = true).
    /// </summary>
    /// <param name="item">Экземпляр предмета, к которому привязан эффект.</param>
    /// <param name="ctx">Контейнер game-side сервисов.</param>
    public abstract bool TryApply(ItemInstance item, IItemEffectContext ctx);
}
