using UnityEngine;

// Visual wandering stays inside the current habitat cell and never advances ecology time.
public sealed class V1AnimalView : MonoBehaviour
{
    private V1World world;
    private V1Animal animal;
    private SpriteRenderer view;
    private Vector3 target;
    private int displayedCell = -1;
    private float nextWander;
    private System.Random random;
    public void Configure(V1World owner, V1Animal state, int seed)
    { world = owner; animal = state; view = GetComponentInChildren<SpriteRenderer>(); random = new System.Random(seed); }
    private void Update()
    {
        if (world == null || animal == null || !animal.alive || world.MenuOpen || world.Busy || world.Ended) return;
        var center = world.Position(animal.cell) + Vector3.up * .18f;
        if (displayedCell != animal.cell)
        {
            if (animal.previousCell >= 0 && Vector3.Distance(transform.position, center) > 1.3f)
                transform.position = world.Position(animal.previousCell) + Vector3.up * .18f;
            displayedCell = animal.cell; target = center; nextWander = Time.time + 1;
        }
        if (Vector3.Distance(transform.position, target) < .02f && Time.time >= nextWander)
        {
            target = center + new Vector3((float)(random.NextDouble() - .5) * .32f, (float)(random.NextDouble() - .5) * .24f);
            nextWander = Time.time + 1 + (float)random.NextDouble() * 3;
        }
        float dx = target.x - transform.position.x;
        if (view != null && Mathf.Abs(dx) > .01f) view.flipX = dx < 0;
        transform.position = Vector3.MoveTowards(transform.position, target, Time.deltaTime * 1.4f);
    }
}
