using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using How2Shop.Chaos;

namespace How2Shop.Security
{
    public enum SecurityGuardState
    {
        Idle,
        Suspicious,
        Investigating,
        Searching,
        Chasing,
        Grabbing,
        Escorting,
        Stunned,
        Returning
    }

    [Serializable]
    public sealed class SecurityGuardTier
    {
        [Range(1, 4)] public int wantedLevel = 1;
        [Min(0f)] public float walkingSpeed = 1.15f;
        [Min(0f)] public float pursuitSpeed = 1.45f;
        [Min(0f)] public float detectionRange = 7.5f;
        [Range(1f, 180f)] public float fieldOfView = 70f;
        [Min(0f)] public float reactionTime = 1.0f;
        [Min(0f)] public float memoryDuration = 4.5f;
        [Min(0f)] public float searchDuration = 2.5f;
        [Min(0)] public int searchPoints = 2;
        [Min(0f)] public float searchRadius = 3f;
        [Min(0f)] public float predictionLeadSeconds;
        [Min(0f)] public float catchDistance = 1.15f;
    }

    /// <summary>Entrance guard which reacts to ChaosManager events and uses the baked NavMesh.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class SecurityGuardAI : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField] private Transform entranceAnchor;
        [SerializeField] private Transform playerTarget;
        [SerializeField] private Animator animator;
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private Transform ejectionAnchor;
        [SerializeField] private LayerMask lineOfSightLayers = -1;
        [SerializeField, Min(0f)] private float eyeHeight = 1.55f;
        [SerializeField, Min(0f)] private float playerEyeHeight = 1.1f;
        [SerializeField, Min(0.5f)] private float shelfOcclusionHeight = 2.6f;
        [SerializeField, Min(0.1f)] private float shelfOcclusionRefreshSeconds = 1.5f;

        [Header("Wanted level behaviour")]
        [SerializeField] private SecurityGuardTier[] wantedTiers =
        {
            new SecurityGuardTier { wantedLevel = 1, walkingSpeed = 1.15f, pursuitSpeed = 1.45f, detectionRange = 7.5f, fieldOfView = 70f, reactionTime = 1.0f, memoryDuration = 4.5f, searchDuration = 2.5f, searchPoints = 2, searchRadius = 3f, predictionLeadSeconds = 0f, catchDistance = 1.1f },
            new SecurityGuardTier { wantedLevel = 2, walkingSpeed = 1.4f, pursuitSpeed = 2.6f, detectionRange = 11f, fieldOfView = 82f, reactionTime = 0.65f, memoryDuration = 8f, searchDuration = 5f, searchPoints = 5, searchRadius = 6f, predictionLeadSeconds = 0.12f, catchDistance = 1.15f },
            new SecurityGuardTier { wantedLevel = 3, walkingSpeed = 1.7f, pursuitSpeed = 4.0f, detectionRange = 16f, fieldOfView = 96f, reactionTime = 0.38f, memoryDuration = 13f, searchDuration = 9f, searchPoints = 8, searchRadius = 10f, predictionLeadSeconds = 0.28f, catchDistance = 1.25f },
            new SecurityGuardTier { wantedLevel = 4, walkingSpeed = 1.9f, pursuitSpeed = 5.2f, detectionRange = 21f, fieldOfView = 110f, reactionTime = 0.18f, memoryDuration = 19f, searchDuration = 14f, searchPoints = 12, searchRadius = 14f, predictionLeadSeconds = 0.45f, catchDistance = 1.4f }
        };

        [Header("Navigation and state timing")]
        [SerializeField, Min(0.1f)] private float destinationSampleRadius = 2f;
        [SerializeField, Min(0.1f)] private float destinationRefreshSeconds = 0.45f;
        [SerializeField, Min(0.1f)] private float destinationChangeThreshold = 0.8f;
        [SerializeField, Min(0.1f)] private float detectionCheckSeconds = 0.12f;
        [SerializeField, Min(0f)] private float lostSightGraceSeconds = 0.35f;
        [SerializeField, Min(0f)] private float searchArrivalPause = 0.45f;
        [SerializeField, Min(0.1f)] private float stuckCheckSeconds = 1.2f;
        [SerializeField, Min(0.1f)] private float stuckTimeoutSeconds = 2.4f;
        [SerializeField, Min(0f)] private float minimumStuckMovement = 0.08f;
        [SerializeField, Min(0.1f)] private float navMeshRecoveryInterval = 1.25f;
        [SerializeField, Min(0f)] private float entranceArrivalDistance = 0.65f;

        [Header("Search feedback")]
        [SerializeField] private AudioClip confusedDialogueClip;
        [SerializeField] private AudioClip warningDialogueClip;
        [SerializeField] private AudioClip chaseDialogueClip;
        [SerializeField] private AudioClip spottedDialogueClip;
        [SerializeField] private AudioClip catchDialogueClip;
        [SerializeField] private AudioClip escapeDialogueClip;
        [SerializeField, Min(1f)] private float minimumConfusedDialogueInterval = 6f;
        [SerializeField, Min(1f)] private float maximumConfusedDialogueInterval = 11f;
        [SerializeField, Range(0f, 1f)] private float confusedDialogueVolume = 0.65f;
        [SerializeField, Range(0f, 1f)] private float dialogueVolume = 0.8f;
        [SerializeField, Min(0f)] private float dialogueCooldownSeconds = 2.5f;
        [SerializeField, Range(0f, 0.5f)] private float animatorAgitationPerStar = 0.08f;

