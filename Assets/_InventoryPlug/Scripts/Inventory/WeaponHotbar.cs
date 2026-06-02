using System;
using UnityEngine;

/// <summary>
/// Хотбар с произвольным количеством слотов, описываемых через <see cref="HotbarConfig"/>
/// (часть <see cref="InventoryConfig"/>). Слоты бывают трёх типов:
///
///   • <see cref="HotbarSlotKind.Reserved"/>        — зарезервированный (нож / стартовое оружие); префаб задан в HotbarSlotEntry.
///   • <see cref="HotbarSlotKind.EquipmentWeapon"/> — зеркалит equipment-слот (PrimaryWeapon / Sidearm / etc).
///   • <see cref="HotbarSlotKind.UserItem"/>        — пользовательский (drag-and-drop любого ItemInstance).
///
/// Активация:
///   • Reserved — берёт в руки reservedPrefab из entry.
///   • EquipmentWeapon — берёт в руки оружие из соответствующего слота, если оно надето.
///   • UserItem — оружие → в руки; расходник → активирует эффект.
///
/// Клавиша <see cref="HotbarConfig.reservedHotkey"/> — мгновенный переход на
/// <see cref="HotbarConfig.reservedHotkeyTarget"/> (по умолчанию H → слот 0).
/// </summary>
[DefaultExecutionOrder(-85)]
public class WeaponHotbar : MonoBehaviour
{
    public class HotbarSlot
    {
        public HotbarSlotKind     kind;
        public EquipmentSlotType? equipmentSource; // только для EquipmentWeapon
        public GameObject         reservedPrefab;  // только для Reserved
        public ItemInstance       userItem;        // только для UserItem
    }

    public static WeaponHotbar Instance { get; private set; }

    [Header("Scene Overrides")]
    [Tooltip("Сценовый тогл. true (default) — берётся из HotbarConfig.enabled. " +
             "false — принудительно отключить хотбар целиком, независимо от конфига.")]
    public bool hotbarEnabledOverride = true;

    [Tooltip("Сценовый override количества слотов. 0 = использовать HotbarConfig.slotCount / slots.Count. " +
             ">0 = принудительно столько слотов в этой сцене.")]
    [Min(0)] public int slotCountOverride = 0;

    public HotbarSlot[] Slots { get; private set; }
    public int ActiveIndex { get; private set; }
    public int SlotCount => Slots != null ? Slots.Length : 0;

    /// <summary>Активен ли хотбар сейчас (с учётом scene-override + config.enabled).</summary>
    public bool IsHotbarEnabled { get; private set; } = true;

    public event Action OnSlotsChanged;
    public event Action<int> OnActiveChanged;

    /// <summary>Срабатывает при включении/выключении хотбара (UI прячет/показывает панель).</summary>
    public event Action<bool> OnEnabledChanged;

    private HotbarConfig hotbarConfig;
    private int firstUserSlotIndex;

