using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class TutorialCharacter : MonoBehaviour
{
    public float moveSpeed=4.5f;
    public SpriteRenderer bodyView;
    public Sprite[] frames;
    private Rigidbody2D body;
    private Vector2 movement;
    private bool verificationOverride;
    private static readonly int[] WalkPoses={1,0,2,0};
    public Vector2 Facing {get;private set;}=Vector2.up;
    public Vector2 Position => body!=null?body.position:(Vector2)transform.position;
    private void Awake() {body=GetComponent<Rigidbody2D>();}
    private void Update()
    {
        if(!verificationOverride) movement=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical")).normalized;
        if(movement.sqrMagnitude>.001f) Facing=movement;
        int direction=Mathf.Abs(Facing.x)>Mathf.Abs(Facing.y)?(Facing.x<0?1:2):(Facing.y>0?3:0);
        int pose=movement.sqrMagnitude>.001f?WalkPoses[Mathf.FloorToInt(Time.time*8)%4]:0;
        if(bodyView!=null && frames!=null && frames.Length==12) {bodyView.sprite=frames[direction*3+pose];bodyView.flipX=false;}
    }
    private void FixedUpdate() {body.velocity=movement*moveSpeed;}
    public void SetVerificationMovement(Vector2 direction) {verificationOverride=true;movement=direction.normalized;}
    public void ReleaseVerificationMovement() {verificationOverride=false;movement=Vector2.zero;}
    public void Teleport(Vector2 position)
    {if(body==null) body=GetComponent<Rigidbody2D>(); body.position=position;body.velocity=Vector2.zero;transform.position=position;}
}
