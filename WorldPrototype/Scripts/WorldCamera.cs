using UnityEngine;

public class WorldCamera : MonoBehaviour
{
    public Transform target;
    public ElementWorld world;
    public float smoothing = 8f;
    private void LateUpdate()
    {
        if (target == null || world == null) return;
        Camera cam = GetComponent<Camera>();
        float halfY = cam.orthographicSize, halfX = halfY * cam.aspect;
        float x = world.width * world.cellSize > halfX * 2 ? Mathf.Clamp(target.position.x, halfX - 0.5f, (world.width - 0.5f) * world.cellSize - halfX) : (world.width - 1) * world.cellSize / 2;
        float y = world.height * world.cellSize > halfY * 2 ? Mathf.Clamp(target.position.y, halfY - 0.5f, (world.height - 0.5f) * world.cellSize - halfY) : (world.height - 1) * world.cellSize / 2;
        transform.position = Vector3.Lerp(transform.position, new Vector3(x, y, -10), 1 - Mathf.Exp(-smoothing * Time.deltaTime));
    }
}
