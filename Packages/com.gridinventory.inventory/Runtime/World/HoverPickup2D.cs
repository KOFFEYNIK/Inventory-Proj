using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Модуль на камеру (2D): подбор предметов через 2D-физику.
///
/// Два режима:
///   • <see cref="HoverMode.Mouse"/>: каждый кадр <see cref="Physics2D.OverlapPoint"/>
///     под позицией мыши (через <see cref="Camera.ScreenToWorldPoint"/>). Если попал в
///     <see cref="WorldItem2D"/> — показывает подсказку и слушает pickupKey.
///   • <see cref="HoverMode.PlayerProximity"/>: каждый кадр <see cref="Physics2D.OverlapCircle"/>
///     вокруг <see cref="player"/> с радиусом <see cref="proximityRadius"/>. Подбор по
///     pickupKey или автоматически, если <see cref="pickupOnHover"/>.
///
/// Сторонние системы могут читать <see cref="CurrentItem"/> и слушать <see cref="OnRightClickedItem"/>.
/// Чтобы заморозить смену цели (когда меню открыто) — выставь <see cref="SuppressHoverUpdates"/>.
/// </summary>
[RequireComponent(typeof(Camera))]
public class HoverPickup2D : MonoBehaviour
{
    public enum HoverMode
    {
        Mouse,
        PlayerProximity,
    }

    [Header("Pickup")]
    [Tooltip("Клавиша подбора. Срабатывает только пока цель активна.")]
    public KeyCode pickupKey = KeyCode.F;

    [Tooltip("Подбирать просто при попадании в зону, без нажатия клавиши.")]
    public bool pickupOnHover = false;

    [Header("Mode")]
    public HoverMode mode = HoverMode.Mouse;

    [Tooltip("Для PlayerProximity: ссылка на игрока. Если null — подбор не работает.")]
    public Transform player;

    [Tooltip("Для PlayerProximity: радиус поиска предметов вокруг игрока (world units).")]
    public float proximityRadius = 1.5f;

    [Header("Raycast / Overlap")]
    [Tooltip("Слои, по которым искать предметы.")]
    public LayerMask hoverMask = ~0;

    [Tooltip("Игнорировать, когда курсор над UI.")]
    public bool ignoreOverUI = true;

    /// <summary>ПКМ при наведённом ПРЕДМЕТЕ — внешние системы могут подписаться и показать меню.</summary>
    public event Action<WorldItem2D> OnRightClickedItem;

    /// <summary>ПКМ при наведённом КОНТЕЙНЕРЕ (ящик/сундук) — проектная прокладка показывает «Открыть».</summary>
    public event Action<WorldContainerBase> OnRightClickedContainer;

    /// <summary>Если true — пропускаем обновление цели (для замораживания под открытым меню).</summary>
    [NonSerialized] public bool SuppressHoverUpdates;

    private Camera             cam;
    private IWorldInteractable current;          // наведённая цель (предмет ИЛИ контейнер)
    private WorldItem2D        currentItem;       // != null только когда цель — пикап
    private WorldContainerBase currentContainer;  // != null только когда цель — ящик

    public WorldItem2D CurrentItem => currentItem;

    void Awake() => cam = GetComponent<Camera>();

    void OnDisable() => SetCurrent(null);

    void Update()
    {
        if (cam == null) return;

        if (ignoreOverUI && mode == HoverMode.Mouse &&
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            SetCurrent(null);
            return;
        }

        if (!SuppressHoverUpdates)
            SetCurrent(FindTarget());

        if (current == null) return;

        if (Input.GetMouseButtonDown(1))
        {
            if (currentItem != null)           OnRightClickedItem?.Invoke(currentItem);
            else if (currentContainer != null) OnRightClickedContainer?.Invoke(currentContainer);
            return;
        }

        if (Input.GetKeyDown(pickupKey))
            current.Interact();
        else if (pickupOnHover && currentItem != null)
            // Авто-подбор по наведению — только для предметов, ящик так не открываем.
            currentItem.TryPickup();
    }

    private IWorldInteractable FindTarget()
    {
        Collider2D hit = mode switch
        {
            HoverMode.Mouse           => Physics2D.OverlapPoint(cam.ScreenToWorldPoint(Input.mousePosition), hoverMask),
            HoverMode.PlayerProximity => player != null
                                         ? Physics2D.OverlapCircle(player.position, proximityRadius, hoverMask)
                                         : null,
            _ => null,
        };
        if (hit == null) return null;

        var item = hit.GetComponentInParent<WorldItem2D>();
        if (item != null) return item;
        return hit.GetComponentInParent<WorldContainerBase>();
    }

    private void SetCurrent(IWorldInteractable target)
    {
        if (ReferenceEquals(target, current)) return;
        current?.SetPromptVisible(false);
        current          = target;
        currentItem      = target as WorldItem2D;
        currentContainer = target as WorldContainerBase;
        current?.SetPromptVisible(true);
    }
}
