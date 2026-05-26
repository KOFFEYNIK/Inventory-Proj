using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Модуль на камеру: при наведении мыши на объект мира создаёт обводку через inverse-hull shader.
///
/// Принцип: каждый кадр Raycast из камеры по позиции курсора. Если попадает в объект с
/// MeshRenderer (опционально — только если у него есть WorldItem) — спавнит дочерний клон
/// его меша с outline-материалом. При смене цели — переспавн, при отсутствии — удаление.
///
/// Цвет, прозрачность и ширина обводки настраиваются в инспекторе и обновляются в реальном времени.
/// </summary>
[RequireComponent(typeof(Camera))]
public class HoverOutline : MonoBehaviour
{
    [Header("Outline appearance")]
    [Tooltip("Цвет контура (альфа берётся из поля Alpha).")]
    [ColorUsage(false)] public Color color = Color.white;

    [Range(0f, 1f)]
    [Tooltip("Прозрачность контура. 0 = невидим, 1 = полностью непрозрачный.")]
    public float alpha = 1f;

    [Range(0f, 0.2f)]
    [Tooltip("Ширина обводки — выдавливание вершин по нормалям, в локальных единицах меша.")]
    public float width = 0.02f;

    [Header("Raycast")]
    [Tooltip("Слои, по которым стрелять. По умолчанию — все.")]
    public LayerMask hoverMask = ~0;

    [Tooltip("Максимальная дистанция луча от камеры.")]
    public float maxDistance = 50f;

    [Tooltip("Игнорировать, когда курсор над UI (инвентарь / меню).")]
    public bool ignoreOverUI = true;

    [Header("Filter")]
    [Tooltip("Обводить только объекты, у которых в иерархии есть компонент WorldItem.")]
    public bool onlyWorldItems = true;

    // ── internals ────────────────────────────────────────────────────────────
    private Camera     cam;
    private Material   outlineMaterial;
    private GameObject outlineClone;
    private Renderer   currentTarget;

    private static readonly int ID_OutlineColor = Shader.PropertyToID("_OutlineColor");
    private static readonly int ID_OutlineWidth = Shader.PropertyToID("_OutlineWidth");

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        cam = GetComponent<Camera>();
        var shader = Shader.Find("Custom/HoverOutline");
        if (shader == null)
        {
            Debug.LogError("[HoverOutline] Шейдер 'Custom/HoverOutline' не найден. " +
                           "Проверь что Assets/Shaders/HoverOutline.shader скомпилирован.");
            enabled = false;
            return;
        }
        outlineMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        ApplyMaterialProperties();
    }

    void OnDisable() => ClearOutline();

    void OnDestroy()
    {
        ClearOutline();
        if (outlineMaterial != null) Destroy(outlineMaterial);
    }

    void OnValidate() => ApplyMaterialProperties();

    void Update()
    {
        if (cam == null || outlineMaterial == null) return;
        ApplyMaterialProperties();

        // Курсор над UI — снимаем подсветку
        if (ignoreOverUI && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            SetTarget(null);
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out var hit, maxDistance, hoverMask, QueryTriggerInteraction.Ignore))
        {
            Renderer rend = hit.collider.GetComponentInParent<Renderer>();
            if (onlyWorldItems && hit.collider.GetComponentInParent<WorldItemBase>() == null)
                rend = null;
            SetTarget(rend);
        }
        else
        {
            SetTarget(null);
        }
    }

    // ── Outline application ──────────────────────────────────────────────────

    private void ApplyMaterialProperties()
    {
        if (outlineMaterial == null) return;
        var c = color; c.a = Mathf.Clamp01(alpha);
        outlineMaterial.SetColor(ID_OutlineColor, c);
        outlineMaterial.SetFloat(ID_OutlineWidth, Mathf.Max(0f, width));
    }

    private void SetTarget(Renderer newTarget)
    {
        if (newTarget == currentTarget) return;
        ClearOutline();
        currentTarget = newTarget;
        if (currentTarget != null) BuildOutlineClone(currentTarget);
    }

    private void BuildOutlineClone(Renderer target)
    {
        // Поддерживаем только MeshRenderer + MeshFilter. SkinnedMeshRenderer пропускаем
        // (нужна синхронизация скелета, для WorldItem-предметов не актуально).
        var mf = target.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        var mesh = mf.sharedMesh;
        int subCount = mesh.subMeshCount;
        var mats = new Material[subCount];
        for (int i = 0; i < subCount; i++) mats[i] = outlineMaterial;

        outlineClone = new GameObject("HoverOutlineClone")
        {
            hideFlags = HideFlags.HideAndDontSave,
            layer     = target.gameObject.layer
        };
        outlineClone.transform.SetParent(target.transform, false);
        outlineClone.transform.localPosition = Vector3.zero;
        outlineClone.transform.localRotation = Quaternion.identity;
        outlineClone.transform.localScale    = Vector3.one;

        var cloneMf = outlineClone.AddComponent<MeshFilter>();
        cloneMf.sharedMesh = mesh;

        var cloneMr = outlineClone.AddComponent<MeshRenderer>();
        cloneMr.sharedMaterials   = mats;
        cloneMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        cloneMr.receiveShadows    = false;
        cloneMr.lightProbeUsage   = UnityEngine.Rendering.LightProbeUsage.Off;
        cloneMr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    private void ClearOutline()
    {
        if (outlineClone != null) Destroy(outlineClone);
        outlineClone  = null;
        currentTarget = null;
    }
}
