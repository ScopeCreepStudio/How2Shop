using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Makes a Humanoid shopper wander to random reachable points while keeping both hands
/// on an assigned shopping-cart model. The cart can be a child of the shopper or a
/// separate scene object.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(NavMeshAgent))]
public sealed class ShopperNPC : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform shoppingCart;
    [SerializeField] private Transform leftHandGrip;
    [SerializeField] private Transform rightHandGrip;

    [Header("Cart local grip points")]
    [Tooltip("Used without an explicit Left Hand Grip. Assumes the cart faces +Z.")]
    [SerializeField] private Vector3 leftHandCartLocalPosition = new Vector3(-1.8f, 4.9f, -3.0f);
    [Tooltip("Used without an explicit Right Hand Grip. Assumes the cart faces +Z.")]
    [SerializeField] private Vector3 rightHandCartLocalPosition = new Vector3(1.8f, 4.9f, -3.0f);
    [SerializeField] private Vector3 leftHandCartLocalEuler;
    [SerializeField] private Vector3 rightHandCartLocalEuler;

    [Header("Wandering")]
    [SerializeField] private bool wanderOnStart = true;
    [SerializeField, Min(1f)] private float wanderRadius = 12f;
    [SerializeField, Min(0f)] private float minimumDestinationDistance = 3f;
    [SerializeField, Min(0.25f)] private float destinationSampleRadius = 1.5f;
    [SerializeField, Min(1)] private int destinationAttempts = 24;
    [SerializeField] private Vector2 pauseDuration = new Vector2(1.5f, 4f);
    [SerializeField, Min(0f)] private float walkingSpeed = 1.2f;
    [SerializeField, Min(0.1f)] private float movementAcceleration = 4f;
    [SerializeField, Min(0f)] private float cartFollowDistance = 0.9f;
    [SerializeField, Min(0f)] private float cartFollowSpeed = 4f;
    [SerializeField] private Vector3 externalCartRotationOffset;

    [Header("IK")]
    [Range(0f, 1f)]
    [SerializeField] private float handIkWeight = 1f;

    private NavMeshPath destinationPath;
    private bool hasWalkingParameter;
    private bool isWaiting;
    private float waitRemaining;
    private float externalCartHeight;

    public Transform ShoppingCart => shoppingCart;
    public Animator CharacterAnimator => animator;
    public NavMeshAgent Agent => agent;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
    }

