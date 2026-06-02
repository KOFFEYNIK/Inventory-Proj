using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Управляет режимом редактирования лейаута инвентаря: кнопка-замок, overlay
/// со слайдерами размеров и кнопками «Сбросить»/«Готово», блокировка drag-and-drop
/// предметов в edit-mode, сохранение overrides в JSON.
///
/// Висит на <see cref="InventoryUI"/>-канвасе. Не активирует ничего сам — все handle'ы
/// и кнопка-замок создаются <see cref="InventoryUI"/> и регистрируются через
/// <see cref="Register"/>. При смене размеров инвентарь пересоздаётся через
/// <see cref="InventoryUI.RebuildInventoryPanel"/> — все handle'ы регистрируются
/// заново, а edit-режим сохраняется.
/// </summary>
public class LayoutEditMode : MonoBehaviour
{
    public static LayoutEditMode Instance { get; private set; }
    public static bool IsActive => Instance != null && Instance.isActive;

    public const float MinCellSize =  24f;
    public const float MaxCellSize =  96f;
    public const float MinSlotSize =  32f;
    public const float MaxSlotSize = 120f;

    private bool isActive;
    private InventoryUI ui;
    private InventoryLayoutCustomization customization;

    private readonly List<LayoutDragHandle> handles = new();

    // Визуальные элементы, видимые только в edit-mode (угловые ручки ресайза окна).
    // В отличие от handle'ов, они сами скрываются (SetActive(false)) вне режима EDIT,
    // чтобы не перехватывать клики. Уничтожаются вместе с inventoryPanel → чистим в ClearHandles.
    private readonly List<GameObject> editOnlyVisuals = new();

    private GameObject editPanel;
    private Text       lockLabel;
    private Slider     cellSlider, slotSlider;
    private Text       cellSliderValue, slotSliderValue;

    private static Font s_Font;
    private static Font GetFont()
    {
        if (s_Font != null) return s_Font;
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (s_Font == null) s_Font = Font.CreateDynamicFontFromOSFont("Arial", 12);
        return s_Font;
    }

    // ── Init / handle registry ───────────────────────────────────────────────

    public void Init(InventoryUI inventoryUi, InventoryLayoutCustomization current)
    {
        Instance      = this;
        ui            = inventoryUi;
        customization = current ?? new InventoryLayoutCustomization();
    }

    /// <summary>Получить текущий customization (для применения при инициализации UI).</summary>
    public InventoryLayoutCustomization GetCustomization() => customization;

    /// <summary>Удаляет старые ссылки на handle'ы (вызывается при rebuild панели).</summary>
    public void ClearHandles()
    {
        handles.Clear();
        editOnlyVisuals.Clear();
    }

    /// <summary>Регистрирует объект, видимый только в режиме EDIT (угловые ручки ресайза).</summary>
    public void RegisterEditOnlyVisual(GameObject go)
    {
        if (go == null) return;
        editOnlyVisuals.Add(go);
        go.SetActive(isActive);
    }

    /// <summary>Регистрирует handle и создаёт ему overlay-подсветку.</summary>
    public void Register(LayoutDragHandle handle)
    {
        if (handle == null) return;
        handles.Add(handle);
        if (handle.overlay == null) handle.overlay = CreateOverlayFrame((RectTransform)handle.transform);
        handle.SetOverlayActive(isActive);
    }

