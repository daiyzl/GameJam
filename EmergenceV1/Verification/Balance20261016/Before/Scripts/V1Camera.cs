using UnityEngine;

public class V1Camera : MonoBehaviour
{
    public V1World world;
    public Transform target;
    private Camera view;
    private void Awake(){view=GetComponent<Camera>();}
    private void LateUpdate()
    {
        if(target==null||world.Simulation==null)return;
        float x=view.orthographicSize*view.aspect,y=view.orthographicSize;
        Vector3 p=target.position+new Vector3(0,2.6f,-10);
        p.x=Mathf.Clamp(p.x,world.Origin.x+x,world.Origin.x+world.Simulation.Width-x);
        p.y=Mathf.Clamp(p.y,world.Origin.y+y,world.Origin.y+world.Simulation.Height-y);
        transform.position=Vector3.Lerp(transform.position,p,1-Mathf.Exp(-Time.deltaTime*7));
    }
}