    private static readonly KeyCode[] DigitKeys =
    {
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
        KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
    };

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        BuildFromConfig();
    }

    private void BuildFromConfig()
    {
        var mgr = InventoryManager.Instance;
        hotbarConfig = mgr != null && mgr.config != null && mgr.config.hotbar != null && mgr.config.hotbar.slots.Count > 0
            ? mgr.config.hotbar
            : HotbarConfig.CreateDefault();

        // Сцен-override на размер. Иначе берём из HotbarConfig.slotCount (если задан).
        if (slotCountOverride > 0)
        {
            int target = slotCountOverride;
            while (hotbarConfig.slots.Count < target) hotbarConfig.slots.Add(new HotbarSlotEntry { kind = HotbarSlotKind.UserItem });
            if (hotbarConfig.slots.Count > target) hotbarConfig.slots.RemoveRange(target, hotbarConfig.slots.Count - target);
        }
        else
        {
            hotbarConfig.ApplySlotCountOverride();
        }

        // Сцен-override на enabled. Если в инспекторе сняли галку — выключаем независимо от конфига.
        IsHotbarEnabled = hotbarEnabledOverride && hotbarConfig.enabled;

        Slots = new HotbarSlot[hotbarConfig.slots.Count];
        firstUserSlotIndex = Slots.Length; // если user-слотов нет
        for (int i = 0; i < Slots.Length; i++)
        {
            var entry = hotbarConfig.slots[i];
            Slots[i] = new HotbarSlot
            {
                kind            = entry.kind,
                equipmentSource = entry.kind == HotbarSlotKind.EquipmentWeapon ? entry.equipmentSource : (EquipmentSlotType?)null,
                reservedPrefab  = entry.kind == HotbarSlotKind.Reserved ? entry.reservedPrefab : null,
            };
            if (entry.kind == HotbarSlotKind.UserItem && i < firstUserSlotIndex)
                firstUserSlotIndex = i;
        }

        ActiveIndex = Mathf.Clamp(hotbarConfig.reservedHotkeyTarget, 0, Mathf.Max(0, Slots.Length - 1));
    }

    /// <summary>Включить/выключить хотбар в рантайме. UI сам подхватит через OnEnabledChanged.</summary>
    public void SetHotbarEnabled(bool enabled)
    {
        if (hotbarEnabledOverride == enabled) return;
        hotbarEnabledOverride = enabled;
        IsHotbarEnabled = hotbarEnabledOverride && (hotbarConfig != null && hotbarConfig.enabled);
        OnEnabledChanged?.Invoke(IsHotbarEnabled);
    }

    /// <summary>Сменить количество слотов в рантайме (триггерит ребилд + UI-обновление).</summary>
    public void SetSlotCount(int count)
    {
        if (count < 0) count = 0;
        if (slotCountOverride == count) return;
        slotCountOverride = count;
        BuildFromConfig();
        SyncEquipmentSlots();
        OnSlotsChanged?.Invoke();
    }

#if UNITY_EDITOR
    // Live-edit поддержка: правка hotbarEnabledOverride / slotCountOverride в инспекторе
    // во время игры тут же перестраивает хотбар и UI.
    private bool   lastEnabledOverride;
    private int    lastSlotCountOverride;

    void OnValidate()
    {
        if (slotCountOverride < 0) slotCountOverride = 0;
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;
        if (lastEnabledOverride != hotbarEnabledOverride)
        {
            lastEnabledOverride = hotbarEnabledOverride;
            IsHotbarEnabled = hotbarEnabledOverride && (hotbarConfig != null && hotbarConfig.enabled);
            OnEnabledChanged?.Invoke(IsHotbarEnabled);
        }
        if (lastSlotCountOverride != slotCountOverride)
        {
            lastSlotCountOverride = slotCountOverride;
            BuildFromConfig();
            SyncEquipmentSlots();
            OnSlotsChanged?.Invoke();
        }
    }
