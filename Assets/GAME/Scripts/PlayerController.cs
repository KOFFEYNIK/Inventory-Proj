using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("Speeds")]
    public float walkSpeed = 3f;
    public float runSpeed = 5f;
    public float sprintMultiplier = 1.75f;
    public float crouchSpeedMultiplier = 0.5f;
    public float jumpForce = 5f;

    [Header("Camera")]
    public Transform cameraTransform;
    public bool faceMovement = true;
    public float rotationSpeed = 10f;

    [Header("Keys (rebindable)")]
    public KeyCode jumpKey = KeyCode.Space;
    public KeyCode sprintKey = KeyCode.LeftShift;
    public KeyCode crouchKey = KeyCode.C;
    public KeyCode toggleRunKey = KeyCode.CapsLock;

    private Rigidbody rb;
    private CapsuleCollider capsule;

    private Vector3 moveDirection;
    private bool isGrounded;
    public LayerMask groundMask = ~0;
    public float groundCheckDistance = 0.2f;

    bool isCrouching = false;
    bool runToggled = false;
    bool jumpPending = false;

    float standingHeight;
    Vector3 standingCenter;
    public float crouchHeight = 1f;
    public Vector3 crouchCenterOffset = new Vector3(0f, -0.5f, 0f);

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            standingHeight = capsule.height;
            standingCenter = capsule.center;
        }
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    private bool InventoryOpen => InventoryUI.Instance != null && InventoryUI.Instance.IsOpen;
    private bool IsDead        => HealthSystem.Instance != null && HealthSystem.Instance.IsDead;

    private void Update()
    {
        if (IsDead || InventoryOpen) return;
        HandleInput();
        HandleJump();
        HandleToggleRun();
        HandleCrouchInput();
    }

    private void FixedUpdate()
    {
        if (IsDead || InventoryOpen)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }
        Move();
    }

    private void HandleInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
        float halfH = capsule != null ? capsule.height * 0.5f : 0.5f;
        float centerY = capsule != null ? capsule.center.y : 0f;
        Vector3 groundOrigin = transform.position + Vector3.up * (centerY - halfH + 0.15f);
        isGrounded = Physics.Raycast(groundOrigin, Vector3.down, groundCheckDistance + 0.15f, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void Move()
    {
        float speed = runToggled ? runSpeed : walkSpeed;
        if (Input.GetKey(sprintKey)) speed *= sprintMultiplier;
        if (isCrouching) speed *= crouchSpeedMultiplier;

        Vector3 desiredMove;
        if (cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = cameraTransform.right;
            camRight.y = 0f;
            camRight.Normalize();
            desiredMove = camRight * moveDirection.x + camForward * moveDirection.z;
        }
        else
        {
            desiredMove = transform.TransformDirection(moveDirection);
        }

        Vector3 move = (desiredMove.sqrMagnitude > 0.0001f) ? desiredMove.normalized * speed : Vector3.zero;
        float yVel = rb.linearVelocity.y;
        if (jumpPending)
        {
            yVel = jumpForce;
            jumpPending = false;
        }
        rb.linearVelocity = new Vector3(move.x, yVel, move.z);

        if (faceMovement && desiredMove.sqrMagnitude > 0.001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(desiredMove);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void HandleJump()
    {
        if (Input.GetKeyDown(jumpKey) && isGrounded && !isCrouching)
            jumpPending = true;
    }

    private void HandleToggleRun()
    {
        if (Input.GetKeyDown(toggleRunKey)) runToggled = !runToggled;
    }

    private void HandleCrouchInput()
    {
        if (Input.GetKeyDown(crouchKey))
        {
            isCrouching = !isCrouching;
            ApplyCrouch(isCrouching);
        }
    }

    void ApplyCrouch(bool crouch)
    {
        if (capsule == null) return;
        if (crouch)
        {
            capsule.height = Mathf.Max(0.1f, crouchHeight);
            capsule.center = standingCenter + crouchCenterOffset;
        }
        else
        {
            capsule.height = standingHeight;
            capsule.center = standingCenter;
        }
    }

    private void OnValidate()
    {
        if (walkSpeed < 0f) walkSpeed = 0f;
        if (runSpeed < 0f) runSpeed = 0f;
        if (sprintMultiplier < 1f) sprintMultiplier = 1f;
        if (crouchSpeedMultiplier <= 0f) crouchSpeedMultiplier = 0.1f;
        if (jumpForce < 0f) jumpForce = 0f;
    }
}
