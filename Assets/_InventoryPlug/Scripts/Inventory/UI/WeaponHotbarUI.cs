using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DefaultExecutionOrder(-70)]
public class WeaponHotbarUI : MonoBehaviour
{
    public static WeaponHotbarUI Instance { get; private set; }

    [Header("Layout")]
    public float slotSize     = 64f;
    public float slotGap      = 4f;
    public float bottomOffset = 24f;
    public float padding      = 6f;

    private Canvas              hotbarCanvas;
    private GameObject          panel;
    private WeaponHotbarSlotUI[] slotUIs;

    private static Font s_Font;
    private static Font GetFont()
    {
        if (s_Font != null) return s_Font;
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (s_Font == null) s_Font = Font.CreateDynamicFontFromOSFont("Arial", 12);
        return s_Font;
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureEventSystem();
        BuildCanvas();
        BuildHotbar();
    }

    void Start()
    {
        var hb = WeaponHotbar.Instance;
        if (hb != null)
        {
            hb.OnSlotsChanged   += HandleSlotsChanged;
            hb.OnActiveChanged  += OnActiveChanged;
            hb.OnEnabledChanged += HandleEnabledChanged;
            ApplyEnabledState(hb.IsHotbarEnabled);
        }
        RefreshAll();
    }

    void OnDestroy()
    {
        var hb = WeaponHotbar.Instance;
        if (hb != null)
        {
            hb.OnSlotsChanged   -= HandleSlotsChanged;
            hb.OnActiveChanged  -= OnActiveChanged;
            hb.OnEnabledChanged -= HandleEnabledChanged;
        }
        if (Instance == this) Instance = null;
    }

    private void HandleEnabledChanged(bool enabled) => ApplyEnabledState(enabled);

    private void ApplyEnabledState(bool enabled)
    {
        if (panel != null) panel.SetActive(enabled);
    }

    /// <summary>Хотбар сообщил, что состав слотов изменился (включая смену количества).
    /// Если число слотов отличается от того, что мы построили — пересобираем UI с нуля.</summary>
    private void HandleSlotsChanged()
    {
        int currentUiCount = slotUIs != null ? slotUIs.Length : 0;
        int desired = WeaponHotbar.Instance != null ? WeaponHotbar.Instance.SlotCount : 0;
        if (currentUiCount != desired)
        {
            RebuildHotbar();
            return;
        }
        RefreshAll();
    }

    private void RebuildHotbar()
    {
        if (panel != null) { Destroy(panel); panel = null; slotUIs = null; }
        BuildHotbar();
        RefreshAll();
        if (WeaponHotbar.Instance != null)
            ApplyEnabledState(WeaponHotbar.Instance.IsHotbarEnabled);
    }

    private void BuildCanvas()
    {
        var go = new GameObject("WeaponHotbarCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(go);

        hotbarCanvas = go.GetComponent<Canvas>();
        hotbarCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        hotbarCanvas.sortingOrder = 90; // ниже InventoryCanvas (100)

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
    }

    private void BuildHotbar()
    {
        int slotCount = WeaponHotbar.Instance != null ? WeaponHotbar.Instance.SlotCount : 0;
        if (slotCount <= 0) return;
        float w = slotCount * slotSize + (slotCount - 1) * slotGap + padding * 2f;
        float h = slotSize + padding * 2f;

        panel = new GameObject("HotbarPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(hotbarCanvas.transform, false);
        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, bottomOffset);
        rt.sizeDelta = new Vector2(w, h);

        var bg = panel.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        slotUIs = new WeaponHotbarSlotUI[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            var slotGo = new GameObject($"HotbarSlot_{i}", typeof(RectTransform), typeof(Image));
            slotGo.transform.SetParent(panel.transform, false);
            var srt = slotGo.GetComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot     = new Vector2(0f, 0.5f);
            srt.sizeDelta = new Vector2(slotSize, slotSize);
            srt.anchoredPosition = new Vector2(padding + i * (slotSize + slotGap), 0f);

            var ui = slotGo.AddComponent<WeaponHotbarSlotUI>();
            ui.Init(i, GetFont());
            slotUIs[i] = ui;
        }
    }

    private void RefreshAll()
    {
        if (slotUIs == null) return;
        for (int i = 0; i < slotUIs.Length; i++) slotUIs[i]?.Refresh();
    }

    private void OnActiveChanged(int _) => RefreshAll();

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(es);
    }
}
