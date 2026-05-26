/// <summary>
/// Тип слота хотбара. Раньше жил как nested-enum в WeaponHotbar — теперь top-level в плагине,
/// чтобы конфиг был сериализуемым без зависимости от game-side класса.
/// </summary>
public enum HotbarSlotKind
{
    /// <summary>Пустой слот. Пользователь может drag-and-drop сюда любой предмет.</summary>
    Empty = 0,

    /// <summary>Зарезервированный слот: предмет (обычно нож / стартовое оружие) задаётся префабом в config.</summary>
    Reserved = 1,

    /// <summary>Слот авто-привязан к одному из equipment-слотов (PrimaryWeapon / Sidearm / etc).</summary>
    EquipmentWeapon = 2,

    /// <summary>Слот пользовательский — заполняется через drag-and-drop любого ItemInstance.</summary>
    UserItem = 3,
}
