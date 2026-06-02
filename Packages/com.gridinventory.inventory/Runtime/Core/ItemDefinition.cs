using UnityEngine;
using UnityEngine.Serialization;

/// <summary>В каком виде предмет существует в мире: 3D (мешевый префаб с Rigidbody) или
/// 2D (спрайтовый префаб с Rigidbody2D). Влияет на дроп из инвентаря и на префаб,
/// который инстанциируется при <see cref="WorldItem3D"/>/<see cref="WorldItem2D"/>.Spawn.</summary>
public enum WorldMode { ThreeD, TwoD }

[CreateAssetMenu(fileName = "NewItem", menuName = "Inventory/Item Definition")]
public class ItemDefinition : ScriptableObject
{
    [Header("Identity")]
    public string itemId;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Type")]
    public ItemType itemType = ItemType.Generic;

    [Header("Grid Size")]
    public int width = 1;
    public int height = 1;
    public bool canRotate = true;

    [Header("Stack")]
    [Tooltip("Разрешено ли объединять одинаковые предметы в один стак. Если выключено — " +
             "каждый предмет занимает отдельную ячейку, maxStackSize игнорируется.")]
    public bool canStack = false;

    [Tooltip("Максимум предметов в одном стаке. Работает только при canStack = true.")]
    public int maxStackSize = 1;

    /// <summary>Эффективный предел стака: <see cref="maxStackSize"/> при <see cref="canStack"/>,
    /// иначе 1 (стакирование выключено).</summary>
    public int MaxStack => canStack ? Mathf.Max(1, maxStackSize) : 1;

    [Header("Weight")]
    public float weightPerUnit = 0.1f;

    [Header("Container")]
    public bool isContainer;
    public int containerWidth = 4;
    public int containerHeight = 3;
    public float containerMaxWeight = 0f; // 0 = unlimited

    [Header("World presentation (2D / 3D)")]
    [Tooltip("Режим существования предмета в мире.\n" +
             "• ThreeD — при дропе спавнится WorldItem3D с worldPrefab3D (мешевый префаб + Rigidbody).\n" +
             "• TwoD — при дропе спавнится WorldItem2D с worldPrefab2D (спрайтовый префаб + Rigidbody2D).\n" +
             "Окно осмотра в обоих случаях использует 3D-камеру, но префаб берётся под текущий режим.")]
    public WorldMode worldMode = WorldMode.ThreeD;

    [Tooltip("Префаб для 3D-мира. Используется при WorldMode = ThreeD. Должен содержать Mesh/Renderer; " +
             "Rigidbody добавится автоматически при дропе. Если null — fallback на цветной куб.")]
    [FormerlySerializedAs("worldPrefab")]
    public GameObject worldPrefab3D;

    [Tooltip("Префаб для 2D-мира. Используется при WorldMode = TwoD. Должен содержать SpriteRenderer и " +
             "Collider2D; Rigidbody2D добавится автоматически. Если null — fallback на спрайт из поля Icon.")]
    public GameObject worldPrefab2D;

    /// <summary>Префаб, который инстанциируется в мире под текущий <see cref="worldMode"/>.
    /// Используется внутри <see cref="WorldItem2D"/> / <see cref="WorldItem3D"/>.Spawn и в окне осмотра.</summary>
    public GameObject ActiveWorldPrefab => worldMode == WorldMode.TwoD ? worldPrefab2D : worldPrefab3D;

    [Header("Weapon")]
    [Tooltip("Префаб оружия, который активируется через хотбар. Если задан — предмет считается " +
             "оружием и может быть взят в руки. Конкретный компонент-носитель определяется проектом.")]
    public GameObject weaponPrefab;

    [Header("Effect")]
    [Tooltip("Если задано — пункт 'Использовать' активирует эффект. Конкретная игровая логика " +
             "(голод, жажда, лечение, бафы…) определяется типом эффекта-наследника ItemEffect.")]
    public ItemEffect consumable;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(itemId)) itemId = name;
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        maxStackSize = Mathf.Max(1, maxStackSize);
        weightPerUnit = Mathf.Max(0f, weightPerUnit);
        containerWidth = Mathf.Max(1, containerWidth);
        containerHeight = Mathf.Max(1, containerHeight);
        containerMaxWeight = Mathf.Max(0f, containerMaxWeight);
    }
}
