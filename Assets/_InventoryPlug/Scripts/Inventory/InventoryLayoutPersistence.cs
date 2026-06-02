using UnityEngine;

/// <summary>
/// Загрузка / сохранение <see cref="InventoryLayoutCustomization"/> в JSON через
/// <see cref="InventoryPersistence.Provider"/>. По умолчанию провайдер пишет
/// в <c>Application.persistentDataPath/inventory_layout.json</c>.
/// </summary>
public static class InventoryLayoutPersistence
{
    /// <summary>Возвращает сохранённый кастом или null, если слота нет / он битый.</summary>
    public static InventoryLayoutCustomization Load()
    {
        try
        {
            var json = InventoryPersistence.Provider.Read(InventoryPersistence.LayoutKey);
            if (string.IsNullOrWhiteSpace(json)) return null;
            return JsonUtility.FromJson<InventoryLayoutCustomization>(json);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[InventoryLayoutPersistence] Не удалось прочитать layout: {ex.Message}");
            return null;
        }
    }

    public static void Save(InventoryLayoutCustomization customization)
    {
        if (customization == null) return;
        try
        {
            var json = JsonUtility.ToJson(customization, prettyPrint: true);
            InventoryPersistence.Provider.Write(InventoryPersistence.LayoutKey, json);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[InventoryLayoutPersistence] Не удалось сохранить layout: {ex.Message}");
        }
    }

    public static void Delete() => InventoryPersistence.Provider.Delete(InventoryPersistence.LayoutKey);
}
