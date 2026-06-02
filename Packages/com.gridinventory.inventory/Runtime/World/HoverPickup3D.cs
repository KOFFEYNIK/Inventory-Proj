using System;
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
/// Сторонние системы (например, контекстное меню в проекте) могут читать <see cref="CurrentItem"/>
/// и слушать <see cref="OnRightClickedItem"/>, чтобы реагировать на ПКМ по предмету без правок этого скрипта.
/// Чтобы заморозить смену цели (когда меню открыто) — выставь <see cref="SuppressHoverUpdates"/>.
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
    public float maxDistance = 100f;

    [Tooltip("Слои, по которым стрелять. По умолчанию — все.")]
    public LayerMask hoverMask = ~0;

    [Tooltip("Игнорировать, когда курсор над UI (открытый инвентарь или меню).")]
    public bool ignoreOverUI = true;

    /// <summary>Срабатывает, когда пользователь нажал ПКМ при наведённом ПРЕДМЕТЕ. Внешние
    /// системы (контекстное меню в проекте) могут подписаться и показать своё меню.</summary>
    public event Action<WorldItem3D> OnRightClickedItem;

    /// <summary>Срабатывает, когда пользователь нажал ПКМ при наведённом КОНТЕЙНЕРЕ (ящик/сундук).
    /// Проектная прокладка показывает меню «Открыть».</summary>
    public event Action<WorldContainerBase> OnRightClickedContainer;

    /// <summary>Если true — каждый кадр пропускаем обновление цели. Использовать когда
    /// открыто контекстное меню, чтобы цель «замораживалась» под меню.</summary>
    [NonSerialized] public bool SuppressHoverUpdates;

    private Camera             cam;
    private IWorldInteractable current;          // наведённая цель (предмет ИЛИ контейнер)
    private WorldItem3D        currentItem;       // != null только когда цель — пикап
    private WorldContainerBase currentContainer;  // != null только когда цель — ящик

    /// <summary>Текущий наведённый ПРЕДМЕТ под курсором. null, если наведён ящик или ничего.</summary>
    public WorldItem3D CurrentItem => currentItem;

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

        if (!SuppressHoverUpdates)
            SetCurrent(RaycastInteractable());

        if (current == null) return;

        if (Input.GetMouseButtonDown(1))
        {
            // ПКМ сам по себе не взаимодействует — только открывает контекстное меню проекта.
            if (currentItem != null)           OnRightClickedItem?.Invoke(currentItem);
            else if (currentContainer != null) OnRightClickedContainer?.Invoke(currentContainer);
            return;
        }

        if (Input.GetKeyDown(pickupKey))
            current.Interact();
        else if (pickupOnHover && currentItem != null)
            // Авто-подбор по наведению — только для предметов, ящик так не открываем (был бы спам).
            currentItem.TryPickup();
    }

    private IWorldInteractable RaycastInteractable()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, maxDistance, hoverMask, QueryTriggerInteraction.Ignore))
            return null;

        var item = hit.collider.GetComponentInParent<WorldItem3D>();
        if (item != null) return item;
        return hit.collider.GetComponentInParent<WorldContainerBase>();
    }

    private void SetCurrent(IWorldInteractable target)
    {
        if (ReferenceEquals(target, current)) return;
        current?.SetPromptVisible(false);
        current          = target;
        currentItem      = target as WorldItem3D;
        currentContainer = target as WorldContainerBase;
        current?.SetPromptVisible(true);
    }
}
