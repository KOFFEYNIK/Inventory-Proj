using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// RTS-стиль управления: ЛКМ выделяет <see cref="RTSUnit"/> под курсором,
/// ПКМ отправляет выделенного юнита в точку клика по земле.
///
/// Вешается на любой GameObject в сцене (например, на тот же объект, что Camera или Player).
/// Поле <see cref="cam"/> — камера, через которую делается raycast (по умолчанию Camera.main).
/// </summary>
public class RTSCommander : MonoBehaviour
{
    public Camera cam;

    [Tooltip("Слои, по которым может ходить юнит (земля). По умолчанию все.")]
    public LayerMask groundMask = ~0;

    [Header("Context Menu")]
    [Tooltip("Если true — ПКМ открывает контекстное меню рядом с курсором (как в инвентаре). " +
             "Если false — старое поведение: ПКМ сразу даёт юниту команду «идти сюда».")]
    public bool useContextMenu = true;

    [Tooltip("Юнит, который будет выделен автоматически при старте сцены. Если null — " +
             "никто не выделен, пока пользователь не кликнет ЛКМ.")]
    public RTSUnit autoSelectUnit;

    public RTSUnit Selected { get; private set; }

    void Start()
    {
        if (cam == null) cam = Camera.main;
        if (autoSelectUnit != null) Select(autoSelectUnit);
    }

    void Update()
    {
        if (cam == null) return;
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        // Когда меню уже открыто — оно само обработает ПКМ/Escape для закрытия.
        if (useContextMenu && WorldContextMenu.Instance != null && WorldContextMenu.Instance.IsOpen)
            return;

        if (Input.GetMouseButtonDown(0)) HandleLMB();
        if (Input.GetMouseButtonDown(1)) HandleRMB();
    }

    private void HandleLMB()
    {
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
        {
            var unit = hit.collider.GetComponentInParent<RTSUnit>();
            Select(unit);
        }
        else
        {
            Select(null);
        }
    }

    private void HandleRMB()
    {
        if (Selected == null) return;

        // Если игрок навёлся на мировой предмет — RMB обрабатывает HoverPickup
        // (он откроет своё контекстное меню «Осмотр / Подобрать»). Move-команду не даём.
        var hover3D = cam.GetComponent<HoverPickup3D>();
        if (hover3D != null && hover3D.CurrentItem != null) return;
        var hover2D = cam.GetComponent<HoverPickup2D>();
        if (hover2D != null && hover2D.CurrentItem != null) return;

        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out var hit, 1000f, groundMask, QueryTriggerInteraction.Ignore))
            return;

        // Если raycast попал в коллайдер предмета (например, groundMask = Everything),
        // тоже игнорируем — пусть меню пикапа открывается, а не Move-команда.
        if (hit.collider != null && hit.collider.GetComponentInParent<WorldItemBase>() != null)
            return;

        if (useContextMenu)
        {
            var dest = hit.point;
            var unit = Selected;
            WorldContextMenu.Ensure().Show(Input.mousePosition,
                ("Идти сюда",       () => { if (unit != null) unit.MoveTo(dest); }),
                ("Стоять",          () => { if (unit != null) unit.MoveTo(unit.transform.position); }),
                ("Снять выделение", () => Select(null))
            );
        }
        else
        {
            Selected.MoveTo(hit.point);
        }
    }

    private void Select(RTSUnit unit)
    {
        if (Selected != null) Selected.SetSelected(false);
        Selected = unit;
        if (Selected != null) Selected.SetSelected(true);

        // Переключаем «активный инвентарь» под выбранного юнита, если у него есть RTSUnitInventory.
        // Без него — оставляем текущий активный (например, общий InventoryRig).
        // Это даёт поведение тактических RPG (Baldur's Gate): подбор и UI работают с инвентарём выбранного.
        if (Selected != null)
        {
            var unitInv = Selected.GetComponent<RTSUnitInventory>();
            if (unitInv != null) unitInv.Activate();
        }
    }
}
