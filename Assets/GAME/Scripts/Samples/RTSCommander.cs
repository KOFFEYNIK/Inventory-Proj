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

    public RTSUnit Selected { get; private set; }

    void Start()
    {
        if (cam == null) cam = Camera.main;
    }

    void Update()
    {
        if (cam == null) return;
        if (InventoryUI.Instance != null && InventoryUI.Instance.IsOpen) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

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
        var ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, 1000f, groundMask, QueryTriggerInteraction.Ignore))
            Selected.MoveTo(hit.point);
    }

    private void Select(RTSUnit unit)
    {
        if (Selected != null) Selected.SetSelected(false);
        Selected = unit;
        if (Selected != null) Selected.SetSelected(true);
    }
}
