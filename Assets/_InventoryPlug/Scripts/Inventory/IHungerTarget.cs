/// <summary>
/// Game-side интерфейс для эффектов, восстанавливающих голод. Реализуется модулем
/// <see cref="HungerSystem"/>. Регистрируется в <see cref="IItemEffectContext"/>
/// в <see cref="ItemUseService"/>.
///
/// Если модуль не подключён к игроку — интерфейс просто не зарегистрирован, и
/// <see cref="ConsumableEffect"/> молча пропускает restoreHunger.
/// </summary>
public interface IHungerTarget
{
    void AddHunger(float amount);
}