        [Header("Catch and escort")]
        [SerializeField, Min(0f)] private float grabHoldSeconds = 0.55f;
        [SerializeField, Min(0f)] private float escortFollowDistance = 0.85f;
        [SerializeField, Min(0f)] private float escortSpeed = 1.15f;
        [SerializeField, Min(0f)] private float escortArrivalDistance = 1.25f;
        [SerializeField, Min(0f)] private float catchCooldownSeconds = 6f;

        [Header("Runtime (read only in play mode)")]
        [SerializeField] private SecurityGuardState currentState = SecurityGuardState.Idle;
        [SerializeField, Range(0, 4)] private int currentWantedLevel;
        [SerializeField] private Vector3 lastKnownPlayerPosition;
        [SerializeField] private Vector3 lastDisturbancePosition;

        private readonly List<Vector3> searchLocations = new List<Vector3>();
        private ChaosImpactTarget[] shelfOcclusionTargets;
        private NavMeshPath pathBuffer;
        private ChaosManager chaosManager;
        private AudioSource guardAudioSource;
        private CharacterController playerController;
        private Vector3 entrancePosition;
        private Vector3 escortDestination;
        private Vector3 requestedDestination;
        private Vector3 previousGuardPosition;
        private float stateDeadline;
        private float nextDetectionAt;
        private float nextDestinationRefreshAt;
        private float nextStuckCheckAt;
        private float nextNavMeshRecoveryAt;
        private float stuckSince = -1f;
        private float visibleSince = -1f;
        private float lastSeenAt = -1000f;
        private float lastDisturbanceAt = -1000f;
        private float attentionUntil;
        private float catchAllowedAt;
        private float searchStartedAt;
        private float nextSearchDialogueAt;
        private float nextSearchMoveAt;
        private float nextDialogueAllowedAt;
        private float nextShelfOcclusionRefreshAt;
        private int searchLocationIndex;
        private bool hasDestination;
        private bool searchingLastKnown;
        private bool searchSweepStarted;
        private bool searchSweepWaiting;
        private bool searchSweepComplete;
        private bool caughtPlayer;
        private bool warnedMissingEjectionAnchor;
        private bool chaosEventsSubscribed;

        public SecurityGuardState CurrentState => currentState;
        public int CurrentWantedLevel => currentWantedLevel;
        public Transform PlayerTarget => playerTarget;
        public Vector3 EjectionPosition => ejectionAnchor != null ? ejectionAnchor.position : entrancePosition;
        public bool IsPlayerCaptured => caughtPlayer;
        public event Action<SecurityGuardState, SecurityGuardState> StateChanged;
        public event Action<SecurityGuardAI> PlayerSpotted;
        public event Action<SecurityGuardAI, Transform> CatchAttempted;
        public event Action<SecurityGuardAI, Transform> PlayerEscorted;

        private void Reset()
        {
            agent = GetComponent<NavMeshAgent>();
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            EnsurePathBuffer();
            if (agent == null)
                agent = GetComponent<NavMeshAgent>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);
            guardAudioSource = GetComponent<AudioSource>();
            if (guardAudioSource == null)
                guardAudioSource = gameObject.AddComponent<AudioSource>();
            guardAudioSource.playOnAwake = false;
            guardAudioSource.loop = false;
            guardAudioSource.spatialBlend = 0.7f;
            guardAudioSource.volume = 1f;
            if (warningDialogueClip == null)
                warningDialogueClip = Resources.Load<AudioClip>("SFX/SFX_CHARACTER_GUARD_WARNING");
            if (chaseDialogueClip == null)
                chaseDialogueClip = Resources.Load<AudioClip>("SFX/SFX_CHARACTER_GUARD_CHASE");
            if (catchDialogueClip == null)
                catchDialogueClip = Resources.Load<AudioClip>("SFX/SFX_CHARACTER_GUARD_CATCH");
            if (escapeDialogueClip == null)
                escapeDialogueClip = Resources.Load<AudioClip>("SFX/SFX_CHARACTER_GUARD_ESCAPE");
            if (spottedDialogueClip == null)
                spottedDialogueClip = warningDialogueClip;
            if (playerTarget == null)
            {
                PlayerMovement foundPlayer = FindAnyObjectByType<PlayerMovement>();
                if (foundPlayer != null)
                    playerTarget = foundPlayer.transform;
            }

            if (playerTarget != null)
                playerController = playerTarget.GetComponent<CharacterController>();

