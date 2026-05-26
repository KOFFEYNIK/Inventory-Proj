using UnityEngine;

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
    public int maxStackSize = 1;

    [Header("Weight")]
    public float weightPerUnit = 0.1f;

    [Header("Container")]
    public bool isContainer;
    public int containerWidth = 4;
    public int containerHeight = 3;
    public float containerMaxWeight = 0f; // 0 = unlimited

    [Header("Visual")]
    [Tooltip("Prefab используется и при выбрасывании предмета в мир (WorldItem.Spawn), " +
             "и при осмотре в окне инвентаря. Если null — создаётся цветной куб по размеру предмета.")]
    public GameObject worldPrefab;

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
