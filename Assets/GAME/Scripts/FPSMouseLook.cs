using UnityEngine;

/// <summary>
/// Простой FPS-mouse-look для sample-сцены. Вешается на камеру — она child от
/// тела игрока. Этот компонент крутит камеру по pitch (X), тело — по yaw (Y).
///
/// Курсор автоматически unlock'нется, когда открыт <see cref="InventoryUI"/>,
/// чтобы можно было кликать по UI инвентаря.
/// </summary>
public class FPSMouseLook : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Transform тела игрока (вращается по yaw). Если null — вращаем сам объект полностью.")]
    public Transform body;

    [Header("Sensitivity")]
    public float sensitivity = 2f;

    [Header("Pitch clamp")]
    public float minPitch = -85f;
    public float maxPitch =  85f;

    [Header("Cursor")]
    [Tooltip("Управлять курсором из этого компонента (lock на старте, unlock при открытом инвентаре).")]
    public bool manageCursor = true;

    [Tooltip("Залочить курсор в центре экрана при старте. Игнорируется если manageCursor=false.")]
    public bool lockCursorAtStart = true;

    private float pitch;

    void Start()
    {
        if (manageCursor && lockCursorAtStart) LockCursor(true);
        pitch = transform.localEulerAngles.x;
        if (pitch > 180f) pitch -= 360f;
    }

    void Update()
    {
        bool invOpen = InventoryUI.Instance != null && InventoryUI.Instance.IsOpen;
        if (manageCursor) LockCursor(!invOpen);
        if (invOpen) return;

        float mx = Input.GetAxis("Mouse X") * sensitivity;
        float my = Input.GetAxis("Mouse Y") * sensitivity;

        pitch -= my;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        if (body != null) body.Rotate(Vector3.up * mx);
        else              transform.Rotate(Vector3.up * mx, Space.World);
    }

    private static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible   = !locked;
    }
}
