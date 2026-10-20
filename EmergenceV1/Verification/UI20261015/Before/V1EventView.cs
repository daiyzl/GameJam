using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Event results are computed only in AdvancePhase; this component renders them.
public sealed class V1EventView : MonoBehaviour
{
    private V1World world;
    private ParticleSystem snow;
    private Material snowMaterial, boltMaterial;
    private Texture2D flakeTexture, coverTexture;
    private Sprite coverSprite;
    private Tile coverTile;
    private Tilemap cover;
    private SpriteRenderer flash;
    private Sprite flashSprite;
    private readonly List<LineRenderer> bolts = new List<LineRenderer>();
    private float elapsed;
    public bool IsSnowing => snow != null && snow.isPlaying;
    public int SnowParticleCount => snow != null ? snow.particleCount : 0;
    public int LightningCount => bolts.Count;
    public bool LightningVisible { get { foreach (var bolt in bolts) if (bolt != null && bolt.enabled) return true; return false; } }
    public Vector2 RainArea { get; private set; }

    public void Clear()
    {
        if (world != null && world.weather != null) world.weather.StopRain();
        if (snow != null) snow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (cover != null) cover.ClearAllTiles();
        foreach (var bolt in bolts) if (bolt != null) { bolt.gameObject.SetActive(false); Destroy(bolt.gameObject); }
        bolts.Clear(); elapsed = 0;
        if (flash != null) flash.enabled = false;
    }

    public void Play(V1World owner)
    {
        Clear(); world = owner;
        var s = owner.Simulation;
        var center = owner.Position(0) + new Vector3((s.Width - 1) * .5f, (s.Height - 1) * .5f, 0);
        RainArea = new Vector2(s.Width + 2, s.Height + 8);
        if ((s.CurrentEvent == V1PhaseEvent.Rain || s.CurrentEvent == V1PhaseEvent.Thunderstorm) && owner.weather != null)
        {
            owner.weather.BeginRain(center);
            foreach (var ps in owner.weather.GetComponentsInChildren<ParticleSystem>())
            {
                var shape = ps.shape; shape.scale = new Vector3(RainArea.x, RainArea.y, .01f);
                var main = ps.main; main.maxParticles = 2400;
                var emission = ps.emission;
                bool falling = ps.gameObject.name.Contains("雨线");
                emission.rateOverTime = s.Width * s.Height * (falling ? (s.CurrentEvent == V1PhaseEvent.Thunderstorm ? 1.6f : .9f) : .08f);
            }
        }
        if (s.CurrentEvent == V1PhaseEvent.Snow)
        {
            EnsureSnow(); snow.transform.position = center + Vector3.up * 2;
            var shape = snow.shape; shape.scale = new Vector3(s.Width + 2, s.Height + 5, .01f);
            var emission = snow.emission; emission.rateOverTime = s.Width * s.Height * .65f;
            snow.Play();
        }
        foreach (int cell in s.LightningCells)
        {
            if (boltMaterial == null) boltMaterial = new Material(Shader.Find("Sprites/Default"));
            var go = new GameObject("落雷"); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>(); line.sharedMaterial = boltMaterial;
            line.positionCount = 7; line.useWorldSpace = true; line.sortingOrder = 55;
            line.startWidth = .10f; line.endWidth = .035f;
            line.startColor = new Color(.72f, .85f, 1); line.endColor = Color.white;
            var end = owner.Position(cell);
            for (int p = 0; p < 7; p++) line.SetPosition(p, end + new Vector3(p == 0 ? 0 : (p % 2 == 0 ? -.35f : .3f), p * .9f, 0));
            bolts.Add(line);
        }
        if (bolts.Count > 0)
        {
            if (flash == null)
            {
                flashSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
                var go = new GameObject("雷雨微光"); go.transform.SetParent(transform, false); flash = go.AddComponent<SpriteRenderer>();
                flash.sprite = flashSprite; flash.sharedMaterial = owner.spriteMaterial; flash.sortingOrder = 50;
            }
            flash.transform.position = center; flash.transform.localScale = new Vector3(s.Width, s.Height, 1);
            flash.color = new Color(.8f, .88f, 1, .09f);
        }
        RefreshSnowCover();
    }

