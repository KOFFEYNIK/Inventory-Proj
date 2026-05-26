using System;
using UnityEngine;
using UnityEngine.UI;

public class ContextMenuUI : MonoBehaviour
{
    public static ContextMenuUI Instance { get; private set; }

    private GameObject       panel;
    private ItemInstance     targetItem;
    private GridContainer    targetContainer;
    private EquipmentSlotUI  targetSlot;
    private int              shownFrame = -1;

    private const float BtnW = 160f, BtnH = 30f, BtnGap = 2f, Pad = 4f;

    // Параметры броска при выбрасывании предмета из инвентаря
    private const float DropForwardOffset = 0.35f; // отступ вперёд от тела игрока (m), чтобы не клипало в коллайдер
    private const float DropBellyDrop     = 0.15f; // сместить чуть ниже центра капсулы, к животу (m). 0 = центр капсулы (~грудь)
    private const float DropForwardSpeed  = 3.0f;  // скорость броска вперёд (m/s)
    private const float DropUpSpeed       = 1.2f;  // вертикальная составляющая (m/s)
    private const float DropFallbackUp    = 1.0f;  // высота спавна над transform.position если коллайдер не найден

    private static Font s_Font;
    private static Font GetFont()
    {
        if (s_Font != null) return s_Font;
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (s_Font == null) s_Font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        return s_Font;
    }

    void Awake()
    {
        Instance = this;
        BuildPanel();
        panel.SetActive(false);
    }

    private void BuildPanel()
    {
        float panelW = BtnW + Pad * 2f;
        float panelH = 4f * BtnH + 3f * BtnGap + Pad * 2f;

        panel = new GameObject("ContextMenu", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.08f, 0.97f);

        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(panelW, panelH);

        AddBtn("Использовать", 0, () => OnAction("use"));
        AddBtn("Осмотреть",    1, () => OnAction("inspect"));
        AddBtn("Открыть",      2, () => OnAction("open"));
        AddBtn("Выкинуть",     3, () => OnAction("drop"));
    }