private void Awake()
{
    EnsureDestinationPath();

    if (animator == null)
        animator = GetComponent<Animator>();
    if (agent == null)
        agent = GetComponent<NavMeshAgent>();

    if (agent != null)
    {
        agent.speed = walkingSpeed;
        agent.acceleration = movementAcceleration;
        agent.angularSpeed = 240f;
        agent.stoppingDistance = 0.25f;
        agent.autoBraking = true;
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = Random.Range(35, 65);
    }

    if (animator != null)
    {
        animator.applyRootMotion = false;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.name == "Walking" && parameter.type == AnimatorControllerParameterType.Bool)
            {
                hasWalkingParameter = true;
                break;
            }
        }
    }

    if (shoppingCart != null && !shoppingCart.IsChildOf(transform))
        externalCartHeight = shoppingCart.position.y;

    if (GetComponent<ShopperRagdoll>() == null)
        gameObject.AddComponent<ShopperRagdoll>();
}

    private void OnEnable()
    {
        // Enter Play Mode can keep scene objects alive without rerunning Awake.
        EnsureDestinationPath();
    }

    private void Start()
    {
        if (!wanderOnStart)
            return;

        if (shoppingCart == null)
        {
            Debug.LogWarning(name + " needs a Shopping Cart model assigned before it can wander.", this);
            return;
        }

        if (!PlaceAgentOnNavMesh())
        {
            Debug.LogWarning(name + " could not find a nearby baked NavMesh.", this);
            return;
        }

        ChooseNextDestination();
    }

    private void Update()
    {
        if (!wanderOnStart || shoppingCart == null || agent == null || !agent.isOnNavMesh)
        {
            SetWalking(false);
            UpdateCartRollAudio(false);
            return;
        }

        UpdateExternalCart();

        if (isWaiting)
        {
            UpdateCartRollAudio(false);
            waitRemaining -= Time.deltaTime;
            if (waitRemaining <= 0f)
                ChooseNextDestination();
            return;
        }

        bool moving = !agent.isStopped && agent.velocity.sqrMagnitude > 0.04f;
        SetWalking(moving);
        UpdateCartRollAudio(moving);

        if (agent.pathPending)
            return;

        if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete ||
            agent.remainingDistance <= agent.stoppingDistance + 0.12f)
        {
            BeginPause();
        }
    }

    private bool PlaceAgentOnNavMesh()
    {
        if (agent.isOnNavMesh)
            return true;

        NavMeshHit hit;
        if (!NavMesh.SamplePosition(transform.position, out hit, 3f, agent.areaMask))
            return false;

        return agent.Warp(hit.position);
    }

    private void ChooseNextDestination()
    {
        EnsureDestinationPath();
        if (agent == null || !agent.isOnNavMesh || shoppingCart == null)
        {
            BeginPause();
            return;
        }

        agent.speed = walkingSpeed;
        agent.isStopped = false;
        isWaiting = false;

        float minimumDistance = Mathf.Min(minimumDestinationDistance, wanderRadius);
        for (int i = 0; i < destinationAttempts; i++)
        {
            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            if (offset.sqrMagnitude < 0.001f)
                offset = Vector2.right;
            if (offset.magnitude < minimumDistance)
                offset = offset.normalized * minimumDistance;

            Vector3 candidate = transform.position + new Vector3(offset.x, 0f, offset.y);
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(candidate, out hit, destinationSampleRadius, agent.areaMask))
                continue;

            if (!agent.CalculatePath(hit.position, destinationPath) ||
                destinationPath.status != NavMeshPathStatus.PathComplete ||
                destinationPath.corners.Length < 2)
                continue;

            if (agent.SetPath(destinationPath))
                return;
        }

        BeginPause();
    }

    private void EnsureDestinationPath()
    {
        if (destinationPath == null)
            destinationPath = new NavMeshPath();
    }

    private void BeginPause()
    {
        if (agent != null && agent.isOnNavMesh)
            agent.isStopped = true;

        isWaiting = true;
        float shortestPause = Mathf.Min(pauseDuration.x, pauseDuration.y);
        float longestPause = Mathf.Max(pauseDuration.x, pauseDuration.y);
        waitRemaining = Random.Range(shortestPause, longestPause);
        SetWalking(false);
    }

    private void UpdateExternalCart()
    {
        if (shoppingCart == null || shoppingCart == transform || shoppingCart.IsChildOf(transform))
            return;

        Vector3 targetPosition = transform.position + transform.forward * cartFollowDistance;
        targetPosition.y = externalCartHeight;
        shoppingCart.position = Vector3.MoveTowards(
            shoppingCart.position,
            targetPosition,
            cartFollowSpeed * Time.deltaTime);

        Quaternion targetRotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f) *
                                    Quaternion.Euler(externalCartRotationOffset);
        shoppingCart.rotation = Quaternion.RotateTowards(
            shoppingCart.rotation,
            targetRotation,
            240f * Time.deltaTime);
    }

    private void SetWalking(bool walking)
    {
        if (animator != null && hasWalkingParameter && animator.GetBool("Walking") != walking)
            animator.SetBool("Walking", walking);
    }

    private void UpdateCartRollAudio(bool rolling)
    {
        if (shoppingCart != null && GameplaySfxPlayer.Instance != null)
            GameplaySfxPlayer.Instance.SetCartRolling(
                shoppingCart,
                rolling,
                agent != null ? agent.velocity.magnitude : 0f);
    }

    /// <summary>Bind this shopper to an already-instantiated cart model in the scene.</summary>
    public void SetShoppingCart(Transform cart)
    {
        shoppingCart = cart;
        if (shoppingCart != null && !shoppingCart.IsChildOf(transform))
            externalCartHeight = shoppingCart.position.y;
    }

    /// <summary>Use explicit transforms on the cart when its handle positions are known.</summary>
    public void SetHandGripTargets(Transform left, Transform right)
    {
        leftHandGrip = left;
        rightHandGrip = right;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null)
            return;

        float weight = shoppingCart != null && animator.isHuman ? handIkWeight : 0f;
        ApplyHand(AvatarIKGoal.LeftHand, leftHandGrip, leftHandCartLocalPosition, leftHandCartLocalEuler, weight);
        ApplyHand(AvatarIKGoal.RightHand, rightHandGrip, rightHandCartLocalPosition, rightHandCartLocalEuler, weight);
    }

    private void ApplyHand(AvatarIKGoal goal, Transform grip, Vector3 cartLocalPosition, Vector3 cartLocalEuler, float weight)
    {
        animator.SetIKPositionWeight(goal, weight);
        animator.SetIKRotationWeight(goal, weight);

        if (weight <= 0f)
            return;

        Vector3 position = grip != null ? grip.position : shoppingCart.TransformPoint(cartLocalPosition);
        Quaternion rotation = grip != null
            ? grip.rotation
            : shoppingCart.rotation * Quaternion.Euler(cartLocalEuler);

        animator.SetIKPosition(goal, position);
        animator.SetIKRotation(goal, rotation);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (shoppingCart == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(leftHandGrip != null ? leftHandGrip.position : shoppingCart.TransformPoint(leftHandCartLocalPosition), 0.06f);
        Gizmos.DrawWireSphere(rightHandGrip != null ? rightHandGrip.position : shoppingCart.TransformPoint(rightHandCartLocalPosition), 0.06f);
    }
#endif


public void SuspendForRagdoll()
{
    SetWalking(false);
    UpdateCartRollAudio(false);
    isWaiting = true;
    if (agent != null && agent.enabled)
    {
        if (agent.isOnNavMesh)
            agent.isStopped = true;
        agent.enabled = false;
    }
    if (animator != null)
        animator.enabled = false;
    enabled = false;
}

public bool ResumeFromRagdoll(Vector3 position, Quaternion rotation)
{
    transform.SetPositionAndRotation(position, rotation);
    if (animator != null)
    {
        animator.enabled = true;
        animator.Rebind();
        SetWalking(false);
    }

    if (agent != null)
    {
        agent.enabled = true;
        if (!agent.Warp(position))
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(position, out hit, 3f, agent.areaMask) || !agent.Warp(hit.position))
            {
                enabled = true;
                return false;
            }
            transform.position = hit.position;
        }
        agent.isStopped = false;
    }

    enabled = true;
    if (wanderOnStart)
        ChooseNextDestination();
    else
        SetWalking(false);
    return true;
}
}
