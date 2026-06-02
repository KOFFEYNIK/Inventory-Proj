using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-утилита: превращает выделенный GameObject в статичный мировой контейнер
/// (ящик/сундук), в который можно класть и забирать предметы.
///
///   Tools → Inventory → Make Container From Selected — 3D
///       Добавляет BoxCollider (не-триггер, auto-fit) + WorldContainer3D.
///
///   Tools → Inventory → Make Container From Selected — 2D
///       Добавляет BoxCollider2D (не-триггер, auto-fit) + WorldContainer2D.
///
/// В отличие от <see cref="PickupConverter"/> НЕ добавляет Rigidbody — ящик статичен и стоит в мире.
/// Число ячеек, заголовок и лимит веса настраиваются на компоненте в инспекторе
/// (поля gridWidth / gridHeight / displayTitle / maxWeight).
/// </summary>
public static class WorldContainerConverter
{
    public enum Mode { ThreeD, TwoD }

    private const string MenuPath3D = "Tools/Inventory/Make Container From Selected — 3D";
    private const string MenuPath2D = "Tools/Inventory/Make Container From Selected — 2D";

    [MenuItem(MenuPath3D)] public static void MakeContainer3D() => Convert(Mode.ThreeD);
    [MenuItem(MenuPath2D)] public static void MakeContainer2D() => Convert(Mode.TwoD);

    [MenuItem(MenuPath3D, true)] public static bool ValidateMake3D() => Selection.gameObjects.Length > 0;
    [MenuItem(MenuPath2D, true)] public static bool ValidateMake2D() => Selection.gameObjects.Length > 0;

    private static void Convert(Mode mode)
    {
        var targets = Selection.gameObjects;
        if (targets == null || targets.Length == 0)
        {
            EditorUtility.DisplayDialog("World Container Converter",
                "Сначала выдели один или несколько GameObject в Hierarchy.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName($"Make Container ({(mode == Mode.ThreeD ? "3D" : "2D")})");
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
        string summary = $"Назначено ящиков: {converted}/{targets.Length}\n\n" + string.Join("\n", report);
        Debug.Log("[WorldContainerConverter]\n" + summary);
        EditorUtility.DisplayDialog("World Container Converter", summary, "OK");
    }

    private static bool ConvertOne3D(GameObject go, List<string> report)
    {
        var sb = new StringBuilder();
        sb.Append("• ").Append(go.name).Append(": ");

        // 1) Collider (3D), не-триггер, auto-fit если новый
        var col = go.GetComponent<Collider>();
        if (col == null)
        {
            var bc = Undo.AddComponent<BoxCollider>(go);
            FitBoxColliderToRenderers(bc, go);
            sb.Append("+BoxCollider(auto-fit); ");
            col = bc;
        }
        else sb.Append("=Collider; ");

        if (col.isTrigger)
        {
            Undo.RecordObject(col, "Set Collider Non-Trigger");
            col.isTrigger = false;
            sb.Append("(trigger→solid); ");
        }

        // 2) WorldContainer3D (без Rigidbody — ящик статичен)
        var wc = go.GetComponent<WorldContainer3D>();
        if (wc == null) { wc = Undo.AddComponent<WorldContainer3D>(go); sb.Append("+WorldContainer3D; "); }
        else            sb.Append("=WorldContainer3D; ");

        sb.Append($"grid={wc.gridWidth}×{wc.gridHeight}; ");

        EditorUtility.SetDirty(go);
        report.Add(sb.ToString());
        return true;
    }

    private static bool ConvertOne2D(GameObject go, List<string> report)
    {
        var sb = new StringBuilder();
        sb.Append("• ").Append(go.name).Append(": ");

        // 1) Collider2D, не-триггер, auto-fit если новый
        var col = go.GetComponent<Collider2D>();
        if (col == null)
        {
            var bc = Undo.AddComponent<BoxCollider2D>(go);
            FitBoxCollider2DToRenderers(bc, go);
            sb.Append("+BoxCollider2D(auto-fit); ");
            col = bc;
        }
        else sb.Append("=Collider2D; ");

        if (col.isTrigger)
        {
            Undo.RecordObject(col, "Set Collider2D Non-Trigger");
            col.isTrigger = false;
            sb.Append("(trigger→solid); ");
        }

        // 2) WorldContainer2D (без Rigidbody — ящик статичен)
        var wc = go.GetComponent<WorldContainer2D>();
        if (wc == null) { wc = Undo.AddComponent<WorldContainer2D>(go); sb.Append("+WorldContainer2D; "); }
        else            sb.Append("=WorldContainer2D; ");

        sb.Append($"grid={wc.gridWidth}×{wc.gridHeight}; ");

        EditorUtility.SetDirty(go);
        report.Add(sb.ToString());
        return true;
    }

    // ── Helpers (auto-fit коллайдеров под рендереры) ──────────────────────────

    private static void FitBoxColliderToRenderers(BoxCollider bc, GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { bc.center = Vector3.zero; bc.size = Vector3.one; return; }

        var worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) worldBounds.Encapsulate(renderers[i].bounds);

        var t = bc.transform;
        var s = t.lossyScale;
        bc.center = t.InverseTransformPoint(worldBounds.center);
        bc.size = new Vector3(
            worldBounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
            worldBounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)),
            worldBounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(s.z)));
    }

    private static void FitBoxCollider2DToRenderers(BoxCollider2D bc, GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { bc.offset = Vector2.zero; bc.size = Vector2.one; return; }

        var worldBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) worldBounds.Encapsulate(renderers[i].bounds);

        var t = bc.transform;
        var s = t.lossyScale;
        var localCenter = t.InverseTransformPoint(worldBounds.center);
        bc.offset = new Vector2(localCenter.x, localCenter.y);
        bc.size = new Vector2(
            worldBounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(s.x)),
            worldBounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(s.y)));
    }
}
