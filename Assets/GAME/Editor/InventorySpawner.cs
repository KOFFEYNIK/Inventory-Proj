using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-утилита: автоматически разворачивает инвентарь в текущей сцене.
///
/// Два режима:
///   • Tools → Inventory → Spawn Inventory On Player (3D)
///       Главная камера получает HoverOutline + HoverPickup3D (Physics.Raycast).
///   • Tools → Inventory → Spawn Inventory On Player (2D)
///       Главная камера получает HoverPickup2D (Physics2D.OverlapPoint от мыши).
///       HoverOutline (3D-mesh подсветка) не добавляется.
///
/// Что общего:
///   1. Находит игрока (Tag="Player" или выбранный объект с PlayerController).
///   2. Создаёт дочерний 'InventoryRig' с InventoryManager + InventoryUI + WeaponHotbar(+UI).
///      Подключаемые модули (HungerSystem+HungerHUD, ThirstSystem+ThirstHUD) — управляются
///      настройками withHunger / withThirst. Систему здоровья включай через HealthSpawner.
///   3. Автозаполняет InventoryManager.itemDatabase всеми ItemDefinition-ассетами проекта.
///   4. Привязывает к InventoryUI.Layout ассет DefaultInventoryLayout (создаёт, если нет).
///   5. Помечает сцену dirty.
///
/// Запуск повторно идемпотентен — существующие компоненты переиспользуются, дубликатов не плодит.
/// </summary>
public static class InventorySpawner
{
    public enum Mode { ThreeD, TwoD }

    private const string MenuPath3D         = "Tools/Inventory/Spawn Inventory On Player (3D)";
    private const string MenuPath2D         = "Tools/Inventory/Spawn Inventory On Player (2D)";
    private const string PlayerTag          = "Player";
    private const string RigChildName       = "InventoryRig";
    private const string LayoutFolder       = "Assets/GAME/Inventory";
    private const string LayoutAssetPath    = "Assets/GAME/Inventory/DefaultInventoryLayout.asset";

    [MenuItem(MenuPath3D)] public static void Spawn3D() => Spawn(Mode.ThreeD);
    [MenuItem(MenuPath2D)] public static void Spawn2D() => Spawn(Mode.TwoD);

    /// <summary>
    /// Спавн с настройкой подключаемых модулей. По умолчанию hunger и thirst подключены.
    /// </summary>
    public static void Spawn(Mode mode) => Spawn(mode, withHunger: true, withThirst: true);

