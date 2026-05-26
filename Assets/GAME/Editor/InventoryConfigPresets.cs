using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-утилита: генерирует готовые пресеты <see cref="InventoryConfig"/> для типовых жанров.
///
///   Tools → Inventory → Create Config Preset → Tarkov-like
///   Tools → Inventory → Create Config Preset → Diablo-like
///   Tools → Inventory → Create Config Preset → Minecraft-like
///
/// Сохраняет .asset в Assets/GAME/Inventory/Configs/, можно тут же привязать к
/// InventoryManager.config через инспектор.
/// </summary>
public static class InventoryConfigPresets
{
    private const string ConfigsFolder = "Assets/GAME/Inventory/Configs";

    // ── Tarkov-like ──────────────────────────────────────────────────────────

    [MenuItem("Tools/Inventory/Create Config Preset/Tarkov-like")]
    public static void CreateTarkovLike()
    {
        var c = InventoryConfig.CreateDefault();
        SaveAndPing(c, "InventoryConfig_TarkovLike");
    }

    // ── Diablo-like ──────────────────────────────────────────────────────────

    [MenuItem("Tools/Inventory/Create Config Preset/Diablo-like")]
    public static void CreateDiabloLike()
    {
        var c = ScriptableObject.CreateInstance<InventoryConfig>();

        // Карманов нет. Основной инвентарь — большой backpack-grid 10×4 (виден после Equip).
        c.pockets = new List<PocketEntry>();

        c.equipment = new List<EquipmentSlotEntry>
        {
            new() { type = EquipmentSlotType.Backpack,        accepts = ItemType.Backpack },
            new() { type = EquipmentSlotType.BodyArmor,       accepts = ItemType.BodyArmor },
            new() { type = EquipmentSlotType.Helmet,          accepts = ItemType.Helmet },
            new() { type = EquipmentSlotType.PrimaryWeapon,   accepts = ItemType.PrimaryWeapon },
            new() { type = EquipmentSlotType.SecondaryWeapon, accepts = ItemType.SecondaryWeapon },
        };

        // Хотбар: 8 пользовательских слотов (зелья и т.п.).
        c.hotbar = new HotbarConfig
        {
            slots = new List<HotbarSlotEntry>
            {
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
            },
            reservedHotkey       = KeyCode.None,
            reservedHotkeyTarget = 0,
        };

        SaveAndPing(c, "InventoryConfig_DiabloLike");
    }

    // ── Minecraft-like ───────────────────────────────────────────────────────

    [MenuItem("Tools/Inventory/Create Config Preset/Minecraft-like")]
    public static void CreateMinecraftLike()
    {
        var c = ScriptableObject.CreateInstance<InventoryConfig>();

        // Один основной "карман" 9×3 — главный инвентарь. И один 9×1 — отдельный hotbar-grid,
        // если игра требует drag-and-drop между ними. Здесь упрощение: один pocket 9×3.
        c.pockets = new List<PocketEntry>
        {
            new() { id = "MainInventory", width = 9, height = 3 },
        };

        // Экипировка: только шлем и нагрудник (как в Minecraft armor slots).
        c.equipment = new List<EquipmentSlotEntry>
        {
            new() { type = EquipmentSlotType.Helmet,    accepts = ItemType.Helmet },
            new() { type = EquipmentSlotType.BodyArmor, accepts = ItemType.BodyArmor },
        };

        // Хотбар: 9 user-слотов, ничего зарезервированного.
        c.hotbar = new HotbarConfig
        {
            slots = new List<HotbarSlotEntry>
            {
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
                new() { kind = HotbarSlotKind.UserItem },
            },
            reservedHotkey       = KeyCode.None,
            reservedHotkeyTarget = 0,
        };

        SaveAndPing(c, "InventoryConfig_MinecraftLike");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void SaveAndPing(InventoryConfig c, string name)
    {
        EnsureFolder(ConfigsFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{ConfigsFolder}/{name}.asset");
        AssetDatabase.CreateAsset(c, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var asset = AssetDatabase.LoadAssetAtPath<InventoryConfig>(path);
        Selection.activeObject = asset;
        EditorGUIUtility.PingObject(asset);
        Debug.Log($"[InventoryConfigPresets] Создан: {path}");
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
