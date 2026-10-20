using UnityEngine;

// One authoring component per ground patch. No per-tile Update or independent simulation.
public class ElementTile : MonoBehaviour
{
    [Header("地块坐标与地形")]
    public Vector2Int gridPosition;
    public float elevation;
    public bool blocksSimulation;
    public bool alwaysBlocksWalking;
    [Header("初始五行数量：运行状态由世界管理器统一保存")]
    [Min(0)] public float gold;
    [Min(0)] public float wood;
    [Min(0)] public float water;
    [Min(0)] public float fire;
    [Min(0)] public float earth;
    [Header("场景表现")]
    public SpriteRenderer waterView, treeView, goldView, fireView, selectionView;
    public Collider2D walkingCollider;
    public ElementWorld World { get; private set; }
    public int Index { get; private set; } = -1;
    public CellState Initial => new CellState { Gold = Mathf.Max(0, gold), Wood = Mathf.Max(0, wood),
        Water = Mathf.Max(0, water), Fire = Mathf.Max(0, fire), Earth = Mathf.Max(0, earth) };
    public CellState Current => World != null && World.Simulation != null ? World.Simulation.GetCell(Index) : Initial;
    public void Bind(ElementWorld world, int index) { World = world; Index = index; }
    public void RefreshVisuals(CellState c, float woodThreshold)
    {
        if (waterView != null)
        {
            waterView.enabled = c.Water > 0.05f;
            // Overlap neighboring water patches so a lake reads as continuous terrain.
            waterView.transform.localScale = Vector3.one * 1.2f;
            waterView.color = new Color(0.32f, 0.68f, 0.80f, Mathf.Clamp(0.3f + c.Water * 0.05f, 0.3f, 0.9f));
        }
        if (treeView != null)
        {
            treeView.enabled = c.Wood > 0.05f;
            treeView.transform.localScale = Vector3.one * Mathf.Clamp(0.6f + c.Wood * 0.025f, 0.65f, 1.05f);
            treeView.color = c.Fire > 0.05f ? new Color(0.78f, 0.55f, 0.28f) : Color.white;
        }
        if (goldView != null) goldView.enabled = c.Gold > 0.05f;
        if (fireView != null)
        {
            fireView.enabled = c.Fire > 0.005f;
            fireView.transform.localScale = Vector3.one * Mathf.Clamp(0.55f + c.Fire * 0.08f, 0.55f, 1.35f);
        }
        if (walkingCollider != null) walkingCollider.enabled = alwaysBlocksWalking || c.Wood >= woodThreshold;
    }
    public void SetSelected(bool selected) 
    { 
        if (selectionView != null)
            selectionView.enabled = selected; 
    }
}
