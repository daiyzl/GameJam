using System.Collections.Generic;
using UnityEngine;

// Short visual playback only; landing/seed outcomes are already resolved by AdvancePhase.
public sealed class V1BirdFlockView : MonoBehaviour
{
    private sealed class Bird
    {
        public Transform root;
        public SpriteRenderer sprite;
        public Vector3 start, landing, exit;
        public float delay;
    }
    private readonly List<Bird> birds = new List<Bird>();
    private V1World world;
    private float elapsed;
    public int VisibleBirdCount => birds.Count;

    public void Clear()
    {
        foreach (var bird in birds) if (bird.root != null)
        { bird.root.gameObject.SetActive(false); Destroy(bird.root.gameObject); }
        birds.Clear(); elapsed = 0;
    }

    public void Play(V1World owner)
    {
        Clear(); world = owner;
        if (owner.Simulation.BirdLandings.Count == 0 || owner.birdSprites == null || owner.birdSprites.Length < 4) return;
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
            bird.sprite = root.gameObject.AddComponent<SpriteRenderer>();
            bird.sprite.sprite = owner.birdSprites[0]; bird.sprite.sharedMaterial = owner.spriteMaterial;
            bird.sprite.sortingOrder = 45; bird.sprite.flipX = direction.x < 0;
            root.localScale = Vector3.one * (.85f / owner.birdSprites[0].bounds.size.x);
            birds.Add(bird);
        }
        Pose(0);
    }

    private void Update()
    {
        if (birds.Count == 0 || world == null || world.MenuOpen || world.Busy) return;
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
            int frame = resting ? 3 : new[] { 0, 1, 2, 1 }[(int)(t * 9) % 4];
            bird.sprite.sprite = world.birdSprites[frame];
        }
    }
}
