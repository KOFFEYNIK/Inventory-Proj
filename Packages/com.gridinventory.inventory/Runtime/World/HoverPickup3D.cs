using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Scripting.APIUpdating;

/// <summary>
/// Модуль на камеру (3D): подбор предметов только при наведении курсора.
///
/// Каждый кадр стреляет лучом из камеры через позицию мыши. Если попадает в коллайдер с
/// <see cref="WorldItem3D"/> в иерархии — показывает его world-space подсказку и слушает
/// клавишу <see cref="pickupKey"/>. При смене цели — старая подсказка скрывается.
///
/// Курсор должен быть видимым (не залочен Cursor.lockState = Locked).
///
/// Раньше класс назывался <c>HoverPickup</c> в Assembly-CSharp.
/// </summary>
[RequireComponent(typeof(Camera))]
[MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "HoverPickup")]
public class HoverPickup3D : MonoBehaviour
{
    [Header("Pickup")]
    [Tooltip("Клавиша подбора. Срабатывает только пока курсор наведён на предмет.")]
    public KeyCode pickupKey = KeyCode.F;

    [Tooltip("Подбирать просто по наведению, без нажатия клавиши. По умолчанию выключено.")]
    public bool pickupOnHover = false;

    [Header("Raycast")]
    [Tooltip("Максимальная дистанция от камеры до предмета.")]
    public float maxDistance = 10f;

    [Tooltip("Слои, по которым стрелять. По умолчанию — все.")]
    public LayerMask hoverMask = ~0;

    [Tooltip("Игнорировать, когда курсор над UI (открытый инвентарь или меню).")]
    public bool ignoreOverUI = true;

    private Camera      cam;
    private WorldItem3D currentItem;

    void Awake() => cam = GetComponent<Camera>();

    void OnDisable() => SetCurrent(null);

    void Update()
    {
        if (cam == null) return;

        if (ignoreOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            SetCurrent(null);
            return;
        }

        var target = RaycastWorldItem();
        SetCurrent(target);

        if (currentItem == null) return;

        if (pickupOnHover || Input.GetKeyDown(pickupKey))
            currentItem.TryPickup();
    }

    private WorldItem3D RaycastWorldItem()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, maxDistance, hoverMask, QueryTriggerInteraction.Ignore))
            return null;
        return hit.collider.GetComponentInParent<WorldItem3D>();
    }

    private void SetCurrent(WorldItem3D target)
    {
        if (target == currentItem) return;
        if (currentItem != null) currentItem.SetPromptVisible(false);
        currentItem = target;
        if (currentItem != null) currentItem.SetPromptVisible(true);
    }
}
