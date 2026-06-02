using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Универсальное контекстное меню для мира (RTS/TopDown/любое). По стилю —
/// клон <see cref="ContextMenuUI"/> из инвентаря, но не привязано к ItemInstance:
/// принимает произвольный список (label, action). Авто-закрытие на RMB/Escape.
///
/// Использование:
/// <code>
/// WorldContextMenu.Ensure().Show(Input.mousePosition,
///     ("Идти сюда", () => unit.MoveTo(point)),
///     ("Стоять",    () => unit.MoveTo(unit.transform.position)));
/// </code>
/// </summary>
[DefaultExecutionOrder(-50)]
public class WorldContextMenu : MonoBehaviour
{
    public static WorldContextMenu Instance { get; private set; }

    [Header("Style")]
    public Vector2 buttonSize = new(160f, 30f);
    public float   buttonGap  = 2f;
    public float   padding    = 4f;
    public int     fontSize   = 13;
    public Color   panelColor     = new(0.08f, 0.08f, 0.08f, 0.97f);
    public Color   buttonColor    = new(0.18f, 0.18f, 0.18f, 1f);
    public Color   highlightColor = new(0.32f, 0.32f, 0.32f, 1f);
    public Color   pressedColor   = new(0.12f, 0.12f, 0.12f, 1f);
    [Range(0, 200)] public int sortingOrder = 110;

    public class Entry { public string label; public Action action; }

    private Canvas        canvas;
    private GameObject    panel;
    private RectTransform panelRt;
    private readonly List<GameObject> spawnedButtons = new();
    private int shownFrame = -1;

    public bool IsOpen => panel != null && panel.activeSelf;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        BuildCanvas();
        BuildPanel();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!IsOpen) return;
        if (Time.frameCount == shownFrame) return; // игнор кадра открытия
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))
            Hide();
    }

    /// <summary>Создать (если ещё нет) экземпляр меню и вернуть его. Удобно для контроллеров.</summary>
    public static WorldContextMenu Ensure()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("WorldContextMenu");
        return go.AddComponent<WorldContextMenu>();
    }

    private void BuildCanvas()
    {
        var go = new GameObject("WorldContextMenuCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);

        canvas = go.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;
    }

    private void BuildPanel()
    {
        panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        panel.GetComponent<Image>().color = panelColor;

        panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = panelRt.pivot = new Vector2(0f, 1f);
        panel.SetActive(false);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void Show(Vector2 screenPos, IReadOnlyList<Entry> entries)
    {
        if (entries == null || entries.Count == 0) { Hide(); return; }

        // Сбросить старые кнопки. Не пересоздаём панель/канвас — только кнопки.
        for (int i = 0; i < spawnedButtons.Count; i++)
            if (spawnedButtons[i] != null) Destroy(spawnedButtons[i]);
        spawnedButtons.Clear();

        float panelW = buttonSize.x + padding * 2f;
        float panelH = entries.Count * buttonSize.y
                     + (entries.Count - 1) * buttonGap
                     + padding * 2f;
        panelRt.sizeDelta = new Vector2(panelW, panelH);

        for (int i = 0; i < entries.Count; i++)
            AddButton(entries[i], i);

        panel.transform.SetAsLastSibling();
        panel.SetActive(true);
        panelRt.position = new Vector3(screenPos.x, screenPos.y, 0f);
        shownFrame = Time.frameCount;
    }

    /// <summary>Перегрузка для удобного inline-вызова с tuple'ами.</summary>
    public void Show(Vector2 screenPos, params (string label, Action action)[] items)
    {
        if (items == null || items.Length == 0) { Hide(); return; }
        var list = new List<Entry>(items.Length);
        for (int i = 0; i < items.Length; i++)
            list.Add(new Entry { label = items[i].label, action = items[i].action });
        Show(screenPos, list);
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    // ── Build button ─────────────────────────────────────────────────────────

    private void AddButton(Entry e, int row)
    {
        var go = new GameObject(e.label ?? $"Btn_{row}",
            typeof(RectTransform), typeof(Image), typeof(Button));
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
        var captured = e.action;
        btn.onClick.AddListener(() => { captured?.Invoke(); Hide(); });

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var trt = textGo.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(6f, 0f); trt.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.font          = HudFont.Get();
        text.text          = e.label;
        text.alignment     = TextAnchor.MiddleLeft;
        text.fontSize      = fontSize;
        text.color         = Color.white;
        text.raycastTarget = false;

        spawnedButtons.Add(go);
    }
}
