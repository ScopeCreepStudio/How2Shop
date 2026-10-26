using UnityEngine;
using UnityEngine.InputSystem;
using How2Shop.Chaos;

[RequireComponent(typeof(Rigidbody))]
public class ShoppingCartController : MonoBehaviour
{
    [Header("Player connection")]
    public Transform player;
    public Vector3 gripOffset = new Vector3(0f, 0.32f, 0.35f);
    public Vector3 handle = new Vector3(-3.46f, 6.04f, 0f);

    [Header("Cart wheel points (model local X is forward)")]
    public Vector3 rearWheel = new Vector3(-2.75f, 0f, 0f);
    public Vector3 frontWheel = new Vector3(2.75f, 0f, 0f);

    [Header("Push feel")]
    [Min(0f)] public float pull = 48f;
    [Min(0f)] public float damping = 18f;
    [Min(0f)] public float rearGrip = 13f;
    [Min(0f)] public float frontGrip = 7f;
    [Min(1f)] public float playerSpeedMultiplier = 1.5f;
    [Min(0f)] public float alignmentStrength = 8f;
    [Min(0f)] public float alignmentDamping = 4f;

    [Header("Drifting (hold Left Shift while pushing)")]
    [Min(0f)] public float driftRearGrip = 1.4f;
    [Min(0f)] public float driftFrontGrip = 2.2f;
    [Min(0f)] public float driftSteeringTorque = 5f;
    [Min(0f)] public float driftAlignmentStrength = 0.65f;
    [Min(0f)] public float driftAlignmentDamping = 1.2f;

    [Header("Audio")]
    public AudioClip cartRollClip;
    public AudioClip[] driftScreechClips;
    public AudioClip impactClip;

    private Rigidbody rb;
    private PlayerMovement playerMovement;
    private Vector3 casterDir;
    private Vector3 lastPlayerPos;
    private Vector3 playerVel;
    private bool playerHolding;
    private bool drifting;
    private float nextToggleTime;
    private AudioSource rollingAudio;
    private AudioSource effectsAudio;
    private float nextScreechTime;
    private float nextImpactSoundTime;
    private float lastPlayerPushTime = -1000f;

    public bool IsPlayerHolding => playerHolding;
    public bool IsDrifting => drifting;
    public bool IsPlayerDrivenCart => GetComponent<How2Shop.Items.ShoppingCart>() != null;
    public bool WasRecentlyPushedByPlayer => IsPlayerDrivenCart && Time.time - lastPlayerPushTime <= 0.8f;
    public float CurrentSpeed => rb != null ? rb.linearVelocity.magnitude : playerVel.magnitude;

    private void Awake()
    {
        if (GetComponentInParent<ShopperNPC>() != null)
        {
            enabled = false;
            return;
        }

        rb = GetComponent<Rigidbody>();
        if (player == null)
        {
            PlayerMovement foundPlayer = FindFirstObjectByType<PlayerMovement>();
            if (foundPlayer != null)
                player = foundPlayer.transform;
        }

        playerMovement = player != null
            ? player.GetComponent<PlayerMovement>() ?? player.GetComponentInParent<PlayerMovement>()
            : null;

        SetupRigidbody();
        SetupCollider();
        AudioSource[] sources = GetComponents<AudioSource>();
        rollingAudio = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
        effectsAudio = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
        ConfigureAudio(rollingAudio, true);
        ConfigureAudio(effectsAudio, false);
        rollingAudio.clip = cartRollClip;
        if (cartRollClip != null) rollingAudio.Play();
        casterDir = CartForward;

        if (player != null)
            lastPlayerPos = player.position;
        else
            Debug.LogWarning("ShoppingCartController could not find a PlayerMovement component.", this);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || player == null)
            return;

        if (keyboard.eKey.wasPressedThisFrame && Time.time >= nextToggleTime)
        {
            SetPlayerHolding(!playerHolding);
            nextToggleTime = Time.time + 0.25f;
        }

