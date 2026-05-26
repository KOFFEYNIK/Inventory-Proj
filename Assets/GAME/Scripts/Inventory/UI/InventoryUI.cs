using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Добавь этот компонент на любой GameObject в сцене.
/// Привяжи InventoryLayout SO к полю Layout — позиции всех элементов задаются там.
/// Canvas и UI создаются программно при старте.
/// </summary>
public class InventoryUI : MonoBehaviour
{
    public static InventoryUI Instance { get; private set; }

    [Header("Layout")]
    public InventoryLayout Layout;

    [Header("Controls")]
    public KeyCode ToggleKey = KeyCode.Tab;

    [Header("Cursor")]
    [Tooltip("Если true — UI сам управляет курсором (показывает на Open, прячет на Close). " +
             "Для FPS-сцен обычно false: курсором рулит контроллер камеры (например FPSMouseLook).")]
    public bool manageCursor = false;

    [Tooltip("Лочить курсор обратно при закрытии инвентаря. Имеет смысл только если manageCursor = true.")]
    public bool lockCursorOnClose = true;

    public bool IsOpen { get; private set; }

    public static System.Action<bool> OnInventoryStateChanged;

    private Canvas     rootCanvas;
    private GameObject inventoryPanel;
    private GameObject content;

    private readonly Dictionary<EquipmentSlotType, EquipmentSlotUI> equipmentSlotUIs = new();
    // Включает И обычные equipment-слоты, И indicator-слоты. Dictionary выше содержит
    // только один UI на тип (для lookup'а из DragDropController); этот список —
    // для итерации Refresh, чтобы indicators тоже обновлялись.
    private readonly List<EquipmentSlotUI> allEquipmentSlotUIs = new();
    private readonly GridUI[] pocketUIs = new GridUI[4];

    private GameObject backpackPanelGO, rigPanelGO, secureCasePanelGO;
    private GridUI     backpackGridUI,  rigGridUI,  secureCaseGridUI;
    private string     lastBackpackContainerId, lastRigContainerId, lastSecureCaseContainerId;

    /// <summary>Runtime-копия SO для безопасной модификации overrides. Не пишет в asset.</summary>
    private InventoryLayoutCustomization customization;

    // Vitals bars in the inventory window
    private RectTransform hungerFillRt, thirstFillRt;
    private Text          hungerLabel,  thirstLabel;

    // Health bars in the inventory window (per body part)
    private class HealthBarView { public Image fillImage; public RectTransform fillRt; public Text label; }
    private readonly Dictionary<BodyPartType, HealthBarView> healthBars = new();

    private readonly List<GameObject> nestedWindows = new();
    private readonly Dictionary<string, GameObject> nestedWindowsByContainer = new();

    private static Font s_Font;
    private static Font GetFont()
    {
        if (s_Font != null) return s_Font;
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (s_Font == null) s_Font = Font.CreateDynamicFontFromOSFont("Arial", 12);
        return s_Font;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (Layout == null) Layout = ScriptableObject.CreateInstance<InventoryLayout>();

        // Runtime-копия SO — overrides из JSON не загрязняют ассет.
        Layout = Instantiate(Layout);
        customization = InventoryLayoutPersistence.Load() ?? new InventoryLayoutCustomization();
        customization.ApplyTo(Layout);
        ApplyCellSizesToGrid();

        EnsureEventSystem();
        BuildCanvas();
        BuildInventoryPanel();
        inventoryPanel.SetActive(false);
    }

    private void ApplyCellSizesToGrid()
    {
        GridUI.CellSize = Layout.cellSize;
        GridUI.CellGap  = Layout.cellGap;
    }

    void Start()
    {
        var mgr = InventoryManager.Instance;
        if (mgr == null) return;
        mgr.OnInventoryChanged += Refresh;

        if (HungerSystem.Instance != null)
            HungerSystem.Instance.OnChanged += RefreshHunger;
        if (ThirstSystem.Instance != null)
            ThirstSystem.Instance.OnChanged += RefreshThirst;

        if (HealthSystem.Instance != null)
            HealthSystem.Instance.OnHealthChanged += RefreshHealth;

        Refresh();
        RefreshHunger();
        RefreshThirst();
        RefreshHealth();
    }

