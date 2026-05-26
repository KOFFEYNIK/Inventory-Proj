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

    public HotbarSlot[] Slots { get; private set; }
    public int ActiveIndex { get; private set; }
    public int SlotCount => Slots != null ? Slots.Length : 0;

    public event Action OnSlotsChanged;
    public event Action<int> OnActiveChanged;

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

    void Start()
    {
        var mgr = InventoryManager.Instance;
        if (mgr != null) mgr.OnInventoryChanged += OnInventoryChanged;
        SyncEquipmentSlots();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= OnInventoryChanged;
    }

    void Update()
    {
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
                if (slot.reservedPrefab == null) return;
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
