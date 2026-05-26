using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GridContainer
{
    public string containerId;
    public int    width;
    public int    height;
    public float  maxWeight; // 0 = unlimited

    private string[,] grid;
    private readonly Dictionary<string, (ItemInstance item, Vector2Int pos)> placed = new();

    public float TotalWeight
    {
        get { float w = 0f; foreach (var kv in placed) w += kv.Value.item.TotalWeight; return w; }
    }

    public GridContainer() { }

    public GridContainer(int w, int h, float maxW = 0f)
    {
        containerId = Guid.NewGuid().ToString();
        width    = w;
        height   = h;
        maxWeight = maxW;
        grid = new string[w, h];
    }

    public void InitGrid() => grid = new string[width, height];

    // ── Queries ──────────────────────────────────────────────────────────────

    public bool CanPlace(ItemInstance item, Vector2Int pos)
    {
        // Reject inserting a container into itself or anywhere inside its own nested tree.
        if (item.nestedContainer != null && IsContainedWithin(this, item.nestedContainer))
            return false;

        int iw = item.CurrentWidth, ih = item.CurrentHeight;
        if (pos.x < 0 || pos.y < 0 || pos.x + iw > width || pos.y + ih > height)
            return false;

        for (int x = pos.x; x < pos.x + iw; x++)
            for (int y = pos.y; y < pos.y + ih; y++)
                if (grid[x, y] != null && grid[x, y] != item.instanceId)
                    return false;

        if (maxWeight > 0f)
        {
            float alreadyHere = placed.ContainsKey(item.instanceId) ? item.TotalWeight : 0f;
            if (TotalWeight - alreadyHere + item.TotalWeight > maxWeight) return false;
        }
        return true;
    }

    private static bool IsContainedWithin(GridContainer target, GridContainer ancestor)
    {
        if (ancestor == null) return false;
        if (target == ancestor) return true;
        foreach (var kv in ancestor.placed)
        {
            var nested = kv.Value.item.nestedContainer;
            if (nested != null && IsContainedWithin(target, nested)) return true;
        }
        return false;
    }

    public bool TryGetPosition(ItemInstance item, out Vector2Int pos)
    {
        if (placed.TryGetValue(item.instanceId, out var d)) { pos = d.pos; return true; }
        pos = default; return false;
    }

    public ItemInstance GetItemAt(int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height) return null;
        string id = grid[x, y];
        return id != null && placed.TryGetValue(id, out var d) ? d.item : null;
    }

    public List<(ItemInstance item, Vector2Int pos)> GetAllItems()
    {
        var list = new List<(ItemInstance, Vector2Int)>();
        foreach (var kv in placed) list.Add(kv.Value);
        return list;
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    public bool TryPlace(ItemInstance item, Vector2Int pos)
    {
        if (!CanPlace(item, pos)) return false;
        if (placed.ContainsKey(item.instanceId)) ClearCells(item, placed[item.instanceId].pos);
        FillCells(item, pos);
        placed[item.instanceId] = (item, pos);
        return true;
    }

    public void Remove(ItemInstance item)
    {
        if (!placed.TryGetValue(item.instanceId, out var d)) return;
        ClearCells(item, d.pos);
        placed.Remove(item.instanceId);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void FillCells(ItemInstance item, Vector2Int pos)
    {
        for (int x = pos.x; x < pos.x + item.CurrentWidth; x++)
            for (int y = pos.y; y < pos.y + item.CurrentHeight; y++)
                grid[x, y] = item.instanceId;
    }

    private void ClearCells(ItemInstance item, Vector2Int pos)
    {
        for (int x = pos.x; x < pos.x + item.CurrentWidth; x++)
            for (int y = pos.y; y < pos.y + item.CurrentHeight; y++)
                if (grid[x, y] == item.instanceId) grid[x, y] = null;
    }
}