    private Image CreateOverlayFrame(RectTransform target)
    {
        var go = new GameObject("LayoutOverlay", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(target, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();   // рендериться поверх BG/Fill/Label дочерних
        var img = go.GetComponent<Image>();
        img.color = new Color(0.30f, 0.65f, 1f, 0.22f);
        img.raycastTarget = false;
        var outline = go.AddComponent<Outline>();
        outline.effectColor = new Color(0.30f, 0.65f, 1f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);
        return img;
    }

    // ── Edit panel + lock button (вызываются InventoryUI.BuildInventoryPanel) ──

    public Button CreateLockButton(Transform parent, Vector2 anchoredPosFromTopRight, Vector2 size)
    {
        var go = new GameObject("LockButton", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);
        rt.anchoredPosition = anchoredPosFromTopRight;
        rt.sizeDelta = size;

        var img = go.GetComponent<Image>();
        img.color = new Color(0.20f, 0.20f, 0.20f, 1f);

        var lblGo = new GameObject("Lbl", typeof(RectTransform), typeof(Text));
        lblGo.transform.SetParent(go.transform, false);
        var lrt = (RectTransform)lblGo.transform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        lockLabel = lblGo.GetComponent<Text>();
        lockLabel.font = GetFont();
        lockLabel.fontSize = 12;
        lockLabel.alignment = TextAnchor.MiddleCenter;
        lockLabel.color = Color.white;
        lockLabel.raycastTarget = false;
        lockLabel.fontStyle = FontStyle.Bold;
        UpdateLockLabel();

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Toggle);
        return btn;
    }

    public void CreateEditPanel(Transform parent, Vector2 anchoredPosFromTop, Vector2 size)
    {
        // Если уже был — уничтожаем, иначе RebuildInventoryPanel плодит дубликаты
        // (editPanel живёт на rootCanvas, не на content — он не destroy-ится вместе
        //  с inventoryPanel).
        if (editPanel != null) Destroy(editPanel);

        var panel = new GameObject("LayoutEditPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        var rt = (RectTransform)panel.transform;
        // Anchor — верх экрана; pivot=(0.5,1) → панель свисает ВНИЗ от anchor.
        // Это уводит её из-под окна инвентаря (которое центрировано на экране).
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPosFromTop;
        rt.sizeDelta = size;
        panel.GetComponent<Image>().color = new Color(0.05f, 0.05f, 0.05f, 0.92f);
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.30f, 0.65f, 1f, 0.7f);
        outline.effectDistance = new Vector2(1f, -1f);

        float x = 12f, y = -10f;

        MakeLabel(panel.transform, "РЕДАКТИРОВАНИЕ ЛЕЙАУТА", 12, TextAnchor.UpperLeft,
                  new Rect(x, y, size.x - 24f, 16f));
        y -= 22f;

        // Cell size slider
        MakeLabel(panel.transform, "Размер ячейки сетки", 11, TextAnchor.UpperLeft,
                  new Rect(x, y, 170f, 14f));
        cellSliderValue = MakeValueLabel(panel.transform, new Rect(x + 175f, y, 50f, 14f));
        y -= 18f;
        cellSlider = CreateSlider(panel.transform, new Rect(x, y, size.x - 24f, 18f),
                                  MinCellSize, MaxCellSize,
                                  customization.cellSizeSet ? customization.cellSize : 50f,
                                  OnCellSliderChanged);
        UpdateValueLabel(cellSliderValue, cellSlider.value);
        y -= 26f;

        // Slot size slider
        MakeLabel(panel.transform, "Размер 1×1-слота экипировки", 11, TextAnchor.UpperLeft,
                  new Rect(x, y, 220f, 14f));
        slotSliderValue = MakeValueLabel(panel.transform, new Rect(x + 225f, y, 50f, 14f));
        y -= 18f;
        slotSlider = CreateSlider(panel.transform, new Rect(x, y, size.x - 24f, 18f),
                                  MinSlotSize, MaxSlotSize,
                                  customization.slotSizeSet ? customization.slotSize : 54f,
                                  OnSlotSliderChanged);
        UpdateValueLabel(slotSliderValue, slotSlider.value);
        y -= 32f;

        // Apply button (применяет изменения размеров — нужен ребилд UI)
        MakeButton(panel.transform, new Rect(x, y, 140f, 26f), "Применить размеры",
                   ApplySizesAndRebuild, new Color(0.15f, 0.35f, 0.15f, 1f));

        // Reset button
        MakeButton(panel.transform, new Rect(x + 150f, y, 140f, 26f), "Сбросить лейаут",
                   ResetAll, new Color(0.40f, 0.15f, 0.15f, 1f));

        // Done button
        MakeButton(panel.transform, new Rect(size.x - 12f - 100f, y, 100f, 26f), "Готово",
                   Toggle, new Color(0.20f, 0.20f, 0.20f, 1f));

        editPanel = panel;
        panel.SetActive(isActive);
    }

    // ── Edit-mode lifecycle ──────────────────────────────────────────────────

    public void Toggle()
    {
        if (isActive) Exit();
        else          Enter();
    }

    private void Enter()
    {
        isActive = true;
        foreach (var h in handles) if (h != null) h.SetOverlayActive(true);
        foreach (var v in editOnlyVisuals) if (v != null) v.SetActive(true);
        SetItemRaycastsActive(false);
        if (editPanel != null) editPanel.SetActive(true);
        UpdateLockLabel();
    }

    private void Exit()
    {
        isActive = false;
        foreach (var h in handles) if (h != null) h.SetOverlayActive(false);
        foreach (var v in editOnlyVisuals) if (v != null) v.SetActive(false);
        SetItemRaycastsActive(true);
        if (editPanel != null) editPanel.SetActive(false);
        UpdateLockLabel();
        InventoryLayoutPersistence.Save(customization);
    }

    // ── Handle-moved callback (from LayoutDragHandle) ────────────────────────

    public void RecordHandleMoved(LayoutDragHandle h, Vector2 pos)
    {
        if (h == null) return;

        if (!string.IsNullOrEmpty(h.PanelId))
            customization.SetPanel(h.PanelId, pos);
        else if (h.HasSlotType && h.IsIndicator)
            customization.SetIndicator(h.SlotType, pos);
        else if (h.HasSlotType)
            customization.SetEquipment(h.SlotType, pos);

        InventoryLayoutPersistence.Save(customization);
    }

    // ── Slider callbacks ─────────────────────────────────────────────────────

    private void OnCellSliderChanged(float v)
    {
        customization.SetCellSize(v);
        UpdateValueLabel(cellSliderValue, v);
    }

    private void OnSlotSliderChanged(float v)
    {
        customization.SetSlotSize(v);
        UpdateValueLabel(slotSliderValue, v);
    }

    private void ApplySizesAndRebuild()
    {
        InventoryLayoutPersistence.Save(customization);
        if (ui != null) ui.RebuildInventoryPanel();
        // После ребилда InventoryUI заново зарегистрирует handle'ы;
        // повторно входим в edit-mode, чтобы overlay и панель остались видимы.
        Enter();
    }

    private void ResetAll()
    {
        customization.ResetAll();
        InventoryLayoutPersistence.Delete();
        if (ui != null) ui.RebuildInventoryPanel();
        Enter();
    }

    // ── ItemUI raycast freezing ──────────────────────────────────────────────

    private void SetItemRaycastsActive(bool active)
    {
        // ItemUI висят на дочерних элементах InventoryCanvas (он отдельный GO от InventoryUI).
        // Через FindObjects ловим всех — включая ItemUI внутри вложенных окон контейнеров.
        var items = Object.FindObjectsByType<ItemUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var it in items)
        {
            if (it == null) continue;
            var img = it.GetComponent<Image>();
            if (img != null) img.raycastTarget = active;
        }
    }

