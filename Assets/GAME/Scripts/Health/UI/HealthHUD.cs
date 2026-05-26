using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Постоянный HUD здоровья: 7 горизонтальных полосок (по одной на часть тела).
/// Каждую полоску можно перетаскивать мышью. Позиции сохраняются в PlayerPrefs.
/// </summary>
[DefaultExecutionOrder(-80)]
public class HealthHUD : MonoBehaviour
{
    public static HealthHUD Instance { get; private set; }

    [Header("Layout (screen-space)")]
    [Tooltip("Начальная позиция блока (верхний-правый угол). Применяется только если позиция не была сохранена.")]
    public Vector2 anchoredPosition = new(-20f, -20f);
    public Vector2 barSize          = new(180f, 14f);
    public float   barGap           = 3f;
    public int     labelFontSize    = 10;
    public bool    showLabels       = true;
    [Range(0, 200)] public int sortingOrder = 49;

    [Header("Drag")]
    [Tooltip("Разрешить перетаскивание каждой полоски здоровья. Позиции сохраняются в PlayerPrefs.")]
    public bool draggable = true;

    [Header("Colors")]
    public Color healthyColor    = new(0.85f, 0.30f, 0.30f, 1f);
    public Color destroyedColor  = new(0.05f, 0.05f, 0.05f, 1f);
    public Color backgroundColor = new(0f, 0f, 0f, 0.55f);
    public Color labelColor      = new(1f, 1f, 1f, 0.9f);

    private Canvas           canvas;
    private GraphicRaycaster raycaster;

    private static readonly Dictionary<BodyPartType, string> Labels = new()
    {
        { BodyPartType.Head,     "ГОЛОВА"  },
        { BodyPartType.Chest,    "ГРУДЬ"   },
        { BodyPartType.Stomach,  "ЖИВОТ"   },
        { BodyPartType.LeftArm,  "Л. РУКА" },
        { BodyPartType.RightArm, "П. РУКА" },
        { BodyPartType.LeftLeg,  "Л. НОГА" },
        { BodyPartType.RightLeg, "П. НОГА" },
    };

    private class BarView
    {
        public RectTransform rootRt;
        public RectTransform fillRt;
        public Image         fillImage;
        public Image         overlayImage; // прозрачный слой для raycast / drag
        public Text          label;
        public int           row;
        public UIDragger     dragger;
    }

    private readonly Dictionary<BodyPartType, BarView> bars = new();

    private static Font s_Font;
    private static Font GetFont()
    {
        if (s_Font != null) return s_Font;
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (s_Font == null) s_Font = Font.CreateDynamicFontFromOSFont("Arial", 12);
        return s_Font;
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildCanvas();
        BuildBars();
    }

    void OnEnable()
    {
        if (HealthSystem.Instance != null)
            HealthSystem.Instance.OnHealthChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (HealthSystem.Instance != null)
            HealthSystem.Instance.OnHealthChanged -= Refresh;
    }

