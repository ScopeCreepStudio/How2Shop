using System;
using System.Collections.Generic;
using How2Shop.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace How2Shop.Chaos
{
    public enum ChaosEventType
    {
        ItemThrown,
        ItemHitShopper,
        PlayerWalkIntoShopper,
        PlayerSprintIntoShopper,
        PushTrolleyIntoShopper,
        BreakProduct,
        KnockProductsOffShelves,
        KnockOverShelf,
        ItemHitSecurityGuard,
        DamageCheckoutEquipment,
        AttemptedTheft
    }

    [Serializable]
    public sealed class ChaosEventConfig
    {
        public ChaosEventType eventType;
        [Min(0f)] public float chaosAmount;
        [Min(0f)] public float minimumImpactSpeed;
        [Min(0f)] public float perObjectCooldown = 1f;
    }

    /// <summary>Central, security-independent owner of the game's chaos and wanted state.</summary>
    [DisallowMultipleComponent]
    public sealed class ChaosManager : MonoBehaviour
    {
        private const float MaximumChaos = 100f;
        private static readonly float[] WantedThresholds = { 10f, 25f, 50f, 75f };

        [Header("Event values (editable)")]
        [SerializeField] private ChaosEventConfig[] eventConfigs =
        {
            new ChaosEventConfig { eventType = ChaosEventType.ItemThrown, chaosAmount = 3f, minimumImpactSpeed = 0f, perObjectCooldown = 0.25f },
            new ChaosEventConfig { eventType = ChaosEventType.ItemHitShopper, chaosAmount = 15f, minimumImpactSpeed = 4f, perObjectCooldown = 1f },
            new ChaosEventConfig { eventType = ChaosEventType.PlayerWalkIntoShopper, chaosAmount = 5f, minimumImpactSpeed = 0.4f, perObjectCooldown = 1f },
            new ChaosEventConfig { eventType = ChaosEventType.PlayerSprintIntoShopper, chaosAmount = 10f, minimumImpactSpeed = 2f, perObjectCooldown = 1f },
            new ChaosEventConfig { eventType = ChaosEventType.PushTrolleyIntoShopper, chaosAmount = 12f, minimumImpactSpeed = 2.5f, perObjectCooldown = 1f },
            new ChaosEventConfig { eventType = ChaosEventType.BreakProduct, chaosAmount = 8f, minimumImpactSpeed = 2.5f, perObjectCooldown = 1f },
            new ChaosEventConfig { eventType = ChaosEventType.KnockProductsOffShelves, chaosAmount = 5f, minimumImpactSpeed = 2f, perObjectCooldown = 1f },
            new ChaosEventConfig { eventType = ChaosEventType.KnockOverShelf, chaosAmount = 20f, minimumImpactSpeed = 3f, perObjectCooldown = 1.5f },
            new ChaosEventConfig { eventType = ChaosEventType.ItemHitSecurityGuard, chaosAmount = 25f, minimumImpactSpeed = 4f, perObjectCooldown = 1f },
            new ChaosEventConfig { eventType = ChaosEventType.DamageCheckoutEquipment, chaosAmount = 15f, minimumImpactSpeed = 3f, perObjectCooldown = 1f },
                new ChaosEventConfig { eventType = ChaosEventType.AttemptedTheft, chaosAmount = 30f, minimumImpactSpeed = 0f, perObjectCooldown = 3f }
        };

        [Header("Decay")]
        [SerializeField, Min(0f)] private float decayDelayAfterEvent = 5f;
        [Tooltip("Absolute decay rate for 0, 1, 2, 3, and 4 stars, in chaos points per second.")]
        [SerializeField] private float[] wantedLevelDecayRates = { 3f, 3f, 2f, 1.25f, 0.75f };
        [SerializeField, Min(0f)] private float searchingDecayMultiplier = 0.6f;
        [SerializeField, Min(0f)] private float noLeadDecayMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float wantedLevelHysteresis = 2f;

        [Header("Fragile products")]
        [SerializeField, Min(0f)] private float fragileProductBreakImpactSpeed = 16.5f;

        [Header("Runtime state")]
        [SerializeField, Range(0f, 100f)] private float currentChaos;
        [SerializeField, Range(0, 4)] private int wantedLevel;
        [SerializeField] private bool securityPursuitActive;
        [SerializeField] private bool securitySearching;
        [SerializeField] private bool securityHasPlayerLead;

        [Header("Inspector debug")]
        [SerializeField, Range(0f, 100f)] private float debugChaosValue = 50f;

        private readonly Dictionary<CooldownKey, float> nextAllowedReportTime = new Dictionary<CooldownKey, float>();
        private float lastChaosEventTime;

        private static ChaosManager instance;

        public static ChaosManager Instance
        {
            get
            {
                if (instance == null)
                    instance = FindAnyObjectByType<ChaosManager>();
                return instance;
            }
            private set => instance = value;
        }

        public float CurrentChaos => currentChaos;
        public int WantedLevel => wantedLevel;
        public bool IsSecurityPursuitActive => securityPursuitActive;
        public float FragileProductBreakImpactSpeed => fragileProductBreakImpactSpeed;

        public event Action<float, float> ChaosChanged;
        public event Action<int, int> WantedLevelChanged;
        public event Action<ChaosEventType, float> ChaosEventReported;
        public event Action<ChaosEventType, Vector3, float> ChaosDisturbanceReported;

        private struct CooldownKey : IEquatable<CooldownKey>
        {
            private readonly ChaosEventType eventType;
            private readonly EntityId targetId;

            public CooldownKey(ChaosEventType eventType, EntityId targetId)
            {
                this.eventType = eventType;
                this.targetId = targetId;
            }

            public bool Equals(CooldownKey other) => eventType == other.eventType && targetId.Equals(other.targetId);
            public override bool Equals(object obj) => obj is CooldownKey other && Equals(other);
            public override int GetHashCode() => ((int)eventType * 397) ^ targetId.GetHashCode();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            lastChaosEventTime = Time.time;
            EnsureEventConfigs();
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            InstallSceneImpactTargets();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (instance == this)
                instance = null;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => InstallSceneImpactTargets();

        private void InstallSceneImpactTargets()
        {
            ItemSpriteView[] products = FindObjectsByType<ItemSpriteView>();
            for (int i = 0; i < products.Length; i++)
            {
                ItemSpriteView product = products[i];
                if (product == null || product.GetComponent<Collider>() == null && product.GetComponentInChildren<Collider>() == null)
                    continue;
                if (product.Item == null || !product.Item.HasTag("fragile"))
                    continue;

                ChaosImpactTarget target = product.GetComponent<ChaosImpactTarget>();
                if (target != null)
                {
                    if (target.EventType == ChaosEventType.BreakProduct)
                        target.Configure(ChaosEventType.BreakProduct, ChaosImpactSource.ThrownItem,
                            fragileProductBreakImpactSpeed, true);
                    continue;
                }

                target = product.gameObject.AddComponent<ChaosImpactTarget>();
                target.Configure(ChaosEventType.BreakProduct, ChaosImpactSource.ThrownItem,
                    fragileProductBreakImpactSpeed, true);
            }

            Collider[] colliders = FindObjectsByType<Collider>();
            var configuredShelfObjects = new HashSet<GameObject>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider == null)
                    continue;

                bool looksLikeShelf = collider.gameObject.name.IndexOf("shelf", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      collider.gameObject.name.IndexOf("rack", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!looksLikeShelf)
                    continue;

                Rigidbody attachedBody = collider.attachedRigidbody;
                GameObject targetObject = attachedBody != null ? attachedBody.gameObject : collider.gameObject;
                if (!configuredShelfObjects.Add(targetObject) || targetObject.GetComponent<ChaosImpactTarget>() != null)
                    continue;

                ChaosImpactTarget target = targetObject.AddComponent<ChaosImpactTarget>();
                ChaosEventType type = attachedBody != null && !attachedBody.isKinematic
                    ? ChaosEventType.KnockOverShelf
                    : ChaosEventType.KnockProductsOffShelves;
                float threshold = type == ChaosEventType.KnockOverShelf ? 3f : 2f;
                target.Configure(type, ChaosImpactSource.ThrownItem | ChaosImpactSource.PlayerTrolley, threshold, false);
            }
        }

        private void Update()
        {
            if (securityPursuitActive || currentChaos <= 0f || Time.time - lastChaosEventTime < decayDelayAfterEvent)
                return;

            float decayRate = GetDecayRate(wantedLevel);
            if (securitySearching)
                decayRate *= searchingDecayMultiplier;
            else if (!securityHasPlayerLead)
                decayRate *= noLeadDecayMultiplier;
            SetChaosInternal(currentChaos - decayRate * Time.deltaTime);
        }

        /// <summary>Add positive chaos and reset the inactivity decay timer.</summary>
        public void AddChaos(float amount)
        {
            if (amount <= 0f)
                return;

            lastChaosEventTime = Time.time;
            SetChaosInternal(currentChaos + amount);
        }

        /// <summary>Remove chaos without treating the reduction as a new trouble event.</summary>
        public void RemoveChaos(float amount)
        {
            if (amount <= 0f)
                return;

            SetChaosInternal(currentChaos - amount);
        }

        /// <summary>Reports a configured event if its impact threshold and cooldown allow it.</summary>
        public bool ReportEvent(ChaosEventType eventType, GameObject target = null, GameObject instigator = null, float impactSpeed = 0f)
        {
            ChaosEventConfig config = GetConfig(eventType);
            if (config == null || config.chaosAmount <= 0f || impactSpeed < config.minimumImpactSpeed)
                return false;

            EntityId targetId = target != null
                ? target.GetEntityId()
                : (instigator != null ? instigator.GetEntityId() : EntityId.None);
            CooldownKey key = new CooldownKey(eventType, targetId);
            float now = Time.time;
            float nextAllowed;
            if (nextAllowedReportTime.TryGetValue(key, out nextAllowed) && now < nextAllowed)
                return false;

            nextAllowedReportTime[key] = now + config.perObjectCooldown;
            AddChaos(config.chaosAmount);
            ChaosEventReported?.Invoke(eventType, config.chaosAmount);

            GameObject disturbanceObject = target != null ? target : instigator;
            Vector3 disturbancePosition = disturbanceObject != null
                ? disturbanceObject.transform.position
                : transform.position;
            ChaosDisturbanceReported?.Invoke(eventType, disturbancePosition, config.chaosAmount);
            return true;
        }

        /// <summary>Debug override for chaos; values are clamped to 0-100.</summary>
        public void SetChaosForDebug(float value)
        {
            float clamped = Mathf.Clamp(value, 0f, MaximumChaos);
            if (clamped > currentChaos)
                lastChaosEventTime = Time.time;
            SetChaosInternal(clamped);
        }

        public void SetWantedLevelForDebug(int level)
        {
            int clampedLevel = Mathf.Clamp(level, 0, 4);
            float[] representativeValues = { 0f, 15f, 37f, 62f, 87f };
            lastChaosEventTime = Time.time;
            int oldLevel = wantedLevel;
            float oldChaos = currentChaos;
            currentChaos = representativeValues[clampedLevel];
            wantedLevel = clampedLevel;
            if (!Mathf.Approximately(oldChaos, currentChaos))
                ChaosChanged?.Invoke(oldChaos, currentChaos);
            if (oldLevel != wantedLevel)
                WantedLevelChanged?.Invoke(oldLevel, wantedLevel);
        }

        public void ResetChaos()
        {
            lastChaosEventTime = Time.time;
            nextAllowedReportTime.Clear();
            SetChaosInternal(0f);
        }

        /// <summary>Security AI can pause decay through this API without being required by the meter.</summary>
        public void SetSecurityPursuitActive(bool active)
        {
            securityPursuitActive = active;
        }

        /// <summary>Lets the guard apply the slower decay rate while methodically searching.</summary>
        public void SetSecuritySearching(bool active)
        {
            securitySearching = active;
        }

        /// <summary>True only while the guard currently has direct visual contact with the player.</summary>
        public void SetSecurityPlayerLead(bool hasLead)
        {
            securityHasPlayerLead = hasLead;
        }

        private void SetChaosInternal(float value)
        {
            float oldChaos = currentChaos;
            currentChaos = Mathf.Clamp(value, 0f, MaximumChaos);
            if (!Mathf.Approximately(oldChaos, currentChaos))
                ChaosChanged?.Invoke(oldChaos, currentChaos);

            int oldLevel = wantedLevel;
            wantedLevel = CalculateWantedLevel(currentChaos, wantedLevel, wantedLevelHysteresis);
            if (oldLevel != wantedLevel)
                WantedLevelChanged?.Invoke(oldLevel, wantedLevel);
        }

        private ChaosEventConfig GetConfig(ChaosEventType eventType)
        {
            if (eventConfigs == null)
                return null;

            for (int i = 0; i < eventConfigs.Length; i++)
            {
                if (eventConfigs[i] != null && eventConfigs[i].eventType == eventType)
                    return eventConfigs[i];
            }
            return null;
        }

        private float GetDecayRate(int level)
        {
            if (wantedLevelDecayRates == null || wantedLevelDecayRates.Length == 0)
                return 0f;
            return Mathf.Max(0f, wantedLevelDecayRates[Mathf.Clamp(level, 0, wantedLevelDecayRates.Length - 1)]);
        }

        private void EnsureEventConfigs()
        {
            if (eventConfigs != null && eventConfigs.Length == Enum.GetValues(typeof(ChaosEventType)).Length)
                return;

            ChaosEventConfig[] defaults = CreateDefaultConfigs();
            if (eventConfigs == null || eventConfigs.Length == 0)
            {
                eventConfigs = defaults;
                return;
            }

            foreach (ChaosEventConfig config in defaults)
            {
                if (GetConfig(config.eventType) == null)
                {
                    Array.Resize(ref eventConfigs, eventConfigs.Length + 1);
                    eventConfigs[eventConfigs.Length - 1] = config;
                }
            }
        }

        private static ChaosEventConfig[] CreateDefaultConfigs()
        {
            return new[]
            {
                new ChaosEventConfig { eventType = ChaosEventType.ItemThrown, chaosAmount = 3f, minimumImpactSpeed = 0f, perObjectCooldown = 0.25f },
                new ChaosEventConfig { eventType = ChaosEventType.ItemHitShopper, chaosAmount = 15f, minimumImpactSpeed = 4f, perObjectCooldown = 1f },
                new ChaosEventConfig { eventType = ChaosEventType.PlayerWalkIntoShopper, chaosAmount = 5f, minimumImpactSpeed = 0.4f, perObjectCooldown = 1f },
                new ChaosEventConfig { eventType = ChaosEventType.PlayerSprintIntoShopper, chaosAmount = 10f, minimumImpactSpeed = 2f, perObjectCooldown = 1f },
                new ChaosEventConfig { eventType = ChaosEventType.PushTrolleyIntoShopper, chaosAmount = 12f, minimumImpactSpeed = 2.5f, perObjectCooldown = 1f },
                new ChaosEventConfig { eventType = ChaosEventType.BreakProduct, chaosAmount = 8f, minimumImpactSpeed = 2.5f, perObjectCooldown = 1f },
                new ChaosEventConfig { eventType = ChaosEventType.KnockProductsOffShelves, chaosAmount = 5f, minimumImpactSpeed = 2f, perObjectCooldown = 1f },
                new ChaosEventConfig { eventType = ChaosEventType.KnockOverShelf, chaosAmount = 20f, minimumImpactSpeed = 3f, perObjectCooldown = 1.5f },
                new ChaosEventConfig { eventType = ChaosEventType.ItemHitSecurityGuard, chaosAmount = 25f, minimumImpactSpeed = 4f, perObjectCooldown = 1f },
                new ChaosEventConfig { eventType = ChaosEventType.DamageCheckoutEquipment, chaosAmount = 15f, minimumImpactSpeed = 3f, perObjectCooldown = 1f },
                new ChaosEventConfig { eventType = ChaosEventType.AttemptedTheft, chaosAmount = 30f, minimumImpactSpeed = 0f, perObjectCooldown = 3f }
            };
        }

        private static int CalculateWantedLevel(float chaos, int currentLevel, float hysteresis)
        {
            int level = Mathf.Clamp(currentLevel, 0, 4);
            while (level < 4 && chaos >= WantedThresholds[level])
                level++;

            while (level > 0 && chaos < WantedThresholds[level - 1] - Mathf.Max(0f, hysteresis))
                level--;

            return level;
        }

        [ContextMenu("Debug/Add 10 Chaos")]
        private void DebugAdd10Chaos() => AddChaos(10f);

        [ContextMenu("Debug/Remove 10 Chaos")]
        private void DebugRemove10Chaos() => RemoveChaos(10f);

        [ContextMenu("Debug/Set Wanted Level/0 Stars")]
        private void DebugSetWanted0() => SetWantedLevelForDebug(0);

        [ContextMenu("Debug/Set Wanted Level/1 Star")]
        private void DebugSetWanted1() => SetWantedLevelForDebug(1);

        [ContextMenu("Debug/Set Wanted Level/2 Stars")]
        private void DebugSetWanted2() => SetWantedLevelForDebug(2);

        [ContextMenu("Debug/Set Wanted Level/3 Stars")]
        private void DebugSetWanted3() => SetWantedLevelForDebug(3);

        [ContextMenu("Debug/Set Wanted Level/4 Stars")]
        private void DebugSetWanted4() => SetWantedLevelForDebug(4);

        [ContextMenu("Debug/Reset Chaos")]
        private void DebugResetChaos() => ResetChaos();

        [ContextMenu("Debug/Set Chaos To Inspector Value")]
        private void DebugSetChaosToInspectorValue() => SetChaosForDebug(debugChaosValue);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInstanceExists()
        {
            if (Instance != null || FindAnyObjectByType<ChaosManager>() != null)
                return;

            // The MainMenu should not create a default manager that would win the
            // singleton race against the Inspector-configured instance in MainScene.
            if (FindAnyObjectByType<PlayerMovement>() == null)
                return;

            GameObject managerObject = new GameObject("Chaos Manager");
            managerObject.AddComponent<ChaosManager>();
        }
    }
}
