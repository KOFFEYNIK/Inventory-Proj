using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Менеджер player-модулей. Висит на InventoryRig вместе с конкретными системами
/// (<see cref="HungerSystem"/>, <see cref="ThirstSystem"/>, <see cref="HealthSystem"/>
/// и любыми пользовательскими, реализующими <see cref="IPlayerModule"/>).
///
/// В <c>Awake</c> собирает все компоненты-модули с того же GameObject и предоставляет
/// единый lookup через <see cref="Get{T}"/>. Сами модули продолжают жить как
/// независимые MonoBehaviour'ы — менеджер не управляет их жизненным циклом, только
/// агрегирует.
///
/// <para>DefaultExecutionOrder = -95: после <see cref="InventoryManager"/> (-100), но
/// до самих модулей (-90), чтобы успеть подцепить их по факту наличия.</para>
/// </summary>
[DefaultExecutionOrder(-95)]
public class PlayerModuleManager : MonoBehaviour
{
    public static PlayerModuleManager Instance { get; private set; }

    [Tooltip("Runtime-список обнаруженных модулей. Заполняется автоматически в Awake.")]
    [SerializeField] private List<string> detectedModules = new();

    private readonly List<IPlayerModule> modules = new();

    /// <summary>Все обнаруженные модули.</summary>
    public IReadOnlyList<IPlayerModule> Modules => modules;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        CollectSiblingModules();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Заново перечитывает все компоненты-модули с этого GameObject. Полезно после
    /// runtime-добавления нового модуля (хотя обычно их добавляют в эдиторе до Play).
    /// </summary>
    public void RebuildRegistry()
    {
        CollectSiblingModules();
    }

    private void CollectSiblingModules()
    {
        modules.Clear();
        detectedModules.Clear();

        var found = GetComponents<IPlayerModule>();
        foreach (var m in found)
        {
            modules.Add(m);
            detectedModules.Add(m.ModuleName);
        }
    }

    /// <summary>
    /// Найти модуль по интерфейсу или типу. Возвращает null, если не подключён.
    /// Пример: <c>manager.Get&lt;IHungerTarget&gt;()</c>.
    /// </summary>
    public T Get<T>() where T : class
    {
        for (int i = 0; i < modules.Count; i++)
        {
            if (modules[i] is T typed) return typed;
        }
        return null;
    }

    public bool Has<T>() where T : class => Get<T>() != null;
}