#endif

    // Менеджер, чьё OnInventoryChanged мы слушаем (для корректной отписки при свапе).
    private InventoryManager subscribedMgr;

    void Start()
    {
        InventoryManager.OnActiveChanged += HandleActiveChanged;
        SubscribeTo(InventoryManager.Instance);
        SyncEquipmentSlots();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        InventoryManager.OnActiveChanged -= HandleActiveChanged;
        if (subscribedMgr != null)
        {
            subscribedMgr.OnInventoryChanged -= OnInventoryChanged;
            subscribedMgr = null;
        }
    }

    private void SubscribeTo(InventoryManager mgr)
    {
        if (subscribedMgr == mgr) return;
        if (subscribedMgr != null) subscribedMgr.OnInventoryChanged -= OnInventoryChanged;
        subscribedMgr = mgr;
        if (subscribedMgr != null) subscribedMgr.OnInventoryChanged += OnInventoryChanged;
    }

    private void HandleActiveChanged(InventoryManager mgr)
    {
        SubscribeTo(mgr);
        // Хотбар привязан к equipment-слотам активного менеджера → при свапе пересобираем.
        BuildFromConfig();
        SyncEquipmentSlots();
        OnInventoryChanged();
    }

    void Update()
    {
        if (!IsHotbarEnabled) return;

        int digitCount = Mathf.Min(DigitKeys.Length, Slots.Length);
        for (int i = 0; i < digitCount; i++)
            if (Input.GetKeyDown(DigitKeys[i])) { SelectSlot(i); return; }

        if (Input.GetKeyDown(hotbarConfig.reservedHotkey))
            SelectSlot(hotbarConfig.reservedHotkeyTarget);
    }

    // ── Slot state ────────────────────────────────────────────────────────────

    public bool IsSlotEmpty(int index)
    {
        if (index < 0 || index >= Slots.Length) return true;
        var slot = Slots[index];
        switch (slot.kind)
        {
            case HotbarSlotKind.Reserved:        return slot.reservedPrefab == null;
            case HotbarSlotKind.EquipmentWeapon: return GetEquipmentItem(slot.equipmentSource.Value) == null;
            case HotbarSlotKind.UserItem:        return slot.userItem == null;
            default:                              return true;
        }
    }

    /// <summary>Возвращает ItemInstance для подсветки/иконки. Для reserved-слота — null (нет ItemInstance).</summary>
    public ItemInstance GetSlotItem(int index)
    {
        if (index < 0 || index >= Slots.Length) return null;
        var slot = Slots[index];
        switch (slot.kind)
        {
            case HotbarSlotKind.EquipmentWeapon: return GetEquipmentItem(slot.equipmentSource.Value);
            case HotbarSlotKind.UserItem:        return slot.userItem;
            default:                              return null;
        }
    }

    public GameObject GetActiveWeaponPrefab()
    {
        if (ActiveIndex < 0 || ActiveIndex >= Slots.Length) return null;
        var slot = Slots[ActiveIndex];
        switch (slot.kind)
        {
            case HotbarSlotKind.Reserved:
                return slot.reservedPrefab;
            case HotbarSlotKind.EquipmentWeapon:
            {
                var item = GetEquipmentItem(slot.equipmentSource.Value);
                return item?.definition?.weaponPrefab;
            }
            case HotbarSlotKind.UserItem:
                return slot.userItem?.definition?.weaponPrefab;
        }
        return null;
    }

    /// <summary>Слот считается user-assignable, если он объявлен как UserItem или Empty (свободно для drag).</summary>
    public bool IsUserSlot(int index)
    {
        if (index < 0 || index >= Slots.Length) return false;
        var k = Slots[index].kind;
        return k == HotbarSlotKind.UserItem || k == HotbarSlotKind.Empty;
    }

    /// <summary>Имя клавиши для подсказки в UI (1..9, 0). Для слотов с индексом ≥ 10 — пустая строка.</summary>
    public static string GetKeyLabel(int index)
    {
        if (index < 0 || index >= DigitKeys.Length) return "";
        return index == 9 ? "0" : (index + 1).ToString();
    }

    // ── Selection ─────────────────────────────────────────────────────────────

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= Slots.Length) return;
        var slot = Slots[index];

        switch (slot.kind)
        {
            case HotbarSlotKind.Reserved:
                // reservedPrefab != null → взять в руки заданный префаб (нож/стартовое оружие).
                // reservedPrefab == null → «пустые руки»: слот всё равно становится активным,
                // потребитель (например, PBS2D_InventoryBridge) получает null-префаб и снимает
                // текущее оружие. Так H / «1» возвращают бойца к безоружному состоянию.
                SetActive(index);
                return;

            case HotbarSlotKind.EquipmentWeapon:
            {
                var item = GetEquipmentItem(slot.equipmentSource.Value);
                if (item == null || item.definition == null || item.definition.weaponPrefab == null)
                {
                    Debug.Log($"[Hotbar] Слот {GetKeyLabel(index)} ({slot.equipmentSource}) пуст или не оружие.");
                    return;
                }
                SetActive(index);
                return;
            }

            case HotbarSlotKind.UserItem:
            {
                var item = slot.userItem;
                if (item == null) return;
                if (item.definition.weaponPrefab != null)
                {
                    SetActive(index);
                    return;
                }
                if (item.definition.consumable != null)
                {
                    var mgr = InventoryManager.Instance;
                    GridContainer container = null;
                    EquipmentSlotType? slotType = null;
                    if (mgr != null)
                    {
                        slotType = mgr.FindSlotByEquippedItem(item);
                        if (!slotType.HasValue) mgr.TryLocateItem(item, out container, out _);
                    }
                    // UI обновится сам — он подписан на OnInventoryChanged / OnVitalsChanged / OnHealthChanged.
                    ItemUseService.TryUse(item, container, slotType);
                    return;
                }
                Debug.Log($"[Hotbar] {item.definition.displayName} нельзя ни взять в руки, ни использовать.");
                return;
            }
        }
    }

    private void SetActive(int index)
    {
        if (ActiveIndex == index) return;
        ActiveIndex = index;
        OnActiveChanged?.Invoke(index);
    }

    // ── User slot assignment ──────────────────────────────────────────────────

    public bool AssignUserSlot(int slotIndex, ItemInstance item)
    {
        if (!IsUserSlot(slotIndex) || item == null) return false;

        for (int i = 0; i < Slots.Length; i++)
            if (i != slotIndex && Slots[i].userItem == item)
            {
                Slots[i].userItem = null;
                if (Slots[i].kind == HotbarSlotKind.UserItem) Slots[i].kind = HotbarSlotKind.Empty;
            }

        Slots[slotIndex].userItem = item;
        Slots[slotIndex].kind = HotbarSlotKind.UserItem;
        OnSlotsChanged?.Invoke();
        return true;
    }

    public void ClearUserSlot(int slotIndex)
    {
        if (!IsUserSlot(slotIndex)) return;
        if (Slots[slotIndex].userItem == null && Slots[slotIndex].kind == HotbarSlotKind.Empty) return;

        Slots[slotIndex].userItem = null;
        Slots[slotIndex].kind = HotbarSlotKind.Empty;

        if (ActiveIndex == slotIndex) SetActive(hotbarConfig.reservedHotkeyTarget);
        OnSlotsChanged?.Invoke();
    }

    // ── Inventory sync ────────────────────────────────────────────────────────

    private void OnInventoryChanged()
    {
        SyncEquipmentSlots();
        PruneOrphanUserItems();
    }

    private void SyncEquipmentSlots()
    {
        var mgr = InventoryManager.Instance;
        if (mgr == null) return;

        bool activeWeaponGone = false;
        if (ActiveIndex >= 0 && ActiveIndex < Slots.Length &&
            Slots[ActiveIndex].kind == HotbarSlotKind.EquipmentWeapon)
        {
            var src = Slots[ActiveIndex].equipmentSource;
            if (src.HasValue && GetEquipmentItem(src.Value) == null)
                activeWeaponGone = true;
        }

        if (activeWeaponGone) SetActive(hotbarConfig.reservedHotkeyTarget);
        OnSlotsChanged?.Invoke();
    }

    private void PruneOrphanUserItems()
    {
        var mgr = InventoryManager.Instance;
        if (mgr == null) return;
        bool changed = false;
        for (int i = 0; i < Slots.Length; i++)
        {
            if (Slots[i].kind != HotbarSlotKind.UserItem) continue;
            var item = Slots[i].userItem;
            if (item == null) continue;
            if (ItemStillInInventory(mgr, item)) continue;

            Slots[i].userItem = null;
            Slots[i].kind = HotbarSlotKind.Empty;
            changed = true;
            if (ActiveIndex == i) SetActive(hotbarConfig.reservedHotkeyTarget);
        }
        if (changed) OnSlotsChanged?.Invoke();
    }

    private static bool ItemStillInInventory(InventoryManager mgr, ItemInstance item)
    {
        if (mgr.FindSlotByEquippedItem(item).HasValue) return true;
        return mgr.TryLocateItem(item, out _, out _);
    }

    private static ItemInstance GetEquipmentItem(EquipmentSlotType type)
    {
        var mgr = InventoryManager.Instance;
        return mgr?.GetSlot(type)?.EquippedItem;
    }
}
