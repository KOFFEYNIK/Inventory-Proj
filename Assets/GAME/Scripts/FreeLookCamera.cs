using UnityEngine;

public class FreeLookCamera : MonoBehaviour
{
    public KeyCode activationKey = KeyCode.Mouse2;
    public float sensitivity = 3f;
    public bool invertY = false;
    public bool clampVertical = true;
    public float minPitch = -80f;
    public float maxPitch = 80f;
    public Transform target;
    public float distance = 10f;
    public bool orbitMode = false;
    public float zoomSpeed = 2f;
    public float minDistance = 1f;
    public float maxDistance = 50f;

    float yaw;
    float pitch;

    public float Yaw => yaw;
    public float Pitch => pitch;

    public void AddRecoilImpulse(float deltaPitch, float deltaYaw = 0f)
    {
        pitch += deltaPitch;
        yaw += deltaYaw;
        if (clampVertical) pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void Start()
    {
        Vector3 e = transform.eulerAngles;
        yaw = e.y;
        pitch = e.x;
        if (target != null && orbitMode)
        {
            distance = Vector3.Distance(transform.position, target.position);
            Quaternion look = Quaternion.LookRotation(target.position - transform.position);
            Vector3 angles = look.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;
            UpdatePosition();
            transform.LookAt(target.position);
        }
    }

    private bool InventoryOpen => InventoryUI.Instance != null && InventoryUI.Instance.IsOpen;

    void Update()
    {
        if (!InventoryOpen && Input.GetKey(activationKey))
        {
            float mx = Input.GetAxis("Mouse X");
            float my = Input.GetAxis("Mouse Y");
            yaw += mx * sensitivity;
            pitch += (invertY ? my : -my) * sensitivity;
            if (clampVertical) pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            if (target == null || !orbitMode)
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        if (target != null && orbitMode)
        {
            if (!InventoryOpen)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.0001f)
                    distance = Mathf.Clamp(distance - scroll * zoomSpeed * 10f, minDistance, maxDistance);
            }

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = target.position + rot * new Vector3(0f, 0f, -distance);
            transform.LookAt(target.position);
        }
    }

    void UpdatePosition()
    {
        transform.position = target.position - transform.forward * distance;
    }

    void OnValidate()
    {
        if (sensitivity < 0f) sensitivity = 0f;
        if (distance < 0f) distance = 0f;
        if (minPitch > maxPitch) minPitch = maxPitch - 1f;
    }
}
