using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Одна ячейка хотбара. Является drop-target для drag-drop из инвентаря
/// (только для пользовательских слотов 5-0). ЛКМ = выбрать слот, ПКМ по
/// пользовательской ячейке = очистить.
/// </summary>
public class WeaponHotbarSlotUI : MonoBehaviour, IPointerClickHandler
{
    public int SlotIndex { get; private set; }

    private Image       background;
    private Image       iconImage;
    private GameObject  iconGo;
    private Text        nameLabel;
    private Text        keyLabel;
    private GameObject  activeFrame;
    private GameObject  stackBadge;
    private Text        stackText;
    private Font        font;

    private static readonly Color BgEmptyColor   = new(0.10f, 0.10f, 0.10f, 0.95f);
    private static readonly Color BgItemColor    = new(0.06f, 0.06f, 0.06f, 0.95f);
    private static readonly Color BgReservedColor = new(0.13f, 0.10f, 0.10f, 0.95f);
    private static readonly Color BgHighlightOK  = new(0.10f, 0.55f, 0.10f, 0.85f);
    private static readonly Color BgHighlightBad = new(0.55f, 0.10f, 0.10f, 0.85f);

    public void Init(int slotIndex, Font fontRef)
    {
        SlotIndex = slotIndex;
        font = fontRef;
        BuildVisuals();
    }

