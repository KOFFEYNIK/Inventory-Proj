using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Контекстное меню для предметов в инвентаре. Все пункты — настраиваемые: и подписи,
/// и видимость, и доп.пункты можно добавить через инспектор. Если все стандартные
/// пункты выключены и <see cref="extraEntries"/> пуст — меню не откроется.
/// </summary>
public class ContextMenuUI : MonoBehaviour
{
    public static ContextMenuUI Instance { get; private set; }

    public enum BuiltinAction { Use, Inspect, Open, Split, Drop }

    [Serializable]
    public class BuiltinEntryConfig
    {
        public BuiltinAction action;
        public string        label   = "";
        public bool          enabled = true;
    }

    /// <summary>Произвольный кастомный пункт (для проектных нужд). Колбэк назначается
    /// в рантайме через <see cref="AddExtraEntry"/>.</summary>
    [Serializable]
    public class ExtraEntry
    {
        public string label = "Действие";
        public bool   enabled = true;
        /// <summary>Назначается из кода. Получает текущий <see cref="ItemInstance"/>.</summary>
        [NonSerialized] public Action<ItemInstance> handler;
        /// <summary>Необязательный фильтр видимости: если задан и вернул false для предмета —
        /// пункт не показывается. Назначается из кода (например, «только для магазинов»).</summary>
        [NonSerialized] public Func<ItemInstance, bool> visible;
    }

    [Header("Built-in entries (labels editable)")]
    public List<BuiltinEntryConfig> entries = new()
    {
        new BuiltinEntryConfig { action = BuiltinAction.Use,     label = "Использовать", enabled = true },
        new BuiltinEntryConfig { action = BuiltinAction.Inspect, label = "Осмотреть",    enabled = true },
        new BuiltinEntryConfig { action = BuiltinAction.Open,    label = "Открыть",      enabled = true },
        new BuiltinEntryConfig { action = BuiltinAction.Split,   label = "Разделить",    enabled = true },
        new BuiltinEntryConfig { action = BuiltinAction.Drop,    label = "Выкинуть",     enabled = true },
    };

    [Header("Extra entries (project-specific). Handler is assigned in code.")]
    public List<ExtraEntry> extraEntries = new();

    [Header("Style")]
    public Vector2 buttonSize = new(160f, 30f);
    public float   buttonGap  = 2f;
    public float   padding    = 4f;
    public int     fontSize   = 13;
    public Color   panelColor     = new(0.08f, 0.08f, 0.08f, 0.97f);
    public Color   buttonColor    = new(0.18f, 0.18f, 0.18f, 1f);
    public Color   highlightColor = new(0.32f, 0.32f, 0.32f, 1f);
    public Color   pressedColor   = new(0.12f, 0.12f, 0.12f, 1f);

    private GameObject       panel;
    private RectTransform    panelRt;
    private ItemInstance     targetItem;
    private GridContainer    targetContainer;
    private EquipmentSlotUI  targetSlot;
    private int              shownFrame = -1;
    private readonly List<GameObject> spawnedButtons = new();

    // Параметры броска при выбрасывании предмета из инвентаря
    private const float DropForwardOffset = 0.35f;
    private const float DropBellyDrop     = 0.15f;
    private const float DropForwardSpeed  = 3.0f;
    private const float DropUpSpeed       = 1.2f;
    private const float DropFallbackUp    = 1.0f;

    void Awake()
    {
        Instance = this;
        EnsureSplitEntry();
        BuildPanel();
        panel.SetActive(false);
    }

