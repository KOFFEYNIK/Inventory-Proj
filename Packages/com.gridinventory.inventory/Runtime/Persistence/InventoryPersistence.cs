/// <summary>
/// Глобальная точка инжекции <see cref="ISaveProvider"/> для всех модулей инвентаря.
/// По умолчанию используется <see cref="FileSaveProvider"/> (файлы под persistentDataPath).
///
/// Чтобы подменить (например, на PlayerPrefs или облачный провайдер) — присвой
/// <see cref="Provider"/> из кода проекта до первого Save/Load.
///
/// Стандартные ключи:
///   "inventory"        — основной слот инвентаря (контейнеры + экипировка).
///   "inventory_layout" — пользовательские overrides UI-лейаута.
/// </summary>
public static class InventoryPersistence
{
    public const string InventoryKey = "inventory";
    public const string LayoutKey    = "inventory_layout";

    private static ISaveProvider provider;

    public static ISaveProvider Provider
    {
        get => provider ??= new FileSaveProvider();
        set => provider = value;
    }
}