    private void BuildVisuals()
    {
        background = GetComponent<Image>();
        if (background == null) background = gameObject.AddComponent<Image>();
        background.color = BgEmptyColor;
        background.raycastTarget = true;

        // Key hint (1..9, 0) in top-left
        var keyGo = new GameObject("Key", typeof(RectTransform), typeof(Text));
        keyGo.transform.SetParent(transform, false);
        var krt = keyGo.GetComponent<RectTransform>();
        krt.anchorMin = new Vector2(0f, 1f);
        krt.anchorMax = new Vector2(0f, 1f);
        krt.pivot     = new Vector2(0f, 1f);
        krt.anchoredPosition = new Vector2(4f, -2f);
        krt.sizeDelta = new Vector2(20f, 14f);
        keyLabel = keyGo.GetComponent<Text>();
        keyLabel.font      = font;
        keyLabel.fontSize  = 11;
        keyLabel.alignment = TextAnchor.UpperLeft;
        keyLabel.color     = new Color(1f, 1f, 1f, 0.85f);
        keyLabel.text      = WeaponHotbar.GetKeyLabel(SlotIndex);
        keyLabel.raycastTarget = false;

        // Active frame (border-only overlay)
        activeFrame = new GameObject("ActiveFrame", typeof(RectTransform));
        activeFrame.transform.SetParent(transform, false);
        var art = activeFrame.GetComponent<RectTransform>();
        art.anchorMin = Vector2.zero; art.anchorMax = Vector2.one;
        art.offsetMin = art.offsetMax = Vector2.zero;
        BuildBorder(activeFrame.transform);
        activeFrame.SetActive(false);

        // Name label (bottom, мини-подпись)
        var nameGo = new GameObject("Name", typeof(RectTransform), typeof(Text));
        nameGo.transform.SetParent(transform, false);
        var nrt = nameGo.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0f, 0f);
        nrt.anchorMax = new Vector2(1f, 0f);
        nrt.pivot     = new Vector2(0.5f, 0f);
        nrt.anchoredPosition = new Vector2(0f, 2f);
        nrt.sizeDelta = new Vector2(0f, 12f);
        nameLabel = nameGo.GetComponent<Text>();
        nameLabel.font      = font;
        nameLabel.fontSize  = 9;
        nameLabel.alignment = TextAnchor.LowerCenter;
        nameLabel.color     = new Color(0.85f, 0.85f, 0.85f, 0.9f);
        nameLabel.raycastTarget = false;
        nameLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    private void BuildBorder(Transform parent)
    {
        var color = new Color(0.95f, 0.85f, 0.20f, 1f);
        const float thickness = 2f;
        // top
        AddBorderEdge(parent, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, thickness));
        // bottom
        AddBorderEdge(parent, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, thickness));
        // left
        AddBorderEdge(parent, color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(thickness, 0f));
        // right
        AddBorderEdge(parent, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(thickness, 0f));
    }

    private static void AddBorderEdge(Transform parent, Color color, Vector2 aMin, Vector2 aMax, Vector2 size)
    {
        var go = new GameObject("Edge", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        if (size.x > 0f && size.y > 0f) rt.sizeDelta = size;
        else if (size.x > 0f) rt.sizeDelta = new Vector2(size.x, 0f);
        else                  rt.sizeDelta = new Vector2(0f, size.y);
        rt.anchoredPosition = Vector2.zero;
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    public void Refresh()
    {
        var hb = WeaponHotbar.Instance;
        if (hb == null) return;
        var slot = hb.Slots[SlotIndex];

        // Active highlight
        if (activeFrame != null) activeFrame.SetActive(hb.ActiveIndex == SlotIndex);

        // Reset
        if (iconGo != null) { Destroy(iconGo); iconGo = null; iconImage = null; }
        if (stackBadge != null) { Destroy(stackBadge); stackBadge = null; stackText = null; }
        if (nameLabel != null) nameLabel.text = string.Empty;
        background.color = BgEmptyColor;

        Sprite sprite = null;
        Color iconTint = Color.white;
        string label = string.Empty;
        int stack = 1;

        switch (slot.kind)
        {
            case HotbarSlotKind.Reserved:
                background.color = BgReservedColor;
                label = slot.reservedPrefab != null ? slot.reservedPrefab.name : "RESERVED";
                iconTint = new Color(0.85f, 0.85f, 0.85f, 1f);
                break;

            case HotbarSlotKind.EquipmentWeapon:
            {
                var item = hb.GetSlotItem(SlotIndex);
                background.color = BgReservedColor;
                if (item != null)
                {
                    sprite = item.definition.icon;
                    label  = item.definition.displayName;
                    stack  = item.stackCount;
                    background.color = BgItemColor;
                }
                else
                {
                    label = SlotReservedLabel(slot.equipmentSource);
                }
                break;
            }

            case HotbarSlotKind.UserItem:
            {
                var item = slot.userItem;
                if (item != null)
                {
                    sprite = item.definition.icon;
                    label  = item.definition.displayName;
                    stack  = item.stackCount;
                    background.color = BgItemColor;
                }
                break;
            }

            default:
                background.color = BgEmptyColor;
                break;
        }

        if (sprite != null || !string.IsNullOrEmpty(label))
        {
            iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(transform, false);
            var irt = iconGo.GetComponent<RectTransform>();
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(6f, 14f);
            irt.offsetMax = new Vector2(-6f, -16f);
            iconImage = iconGo.GetComponent<Image>();
            if (sprite != null)
            {
                iconImage.sprite = sprite;
                iconImage.preserveAspect = true;
                iconImage.color = iconTint;
            }
            else
            {
                // Без иконки — нейтральная заливка
                iconImage.color = new Color(0.28f, 0.38f, 0.28f, 0.7f);
            }
            iconImage.raycastTarget = false;
            // Поднять активный фрейм над иконкой
            if (activeFrame != null) activeFrame.transform.SetAsLastSibling();
        }

        if (nameLabel != null) nameLabel.text = label;

        if (stack > 1)
        {
            stackBadge = new GameObject("Stack", typeof(RectTransform), typeof(Text));
            stackBadge.transform.SetParent(transform, false);
            var srt = stackBadge.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(1f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot     = new Vector2(1f, 1f);
            srt.anchoredPosition = new Vector2(-4f, -2f);
            srt.sizeDelta = new Vector2(30f, 14f);
            stackText = stackBadge.GetComponent<Text>();
            stackText.font      = font;
            stackText.fontSize  = 11;
            stackText.alignment = TextAnchor.UpperRight;
            stackText.color     = Color.white;
            stackText.text      = "x" + stack;
            stackText.raycastTarget = false;
        }
    }

    public void SetHighlight(bool active, bool canPlace)
    {
        if (!active) { Refresh(); return; }
        background.color = canPlace ? BgHighlightOK : BgHighlightBad;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left)
        {
            WeaponHotbar.Instance?.SelectSlot(SlotIndex);
        }
        else if (e.button == PointerEventData.InputButton.Right)
        {
            var hb = WeaponHotbar.Instance;
            if (hb != null && hb.IsUserSlot(SlotIndex)) hb.ClearUserSlot(SlotIndex);
        }
    }

    private static string SlotReservedLabel(EquipmentSlotType? src) => src switch
    {
        EquipmentSlotType.PrimaryWeapon   => "ОСНОВНОЕ",
        EquipmentSlotType.SecondaryWeapon => "ДОП.",
        EquipmentSlotType.Holster         => "КОБУРА",
        _                                  => string.Empty
    };
}
