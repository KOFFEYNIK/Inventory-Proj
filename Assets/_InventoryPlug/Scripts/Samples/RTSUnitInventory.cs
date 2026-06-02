using UnityEngine;

/// <summary>
/// Компонент «инвентарь юнита» для тактических RPG / RTS-стиля (Baldur's Gate, X-COM и т.п.).
/// Создаёт собственный <see cref="InventoryManager"/> на дочернем GameObject —
/// исключительно как контейнер данных (без UI, без модулей, без DontDestroyOnLoad).
///
/// <para>
/// При выборе юнита через <see cref="RTSCommander"/> вызывается
/// <see cref="InventoryManager.SetActive"/> с этим менеджером — UI/хотбар/<see cref="Inventory.Service"/>
/// (а значит, и подбор предметов) переключаются на него.
/// </para>
///
/// <para>
/// Каждый юнит получает собственное хранилище (карманы, equipment-слоты, контейнеры) и
/// может иметь индивидуальный <see cref="InventoryConfig"/> (например, у Tank-а другой набор слотов,
/// у Scout-а — другой). Конфиг можно оставить пустым — тогда используется встроенный дефолт.
/// </para>
///
/// <para>
/// Расширение: подсистема плагин-агностична. Чтобы прицепить к нему свои фишки, подпишись
/// на <see cref="InventoryManager.OnActiveChanged"/> и читай <see cref="Manager"/>.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class RTSUnitInventory : MonoBehaviour
{
    [Header("Per-unit config")]
    [Tooltip("Опциональный override. Если null — используется дефолтный InventoryConfig.")]
    public InventoryConfig config;

    [Tooltip("База предметов, если есть Save/Load (для каждого юнита можно свою).")]
    public ItemDefinition[] itemDatabase;

    [Tooltip("Имя инвентаря для дебага. По умолчанию — имя GameObject.")]
    public string inventoryName;

    /// <summary>Созданный data-only менеджер. null до Awake.</summary>
    public InventoryManager Manager { get; private set; }

    void Awake()
    {
        BuildDataManager();
    }

    void OnDestroy()
    {
        // Если этот юнит был активным — снять «активность», чтобы UI не показывал
        // стейл-данные мёртвого менеджера. Само GO с Manager уничтожится Unity'ем
        // как ребёнок этого GameObject.
        if (Manager != null && InventoryManager.Instance == Manager)
            InventoryManager.SetActive(null);
    }

    private void BuildDataManager()
    {
        // Создаём дочерний GO в неактивном состоянии, чтобы выставить поля
        // ДО вызова InventoryManager.Awake (он работает на InitDefaultContainers).
        var go = new GameObject("UnitInventoryData");
        go.SetActive(false);
        go.transform.SetParent(transform, false);

        Manager = go.AddComponent<InventoryManager>();
        Manager.config                  = config;
        Manager.itemDatabase            = itemDatabase;
        Manager.isPlayerRig             = false;  // per-unit data-only, без DDOL и reparent
        Manager.registerAsActiveOnAwake = false;  // активным становится только через RTSCommander.Select
        Manager.inventoryName           = string.IsNullOrEmpty(inventoryName) ? name : inventoryName;

        go.SetActive(true); // теперь Awake запустится с правильными полями
    }

    /// <summary>Сделать инвентарь этого юнита активным. Эквивалент
    /// <see cref="InventoryManager.SetActive"/>(Manager).</summary>
    public void Activate()
    {
        if (Manager != null) InventoryManager.SetActive(Manager);
    }
}