    private void AddBtn(string label, int row, Action onClick)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(panel.transform, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(Pad, -(Pad + row * (BtnH + BtnGap)));
        rt.sizeDelta = new Vector2(BtnW, BtnH);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.18f, 0.18f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cols = btn.colors;
        cols.highlightedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
        cols.pressedColor     = new Color(0.12f, 0.12f, 0.12f, 1f);
        btn.colors            = cols;
        btn.onClick.AddListener(() => { onClick(); Hide(); });

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(6f, 0f); trt.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.font      = GetFont();
        text.text      = label;
        text.alignment = TextAnchor.MiddleLeft;
        text.fontSize  = 13;
        text.color     = Color.white;
        text.raycastTarget = false;

        go.name = label; // used for show/hide "Открыть"
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(ItemInstance item, GridContainer container, Vector2 screenPos, EquipmentSlotUI slot = null)
    {
        // Toggle: повторный RMB на том же самом предмете закрывает меню.
        // Если меню открыто для другого предмета — переоткрываем для нового.
        if (panel.activeSelf && targetItem == item)
        {
            Hide();
            return;
        }

        targetItem      = item;
        targetContainer = container;
        targetSlot      = slot;

        // Must be last sibling so it renders on top of the inventory panel.
        // ContextMenuUI.Awake fires before inventoryPanel is created, so without
        // this call the context menu panel would be hidden behind the inventory.
        panel.transform.SetAsLastSibling();
        panel.SetActive(true);
        shownFrame = Time.frameCount;

        // Show "Открыть" only for containers
        foreach (Transform t in panel.transform)
            if (t.name == "Открыть") t.gameObject.SetActive(item.definition.isContainer);

        // Position near cursor (screen space overlay = screen coords)
        var rt = panel.GetComponent<RectTransform>();
        rt.position = new Vector3(screenPos.x, screenPos.y, 0f);
    }

    public void Hide()
    {
        panel.SetActive(false);
        targetItem      = null;
        targetContainer = null;
        targetSlot      = null;
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    private void OnAction(string action)
    {
        if (targetItem == null) return;
        switch (action)
        {
            case "use":
                UseTargetItem();
                break;

            case "inspect":
                {
                    Debug.Log($"[ContextMenuUI] inspect → {targetItem.definition.displayName}");
                    var inspector = InspectWindowUI.Instance;
                    if (inspector == null)
                    {
                        // Fallback: после hot-reload компонент может ещё не быть на канвасе.
                        Debug.LogWarning("[ContextMenuUI] InspectWindowUI.Instance was null — creating on canvas");
                        var canvas = GetComponentInParent<Canvas>();
                        var host   = canvas != null ? canvas.gameObject : gameObject;
                        inspector  = host.AddComponent<InspectWindowUI>();
                        inspector.RootCanvas = canvas;
                    }
                    inspector.Show(targetItem);
                }
                break;

            case "open":
                if (targetItem.nestedContainer != null)
                    InventoryUI.Instance?.OpenNestedContainer(
                        targetItem.nestedContainer, targetItem.definition.displayName);
                break;

            case "drop":
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

    // ── Drop (выброс из инвентаря) ───────────────────────────────────────────

    /// <summary>
    /// Спавнит предмет из живота персонажа с детерминированной скоростью вперёд.
    ///
    /// Позиция: центр CapsuleCollider игрока минус DropBellyDrop по Y (живот, не грудь),
    /// плюс DropForwardOffset вдоль горизонтального forward-вектора (чтобы предмет не клипался
    /// в коллайдер). Если коллайдер не найден — fallback на transform.position + up.
    ///
    /// Направление броска — горизонтальная проекция взгляда камеры (туда, куда смотрит игрок),
    /// иначе при стоянии player.transform.forward может смотреть куда угодно.
    /// </summary>
    private static void DropItemFromPlayer(ItemInstance dropped)
    {
        if (dropped == null) return;

        var player = FindPlayerObject();
        Vector3 origin   = ResolveBellyOrigin(player);
        Vector3 forward  = ResolveAimForward(player);

        Vector3 spawnPos = origin + forward * DropForwardOffset;
        Vector3 velocity = forward * DropForwardSpeed + Vector3.up * DropUpSpeed;

        WorldItem3D.SpawnWithVelocity(dropped, spawnPos, velocity);
    }

    private static GameObject FindPlayerObject()
    {
        // По тегу (универсально для любого контроллера — собственного, Invector и т.д.)
        var byTag = GameObject.FindGameObjectWithTag("Player");
        if (byTag != null) return byTag;

        // Фолбэк — наш PlayerController
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

        // CapsuleCollider.bounds.center всегда даёт мировой центр капсулы вне зависимости от
        // того, где находится pivot самого GameObject (важно для Invector и других контроллеров).
        var capsule = player.GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
            return capsule.bounds.center - Vector3.up * DropBellyDrop;

        // Если капсулы нет — пробуем любой CharacterController
        var cc = player.GetComponentInChildren<CharacterController>();
        if (cc != null)
            return cc.bounds.center - Vector3.up * DropBellyDrop;

        // Самый базовый fallback
        return player.transform.position + Vector3.up * DropFallbackUp;
    }

    private static Vector3 ResolveAimForward(GameObject player)
    {
        // Главный вариант — куда смотрит камера (в третьем лице это направление взгляда игрока)
        var cam = Camera.main;
        Vector3 src = cam != null
            ? cam.transform.forward
            : (player != null ? player.transform.forward : Vector3.forward);

        // Горизонтальная проекция, чтобы не швырнуть в небо/в пол при наклоне камеры
        Vector3 flat = new Vector3(src.x, 0f, src.z);
        if (flat.sqrMagnitude < 0.001f)
        {
            // Камера смотрит строго вниз/вверх — используем forward игрока
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
