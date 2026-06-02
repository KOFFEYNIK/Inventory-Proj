using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Игровая прокладка между <see cref="HoverPickup3D"/>/<see cref="HoverPickup2D"/> и
/// <see cref="WorldContextMenu"/>. Вешается на ту же камеру, что и HoverPickup; подписывается
/// на ПКМ-событие и открывает меню с настраиваемыми пунктами «Осмотр» / «Подобрать».
///
/// Все подписи и видимости пунктов редактируются в инспекторе. Также можно добавить
/// произвольные пункты через <see cref="AddExtraEntry"/> из кода проекта.
///
/// Если меню отключено (<see cref="useContextMenu"/> = false), компонент ничего не делает —
/// поведение HoverPickup остаётся прежним (клавиша подбора).
/// </summary>
[DefaultExecutionOrder(-40)]
public class WorldPickupContextMenu : MonoBehaviour
{
    [Header("General")]
    [Tooltip("Если false — меню вообще не открывается, ПКМ-событие игнорируется.")]
    public bool useContextMenu = true;

    [Header("Built-in entries (labels editable)")]
    [Tooltip("Показывать пункт «Осмотр».")]
    public bool   showInspect = true;
    [Tooltip("Подпись пункта «Осмотр». Пустая = пункт скрыт.")]
    public string labelInspect = "Осмотр";

    [Tooltip("Показывать пункт «Подобрать».")]
    public bool   showPickup = true;
    [Tooltip("Подпись пункта «Подобрать». Пустая = пункт скрыт.")]
    public string labelPickup  = "Подобрать";

    [Tooltip("Показывать пункт «Открыть» для мировых ящиков/сундуков.")]
    public bool   showOpen = true;
    [Tooltip("Подпись пункта «Открыть». Пустая = пункт скрыт.")]
    public string labelOpen = "Открыть";

    /// <summary>Произвольные пункты, которые проект может добавить из кода.</summary>
    private class ExtraEntry
    {
        public string label;
        public Action<WorldItemBase> handler;
    }
    private readonly List<ExtraEntry> extras = new();

    private HoverPickup3D hover3D;
    private HoverPickup2D hover2D;

    void Awake()
    {
        hover3D = GetComponent<HoverPickup3D>();
        hover2D = GetComponent<HoverPickup2D>();
    }

    void OnEnable()
    {
        if (hover3D != null) hover3D.OnRightClickedItem += HandleRMB3D;
        if (hover2D != null) hover2D.OnRightClickedItem += HandleRMB2D;
        if (hover3D != null) hover3D.OnRightClickedContainer += OpenContainerMenu;
        if (hover2D != null) hover2D.OnRightClickedContainer += OpenContainerMenu;
    }

    void OnDisable()
    {
        if (hover3D != null) hover3D.OnRightClickedItem -= HandleRMB3D;
        if (hover2D != null) hover2D.OnRightClickedItem -= HandleRMB2D;
        if (hover3D != null) hover3D.OnRightClickedContainer -= OpenContainerMenu;
        if (hover2D != null) hover2D.OnRightClickedContainer -= OpenContainerMenu;
        // На всякий случай снять «заморозку», если меню закрылось вместе со скриптом.
        if (hover3D != null) hover3D.SuppressHoverUpdates = false;
        if (hover2D != null) hover2D.SuppressHoverUpdates = false;
    }

    void Update()
    {
        // Пока меню открыто — замораживаем смену цели у HoverPickup, чтобы не «отвалилась»
        // подсветка/цель пока пользователь выбирает пункт меню.
        bool menuOpen = useContextMenu && WorldContextMenu.Instance != null && WorldContextMenu.Instance.IsOpen;
        if (hover3D != null) hover3D.SuppressHoverUpdates = menuOpen;
        if (hover2D != null) hover2D.SuppressHoverUpdates = menuOpen;
    }

    /// <summary>Добавить пункт меню из проектного кода. Колбэк получает текущий WorldItemBase.</summary>
    public void AddExtraEntry(string label, Action<WorldItemBase> handler)
    {
        if (string.IsNullOrEmpty(label) || handler == null) return;
        extras.Add(new ExtraEntry { label = label, handler = handler });
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private void HandleRMB3D(WorldItem3D item) => Open(item);
    private void HandleRMB2D(WorldItem2D item) => Open(item);

    /// <summary>ПКМ по мировому ящику/сундуку — меню с единственным пунктом «Открыть».</summary>
    private void OpenContainerMenu(WorldContainerBase container)
    {
        if (!useContextMenu || container == null) return;
        if (!showOpen || string.IsNullOrEmpty(labelOpen)) return;

        WorldContextMenu.Ensure().Show(Input.mousePosition, new (string, Action)[]
        {
            (labelOpen, () => { if (container != null) container.Open(); }),
        });
    }

    private void Open(WorldItemBase item)
    {
        if (!useContextMenu || item == null || item.definition == null) return;

        var entries = new List<(string, Action)>(2 + extras.Count);

        if (showInspect && !string.IsNullOrEmpty(labelInspect))
            entries.Add((labelInspect, () => InspectWorldItem(item)));

        if (showPickup && !string.IsNullOrEmpty(labelPickup))
            entries.Add((labelPickup, () => { if (item != null) item.TryPickup(); }));

        foreach (var ex in extras)
        {
            if (ex == null || string.IsNullOrEmpty(ex.label) || ex.handler == null) continue;
            var captured = ex;
            entries.Add((ex.label, () => captured.handler?.Invoke(item)));
        }

        if (entries.Count == 0) return;

        WorldContextMenu.Ensure().Show(Input.mousePosition, entries.ToArray());
    }

    private static void InspectWorldItem(WorldItemBase item)
    {
        if (item == null || item.definition == null) return;
        var inst = item.preservedInstance ?? new ItemInstance(item.definition, item.stackCount);

        var inspector = InspectWindowUI.Instance;
        if (inspector == null)
        {
            // Подцепляемся к любому существующему Canvas. Если нет — создаём минимальный
            // overlay-canvas под окно осмотра.
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            GameObject host;
            if (canvas != null) host = canvas.gameObject;
            else
            {
                host = new GameObject("InspectWindowCanvas",
                    typeof(Canvas),
                    typeof(UnityEngine.UI.CanvasScaler),
                    typeof(UnityEngine.UI.GraphicRaycaster));
                var c = host.GetComponent<Canvas>();
                c.renderMode   = RenderMode.ScreenSpaceOverlay;
                c.sortingOrder = 90;
                canvas = c;
            }
            inspector = host.AddComponent<InspectWindowUI>();
            inspector.RootCanvas = canvas;
        }
        inspector.Show(inst);
    }
}
