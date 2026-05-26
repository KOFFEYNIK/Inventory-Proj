using System;
using System.Collections.Generic;
using UnityEngine;

// Гарантируем инициализацию ДО любых других MonoBehaviour'ов (особенно InventoryUI),
// иначе InventoryUI.Awake может прочитать Instance == null и не создать слоты экипировки.
[DefaultExecutionOrder(-100)]
public class InventoryManager : MonoBehaviour, IInventoryService
{
    public static InventoryManager Instance { get; private set; }

    public const string SecureCaseContainerId = "SecureCase";

    [Header("Item Database")]
    public ItemDefinition[] itemDatabase;

    [Header("Config")]
    [Tooltip("Описывает количество и размер карманов + список equipment-слотов. " +
             "Если не задано — используется встроенный Tarkov-like дефолт " +
             "(4 кармана 1×1 + 9 equipment-слотов).")]
    public InventoryConfig config;

    public GridContainer[] Pockets { get; private set; } = System.Array.Empty<GridContainer>();

    public Dictionary<EquipmentSlotType, EquipmentSlot> EquipmentSlots { get; }
        = new Dictionary<EquipmentSlotType, EquipmentSlot>();

    private readonly Dictionary<string, GridContainer> allContainers = new();

    public event Action OnInventoryChanged;

