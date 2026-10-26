using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

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

    [Header("Shopper audio")]
    [SerializeField] private AudioClip cartRollClip;
    [SerializeField] private AudioClip impactClip;
    [SerializeField] private AudioClip fallClip;
    [SerializeField] private AudioClip fallingScreamClip;
    [SerializeField] private AudioClip landingClip;
    [SerializeField] private AudioClip recoveryClip;

    [Header("Shopper voice lines")]
    [SerializeField] private bool voiceLinesEnabled = true;
    [SerializeField, Min(1f)] private float passingPlayerRadius = 5.5f;
    [SerializeField, Min(1f)] private float voiceHearingRadius = 12f;
    [SerializeField, Min(1f)] private float blockingDistance = 3f;
    [SerializeField, Min(0.25f)] private float blockingLineDelay = 2.25f;
    [SerializeField, Min(0f)] private float blockingVoiceCooldown = 9f;
    [SerializeField, Min(0f)] private float collisionVoiceCooldown = 3f;
    [SerializeField] private Vector2 idleVoiceInterval = new Vector2(16f, 28f);

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
    private AudioSource cartAudio;
    private AudioSource effectsAudio;
    private AudioSource voiceAudio;
    private bool ragdollLandingPlayed;
    private AudioClip[] blockingVoiceLines = Array.Empty<AudioClip>();
    private AudioClip[] collisionVoiceLines = Array.Empty<AudioClip>();
    private AudioClip[] idleVoiceLines = Array.Empty<AudioClip>();
    private AudioClip[] passingVoiceLines = Array.Empty<AudioClip>();
    private PlayerMovement playerMovement;
    private AudioClip lastVoiceLine;
    private bool wasPlayerNear;
    private bool pendingPassingVoice;
    private float blockedPlayerDuration;
    private float nextCollisionVoiceAt;
    private float nextBlockingVoiceAt;
    private float nextPassingVoiceAt;
    private float nextIdleVoiceAt;

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

    cartAudio = gameObject.AddComponent<AudioSource>();
    cartAudio.clip = cartRollClip;
    cartAudio.loop = true;
    cartAudio.playOnAwake = false;
    cartAudio.spatialBlend = 1f;
    cartAudio.maxDistance = 24f;
    if (cartRollClip != null) cartAudio.Play();
    effectsAudio = gameObject.AddComponent<AudioSource>();
    effectsAudio.playOnAwake = false;
    effectsAudio.spatialBlend = 1f;
    effectsAudio.maxDistance = 30f;

    voiceAudio = gameObject.AddComponent<AudioSource>();
    voiceAudio.playOnAwake = false;
    voiceAudio.loop = false;
    voiceAudio.spatialBlend = 1f;
    voiceAudio.volume = 0.9f;
    voiceAudio.minDistance = 1.5f;
    voiceAudio.maxDistance = voiceHearingRadius;
    voiceAudio.rolloffMode = AudioRolloffMode.Logarithmic;
    voiceAudio.dopplerLevel = 0f;
    LoadVoiceLines();
    nextIdleVoiceAt = Time.time + Random.Range(5f, 12f);
    playerMovement = FindFirstObjectByType<PlayerMovement>();

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
        UpdateVoiceReactions();

        if (!wanderOnStart || shoppingCart == null || agent == null || !agent.isOnNavMesh)
        {
            SetWalking(false);
            UpdateCartRollAudio(false);
            return;
        }

        UpdateExternalCart();
        cartAudio.volume = Mathf.Clamp01(agent.velocity.magnitude / 2f) * 0.55f;

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

    private void LoadVoiceLines()
    {
        if (!voiceLinesEnabled)
            return;

        AudioClip[] allLines = Resources.LoadAll<AudioClip>("Shopper1_Voice_Clips");
        blockingVoiceLines = FilterVoiceLines(allLines, "BlockingIsle_");
        collisionVoiceLines = FilterVoiceLines(allLines, "Collisions_");
        idleVoiceLines = FilterVoiceLines(allLines, "IdleConversations_");
        passingVoiceLines = FilterVoiceLines(allLines, "PassingPlayer_");

        if (allLines.Length == 0)
            Debug.LogWarning(name + " could not load voice lines from Resources/Shopper1_Voice_Clips.", this);
    }

    private static AudioClip[] FilterVoiceLines(AudioClip[] source, string prefix)
    {
        var matches = new List<AudioClip>();
        for (int i = 0; i < source.Length; i++)
        {
            AudioClip clip = source[i];
            if (clip != null && clip.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                matches.Add(clip);
        }

        matches.Sort((left, right) => string.Compare(left.name, right.name, StringComparison.OrdinalIgnoreCase));
        return matches.ToArray();
    }

    private void UpdateVoiceReactions()
    {
        if (!voiceLinesEnabled || voiceAudio == null)
            return;

        if (playerMovement == null)
            playerMovement = FindFirstObjectByType<PlayerMovement>();
        if (playerMovement == null || !playerMovement.isActiveAndEnabled)
            return;

        float playerDistance = Vector3.Distance(transform.position, playerMovement.transform.position);
        bool insidePassingRadius = playerDistance <= passingPlayerRadius;
        if (insidePassingRadius && !wasPlayerNear)
            pendingPassingVoice = true;

        if (!insidePassingRadius && playerDistance > passingPlayerRadius + 1.5f)
        {
            wasPlayerNear = false;
            pendingPassingVoice = false;
        }
        else if (insidePassingRadius)
        {
            wasPlayerNear = true;
        }

        if (pendingPassingVoice && insidePassingRadius &&
            TryPlayVoiceLine(passingVoiceLines, ref nextPassingVoiceAt, 12f, false, 0.9f))
            pendingPassingVoice = false;

        if (playerDistance <= voiceHearingRadius && IsShopperIdleForVoice() &&
            Time.time >= nextIdleVoiceAt &&
            TryPlayVoiceLine(idleVoiceLines, ref nextIdleVoiceAt, 0f, false, 0.75f))
        {
            float shortest = Mathf.Min(idleVoiceInterval.x, idleVoiceInterval.y);
            float longest = Mathf.Max(idleVoiceInterval.x, idleVoiceInterval.y);
            nextIdleVoiceAt = Time.time + Random.Range(shortest, longest);
        }

        if (IsPlayerBlockingPath())
            blockedPlayerDuration += Time.deltaTime;
        else
            blockedPlayerDuration = 0f;

        if (blockedPlayerDuration >= blockingLineDelay &&
            TryPlayVoiceLine(blockingVoiceLines, ref nextBlockingVoiceAt, blockingVoiceCooldown, false, 0.95f))
            blockedPlayerDuration = 0f;
    }

    private bool IsShopperIdleForVoice()
    {
        if (isWaiting)
            return true;

        return agent != null && agent.enabled && agent.isOnNavMesh && !agent.pathPending &&
               !agent.hasPath && agent.velocity.sqrMagnitude < 0.04f;
    }

    private bool IsPlayerBlockingPath()
    {
        if (playerMovement == null || agent == null || !agent.enabled || !agent.isOnNavMesh ||
            !agent.hasPath || agent.pathPending || agent.isStopped || isWaiting ||
            agent.remainingDistance <= agent.stoppingDistance + 0.35f)
            return false;

        Vector3 toPlayer = playerMovement.transform.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        if (distance < 0.1f || distance > blockingDistance)
            return false;

        Vector3 heading = agent.steeringTarget - transform.position;
        heading.y = 0f;
        if (heading.sqrMagnitude < 0.01f)
            heading = transform.forward;
        heading.Normalize();

        Vector3 towardPlayer = toPlayer / distance;
        bool playerIsAhead = Vector3.Dot(heading, towardPlayer) > 0.55f &&
                             Vector3.Cross(heading, towardPlayer).magnitude < 0.9f;
        return playerIsAhead && agent.velocity.sqrMagnitude < 0.09f;
    }

    private bool TryPlayVoiceLine(
        AudioClip[] lines,
        ref float nextAllowedAt,
        float cooldown,
        bool interruptCurrent,
        float volume)
    {
        if (!voiceLinesEnabled || voiceAudio == null || lines == null || lines.Length == 0 ||
            Time.time < nextAllowedAt)
            return false;

        if (voiceAudio.isPlaying)
        {
            if (!interruptCurrent)
                return false;
            voiceAudio.Stop();
        }

        AudioClip selected = lines[Random.Range(0, lines.Length)];
        if (selected == lastVoiceLine && lines.Length > 1)
            selected = lines[(Array.IndexOf(lines, selected) + 1) % lines.Length];
        if (selected == null)
            return false;

        voiceAudio.PlayOneShot(selected, volume);
        lastVoiceLine = selected;
        nextAllowedAt = Time.time + cooldown;
        return true;
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
    if (cartAudio != null) cartAudio.volume = 0f;
    ragdollLandingPlayed = false;
    PlayCollisionVoiceLine();
    PlayShopperSound(impactClip, 0.9f);
    PlayShopperSound(fallClip, 0.85f);
    PlayShopperSound(fallingScreamClip, 0.8f);
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

public void NotifyRagdollLanding(float impactSpeed)
{
    if (ragdollLandingPlayed || impactSpeed < 1.1f) return;
    ragdollLandingPlayed = true;
    PlayShopperSound(landingClip, 1f);
}

public void PlayCollisionVoiceLine()
{
    TryPlayVoiceLine(collisionVoiceLines, ref nextCollisionVoiceAt, collisionVoiceCooldown, true, 1f);
}

private void PlayShopperSound(AudioClip clip, float volume)
{
    if (clip != null && effectsAudio != null) effectsAudio.PlayOneShot(clip, volume);
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
    PlayShopperSound(recoveryClip, 0.8f);
    if (wanderOnStart)
        ChooseNextDestination();
    else
        SetWalking(false);
    return true;
}
}