        drifting = playerHolding && keyboard.leftShiftKey.isPressed;
        float speed = rb != null ? new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude : 0f;
        if (GameplaySfxPlayer.Instance != null && IsPlayerDrivenCart)
        {
            GameplaySfxPlayer.Instance.SetCartRolling(transform, speed > 0.15f, speed);
            rollingAudio.volume = 0f;
        }
        else
        {
            rollingAudio.volume = Mathf.Clamp01(speed / 5f) * (playerHolding ? 0.75f : 0.35f);
        }
        if (drifting && speed > 1.4f && Time.time >= nextScreechTime && driftScreechClips != null && driftScreechClips.Length > 0)
        {
            AudioClip clip = driftScreechClips[Random.Range(0, driftScreechClips.Length)];
            if (clip != null) effectsAudio.PlayOneShot(clip, Mathf.Clamp01(speed / 7f));
            nextScreechTime = Time.time + 0.8f;
        }
    }

    private void FixedUpdate()
    {
        if (!playerHolding || player == null || rb == null)
            return;

        UpdatePlayerVelocity();
        PullHandle();
        AlignCart();
        GripRearWheel();
        GripFrontWheel();
        if (drifting)
            SteerDrift();
    }

    private void OnDisable()
    {
        if (playerMovement != null)
            playerMovement.SetCartBoost(false, playerSpeedMultiplier);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null)
            return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (IsPlayerDrivenCart && WasRecentlyPushedByPlayer)
        {
            ShopperRagdoll shopper = collision.collider.GetComponentInParent<ShopperRagdoll>();
            if (shopper != null)
            {
                shopper.ReportPlayerTrolleyImpact(this, impactSpeed);
            }
            else
            {
                ShopperCartImpactRelay shopperCartRelay = collision.collider.GetComponentInParent<ShopperCartImpactRelay>();
                if (shopperCartRelay != null)
                {
                    shopperCartRelay.ReportPlayerTrolleyImpact(this, impactSpeed);
                }
                else
                {
                    ChaosImpactTarget chaosTarget = collision.collider.GetComponentInParent<ChaosImpactTarget>();
                    if (chaosTarget != null)
                        chaosTarget.TryReportImpact(ChaosImpactSource.PlayerTrolley, gameObject, impactSpeed);
                }
            }
        }

        if (impactSpeed < 2.5f || Time.unscaledTime < nextImpactSoundTime)
            return;

        nextImpactSoundTime = Time.unscaledTime + 0.8f;
        Vector3 contactPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : transform.position;

        if (GameplaySfxPlayer.Instance != null && IsPlayerDrivenCart)
            GameplaySfxPlayer.Instance.PlayCartCrash(contactPoint, impactSpeed);
        else if (impactClip != null)
            effectsAudio.PlayOneShot(impactClip, Mathf.Clamp01(impactSpeed / 10f));
    }

    private void SetPlayerHolding(bool holding)
    {
        playerHolding = holding;
        drifting = false;
        if (player != null)
            lastPlayerPos = player.position;

        if (playerMovement != null)
            playerMovement.SetCartBoost(holding, playerSpeedMultiplier);
    }

    private void SetupRigidbody()
    {
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        rb.angularDamping = 0.6f;
        rb.linearDamping = 0.35f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
    }

    private static void ConfigureAudio(AudioSource source, bool loop)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 1f;
        source.minDistance = 2f;
        source.maxDistance = 24f;
    }

    private void SetupCollider()
    {
        Collider cartCollider = GetComponent<Collider>();
        if (cartCollider == null)
            return;

        PhysicsMaterial slippery = new PhysicsMaterial("Cart wheels");
        slippery.dynamicFriction = 0f;
        slippery.staticFriction = 0f;
        slippery.frictionCombine = PhysicsMaterialCombine.Minimum;
        cartCollider.material = slippery;

        if (player == null)
            return;

        Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
        for (int i = 0; i < playerColliders.Length; i++)
            Physics.IgnoreCollision(cartCollider, playerColliders[i], true);
    }

    private Vector3 CartForward => transform.right;

    private void UpdatePlayerVelocity()
    {
        Vector3 rawVelocity = (player.position - lastPlayerPos) / Time.fixedDeltaTime;
        rawVelocity.y = 0f;
        playerVel = Vector3.Lerp(playerVel, rawVelocity, 0.55f);
        if (IsPlayerDrivenCart && playerVel.sqrMagnitude > 0.25f)
            lastPlayerPushTime = Time.time;
        lastPlayerPos = player.position;
    }

    private void PullHandle()
    {
        Vector3 handlePosition = transform.TransformPoint(handle);
        Vector3 targetPosition = player.TransformPoint(gripOffset);
        Vector3 error = targetPosition - handlePosition;
        Vector3 relativeVelocity = playerVel - rb.GetPointVelocity(handlePosition);
        error.y = 0f;
        relativeVelocity.y = 0f;
        rb.AddForceAtPosition((error * pull + relativeVelocity * damping) * rb.mass, handlePosition);
    }

    private void AlignCart()
    {
        Vector3 desiredForward = player.forward;
        desiredForward.y = 0f;
        if (desiredForward.sqrMagnitude < 0.001f)
            return;

        float strength = drifting ? driftAlignmentStrength : alignmentStrength;
        float dampingStrength = drifting ? driftAlignmentDamping : alignmentDamping;
        float yawError = Vector3.SignedAngle(CartForward, desiredForward.normalized, Vector3.up);
        float yawAcceleration = yawError * strength - rb.angularVelocity.y * dampingStrength;
        rb.AddTorque(Vector3.up * yawAcceleration, ForceMode.Acceleration);
    }

    private void GripRearWheel()
    {
        ApplySideGrip(transform.TransformPoint(rearWheel), CartForward, drifting ? driftRearGrip : rearGrip);
    }

    private void GripFrontWheel()
    {
        Vector3 frontPosition = transform.TransformPoint(frontWheel);
        SwivelCaster(frontPosition);
        ApplySideGrip(frontPosition, casterDir, drifting ? driftFrontGrip : frontGrip);
    }

    private void SwivelCaster(Vector3 wheelPosition)
    {
        Vector3 wheelVelocity = rb.GetPointVelocity(wheelPosition);
        wheelVelocity.y = 0f;
        if (wheelVelocity.magnitude < 0.1f)
            return;

        float sign = Mathf.Sign(Vector3.Dot(casterDir, wheelVelocity));
        casterDir = Vector3.RotateTowards(casterDir, wheelVelocity.normalized * sign, 6f * Time.fixedDeltaTime, 0f);
    }

    private void ApplySideGrip(Vector3 position, Vector3 wheelForward, float grip)
    {
        Vector3 velocity = rb.GetPointVelocity(position);
        velocity.y = 0f;
        wheelForward.y = 0f;
        if (wheelForward.sqrMagnitude < 0.001f)
            return;

        Vector3 side = Vector3.Cross(Vector3.up, wheelForward.normalized);
        float sideSpeed = Vector3.Dot(velocity, side);
        rb.AddForceAtPosition(-side * sideSpeed * grip * rb.mass * 0.5f, position);
    }

    private void SteerDrift()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        float steer = 0f;
        if (keyboard.dKey.isPressed) steer += 1f;
        if (keyboard.aKey.isPressed) steer -= 1f;

        float speed = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z).magnitude;
        float speedFactor = Mathf.Clamp01(speed / 2f);
        rb.AddTorque(Vector3.up * (steer * driftSteeringTorque * speedFactor), ForceMode.Acceleration);
    }

    private void OnDrawGizmosSelected()
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
