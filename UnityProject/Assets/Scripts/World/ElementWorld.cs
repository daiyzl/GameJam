using UnityEngine;

[DefaultExecutionOrder(-100)]
public class ElementWorld : MonoBehaviour
{
    [Range(1, 64)] public int width = 24;
    [Range(1, 64)] public int height = 18;
    public float cellSize = 1f;
    public SimulationSettings rules = new SimulationSettings { StepSeconds = 1f, FlowRate = 4f };
    public WorldCharacter player;
    public bool simulateAutomatically = true;
    public FiveElementSimulation Simulation { get; private set; }
    public ElementTile SelectedTile { get; private set; }
    public Element SelectedElement { get; private set; }
    public string Message { get; private set; } = "走近地块，观察五行变化";
    private ElementTile[] tiles;
    private float accumulator;
    public Vector2 SpawnPosition = new Vector2(12, 5);

    private void Awake()
    {
        Application.runInBackground = true;
        // Other saved verification scenes may still be loaded additively in the editor.
        foreach (PrototypeView view in FindObjectsOfType<PrototypeView>())
            if (view.gameObject.scene != gameObject.scene) view.enabled = false;
        foreach (GameRuler ruler in FindObjectsOfType<GameRuler>())
            if (ruler.gameObject.scene != gameObject.scene) ruler.enabled = false;
        RebuildFromScene();
    }
    public void RebuildFromScene()
    {
        width = Mathf.Clamp(width, 1, 64); height = Mathf.Clamp(height, 1, 64);
        var level = new LevelDefinition(width, height) { Name = "五行小世界", StepLimit = int.MaxValue, TargetWater = float.MaxValue };
        tiles = new ElementTile[width * height];
        for (int i = 0; i < level.Blocked.Length; i++) level.Blocked[i] = true;
        foreach (ElementTile tile in GetComponentsInChildren<ElementTile>())
        {
            int i = level.Index(tile.gridPosition.x, tile.gridPosition.y);
            if (i < 0 || tiles[i] != null) { Debug.LogError("地块坐标越界或重复：" + tile.name, tile); continue; }
            tiles[i] = tile; level.Initial[i] = tile.Initial;
            level.Elevation[i] = tile.elevation; level.Blocked[i] = tile.blocksSimulation;
            tile.Bind(this, i);
        }
        Simulation = new FiveElementSimulation(level, rules);
        accumulator = 0; RefreshWorld();
    }
    private void Update()
    {
        if (Simulation == null) return;
        if (Input.GetKeyDown(KeyCode.Space)) simulateAutomatically = !simulateAutomatically;
        if (Input.GetKeyDown(KeyCode.R)) ResetWorld();
        if (Input.GetKeyDown(KeyCode.Period)) StepOnce();
        for (int e = 0; e < 5; e++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + e))) SelectedElement = (Element)e;
        if (player != null)
        {
            Vector2 point = player.Position + player.Facing * 0.8f;
            ElementTile selected = TileAtWorld(point);
            if (selected != SelectedTile)
            {
                if (SelectedTile != null) SelectedTile.SetSelected(false);
                SelectedTile = selected;
                if (SelectedTile != null) SelectedTile.SetSelected(true);
            }
        }
        if (Input.GetKeyDown(KeyCode.E)) AddToSelected();
        if (!simulateAutomatically) return;
        accumulator += Time.deltaTime;
        int count = 0;
        while (accumulator >= rules.StepSeconds && count++ < 8)
        { accumulator -= rules.StepSeconds; Simulation.Step(); RefreshWorld(); }
    }
    public ElementTile TileAtWorld(Vector2 point)
    {
        Vector2 local = point - (Vector2)transform.position;
        int x = Mathf.RoundToInt(local.x / cellSize), y = Mathf.RoundToInt(local.y / cellSize);
        int i = Simulation.Level.Index(x, y);
        return i >= 0 ? tiles[i] : null;
    }
    public void AddToSelected()
    {
        if (SelectedTile == null || SelectedTile.blocksSimulation) { Message = "面前没有可操作的地块"; return; }
        Simulation.AddResource(SelectedTile.Index, SelectedElement, 1);
        RefreshWorld(); Message = "已向面前地块加入 1 份" + ElementName(SelectedElement);
    }
    public void StepOnce()
    { simulateAutomatically = false; accumulator = 0; Simulation.Step(); RefreshWorld(); }
    public void ResetWorld()
    {
        RebuildFromScene();
        if (player != null) player.Teleport(SpawnPosition + (Vector2)transform.position);
        Message = "世界已恢复为地块 Inspector 的初始配置";
    }
    private void RefreshWorld()
    {
        for (int i = 0; i < tiles.Length; i++)
            if (tiles[i] != null) tiles[i].RefreshVisuals(Simulation.GetCell(i), rules.WoodBlockThreshold);
    }
    public static string ElementName(Element e) => e == Element.Gold ? "金" : e == Element.Wood ? "木" : e == Element.Water ? "水" : e == Element.Fire ? "火" : "土";
}
