using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Рендерит world-префаб предмета (3D или 2D) в спрайт-превью для UI.
/// Заменяет статичные иконки: в сетке инвентаря, экипировке, хотбаре и призраке перетаскивания
/// показываются настоящие префаб-объекты, а не <see cref="ItemDefinition.icon"/>.
///
/// Рендер происходит ОДИН раз на <see cref="ItemDefinition"/> и кешируется — это снимок префаба,
/// а не живая камера на каждый слот, поэтому дёшево по производительности.
///
/// Принцип (повторяет окно осмотра): скрытый camera rig в изолированном месте (y=-1000),
/// 3D Point-light'ы, инстанс префаба без физики/коллайдеров, кадрирование по bounds,
/// один Render() в RenderTexture → ReadPixels в Texture2D → Sprite.
/// </summary>
public static class ItemPreviewRenderer
{
    private const int   DefaultSize     = 128;
    private static readonly Vector3 IsolationOrigin = new(0f, -1000f, 0f);

    // Кэш превью по определению предмета. Один Sprite на тип предмета.
    private static readonly Dictionary<ItemDefinition, Sprite> s_Cache = new();

    private static GameObject s_Rig;
    private static Camera     s_Camera;

    /// <summary>
    /// Возвращает закешированный спрайт для предмета. Приоритет: назначенная
    /// <see cref="ItemDefinition.icon"/> → иначе превью префаба (2D-спрайт или рендер 3D-меша).
    /// Рендерит/кеширует при первом запросе.
    /// </summary>
    public static Sprite GetSprite(ItemDefinition def, int size = DefaultSize)
    {
        if (def == null) return null;
        if (s_Cache.TryGetValue(def, out var cached) && cached != null) return cached;

        // Приоритет: назначенная иконка → иначе превью префаба
        // (2D — спрайт из SpriteRenderer'а, 3D — рендер меша).
        Sprite sprite = def.icon;

        // 2D без иконки: спрайт берём НАПРЯМУЮ из SpriteRenderer'а world-префаба —
        // надёжнее off-screen рендера (нет артефактов кадрирования) и не зависит от того,
        // в какое поле (2D/3D) назначен спрайт-префаб.
        if (sprite == null && def.worldMode == WorldMode.TwoD)
            sprite = ExtractSpriteFromPrefab(def);

        // 3D без иконки (или у 2D не нашлось SpriteRenderer'а) — рендерим префаб в превью.
        sprite ??= Render(def, size);

        s_Cache[def] = sprite;
        return sprite;
    }

