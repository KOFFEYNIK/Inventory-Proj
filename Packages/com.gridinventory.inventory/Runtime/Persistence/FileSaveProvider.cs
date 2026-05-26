using System.IO;
using UnityEngine;

/// <summary>
/// Дефолтная реализация <see cref="ISaveProvider"/>: пишет в JSON-файлы под
/// <see cref="Application.persistentDataPath"/>. Ключ маппится в файл &lt;key&gt;.json
/// (если ключ уже содержит ".", используется как есть).
///
/// Опционально можно задать поддиректорию (например, "inventory/") — удобно
/// группировать несколько слотов одного пакета.
/// </summary>
public class FileSaveProvider : ISaveProvider
{
    private readonly string root;
    private readonly string extension;

    /// <param name="subDirectory">Опциональная поддиректория под persistentDataPath. Может быть null/пустой.</param>
    /// <param name="extension">Расширение, добавляемое к ключу, если у ключа его нет. По умолчанию ".json".</param>
    public FileSaveProvider(string subDirectory = null, string extension = ".json")
    {
        this.extension = extension ?? "";
        root = string.IsNullOrEmpty(subDirectory)
            ? Application.persistentDataPath
            : Path.Combine(Application.persistentDataPath, subDirectory);
    }

    private string PathFor(string key)
    {
        string fileName = key.Contains(".") ? key : key + extension;
        return Path.Combine(root, fileName);
    }

    public void Write(string key, string contents)
    {
        try
        {
            if (!Directory.Exists(root)) Directory.CreateDirectory(root);
            File.WriteAllText(PathFor(key), contents ?? "");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[FileSaveProvider] Write '{key}' failed: {ex.Message}");
        }
    }

    public string Read(string key)
    {
        try
        {
            var path = PathFor(key);
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[FileSaveProvider] Read '{key}' failed: {ex.Message}");
            return null;
        }
    }

    public bool Exists(string key) => File.Exists(PathFor(key));

    public void Delete(string key)
    {
        try
        {
            var path = PathFor(key);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[FileSaveProvider] Delete '{key}' failed: {ex.Message}");
        }
    }
}