    void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInventoryChanged -= Refresh;
        if (HungerSystem.Instance != null)
            HungerSystem.Instance.OnChanged -= RefreshHunger;
        if (ThirstSystem.Instance != null)
            ThirstSystem.Instance.OnChanged -= RefreshThirst;
        if (HealthSystem.Instance != null)
            HealthSystem.Instance.OnHealthChanged -= RefreshHealth;
    }

    void Update()
    {
        if (Input.GetKeyDown(ToggleKey)) Toggle();
    }

    // ── Canvas setup ──────────────────────────────────────────────────────────

    private void BuildCanvas()
    {
        var go = new GameObject("InventoryCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(go);

        rootCanvas = go.GetComponent<Canvas>();
        rootCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 100;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        var ddc = go.AddComponent<DragDropController>();
        ddc.RootCanvas = rootCanvas;

        go.AddComponent<ContextMenuUI>();

        var inspect = go.AddComponent<InspectWindowUI>();
        inspect.RootCanvas = rootCanvas;

        // LayoutEditMode — управление режимом редактирования. Создаётся один раз;
        // при ребилде инвентаря его handle-список сбрасывается, но сам объект жив.
        var editMode = go.AddComponent<LayoutEditMode>();
        editMode.Init(this, customization);
    }

    // ── Inventory panel ───────────────────────────────────────────────────────

    private void BuildInventoryPanel()
    {
        inventoryPanel = MakePanel("InventoryOverlay", rootCanvas.transform,
            new Color(0, 0, 0, 0.55f));
        StretchFull(inventoryPanel.GetComponent<RectTransform>());

        var closeBtn = inventoryPanel.AddComponent<Button>();
        closeBtn.onClick.AddListener(Close);
        inventoryPanel.GetComponent<Image>().raycastTarget = true;

        content = MakePanel("Content", inventoryPanel.transform, new Color(0.08f, 0.08f, 0.08f, 0.97f));
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = contentRt.anchorMax = new Vector2(0.5f, 0.5f);
        contentRt.pivot     = new Vector2(0.5f, 0.5f);
        contentRt.sizeDelta = Layout.windowSize;
        contentRt.anchoredPosition = Vector2.zero;

        var blocker = content.AddComponent<Button>();
        blocker.onClick.AddListener(() => { });

        // Title bar
        var titleBar = MakePanel("TitleBar", content.transform, new Color(0.05f, 0.05f, 0.05f, 1f));
        SetRectTL(titleBar.GetComponent<RectTransform>(), 0, 0, Layout.windowSize.x, Layout.titleBarHeight);
        MakeLabel("ИНВЕНТАРЬ", titleBar.transform, 16, TextAnchor.MiddleCenter,
                  new Rect(0, 0, Layout.windowSize.x, Layout.titleBarHeight));

        BuildEquipmentSlots();
        BuildContainerPanels();
        BuildPockets();
        BuildButtons();
        BuildVitalsBars();
        BuildHealthPanel();

        // Layout-edit UI: кнопка-замок в правом верхнем углу + панель со слайдерами внизу.
        if (LayoutEditMode.Instance != null)
        {
            LayoutEditMode.Instance.CreateLockButton(content.transform, new Vector2(-4f, -4f), new Vector2(60f, 22f));
            // Edit-panel создаётся вне content (на rootCanvas) — иначе закрывал бы
            // vitals/health bars в нижней части окна. Позиционируется к нижнему
            // центру экрана с pivot=(0.5,1), чтобы стоять над hotbar'ом.
            LayoutEditMode.Instance.CreateEditPanel(rootCanvas.transform, new Vector2(0f, -8f), new Vector2(560f, 130f));
        }
    }

    /// <summary>
    /// Полностью пересоздаёт inventoryPanel (нужно после изменения cellSize/slotSize в
    /// LayoutEditMode). Сохраняет состояние «открыт ли инвентарь» и сбрасывает старые
    /// LayoutDragHandle-ссылки в LayoutEditMode перед уничтожением.
    /// </summary>
    public void RebuildInventoryPanel()
    {
        bool wasOpen = IsOpen;

        LayoutEditMode.Instance?.ClearHandles();

        if (inventoryPanel != null)
        {
            // GC-роли (equipmentSlotUIs, pockets, кеши гридов) обнуляем — все GameObject'ы
            // уничтожаются вместе с inventoryPanel.
            equipmentSlotUIs.Clear();
            allEquipmentSlotUIs.Clear();
            for (int i = 0; i < pocketUIs.Length; i++) pocketUIs[i] = null;
            backpackGridUI = rigGridUI = secureCaseGridUI = null;
            backpackPanelGO = rigPanelGO = secureCasePanelGO = null;
            lastBackpackContainerId = lastRigContainerId = lastSecureCaseContainerId = null;
            healthBars.Clear();

            Destroy(inventoryPanel);
            inventoryPanel = null;
        }

        ApplyCellSizesToGrid();
        BuildInventoryPanel();
        inventoryPanel.SetActive(wasOpen);
        if (wasOpen) Refresh();
    }

    private void BuildEquipmentSlots()
    {
        var mgr = InventoryManager.Instance;
        if (mgr == null)
        {
            Debug.LogWarning("[InventoryUI] BuildEquipmentSlots: InventoryManager.Instance == null. " +
                             "Проверь, что InventoryManager имеет [DefaultExecutionOrder(-100)] или " +
                             "висит на том же GameObject раньше InventoryUI.");
            return;
        }

        foreach (var entry in Layout.equipmentSlots)
            if (entry != null && entry.enabled)
                CreateEquipmentSlot(entry.type, entry.anchoredPosition, Layout.slotSize, isIndicator: false);

        if (Layout.backpackSlotIndicator != null && Layout.backpackSlotIndicator.enabled)
            CreateEquipmentSlot(EquipmentSlotType.Backpack, Layout.backpackSlotIndicator.anchoredPosition, Layout.slotSize, isIndicator: true);
        if (Layout.rigSlotIndicator != null && Layout.rigSlotIndicator.enabled)
            CreateEquipmentSlot(EquipmentSlotType.Rig, Layout.rigSlotIndicator.anchoredPosition, Layout.slotSize, isIndicator: true);
        if (Layout.secureCaseSlotIndicator != null && Layout.secureCaseSlotIndicator.enabled)
            CreateEquipmentSlot(EquipmentSlotType.SecureCase, Layout.secureCaseSlotIndicator.anchoredPosition, Layout.slotSize, isIndicator: true);
    }

    private void CreateEquipmentSlot(EquipmentSlotType type, Vector2 pos, float size, bool isIndicator)
    {
        var mgr = InventoryManager.Instance;
        var slot = mgr?.GetSlot(type);
        if (slot == null) return;

        // Имя различает обычные слоты и indicator-слоты (одного типа может быть два:
        // например, Backpack-индикатор + Backpack-equip-слот недопустим, но различие
        // полезно для отладки и для key'а в LayoutEditMode).
        string name = isIndicator ? $"Indicator_{type}" : $"EquipSlot_{type}";
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(content.transform, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);

        var ui = go.AddComponent<EquipmentSlotUI>();
        ui.Init(type, slot, size);
        allEquipmentSlotUIs.Add(ui);
        // Dictionary хранит один UI на тип для lookup'а (drag-drop). Indicator
        // не должен перезаписывать обычный equipment-slot; если обычного нет
        // (как для Backpack/Rig/SecureCase), indicator занимает это место.
        if (!isIndicator || !equipmentSlotUIs.ContainsKey(type))
            equipmentSlotUIs[type] = ui;

        // LayoutDragHandle — позволяет в edit-mode двигать этот слот мышью.
        var handle = go.AddComponent<LayoutDragHandle>();
        handle.HasSlotType = true;
        handle.SlotType    = type;
        handle.IsIndicator = isIndicator;
        LayoutEditMode.Instance?.Register(handle);
    }

    private void BuildContainerPanels()
    {
        backpackPanelGO   = CreateContainerPanelHost("BackpackPanel",   "Backpack",   Layout.backpackPanelPos,   Layout.backpackEnabled);
        rigPanelGO        = CreateContainerPanelHost("RigPanel",        "Rig",        Layout.rigPanelPos,        Layout.rigEnabled);
        secureCasePanelGO = CreateContainerPanelHost("SecureCasePanel", "SecureCase", Layout.secureCasePanelPos, Layout.secureCaseEnabled);
    }

    private GameObject CreateContainerPanelHost(string name, string panelKey, Vector2 pos, bool enabled)
    {
        if (!enabled) return null;
        var host = new GameObject(name, typeof(RectTransform));
        host.transform.SetParent(content.transform, false);
        var rt = host.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        // Размер хоста изначально (0,0) — выставляется при наполнении гридом. Чтобы
        // LayoutDragHandle ловил клики в edit-mode даже на пустой/скрытой панели,
        // даём ей фиксированный «hitbox» 200×60 — этого хватает, чтобы захватить.
        rt.sizeDelta = new Vector2(200f, 60f);
        host.SetActive(false);

        // Прозрачный raycast-target нужен чтобы LayoutDragHandle получал события.
        // В обычном режиме панель спрятана (SetActive(false)) — handle тоже не работает.
        var img = host.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.001f);
        img.raycastTarget = true;

        var handle = host.AddComponent<LayoutDragHandle>();
        handle.PanelId = panelKey;
        LayoutEditMode.Instance?.Register(handle);

        return host;
    }

    private void BuildPockets()
    {
        if (!Layout.pocketsEnabled) return;
        Vector2[] positions = { Layout.pocket1Pos, Layout.pocket2Pos, Layout.pocket3Pos, Layout.pocket4Pos };
        var mgr = InventoryManager.Instance;
        // Layout сейчас знает 4 позиции — лишние карманы (если в config их > 4) не рисуем.
        // Если карманов < 4, рисуем сколько есть.
        int count = mgr != null ? Mathf.Min(positions.Length, mgr.Pockets.Length) : positions.Length;

        for (int i = 0; i < count; i++)
        {
            string panelId = mgr != null && mgr.Pockets[i] != null && !string.IsNullOrEmpty(mgr.Pockets[i].containerId)
                ? mgr.Pockets[i].containerId
                : $"Pocket{i + 1}";
            var grid = CreateGridUI(panelId, content.transform, positions[i]);
            if (mgr != null && mgr.Pockets[i] != null) grid.Init(mgr.Pockets[i]);
            pocketUIs[i] = grid;

            // Карман — отдельный draggable; handle висит на самом GridUI GameObject.
            var handle = grid.gameObject.AddComponent<LayoutDragHandle>();
            handle.PanelId = panelId;
            LayoutEditMode.Instance?.Register(handle);
        }
    }

    private void BuildButtons()
    {
        MakeButton("Сохранить", content.transform,
            new Rect(Layout.saveButtonPos.x, Layout.saveButtonPos.y, Layout.buttonSize.x, Layout.buttonSize.y),
            () => InventoryManager.Instance?.Save());
        MakeButton("Загрузить", content.transform,
            new Rect(Layout.loadButtonPos.x, Layout.loadButtonPos.y, Layout.buttonSize.x, Layout.buttonSize.y),
            () => { InventoryManager.Instance?.Load(); Refresh(); });
    }

    // ── Vitals bars (inside the inventory window) ────────────────────────────

    private void BuildVitalsBars()
    {
        if (Layout == null || !Layout.vitalsBarsEnabled) return;

        // Каждая полоска появляется только если соответствующий модуль подключён к игроку.
        if (HungerSystem.Instance != null)
        {
            var hungerColor = new Color(0.95f, 0.85f, 0.20f);
            (hungerFillRt, hungerLabel) = CreateVitalsBar("HungerBar", "Hunger",
                Layout.hungerBarPos, Layout.vitalsBarSize, hungerColor, "ГОЛОД");
        }

        if (ThirstSystem.Instance != null)
        {
            var thirstColor = new Color(0.20f, 0.55f, 0.95f);
            (thirstFillRt, thirstLabel) = CreateVitalsBar("ThirstBar", "Thirst",
                Layout.thirstBarPos, Layout.vitalsBarSize, thirstColor, "ЖАЖДА");
        }
    }

    private (RectTransform fill, Text label) CreateVitalsBar(string name, string panelId,
        Vector2 pos, Vector2 size, Color fillColor, string labelText)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(content.transform, false);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        // Прозрачный raycast-target поверх полоски — фон BG ниже имеет alpha 0.55,
        // он уже raycastable, но нужно явно raycastTarget = true чтобы handle ловил.
        // (BG ниже создаётся как Image — он по умолчанию raycastTarget = true.)
        var handle = root.AddComponent<LayoutDragHandle>();
        handle.PanelId = panelId;
        LayoutEditMode.Instance?.Register(handle);

        // BG
        var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(root.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // Fill
        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(root.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot     = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = new Vector2(2f, 0f);
        fillRt.sizeDelta = new Vector2(size.x - 4f, -4f);
        fillGo.GetComponent<Image>().color = fillColor;

        // Label
        var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        lblGo.transform.SetParent(root.transform, false);
        var lblRt = lblGo.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = new Vector2(8f, 0f); lblRt.offsetMax = new Vector2(-8f, 0f);
        var lbl = lblGo.GetComponent<Text>();
        lbl.font = GetFont();
        lbl.fontSize = 12;
        lbl.alignment = TextAnchor.MiddleLeft;
        lbl.color = new Color(1f, 1f, 1f, 0.95f);
        lbl.text = labelText;
        lbl.raycastTarget = false;

        return (fillRt, lbl);
    }

    public void RefreshHunger()
    {
        var v = HungerSystem.Instance;
        if (v == null || hungerFillRt == null) return;
        UpdateVitalsBar(hungerFillRt, v.HungerNormalized, Layout != null ? Layout.vitalsBarSize.x : 300f);
        if (hungerLabel != null) hungerLabel.text = $"ГОЛОД  {Mathf.RoundToInt(v.Hunger)}/{Mathf.RoundToInt(v.maxHunger)}";
    }

    public void RefreshThirst()
    {
        var v = ThirstSystem.Instance;
        if (v == null || thirstFillRt == null) return;
        UpdateVitalsBar(thirstFillRt, v.ThirstNormalized, Layout != null ? Layout.vitalsBarSize.x : 300f);
        if (thirstLabel != null) thirstLabel.text = $"ЖАЖДА  {Mathf.RoundToInt(v.Thirst)}/{Mathf.RoundToInt(v.maxThirst)}";
    }

    private static void UpdateVitalsBar(RectTransform fillRt, float normalized, float fullWidth)
    {
        if (fillRt == null) return;
        float innerW = Mathf.Max(0f, fullWidth - 4f);
        fillRt.sizeDelta = new Vector2(innerW * Mathf.Clamp01(normalized), -4f);
    }

    // ── Health panel ─────────────────────────────────────────────────────────

    private static readonly Dictionary<BodyPartType, string> HealthLabels = new()
    {
        { BodyPartType.Head,     "ГОЛОВА"  },
        { BodyPartType.Chest,    "ГРУДЬ"   },
        { BodyPartType.Stomach,  "ЖИВОТ"   },
        { BodyPartType.LeftArm,  "Л. РУКА" },
        { BodyPartType.RightArm, "П. РУКА" },
        { BodyPartType.LeftLeg,  "Л. НОГА" },
        { BodyPartType.RightLeg, "П. НОГА" },
    };

    private static readonly Color HealthyColor   = new(0.85f, 0.30f, 0.30f, 1f);
    private static readonly Color DestroyedColor = new(0.05f, 0.05f, 0.05f, 1f);

    private void BuildHealthPanel()
    {
        if (Layout == null || !Layout.healthPanelEnabled) return;
        // Модуль здоровья не подключён к игроку — панель не строим.
        if (HealthSystem.Instance == null) return;

        // Лейбл «ЗДОРОВЬЕ» — самостоятельный draggable + общая «ручка» для всех
        // 7 столбиков. Позиция лейбла = Layout.healthPanelPos + (0, 16) (на 16px выше
        // первой строки). Сама Layout.healthPanelPos управляется ключом "HealthLabel".
        var labelHandle = CreateGroupHealthLabel("ЗДОРОВЬЕ",
            new Vector2(Layout.healthPanelPos.x, Layout.healthPanelPos.y + 16f),
            new Vector2(Layout.healthBarSize.x, 16f));

        var childHandles = new System.Collections.Generic.List<LayoutDragHandle>();

        int row = 0;
        foreach (var p in HealthSystem.AllParts())
        {
            string panelId = $"Health_{p}";
            // Если игрок индивидуально подвинул этот столбик — используем его override-позицию.
            // Иначе — стандартная row-раскладка от Layout.healthPanelPos.
            Vector2 pos;
            if (customization == null || !customization.TryGetPanel(panelId, out pos))
            {
                pos = new Vector2(
                    Layout.healthPanelPos.x,
                    Layout.healthPanelPos.y - row * (Layout.healthBarSize.y + Layout.healthBarGap));
            }
            string lbl = HealthLabels.TryGetValue(p, out var n) ? n : p.ToString();
            healthBars[p] = CreateHealthBar($"HealthBar_{p}", panelId, pos, Layout.healthBarSize, lbl);

            // Запоминаем handle столбика — лейбл будет двигать их группой.
            var ch = healthBars[p].fillRt != null
                ? healthBars[p].fillRt.GetComponentInParent<LayoutDragHandle>() : null;
            if (ch != null) childHandles.Add(ch);

            row++;
        }

        if (labelHandle != null) labelHandle.groupChildren = childHandles.ToArray();
    }

    /// <summary>Создаёт draggable-лейбл «ЗДОРОВЬЕ», работающий как групповая ручка.</summary>
    private LayoutDragHandle CreateGroupHealthLabel(string text, Vector2 pos, Vector2 size)
    {
        var root = new GameObject("HealthLabel", typeof(RectTransform));
        root.transform.SetParent(content.transform, false);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        // Прозрачный raycast-target — handle ловит drag по всему прямоугольнику лейбла.
        var img = root.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.001f);
        img.raycastTarget = true;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(root.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var t = textGo.GetComponent<Text>();
        t.font = GetFont();
        t.text = text;
        t.fontSize = 12;
        t.alignment = TextAnchor.UpperLeft;
        t.color = new Color(0.75f, 0.75f, 0.75f);
        t.raycastTarget = false;

        var handle = root.AddComponent<LayoutDragHandle>();
        handle.PanelId = "HealthLabel";
        LayoutEditMode.Instance?.Register(handle);
        return handle;
    }

    private HealthBarView CreateHealthBar(string name, string panelId, Vector2 pos, Vector2 size, string labelText)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(content.transform, false);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        // LayoutDragHandle позволяет двигать отдельный столбик в edit-mode.
        var handle = root.AddComponent<LayoutDragHandle>();
        handle.PanelId = panelId;
        LayoutEditMode.Instance?.Register(handle);

        // BG
        var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(root.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bgGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // Fill
        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(root.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot     = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = new Vector2(2f, 0f);
        fillRt.sizeDelta = new Vector2(size.x - 4f, -4f);
        var fillImg = fillGo.GetComponent<Image>();
        fillImg.color = HealthyColor;

        // Label
        var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        lblGo.transform.SetParent(root.transform, false);
        var lblRt = lblGo.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = new Vector2(8f, 0f); lblRt.offsetMax = new Vector2(-8f, 0f);
        var lbl = lblGo.GetComponent<Text>();
        lbl.font = GetFont();
        lbl.fontSize = 11;
        lbl.alignment = TextAnchor.MiddleLeft;
        lbl.color = new Color(1f, 1f, 1f, 0.95f);
        lbl.text = labelText;
        lbl.raycastTarget = false;

        return new HealthBarView { fillImage = fillImg, fillRt = fillRt, label = lbl };
    }

    public void RefreshHealth()
    {
        var hs = HealthSystem.Instance;
        if (hs == null || Layout == null) return;

        float innerW = Mathf.Max(0f, Layout.healthBarSize.x - 4f);
        foreach (var kv in healthBars)
        {
            var p    = kv.Key;
            var view = kv.Value;
            if (view == null) continue;

            float hp        = hs.GetHp(p);
            float max       = hs.GetMaxHp(p);
            bool  isDestroy = hs.IsDestroyed(p);

            if (isDestroy)
            {
                view.fillImage.color = DestroyedColor;
                view.fillRt.sizeDelta = new Vector2(innerW, -4f);
            }
            else
            {
                view.fillImage.color = HealthyColor;
                float n = max > 0f ? Mathf.Clamp01(hp / max) : 0f;
                view.fillRt.sizeDelta = new Vector2(innerW * n, -4f);
            }

            if (view.label != null)
            {
                string name = HealthLabels.TryGetValue(p, out var n) ? n : p.ToString();
                view.label.text = isDestroy
                    ? $"{name}  —"
                    : $"{name}  {Mathf.CeilToInt(hp)}/{Mathf.RoundToInt(max)}";
            }
        }
    }

    // ── Nested container window ───────────────────────────────────────────────

    public void OpenNestedContainer(GridContainer container, string title)
    {
        if (container == null) return;
        if (nestedWindowsByContainer.TryGetValue(container.containerId, out var existing) && existing != null) return;

        InventoryManager.Instance?.RegisterContainer(container);

        float gridW = container.width  * (GridUI.CellSize + GridUI.CellGap) + 16f;
        float gridH = container.height * (GridUI.CellSize + GridUI.CellGap) + 48f;

        var win = MakePanel($"Win_{title}", rootCanvas.transform, new Color(0.09f, 0.09f, 0.09f, 0.97f));
        var winRt = win.GetComponent<RectTransform>();
        winRt.anchorMin = winRt.anchorMax = new Vector2(0.5f, 0.5f);
        winRt.pivot     = new Vector2(0.5f, 0.5f);
        winRt.sizeDelta = new Vector2(gridW, gridH);
        winRt.anchoredPosition = new Vector2(
            Random.Range(-150f, 150f), Random.Range(-80f, 80f));

        var titleBar = MakePanel("TitleBar_W", win.transform, new Color(0.05f, 0.05f, 0.05f, 1f));
        SetRectTL(titleBar.GetComponent<RectTransform>(), 0, 0, gridW, 24);
        MakeLabel(title.ToUpper(), titleBar.transform, 13, TextAnchor.MiddleCenter,
                  new Rect(0, 0, gridW - 24, 24));
        var dragger = win.AddComponent<UIDragger>();
        dragger.RootCanvas = rootCanvas;

        string containerId = container.containerId;
        MakeButton("X", titleBar.transform, new Rect(gridW - 24, 0, 24, 24),
            () => {
                nestedWindowsByContainer.Remove(containerId);
                nestedWindows.Remove(win);
                Destroy(win);
            });

        var gridUI = CreateGridUI("Grid_" + title, win.transform, new Vector2(8, -24));
        gridUI.Init(container);

        nestedWindows.Add(win);
        nestedWindowsByContainer[container.containerId] = win;
    }

    // ── Open / Close / Refresh ────────────────────────────────────────────────

    public void Toggle() { if (IsOpen) Close(); else Open(); }

    public void Open()
    {
        IsOpen = true;
        inventoryPanel.SetActive(true);
        if (manageCursor)
        {
            Cursor.visible   = true;
            Cursor.lockState = CursorLockMode.None;
        }
        OnInventoryStateChanged?.Invoke(true);
        Refresh();
    }

    public void Close()
    {
        IsOpen = false;
        DragDropController.Instance?.CancelDrag();
        // Edit-panel живёт на rootCanvas (не на inventoryPanel) — без явного Toggle
        // он остался бы виден даже при закрытом инвентаре.
        if (LayoutEditMode.IsActive) LayoutEditMode.Instance.Toggle();
        inventoryPanel.SetActive(false);
        CloseAllNested();
        ContextMenuUI.Instance?.Hide();
        InspectWindowUI.Instance?.Close();
        if (manageCursor && lockCursorOnClose)
        {
            Cursor.visible   = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
        OnInventoryStateChanged?.Invoke(false);
    }

    public void Refresh()
    {
        // Итерируемся по списку (включает indicators) — dictionary бы пропустил их,
        // если бы их тип уже занимал обычный equipment-slot.
        for (int i = 0; i < allEquipmentSlotUIs.Count; i++) allEquipmentSlotUIs[i]?.Refresh();
        RefreshContainerPanel(EquipmentSlotType.Backpack,   backpackPanelGO,   ref backpackGridUI,   ref lastBackpackContainerId,   "РЮКЗАК");
        RefreshContainerPanel(EquipmentSlotType.Rig,        rigPanelGO,        ref rigGridUI,        ref lastRigContainerId,        "РАЗГРУЗКА");
        RefreshContainerPanel(EquipmentSlotType.SecureCase, secureCasePanelGO, ref secureCaseGridUI, ref lastSecureCaseContainerId, "КЕЙС");

        var mgr = InventoryManager.Instance;
        int pocketCount = mgr != null ? Mathf.Min(pocketUIs.Length, mgr.Pockets.Length) : 0;
        for (int i = 0; i < pocketCount; i++)
        {
            if (pocketUIs[i] == null) continue;
            if (pocketUIs[i].Container != mgr.Pockets[i]) pocketUIs[i].Init(mgr.Pockets[i]);
            else pocketUIs[i].Refresh();
        }

        RefreshHunger();
        RefreshThirst();
        RefreshHealth();
    }

    private void RefreshContainerPanel(EquipmentSlotType slotType, GameObject host,
                                       ref GridUI cachedGrid, ref string lastContainerId, string title)
    {
        if (host == null) return;
        var mgr = InventoryManager.Instance;
        var slot = mgr?.GetSlot(slotType);
        var container = slot?.EquippedItem?.nestedContainer;

        if (container == null)
        {
            host.SetActive(false);
            lastContainerId = null;
            return;
        }

        host.SetActive(true);
        InventoryManager.Instance.RegisterContainer(container);

        if (cachedGrid == null || lastContainerId != container.containerId)
        {
            foreach (Transform t in host.transform) Destroy(t.gameObject);

            MakeLabel(title, host.transform, 11, TextAnchor.UpperLeft, new Rect(0, 0, 200, 18));
            cachedGrid = CreateGridUI($"Grid_{slotType}", host.transform, new Vector2(0, -20));
            cachedGrid.Init(container);
            lastContainerId = container.containerId;
        }
        else
        {
            cachedGrid.Refresh();
        }
    }

    private void CloseAllNested()
    {
        foreach (var w in nestedWindows) if (w != null) Destroy(w);
        nestedWindows.Clear();
        nestedWindowsByContainer.Clear();
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private GridUI CreateGridUI(string name, Transform parent, Vector2 anchoredPos)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;

        var cellsGo = new GameObject("Cells", typeof(RectTransform));
        cellsGo.transform.SetParent(go.transform, false);

        var itemsGo = new GameObject("Items", typeof(RectTransform));
        itemsGo.transform.SetParent(go.transform, false);

        var ui = go.AddComponent<GridUI>();
        ui.cellsParent = cellsGo.GetComponent<RectTransform>();
        ui.itemsParent = itemsGo.GetComponent<RectTransform>();
        return ui;
    }

    private static GameObject MakePanel(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go;
    }

    private static void SetRectTL(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin        = new Vector2(0, 1);
        rt.anchorMax        = new Vector2(0, 1);
        rt.pivot            = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(x, y);
        rt.sizeDelta        = new Vector2(w, h);
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void MakeLabel(string text, Transform parent, int fontSize,
                                   TextAnchor anchor, Rect rect)
    {
        var go = new GameObject("Lbl_" + text, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 1);
        rt.anchoredPosition = new Vector2(rect.x, rect.y);
        rt.sizeDelta        = new Vector2(rect.width, rect.height);

        var t = go.GetComponent<Text>();
        t.font      = GetFont();
        t.text      = text;
        t.fontSize  = fontSize;
        t.alignment = anchor;
        t.color     = new Color(0.75f, 0.75f, 0.75f, 1f);
        t.raycastTarget = false;
    }

    private static void MakeButton(string label, Transform parent, Rect rect, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
        rt.pivot     = new Vector2(0, 0);
        rt.anchoredPosition = new Vector2(rect.x, rect.y);
        rt.sizeDelta        = new Vector2(rect.width, rect.height);

        var img = go.GetComponent<Image>();
        img.color = new Color(0.20f, 0.20f, 0.20f, 1f);

        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var cols = btn.colors;
        cols.highlightedColor = new Color(0.30f, 0.30f, 0.30f, 1f);
        cols.pressedColor     = new Color(0.12f, 0.12f, 0.12f, 1f);
        btn.colors = cols;
        btn.onClick.AddListener(onClick);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var t = textGo.GetComponent<Text>();
        t.font      = GetFont();
        t.text      = label;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontSize  = 13;
        t.color     = Color.white;
        t.raycastTarget = false;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(es);
    }
}
