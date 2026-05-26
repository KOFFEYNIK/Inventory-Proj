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
/// Удобен для top-down 2D, side-scroller / платформера, и pointer-driven UI.
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

    private Camera      cam;
    private WorldItem2D currentItem;

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

        var target = FindTarget();
        SetCurrent(target);

        if (currentItem == null) return;

        if (pickupOnHover || Input.GetKeyDown(pickupKey))
            currentItem.TryPickup();
    }

    private WorldItem2D FindTarget()
    {
        switch (mode)
        {
            case HoverMode.Mouse:
                return FindMouseTarget();
            case HoverMode.PlayerProximity:
                return FindProximityTarget();
            default:
                return null;
        }
    }

    private WorldItem2D FindMouseTarget()
    {
        Vector3 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
        var collider = Physics2D.OverlapPoint(worldPos, hoverMask);
        return collider != null ? collider.GetComponentInParent<WorldItem2D>() : null;
    }

    private WorldItem2D FindProximityTarget()
    {
        if (player == null) return null;
        var hit = Physics2D.OverlapCircle(player.position, proximityRadius, hoverMask);
        return hit != null ? hit.GetComponentInParent<WorldItem2D>() : null;
    }

    private void SetCurrent(WorldItem2D target)
    {
        if (target == currentItem) return;
        if (currentItem != null) currentItem.SetPromptVisible(false);
        currentItem = target;
        if (currentItem != null) currentItem.SetPromptVisible(true);
    }
}
