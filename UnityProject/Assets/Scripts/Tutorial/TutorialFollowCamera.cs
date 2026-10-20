using UnityEngine;
public class TutorialFollowCamera : MonoBehaviour
{
    public TutorialWorld world;
    public Transform target;
    [Tooltip("向前展示教学地块，留出顶部提示面板的空间。")]
    public float lookAhead = 3f;
    private Camera cameraComponent;
    private void Awake() {cameraComponent=GetComponent<Camera>();}
    private void LateUpdate()
    {
        if(world==null||world.Simulation==null||target==null)return;
        Vector3 min=world.ground.CellToWorld(world.Origin),max=world.ground.CellToWorld(world.Origin+new Vector3Int(world.Simulation.Level.Width,world.Simulation.Level.Height,0));
        float hy=cameraComponent.orthographicSize,hx=hy*cameraComponent.aspect;
        float x=max.x-min.x>hx*2?Mathf.Clamp(target.position.x,min.x+hx,max.x-hx):(min.x+max.x)/2;
        float y=max.y-min.y>hy*2?Mathf.Clamp(target.position.y+lookAhead,min.y+hy,max.y-hy):(min.y+max.y)/2;
        transform.position=Vector3.Lerp(transform.position,new Vector3(x,y,-10),1-Mathf.Exp(-8*Time.deltaTime));
    }
}
