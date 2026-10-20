using UnityEngine;
public class SettlementCamera : MonoBehaviour
{
    public SettlementWorld world;
    public Transform target;
    public float lookAhead=2;
    private Camera cameraComponent;
    private void Awake(){cameraComponent=GetComponent<Camera>();}
    private void LateUpdate()
    {
        if(world.Simulation==null)return;Vector3 min=world.ground.CellToWorld(world.Origin),max=world.ground.CellToWorld(world.Origin+new Vector3Int(world.Simulation.Width,world.Simulation.Height,0));
        float hy=cameraComponent.orthographicSize,hx=hy*cameraComponent.aspect;
        float x=max.x-min.x>hx*2?Mathf.Clamp(target.position.x,min.x+hx,max.x-hx):(min.x+max.x)/2;
        float y=max.y-min.y>hy*2?Mathf.Clamp(target.position.y+lookAhead,min.y+hy,max.y-hy):(min.y+max.y)/2;
        transform.position=Vector3.Lerp(transform.position,new Vector3(x,y,-10),1-Mathf.Exp(-7*Time.deltaTime));
    }
}