    /// <summary>Список <see cref="entries"/> мог быть сериализован в сценах/префабах ДО появления
    /// пункта Split — Unity не добавит его автоматически. Вставляем перед Drop, если отсутствует.</summary>
    private void EnsureSplitEntry()
    {
        if (entries == null) { entries = new List<BuiltinEntryConfig>(); return; }
        if (entries.Exists(e => e != null && e.action == BuiltinAction.Split)) return;

        var split = new BuiltinEntryConfig { action = BuiltinAction.Split, label = "Разделить", enabled = true };
        int dropIdx = entries.FindIndex(e => e != null && e.action == BuiltinAction.Drop);
        if (dropIdx >= 0) entries.Insert(dropIdx, split);
        else entries.Add(split);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildPanel()
    {
        panel = new GameObject("ContextMenu", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        panel.GetComponent<Image>().color = panelColor;

        panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0f, 1f);
    }

    /// <summary>
    /// Добавить произвольный пункт меню из кода. Если pункт с такой подписью уже есть —
    /// перезаписывает колбэк. Сохраняется до <see cref="OnDestroy"/>.
    /// </summary>
    public void AddExtraEntry(string label, Action<ItemInstance> handler, bool enabled = true,
                              Func<ItemInstance, bool> visible = null)
    {
        if (string.IsNullOrEmpty(label) || handler == null) return;
        var existing = extraEntries.Find(e => e.label == label);
        if (existing != null) { existing.handler = handler; existing.enabled = enabled; existing.visible = visible; return; }
        extraEntries.Add(new ExtraEntry { label = label, enabled = enabled, handler = handler, visible = visible });
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(ItemInstance item, GridContainer container, Vector2 screenPos, EquipmentSlotUI slot = null)
    {
        if (item == null || item.definition == null) return;

        // Toggle: повторный RMB на том же самом предмете закрывает меню.
        if (panel.activeSelf && targetItem == item)
        {
            Hide();
            return;
        }

        targetItem      = item;
        targetContainer = container;
        targetSlot      = slot;

        RebuildButtons(item);

        if (spawnedButtons.Count == 0)
        {
            Hide();
            return;
        }

        // Must be last sibling so it renders on top of the inventory panel.
        panel.transform.SetAsLastSibling();
        panel.SetActive(true);
        shownFrame = Time.frameCount;

        // Position near cursor (screen space overlay = screen coords)
        panelRt.position = new Vector3(screenPos.x, screenPos.y, 0f);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
        targetItem      = null;
        targetContainer = null;
        targetSlot      = null;
    }

    // ── Buttons ───────────────────────────────────────────────────────────────

    private void RebuildButtons(ItemInstance item)
    {
        // Снести старые кнопки (их состав зависит от item: «Открыть» появляется только для контейнеров).
        for (int i = 0; i < spawnedButtons.Count; i++)
            if (spawnedButtons[i] != null) Destroy(spawnedButtons[i]);
        spawnedButtons.Clear();

        int row = 0;

        // Built-in entries в порядке, заданном в инспекторе
        foreach (var e in entries)
        {
            if (e == null || !e.enabled) continue;
            if (string.IsNullOrEmpty(e.label)) continue;
            if (e.action == BuiltinAction.Open && !item.definition.isContainer) continue;
            // «Разделить» — только для стака >1, который лежит в сетке (не в слоте экипировки).
            if (e.action == BuiltinAction.Split &&
                (targetContainer == null || !item.IsStackable || item.stackCount < 2)) continue;

            var act = e.action;
            AddBtn(e.label, row++, () => OnBuiltin(act));
        }

        // Extra entries
        foreach (var e in extraEntries)
        {
            if (e == null || !e.enabled || e.handler == null) continue;
            if (string.IsNullOrEmpty(e.label)) continue;
            if (e.visible != null && !e.visible(item)) continue; // фильтр видимости (напр. только магазины)
            var handler = e.handler;
            AddBtn(e.label, row++, () => { handler.Invoke(targetItem); });
        }

        // Подогнать размер панели под количество видимых пунктов
        int count = spawnedButtons.Count;
        if (count > 0)
        {
            float w = buttonSize.x + padding * 2f;
            float h = count * buttonSize.y + (count - 1) * buttonGap + padding * 2f;
            panelRt.sizeDelta = new Vector2(w, h);
        }
    }

    private void AddBtn(string label, int row, Action onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(panel.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(padding, -(padding + row * (buttonSize.y + buttonGap)));
        rt.sizeDelta = buttonSize;

        var img = go.GetComponent<Image>();
        img.color = buttonColor;

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cols = btn.colors;
        cols.highlightedColor = highlightColor;
        cols.pressedColor     = pressedColor;
        btn.colors            = cols;
        btn.onClick.AddListener(() => { onClick?.Invoke(); Hide(); });

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(6f, 0f); trt.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.font      = HudFont.Get();
        text.text      = label;
        text.alignment = TextAnchor.MiddleLeft;
        text.fontSize  = fontSize;
        text.color     = Color.white;
        text.raycastTarget = false;

        spawnedButtons.Add(go);
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void OnBuiltin(BuiltinAction action)
    {
        if (targetItem == null) return;
        switch (action)
        {
            case BuiltinAction.Use:
                UseTargetItem();
                break;

            case BuiltinAction.Inspect:
                {
                    Debug.Log($"[ContextMenuUI] inspect → {targetItem.definition.displayName}");
                    var inspector = InspectWindowUI.Instance;
                    if (inspector == null)
                    {
                        Debug.LogWarning("[ContextMenuUI] InspectWindowUI.Instance was null — creating on canvas");
                        var canvas = GetComponentInParent<Canvas>();
                        var host   = canvas != null ? canvas.gameObject : gameObject;
                        inspector  = host.AddComponent<InspectWindowUI>();
                        inspector.RootCanvas = canvas;
                    }
                    inspector.Show(targetItem);
                }
                break;

            case BuiltinAction.Open:
                if (targetItem.nestedContainer != null)
                    InventoryUI.Instance?.OpenNestedContainer(
                        targetItem.nestedContainer, targetItem.definition.displayName);
                break;

            case BuiltinAction.Split:
                SplitTargetStack();
                break;

            case BuiltinAction.Drop:
                {
                    var dropped = targetItem;
                    if (targetContainer != null)
                        InventoryManager.Instance?.RemoveItem(dropped, targetContainer);
                    else if (targetSlot != null)
                        InventoryManager.Instance?.Unequip(targetSlot.SlotType);
                    InventoryUI.Instance?.Refresh();

                    DropItemFromPlayer(dropped);
                }
                break;
        }
    }

    // ── Split (разделение стака) ─────────────────────────────────────────────

    /// <summary>Отделяет половину (округление вверх) текущего стака в новый предмет и кладёт
    /// его в первую свободную ячейку того же контейнера. Если места нет — стак не трогаем.</summary>
    private void SplitTargetStack()
    {
        if (targetItem == null || targetContainer == null) return;
        if (!targetItem.IsStackable || targetItem.stackCount < 2) return;

        int take = Mathf.CeilToInt(targetItem.stackCount / 2f);
        var half = new ItemInstance
        {
            instanceId = Guid.NewGuid().ToString(),
            definition = targetItem.definition,
            stackCount = take,
            isRotated  = targetItem.isRotated
        };

        if (!TryPlaceAnywhere(targetContainer, half)) return; // нет места — отмена

        targetItem.stackCount -= take;
        InventoryManager.Instance?.NotifyChanged();
        InventoryUI.Instance?.Refresh();
    }

    private static bool TryPlaceAnywhere(GridContainer container, ItemInstance item)
    {
        for (int y = 0; y < container.height; y++)
            for (int x = 0; x < container.width; x++)
                if (container.TryPlace(item, new Vector2Int(x, y)))
                    return true;
        return false;
    }

    // ── Drop (выброс из инвентаря) ───────────────────────────────────────────

    private static void DropItemFromPlayer(ItemInstance dropped)
    {
        if (dropped == null || dropped.definition == null) return;

        var player = FindPlayerObject();

        // Для 2D-предметов считаем origin/forward в 2D-плоскости (XY), для 3D — как раньше.
        // Диспатчер WorldItemSpawner.SpawnDropped сам выберет 2D или 3D-спавнер по
        // ItemDefinition.worldMode.
        Vector3 origin, forward;
        if (dropped.definition.worldMode == WorldMode.TwoD)
        {
            ResolveDrop2D(player, out origin, out forward);
        }
        else
        {
            origin  = ResolveBellyOrigin(player);
            forward = ResolveAimForward(player);
        }

        Vector3 spawnPos = origin + forward * DropForwardOffset;
        Vector3 velocity = forward * DropForwardSpeed + Vector3.up * DropUpSpeed;

        WorldItemSpawner.SpawnDropped(dropped, spawnPos, velocity);
    }

    /// <summary>Origin/forward для дропа в 2D-сцене. Origin — позиция игрока (без капсулы),
    /// forward — горизонтальное направление взгляда (по знаку transform.localScale.x: вправо/влево).
    /// Если игрок не найден — нулевая точка + вправо.</summary>
    private static void ResolveDrop2D(GameObject player, out Vector3 origin, out Vector3 forward)
    {
        if (player == null)
        {
            origin  = Vector3.zero;
            forward = Vector3.right;
            return;
        }
        origin = player.transform.position;
        // В 2D обычно «куда смотрит игрок» определяется зеркалом спрайта: scale.x < 0 = влево.
        float sx = player.transform.localScale.x;
        forward = (sx < 0f ? Vector3.left : Vector3.right);
    }

    private static GameObject FindPlayerObject()
    {
        var byTag = GameObject.FindGameObjectWithTag("Player");
        if (byTag != null) return byTag;
        var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        return pc != null ? pc.gameObject : null;
    }

    private static Vector3 ResolveBellyOrigin(GameObject player)
    {
        if (player == null)
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position : Vector3.zero;
        }

        var capsule = player.GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
            return capsule.bounds.center - Vector3.up * DropBellyDrop;

        var cc = player.GetComponentInChildren<CharacterController>();
        if (cc != null)
            return cc.bounds.center - Vector3.up * DropBellyDrop;

        return player.transform.position + Vector3.up * DropFallbackUp;
    }

    private static Vector3 ResolveAimForward(GameObject player)
    {
        var cam = Camera.main;
        Vector3 src = cam != null
            ? cam.transform.forward
            : (player != null ? player.transform.forward : Vector3.forward);

        Vector3 flat = new Vector3(src.x, 0f, src.z);
        if (flat.sqrMagnitude < 0.001f)
        {
            flat = player != null
                ? new Vector3(player.transform.forward.x, 0f, player.transform.forward.z)
                : Vector3.forward;
            if (flat.sqrMagnitude < 0.001f) flat = Vector3.forward;
        }
        return flat.normalized;
    }

    // ── Use (consumable) ─────────────────────────────────────────────────────

    private void UseTargetItem()
    {
        if (targetItem == null) return;
        EquipmentSlotType? slotType = targetSlot != null ? targetSlot.SlotType : (EquipmentSlotType?)null;
        if (ItemUseService.TryUse(targetItem, targetContainer, slotType))
            InventoryUI.Instance?.Refresh();
    }

    void Update()
    {
        if (!panel.activeSelf) return;
        if (Time.frameCount == shownFrame) return; // ignore the frame we just opened on
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            Hide();
    }
}