    public GridContainer BackpackContainer  => GetEquippedNested(EquipmentSlotType.Backpack);
    public GridContainer RigContainer       => GetEquippedNested(EquipmentSlotType.Rig);
    public GridContainer SecureCaseContainer => GetEquippedNested(EquipmentSlotType.SecureCase);

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        Inventory.Service = this;
        // DontDestroyOnLoad requires root GameObject — InventoryRig is a child of the
        // player prefab, so unparent it once at the earliest singleton (this one runs
        // at DefaultExecutionOrder(-100), before HungerSystem / ThirstSystem / WeaponHotbar /
        // WeaponHotbarUI / InventoryUI Awake — they all share this GameObject, so
        // re-parenting it lifts the whole rig to scene root for them too).
        if (transform.parent != null) transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        InitDefaultContainers();
    }

    void InitDefaultContainers()
    {
        var cfg = config != null ? config : InventoryConfig.CreateDefault();

        // ── Pockets ─────────────────────────────────────────────────────────
        var pocketList = cfg.pockets;
        Pockets = new GridContainer[pocketList != null ? pocketList.Count : 0];
        for (int i = 0; i < Pockets.Length; i++)
        {
            var entry = pocketList[i];
            string id = string.IsNullOrEmpty(entry.id) ? $"Pocket{i + 1}" : entry.id;
            Pockets[i] = new GridContainer(entry.width, entry.height, entry.maxWeight) { containerId = id };
            RegisterContainer(Pockets[i]);
        }

        // ── Equipment slots ─────────────────────────────────────────────────
        if (cfg.equipment != null)
            foreach (var e in cfg.equipment)
                EquipmentSlots[e.type] = new EquipmentSlot(e.type, e.accepts);
    }

    // ── Container registry ────────────────────────────────────────────────────

    public void RegisterContainer(GridContainer c)
    {
        if (c != null && !allContainers.ContainsKey(c.containerId))
            allContainers[c.containerId] = c;
    }

    public void RegisterContainerRecursive(GridContainer c)
    {
        if (c == null) return;
        RegisterContainer(c);
        foreach (var (item, _) in c.GetAllItems())
            if (item.nestedContainer != null)
                RegisterContainerRecursive(item.nestedContainer);
    }

    public GridContainer GetContainer(string id) =>
        allContainers.TryGetValue(id, out var c) ? c : null;

    public IEnumerable<GridContainer> AllContainers => allContainers.Values;

    public bool TryLocateItem(ItemInstance item, out GridContainer container, out Vector2Int pos)
    {
        container = null;
        pos = default;
        if (item == null) return false;
        foreach (var c in allContainers.Values)
        {
            if (c == null) continue;
            if (c.TryGetPosition(item, out pos)) { container = c; return true; }
        }
        return false;
    }

    public ItemDefinition FindDefinition(string id)
    {
        foreach (var d in itemDatabase)
            if (d != null && d.itemId == id) return d;
        return null;
    }

    // ── Equipment API ─────────────────────────────────────────────────────────

    public EquipmentSlot GetSlot(EquipmentSlotType type) =>
        EquipmentSlots.TryGetValue(type, out var s) ? s : null;

    public GridContainer GetEquippedNested(EquipmentSlotType type)
    {
        var slot = GetSlot(type);
        return slot?.EquippedItem?.nestedContainer;
    }

    public bool TryEquipAnyMatchingSlot(ItemInstance item)
    {
        if (item == null) return false;
        foreach (var kv in EquipmentSlots)
        {
            var slot = kv.Value;
            if (slot.IsEmpty && slot.Accepts(item))
                return TryEquip(kv.Key, item);
        }
        return false;
    }

    public bool TryEquip(EquipmentSlotType slotType, ItemInstance item)
    {
        var slot = GetSlot(slotType);
        if (slot == null || !slot.Accepts(item)) return false;
        if (!slot.IsEmpty) return false;

        slot.EquippedItem = item;
        if (item.nestedContainer != null)
            RegisterContainerRecursive(item.nestedContainer);
        OnInventoryChanged?.Invoke();
        return true;
    }

    public ItemInstance Unequip(EquipmentSlotType slotType)
    {
        var slot = GetSlot(slotType);
        if (slot == null || slot.IsEmpty) return null;
        var item = slot.EquippedItem;
        slot.EquippedItem = null;
        OnInventoryChanged?.Invoke();
        return item;
    }

    public EquipmentSlotType? FindSlotByEquippedItem(ItemInstance item)
    {
        if (item == null) return null;
        foreach (var kv in EquipmentSlots)
            if (kv.Value.EquippedItem == item) return kv.Key;
        return null;
    }

    public IEnumerable<GridContainer> GetPickupContainers()
    {
        for (int i = 0; i < Pockets.Length; i++)
            if (Pockets[i] != null) yield return Pockets[i];

        var rig = RigContainer;
        if (rig != null) yield return rig;

        var bp = BackpackContainer;
        if (bp != null) yield return bp;
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public bool MoveItem(ItemInstance item, GridContainer from, GridContainer to, Vector2Int toPos)
    {
        if (!to.CanPlace(item, toPos)) return false;
        from?.Remove(item);
        if (!to.TryPlace(item, toPos))
        {
            if (from != null) TryRestoreToOrigin(item, from);
            return false;
        }
        OnInventoryChanged?.Invoke();
        return true;
    }

    public void RemoveItem(ItemInstance item, GridContainer container)
    {
        container.Remove(item);
        OnInventoryChanged?.Invoke();
    }

    public void NotifyChanged() => OnInventoryChanged?.Invoke();

    private void TryRestoreToOrigin(ItemInstance item, GridContainer origin)
    {
        for (int y = 0; y < origin.height; y++)
            for (int x = 0; x < origin.width; x++)
                if (origin.TryPlace(item, new Vector2Int(x, y))) return;
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    public void Save()
    {
        var save = new InventorySaveData();

        for (int i = 0; i < Pockets.Length; i++)
            CollectContainer(Pockets[i], save);

        foreach (var kv in EquipmentSlots)
        {
            var slot = kv.Value;
            var slotData = new EquipmentSlotSaveData { slotType = (int)kv.Key };
            if (!slot.IsEmpty)
            {
                slotData.equippedInstanceId = slot.EquippedItem.instanceId;
                slotData.equippedItem = MakeItemSaveData(slot.EquippedItem, -1, -1);
                if (slot.EquippedItem.nestedContainer != null)
                    CollectContainer(slot.EquippedItem.nestedContainer, save);
            }
            save.equipmentSlots.Add(slotData);
        }

        InventoryPersistence.Provider.Write(InventoryPersistence.InventoryKey, JsonUtility.ToJson(save, true));
        Debug.Log($"[Inventory] Saved → {InventoryPersistence.InventoryKey}");
    }

    private void CollectContainer(GridContainer c, InventorySaveData save)
    {
        if (c == null) return;
        if (save.containers.Exists(x => x.containerId == c.containerId)) return;

        var cData = new ContainerSaveData
        {
            containerId = c.containerId,
            width       = c.width,
            height      = c.height,
            maxWeight   = c.maxWeight
        };

        foreach (var (item, pos) in c.GetAllItems())
        {
            cData.items.Add(MakeItemSaveData(item, pos.x, pos.y));
            if (item.nestedContainer != null)
                CollectContainer(item.nestedContainer, save);
        }
        save.containers.Add(cData);
    }

    private ItemSaveData MakeItemSaveData(ItemInstance item, int x, int y) => new()
    {
        instanceId        = item.instanceId,
        definitionId      = item.definition.itemId,
        stackCount        = item.stackCount,
        isRotated         = item.isRotated,
        posX              = x,
        posY              = y,
        nestedContainerId = item.nestedContainer?.containerId
    };

    // ── Load ──────────────────────────────────────────────────────────────────

    public void Load()
    {
        var json = InventoryPersistence.Provider.Read(InventoryPersistence.InventoryKey);
        if (string.IsNullOrWhiteSpace(json)) { Debug.LogWarning("[Inventory] No save data found."); return; }

        var save = JsonUtility.FromJson<InventorySaveData>(json);
        var cMap = new Dictionary<string, GridContainer>();

        foreach (var cData in save.containers)
        {
            var c = new GridContainer(cData.width, cData.height, cData.maxWeight)
            {
                containerId = cData.containerId
            };
            cMap[c.containerId] = c;
        }

        // Restore equipped items first (so we know their instanceIds) — needed to
        // skip placing them as grid contents (they have no x/y).
        var equippedById = new Dictionary<string, ItemInstance>();
        foreach (var slotData in save.equipmentSlots)
        {
            if (slotData.equippedItem == null || string.IsNullOrEmpty(slotData.equippedItem.definitionId))
                continue;
            var def = FindDefinition(slotData.equippedItem.definitionId);
            if (def == null) continue;

            var inst = new ItemInstance
            {
                instanceId  = slotData.equippedItem.instanceId,
                definition  = def,
                stackCount  = slotData.equippedItem.stackCount,
                isRotated   = slotData.equippedItem.isRotated,
                nestedContainer = !string.IsNullOrEmpty(slotData.equippedItem.nestedContainerId) &&
                                  cMap.TryGetValue(slotData.equippedItem.nestedContainerId, out var nested)
                                  ? nested : null
            };
            equippedById[inst.instanceId] = inst;
        }

        foreach (var cData in save.containers)
        {
            var c = cMap[cData.containerId];
            foreach (var iData in cData.items)
            {
                if (equippedById.ContainsKey(iData.instanceId)) continue;
                var def = FindDefinition(iData.definitionId);
                if (def == null) continue;

                var item = new ItemInstance
                {
                    instanceId  = iData.instanceId,
                    definition  = def,
                    stackCount  = iData.stackCount,
                    isRotated   = iData.isRotated,
                    nestedContainer = !string.IsNullOrEmpty(iData.nestedContainerId) &&
                                      cMap.TryGetValue(iData.nestedContainerId, out var nested)
                                      ? nested : null
                };
                c.TryPlace(item, new Vector2Int(iData.posX, iData.posY));
            }
        }

        allContainers.Clear();
        foreach (var kv in cMap) allContainers[kv.Key] = kv.Value;

        // Wire pockets back. Используется тот же config, что был при InitDefaultContainers —
        // размер/id берутся оттуда, чтобы количество совпадало с UI.
        var cfg = config != null ? config : InventoryConfig.CreateDefault();
        var pocketEntries = cfg.pockets;
        int pocketCount = pocketEntries != null ? pocketEntries.Count : 0;
        Pockets = new GridContainer[pocketCount];
        for (int i = 0; i < pocketCount; i++)
        {
            var entry = pocketEntries[i];
            string id = string.IsNullOrEmpty(entry.id) ? $"Pocket{i + 1}" : entry.id;
            Pockets[i] = cMap.TryGetValue(id, out var p)
                ? p
                : new GridContainer(entry.width, entry.height, entry.maxWeight) { containerId = id };
            RegisterContainer(Pockets[i]);
        }

        // Restore equipment assignments
        foreach (var kv in EquipmentSlots) kv.Value.EquippedItem = null;
        foreach (var slotData in save.equipmentSlots)
        {
            var slotType = (EquipmentSlotType)slotData.slotType;
            if (!EquipmentSlots.TryGetValue(slotType, out var slot)) continue;
            if (string.IsNullOrEmpty(slotData.equippedInstanceId)) continue;
            if (equippedById.TryGetValue(slotData.equippedInstanceId, out var inst))
                slot.EquippedItem = inst;
        }

        Debug.Log("[Inventory] Loaded.");
        OnInventoryChanged?.Invoke();
    }
}