    private void UpdateLockLabel()
    {
        if (lockLabel == null) return;
        // LegacyRuntime font не покрывает emoji — используем текст. Цвет показывает состояние.
        lockLabel.text  = isActive ? "OK" : "EDIT";
        lockLabel.color = isActive ? new Color(0.30f, 1f, 0.30f) : new Color(0.85f, 0.85f, 0.85f);
    }

    // ── UI builders ──────────────────────────────────────────────────────────

    private static Slider CreateSlider(Transform parent, Rect rect, float min, float max,
                                       float initial, System.Action<float> onChanged)
    {
        var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        SetRectTL(rt, rect);

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(go.transform, false);
        var bgRt = (RectTransform)bgGo.transform;
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bgGo.GetComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f, 1f);

        var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGo.transform.SetParent(go.transform, false);
        var faRt = (RectTransform)fillAreaGo.transform;
        faRt.anchorMin = new Vector2(0f, 0.25f);
        faRt.anchorMax = new Vector2(1f, 0.75f);
        faRt.offsetMin = new Vector2(5f, 0f); faRt.offsetMax = new Vector2(-5f, 0f);

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        var fillRt = (RectTransform)fillGo.transform;
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
        fillGo.GetComponent<Image>().color = new Color(0.30f, 0.65f, 1f, 0.9f);

        var handleAreaGo = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleAreaGo.transform.SetParent(go.transform, false);
        var haRt = (RectTransform)handleAreaGo.transform;
        haRt.anchorMin = Vector2.zero; haRt.anchorMax = Vector2.one;
        haRt.offsetMin = new Vector2(8f, 0f); haRt.offsetMax = new Vector2(-8f, 0f);

        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        handleGo.transform.SetParent(handleAreaGo.transform, false);
        var hRt = (RectTransform)handleGo.transform;
        hRt.anchorMin = new Vector2(0.5f, 0f); hRt.anchorMax = new Vector2(0.5f, 1f);
        hRt.sizeDelta = new Vector2(14f, 0f);
        handleGo.GetComponent<Image>().color = Color.white;

        var s = go.GetComponent<Slider>();
        s.fillRect    = fillRt;
        s.handleRect  = hRt;
        s.targetGraphic = handleGo.GetComponent<Image>();
        s.direction   = Slider.Direction.LeftToRight;
        s.minValue    = min;
        s.maxValue    = max;
        s.wholeNumbers = false;
        s.value       = Mathf.Clamp(initial, min, max);
        s.onValueChanged.AddListener(v => onChanged(v));
        return s;
    }

    private static Text MakeValueLabel(Transform parent, Rect rect)
    {
        var go = new GameObject("Value", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        SetRectTL(rt, rect);
        var t = go.GetComponent<Text>();
        t.font = GetFont();
        t.fontSize = 11;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = new Color(0.30f, 0.65f, 1f);
        t.raycastTarget = false;
        return t;
    }

    private static void UpdateValueLabel(Text t, float v)
    {
        if (t != null) t.text = $"{Mathf.RoundToInt(v)} px";
    }

    private static void MakeLabel(Transform parent, string text, int fontSize,
                                   TextAnchor anchor, Rect rect)
    {
        var go = new GameObject("Lbl", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        SetRectTL(rt, rect);
        var t = go.GetComponent<Text>();
        t.font = GetFont();
        t.text = text;
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = new Color(0.85f, 0.85f, 0.85f);
        t.raycastTarget = false;
    }

    private static void MakeButton(Transform parent, Rect rect, string label,
                                    UnityEngine.Events.UnityAction onClick, Color color)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        SetRectTL(rt, rect);
        var img = go.GetComponent<Image>();
        img.color = color;
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var t = textGo.GetComponent<Text>();
        t.font = GetFont();
        t.text = label;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = 12;
        t.color = Color.white;
        t.raycastTarget = false;
    }

    private static void SetRectTL(RectTransform rt, Rect rect)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(rect.x, rect.y);
        rt.sizeDelta = new Vector2(rect.width, rect.height);
    }
}
