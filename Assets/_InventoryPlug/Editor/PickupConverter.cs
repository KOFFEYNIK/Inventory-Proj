using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-утилита: превращает любой GameObject в готовый Pickup одним нажатием.
/// Два режима — 3D и 2D:
///
///   Tools → Inventory → Make Pickup From Selected — 3D (Ctrl+Alt+P)
///       Добавляет BoxCollider + Rigidbody + WorldItem3D. Подбирает ItemDefinition по имени.
///
///   Tools → Inventory → Make Pickup From Selected — 2D (Ctrl+Alt+Shift+P)
///       Добавляет BoxCollider2D + Rigidbody2D + WorldItem2D. Подбирает ItemDefinition по имени.
///
///   Tools → Inventory → Save Selected As Pickup Prefab...
///       Сохраняет выделенный GameObject как Prefab в Assets/GAME/Prefabs/Equipment/
///       и привязывает prefab в ItemDefinition.worldPrefab.
/// </summary>
public static class PickupConverter
{
    public enum Mode { ThreeD, TwoD }

    private const string ConvertMenuPath3D = "Tools/Inventory/Make Pickup From Selected — 3D %&p";       // Ctrl+Alt+P
    private const string ConvertMenuPath2D = "Tools/Inventory/Make Pickup From Selected — 2D %&#p";      // Ctrl+Alt+Shift+P
    private const string SaveMenuPath      = "Tools/Inventory/Save Selected As Pickup Prefab...";
    private const string DefaultPrefabFolder = "Assets/GAME/Prefabs/Equipment";

    // ── Convert ──────────────────────────────────────────────────────────────

    [MenuItem(ConvertMenuPath3D)] public static void MakePickup3D() => Convert(Mode.ThreeD);
    [MenuItem(ConvertMenuPath2D)] public static void MakePickup2D() => Convert(Mode.TwoD);

    [MenuItem(ConvertMenuPath3D, true)] public static bool ValidateMakePickup3D() => Selection.gameObjects.Length > 0;
    [MenuItem(ConvertMenuPath2D, true)] public static bool ValidateMakePickup2D() => Selection.gameObjects.Length > 0;

