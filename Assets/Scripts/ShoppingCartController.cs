using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ShoppingCart : MonoBehaviour
{
    public Transform player;
    public Vector3 gripOffset = new Vector3(0, 0, 0.7f);
    public Vector3 handle = new Vector3(0, 0.9f, -0.55f);
    public Vector3 rearWheel = new Vector3(0, 0, -0.4f);
    public Vector3 frontWheel = new Vector3(0, 0, 0.4f);
    public float pull = 60f;
    public float damping = 14f;
    public float rearGrip = 14f;
    public float frontGrip = 4f;

    Rigidbody rb;
    Vector3 casterDir;
    Vector3 lastPlayerPos;
    Vector3 playerVel;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        SetupRigidbody();
        SetupCollider();
        casterDir = transform.forward;
        lastPlayerPos = player.position;
    }

    void FixedUpdate()
    {
        UpdatePlayerVelocity();
        PullHandle();
        GripRearWheel();
        GripFrontWheel();
    }

    void SetupRigidbody()
    {
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.angularDamping = 0.6f;
        rb.linearDamping = 0.5f;
    }

    void SetupCollider()
    {
        Collider cartCollider = GetComponent<Collider>();

        PhysicsMaterial slippery = new PhysicsMaterial();
        slippery.dynamicFriction = 0;
        slippery.staticFriction = 0;
        slippery.frictionCombine = PhysicsMaterialCombine.Minimum;
        cartCollider.material = slippery;

        Physics.IgnoreCollision(cartCollider, player.GetComponent<Collider>());
    }

    void UpdatePlayerVelocity()
    {
        Vector3 rawVel = (player.position - lastPlayerPos) / Time.fixedDeltaTime;
        playerVel = Vector3.Lerp(playerVel, rawVel, 0.5f);
        lastPlayerPos = player.position;
    }

    void PullHandle()
    {
        Vector3 handlePos = transform.TransformPoint(handle);
        Vector3 error = player.TransformPoint(gripOffset) - handlePos;
        Vector3 relVel = playerVel - rb.GetPointVelocity(handlePos);
        error.y = 0;
        relVel.y = 0;
        rb.AddForceAtPosition((error * pull + relVel * damping) * rb.mass, handlePos);
    }

    void GripRearWheel()
    {
        ApplySideGrip(transform.TransformPoint(rearWheel), transform.forward, rearGrip);
    }

    void GripFrontWheel()
    {
        Vector3 frontPos = transform.TransformPoint(frontWheel);
        SwivelCaster(frontPos);
        ApplySideGrip(frontPos, casterDir, frontGrip);
    }

    void SwivelCaster(Vector3 wheelPos)
    {
        Vector3 wheelVel = rb.GetPointVelocity(wheelPos);
        wheelVel.y = 0;
        if (wheelVel.magnitude < 0.1f) return;

        float sign = Mathf.Sign(Vector3.Dot(casterDir, wheelVel));
        casterDir = Vector3.RotateTowards(casterDir, wheelVel.normalized * sign, 6f * Time.fixedDeltaTime, 0);
    }

    void ApplySideGrip(Vector3 pos, Vector3 wheelForward, float grip)
    {
        Vector3 vel = rb.GetPointVelocity(pos);
        vel.y = 0;
        wheelForward.y = 0;
        Vector3 right = Vector3.Cross(Vector3.up, wheelForward.normalized);
        float sideSpeed = Vector3.Dot(vel, right);
        rb.AddForceAtPosition(-right * sideSpeed * grip * rb.mass * 0.5f, pos);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(transform.TransformPoint(handle), 0.06f);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.TransformPoint(rearWheel), 0.06f);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.TransformPoint(frontWheel), 0.06f);

        if (player != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(player.TransformPoint(gripOffset), 0.05f);
        }
    }
}