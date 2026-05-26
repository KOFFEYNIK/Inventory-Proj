using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GridUI : MonoBehaviour
{
    // ВНИМАНИЕ: было const, переведено в static, чтобы LayoutEditMode мог
    // менять размер ячейки в рантайме. При смене значения требуется пересоздать
    // все GridUI — это делает InventoryUI.RebuildInventoryPanel().
    public static float CellSize = 50f;
    public static float CellGap  = 2f;

    public GridContainer Container { get; private set; }

    [HideInInspector] public RectTransform cellsParent;
    [HideInInspector] public RectTransform itemsParent;

    private Image[,] cells;
    private readonly Dictionary<string, ItemUI> itemUIs = new();

    public static readonly Color ColNormal  = new(0.14f, 0.14f, 0.14f, 1f);
    public static readonly Color ColOk      = new(0.10f, 0.55f, 0.10f, 0.85f);
    public static readonly Color ColBlocked = new(0.55f, 0.10f, 0.10f, 0.85f);

    private static Font s_Font;
    private static Font GetFont()
    {
        if (s_Font != null) return s_Font;
        s_Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (s_Font == null) s_Font = Font.CreateDynamicFontFromOSFont("Arial", 12);
        return s_Font;
    }

    // ── Init ─────────────────────────────────────────────────────────────────

    public void Init(GridContainer container)
    {
        Container = container;
        BuildGrid();
        Refresh();
    }

    private void BuildGrid()
    {
        foreach (Transform t in cellsParent) Destroy(t.gameObject);
        foreach (Transform t in itemsParent) Destroy(t.gameObject);
        itemUIs.Clear();

        int  w      = Container.width;
        int  h      = Container.height;
        float totalW = w * CellSize + (w - 1) * CellGap;
        float totalH = h * CellSize + (h - 1) * CellGap;

        // Only set sizeDelta on the grid root — anchor/pivot are set externally
        // by CreateGridUI and must not be overridden here.
        GetComponent<RectTransform>().sizeDelta = new Vector2(totalW, totalH);
        SetSize(cellsParent,  totalW, totalH);
        SetSize(itemsParent,  totalW, totalH);

        // Background behind cells acts as grid line colour
        var bg = cellsParent.GetComponent<Image>();
        if (bg == null) bg = cellsParent.gameObject.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.06f, 1f);
        bg.raycastTarget = false;

        cells = new Image[w, h];
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var go  = new GameObject($"C{x}_{y}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(cellsParent, false);

                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot     = Vector2.zero;
                rt.sizeDelta = new Vector2(CellSize, CellSize);
                rt.anchoredPosition = CellToPixel(new Vector2Int(x, y));

                var img = go.GetComponent<Image>();
                img.color = ColNormal;
                cells[x, y] = img;
            }
        }
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    public void Refresh()
    {
        if (Container == null) return;
        ResetCellColors();

        foreach (Transform t in itemsParent) Destroy(t.gameObject);
        itemUIs.Clear();

        foreach (var (item, pos) in Container.GetAllItems())
            SpawnItemUI(item, pos);
    }

    public void SpawnItemUI(ItemInstance item, Vector2Int pos)
    {
        float iw = item.CurrentWidth  * CellSize + (item.CurrentWidth  - 1) * CellGap;
        float ih = item.CurrentHeight * CellSize + (item.CurrentHeight - 1) * CellGap;

        var go = new GameObject($"Item_{item.definition.displayName}", typeof(RectTransform), typeof(Image), typeof(ItemUI));
        go.transform.SetParent(itemsParent, false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot     = Vector2.zero;
        rt.sizeDelta = new Vector2(iw, ih);
        rt.anchoredPosition = CellToPixel(pos);

        var img = go.GetComponent<Image>();
        img.color = item.definition.isContainer
            ? new Color(0.20f, 0.30f, 0.40f, 0.95f)
            : new Color(0.28f, 0.38f, 0.28f, 0.95f);
        if (item.definition.icon != null) { img.sprite = item.definition.icon; img.type = Image.Type.Simple; }

        // Label
        CreateLabel(go.transform, item.definition.displayName,
                    item.CurrentWidth == 1 && item.CurrentHeight == 1 ? 9 : 11);

        var ui = go.GetComponent<ItemUI>();
        ui.Init(item, this, Container);
        itemUIs[item.instanceId] = ui;
    }

    // ── Highlight ─────────────────────────────────────────────────────────────

    public void HighlightCells(Vector2Int origin, int w, int h, bool canPlace)
    {
        ResetCellColors();
        Color col = canPlace ? ColOk : ColBlocked;
        for (int x = origin.x; x < origin.x + w; x++)
            for (int y = origin.y; y < origin.y + h; y++)
                if (x >= 0 && y >= 0 && x < Container.width && y < Container.height)
                    cells[x, y].color = col;
    }

    public void ResetCellColors()
    {
        if (cells == null) return;
        foreach (var c in cells) if (c != null) c.color = ColNormal;
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    public Vector2 CellToPixel(Vector2Int cell) =>
        new(cell.x * (CellSize + CellGap), cell.y * (CellSize + CellGap));

    public Vector2Int PixelToCell(Vector2 local)
    {
        int x = Mathf.FloorToInt(local.x / (CellSize + CellGap));
        int y = Mathf.FloorToInt(local.y / (CellSize + CellGap));
        return new Vector2Int(x, y);
    }

    // ── Util ──────────────────────────────────────────────────────────────────

    private static void SetSize(RectTransform rt, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot     = Vector2.zero;
        rt.sizeDelta = new Vector2(w, h);
    }

    private static void CreateLabel(Transform parent, string text, int fontSize)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var t = go.GetComponent<Text>();
        t.font      = GetFont();
        t.text      = text;
        t.fontSize  = fontSize;
        t.alignment = TextAnchor.LowerLeft;
        t.color     = Color.white;
        t.raycastTarget = false;
    }
}
