using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Окно осмотра предмета:
/// - 3D-вьюпорт через RenderTexture + изолированную камеру
/// - Описание, тип, вес, размер, заполненность контейнера
/// - ЛКМ + drag по вьюпорту — вращение модели (yaw/pitch)
/// - Колесо мыши над вьюпортом — приближение/отдаление
/// </summary>
public class InspectWindowUI : MonoBehaviour
{
    public static InspectWindowUI Instance { get; private set; }

    public Canvas RootCanvas;

    // ── Layout ────────────────────────────────────────────────────────────────
    private const float WindowW   = 520f;
    private const float WindowH   = 620f;
    private const float TitleH    = 28f;
    private const float ViewportH = 320f;
    private const float Padding   = 10f;

    // RenderTexture с пропорциями ~ viewport (500×320), чтобы модель не сплющивалась
    // при отрисовке через RawImage. Чуть кратные 8 размеры для GPU happy-path.
    private const int   RTWidth   = 640;
    private const int   RTHeight  = 408;

    // Помещаем модель далеко от игровой геометрии, чтобы инспекторная камера ничего лишнего не зацепила.
    // Не слишком далеко — float precision деградирует за ~5000 единиц от нуля.
    private static readonly Vector3 IsolationOrigin = new(0f, -1000f, 0f);

    // ── State ─────────────────────────────────────────────────────────────────
    private GameObject    panel;
    private RawImage      viewportImage;
    private Text          descriptionText;
    private Text          statsText;
    private Text          titleText;

    private RenderTexture renderTexture;
    private Camera        inspectCamera;
    private GameObject    inspectHost;     // контейнер с моделью (вращается)
    private GameObject    cameraRig;       // GameObject камеры и света

    private bool          isDragging;
    private bool          isTwoD;          // предмет в режиме WorldMode.TwoD — рендерим плоский спрайт лицом к камере
    private Vector2       lastMousePos;
    private float         yaw, pitch;
    private float         distance = 1.5f;
    private const float   MinDistance = 0.4f;
    private const float   MaxDistance = 8f;
    private const float   RotateSpeed = 0.4f;
    private const float   ZoomSpeed   = 0.4f;

