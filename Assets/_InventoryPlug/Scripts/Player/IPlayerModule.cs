/// <summary>
/// Маркер-интерфейс для подключаемых player-модулей (голод, жажда, здоровье и т.д.).
/// <see cref="PlayerModuleManager"/> автоматически собирает все компоненты с этим
/// интерфейсом, прикреплённые к тому же GameObject (InventoryRig).
///
/// Чтобы добавить новый модуль:
///   1) Реализуй MonoBehaviour, имплементирующий IPlayerModule.
///   2) Положи его на тот же GameObject, где висит PlayerModuleManager.
///   3) Если эффекты должны его дёргать — реализуй также свой target-интерфейс
///      (например IHungerTarget) и регистрируй в IItemEffectContext через
///      ItemUseService.BuildContext().
/// </summary>
public interface IPlayerModule
{
    /// <summary>Имя для отладочного вывода и инспектора.</summary>
    string ModuleName { get; }
}