    public static void Spawn(Mode mode, bool withHunger, bool withThirst)
    {
        var player = FindPlayer();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Inventory Spawner",
                "Не найден игрок. Убедись, что у GameObject игрока стоит Tag='Player', " +
                "или выдели его в Hierarchy и запусти команду ещё раз.", "OK");
            return;
        }

        var report = new List<string> { $"Mode: {(mode == Mode.ThreeD ? "3D" : "2D")}" };
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName($"Spawn Inventory On Player ({(mode == Mode.ThreeD ? "3D" : "2D")})");
        int undoGroup = Undo.GetCurrentGroup();

        // 1. Rig (child)
        var rig = GetOrCreateChild(player, RigChildName, out bool rigCreated);
        report.Add(rigCreated ? $"+ Создан {RigChildName} (child of {player.name})"
                              : $"= Использован существующий {RigChildName}");

        // 2. InventoryManager
        var manager = GetOrAddComponent<InventoryManager>(rig, out bool mgrCreated);
        var dbCount = PopulateItemDatabase(manager);
        report.Add(mgrCreated ? $"+ InventoryManager добавлен (itemDatabase: {dbCount})"
                              : $"= InventoryManager уже был (itemDatabase обновлён: {dbCount})");

        // 3. InventoryUI
        var ui = GetOrAddComponent<InventoryUI>(rig, out bool uiCreated);
        var layout = LoadOrCreateLayout();
        if (ui.Layout != layout)
        {
            var so = new SerializedObject(ui);
            so.FindProperty("Layout").objectReferenceValue = layout;
            so.ApplyModifiedProperties();
        }
        report.Add(uiCreated ? $"+ InventoryUI добавлен (Layout: {layout.name})"
                             : $"= InventoryUI уже был (Layout: {layout.name})");

        // 3.5 Менеджер player-модулей — собирает все IPlayerModule с этого GameObject.
        var moduleMgr = GetOrAddComponent<PlayerModuleManager>(rig, out bool mmCreated);
        report.Add(mmCreated ? "+ PlayerModuleManager добавлен"
                             : "= PlayerModuleManager уже был");

        // 3.6 Подключаемые модули: голод и жажда. Каждый = пара System + HUD.
        HungerSystem hunger = null; HungerHUD hungerHud = null;
        if (withHunger)
        {
            hunger    = GetOrAddComponent<HungerSystem>(rig, out bool huCreated);
            hungerHud = GetOrAddComponent<HungerHUD>(rig,   out bool huHudCreated);
            report.Add(huCreated     ? "+ HungerSystem добавлен" : "= HungerSystem уже был");
            report.Add(huHudCreated  ? "+ HungerHUD добавлен"    : "= HungerHUD уже был");
        }
        else report.Add("- Hunger пропущен (withHunger=false)");

        ThirstSystem thirst = null; ThirstHUD thirstHud = null;
        if (withThirst)
        {
            thirst    = GetOrAddComponent<ThirstSystem>(rig, out bool thCreated);
            thirstHud = GetOrAddComponent<ThirstHUD>(rig,    out bool thHudCreated);
            report.Add(thCreated    ? "+ ThirstSystem добавлен" : "= ThirstSystem уже был");
            report.Add(thHudCreated ? "+ ThirstHUD добавлен"    : "= ThirstHUD уже был");
        }
        else report.Add("- Thirst пропущен (withThirst=false)");

        // 3.6 WeaponHotbar + WeaponHotbarUI
        var hotbar = GetOrAddComponent<WeaponHotbar>(rig, out bool hotbarCreated);
        report.Add(hotbarCreated
            ? "+ WeaponHotbar добавлен (reservedPrefab нужно назначить вручную в InventoryConfig)"
            : "= WeaponHotbar уже был");

        var hotbarUI = GetOrAddComponent<WeaponHotbarUI>(rig, out bool hotbarUICreated);
        report.Add(hotbarUICreated ? "+ WeaponHotbarUI добавлен (панель внизу экрана)"
                                   : "= WeaponHotbarUI уже был");

        EditorUtility.SetDirty(manager);
        EditorUtility.SetDirty(ui);
        EditorUtility.SetDirty(moduleMgr);
        if (hunger    != null) EditorUtility.SetDirty(hunger);
        if (hungerHud != null) EditorUtility.SetDirty(hungerHud);
        if (thirst    != null) EditorUtility.SetDirty(thirst);
        if (thirstHud != null) EditorUtility.SetDirty(thirstHud);
        EditorUtility.SetDirty(hotbar);
        EditorUtility.SetDirty(hotbarUI);
        EditorUtility.SetDirty(rig);

        // 4. Камера: 2D/3D-варианты подбора
        var cam = Camera.main;
        if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            if (mode == Mode.ThreeD)
            {
                EnsureCameraComponent<HoverOutline>(cam.gameObject,   "HoverOutline",   report);
                EnsureCameraComponent<HoverPickup3D>(cam.gameObject,  "HoverPickup3D",  report);
            }
            else // 2D
            {
                // HoverOutline в 2D неактуален (он рисует обводку 3D-меша). Пропускаем.
                EnsureCameraComponent<HoverPickup2D>(cam.gameObject, "HoverPickup2D", report);
            }
            EditorUtility.SetDirty(cam.gameObject);
        }
        else
        {
            report.Add("! Камера не найдена — модули камеры не добавлены");
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(rig.scene);

        string summary = string.Join("\n", report);
        Debug.Log("[InventorySpawner]\n" + summary);
        EditorUtility.DisplayDialog("Inventory Spawner", summary +
            "\n\nГотово. Не забудь сохранить сцену (Ctrl+S).", "OK");
    }

    // ── Per-module add/remove menu items ──────────────────────────────────────

    [MenuItem("Tools/Inventory/Modules/Add Hunger Module")]
    private static void AddHungerMenu()    => ToggleModule<HungerSystem, HungerHUD>(add: true,  label: "Hunger");
    [MenuItem("Tools/Inventory/Modules/Remove Hunger Module")]
    private static void RemoveHungerMenu() => ToggleModule<HungerSystem, HungerHUD>(add: false, label: "Hunger");

    [MenuItem("Tools/Inventory/Modules/Add Thirst Module")]
    private static void AddThirstMenu()    => ToggleModule<ThirstSystem, ThirstHUD>(add: true,  label: "Thirst");
    [MenuItem("Tools/Inventory/Modules/Remove Thirst Module")]
    private static void RemoveThirstMenu() => ToggleModule<ThirstSystem, ThirstHUD>(add: false, label: "Thirst");

    [MenuItem("Tools/Inventory/Modules/Add Health Module")]
    private static void AddHealthMenu()    => ToggleModule<HealthSystem, HealthHUD>(add: true,  label: "Health");
    [MenuItem("Tools/Inventory/Modules/Remove Health Module")]
    private static void RemoveHealthMenu() => ToggleModule<HealthSystem, HealthHUD>(add: false, label: "Health");

    private static void ToggleModule<TSystem, THud>(bool add, string label)
        where TSystem : Component
        where THud    : Component
    {
        var player = FindPlayer();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Inventory Modules",
                "Не найден игрок. Сначала запусти Spawn Inventory On Player.", "OK");
            return;
        }
        var rig = FindChild(player, RigChildName);
        if (rig == null)
        {
            EditorUtility.DisplayDialog("Inventory Modules",
                $"Не найден {RigChildName} под игроком. Сначала запусти Spawn Inventory On Player.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName($"{(add ? "Add" : "Remove")} {label} Module");

        if (add)
        {
            if (rig.GetComponent<TSystem>() == null) Undo.AddComponent<TSystem>(rig);
            if (rig.GetComponent<THud>()    == null) Undo.AddComponent<THud>(rig);
            Debug.Log($"[InventorySpawner] {label} module добавлен");
        }
        else
        {
            var sys = rig.GetComponent<TSystem>(); if (sys != null) Undo.DestroyObjectImmediate(sys);
            var hud = rig.GetComponent<THud>();    if (hud != null) Undo.DestroyObjectImmediate(hud);
            Debug.Log($"[InventorySpawner] {label} module удалён");
        }

        EditorSceneManager.MarkSceneDirty(rig.scene);
    }

    private static GameObject FindChild(GameObject parent, string name)
    {
        for (int i = 0; i < parent.transform.childCount; i++)
        {
            var t = parent.transform.GetChild(i);
            if (t.name == name) return t.gameObject;
        }
        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static GameObject FindPlayer()
    {
        var byTag = GameObject.FindGameObjectWithTag(PlayerTag);
        if (byTag != null) return byTag;

        if (Selection.activeGameObject != null &&
            Selection.activeGameObject.GetComponent<PlayerController>() != null)
            return Selection.activeGameObject;

        var pc = Object.FindFirstObjectByType<PlayerController>();
        return pc != null ? pc.gameObject : null;
    }

    private static GameObject GetOrCreateChild(GameObject parent, string name, out bool created)
    {
        for (int i = 0; i < parent.transform.childCount; i++)
        {
            var t = parent.transform.GetChild(i);
            if (t.name == name) { created = false; return t.gameObject; }
        }
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        Undo.SetTransformParent(go.transform, parent.transform, "Parent " + name);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;
        created = true;
        return go;
    }

    private static T GetOrAddComponent<T>(GameObject go, out bool added) where T : Component
    {
        var existing = go.GetComponent<T>();
        if (existing != null) { added = false; return existing; }
        added = true;
        return Undo.AddComponent<T>(go);
    }

    private static void EnsureCameraComponent<T>(GameObject go, string label, List<string> report) where T : Component
    {
        if (go.GetComponent<T>() != null)
        {
            report.Add($"= {label} уже был на камере '{go.name}'");
            return;
        }
        Undo.AddComponent<T>(go);
        report.Add($"+ {label} добавлен на камеру '{go.name}'");
    }

    private static int PopulateItemDatabase(InventoryManager mgr)
    {
        var guids = AssetDatabase.FindAssets("t:ItemDefinition");
        var list = new List<ItemDefinition>(guids.Length);
        foreach (var g in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            var def  = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
            if (def != null) list.Add(def);
        }
        var so = new SerializedObject(mgr);
        var prop = so.FindProperty("itemDatabase");
        prop.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = list[i];
        so.ApplyModifiedProperties();
        return list.Count;
    }

    private static InventoryLayout LoadOrCreateLayout()
    {
        var existing = AssetDatabase.LoadAssetAtPath<InventoryLayout>(LayoutAssetPath);
        if (existing != null) return existing;

        var guids = AssetDatabase.FindAssets("t:InventoryLayout");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<InventoryLayout>(path);
        }

        EnsureFolder(LayoutFolder);
        var layout = ScriptableObject.CreateInstance<InventoryLayout>();
        AssetDatabase.CreateAsset(layout, LayoutAssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[InventorySpawner] Создан дефолтный лейаут: " + LayoutAssetPath);
        return layout;
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