    private static Font s_Font;
    private static Font GetFont()
    {
        if (s_Font != null) return s_Font;
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (s_Font == null) s_Font = Font.CreateDynamicFontFromOSFont("Arial", 12);
        return s_Font;
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        TeardownRender();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Show(ItemInstance item)
    {
        if (item == null || item.definition == null)
        {
            Debug.LogWarning("[InspectWindowUI] Show called with null item/definition");
            return;
        }
        Debug.Log($"[InspectWindowUI] Show '{item.definition.displayName}'");

        // Если уже открыто — закрываем чтобы не плодить окон/камер
        Close();
        Build(item);
    }

    public void Close()
    {
        if (panel != null) Destroy(panel);
        panel           = null;
        viewportImage   = null;
        descriptionText = null;
        statsText       = null;
        titleText       = null;

        TeardownRender();
        isDragging = false;
    }

    // ── Build ─────────────────────────────────────────────────────────────────

    private void Build(ItemInstance item)
    {
        if (RootCanvas == null) RootCanvas = GetComponentInParent<Canvas>();
        if (RootCanvas == null) { Debug.LogWarning("[InspectWindowUI] No RootCanvas"); return; }

        // Определяем режим ДО BuildRender — SpawnInspectModel ветвится по нему.
        isTwoD = item.definition.worldMode == WorldMode.TwoD;

        BuildRender(item);
        BuildPanel(item);
        UpdateStats(item);

        // Стартовые углы и дистанция. Для 2D-спрайта вращение не нужно — он плоский,
        // ставим строго лицом к камере (yaw/pitch = 0), иначе спрайт виден «карточкой».
        yaw      = isTwoD ? 0f : 25f;
        pitch    = isTwoD ? 0f : -15f;
        distance = ComputeAutoDistance(item.definition);
        ApplyCameraTransform();
    }

    private void BuildPanel(ItemInstance item)
    {
        panel = MakePanel("InspectWindow", RootCanvas.transform, new Color(0.08f, 0.08f, 0.08f, 0.97f));
        panel.transform.SetAsLastSibling();

        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(WindowW, WindowH);
        rt.anchoredPosition = new Vector2(120f, 0f); // чуть в стороне от центрального инвентаря

        // Title bar
        var titleBar = MakePanel("TitleBar", panel.transform, new Color(0.05f, 0.05f, 0.05f, 1f));
        SetRectTL(titleBar.GetComponent<RectTransform>(), 0, 0, WindowW, TitleH);
        titleText = MakeLabel($"ОСМОТР: {item.definition.displayName.ToUpper()}",
                              titleBar.transform, 13, TextAnchor.MiddleLeft,
                              new Rect(10, 0, WindowW - TitleH - 10, TitleH));

        // UIDragger вешаем ТОЛЬКО на тайтлбар (но двигает он окно panel),
        // иначе drag во вьюпорте начнёт тащить окно вместо вращения модели.
        var titleDragger = titleBar.AddComponent<UIDragger>();
        titleDragger.RootCanvas = RootCanvas;
        titleDragger.Target     = panel.GetComponent<RectTransform>();

        // Close
        MakeButton("X", titleBar.transform,
            new Rect(WindowW - TitleH, 0, TitleH, TitleH), Close);

        // Viewport: фон + отдельный child с RawImage (на одном GameObject Image и RawImage
        // конфликтуют за CanvasRenderer и RawImage не отрисуется).
        var vpHost = MakePanel("Viewport", panel.transform, new Color(0.04f, 0.04f, 0.04f, 1f));
        SetRectTL(vpHost.GetComponent<RectTransform>(),
                  Padding, -(TitleH + Padding), WindowW - Padding * 2, ViewportH);

        var rawGo = new GameObject("RawImage", typeof(RectTransform), typeof(RawImage));
        rawGo.transform.SetParent(vpHost.transform, false);
        var rawRt = rawGo.GetComponent<RectTransform>();
        rawRt.anchorMin = Vector2.zero; rawRt.anchorMax = Vector2.one;
        rawRt.offsetMin = rawRt.offsetMax = Vector2.zero;

        viewportImage = rawGo.GetComponent<RawImage>();
        viewportImage.texture       = renderTexture;
        viewportImage.raycastTarget = true;

        // Description
        float descY = TitleH + Padding + ViewportH + Padding;
        var descLbl = MakeLabel("ОПИСАНИЕ", panel.transform, 11, TextAnchor.UpperLeft,
                                new Rect(Padding, -descY, WindowW - Padding * 2, 16));
        descLbl.color = new Color(0.55f, 0.55f, 0.55f);

        var descGo = new GameObject("DescriptionText", typeof(RectTransform), typeof(Text));
        descGo.transform.SetParent(panel.transform, false);
        var descRt = descGo.GetComponent<RectTransform>();
        descRt.anchorMin = descRt.anchorMax = new Vector2(0f, 1f);
        descRt.pivot     = new Vector2(0f, 1f);
        descRt.anchoredPosition = new Vector2(Padding, -(descY + 18f));
        descRt.sizeDelta = new Vector2(WindowW - Padding * 2, 110f);

        descriptionText = descGo.GetComponent<Text>();
        descriptionText.font      = GetFont();
        descriptionText.text      = string.IsNullOrEmpty(item.definition.description)
            ? "<нет описания>"
            : item.definition.description;
        descriptionText.fontSize  = 12;
        descriptionText.alignment = TextAnchor.UpperLeft;
        descriptionText.color     = new Color(0.85f, 0.85f, 0.85f);
        descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descriptionText.verticalOverflow   = VerticalWrapMode.Truncate;
        descriptionText.raycastTarget      = false;

        // Stats
        float statsY = descY + 18f + 110f + Padding;
        var statsLbl = MakeLabel("ПАРАМЕТРЫ", panel.transform, 11, TextAnchor.UpperLeft,
                                 new Rect(Padding, -statsY, WindowW - Padding * 2, 16));
        statsLbl.color = new Color(0.55f, 0.55f, 0.55f);

        var statsGo = new GameObject("StatsText", typeof(RectTransform), typeof(Text));
        statsGo.transform.SetParent(panel.transform, false);
        var statsRt = statsGo.GetComponent<RectTransform>();
        statsRt.anchorMin = statsRt.anchorMax = new Vector2(0f, 1f);
        statsRt.pivot     = new Vector2(0f, 1f);
        statsRt.anchoredPosition = new Vector2(Padding, -(statsY + 18f));
        statsRt.sizeDelta = new Vector2(WindowW - Padding * 2, 100f);

        statsText = statsGo.GetComponent<Text>();
        statsText.font          = GetFont();
        statsText.fontSize      = 12;
        statsText.alignment     = TextAnchor.UpperLeft;
        statsText.color         = new Color(0.92f, 0.92f, 0.92f);
        statsText.raycastTarget = false;
    }

    // ── 3D render setup ───────────────────────────────────────────────────────

    private void BuildRender(ItemInstance item)
    {
        renderTexture = new RenderTexture(RTWidth, RTHeight, 24, RenderTextureFormat.ARGB32);
        renderTexture.name = "InspectRT";
        renderTexture.Create();

        // Камера + свет в изолированном месте
        cameraRig = new GameObject("InspectCameraRig");
        cameraRig.transform.position = IsolationOrigin;

        inspectCamera = new GameObject("InspectCamera", typeof(Camera)).GetComponent<Camera>();
        inspectCamera.transform.SetParent(cameraRig.transform, false);
        inspectCamera.clearFlags      = CameraClearFlags.SolidColor;
        inspectCamera.backgroundColor = new Color(0.05f, 0.05f, 0.06f, 1f);
        inspectCamera.fieldOfView     = 45f;
        inspectCamera.nearClipPlane   = 0.01f;
        inspectCamera.farClipPlane    = 50f;
        inspectCamera.targetTexture   = renderTexture;
        inspectCamera.cullingMask     = ~0;  // всё что попадает в frustum в изоляции
        inspectCamera.depth           = 99;
        inspectCamera.allowHDR        = false;
        inspectCamera.allowMSAA       = true;
        // Aspect наследуется из targetTexture (RTWidth/RTHeight) — модель не сплющивается.

        // ВАЖНО: Directional light НЕЛЬЗЯ — он освещает ВСЁ (включая основной мир)
        // и URP может выбрать его как main light → тени в мире начинают использовать
        // его направление. Используем Point light'ы — они светят в радиусе, далеко
        // от мира (camera rig на y=-1000), мир их не видит.
        CreateInspectPointLight("InspectKeyLight",  new Vector3( 1.2f,  1.5f, -1.5f), 2.5f, 8f, Color.white);
        CreateInspectPointLight("InspectFillLight", new Vector3(-1.0f,  0.5f, -1.0f), 1.0f, 6f, new Color(0.85f, 0.9f, 1f));
        CreateInspectPointLight("InspectRimLight",  new Vector3( 0.0f, -0.8f,  1.5f), 0.6f, 5f, new Color(1f, 0.95f, 0.85f));

        // Хост модели (нулевая позиция = IsolationOrigin)
        inspectHost = new GameObject("InspectHost");
        inspectHost.transform.position = IsolationOrigin;

        // Создаём модель
        var model = SpawnInspectModel(item, inspectHost.transform);
        // Центрируем модель в host (учитываем bounds)
        CenterModelOnHost(model);
    }

    private void CreateInspectPointLight(string name, Vector3 localPos, float intensity, float range, Color color)
    {
        var go = new GameObject(name, typeof(Light));
        go.transform.SetParent(cameraRig.transform, false);
        go.transform.localPosition = localPos;
        var light = go.GetComponent<Light>();
        light.type      = LightType.Point;
        light.range     = range;
        light.intensity = intensity;
        light.color     = color;
        light.shadows   = LightShadows.None;   // тени от inspect не нужны — экономим перформанс
    }

    private GameObject SpawnInspectModel(ItemInstance item, Transform parent)
    {
        GameObject model;
        var def = item.definition;

        if (def.ActiveWorldPrefab != null)
        {
            // Используется worldPrefab2D при WorldMode.TwoD, иначе worldPrefab3D.
            model = Instantiate(def.ActiveWorldPrefab, parent);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
        }
        else if (isTwoD)
        {
            // Fallback для 2D, если worldPrefab2D не задан — плоский спрайт из иконки.
            model = new GameObject("InspectSprite2D", typeof(SpriteRenderer));
            model.transform.SetParent(parent, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            var sr = model.GetComponent<SpriteRenderer>();
            sr.sprite = def.icon;
            if (def.icon == null) sr.color = new Color(0.4f, 0.7f, 0.4f);
        }
        else
        {
            model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.SetParent(parent, false);
            model.transform.localScale = new Vector3(
                Mathf.Max(0.4f, 0.25f * def.width),
                0.25f,
                Mathf.Max(0.4f, 0.25f * def.height));
            var r = model.GetComponent<Renderer>();
            if (r != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                r.material = new Material(shader);
                r.material.color = def.isContainer
                    ? new Color(0.3f, 0.5f, 0.7f)
                    : new Color(0.4f, 0.7f, 0.4f);
            }
        }

        // Отключаем всё, что может влиять на сцену / физику — и 3D, и 2D.
        // Без удаления Rigidbody2D спрайт падает под гравитацией и улетает из вьюпорта.
        foreach (var rb  in model.GetComponentsInChildren<Rigidbody>(true))     DestroyImmediate(rb);
        foreach (var rb2 in model.GetComponentsInChildren<Rigidbody2D>(true))   DestroyImmediate(rb2);
        foreach (var c   in model.GetComponentsInChildren<Collider>(true))      c.enabled = false;
        foreach (var c2  in model.GetComponentsInChildren<Collider2D>(true))    c2.enabled = false;
        foreach (var wi  in model.GetComponentsInChildren<WorldItemBase>(true)) DestroyImmediate(wi);

        // 2D: подготавливаем спрайты к показу в окне осмотра.
        if (isTwoD) PrepareSprites2D(model);

        return model;
    }

    /// <summary>
    /// Готовит спрайты 2D-модели к показу в окне осмотра: принудительно ставит unlit-материал
    /// (Sprites/Default). В 2D-URP часто используется Sprite-Lit-Default, который без Light2D
    /// рисуется ЧЁРНЫМ, а в окне только 3D Point-light'ы. Спрайт префаба не подменяется —
    /// рендерим префаб как есть (визуал задаётся самим префабом).
    /// </summary>
    private static void PrepareSprites2D(GameObject model)
    {
        var sprites = model.GetComponentsInChildren<SpriteRenderer>(true);
        if (sprites.Length == 0) return;

        var shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        var unlit = new Material(shader);

        foreach (var sr in sprites) sr.sharedMaterial = unlit;
    }

    private static void CenterModelOnHost(GameObject model)
    {
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        // Сдвигаем модель так, чтобы её центр совпал с центром host
        Vector3 offset = model.transform.parent.position - bounds.center;
        model.transform.position += offset;
    }

    private float ComputeAutoDistance(ItemDefinition def)
    {
        // Считаем диагональ от размера предмета в "единицах"
        float maxDim = Mathf.Max(def.width, def.height, 1);
        // Для prefab пересчитаем по реальным bounds после инстанса
        if (inspectHost != null)
        {
            var renderers = inspectHost.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                maxDim = b.extents.magnitude * 2f;
            }
        }
        return Mathf.Clamp(maxDim * 1.8f, MinDistance, MaxDistance);
    }

    private void TeardownRender()
    {
        if (inspectCamera != null) inspectCamera.targetTexture = null;
        if (cameraRig    != null) Destroy(cameraRig);
        if (inspectHost  != null) Destroy(inspectHost);
        if (renderTexture != null) { renderTexture.Release(); Destroy(renderTexture); }
        cameraRig = null; inspectHost = null; inspectCamera = null; renderTexture = null;
    }

    // ── Camera control ────────────────────────────────────────────────────────

    void Update()
    {
        if (panel == null || inspectHost == null) return;

        // Колесо мыши для зума (если курсор над вьюпортом)
        if (IsPointerOverViewport(Input.mousePosition))
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                distance = Mathf.Clamp(distance - scroll * ZoomSpeed, MinDistance, MaxDistance);
                ApplyCameraTransform();
            }
        }

