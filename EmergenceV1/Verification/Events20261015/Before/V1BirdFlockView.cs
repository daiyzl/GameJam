using System.Collections.Generic;
using UnityEngine;

// Short visual playback only; landing/seed outcomes are already resolved by AdvancePhase.
public sealed class V1BirdFlockView : MonoBehaviour
{
    private sealed class Bird
    {
        public Transform root;
        public LineRenderer wings;
        public Vector3 start, landing, exit;
        public float delay;
    }
    private readonly List<Bird> birds = new List<Bird>();
    private Material material;
    private V1World world;
    private float elapsed;
    public int VisibleBirdCount => birds.Count;

    public void Clear()
    {
        foreach (var bird in birds) if (bird.root != null)
        { bird.root.gameObject.SetActive(false); Destroy(bird.root.gameObject); }
        birds.Clear(); elapsed = 0;
    }

    private LineRenderer Line(Transform root, string label, int points, float width, Color color)
    {
        var go = new GameObject(label); go.transform.SetParent(root, false);
        var line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = material; line.useWorldSpace = false; line.positionCount = points;
        line.startWidth = line.endWidth = width; line.startColor = line.endColor = color;
        line.sortingOrder = 45; line.numCapVertices = 2;
        return line;
    }

    public void Play(V1World owner)
    {
        Clear(); world = owner;
        if (owner.Simulation.BirdLandings.Count == 0) return;
        if (material == null) material = new Material(Shader.Find("Sprites/Default"));
        foreach (var result in owner.Simulation.BirdLandings)
        {
            int index = birds.Count;
            var root = new GameObject("春季迁徙鸟 " + (index + 1)).transform;
            root.SetParent(transform, false);
            Vector3 direction = result.entryEdge == 0 ? Vector3.right : result.entryEdge == 1 ? Vector3.down :
                result.entryEdge == 2 ? Vector3.left : Vector3.up;
            var landing = owner.Position(result.cell) + new Vector3((index % 3 - 1) * .16f, .12f, 0);
            var bird = new Bird { root = root, landing = landing, start = landing - direction * 7 + Vector3.up * 2,
                exit = landing + direction * 8 + Vector3.up * 4, delay = index * .09f };
            root.position = bird.start;
            root.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            bird.wings = Line(root, "鸟翼", 5, .045f, new Color(.89f, .87f, .7f));
            var body = Line(root, "鸟身", 2, .075f, new Color(.26f, .19f, .12f));
            body.SetPosition(0, new Vector3(-.13f, 0, 0)); body.SetPosition(1, new Vector3(.16f, 0, 0));
            birds.Add(bird);
        }
        Pose(0);
    }

    private void Update()
    {
        if (birds.Count == 0 || world == null || world.ToolsOpen || world.Busy) return;
        elapsed += Time.deltaTime; Pose(elapsed);
        if (elapsed > 7) Clear();
    }

    private void Pose(float time)
    {
        foreach (var bird in birds)
        {
            float t = Mathf.Max(0, time - bird.delay);
            bool resting = t >= 2.2f && t <= 4;
            if (t < 2.2f) bird.root.position = Vector3.Lerp(bird.start, bird.landing, Mathf.SmoothStep(0, 1, t / 2.2f));
            else if (resting) bird.root.position = bird.landing;
            else bird.root.position = Vector3.Lerp(bird.landing, bird.exit, Mathf.SmoothStep(0, 1, (t - 4) / 2.3f));
            float flap = resting ? .035f : Mathf.Sin(t * 18 + bird.delay * 4) * .18f;
            bird.wings.SetPosition(0, new Vector3(-.09f + flap, -.3f, 0));
            bird.wings.SetPosition(1, new Vector3(.04f, -.15f, 0));
            bird.wings.SetPosition(2, Vector3.zero);
            bird.wings.SetPosition(3, new Vector3(.04f, .15f, 0));
            bird.wings.SetPosition(4, new Vector3(-.09f + flap, .3f, 0));
        }
    }
    private void OnDestroy() { if (material != null) Destroy(material); }
}
