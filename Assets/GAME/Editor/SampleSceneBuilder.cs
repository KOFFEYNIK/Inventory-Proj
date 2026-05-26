using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor-утилита: создаёт минимальные демо-сцены 2D и 3D для проверки плагина end-to-end.
///
///   Tools → Inventory → Samples → Build 3D Sample (Tarkov-like)
///       Plane + Camera + Player(cube, tag) + 3 WorldItem3D-пикапа + InventoryRig (HoverPickup3D).
///       Привязывает InventoryConfig_TarkovLike, если он есть в проекте.
///
///   Tools → Inventory → Samples → Build 2D Sample (Diablo-like)
///       Ortho-камера + Player(sprite, tag) + 3 WorldItem2D-пикапа + InventoryRig (HoverPickup2D, Mouse).
///       Привязывает InventoryConfig_DiabloLike, если он есть.
///
/// Сохраняет результат в Assets/GAME/Scenes/. После запуска сцены: подведи мышь к пикапу,
/// нажми F — предмет попадёт в инвентарь. Tab — открыть/закрыть инвентарь.
/// </summary>
public static class SampleSceneBuilder
{
    private const string ScenesFolder = "Assets/GAME/Scenes";
    private const string PlayerTag    = "Player";

    // ── 3D ───────────────────────────────────────────────────────────────────

    [MenuItem("Tools/Inventory/Samples/Build 3D Sample (Tarkov-like)")]
    public static void Build3D()
    {
        if (!ConfirmReplaceScene("3D")) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 1) Ground
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(2f, 1f, 2f);
        TintRenderer(ground, new Color(0.45f, 0.45f, 0.45f));

