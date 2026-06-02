using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-утилита: разворачивает систему здоровья на игроке в текущей сцене.
///
///   Tools → Health → Spawn Health On Player
///
/// Делает следующее:
///   1. Находит игрока (Tag="Player" → выбранный объект с PlayerController → первый PlayerController в сцене).
///   2. На игроке находит или создаёт дочерний GameObject 'InventoryRig' (тот же rig, который использует InventorySpawner —
///      HealthSystem и HealthHUD живут рядом с HungerSystem/HungerHUD/ThirstSystem/ThirstHUD).
///   3. Вешает на rig компоненты HealthSystem + HealthHUD.
///   4. Загружает или создаёт DefaultHealthConfig в Assets/GAME/Inventory/ и привязывает к HealthSystem.config.
///   5. Помечает сцену dirty.
///
/// Идемпотентно — повторный запуск переиспользует существующие компоненты и конфиг.
/// </summary>
public static class HealthSpawner
{
    private const string MenuPath           = "Tools/Health/Spawn Health On Player";
    private const string PlayerTag          = "Player";
    private const string RigChildName       = "InventoryRig";
    private const string ConfigFolder       = "Assets/GAME/Inventory";
    private const string ConfigAssetPath    = "Assets/GAME/Inventory/DefaultHealthConfig.asset";

    [MenuItem(MenuPath)]
    public static void Spawn()
    {
        var player = FindPlayer();
        if (player == null)
        {
            EditorUtility.DisplayDialog("Health Spawner",
                "Не найден игрок. Убедись, что у GameObject игрока стоит Tag='Player', " +
                "или выдели его в Hierarchy и запусти команду ещё раз.", "OK");
            return;
        }

        var report = new List<string>();
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Spawn Health On Player");
        int undoGroup = Undo.GetCurrentGroup();

        // 1. Rig (child)
        var rig = GetOrCreateChild(player, RigChildName, out bool rigCreated);
        report.Add(rigCreated ? $"+ Создан {RigChildName} (child of {player.name})"
                              : $"= Использован существующий {RigChildName}");

        // 1.5 PlayerModuleManager (агрегатор всех IPlayerModule на rig)
        var moduleMgr = GetOrAddComponent<PlayerModuleManager>(rig, out bool mmCreated);
        report.Add(mmCreated ? "+ PlayerModuleManager добавлен"
                             : "= PlayerModuleManager уже был");

        // 2. HealthSystem
        var hs = GetOrAddComponent<HealthSystem>(rig, out bool hsCreated);
        var config = LoadOrCreateConfig(out bool configCreated);
        if (hs.config != config)
        {
            var so = new SerializedObject(hs);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedProperties();
        }
        report.Add(hsCreated ? $"+ HealthSystem добавлен (config: {config.name})"
                             : $"= HealthSystem уже был (config: {config.name})");
        report.Add(configCreated ? $"+ Создан {ConfigAssetPath}"
                                 : $"= Использован существующий {config.name}");

        // 3. HealthHUD
        var hud = GetOrAddComponent<HealthHUD>(rig, out bool hudCreated);
        report.Add(hudCreated ? "+ HealthHUD добавлен (7 полосок, правый-верхний угол)"
                              : "= HealthHUD уже был");

        EditorUtility.SetDirty(moduleMgr);
        EditorUtility.SetDirty(hs);
        EditorUtility.SetDirty(hud);
        EditorUtility.SetDirty(rig);

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(rig.scene);

        string summary = string.Join("\n", report);
        Debug.Log("[HealthSpawner]\n" + summary);
        EditorUtility.DisplayDialog("Health Spawner", summary +
            "\n\nГотово. Не забудь сохранить сцену (Ctrl+S).", "OK");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static GameObject FindPlayer()
    {
        var byTag = GameObject.FindGameObjectWithTag(PlayerTag);
        if (byTag != null) return byTag;

        if (Selection.activeGameObject != null &&
            Selection.activeGameObject.GetComponent<PlayerController>() != null)
            return Selection.activeGameObject;

        var pc = Object.FindAnyObjectByType<PlayerController>();
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

    private static HealthConfig LoadOrCreateConfig(out bool created)
    {
        var existing = AssetDatabase.LoadAssetAtPath<HealthConfig>(ConfigAssetPath);
        if (existing != null) { created = false; return existing; }

        // Любой существующий HealthConfig подойдёт — берём первый
        var guids = AssetDatabase.FindAssets("t:HealthConfig");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            created = false;
            return AssetDatabase.LoadAssetAtPath<HealthConfig>(path);
        }

        // Создаём дефолтный
        EnsureFolder(ConfigFolder);
        var config = ScriptableObject.CreateInstance<HealthConfig>();
        AssetDatabase.CreateAsset(config, ConfigAssetPath);
        AssetDatabase.SaveAssets();
        created = true;
        return config;
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