        // Drag-rotation — только для 3D. Плоский 2D-спрайт вращать смысла нет
        // (на 90° он исчезает ребром), оставляем только зум колесом.
        if (!isTwoD && Input.GetMouseButtonDown(0) && IsPointerOverViewport(Input.mousePosition))
        {
            isDragging   = true;
            lastMousePos = Input.mousePosition;
        }
        if (!isTwoD && isDragging && Input.GetMouseButton(0))
        {
            Vector2 delta = (Vector2)Input.mousePosition - lastMousePos;
            lastMousePos  = Input.mousePosition;

            yaw   -= delta.x * RotateSpeed;
            pitch += delta.y * RotateSpeed;
            pitch  = Mathf.Clamp(pitch, -85f, 85f);

            ApplyHostRotation();
        }
        if (Input.GetMouseButtonUp(0)) isDragging = false;
    }

    private void ApplyHostRotation()
    {
        if (inspectHost == null) return;
        inspectHost.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private void ApplyCameraTransform()
    {
        if (inspectCamera == null) return;
        // Камера смотрит на центр host вдоль оси Z
        inspectCamera.transform.localPosition = new Vector3(0f, 0f, -distance);
        inspectCamera.transform.localRotation = Quaternion.identity;
        ApplyHostRotation();
    }

    private bool IsPointerOverViewport(Vector2 screenPos)
    {
        if (viewportImage == null) return false;
        return RectTransformUtility.RectangleContainsScreenPoint(
            viewportImage.rectTransform, screenPos,
            RootCanvas != null ? RootCanvas.worldCamera : null);
    }

    // ── Stats ─────────────────────────────────────────────────────────────────

    private void UpdateStats(ItemInstance item)
    {
        if (statsText == null) return;

        var def = item.definition;
        int totalCells = def.width * def.height;
        string sizeStr = totalCells > 1
            ? $"{def.width}×{def.height} ({totalCells} ячеек)"
            : $"{def.width}×{def.height} (1 ячейка)";

        string typeStr = def.itemType == ItemType.Generic ? "обычный" : def.itemType.ToString();
        string lines = $"Тип: {typeStr}\n" +
                       $"Вес: {item.TotalWeight:F2} кг\n" +
                       $"Размер: {sizeStr}";

        if (def.isContainer && item.nestedContainer != null)
        {
            int containerTotal    = item.nestedContainer.width * item.nestedContainer.height;
            int containerOccupied = CountOccupiedCells(item.nestedContainer);
            lines += $"\nСодержимое: {containerOccupied}/{containerTotal} ячеек";
            if (def.containerMaxWeight > 0f)
                lines += $"  •  {item.nestedContainer.TotalWeight:F2}/{def.containerMaxWeight:F2} кг";
        }

        if (def.MaxStack > 1)
            lines += $"\nСтак: {item.stackCount}/{def.MaxStack}";

        statsText.text = lines;
    }

    private static int CountOccupiedCells(GridContainer container)
    {
        int sum = 0;
        foreach (var (item, _) in container.GetAllItems())
            sum += item.CurrentWidth * item.CurrentHeight;
        return sum;
    }

    // ── UI helpers (locally cloned from InventoryUI for independence) ─────────

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

    private static Text MakeLabel(string text, Transform parent, int fontSize,
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
        t.font          = GetFont();
        t.text          = text;
        t.fontSize      = fontSize;
        t.alignment     = anchor;
        t.color         = new Color(0.85f, 0.85f, 0.85f);
        t.raycastTarget = false;
        return t;
    }

    private static void MakeButton(string label, Transform parent, Rect rect,
                                   UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
        rt.pivot     = new Vector2(0, 1);
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
        t.font          = GetFont();
        t.text          = label;
        t.alignment     = TextAnchor.MiddleCenter;
        t.fontSize      = 13;
        t.color         = Color.white;
        t.raycastTarget = false;
    }
}