        // 2) Player — Capsule с Rigidbody + CapsuleCollider + PlayerController
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);
        EnsureTag(PlayerTag);
        player.tag = PlayerTag;
        TintRenderer(player, new Color(0.2f, 0.6f, 0.9f));

        var rb = player.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
        // CapsuleCollider уже есть от CreatePrimitive.
        var pc = player.AddComponent<PlayerController>();
        pc.faceMovement = false; // в FPS тело крутится через mouse-look, а не auto-face

        // 3) Камера — child от player, на высоте головы.
        // NewScene создал отдельную Main Camera; уничтожим её и сделаем child-камеру.
        if (Camera.main != null) Object.DestroyImmediate(Camera.main.gameObject);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 0.7f, 0f); // глаза примерно на y=1.7 в world
        camGo.transform.localRotation = Quaternion.identity;
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();

        var look = camGo.AddComponent<FPSMouseLook>();
        look.body        = player.transform;
        look.sensitivity = 2f;

        // PlayerController.cameraTransform → наша FPS-камера
        var pcSO = new SerializedObject(pc);
        pcSO.FindProperty("cameraTransform").objectReferenceValue = camGo.transform;
        pcSO.ApplyModifiedProperties();

        // 4) Три WorldItem3D-пикапа вокруг игрока
        Spawn3DPickup("Pickup_1", new Vector3(-1.5f, 0.25f, 2f), new Color(0.9f, 0.3f, 0.3f));
        Spawn3DPickup("Pickup_2", new Vector3( 1.5f, 0.25f, 2f), new Color(0.3f, 0.9f, 0.3f));
        Spawn3DPickup("Pickup_3", new Vector3( 0f,   0.25f, 3.5f), new Color(0.9f, 0.8f, 0.2f));

        // 5) Light (directional), чтобы URP не остался без основного света
        EnsureDirectionalLight();

        // 6) Выбираем Player и зовём InventorySpawner для 3D
        Selection.activeGameObject = player;
        InventorySpawner.Spawn(InventorySpawner.Mode.ThreeD);

        // 7) Привязываем Tarkov-like config, если есть
        TryAttachConfig("InventoryConfig_TarkovLike");
        // FPS: курсором рулит FPSMouseLook (lock на старте, unlock при открытом инвентаре),
        // поэтому UI трогать курсор не должен.
        ConfigureCursor(manageCursor: false, lockOnClose: false);

        SaveSampleScene("Sample_Tarkov3D");
    }

    private static void EnsureDirectionalLight()
    {
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (l.type == LightType.Directional) return;

        var lightGo = new GameObject("Directional Light");
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var light = lightGo.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.intensity = 1f;
    }

    private static void Spawn3DPickup(string name, Vector3 pos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * 0.4f;
        TintRenderer(go, color);

        // Collider уже есть (BoxCollider от CreatePrimitive); добавим Rigidbody и WorldItem3D.
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 1f;

        var wi = go.AddComponent<WorldItem3D>();
        wi.stackCount = 1;
        // definition оставляем null — игрок может подобрать в инвентарь только если definition назначен.
        // В sample-сцене юзер сам привязывает любой ItemDefinition из Assets/GAME/Inventory/Items/.
    }

    // ── 2D ───────────────────────────────────────────────────────────────────

    // ── Diablo-like 2D ───────────────────────────────────────────────────────

    [MenuItem("Tools/Inventory/Samples/Build 2D Sample (Diablo-like — top-down)")]
    public static void Build2D_Diablo()
    {
        if (!ConfirmReplaceScene("Diablo 2D")) return;
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 1) Camera: ortho, top-down view на плоскость XY
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic     = true;
            cam.orthographicSize = 5f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.12f, 0.16f);
        }

        // 2) Player — sprite-квадрат с Rigidbody2D + TopDown2DController.
        var player = Create2DSquare("Player", Vector3.zero, new Vector2(0.8f, 0.8f), new Color(0.2f, 0.6f, 0.9f));
        EnsureTag(PlayerTag);
        player.tag = PlayerTag;
        var playerRb = player.AddComponent<Rigidbody2D>();
        playerRb.gravityScale = 0f;
        playerRb.freezeRotation = true;
        var playerCol = player.AddComponent<BoxCollider2D>();
        playerCol.size = Vector2.one;
        player.AddComponent<TopDown2DController>();

        // 3) Несколько пикапов
        Spawn2DPickup("Pickup_1", new Vector3(-2f,  1.5f, 0f), new Color(0.9f, 0.3f, 0.3f));
        Spawn2DPickup("Pickup_2", new Vector3( 2f,  1.5f, 0f), new Color(0.3f, 0.9f, 0.3f));
        Spawn2DPickup("Pickup_3", new Vector3( 0f, -2.0f, 0f), new Color(0.9f, 0.8f, 0.2f));

        // 4) InventoryRig (2D-режим)
        Selection.activeGameObject = player;
        InventorySpawner.Spawn(InventorySpawner.Mode.TwoD);

        TryAttachConfig("InventoryConfig_DiabloLike");
        // 2D top-down: курсор всегда видим, никто его не лочит.
        ConfigureCursor(manageCursor: false, lockOnClose: false);
        SaveSampleScene("Sample_Diablo2D");
    }

    // ── Metroid-like 2D (платформер) ─────────────────────────────────────────

    [MenuItem("Tools/Inventory/Samples/Build 2D Sample (Metroid-like — platformer)")]
    public static void Build2D_Metroid()
    {
        if (!ConfirmReplaceScene("Metroid 2D")) return;
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 1) Camera ortho, side-scroller
        var cam = Camera.main;
        if (cam != null)
        {
            cam.orthographic     = true;
            cam.orthographicSize = 4f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.transform.rotation = Quaternion.identity;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.08f, 0.10f, 0.16f);
        }

        // 2) Земля: длинная платформа
        SpawnPlatform("Ground",     new Vector3( 0f,  -3.0f, 0f), new Vector2(20f, 0.5f), new Color(0.35f, 0.30f, 0.25f));
        SpawnPlatform("Platform_1", new Vector3(-3f,  -1.0f, 0f), new Vector2( 2f, 0.3f), new Color(0.45f, 0.40f, 0.30f));
        SpawnPlatform("Platform_2", new Vector3( 3f,   0.5f, 0f), new Vector2( 2f, 0.3f), new Color(0.45f, 0.40f, 0.30f));

        // 3) Player с гравитацией + Platformer2DController.
        var player = Create2DSquare("Player", new Vector3(0f, -2f, 0f), new Vector2(0.7f, 1f), new Color(0.2f, 0.6f, 0.9f));
        EnsureTag(PlayerTag);
        player.tag = PlayerTag;
        var rb  = player.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 3f;
        var col = player.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;
        player.AddComponent<Platformer2DController>();

        // 4) Пикапы на платформах
        Spawn2DPickup("Pickup_1", new Vector3(-3f, -0.5f, 0f), new Color(0.9f, 0.3f, 0.3f));
        Spawn2DPickup("Pickup_2", new Vector3( 3f,  1.0f, 0f), new Color(0.3f, 0.9f, 0.3f));
        Spawn2DPickup("Pickup_3", new Vector3( 6f, -2.5f, 0f), new Color(0.9f, 0.8f, 0.2f));

        // 5) InventoryRig 2D
        Selection.activeGameObject = player;
        InventorySpawner.Spawn(InventorySpawner.Mode.TwoD);

        TryAttachConfig("InventoryConfig_DiabloLike");
        ConfigureCursor(manageCursor: false, lockOnClose: false);
        SaveSampleScene("Sample_Metroid2D");
    }

    private static void SpawnPlatform(string name, Vector3 pos, Vector2 size, Color color)
    {
        var go = Create2DSquare(name, pos, size, color);
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one; // localScale уже масштабирует
    }

    // ── RTS 3D ───────────────────────────────────────────────────────────────

    [MenuItem("Tools/Inventory/Samples/Build 3D Sample (RTS — mouse-select)")]
    public static void Build3D_RTS()
    {
        if (!ConfirmReplaceScene("RTS 3D")) return;
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 1) Земля (большая) — отдельный layer не делаем, raycast по всем
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(3f, 1f, 3f);
        TintRenderer(ground, new Color(0.35f, 0.45f, 0.30f));

        // 2) Камера: top-down под углом (~60° pitch)
        var cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = new Vector3(0f, 12f, -8f);
            cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.nearClipPlane = 0.1f;
        }

        EnsureDirectionalLight();

        // 3) Три юнита. Один — "герой" с tag Player (на нём будет InventoryRig).
        var hero  = Spawn3DUnit("Hero",  new Vector3(-2f, 0.5f, 0f), new Color(0.2f, 0.6f, 0.9f));
        EnsureTag(PlayerTag); hero.tag = PlayerTag;
        Spawn3DUnit("Unit_2", new Vector3( 0f, 0.5f, 0f), new Color(0.3f, 0.9f, 0.4f));
        Spawn3DUnit("Unit_3", new Vector3( 2f, 0.5f, 0f), new Color(0.9f, 0.6f, 0.3f));

        // 4) Commander — на пустом GameObject (или на камере)
        var commanderGo = new GameObject("RTSCommander");
        var cmd = commanderGo.AddComponent<RTSCommander>();
        if (cam != null) cmd.cam = cam;

        // 5) Несколько мировых пикапов на земле
        Spawn3DPickup("Pickup_1", new Vector3(-1f, 0.2f, 3f), new Color(0.9f, 0.3f, 0.3f));
        Spawn3DPickup("Pickup_2", new Vector3( 1f, 0.2f, 3f), new Color(0.9f, 0.8f, 0.2f));

        // 6) InventoryRig на героя (3D-режим, HoverPickup3D от центра экрана)
        Selection.activeGameObject = hero;
        InventorySpawner.Spawn(InventorySpawner.Mode.ThreeD);

        TryAttachConfig("InventoryConfig_TarkovLike");
        // RTS: курсор всегда видим — им же кликаем юнитов.
        ConfigureCursor(manageCursor: false, lockOnClose: false);
        SaveSampleScene("Sample_RTS3D");
    }

    private static GameObject Spawn3DUnit(string name, Vector3 pos, Color color)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = name;
        go.transform.position = pos;
        TintRenderer(go, color);
        go.AddComponent<RTSUnit>();
        // CapsuleCollider уже есть → попадает в LMB-raycast в Commander.
        return go;
    }

    // ── TopDown 3D с FreeLookCamera (Orbit) ──────────────────────────────────

    [MenuItem("Tools/Inventory/Samples/Build 3D Sample (TopDown — FreeLookCamera Orbit)")]
    public static void Build3D_TopDown()
    {
        if (!ConfirmReplaceScene("TopDown 3D")) return;
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(2f, 1f, 2f);
        TintRenderer(ground, new Color(0.4f, 0.45f, 0.4f));

        // Player с CapsuleCollider + Rigidbody + PlayerController (TPS).
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);
        EnsureTag(PlayerTag); player.tag = PlayerTag;
        TintRenderer(player, new Color(0.2f, 0.6f, 0.9f));

        var rb = player.AddComponent<Rigidbody>();
        rb.freezeRotation = true;
        var pc = player.AddComponent<PlayerController>();
        pc.faceMovement = true;

        // Камера: отдельный объект (НЕ child от player) с FreeLookCamera в orbit-режиме.
        if (Camera.main != null) Object.DestroyImmediate(Camera.main.gameObject);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(0f, 10f, -7f);
        camGo.transform.rotation = Quaternion.Euler(50f, 0f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.clearFlags = CameraClearFlags.Skybox;
        camGo.AddComponent<AudioListener>();

        var orbit = camGo.AddComponent<FreeLookCamera>();
        orbit.target       = player.transform;
        orbit.orbitMode    = true;
        orbit.distance     = 8f;
        orbit.minDistance  = 3f;
        orbit.maxDistance  = 20f;
        orbit.sensitivity  = 3f;
        orbit.clampVertical = true;
        orbit.minPitch     = 10f;
        orbit.maxPitch     = 80f;

        // PlayerController.cameraTransform → orbit камера (движение relative-camera)
        var pcSO = new SerializedObject(pc);
        pcSO.FindProperty("cameraTransform").objectReferenceValue = camGo.transform;
        pcSO.ApplyModifiedProperties();

        EnsureDirectionalLight();

        // 3 пикапа вокруг
        Spawn3DPickup("Pickup_1", new Vector3(-1.5f, 0.25f, 2f), new Color(0.9f, 0.3f, 0.3f));
        Spawn3DPickup("Pickup_2", new Vector3( 1.5f, 0.25f, 2f), new Color(0.3f, 0.9f, 0.3f));
        Spawn3DPickup("Pickup_3", new Vector3( 0f,   0.25f, 4f), new Color(0.9f, 0.8f, 0.2f));

        Selection.activeGameObject = player;
        InventorySpawner.Spawn(InventorySpawner.Mode.ThreeD);

        TryAttachConfig("InventoryConfig_TarkovLike");
        // TopDown FreeLookCamera Orbit: камера орбитит по drag, курсор не лочим.
        ConfigureCursor(manageCursor: false, lockOnClose: false);
        SaveSampleScene("Sample_TopDown3D");
    }

    private static GameObject Create2DSquare(string name, Vector3 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.position   = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = MakeOnePixelSprite();
        sr.color  = color;
        return go;
    }

    private static void Spawn2DPickup(string name, Vector3 pos, Color color)
    {
        var go = Create2DSquare(name, pos, new Vector2(0.4f, 0.4f), color);

        // 2D физика и WorldItem2D
        var col = go.AddComponent<BoxCollider2D>();
        col.size = Vector2.one;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Static; // пикап не падает в sample

        var wi = go.AddComponent<WorldItem2D>();
        wi.stackCount = 1;
    }

    private static Sprite s_OnePixelSprite;
    private static Sprite MakeOnePixelSprite()
    {
        if (s_OnePixelSprite != null) return s_OnePixelSprite;
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        tex.wrapMode   = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Point;
        s_OnePixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        s_OnePixelSprite.name = "SampleWhite1x1";
        return s_OnePixelSprite;
    }

    // ── Common helpers ───────────────────────────────────────────────────────

    private static bool ConfirmReplaceScene(string label)
    {
        return EditorUtility.DisplayDialog(
            $"Build {label} Sample Scene",
            "Будет создана новая сцена. Несохранённые изменения текущей сцены будут потеряны.\n\nПродолжить?",
            "Да, создать", "Отмена");
    }

    private static void EnsureTag(string tag)
    {
        var tagsProp = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0])
            .FindProperty("tags");
        if (tagsProp == null) return;

        for (int i = 0; i < tagsProp.arraySize; i++)
            if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) return;

        tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
        tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
        tagsProp.serializedObject.ApplyModifiedProperties();
    }

    private static void TintRenderer(GameObject go, Color color)
    {
        var renderer = go.GetComponent<Renderer>();
        if (renderer == null) return;

        // КЛЮЧЕВОЙ МОМЕНТ:
        // В URP/HDRP `new Material(Shader.Find("..."))` создаёт material БЕЗ нужных keywords
        // и текстур — он рендерится magenta. Правильный путь — копировать дефолтный material
        // активного render pipeline. Если pipeline нет (BuiltIn) — Standard через Shader.Find.
        Material baseMat = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
            ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultMaterial
            : null;
        // Запасной вариант — material самого primitive (у Cube/Plane есть default URP-Lit).
        if (baseMat == null) baseMat = renderer.sharedMaterial;

        Material mat = baseMat != null
            ? new Material(baseMat)
            : new Material(Shader.Find("Standard"));

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     color);

        renderer.sharedMaterial = mat;
    }

    /// <summary>
    /// Настраивает курсор у InventoryUI в текущей сцене.
    /// FPS-сцены: manageCursor=true, lockOnClose=true — UI сам прячет курсор обратно.
    /// 2D/RTS/TopDown: manageCursor=false — курсор остаётся видимым всегда, контроллер не трогает.
    /// </summary>
    private static void ConfigureCursor(bool manageCursor, bool lockOnClose)
    {
        var ui = Object.FindFirstObjectByType<InventoryUI>();
        if (ui == null) return;
        var so = new SerializedObject(ui);
        var mc = so.FindProperty("manageCursor");
        var lc = so.FindProperty("lockCursorOnClose");
        if (mc != null) mc.boolValue = manageCursor;
        if (lc != null) lc.boolValue = lockOnClose;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(ui);
    }

    private static void TryAttachConfig(string configAssetName)
    {
        var guids = AssetDatabase.FindAssets($"{configAssetName} t:InventoryConfig");
        if (guids.Length == 0) return;
        var cfg = AssetDatabase.LoadAssetAtPath<InventoryConfig>(AssetDatabase.GUIDToAssetPath(guids[0]));
        if (cfg == null) return;

        var mgr = Object.FindFirstObjectByType<InventoryManager>();
        if (mgr == null) return;

        var so = new SerializedObject(mgr);
        var prop = so.FindProperty("config");
        if (prop != null)
        {
            prop.objectReferenceValue = cfg;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(mgr);
            Debug.Log($"[SampleSceneBuilder] Привязан InventoryConfig: {cfg.name}");
        }
    }

    private static void SaveSampleScene(string name)
    {
        EnsureFolder(ScenesFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{ScenesFolder}/{name}.unity");
        bool saved  = EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);
        if (saved)
        {
            Debug.Log($"[SampleSceneBuilder] Сцена сохранена: {path}");
            EditorUtility.DisplayDialog("Sample Scene", $"Готово!\nПуть: {path}\n\nЗапусти Play (Ctrl+P) и попробуй подобрать пикапы.\nF — подбор (3D: курсор на предмет; 2D: курсор над предметом)\nTab — открыть инвентарь", "OK");
        }
        else
        {
            Debug.LogError($"[SampleSceneBuilder] Не удалось сохранить сцену: {path}");
        }
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder)) return;
        var parts = assetFolder.Split('/');
        string accum = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{accum}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(accum, parts[i]);
            accum = next;
        }
    }
}