            entrancePosition = entranceAnchor != null ? entranceAnchor.position : transform.position;
            currentWantedLevel = ChaosManager.Instance != null ? ChaosManager.Instance.WantedLevel : 0;
            // Until a sighting occurs, the guard only knows its post, never the player's spawn position.
            lastKnownPlayerPosition = entrancePosition;
            lastDisturbancePosition = entrancePosition;
            previousGuardPosition = transform.position;
        }

        private void OnEnable()
        {
            EnsurePathBuffer();
            SubscribeToChaosManager();
        }

        private void EnsurePathBuffer()
        {
            if (pathBuffer == null)
                pathBuffer = new NavMeshPath();
        }

        private void Start()
        {
            if (agent == null)
                return;

            agent.autoBraking = true;
            agent.updatePosition = true;
            agent.updateRotation = true;
            agent.stoppingDistance = 0.15f;
            agent.acceleration = 10f;
            agent.angularSpeed = 240f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

            if (!PlaceAgentOnNavMesh())
            {
                Debug.LogWarning(name + " could not find the baked NavMesh near its entrance position.", this);
                return;
            }

            GoToEntrance(false);
        }

        private void OnDisable()
        {
            if (chaosManager != null && chaosEventsSubscribed)
            {
                chaosManager.WantedLevelChanged -= HandleWantedLevelChanged;
                chaosManager.ChaosDisturbanceReported -= HandleDisturbanceReported;
                chaosManager.SetSecurityPursuitActive(false);
                chaosManager.SetSecuritySearching(false);
                chaosManager.SetSecurityPlayerLead(false);
                chaosEventsSubscribed = false;
            }
        }

        private void Update()
        {
            if (playerTarget == null)
                FindPlayerAgain();
            if (agent == null || !agent.enabled)
            {
                SetAnimatorMovement(false, false);
                return;
            }
            if (!agent.isOnNavMesh)
            {
                if (Time.time >= nextNavMeshRecoveryAt)
                {
                    nextNavMeshRecoveryAt = Time.time + navMeshRecoveryInterval;
                    if (PlaceAgentOnNavMesh())
                    {
                        if (currentState == SecurityGuardState.Escorting)
                            RouteEscortWithFallback();
                        else if (currentState == SecurityGuardState.Chasing)
                            RequestDestination(lastKnownPlayerPosition, true);
                        else
                            GoToEntrance(currentState == SecurityGuardState.Returning);
                    }
                }
                SetAnimatorMovement(false, false);
                return;
            }

            RefreshWantedLevel();
            UpdateDetection();
            ApplyMovementSpeed();
            UpdateStuckRecovery();

            switch (currentState)
            {
                case SecurityGuardState.Idle:
                    UpdateIdle();
                    break;
                case SecurityGuardState.Suspicious:
                    UpdateSuspicious();
                    break;
                case SecurityGuardState.Investigating:
                    UpdateInvestigating();
                    break;
                case SecurityGuardState.Searching:
                    UpdateSearching();
                    break;
                case SecurityGuardState.Chasing:
                    UpdateChasing();
                    break;
                case SecurityGuardState.Grabbing:
                    UpdateGrabbing();
                    break;
                case SecurityGuardState.Escorting:
                    UpdateEscorting();
                    break;
                case SecurityGuardState.Stunned:
                    UpdateStunned();
                    break;
                case SecurityGuardState.Returning:
                    UpdateReturning();
                    break;
            }

            UpdateAnimator();
        }

        private void HandleWantedLevelChanged(int previousLevel, int newLevel)
        {
            currentWantedLevel = Mathf.Clamp(newLevel, 0, 4);
            if (currentWantedLevel > previousLevel && currentState != SecurityGuardState.Grabbing && currentState != SecurityGuardState.Escorting && currentState != SecurityGuardState.Stunned)
            {
                bool recentlySeen = visibleSince >= 0f && playerTarget != null;
                bool hasRecentDisturbance = Time.time - lastDisturbanceAt <= GetTier().memoryDuration;
                Vector3 clue = recentlySeen
                    ? playerTarget.position
                    : hasRecentDisturbance ? lastDisturbancePosition : lastKnownPlayerPosition;
                if (recentlySeen)
                    lastKnownPlayerPosition = playerTarget.position;
                attentionUntil = Time.time + GetTier().memoryDuration;
                BeginSuspicion(clue, false);
            }

            if (currentState == SecurityGuardState.Suspicious)
                stateDeadline = Time.time + GetTier().reactionTime;
            else if (currentState == SecurityGuardState.Searching)
                stateDeadline = searchStartedAt + GetTier().searchDuration;
        }

        private void HandleDisturbanceReported(ChaosEventType eventType, Vector3 position, float amount)
        {
            lastDisturbancePosition = position;
            lastDisturbanceAt = Time.time;
            attentionUntil = Mathf.Max(attentionUntil, Time.time + Mathf.Max(2f, GetTier().memoryDuration));
            TryPlayDialogue(warningDialogueClip, dialogueVolume);

            if (currentState == SecurityGuardState.Grabbing || currentState == SecurityGuardState.Escorting)
                return;

            BeginSuspicion(position, false);
        }

        private void BeginSuspicion(Vector3 clue, bool wantedLevelRose)
        {
            lastDisturbancePosition = clue;
            if (playerTarget != null && wantedLevelRose && visibleSince >= 0f)
                lastKnownPlayerPosition = GetPredictedPlayerPosition();
            if (currentState == SecurityGuardState.Chasing)
                return;

            SetState(SecurityGuardState.Suspicious);
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = true;
            hasDestination = false;
            stateDeadline = Time.time + GetTier().reactionTime;
            FacePoint(clue);
        }

        private void UpdateDetection()
        {
            if (playerTarget == null || Time.time < nextDetectionAt ||
                currentState == SecurityGuardState.Grabbing || currentState == SecurityGuardState.Escorting || currentState == SecurityGuardState.Stunned)
                return;

            nextDetectionAt = Time.time + Mathf.Max(0.05f, detectionCheckSeconds);
            if (currentWantedLevel <= 0 && Time.time > attentionUntil)
            {
                visibleSince = -1f;
                if (chaosManager != null)
                    chaosManager.SetSecurityPlayerLead(false);
                if (currentState == SecurityGuardState.Chasing && Time.time - lastSeenAt > lostSightGraceSeconds)
                    BeginSearching();
                return;
            }

            bool visible = CanSeePlayer();
            if (visible)
            {
                if (chaosManager != null)
                    chaosManager.SetSecurityPlayerLead(true);
                if (visibleSince < 0f)
                {
                    visibleSince = Time.time;
                    PlayerSpotted?.Invoke(this);
                    TryPlayDialogue(spottedDialogueClip != null ? spottedDialogueClip : warningDialogueClip, dialogueVolume);
                }
                lastSeenAt = Time.time;
                lastKnownPlayerPosition = GetPredictedPlayerPosition();

                float reaction = GetTier().reactionTime;
                if (currentState == SecurityGuardState.Chasing)
                    return;
                if (Time.time - visibleSince >= reaction)
                {
                    SetState(SecurityGuardState.Chasing);
                    hasDestination = false;
                    RequestDestination(lastKnownPlayerPosition, true);
                }
            }
            else
            {
                visibleSince = -1f;
                if (chaosManager != null)
                    chaosManager.SetSecurityPlayerLead(false);
            }
        }

        private bool CanSeePlayer()
        {
            if (playerTarget == null)
                return false;

            SecurityGuardTier tier = GetTier();
            Vector3 origin = transform.position + Vector3.up * eyeHeight;
            Vector3 target = GetPlayerEyePosition();
            Vector3 toTarget = target - origin;
            float distance = toTarget.magnitude;
            if (distance > tier.detectionRange || distance < 0.01f)
                return false;
            if (Vector3.Angle(transform.forward, toTarget) > tier.fieldOfView * 0.5f)
                return false;

            RaycastHit hit;
            if (!Physics.Raycast(origin, toTarget.normalized, out hit, distance + 0.1f, lineOfSightLayers, QueryTriggerInteraction.Ignore))
                return !IsBlockedByShelfBounds(origin, target, distance);

            Transform hitTransform = hit.transform;
            bool hitPlayer = hitTransform == playerTarget || hitTransform.IsChildOf(playerTarget) ||
                             playerTarget.IsChildOf(hitTransform);
            return hitPlayer && !IsBlockedByShelfBounds(origin, target, distance);
        }

        private bool IsBlockedByShelfBounds(Vector3 origin, Vector3 target, float distance)
        {
            if (shelfOcclusionTargets == null || Time.time >= nextShelfOcclusionRefreshAt)
            {
                shelfOcclusionTargets = FindObjectsByType<ChaosImpactTarget>();
                nextShelfOcclusionRefreshAt = Time.time + Mathf.Max(0.1f, shelfOcclusionRefreshSeconds);
            }

            Ray sightRay = new Ray(origin, (target - origin).normalized);
            for (int i = 0; i < shelfOcclusionTargets.Length; i++)
            {
                ChaosImpactTarget shelfTarget = shelfOcclusionTargets[i];
                if (shelfTarget == null || (shelfTarget.EventType != ChaosEventType.KnockProductsOffShelves &&
                                            shelfTarget.EventType != ChaosEventType.KnockOverShelf))
                    continue;

                Collider shelfCollider = shelfTarget.GetComponent<Collider>();
                if (shelfCollider == null || !shelfCollider.enabled)
                    continue;

                Bounds colliderBounds = shelfCollider.bounds;
                float top = Mathf.Max(shelfOcclusionHeight, colliderBounds.max.y);
                Bounds aisleOcclusion = new Bounds(
                    new Vector3(colliderBounds.center.x, top * 0.5f, colliderBounds.center.z),
                    new Vector3(colliderBounds.size.x, top, colliderBounds.size.z));
                float hitDistance;
                if (aisleOcclusion.IntersectRay(sightRay, out hitDistance) &&
                    hitDistance > 0.05f && hitDistance < distance - 0.15f)
                    return true;
            }

            return false;
        }

        private bool HasClearContactLine()
        {
            if (playerTarget == null)
                return false;
            Vector3 origin = transform.position + Vector3.up * eyeHeight;
            Vector3 target = GetPlayerEyePosition();
            Vector3 delta = target - origin;
            if (delta.sqrMagnitude < 0.01f)
                return true;
            RaycastHit hit;
            if (!Physics.Raycast(origin, delta.normalized, out hit, delta.magnitude + 0.1f, lineOfSightLayers, QueryTriggerInteraction.Ignore))
                return true;
            return hit.transform == playerTarget || hit.transform.IsChildOf(playerTarget) || playerTarget.IsChildOf(hit.transform);
        }

        private Vector3 GetPlayerEyePosition()
        {
            if (playerController != null && playerController.enabled)
                return playerController.bounds.center;
            return playerTarget.position + Vector3.up * playerEyeHeight;
        }

        private Vector3 GetPredictedPlayerPosition()
        {
            if (playerTarget == null)
                return lastKnownPlayerPosition;
            float lead = GetTier().predictionLeadSeconds;
            if (lead <= 0f || playerController == null)
                return playerTarget.position;
            Vector3 velocity = playerController.velocity;
            velocity.y = 0f;
            velocity = Vector3.ClampMagnitude(velocity, 7f);
            Vector3 predicted = playerTarget.position + velocity * lead;
            predicted.y = playerTarget.position.y;
            return predicted;
        }

        private void UpdateIdle()
        {
            agent.isStopped = true;
            hasDestination = false;
            if (currentWantedLevel > 0)
                BeginSuspicion(lastDisturbancePosition, false);
        }

        private void UpdateSuspicious()
        {
            agent.isStopped = true;
            FacePoint(lastDisturbancePosition);
            if (Time.time >= stateDeadline)
                BeginInvestigation();
        }

        private void BeginInvestigation()
        {
            SetState(SecurityGuardState.Investigating);
            if (!RequestDestination(lastDisturbancePosition, false))
                BeginSearching();
        }

        private void UpdateInvestigating()
        {
            if (Time.time - lastDisturbanceAt > GetTier().memoryDuration && currentWantedLevel <= 1 && visibleSince < 0f)
            {
                BeginReturning();
                return;
            }

            if (HasReachedDestination())
            {
                lastKnownPlayerPosition = playerTarget != null && visibleSince >= 0f
                    ? GetPredictedPlayerPosition()
                    : lastDisturbancePosition;
                BeginSearching();
            }
        }

        private void BeginSearching()
        {
            SetState(SecurityGuardState.Searching);
            agent.isStopped = false;
            searchStartedAt = Time.time;
            stateDeadline = searchStartedAt + Mathf.Max(0.1f, GetTier().searchDuration);
            searchingLastKnown = RequestDestination(lastKnownPlayerPosition, true);
            searchSweepStarted = false;
            searchSweepWaiting = false;
            searchSweepComplete = false;
            searchLocationIndex = 0;
            searchLocations.Clear();
            if (!searchingLastKnown)
                PrepareNearbySearch();
            ScheduleConfusedDialogue();
        }

        private void UpdateSearching()
        {
            if (Time.time >= stateDeadline)
            {
                BeginReturning();
                return;
            }

            PlayConfusedDialogueIfDue();

            if (searchingLastKnown)
            {
                if (!HasReachedDestination())
                {
                    if (!agent.pathPending && !agent.hasPath)
                    {
                        searchingLastKnown = false;
                        PrepareNearbySearch();
                    }
                    return;
                }

                searchingLastKnown = false;
                PrepareNearbySearch();
                return;
            }

            if (searchSweepComplete || Time.time < nextSearchMoveAt)
                return;

            if (!searchSweepStarted)
            {
                searchSweepStarted = true;
                searchSweepWaiting = false;
                if (!RequestNextSearchLocation())
                    searchSweepComplete = true;
                return;
            }

            if (!searchSweepWaiting)
            {
                if (!HasReachedDestination())
                    return;
                agent.isStopped = true;
                searchSweepWaiting = true;
                nextSearchMoveAt = Time.time + searchArrivalPause;
                return;
            }

            searchSweepWaiting = false;
            searchLocationIndex++;
            if (!RequestNextSearchLocation())
            {
                searchSweepComplete = true;
                agent.isStopped = true;
            }
        }

        private void PrepareNearbySearch()
        {
            BuildSearchLocations(lastKnownPlayerPosition);
            searchLocationIndex = 0;
            searchSweepStarted = false;
            searchSweepWaiting = false;
            searchSweepComplete = searchLocations.Count == 0;
            nextSearchMoveAt = Time.time + searchArrivalPause;
            if (searchSweepComplete && agent != null && agent.isOnNavMesh)
                agent.isStopped = true;
        }

        private bool RequestNextSearchLocation()
        {
            while (searchLocationIndex < searchLocations.Count)
            {
                if (RequestDestination(searchLocations[searchLocationIndex], true))
                    return true;
                searchLocationIndex++;
            }
            return false;
        }

        private void ScheduleConfusedDialogue()
        {
            float minimum = Mathf.Max(1f, minimumConfusedDialogueInterval);
            float maximum = Mathf.Max(minimum, maximumConfusedDialogueInterval);
            nextSearchDialogueAt = Time.time + UnityEngine.Random.Range(minimum, maximum);
        }

        private void PlayConfusedDialogueIfDue()
        {
            if (confusedDialogueClip == null || Time.time < nextSearchDialogueAt)
                return;
            TryPlayDialogue(confusedDialogueClip, confusedDialogueVolume);
            ScheduleConfusedDialogue();
        }

        private void TryPlayDialogue(AudioClip clip, float volume)
        {
            if (clip == null || guardAudioSource == null || Time.time < nextDialogueAllowedAt)
                return;

            nextDialogueAllowedAt = Time.time + Mathf.Max(dialogueCooldownSeconds, clip.length + 0.25f);
            guardAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        private void UpdateChasing()
        {
            if (playerTarget == null)
            {
                BeginSearching();
                return;
            }

            if (visibleSince >= 0f && Time.time >= nextDestinationRefreshAt &&
                (requestedDestination - lastKnownPlayerPosition).sqrMagnitude >= destinationChangeThreshold * destinationChangeThreshold)
                RequestDestination(lastKnownPlayerPosition, false);

            if (Time.time - lastSeenAt > lostSightGraceSeconds)
            {
                BeginSearching();
                return;
            }

            if (Vector3.Distance(transform.position, playerTarget.position) <= GetTier().catchDistance && HasClearContactLine() && Time.time >= catchAllowedAt)
                CatchPlayer();
        }

        private void CatchPlayer()
        {
            if (caughtPlayer || playerTarget == null)
                return;
            caughtPlayer = true;
            TryPlayDialogue(catchDialogueClip, dialogueVolume);
            catchAllowedAt = Time.time + catchCooldownSeconds;
            SetState(SecurityGuardState.Grabbing);
            agent.isStopped = true;
            hasDestination = false;
            bool hasCatchHandler = CatchAttempted != null;
            stateDeadline = hasCatchHandler ? float.PositiveInfinity : Time.time + grabHoldSeconds;
            CatchAttempted?.Invoke(this, playerTarget);
        }

        private void UpdateGrabbing()
        {
            agent.isStopped = true;
            if (playerTarget != null)
                FacePoint(playerTarget.position);
            if (Time.time >= stateDeadline)
                BeginEscort();
        }

        /// <summary>Part 4 can call this after its catch sequence succeeds.</summary>
        public void BeginEscort()
        {
            if (currentState != SecurityGuardState.Grabbing && currentState != SecurityGuardState.Escorting)
                return;
            SetState(SecurityGuardState.Escorting);
            agent.isStopped = false;
            previousGuardPosition = transform.position;
            if (ejectionAnchor == null && !warnedMissingEjectionAnchor)
            {
                warnedMissingEjectionAnchor = true;
                Debug.LogWarning(name + " has no ejection point assigned; escorting to the entrance guard post instead.", this);
            }
            escortDestination = EjectionPosition;
            RouteEscortWithFallback();
        }

        /// <summary>Allows a future catch interaction to release the player back into play.</summary>
        public void ReleasePlayer()
        {
            if (!caughtPlayer)
                return;
            caughtPlayer = false;
            if (playerTarget != null)
                lastKnownPlayerPosition = playerTarget.position;
            BeginSearching();
        }

        /// <summary>Releases an escaped player and briefly stuns the guard before pursuit resumes.</summary>
        public void EscapePlayer(float stunSeconds)
        {
            if (!caughtPlayer)
                return;

            caughtPlayer = false;
            TryPlayDialogue(escapeDialogueClip, dialogueVolume);
            if (playerTarget != null)
                lastKnownPlayerPosition = playerTarget.position;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                agent.ResetPath();
            }
            hasDestination = false;
            stateDeadline = Time.time + Mathf.Max(0.1f, stunSeconds);
            SetState(SecurityGuardState.Stunned);
        }

        public void ApplyCatchImmunity(float seconds)
        {
            catchAllowedAt = Mathf.Max(catchAllowedAt, Time.time + Mathf.Max(0f, seconds));
        }

        private void UpdateEscorting()
        {
            if (playerTarget == null)
            {
                EndEscort();
                return;
            }

            float remainingToExit = Vector3.Distance(transform.position, escortDestination);
            if (remainingToExit <= escortArrivalDistance)
            {
                PlayerEscorted?.Invoke(this, playerTarget);
                EndEscort();
                return;
            }

            if (!hasDestination && Time.time >= nextDestinationRefreshAt)
                RouteEscortWithFallback();

            // The capture controller owns player attachment while subscribed. This fallback
            // keeps the guard's escort usable if the optional Part 4 component is absent.
            if (CatchAttempted == null)
            {
                Vector3 followPoint = transform.position - transform.forward * escortFollowDistance;
                Vector3 toFollowPoint = followPoint - playerTarget.position;
                toFollowPoint.y = 0f;
                if (toFollowPoint.sqrMagnitude > 0.04f)
                {
                    Vector3 move = Vector3.ClampMagnitude(toFollowPoint, escortSpeed * Time.deltaTime);
                    if (playerController != null && playerController.enabled)
                        playerController.Move(move);
                    else
                        playerTarget.position += move;
                }
            }
        }

        private void RouteEscortWithFallback()
        {
            if (RequestDestination(escortDestination, true))
                return;
            if ((escortDestination - entrancePosition).sqrMagnitude > 0.01f)
            {
                escortDestination = entrancePosition;
                RequestDestination(escortDestination, true);
            }
        }

        private void UpdateStunned()
        {
            if (agent != null && agent.isOnNavMesh)
                agent.isStopped = true;
            if (Time.time < stateDeadline)
                return;

            if (currentWantedLevel > 0)
            {
                BeginSearching();
            }
            else
            {
                BeginReturning();
            }
        }

        private void EndEscort()
        {
            caughtPlayer = false;
            BeginReturning();
        }

        private void BeginReturning()
        {
            SetState(SecurityGuardState.Returning);
            agent.isStopped = false;
            RequestDestination(entrancePosition, true);
        }

        private void UpdateReturning()
        {
            if (!hasDestination && Time.time >= nextDestinationRefreshAt)
                RequestDestination(entrancePosition, true);
            if (HasReachedDestination() || Vector3.Distance(transform.position, entrancePosition) <= entranceArrivalDistance)
            {
                agent.isStopped = true;
                FacePoint(entranceAnchor != null ? entranceAnchor.position + entranceAnchor.forward : entrancePosition + Vector3.forward);
                hasDestination = false;
                caughtPlayer = false;
                SetState(SecurityGuardState.Idle);
            }
        }

        private void BuildSearchLocations(Vector3 center)
        {
            EnsurePathBuffer();
            searchLocations.Clear();
            SecurityGuardTier tier = GetTier();
            if (agent == null || tier.searchPoints <= 0)
                return;

            int attempts = Mathf.Max(8, tier.searchPoints * 5);
            for (int i = 0; i < attempts && searchLocations.Count < tier.searchPoints; i++)
            {
                float angle = (i * 137.5f) * Mathf.Deg2Rad;
                float radius = tier.searchRadius * Mathf.Sqrt((i + 1f) / attempts);
                Vector3 candidate = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                NavMeshHit hit;
                if (!NavMesh.SamplePosition(candidate, out hit, destinationSampleRadius * 2f, agent.areaMask))
                    continue;
                if (Vector3.Distance(hit.position, center) < 0.75f || ContainsNearby(searchLocations, hit.position, 1.25f))
                    continue;
                if (!agent.CalculatePath(hit.position, pathBuffer) || pathBuffer.status != NavMeshPathStatus.PathComplete)
                    continue;
                searchLocations.Add(hit.position);
            }

            if (searchLocations.Count == 0 && NavMesh.SamplePosition(center, out NavMeshHit centerHit, destinationSampleRadius * 2f, agent.areaMask))
                searchLocations.Add(centerHit.position);
        }

        private static bool ContainsNearby(List<Vector3> locations, Vector3 candidate, float distance)
        {
            for (int i = 0; i < locations.Count; i++)
            {
                if ((locations[i] - candidate).sqrMagnitude < distance * distance)
                    return true;
            }
            return false;
        }

        private bool RequestDestination(Vector3 destination, bool force)
        {
            EnsurePathBuffer();
            if (agent == null || !agent.isOnNavMesh)
                return false;
            if (!force && hasDestination && Time.time < nextDestinationRefreshAt &&
                (destination - requestedDestination).sqrMagnitude < destinationChangeThreshold * destinationChangeThreshold)
                return true;

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(destination, out hit, destinationSampleRadius, agent.areaMask))
            {
                nextDestinationRefreshAt = Time.time + destinationRefreshSeconds;
                return false;
            }
            if (!agent.CalculatePath(hit.position, pathBuffer) || pathBuffer.status != NavMeshPathStatus.PathComplete || pathBuffer.corners.Length == 0)
            {
                nextDestinationRefreshAt = Time.time + destinationRefreshSeconds;
                return false;
            }
            if (!agent.SetPath(pathBuffer))
            {
                nextDestinationRefreshAt = Time.time + destinationRefreshSeconds;
                return false;
            }

            requestedDestination = hit.position;
            hasDestination = true;
            nextDestinationRefreshAt = Time.time + destinationRefreshSeconds;
            agent.isStopped = false;
            return true;
        }

        private bool HasReachedDestination()
        {
            if (agent == null || !agent.isOnNavMesh || agent.pathPending)
                return false;
            if (!hasDestination)
                return false;
            return !agent.hasPath || agent.remainingDistance <= agent.stoppingDistance + 0.2f;
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

        private void GoToEntrance(bool setReturningState)
        {
            if (setReturningState)
                SetState(SecurityGuardState.Returning);
            if (!RequestDestination(entrancePosition, true))
            {
                agent.isStopped = true;
                Debug.LogWarning(name + " has no complete NavMesh path to its entrance point.", this);
            }
        }

        private void UpdateStuckRecovery()
        {
            if (Time.time < nextStuckCheckAt)
                return;
            nextStuckCheckAt = Time.time + stuckCheckSeconds;

            bool tryingToMove = !agent.isStopped && hasDestination && !agent.pathPending &&
                                agent.remainingDistance > agent.stoppingDistance + 0.45f;
            float moved = Vector3.Distance(transform.position, previousGuardPosition);
            previousGuardPosition = transform.position;
            if (!tryingToMove || moved >= minimumStuckMovement)
            {
                stuckSince = -1f;
                return;
            }

            if (stuckSince < 0f)
                stuckSince = Time.time;
            if (Time.time - stuckSince < stuckTimeoutSeconds)
                return;

            stuckSince = Time.time;
            agent.ResetPath();
            hasDestination = false;
            bool recovered = RequestDestination(requestedDestination, true);
            if (!recovered && currentState == SecurityGuardState.Chasing)
                BeginSearching();
            else if (!recovered && currentState == SecurityGuardState.Searching)
            {
                searchingLastKnown = false;
                PrepareNearbySearch();
            }
            else if (!recovered && currentState == SecurityGuardState.Escorting)
                RouteEscortWithFallback();
        }

        private void ApplyMovementSpeed()
        {
            if (agent == null)
                return;
            bool pursuing = currentState == SecurityGuardState.Chasing || currentState == SecurityGuardState.Grabbing;
            agent.speed = pursuing ? GetTier().pursuitSpeed : GetTier().walkingSpeed;
        }

        private void UpdateAnimator()
        {
            if (animator == null)
                return;
            bool moving = agent != null && !agent.isStopped && agent.velocity.sqrMagnitude > 0.04f;
            bool running = moving && (currentState == SecurityGuardState.Chasing || currentState == SecurityGuardState.Grabbing) &&
                           GetTier().pursuitSpeed >= 3f;
            SetAnimatorMovement(moving && !running, running);
            animator.speed = 1f + Mathf.Max(0, currentWantedLevel - 1) * animatorAgitationPerStar;
        }

        private void SetAnimatorMovement(bool walking, bool running)
        {
            if (animator == null)
                return;
            SetBoolIfPresent("Walking", walking);
            SetBoolIfPresent("Running", running);
        }

        private void SetBoolIfPresent(string parameterName, bool value)
        {
            if (animator == null)
                return;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == Animator.StringToHash(parameterName) && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    if (animator.GetBool(parameterName) != value)
                        animator.SetBool(parameterName, value);
                    return;
                }
            }
        }

        private void SetTriggerIfPresent(string parameterName)
        {
            if (animator == null)
                return;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.nameHash == Animator.StringToHash(parameterName) && parameter.type == AnimatorControllerParameterType.Trigger)
                {
                    animator.SetTrigger(parameterName);
                    return;
                }
            }
        }

        private void SetState(SecurityGuardState next)
        {
            if (currentState == next)
                return;
            SecurityGuardState previous = currentState;
            currentState = next;
            SetBoolIfPresent("Dance 1", next == SecurityGuardState.Stunned);
            if (next == SecurityGuardState.Grabbing)
                SetTriggerIfPresent("Greeting");
            if (next == SecurityGuardState.Chasing)
                TryPlayDialogue(chaseDialogueClip, dialogueVolume);
            if (chaosManager != null)
            {
                bool activePursuit = next == SecurityGuardState.Chasing || next == SecurityGuardState.Grabbing || next == SecurityGuardState.Escorting;
                chaosManager.SetSecurityPursuitActive(activePursuit);
                chaosManager.SetSecuritySearching(next == SecurityGuardState.Searching);
                chaosManager.SetSecurityPlayerLead(visibleSince >= 0f && playerTarget != null && next != SecurityGuardState.Searching && next != SecurityGuardState.Returning && next != SecurityGuardState.Idle);
            }
            StateChanged?.Invoke(previous, next);
        }

        [ContextMenu("Debug/Force Investigation")]
        public void DebugForceInvestigation()
        {
            attentionUntil = Time.time + Mathf.Max(4f, GetTier().memoryDuration);
            BeginSuspicion(lastDisturbancePosition == Vector3.zero ? transform.position + transform.forward * 2f : lastDisturbancePosition, false);
        }

        [ContextMenu("Debug/Force Chase")]
        public void DebugForceChase()
        {
            if (playerTarget == null)
                FindPlayerAgain();
            if (playerTarget == null || caughtPlayer)
                return;
            if (currentWantedLevel == 0)
                currentWantedLevel = 1;
            attentionUntil = Time.time + 30f;
            visibleSince = Time.time;
            lastSeenAt = Time.time;
            lastKnownPlayerPosition = GetPredictedPlayerPosition();
            SetState(SecurityGuardState.Chasing);
            hasDestination = false;
            RequestDestination(lastKnownPlayerPosition, true);
        }

        [ContextMenu("Debug/Force Capture")]
        public void DebugForceCapture()
        {
            if (playerTarget == null)
                FindPlayerAgain();
            CatchPlayer();
        }

        [ContextMenu("Debug/Force Escape")]
        public void DebugForceEscape()
        {
            if (playerTarget == null)
                return;
            SecurityGrabEscapeMinigame minigame = playerTarget.GetComponent<SecurityGrabEscapeMinigame>();
            if (minigame != null)
                minigame.DebugForceEscape();
        }

        [ContextMenu("Debug/Reset Chaos And Guard")]
        public void DebugResetSystem()
        {
            if (playerTarget != null)
            {
                SecurityGrabEscapeMinigame minigame = playerTarget.GetComponent<SecurityGrabEscapeMinigame>();
                if (minigame != null)
                    minigame.DebugResetCapture();
            }
            if (chaosManager != null)
                chaosManager.ResetChaos();
            caughtPlayer = false;
            visibleSince = -1f;
            attentionUntil = 0f;
            if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = false;
                agent.ResetPath();
            }
            GoToEntrance(true);
        }

        private void FacePoint(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                return;
            Quaternion target = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, agent != null ? agent.angularSpeed * Time.deltaTime : 180f * Time.deltaTime);
        }

        private SecurityGuardTier GetTier()
        {
            int wanted = Mathf.Clamp(currentWantedLevel, 1, 4);
            if (wantedTiers != null)
            {
                for (int i = 0; i < wantedTiers.Length; i++)
                {
                    if (wantedTiers[i] != null && wantedTiers[i].wantedLevel == wanted)
                        return wantedTiers[i];
                }
                if (wantedTiers.Length > 0 && wantedTiers[0] != null)
                    return wantedTiers[Mathf.Clamp(wanted - 1, 0, wantedTiers.Length - 1)];
            }
            return new SecurityGuardTier { wantedLevel = wanted };
        }

        private void RefreshWantedLevel()
        {
            if (chaosManager == null)
                SubscribeToChaosManager();
            if (chaosManager != null && currentWantedLevel != chaosManager.WantedLevel)
                HandleWantedLevelChanged(currentWantedLevel, chaosManager.WantedLevel);
        }

        private void SubscribeToChaosManager()
        {
            if (chaosEventsSubscribed)
                return;
            chaosManager = ChaosManager.Instance;
            if (chaosManager == null)
                return;
            currentWantedLevel = chaosManager.WantedLevel;
            chaosManager.WantedLevelChanged += HandleWantedLevelChanged;
            chaosManager.ChaosDisturbanceReported += HandleDisturbanceReported;
            chaosManager.SetSecurityPursuitActive(currentState == SecurityGuardState.Chasing || currentState == SecurityGuardState.Grabbing || currentState == SecurityGuardState.Escorting);
            chaosManager.SetSecuritySearching(currentState == SecurityGuardState.Searching);
            chaosManager.SetSecurityPlayerLead(false);
            chaosEventsSubscribed = true;
        }

        private void FindPlayerAgain()
        {
            PlayerMovement found = FindAnyObjectByType<PlayerMovement>();
            if (found == null)
                return;
            playerTarget = found.transform;
            playerController = found.GetComponent<CharacterController>();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 origin = transform.position + Vector3.up * eyeHeight;
            SecurityGuardTier tier = GetTier();
            Gizmos.color = new Color(1f, 0.75f, 0.15f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, tier.detectionRange);
            if (entranceAnchor != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(entranceAnchor.position, 0.35f);
            }
            Gizmos.color = currentState == SecurityGuardState.Chasing ? Color.red : Color.yellow;
            Gizmos.DrawLine(origin, origin + transform.forward * tier.detectionRange);
            if (currentState == SecurityGuardState.Searching)
            {
                Gizmos.color = Color.magenta;
                for (int i = 0; i < searchLocations.Count; i++)
                    Gizmos.DrawWireSphere(searchLocations[i], 0.3f);
            }
        }
#endif
    }
}
