using UnityEngine;

/// <summary>
/// 2D top-down контроллер для Diablo-like sample-сцены.
/// WASD двигают player'а в плоскости XY через Rigidbody2D (без гравитации).
/// Когда инвентарь открыт — движение отключено.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class TopDown2DController : MonoBehaviour
{
    public float moveSpeed = 5f;

    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale  = 0f;
        rb.freezeRotation = true;
        rb.linearDamping = 8f; // быстрое замедление, когда не двигаемся
    }

    void FixedUpdate()
    {
        bool blocked = InventoryUI.Instance != null && InventoryUI.Instance.IsOpen;
        if (blocked) { rb.linearVelocity = Vector2.zero; return; }

        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        Vector2 move = new Vector2(x, y);
        if (move.sqrMagnitude > 1f) move.Normalize();
        rb.linearVelocity = move * moveSpeed;
    }
}