    /// <summary>
    /// Достаёт готовый спрайт из SpriteRenderer'а 2D-префаба. Перебирает кандидатов
    /// (<see cref="ItemDefinition.ActiveWorldPrefab"/>, затем явные 2D/3D поля), чтобы найти
    /// спрайт даже если префаб назначен «не в то» поле.
    /// </summary>
    private static Sprite ExtractSpriteFromPrefab(ItemDefinition def)
    {
        var candidates = new[] { def.ActiveWorldPrefab, def.worldPrefab2D, def.worldPrefab3D };
        foreach (var prefab in candidates)
        {
            if (prefab == null) continue;
            var sr = prefab.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null && sr.sprite != null) return sr.sprite;
        }
        return null;
    }

    /// <summary>Сбрасывает кэш (например, если префабы/иконки поменялись в рантайме).</summary>
    public static void ClearCache() => s_Cache.Clear();

    // ── Render ──────────────────────────────────────────────────────────────────

    private static Sprite Render(ItemDefinition def, int size)
    {
        EnsureRig();

        var host = new GameObject("PreviewHost");
        host.transform.position = IsolationOrigin;

        var model = SpawnModel(def, host.transform);
        if (model == null) { Object.DestroyImmediate(host); return null; }

        // 3D — лёгкий поворот 3/4, 2D — строго лицом к камере.
        bool isTwoD = def.worldMode == WorldMode.TwoD;
        host.transform.rotation = isTwoD ? Quaternion.identity : Quaternion.Euler(-15f, 25f, 0f);

        if (!TryFrameCamera(host, out float distance))
        {
            Object.DestroyImmediate(host);
            return null;
        }

        s_Camera.transform.localPosition = new Vector3(0f, 0f, -distance);
        s_Camera.transform.localRotation = Quaternion.identity;

        var rt   = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32);
        var prev = RenderTexture.active;
        s_Camera.targetTexture = rt;
        s_Camera.Render();

        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();

        RenderTexture.active   = prev;
        s_Camera.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(host);

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = $"Preview_{def.itemId}";
        return sprite;
    }

    private static GameObject SpawnModel(ItemDefinition def, Transform parent)
    {
        GameObject model;
        bool isTwoD = def.worldMode == WorldMode.TwoD;

        if (def.ActiveWorldPrefab != null)
        {
            model = Object.Instantiate(def.ActiveWorldPrefab, parent);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
        }
        else if (isTwoD)
        {
            // Fallback 2D: спрайт из иконки.
            model = new GameObject("PreviewSprite2D", typeof(SpriteRenderer));
            model.transform.SetParent(parent, false);
            var sr = model.GetComponent<SpriteRenderer>();
            sr.sprite = def.icon;
            if (def.icon == null) sr.color = new Color(0.4f, 0.7f, 0.4f);
        }
        else
        {
            // Fallback 3D: цветной куб.
            model = GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.SetParent(parent, false);
            model.transform.localScale = new Vector3(
                Mathf.Max(0.4f, 0.25f * def.width), 0.25f, Mathf.Max(0.4f, 0.25f * def.height));
            var r = model.GetComponent<Renderer>();
            if (r != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                r.material = new Material(shader)
                {
                    color = def.isContainer ? new Color(0.3f, 0.5f, 0.7f) : new Color(0.4f, 0.7f, 0.4f)
                };
            }
        }

        // Снимаем всё, что влияет на сцену/физику.
        foreach (var rb  in model.GetComponentsInChildren<Rigidbody>(true))     Object.DestroyImmediate(rb);
        foreach (var rb2 in model.GetComponentsInChildren<Rigidbody2D>(true))   Object.DestroyImmediate(rb2);
        foreach (var c   in model.GetComponentsInChildren<Collider>(true))      c.enabled = false;
        foreach (var c2  in model.GetComponentsInChildren<Collider2D>(true))    c2.enabled = false;
        foreach (var wi  in model.GetComponentsInChildren<WorldItemBase>(true)) Object.DestroyImmediate(wi);

        // 2D-спрайты часто на Sprite-Lit-Default — без Light2D рисуются чёрным. Делаем unlit.
        if (isTwoD)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var unlit = new Material(shader);
                foreach (var sr in model.GetComponentsInChildren<SpriteRenderer>(true))
                    sr.sharedMaterial = unlit;
            }
        }

        return model;
    }

    private static bool TryFrameCamera(GameObject host, out float distance)
    {
        distance = 1.5f;
        var renderers = host.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return false;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        // Центрируем модель в host.
        Vector3 offset = host.transform.position - bounds.center;
        foreach (Transform child in host.transform) child.position += offset;

        float maxDim = Mathf.Max(bounds.extents.magnitude * 2f, 0.01f);
        distance = Mathf.Clamp(maxDim * 1.6f, 0.3f, 12f);
        return true;
    }

    // ── Rig ──────────────────────────────────────────────────────────────────────

    private static void EnsureRig()
    {
        if (s_Rig != null && s_Camera != null) return;

        s_Rig = new GameObject("ItemPreviewRig") { hideFlags = HideFlags.HideAndDontSave };
        s_Rig.transform.position = IsolationOrigin;
        Object.DontDestroyOnLoad(s_Rig);

        s_Camera = new GameObject("PreviewCamera", typeof(Camera)).GetComponent<Camera>();
        s_Camera.transform.SetParent(s_Rig.transform, false);
        s_Camera.enabled         = false;                 // рендерим вручную через Render()
        s_Camera.clearFlags      = CameraClearFlags.SolidColor;
        s_Camera.backgroundColor = new Color(0f, 0f, 0f, 0f); // прозрачный фон превью
        s_Camera.fieldOfView     = 45f;
        s_Camera.nearClipPlane   = 0.01f;
        s_Camera.farClipPlane    = 50f;
        s_Camera.cullingMask     = ~0;
        s_Camera.allowHDR        = false;
        s_Camera.allowMSAA       = true;

        // Point-light'ы (НЕ directional — он бы освещал весь мир и влиял на тени сцены).
        AddPointLight("KeyLight",  new Vector3( 1.2f,  1.5f, -1.5f), 2.5f, 8f, Color.white);
        AddPointLight("FillLight", new Vector3(-1.0f,  0.5f, -1.0f), 1.0f, 6f, new Color(0.85f, 0.9f, 1f));
        AddPointLight("RimLight",  new Vector3( 0.0f, -0.8f,  1.5f), 0.6f, 5f, new Color(1f, 0.95f, 0.85f));
    }

    private static void AddPointLight(string name, Vector3 localPos, float intensity, float range, Color color)
    {
        var go = new GameObject(name, typeof(Light));
        go.transform.SetParent(s_Rig.transform, false);
        go.transform.localPosition = localPos;
        var light = go.GetComponent<Light>();
        light.type      = LightType.Point;
        light.range     = range;
        light.intensity = intensity;
        light.color     = color;
        light.shadows   = LightShadows.None;
    }
}