    void Start()
    {
        if (HealthSystem.Instance != null)
        {
            HealthSystem.Instance.OnHealthChanged -= Refresh;
            HealthSystem.Instance.OnHealthChanged += Refresh;
        }
        Refresh();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnApplicationQuit()
    {
        if (draggable) SavePositions();
    }

    // ── Build ────────────────────────────────────────────────────────────────

    private void BuildCanvas()
    {
        var go = new GameObject("HealthHUDCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);

        canvas = go.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        raycaster = go.GetComponent<GraphicRaycaster>();
        raycaster.enabled = draggable;
    }

    private void BuildBars()
    {
        int row = 0;
        foreach (var p in HealthSystem.AllParts())
        {
            bars[p] = CreateBar($"Bar_{p}", canvas.transform, Labels[p], row, p);
            row++;
        }
        ApplyLayout();
    }

    private BarView CreateBar(string name, Transform parent, string labelText, int row, BodyPartType part)
    {
        var root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(1f, 1f);

        // Прозрачный overlay для перехвата pointer-событий (drag)
        var overlay = root.AddComponent<Image>();
        overlay.color = Color.clear;
        overlay.raycastTarget = draggable;

        // BG
        var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(root.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.color = backgroundColor;
        bgImg.raycastTarget = false;

        // Fill
        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(root.transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot     = new Vector2(0f, 0.5f);
        var fillImg = fillGo.GetComponent<Image>();
        fillImg.color = healthyColor;
        fillImg.raycastTarget = false;

        // Label
        Text lbl = null;
        if (showLabels)
        {
            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(root.transform, false);
            var lblRt = lblGo.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(6f, 0f); lblRt.offsetMax = new Vector2(-6f, 0f);

            lbl = lblGo.GetComponent<Text>();
            lbl.font = GetFont();
            lbl.fontSize = labelFontSize;
            lbl.color = labelColor;
            lbl.alignment = TextAnchor.MiddleLeft;
            lbl.text = labelText;
            lbl.raycastTarget = false;
        }

        // UIDragger
        var dragger = root.AddComponent<UIDragger>();
        dragger.RootCanvas = canvas;
        dragger.enabled = draggable;

        // Сохраняем позицию в PlayerPrefs при отпускании
        var et = root.AddComponent<EventTrigger>();
        var entry = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        var capturedPart = part;
        var capturedRt   = rt;
        entry.callback.AddListener(_ =>
        {
            if (draggable) SaveBarPosition(capturedPart, capturedRt.anchoredPosition);
        });
        et.triggers.Add(entry);

        return new BarView
        {
            rootRt       = rt,
            fillRt       = fillRt,
            fillImage    = fillImg,
            overlayImage = overlay,
            label        = lbl,
            row          = row,
            dragger      = dragger,
        };
    }

    // ── Layout & Persistence ─────────────────────────────────────────────────

    private static string PrefsKey(BodyPartType part, string axis) =>
        $"HealthHUD_{part}_{axis}";

    private Vector2 DefaultPosition(int row) => new(
        anchoredPosition.x,
        anchoredPosition.y - row * (barSize.y + barGap));

    /// <summary>
    /// Применяет barSize/barGap/labelFontSize/colors ко всем полоскам.
    /// Позиция берётся из PlayerPrefs, если была сохранена, иначе — дефолтная.
    /// </summary>
    public void ApplyLayout()
    {
        if (bars.Count == 0) return;

        if (raycaster != null) raycaster.enabled = draggable;

        foreach (var kv in bars)
        {
            var view = kv.Value;
            if (view?.rootRt == null) continue;

            view.rootRt.sizeDelta = barSize;

            if (draggable && PlayerPrefs.HasKey(PrefsKey(kv.Key, "x")))
            {
                view.rootRt.anchoredPosition = new Vector2(
                    PlayerPrefs.GetFloat(PrefsKey(kv.Key, "x")),
                    PlayerPrefs.GetFloat(PrefsKey(kv.Key, "y")));
            }
            else
            {
                view.rootRt.anchoredPosition = DefaultPosition(view.row);
            }

            view.fillRt.anchoredPosition = new Vector2(2f, 0f);

            if (view.overlayImage != null)
                view.overlayImage.raycastTarget = draggable;

            if (view.dragger != null)
                view.dragger.enabled = draggable;

            if (view.label != null)
            {
                view.label.fontSize = labelFontSize;
                view.label.color    = labelColor;
                view.label.gameObject.SetActive(showLabels);
            }
        }

        Refresh();
    }

    /// <summary>Сбрасывает позиции всех полосок к дефолтным и удаляет сохранённые позиции.</summary>
    public void ResetBarPositions()
    {
        foreach (var kv in bars)
        {
            PlayerPrefs.DeleteKey(PrefsKey(kv.Key, "x"));
            PlayerPrefs.DeleteKey(PrefsKey(kv.Key, "y"));
            if (kv.Value?.rootRt != null)
                kv.Value.rootRt.anchoredPosition = DefaultPosition(kv.Value.row);
        }
        PlayerPrefs.Save();
    }

    private void SaveBarPosition(BodyPartType part, Vector2 pos)
    {
        PlayerPrefs.SetFloat(PrefsKey(part, "x"), pos.x);
        PlayerPrefs.SetFloat(PrefsKey(part, "y"), pos.y);
        PlayerPrefs.Save();
    }

    private void SavePositions()
    {
        foreach (var kv in bars)
        {
            if (kv.Value?.rootRt != null)
                SaveBarPosition(kv.Key, kv.Value.rootRt.anchoredPosition);
        }
    }

    // ── Refresh ──────────────────────────────────────────────────────────────

    public void Refresh()
    {
        var hs = HealthSystem.Instance;
        if (hs == null) return;

        float innerW = Mathf.Max(0f, barSize.x - 4f);

        foreach (var p in HealthSystem.AllParts())
        {
            if (!bars.TryGetValue(p, out var view)) continue;

            float hp        = hs.GetHp(p);
            float max       = hs.GetMaxHp(p);
            bool  isDestroy = hs.IsDestroyed(p);

            if (isDestroy)
            {
                view.fillImage.color  = destroyedColor;
                view.fillRt.sizeDelta = new Vector2(innerW, -4f);
            }
            else
            {
                view.fillImage.color = healthyColor;
                float n = max > 0f ? Mathf.Clamp01(hp / max) : 0f;
                view.fillRt.sizeDelta = new Vector2(innerW * n, -4f);
            }

            if (view.label != null)
            {
                string labelName = Labels.TryGetValue(p, out var n) ? n : p.ToString();
                view.label.text = isDestroy
                    ? $"{labelName}  —"
                    : $"{labelName}  {Mathf.CeilToInt(hp)}/{Mathf.RoundToInt(max)}";
            }
        }
    }

    void OnValidate()
    {
        if (canvas != null) canvas.sortingOrder = sortingOrder;
        if (bars != null && bars.Count > 0) ApplyLayout();
    }
}
