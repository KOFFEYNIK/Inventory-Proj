/// <summary>
/// Абстракция для чтения и записи строкового слота сохранения (обычно — JSON).
/// Конкретные реализации: файл, PlayerPrefs, облачный сейв, in-memory (для тестов).
///
/// Ключи — короткие logical-имена ("inventory", "inventory_layout"), без расширений и путей.
/// Конкретная реализация сама решает, как маппить ключ в физический ресурс (файл, prefs key).
/// </summary>
public interface ISaveProvider
{
    /// <summary>Записать строку (JSON). Идемпотентно: перезаписывает существующий слот.</summary>
    void Write(string key, string contents);

    /// <summary>Прочитать слот. Возвращает null, если слот не существует или нечитаем.</summary>
    string Read(string key);

    bool Exists(string key);

    void Delete(string key);
}