    private void EnsureSnow()
    {
        if (snow != null) return;
        flakeTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pixels = new Color[64];
        for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            pixels[x + y * 8] = (x == 3 || y == 3 || x == y && x > 1 && x < 6 || x + y == 6 && x > 1 && x < 6) ? Color.white : Color.clear;
        flakeTexture.SetPixels(pixels); flakeTexture.Apply();
        snowMaterial = new Material(Shader.Find("Sprites/Default")) { mainTexture = flakeTexture };
        var go = new GameObject("全图雪粒子"); go.transform.SetParent(transform, false);
        snow = go.AddComponent<ParticleSystem>(); snow.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = snow.main; main.playOnAwake = false; main.startLifetime = 4; main.startSpeed = 0;
        main.startSize = new ParticleSystem.MinMaxCurve(.08f, .18f); main.maxParticles = 3500;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var shape = snow.shape; shape.shapeType = ParticleSystemShapeType.Box;
        var velocity = snow.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-.35f, .2f); velocity.y = new ParticleSystem.MinMaxCurve(-1.3f, -.7f);
        velocity.z = new ParticleSystem.MinMaxCurve(0, 0);
        var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = snowMaterial; renderer.sortingOrder = 36;
    }

    private void RefreshSnowCover()
    {
        bool any = false; foreach (var cell in world.Simulation.Cells) if (cell.Snow > .001f) { any = true; break; }
        if (!any) return;
        if (cover == null)
        {
            coverTexture = new Texture2D(1, 1); coverTexture.SetPixel(0, 0, Color.white); coverTexture.Apply();
            coverSprite = Sprite.Create(coverTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f, 1);
            coverTile = ScriptableObject.CreateInstance<Tile>(); coverTile.sprite = coverSprite;
            coverTile.colliderType = Tile.ColliderType.None; coverTile.flags = TileFlags.None;
            var go = new GameObject("积雪覆盖"); go.transform.SetParent(world.ground.transform.parent, false);
            cover = go.AddComponent<Tilemap>(); var renderer = go.AddComponent<TilemapRenderer>();
            renderer.sharedMaterial = world.spriteMaterial; renderer.sortingOrder = 3;
        }
        for (int i = 0; i < world.Simulation.Cells.Length; i++)
        {
            var c = world.Simulation.Cells[i];
            if (!world.Simulation.Valid(i) || world.Simulation.IsWet(i) || c.Bridge || c.Snow <= .001f) continue;
            var position = world.Coordinate(i); cover.SetTile(position, coverTile);
            cover.SetColor(position, new Color(.91f, .96f, 1, Mathf.Clamp01(c.Snow / .5f) * .75f));
        }
    }
    private void Update()
    {
        if (world == null || world.ToolsOpen || world.Busy) return;
        elapsed += Time.deltaTime;
        for (int i = 0; i < bolts.Count; i++) if (bolts[i] != null)
            bolts[i].enabled = elapsed < 4 && (elapsed + i * .23f) % 1 < .45f;
        if (flash != null) flash.enabled = LightningVisible;
    }
    private void OnDestroy()
    {
        if (snowMaterial != null) Destroy(snowMaterial); if (boltMaterial != null) Destroy(boltMaterial);
        if (flakeTexture != null) Destroy(flakeTexture); if (coverTexture != null) Destroy(coverTexture);
        if (coverSprite != null) Destroy(coverSprite); if (coverTile != null) Destroy(coverTile);
        if (flashSprite != null) Destroy(flashSprite);
        if (cover != null) Destroy(cover.gameObject);
    }
}
