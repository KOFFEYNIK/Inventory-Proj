using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD-полоска жажды. Опциональный модуль — если на игроке нет
/// <see cref="ThirstSystem"/>, компонент сам себя выключает.
/// </summary>
[DefaultExecutionOrder(-80)]
public class ThirstHUD : MonoBehaviour
{
    public static ThirstHUD Instance { get; private set; }

    [Header("Layout (top-left origin)")]
    public Vector2 anchoredPosition = new(20f, -44f);
    public Vector2 barSize          = new(220f, 18f);
    public int     labelFontSize    = 11;
    public bool    showLabel        = true;
    [Range(0, 200)] public int sortingOrder = 50;

    [Header("Colors")]
    public Color fillColor       = new(0.20f, 0.55f, 0.95f, 1f);
    public Color backgroundColor = new(0f, 0f, 0f, 0.55f);
    public Color labelColor      = new(1f, 1f, 1f, 0.9f);

    private Canvas        canvas;
    private RectTransform rootRt, fillRt;
    private Text          label;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildCanvas();
        BuildBar();
    }

    void OnEnable()
    {
        Subscribe();
        Refresh();
    }

    void OnDisable()
    {
        if (ThirstSystem.Instance != null)
            ThirstSystem.Instance.OnChanged -= Refresh;
    }

    void Start()
    {
        Subscribe();
        Refresh();

        if (ThirstSystem.Instance == null && rootRt != null)
            rootRt.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Subscribe()
    {
        if (ThirstSystem.Instance == null) return;
        ThirstSystem.Instance.OnChanged -= Refresh;
        ThirstSystem.Instance.OnChanged += Refresh;
    }

    private void BuildCanvas()
    {
        var go = new GameObject("ThirstHUDCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(transform, false);

        canvas = go.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight  = 0.5f;

        go.GetComponent<GraphicRaycaster>().enabled = false;
    }

    private void BuildBar()
    {
        var root = new GameObject("ThirstBar", typeof(RectTransform));
        root.transform.SetParent(canvas.transform, false);
        rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0f, 1f);
        rootRt.pivot     = new Vector2(0f, 1f);
        rootRt.sizeDelta = barSize;
        rootRt.anchoredPosition = anchoredPosition;

        var bgGo = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(root.transform, false);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bgGo.GetComponent<Image>().color = backgroundColor;

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(root.transform, false);
        fillRt = fillGo.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot     = new Vector2(0f, 0.5f);
        fillRt.anchoredPosition = new Vector2(2f, 0f);
        fillGo.GetComponent<Image>().color = fillColor;

        if (showLabel)
        {
            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(root.transform, false);
            var lblRt = lblGo.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(8f, 0f); lblRt.offsetMax = new Vector2(-8f, 0f);

            label = lblGo.GetComponent<Text>();
            label.font = HudFont.Get();
            label.fontSize = labelFontSize;
            label.color = labelColor;
            label.alignment = TextAnchor.MiddleLeft;
            label.text = "ЖАЖДА";
            label.raycastTarget = false;
        }
    }

    public void Refresh()
    {
        var v = ThirstSystem.Instance;
        if (v == null)
        {
            if (rootRt != null) rootRt.gameObject.SetActive(false);
            return;
        }

        if (rootRt != null && !rootRt.gameObject.activeSelf) rootRt.gameObject.SetActive(true);

        float innerW = Mathf.Max(0f, barSize.x - 4f);
        if (fillRt != null)
            fillRt.sizeDelta = new Vector2(innerW * v.ThirstNormalized, -4f);
        if (label != null)
            label.text = $"ЖАЖДА  {Mathf.RoundToInt(v.Thirst)}/{Mathf.RoundToInt(v.maxThirst)}";
    }
}
