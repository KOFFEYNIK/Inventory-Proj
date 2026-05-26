using UnityEngine;
using UnityEngine.UI;

public class EquipmentSlotUI : MonoBehaviour
{
    public EquipmentSlotType SlotType { get; private set; }
    public EquipmentSlot     Slot     { get; private set; }

    private float slotSize = 54f;

    private Image       background;
    private Text        emptyLabel;
    private GameObject  itemVisual;
    private ItemUI      itemUI;

    private static Font s_Font;
    private static Font GetFont()
    {
        if (s_Font != null) return s_Font;
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (s_Font == null) s_Font = Font.CreateDynamicFontFromOSFont("Arial", 12);
        return s_Font;
    }

    public void Init(EquipmentSlotType type, EquipmentSlot slot, float size, string labelText = null)
    {
        SlotType = type;
        Slot     = slot;
        slotSize = size;
        BuildVisuals(labelText ?? DefaultLabel(type));
        Refresh();
    }

    private void BuildVisuals(string label)
    {
        var rt = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(slotSize, slotSize);

        background = gameObject.GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        background.color = new Color(0.10f, 0.10f, 0.10f, 1f);
        background.raycastTarget = true;

        // Empty-state label
        var lblGo = new GameObject("Lbl", typeof(RectTransform), typeof(Text));
        lblGo.transform.SetParent(transform, false);
        var lblRt = lblGo.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = lblRt.offsetMax = Vector2.zero;

        emptyLabel = lblGo.GetComponent<Text>();
        emptyLabel.font      = GetFont();
        emptyLabel.text      = label;
        emptyLabel.fontSize  = 9;
        emptyLabel.alignment = TextAnchor.MiddleCenter;
        emptyLabel.color     = new Color(0.5f, 0.5f, 0.5f, 1f);
        emptyLabel.raycastTarget = false;
    }

    public void Refresh()
    {
        if (Slot == null) return;
        if (itemVisual != null) { Destroy(itemVisual); itemVisual = null; itemUI = null; }

        if (Slot.IsEmpty)
        {
            if (emptyLabel != null) emptyLabel.enabled = true;
            background.color = new Color(0.10f, 0.10f, 0.10f, 1f);
            return;
        }

        if (emptyLabel != null) emptyLabel.enabled = false;
        background.color = new Color(0.06f, 0.06f, 0.06f, 1f);

        itemVisual = new GameObject($"Equipped_{Slot.EquippedItem.definition.displayName}",
            typeof(RectTransform), typeof(Image), typeof(ItemUI));
        itemVisual.transform.SetParent(transform, false);

        var rt = itemVisual.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(2f, 2f);
        rt.offsetMax = new Vector2(-2f, -2f);

        var img = itemVisual.GetComponent<Image>();
        var def = Slot.EquippedItem.definition;
        img.color = def.isContainer
            ? new Color(0.20f, 0.30f, 0.40f, 0.95f)
            : new Color(0.28f, 0.38f, 0.28f, 0.95f);
        if (def.icon != null) { img.sprite = def.icon; img.type = Image.Type.Simple; }

        var lbl = new GameObject("Label", typeof(RectTransform), typeof(Text));
        lbl.transform.SetParent(itemVisual.transform, false);
        var lblRt = lbl.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = Vector2.one;
        lblRt.offsetMin = lblRt.offsetMax = Vector2.zero;
        var t = lbl.GetComponent<Text>();
        t.font      = GetFont();
        t.text      = def.displayName;
        t.fontSize  = 9;
        t.alignment = TextAnchor.LowerLeft;
        t.color     = Color.white;
        t.raycastTarget = false;

        itemUI = itemVisual.GetComponent<ItemUI>();
        itemUI.InitFromSlot(Slot.EquippedItem, this);
    }

    public void SetHighlight(bool canPlace, bool active)
    {
        if (!active) { background.color = Slot.IsEmpty ? new Color(0.10f, 0.10f, 0.10f, 1f)
                                                       : new Color(0.06f, 0.06f, 0.06f, 1f); return; }
        background.color = canPlace
            ? new Color(0.10f, 0.55f, 0.10f, 0.85f)
            : new Color(0.55f, 0.10f, 0.10f, 0.85f);
    }

    private static string DefaultLabel(EquipmentSlotType type) => type switch
    {
        EquipmentSlotType.Backpack        => "РЮКЗАК",
        EquipmentSlotType.Rig             => "РАЗГРУЗКА",
        EquipmentSlotType.SecureCase      => "КЕЙС",
        EquipmentSlotType.BodyArmor       => "БРОНЯ",
        EquipmentSlotType.Helmet          => "ШЛЕМ",
        EquipmentSlotType.FaceCover       => "МАСКА",
        EquipmentSlotType.PrimaryWeapon   => "ОСНОВНОЕ",
        EquipmentSlotType.SecondaryWeapon => "ДОП.",
        EquipmentSlotType.Holster         => "КОБУРА",
        _                                 => type.ToString()
    };
}
