using UnityEngine;

/// <summary>
/// 2D side-scroller платформер: A/D горизонталь, Space прыжок. Гравитация Rigidbody2D.
/// Ground-check через BoxCast от низа коллайдера. Когда инвентарь открыт — ввод заблокирован.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class Platformer2DController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 9f;

    [Tooltip("Слой(и), которые считаются землёй. По умолчанию все, кроме Player.")]
    public LayerMask groundMask = ~0;

    [Tooltip("Дополнительный отступ ниже коллайдера для проверки 'на земле'.")]
    public float groundCheckDistance = 0.05f;

    private Rigidbody2D   rb;
    private BoxCollider2D col;

    void Awake()
    {
        rb  = GetComponent<Rigidbody2D>();
        col = GetComponent<BoxCollider2D>();
        rb.freezeRotation = true;
    }

    void Update()
    {
        bool blocked = InventoryUI.Instance != null && InventoryUI.Instance.IsOpen;
        if (blocked)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // Горизонталь
        float dir = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
        rb.linearVelocity = new Vector2(dir * moveSpeed, rb.linearVelocity.y);

        // Прыжок
        if (Input.GetKeyDown(KeyCode.Space) && IsGrounded())
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    private bool IsGrounded()
    {
        if (col == null) return false;
        Bounds b = col.bounds;
        Vector2 origin = new Vector2(b.center.x, b.min.y);
        Vector2 size   = new Vector2(b.size.x * 0.95f, 0.02f);
        var hit = Physics2D.BoxCast(origin, size, 0f, Vector2.down, groundCheckDistance, groundMask);
        // Игнорируем попадание в самого себя
        return hit.collider != null && hit.collider != col;
    }
}