    private static void Convert(Mode mode)
    {
        var targets = Selection.gameObjects;
        if (targets == null || targets.Length == 0)
        {
            EditorUtility.DisplayDialog("Pickup Converter",
                "Сначала выдели один или несколько GameObject в Hierarchy.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName($"Make Pickup ({(mode == Mode.ThreeD ? "3D" : "2D")})");
        int group = Undo.GetCurrentGroup();

        int converted = 0;
        var report = new List<string> { $"Mode: {(mode == Mode.ThreeD ? "3D" : "2D")}" };
        foreach (var go in targets)
        {
            if (go == null) continue;
            if (PrefabUtility.IsPartOfPrefabAsset(go))
            {
                report.Add($"! {go.name}: пропущен — это Prefab Asset в Project, не сцена. " +
                           "Перетащи в сцену, конвертируй, потом сохрани обратно.");
                continue;
            }
            bool ok = mode == Mode.ThreeD ? ConvertOne3D(go, report) : ConvertOne2D(go, report);
            if (ok) converted++;
        }

        Undo.CollapseUndoOperations(group);
        string summary = $"Конвертировано: {converted}/{targets.Length}\n\n" + string.Join("\n", report);
        Debug.Log("[PickupConverter]\n" + summary);
        EditorUtility.DisplayDialog("Pickup Converter", summary, "OK");
    }

    private static bool ConvertOne3D(GameObject go, List<string> report)
    {
        var sb = new StringBuilder();
        sb.Append("• ").Append(go.name).Append(": ");

        // 1) ItemDefinition
        var existingWi = go.GetComponent<WorldItem3D>();
        ItemDefinition def = existingWi != null ? existingWi.definition : null;
        if (def == null)
        {
            def = FindMatchingDefinition(go.name);
            sb.Append(def != null ? $"def='{def.name}' (auto); " : "def=НЕТ (выставь руками); ");
        }
        else sb.Append($"def='{def.name}' (уже было); ");

        // 2) Collider (3D)
        var existingCol = go.GetComponent<Collider>();
        if (existingCol == null)
        {
            var bc = Undo.AddComponent<BoxCollider>(go);
            FitBoxColliderToRenderers(bc, go);
            sb.Append("+BoxCollider(auto-fit); ");
            existingCol = bc;
        }
        else sb.Append("=Collider; ");

        if (existingCol.isTrigger)
        {
            Undo.RecordObject(existingCol, "Set Collider Non-Trigger");
            existingCol.isTrigger = false;
            sb.Append("(trigger→solid); ");
        }

        // 3) Rigidbody (3D)
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) { rb = Undo.AddComponent<Rigidbody>(go); sb.Append("+Rigidbody; "); }
        else            sb.Append("=Rigidbody; ");

        if (def != null && def.weightPerUnit > 0f)
        {
            Undo.RecordObject(rb, "Set Mass");
            rb.mass = Mathf.Max(0.01f, def.weightPerUnit);
        }

        // 4) WorldItem3D
        var wi = go.GetComponent<WorldItem3D>();
        if (wi == null) { wi = Undo.AddComponent<WorldItem3D>(go); sb.Append("+WorldItem3D; "); }
        else            sb.Append("=WorldItem3D; ");

        if (def != null && wi.definition != def)
        {
            Undo.RecordObject(wi, "Set Definition");
            wi.definition = def;
            wi.stackCount = Mathf.Max(1, wi.stackCount);
        }

        EditorUtility.SetDirty(go);
        report.Add(sb.ToString());
        return true;
    }

    private static bool ConvertOne2D(GameObject go, List<string> report)
    {
        var sb = new StringBuilder();
        sb.Append("• ").Append(go.name).Append(": ");

        // 1) ItemDefinition
        var existingWi = go.GetComponent<WorldItem2D>();
        ItemDefinition def = existingWi != null ? existingWi.definition : null;
        if (def == null)
        {
            def = FindMatchingDefinition(go.name);
            sb.Append(def != null ? $"def='{def.name}' (auto); " : "def=НЕТ (выставь руками); ");
        }
        else sb.Append($"def='{def.name}' (уже было); ");

        // 2) Collider2D
        var existingCol = go.GetComponent<Collider2D>();
        if (existingCol == null)
        {
            var bc = Undo.AddComponent<BoxCollider2D>(go);
            FitBoxCollider2DToRenderers(bc, go);
            sb.Append("+BoxCollider2D(auto-fit); ");
            existingCol = bc;
        }
        else sb.Append("=Collider2D; ");

        if (existingCol.isTrigger)
        {
            Undo.RecordObject(existingCol, "Set Collider2D Non-Trigger");
            existingCol.isTrigger = false;
            sb.Append("(trigger→solid); ");
        }

        // 3) Rigidbody2D
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null) { rb = Undo.AddComponent<Rigidbody2D>(go); sb.Append("+Rigidbody2D; "); }
        else            sb.Append("=Rigidbody2D; ");

        if (def != null && def.weightPerUnit > 0f)
        {
            Undo.RecordObject(rb, "Set Mass");
            rb.mass = Mathf.Max(0.01f, def.weightPerUnit);
        }

        // 4) WorldItem2D
        var wi = go.GetComponent<WorldItem2D>();
        if (wi == null) { wi = Undo.AddComponent<WorldItem2D>(go); sb.Append("+WorldItem2D; "); }
        else            sb.Append("=WorldItem2D; ");

        if (def != null && wi.definition != def)
        {
            Undo.RecordObject(wi, "Set Definition");
            wi.definition = def;
            wi.stackCount = Mathf.Max(1, wi.stackCount);
        }

        EditorUtility.SetDirty(go);
        report.Add(sb.ToString());
        return true;
    }

    // ── Save as Prefab ───────────────────────────────────────────────────────

