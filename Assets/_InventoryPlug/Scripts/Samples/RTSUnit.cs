using UnityEngine;

/// <summary>
/// Юнит RTS-стиля. Двигается к точке назначения (MoveTo) на постоянной скорости.
/// Выделение визуализируется подсветкой материала (через _BaseColor / _Color).
/// Управляется через <see cref="RTSCommander"/>.
/// </summary>
public class RTSUnit : MonoBehaviour
{
    public float moveSpeed = 4f;
    public float arriveDistance = 0.1f;

    [Tooltip("Подсветка при выделении. Накладывается поверх оригинального цвета материала.")]
    public Color selectedTint = new(1f, 0.9f, 0.4f, 1f);

    private Vector3? destination;
    private Renderer rend;
    private Color    originalColor = Color.white;
    private string   colorProp;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend != null && rend.sharedMaterial != null)
        {
            if (rend.sharedMaterial.HasProperty("_BaseColor"))
            {
                colorProp = "_BaseColor";
                originalColor = rend.sharedMaterial.GetColor(colorProp);
            }
            else if (rend.sharedMaterial.HasProperty("_Color"))
            {
                colorProp = "_Color";
                originalColor = rend.sharedMaterial.GetColor(colorProp);
            }
        }
    }

    public bool IsSelected { get; private set; }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        if (rend == null || rend.sharedMaterial == null || colorProp == null) return;
        rend.sharedMaterial.SetColor(colorProp, selected ? selectedTint : originalColor);
    }

    public void MoveTo(Vector3 point)
    {
        destination = new Vector3(point.x, transform.position.y, point.z);
    }

    void Update()
    {
        if (!destination.HasValue) return;

        Vector3 pos  = transform.position;
        Vector3 dst  = destination.Value;
        Vector3 next = Vector3.MoveTowards(pos, dst, moveSpeed * Time.deltaTime);
        transform.position = next;

        Vector3 dir = dst - pos;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 10f * Time.deltaTime);
        }

        if (Vector3.Distance(pos, dst) < arriveDistance) destination = null;
    }
}
