using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// База статичного мирового контейнера (ящик / сундук). В отличие от <see cref="WorldItemBase"/>
/// его НЕЛЬЗЯ подобрать — он стоит в мире и хранит свой <see cref="GridContainer"/> с настраиваемым
/// числом ячеек (<see cref="gridWidth"/> × <see cref="gridHeight"/>).
///
/// По наведению камеры показывает world-space подсказку «[F] Открыть», а <see cref="Interact"/>
/// (через <see cref="IWorldInteractable"/>) открывает окно содержимого рядом с инвентарём игрока.
///
/// Содержимое живёт только в рантайме (сессия): при перезагрузке сцены контейнер пересоздаётся
/// пустым. Персист (save/load) — отдельная задача.
///
/// 2D/3D-специфика (тип коллайдера, ориентация подсказки) — в наследниках
/// <see cref="WorldContainer3D"/> / <see cref="WorldContainer2D"/>.
/// </summary>
public abstract class WorldContainerBase : MonoBehaviour, IWorldInteractable
{
    [Header("Container")]
    [Tooltip("Заголовок окна. Пусто → имя объекта.")]
    public string displayTitle = "";

    [Tooltip("Ширина сетки в ячейках.")]
    [Min(1)] public int gridWidth = 6;

    [Tooltip("Высота сетки в ячейках.")]
    [Min(1)] public int gridHeight = 4;

    [Tooltip("Лимит суммарного веса содержимого (0 = без лимита).")]
    [Min(0f)] public float maxWeight = 0f;

    [Header("Interaction")]
    [Tooltip("Клавиша открытия. Срабатывает только когда курсор наведён на ящик (HoverPickup на камере).")]
    public KeyCode openKey = KeyCode.F;

    /// <summary>Рантайм-контейнер с содержимым. Создаётся лениво, живёт пока существует объект.</summary>
    [System.NonSerialized] public GridContainer Container;

    protected GameObject promptUI;
    protected Text       promptText;

    private static Font s_Font;
    protected static Font GetFont()
    {
        if (s_Font != null) return s_Font;
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (s_Font == null) s_Font = Font.CreateDynamicFontFromOSFont("Arial", 12);
        return s_Font;
    }

    /// <summary>Заголовок окна: <see cref="displayTitle"/> либо имя объекта.</summary>
    public string Title => string.IsNullOrEmpty(displayTitle) ? gameObject.name : displayTitle;

    // ── Unity ────────────────────────────────────────────────────────────────

    protected virtual void Awake() => EnsureContainer();

    protected virtual void Start()
    {
        EnsureCollider();
        EnsureContainer();
        BuildPromptUI();
        SetPromptVisible(false);
        // Регистрируем в реестре инвентаря, чтобы drag-drop/поиск по id видели контейнер.
        Inventory.Service?.RegisterContainerRecursive(Container);
    }

    protected virtual void LateUpdate()
    {
        if (promptUI != null && promptUI.activeSelf) OrientPrompt();
    }

    /// <summary>Лениво создать рантайм-контейнер по текущим настройкам. id стабилен в пределах
    /// жизни объекта (через InstanceID), чтобы реестр инвентаря не плодил дубликаты.</summary>
    public void EnsureContainer()
    {
        if (Container != null) return;
        Container = new GridContainer(
            Mathf.Max(1, gridWidth),
            Mathf.Max(1, gridHeight),
            Mathf.Max(0f, maxWeight))
        {
            containerId = $"WorldContainer_{GetInstanceID()}"
        };
    }

    /// <summary>Каждый наследник гарантирует нужный коллайдер (не-триггер) для хвата камерой.</summary>
    protected abstract void EnsureCollider();

    /// <summary>3D: билборд к камере. 2D: фиксированная плоскость / no-op.</summary>
    protected abstract void OrientPrompt();

    // ── IWorldInteractable ────────────────────────────────────────────────────

    public void SetPromptVisible(bool visible)
    {
        if (promptUI != null) promptUI.SetActive(visible);
    }

    /// <summary>Открыть окно ящика (то же, что <see cref="Open"/>).</summary>
    public void Interact() => Open();

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Открыть окно содержимого ящика рядом с инвентарём игрока.</summary>
    public void Open()
    {
        EnsureContainer();
        var svc = Inventory.Service;
        if (svc == null)
        {
            Debug.LogWarning("[WorldContainer] Inventory.Service == null — некому открыть окно ящика.");
            return;
        }
        svc.RegisterContainerRecursive(Container);
        svc.OpenContainerWindow(Container, Title);
    }

    // ── Prompt UI ─────────────────────────────────────────────────────────────

    protected virtual Vector3 PromptLocalOffset => Vector3.up * 0.8f;
    protected virtual float   PromptLocalScale  => 0.01f;

    private void BuildPromptUI()
    {
        var canvasGo = new GameObject("ContainerPromptCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = PromptLocalOffset;
        canvasGo.transform.localScale    = Vector3.one * PromptLocalScale;

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 50f);

        var bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGo.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(canvasGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero; textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(4, 4); textRt.offsetMax = new Vector2(-4, -4);

        promptText = textGo.GetComponent<Text>();
        promptText.font      = GetFont();
        promptText.alignment = TextAnchor.MiddleCenter;
        promptText.fontSize  = 14;
        promptText.color     = Color.white;
        UpdatePromptText();

        promptUI = canvasGo;
    }

    protected void UpdatePromptText()
    {
        if (promptText == null) return;
        promptText.text = $"[{openKey}] Открыть\n{Title}";
    }
}