    [MenuItem(SaveMenuPath)]
    public static void SaveAsPrefab()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Save Pickup Prefab",
                "Выдели один GameObject в Hierarchy.", "OK");
            return;
        }
        if (PrefabUtility.IsPartOfPrefabAsset(go))
        {
            EditorUtility.DisplayDialog("Save Pickup Prefab",
                "Это уже Prefab Asset. Перетащи его инстанс в сцену и сохрани оттуда.", "OK");
            return;
        }

        EnsureFolder(DefaultPrefabFolder);
        string path = EditorUtility.SaveFilePanelInProject(
            "Сохранить pickup prefab",
            SanitizeName(go.name),
            "prefab",
            "Выбери куда сохранить prefab",
            DefaultPrefabFolder);
        if (string.IsNullOrEmpty(path)) return;

        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction);
        if (prefab == null)
        {
            Debug.LogError("[PickupConverter] Не удалось сохранить prefab: " + path);
            return;
        }

        // Привязываем prefab → ItemDefinition.worldPrefab. Поддерживаем оба варианта.
        var def = TryFindDefinitionOnPrefab(prefab);
        if (def != null && def.worldPrefab3D != prefab)
        {
            def.worldPrefab3D = prefab;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            Debug.Log($"[PickupConverter] Привязан prefab → ItemDefinition.worldPrefab ({def.name})");
        }

        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log("[PickupConverter] Сохранён pickup prefab: " + path);
    }

    [MenuItem(SaveMenuPath, true)]
    public static bool ValidateSaveAsPrefab() =>
        Selection.activeGameObject != null &&
        !PrefabUtility.IsPartOfPrefabAsset(Selection.activeGameObject);

    private static ItemDefinition TryFindDefinitionOnPrefab(GameObject prefab)
    {
        var wi3d = prefab.GetComponent<WorldItem3D>();
        if (wi3d != null && wi3d.definition != null) return wi3d.definition;
        var wi2d = prefab.GetComponent<WorldItem2D>();
        if (wi2d != null && wi2d.definition != null) return wi2d.definition;
        return null;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static ItemDefinition FindMatchingDefinition(string goName)
    {
        if (string.IsNullOrEmpty(goName)) return null;

        var guids = AssetDatabase.FindAssets("t:ItemDefinition");
        ItemDefinition looseMatch = null;
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var def  = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def == null) continue;

            if (string.Equals(def.name,        goName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(def.displayName, goName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(def.itemId,      goName, StringComparison.OrdinalIgnoreCase))
                return def;

            if (looseMatch == null &&
                (goName.IndexOf(def.name, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 (!string.IsNullOrEmpty(def.displayName) &&
                  goName.IndexOf(def.displayName, StringComparison.OrdinalIgnoreCase) >= 0)))
                looseMatch = def;
        }
        return looseMatch;
    }

    private static void FitBoxColliderToRenderers(BoxCollider bc, GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bc.center = Vector3.zero;
            bc.size   = Vector3.one * 0.5f;
            return;
        }

        var worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) worldBounds.Encapsulate(renderers[i].bounds);

        var t = bc.transform;
        var localCenter = t.InverseTransformPoint(worldBounds.center);
        var s = t.lossyScale;
        bc.center = localCenter;
        bc.size = new Vector3(
            worldBounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
            worldBounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
            worldBounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z))
        );
    }

    /// <summary>
    /// Подгоняет BoxCollider2D под мировые bounds SpriteRenderer'ов (или любых Renderer'ов).
    /// Размер пересчитывается в локальные единицы коллайдера через lossyScale.x/y.
    /// </summary>
    private static void FitBoxCollider2DToRenderers(BoxCollider2D bc, GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
        {
            bc.offset = Vector2.zero;
            bc.size   = Vector2.one * 0.5f;
            return;
        }

        var worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) worldBounds.Encapsulate(renderers[i].bounds);

        var t = bc.transform;
        var localCenter = t.InverseTransformPoint(worldBounds.center);
        var s = t.lossyScale;
        bc.offset = new Vector2(localCenter.x, localCenter.y);
        bc.size = new Vector2(
            worldBounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
            worldBounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y))
        );
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

    private static string SanitizeName(string n)
    {
        var arr = n.ToCharArray();
        for (int i = 0; i < arr.Length; i++)
            if (Path.GetInvalidFileNameChars().Length > 0 && Array.IndexOf(Path.GetInvalidFileNameChars(), arr[i]) >= 0)
                arr[i] = '_';
        return new string(arr);
    }
}
