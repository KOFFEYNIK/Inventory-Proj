using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CustomCursorDrag : MonoBehaviour
{
    [Header("Drag")]
    public KeyCode dragKey = KeyCode.Mouse0;
    public LayerMask interactableMask = ~0;
    public float dragSpeed = 10f;
    public bool useFixedDistanceFromPlayer = true;
    public float dragDistanceFromPlayer = 5f;
    public float pickupDistanceFromPlayer = 3f;
    public float maxDragDistanceFromPlayer = 5f;
    public Transform playerTransform;

    private Camera mainCamera;
    private Rigidbody grabbedRb;
    private bool grabbedUseGravity;
    private Plane dragPlane;
    private Vector3 grabOffset;

    private void Awake()
    {
        mainCamera = GetComponent<Camera>();
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                playerTransform = player.transform;
        }
    }

    private void OnDisable()
    {
        StopDragging();
    }

    private void Update()
    {
        if (grabbedRb == null)
        {
            if (Input.GetKeyDown(dragKey))
                TryBeginDrag();
        }
        else if (Input.GetKeyUp(dragKey))
        {
            StopDragging();
        }
    }

    private void FixedUpdate()
    {
        if (grabbedRb != null)
            UpdateDragging();
    }

    private void TryBeginDrag()
    {
        if (mainCamera == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, interactableMask))
        {
            if (hit.rigidbody != null && !hit.rigidbody.isKinematic)
            {
                if (playerTransform != null)
                {
                    float distanceToPlayer = Vector3.Distance(hit.rigidbody.position, playerTransform.position);
                    if (distanceToPlayer > pickupDistanceFromPlayer)
                        return;
                }

                grabbedRb = hit.rigidbody;
                grabbedUseGravity = grabbedRb.useGravity;
                grabbedRb.useGravity = false;
            }
            else
            {
                return;
            }

            Vector3 hitPoint = hit.point;
            dragPlane = new Plane(-mainCamera.transform.forward, hitPoint);
            grabOffset = grabbedRb.position - hitPoint;
        }
    }

    private void UpdateDragging()
    {
        if (mainCamera == null)
            return;

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        Vector3 targetPoint;

        if (useFixedDistanceFromPlayer && playerTransform != null)
        {
            if (dragDistanceFromPlayer < 0f) dragDistanceFromPlayer = 0f;

            Plane playerPlane = new Plane(-mainCamera.transform.forward, playerTransform.position);
            if (!playerPlane.Raycast(ray, out float enter))
                return;

            Vector3 planePoint = ray.GetPoint(enter);
            Vector3 direction = planePoint - playerTransform.position;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = grabbedRb.position - playerTransform.position;
            }
            direction.Normalize();
            targetPoint = playerTransform.position + direction * dragDistanceFromPlayer;
        }
        else
        {
            if (!dragPlane.Raycast(ray, out float enter))
                return;

            targetPoint = ray.GetPoint(enter) + grabOffset;
        }

        if (playerTransform != null)
        {
            if (maxDragDistanceFromPlayer < 0f) maxDragDistanceFromPlayer = 0f;
            Vector3 fromPlayer = targetPoint - playerTransform.position;
            if (fromPlayer.magnitude > maxDragDistanceFromPlayer)
            {
                targetPoint = playerTransform.position + fromPlayer.normalized * maxDragDistanceFromPlayer;
            }
        }

        if (grabbedRb != null)
        {
            grabbedRb.MovePosition(Vector3.Lerp(grabbedRb.position, targetPoint, Time.fixedDeltaTime * dragSpeed));
        }
    }

    private void StopDragging()
    {
        if (grabbedRb != null)
        {
            grabbedRb.useGravity = grabbedUseGravity;
            grabbedRb = null;
        }
    }
}
