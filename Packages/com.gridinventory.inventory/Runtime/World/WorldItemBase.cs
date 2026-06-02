using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// База для всех мировых пикапов. Содержит общие данные (ItemDefinition, stackCount,
/// preservedInstance), логику подбора (TryPickup) и world-space подсказку. 2D/3D-специфика
/// (ориентация подсказки к камере, физика дропа) — в наследниках.
/// </summary>
public abstract class WorldItemBase : MonoBehaviour, IWorldInteractable
{
    [Header("Item")]
    public ItemDefinition definition;
    public int stackCount = 1;

    /// <summary>
    /// Preserved runtime instance — задаётся фабриками при дропе предмета из инвентаря,
    /// чтобы nested-контейнер (с содержимым) пережил round-trip в мир.
    /// Null для предметов, размещённых в дизайн-тайме.
    /// </summary>
    [System.NonSerialized] public ItemInstance preservedInstance;

    [Header("Pickup")]
    [Tooltip("Клавиша подбора. Подбор сработает только если курсор наведён на предмет (HoverPickup на камере).")]
    public KeyCode pickupKey = KeyCode.F;

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

    // ── Unity ────────────────────────────────────────────────────────────────

    protected virtual void Start()
    {
        EnsureCollider();
        BuildPromptUI();
        SetPromptVisible(false);
    }

    protected virtual void LateUpdate()
    {
        if (promptUI != null && promptUI.activeSelf) OrientPrompt();
    }

    /// <summary>Каждый наследник гарантирует, что у него есть нужный коллайдер и не-триггер.</summary>
    protected abstract void EnsureCollider();

    /// <summary>3D: билборд к камере. 2D: фиксированная плоскость / no-op.</summary>
    protected abstract void OrientPrompt();

    // ── Public API ───────────────────────────────────────────────────────────

    public void SetPromptVisible(bool visible)
    {
        if (promptUI != null) promptUI.SetActive(visible);
    }

    /// <summary>Реализация <see cref="IWorldInteractable"/>: для предмета взаимодействие = подбор.</summary>
    public void Interact() => TryPickup();

    /// <summary>Подобрать предмет в инвентарь. Уничтожает GameObject при успехе.</summary>
    public void TryPickup()
    {
        var inv = Inventory.Service;
        if (inv == null || definition == null) return;

        var item = preservedInstance ?? new ItemInstance(definition, stackCount);
        if (item.nestedContainer != null) inv.RegisterContainerRecursive(item.nestedContainer);

        // 0) Долить в существующие стаки того же типа (если предмет стакаемый)
        if (item.IsStackable)
        {
            foreach (var container in inv.GetPickupContainers())
            {
                if (container.TryStackInto(item) && item.stackCount <= 0)
                {
                    inv.NotifyChanged();
                    Debug.Log($"[Pickup] {definition.displayName} → стакнут в {container.containerId}");
                    Destroy(gameObject);
                    return;
                }
            }
        }

        // 1) Экипировка
        if (inv.TryEquipAnyMatchingSlot(item))
        {
            inv.NotifyChanged();
            Debug.Log($"[Pickup] {definition.displayName} → equipment slot ({definition.itemType})");
            Destroy(gameObject);
            return;
        }

        // 2) Контейнеры в приоритете
        foreach (var container in inv.GetPickupContainers())
        {
            if (TryAddToContainer(item, container))
            {
                inv.NotifyChanged();
                Debug.Log($"[Pickup] {definition.displayName} → {container.containerId}");
                Destroy(gameObject);
                return;
            }
        }

        Debug.Log($"[Pickup] Инвентарь полон — нельзя подобрать {definition.displayName}");
        ShowFullMessage();
    }

    // ── Internals ────────────────────────────────────────────────────────────

    private static bool TryAddToContainer(ItemInstance item, GridContainer container)
    {
        for (int y = 0; y < container.height; y++)
            for (int x = 0; x < container.width; x++)
                if (container.TryPlace(item, new Vector2Int(x, y)))
                    return true;
        return false;
    }

    protected virtual Vector3 PromptLocalOffset => Vector3.up * 0.6f;
    protected virtual float   PromptLocalScale  => 0.01f;

    private void BuildPromptUI()
    {
        var canvasGo = new GameObject("PromptCanvas", typeof(Canvas), typeof(CanvasScaler));
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = PromptLocalOffset;
        canvasGo.transform.localScale    = Vector3.one * PromptLocalScale;

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200f, 60f);

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
        if (promptText == null || definition == null) return;
        string name  = definition.displayName;
        string stack = stackCount > 1 ? $" ×{stackCount}" : "";
        string size  = $"{definition.width}×{definition.height}";
        promptText.text = $"[{pickupKey}] {name}{stack}\n{size}  {definition.weightPerUnit * stackCount:F2} кг";
    }

    protected void ShowFullMessage()
    {
        if (promptText == null) return;
        promptText.text  = "Инвентарь полон!";
        promptText.color = new Color(1f, 0.4f, 0.4f);
        Invoke(nameof(RestorePrompt), 1.5f);
    }

    private void RestorePrompt()
    {
        if (promptText == null) return;
        promptText.color = Color.white;
        UpdatePromptText();
    }
}
