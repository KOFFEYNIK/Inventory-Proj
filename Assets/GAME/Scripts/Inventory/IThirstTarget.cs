/// <summary>
/// Game-side интерфейс для эффектов, восстанавливающих жажду. Реализуется модулем
/// <see cref="ThirstSystem"/>. Регистрируется в <see cref="IItemEffectContext"/>
/// в <see cref="ItemUseService"/>.
///
/// Если модуль не подключён к игроку — интерфейс просто не зарегистрирован, и
/// <see cref="ConsumableEffect"/> молча пропускает restoreThirst.
/// </summary>
public interface IThirstTarget
{
    void AddThirst(float amount);
}
