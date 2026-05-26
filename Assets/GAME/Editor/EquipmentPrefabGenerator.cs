using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-утилита: создаёт ItemDefinition-ассеты (если их ещё нет) и Prefab'ы
/// для базового снаряжения — шлем, рюкзак, разгрузка, броня, тайный кейс,
/// винтовка, пистолет.
///
/// Запуск: Tools → Inventory → Generate Equipment Prefabs
/// </summary>
public static class EquipmentPrefabGenerator
{
    private const string ItemsFolder   = "Assets/Inventory/Items";
    private const string PrefabsFolder = "Assets/Prefabs/Equipment";

    private struct Spec
    {
        public string   id;
        public string   displayName;
        public ItemType itemType;
        public int      width, height;
        public bool     canRotate;
        public float    weight;
        public bool     isContainer;
        public int      containerW, containerH;
        public float    containerMaxWeight;
        public Color    color;
    }

    private static readonly Spec[] Specs = new[]
    {
        new Spec {
            id = "helmet", displayName = "Шлем",
            itemType = ItemType.Helmet,
            width = 2, height = 2, canRotate = false, weight = 1.2f,
            color = new Color(0.40f, 0.40f, 0.42f)
        },
        new Spec {
            id = "backpack_large", displayName = "Рюкзак",
            itemType = ItemType.Backpack,
            width = 4, height = 4, canRotate = false, weight = 2.5f,
            isContainer = true, containerW = 6, containerH = 8, containerMaxWeight = 40f,
            color = new Color(0.45f, 0.30f, 0.18f)
        },
        new Spec {
            id = "rig_tactical", displayName = "Разгрузка",
            itemType = ItemType.Rig,
            width = 3, height = 3, canRotate = false, weight = 1.5f,
            isContainer = true, containerW = 5, containerH = 3, containerMaxWeight = 20f,
            color = new Color(0.30f, 0.35f, 0.20f)
        },
        new Spec {
            id = "body_armor", displayName = "Бронежилет",
            itemType = ItemType.BodyArmor,
            width = 3, height = 3, canRotate = false, weight = 5.5f,
            color = new Color(0.25f, 0.30f, 0.22f)
        },
        new Spec {
            id = "secure_case", displayName = "Тайный кейс",
            itemType = ItemType.SecureCase,
            width = 2, height = 2, canRotate = false, weight = 0.8f,
            isContainer = true, containerW = 3, containerH = 3, containerMaxWeight = 0f,
            color = new Color(0.80f, 0.65f, 0.20f)
        },
        new Spec {
            id = "rifle_main", displayName = "Винтовка",
            itemType = ItemType.PrimaryWeapon,
            width = 4, height = 1, canRotate = true, weight = 3.6f,
            color = new Color(0.20f, 0.20f, 0.20f)
        },
        new Spec {
            id = "pistol_sidearm", displayName = "Пистолет",
            itemType = ItemType.Sidearm,
            width = 2, height = 1, canRotate = true, weight = 0.9f,
            color = new Color(0.15f, 0.15f, 0.15f)
        },
    };

    [MenuItem("Tools/Inventory/Generate Equipment Prefabs")]
    public static void Generate()
    {
        EnsureFolder(ItemsFolder);
        EnsureFolder(PrefabsFolder);

        int created = 0, updated = 0;
        foreach (var spec in Specs)
        {
            var def    = LoadOrCreateDefinition(spec, out bool defCreated);
            var prefab = CreateOrUpdatePrefab(spec, def, out bool prefabCreated);

            // Привязываем сгенерированный prefab обратно в SO, чтобы WorldItem3D.Spawn
            // и окно осмотра использовали именно эту модель.
            if (prefab != null && def.worldPrefab != prefab)
            {
                def.worldPrefab = prefab;
                EditorUtility.SetDirty(def);
            }

            if (defCreated || prefabCreated) created++;
            else updated++;

            Debug.Log($"[EquipmentPrefabGenerator] {(defCreated || prefabCreated ? "Created" : "Updated")}: " +
                      $"{spec.displayName}  →  {AssetDatabase.GetAssetPath(prefab)}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("Equipment Prefab Generator",
            $"Готово.\n\nНовых: {created}\nОбновлено: {updated}\n\nПрефабы: {PrefabsFolder}", "OK");
    }

    // ── ItemDefinition ────────────────────────────────────────────────────────

    private static ItemDefinition LoadOrCreateDefinition(Spec spec, out bool created)
    {
        string path = $"{ItemsFolder}/{spec.id}.asset";
        var def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
        created = def == null;

        if (def == null)
        {
            def = ScriptableObject.CreateInstance<ItemDefinition>();
            AssetDatabase.CreateAsset(def, path);
        }

        def.itemId             = spec.id;
        def.displayName        = spec.displayName;
        def.itemType           = spec.itemType;
        def.width              = spec.width;
        def.height             = spec.height;
        def.canRotate          = spec.canRotate;
        def.maxStackSize       = 1;
        def.weightPerUnit      = spec.weight;
        def.isContainer        = spec.isContainer;
        def.containerWidth     = Mathf.Max(1, spec.containerW);
        def.containerHeight    = Mathf.Max(1, spec.containerH);
        def.containerMaxWeight = spec.containerMaxWeight;

        EditorUtility.SetDirty(def);
        return def;
    }

    // ── Prefab ────────────────────────────────────────────────────────────────

    private static GameObject CreateOrUpdatePrefab(Spec spec, ItemDefinition def, out bool created)
    {
        string path = $"{PrefabsFolder}/{spec.id}.prefab";
        created = !File.Exists(path);

        // Temp GameObject — будет сохранён как Prefab Asset
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = spec.displayName;

        // Размер: масштаб 0.1 на одну "клетку" (как в WorldItem3D.Spawn)
        go.transform.localScale = new Vector3(
            0.1f * spec.width,
            0.1f,
            0.1f * spec.height);

        // Цвет
        var renderer = go.GetComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader == null || mat.shader.name.Contains("Hidden"))
            mat = new Material(Shader.Find("Standard"));
        mat.color = spec.color;
        renderer.sharedMaterial = mat;

        // Физика
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = Mathf.Max(0.1f, spec.weight);

        // BoxCollider уже добавил CreatePrimitive — оставляем как физический
        var col = go.GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = false;

        // Триггер-коллайдер для зоны подбора (немного больше) —
        // WorldItem3D.EnsureTrigger всё равно добавит его в рантайме, если этого нет.
        var trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size      = new Vector3(2f, 2f, 2f);

        // WorldItem3D
        var wi = go.AddComponent<WorldItem3D>();
        wi.definition = def;
        wi.stackCount = 1;

        // Сохранение материала как sub-asset prefab'а, чтобы не плодить .mat-файлы
        // (опционально: можно вынести в отдельный mat-ассет, если нужно расшаривать)
        PrefabUtility.SaveAsPrefabAsset(go, path, out bool success);
        Object.DestroyImmediate(go);

        if (!success)
            Debug.LogError($"[EquipmentPrefabGenerator] Failed to save prefab: {path}");

        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    // ── Folder helpers ────────────────────────────────────────────────────────

    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder)) return;

        var parts = assetFolder.Split('/');
        string accum = parts[0]; // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{accum}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(accum, parts[i]);
            accum = next;
        }
    }
}
