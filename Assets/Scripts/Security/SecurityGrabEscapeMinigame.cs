using System;
using System.Collections;
using System.Collections.Generic;
using How2Shop.Chaos;
using How2Shop.Items;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace How2Shop.Security
{
    /// <summary>Locks only player movement input, attaches the player safely, and handles the guard escape UI.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class SecurityGrabEscapeMinigame : MonoBehaviour
    {
        [Serializable]
        private sealed class EscapeDifficulty
        {
            [Range(1, 4)] public int wantedLevel = 1;
            [Min(1)] public int requiredAlternatingPresses = 6;
            [Min(0f)] public float progressDecayPointsPerSecond = 1f;
        }

        private struct ColliderIgnorePair
        {
            public Collider player;
            public Collider guard;
            public bool wasIgnored;
        }

        [Header("Scene references")]
        [SerializeField] private SecurityGuardAI securityGuard;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerItemInteraction itemInteraction;
        [SerializeField] private CharacterController playerController;
        [SerializeField] private ChaosManager chaosManager;
        [SerializeField] private Canvas hudCanvas;

        [Header("Capture and movement")]
        [SerializeField, Min(0f)] private float guardGrabPause = 0.45f;
        [SerializeField] private Vector3 guardRelativeGrabPosition = new Vector3(0.95f, 0f, 0f);
        [SerializeField, Min(0.1f)] private float attachmentSpeed = 8f;
        [SerializeField, Min(0f)] private float escapeImmunitySeconds = 5f;
        [SerializeField, Min(0.1f)] private float guardStunSeconds = 2.75f;
        [SerializeField, Min(0.1f)] private float safeReleaseSeconds = 0.35f;
        [SerializeField, Min(0.1f)] private float safeReleaseSpeed = 4.5f;

        [Header("Escape difficulty")]
        [SerializeField] private EscapeDifficulty[] difficultyByWantedLevel =
        {
            new EscapeDifficulty { wantedLevel = 1, requiredAlternatingPresses = 6, progressDecayPointsPerSecond = 1f },
            new EscapeDifficulty { wantedLevel = 2, requiredAlternatingPresses = 9, progressDecayPointsPerSecond = 2.2f },
            new EscapeDifficulty { wantedLevel = 3, requiredAlternatingPresses = 13, progressDecayPointsPerSecond = 4.5f },
            new EscapeDifficulty { wantedLevel = 4, requiredAlternatingPresses = 18, progressDecayPointsPerSecond = 7.5f }
        };
        [SerializeField] private Key leftEscapeKey = Key.A;
        [SerializeField] private Key rightEscapeKey = Key.D;

        [Header("UI and sound")]
        [SerializeField] private string storeName = "TESCO";
        [SerializeField] private AudioClip escapeSound;
        [SerializeField] private AudioClip failureSound;

        private const string LeftBindingPreference = "How2Shop.SecurityEscape.LeftBinding";
        private const string RightBindingPreference = "How2Shop.SecurityEscape.RightBinding";

        private InputAction leftEscapeAction;
        private InputAction rightEscapeAction;
        private IDisposable activeRebind;
        private readonly List<ColliderIgnorePair> ignoredColliderPairs = new List<ColliderIgnorePair>();
        private AudioSource audioSource;
        private Coroutine escortCoroutine;
        private Coroutine releaseCoroutine;
        private Coroutine successEffectRoutine;

        private RectTransform escapePanel;
        private RectTransform failurePanel;
        private RectTransform successPanel;
        private Image progressFill;
        private TMP_Text titleText;
        private TMP_Text instructionText;
        private TMP_Text progressText;
        private TMP_Text rebindHintText;
        private TMP_Text failureTitleText;
        private TMP_Text failureScoreText;
        private Button retryButton;

        private bool subscribed;
        private bool isCaptured;
        private bool failureActive;
        private bool controlsSuppressed;
        private bool previousMovementLock;
        private bool previousItemInteractionEnabled;
        private CursorLockMode previousCursorLockState;
        private bool previousCursorVisible;
        private bool cursorStateSaved;
        private Key lastSuccessfulKey = Key.None;
        private EscapeDifficulty activeDifficulty;
        private float escapeEffort;
        private float immunityUntil;

        public bool IsCaptured => isCaptured;
        public float EscapeProgressPercent => GetProgress01() * 100f;

        private void Awake()
        {
            if (playerMovement == null) playerMovement = GetComponent<PlayerMovement>();
            if (itemInteraction == null) itemInteraction = GetComponent<PlayerItemInteraction>();
            if (playerController == null) playerController = GetComponent<CharacterController>();
            if (chaosManager == null) chaosManager = ChaosManager.Instance;
            if (securityGuard == null) securityGuard = FindAnyObjectByType<SecurityGuardAI>();
            if (hudCanvas == null)
            {
                GameObject existingHud = GameObject.Find("Pickup Controls");
                if (existingHud != null) hudCanvas = existingHud.GetComponent<Canvas>();
                if (hudCanvas == null) hudCanvas = FindAnyObjectByType<Canvas>();
            }

            escapeSound ??= Resources.Load<AudioClip>("SFX/SFX_UI_VICTORY_FANFARE");
            failureSound ??= Resources.Load<AudioClip>("SFX/SFX_UI_SAD_TROMBONE");
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.volume = 0.8f;

            leftEscapeAction = new InputAction("Escape Left", InputActionType.Button, BuildKeyboardPath(leftEscapeKey));
            rightEscapeAction = new InputAction("Escape Right", InputActionType.Button, BuildKeyboardPath(rightEscapeKey));
            LoadBindingOverride(leftEscapeAction, LeftBindingPreference);
            LoadBindingOverride(rightEscapeAction, RightBindingPreference);
            BuildUi();
        }

        private void OnEnable()
        {
            if (leftEscapeAction != null) leftEscapeAction.Enable();
            if (rightEscapeAction != null) rightEscapeAction.Enable();
            SubscribeToGuard();
        }

        private void Start()
        {
            if (securityGuard == null)
                Debug.LogWarning("Security grab escape UI could not find the SecurityGuardAI in this scene.", this);
        }

        private void OnDisable()
        {
            UnsubscribeFromGuard();
            DisposeRebind();
            if (leftEscapeAction != null) leftEscapeAction.Disable();
            if (rightEscapeAction != null) rightEscapeAction.Disable();
            if (escortCoroutine != null) StopCoroutine(escortCoroutine);
            if (releaseCoroutine != null) StopCoroutine(releaseCoroutine);
            if (successEffectRoutine != null) StopCoroutine(successEffectRoutine);
            escortCoroutine = null;
            releaseCoroutine = null;
            successEffectRoutine = null;
            isCaptured = false;
            RestoreIgnoredCollisions();
            RestorePlayerControls();
            if (escapePanel != null) escapePanel.gameObject.SetActive(false);
            if (failurePanel != null) failurePanel.gameObject.SetActive(false);
            if (successPanel != null) successPanel.gameObject.SetActive(false);
            RestoreCursor();
        }

        private void OnDestroy()
        {
            DisposeRebind();
            leftEscapeAction?.Dispose();
            rightEscapeAction?.Dispose();
        }

        private void Update()
        {
            if (!isCaptured || failureActive)
                return;

            if (activeDifficulty == null)
                activeDifficulty = GetDifficulty(securityGuard != null ? securityGuard.CurrentWantedLevel : 1);

            float maximumEffort = Mathf.Max(10f, activeDifficulty.requiredAlternatingPresses * 10f);
            escapeEffort = Mathf.Max(0f, escapeEffort - activeDifficulty.progressDecayPointsPerSecond * Time.deltaTime);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
            {
                if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                    RebindRightEscapeKey();
                else
                    RebindLeftEscapeKey();
                return;
            }

            bool pressedLeft = leftEscapeAction != null && leftEscapeAction.WasPressedThisFrame();
            bool pressedRight = rightEscapeAction != null && rightEscapeAction.WasPressedThisFrame();
            if (pressedLeft != pressedRight)
            {
                Key pressedKey = pressedLeft ? leftEscapeKey : rightEscapeKey;
                if (lastSuccessfulKey == Key.None || pressedKey != lastSuccessfulKey)
                {
                    escapeEffort = Mathf.Min(maximumEffort, escapeEffort + 10f);
                    lastSuccessfulKey = pressedKey;
                }
            }

            UpdateEscapeUi();
            if (GetProgress01() >= 1f)
                EscapeSuccessfully();
        }

        private void LateUpdate()
        {
            if (!isCaptured || failureActive || securityGuard == null || playerMovement == null)
                return;
            if (securityGuard.CurrentState != SecurityGuardState.Grabbing && securityGuard.CurrentState != SecurityGuardState.Escorting)
                return;

            Vector3 target = securityGuard.transform.TransformPoint(guardRelativeGrabPosition);
            target.y = transform.position.y;
            Vector3 delta = target - transform.position;
            delta.y = 0f;
            Vector3 movement = Vector3.ClampMagnitude(delta, attachmentSpeed * Time.deltaTime);
            if (playerController != null && playerController.enabled)
                playerController.Move(movement);
            else
                transform.position += movement;
        }

        /// <summary>Starts interactive rebinding for the left side; can be called by an options button.</summary>
        public void RebindLeftEscapeKey() => BeginRebind(leftEscapeAction, true);

        /// <summary>Starts interactive rebinding for the right side; can be called by an options button.</summary>
        public void RebindRightEscapeKey() => BeginRebind(rightEscapeAction, false);

        private void SubscribeToGuard()
        {
            if (subscribed)
                return;
            if (securityGuard == null)
                securityGuard = FindAnyObjectByType<SecurityGuardAI>();
            if (securityGuard == null)
                return;

            securityGuard.CatchAttempted += HandleCatchAttempted;
            securityGuard.PlayerEscorted += HandlePlayerEscorted;
            subscribed = true;
        }

        private void UnsubscribeFromGuard()
        {
            if (!subscribed || securityGuard == null)
                return;
            securityGuard.CatchAttempted -= HandleCatchAttempted;
            securityGuard.PlayerEscorted -= HandlePlayerEscorted;
            subscribed = false;
        }

        private void HandleCatchAttempted(SecurityGuardAI guard, Transform target)
        {
            if (target != transform || failureActive)
                return;

            if (Time.time < immunityUntil)
            {
                guard.ReleasePlayer();
                return;
            }

            securityGuard = guard;
            isCaptured = true;
            previousCursorLockState = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            cursorStateSaved = true;
            activeDifficulty = GetDifficulty(guard.CurrentWantedLevel);
            escapeEffort = 0f;
            lastSuccessfulKey = Key.None;
            SuppressPlayerControls();
            IgnoreGuardCollisions(guard);
            if (escapePanel != null) escapePanel.gameObject.SetActive(true);
            UpdateEscapeUi();

            if (escortCoroutine != null) StopCoroutine(escortCoroutine);
            escortCoroutine = StartCoroutine(BeginDraggingAfterGrab(guard));
        }

        private IEnumerator BeginDraggingAfterGrab(SecurityGuardAI guard)
        {
            yield return new WaitForSeconds(guardGrabPause);
            escortCoroutine = null;
            if (isCaptured && !failureActive && guard != null && guard.IsPlayerCaptured)
                guard.BeginEscort();
        }

        private void HandlePlayerEscorted(SecurityGuardAI guard, Transform target)
        {
            if (target != transform || !isCaptured || failureActive)
                return;

            isCaptured = false;
            failureActive = true;
            if (escortCoroutine != null) StopCoroutine(escortCoroutine);
            escortCoroutine = null;
            if (escapePanel != null) escapePanel.gameObject.SetActive(false);
            ShowFailureScreen();
        }

        private void EscapeSuccessfully()
        {
            if (!isCaptured || failureActive)
                return;

            isCaptured = false;
            if (escortCoroutine != null) StopCoroutine(escortCoroutine);
            escortCoroutine = null;
            if (escapePanel != null) escapePanel.gameObject.SetActive(false);
            immunityUntil = Time.time + escapeImmunitySeconds;
            PlayClipOrFallback(escapeSound, 880f);
            if (successEffectRoutine != null) StopCoroutine(successEffectRoutine);
            successEffectRoutine = StartCoroutine(ShowEscapeSuccessEffect());
            RestoreIgnoredCollisions();
            if (securityGuard != null)
            {
                securityGuard.ApplyCatchImmunity(escapeImmunitySeconds);
                securityGuard.EscapePlayer(guardStunSeconds);
            }

            Vector3 releasePoint = transform.position;
            if (securityGuard != null)
                releasePoint += securityGuard.transform.right * 1.2f;
            releasePoint.y = transform.position.y;
            releaseCoroutine = StartCoroutine(ReleaseSafely(releasePoint));
        }

        [ContextMenu("Debug/Force Escape")]
        public void DebugForceEscape() => EscapeSuccessfully();

        public void DebugResetCapture()
        {
            if (escortCoroutine != null) StopCoroutine(escortCoroutine);
            if (releaseCoroutine != null) StopCoroutine(releaseCoroutine);
            if (successEffectRoutine != null) StopCoroutine(successEffectRoutine);
            escortCoroutine = null;
            releaseCoroutine = null;
            successEffectRoutine = null;
            if (securityGuard != null && securityGuard.IsPlayerCaptured)
                securityGuard.ReleasePlayer();
            isCaptured = false;
            failureActive = false;
            escapeEffort = 0f;
            lastSuccessfulKey = Key.None;
            immunityUntil = 0f;
            RestoreIgnoredCollisions();
            RestorePlayerControls();
            if (escapePanel != null) escapePanel.gameObject.SetActive(false);
            if (failurePanel != null) failurePanel.gameObject.SetActive(false);
            if (successPanel != null) successPanel.gameObject.SetActive(false);
            RestoreCursor();
        }

        private IEnumerator ShowEscapeSuccessEffect()
        {
            if (successPanel == null)
                yield break;
            successPanel.gameObject.SetActive(true);
            successPanel.localScale = Vector3.one * 0.35f;
            float elapsed = 0f;
            const float duration = 1.15f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float bounce = Mathf.Sin(t * Mathf.PI * 5f) * (1f - t) * 0.12f;
                successPanel.localScale = Vector3.one * (Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0f, 1f, t)) + bounce);
                successPanel.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 6f) * 4f * (1f - t));
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            successPanel.localScale = Vector3.one;
            successPanel.localRotation = Quaternion.identity;
            yield return new WaitForSecondsRealtime(0.45f);
            successPanel.gameObject.SetActive(false);
            successEffectRoutine = null;
        }

        private IEnumerator ReleaseSafely(Vector3 releasePoint)
        {
            float elapsed = 0f;
            while (elapsed < safeReleaseSeconds)
            {
                Vector3 delta = releasePoint - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude <= 0.0025f)
                    break;

                float step = Mathf.Min(safeReleaseSpeed * Time.deltaTime, delta.magnitude);
                if (playerController != null && playerController.enabled)
                    playerController.Move(delta.normalized * step);
                else
                    transform.position += delta.normalized * step;
                elapsed += Time.deltaTime;
                yield return null;
            }

            releaseCoroutine = null;
            RestorePlayerControls();
        }

        private void ShowFailureScreen()
        {
            RestoreIgnoredCollisions();
            PlayClipOrFallback(failureSound, 185f);
            if (failurePanel != null)
                failurePanel.gameObject.SetActive(true);
            if (failureTitleText != null)
                failureTitleText.text = "KICKED OUT OF " + storeName.Trim().ToUpperInvariant();
            if (failureScoreText != null)
            {
                float chaos = chaosManager != null ? chaosManager.CurrentChaos : ChaosManager.Instance != null ? ChaosManager.Instance.CurrentChaos : 0f;
                failureScoreText.text = "CHAOS SCORE: " + Mathf.RoundToInt(chaos) + " / 100\nYOU RUINED THE SHOPPING TRIP";
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RetryShopping()
        {
            RestoreIgnoredCollisions();
            RestorePlayerControls();
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void SuppressPlayerControls()
        {
            if (controlsSuppressed)
                return;
            controlsSuppressed = true;
            if (playerMovement != null)
            {
                previousMovementLock = playerMovement.IsMovementInputLocked;
                playerMovement.SetMovementInputLocked(true);
            }
            if (itemInteraction != null)
            {
                previousItemInteractionEnabled = itemInteraction.enabled;
                itemInteraction.enabled = false;
            }
        }

        private void RestorePlayerControls()
        {
            if (!controlsSuppressed)
                return;
            controlsSuppressed = false;
            if (playerMovement != null)
                playerMovement.SetMovementInputLocked(previousMovementLock);
            if (itemInteraction != null)
                itemInteraction.enabled = previousItemInteractionEnabled;
        }

        private void IgnoreGuardCollisions(SecurityGuardAI guard)
        {
            RestoreIgnoredCollisions();
            if (guard == null)
                return;

            Collider[] playerColliders = GetComponentsInChildren<Collider>(true);
            Collider[] guardColliders = guard.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider playerCollider = playerColliders[i];
                if (playerCollider == null || !playerCollider.enabled)
                    continue;
                for (int j = 0; j < guardColliders.Length; j++)
                {
                    Collider guardCollider = guardColliders[j];
                    if (guardCollider == null || !guardCollider.enabled)
                        continue;
                    ignoredColliderPairs.Add(new ColliderIgnorePair
                    {
                        player = playerCollider,
                        guard = guardCollider,
                        wasIgnored = Physics.GetIgnoreCollision(playerCollider, guardCollider)
                    });
                    Physics.IgnoreCollision(playerCollider, guardCollider, true);
                }
            }
        }

        private void RestoreIgnoredCollisions()
        {
            for (int i = 0; i < ignoredColliderPairs.Count; i++)
            {
                ColliderIgnorePair pair = ignoredColliderPairs[i];
                if (pair.player != null && pair.guard != null)
                    Physics.IgnoreCollision(pair.player, pair.guard, pair.wasIgnored);
            }
            ignoredColliderPairs.Clear();
        }

        private EscapeDifficulty GetDifficulty(int wantedLevel)
        {
            int level = Mathf.Clamp(wantedLevel, 1, 4);
            if (difficultyByWantedLevel != null)
            {
                for (int i = 0; i < difficultyByWantedLevel.Length; i++)
                {
                    if (difficultyByWantedLevel[i] != null && difficultyByWantedLevel[i].wantedLevel == level)
                        return difficultyByWantedLevel[i];
                }
                if (difficultyByWantedLevel.Length > 0 && difficultyByWantedLevel[Mathf.Clamp(level - 1, 0, difficultyByWantedLevel.Length - 1)] != null)
                    return difficultyByWantedLevel[Mathf.Clamp(level - 1, 0, difficultyByWantedLevel.Length - 1)];
            }
            return new EscapeDifficulty { wantedLevel = level, requiredAlternatingPresses = 6 + (level - 1) * 4, progressDecayPointsPerSecond = level * 1.5f };
        }

        private float GetProgress01()
        {
            if (activeDifficulty == null)
                return 0f;
            float requiredEffort = Mathf.Max(10f, activeDifficulty.requiredAlternatingPresses * 10f);
            return Mathf.Clamp01(escapeEffort / requiredEffort);
        }

        private void UpdateEscapeUi()
        {
            if (instructionText != null)
            {
                string leftName = leftEscapeAction != null ? leftEscapeAction.GetBindingDisplayString() : leftEscapeKey.ToString();
                string rightName = rightEscapeAction != null ? rightEscapeAction.GetBindingDisplayString() : rightEscapeKey.ToString();
                instructionText.text = "PRESS " + leftName.ToUpperInvariant() + " AND " + rightName.ToUpperInvariant() + " OR ELSE";
            }
            if (progressFill != null)
            {
                progressFill.fillAmount = GetProgress01();
                progressFill.color = Color.Lerp(new Color(0.1f, 0.9f, 0.18f), new Color(1f, 0.12f, 0.05f), GetProgress01());
            }
            if (progressText != null)
            {
                int percent = Mathf.RoundToInt(GetProgress01() * 100f);
                int presses = activeDifficulty != null ? activeDifficulty.requiredAlternatingPresses : 6;
                progressText.text = "ESCAPE: " + percent + "%   (" + presses + " ALTERNATING PRESSES)";
            }
            if (rebindHintText != null)
                rebindHintText.text = "TAB: REBIND LEFT     SHIFT + TAB: REBIND RIGHT";
        }

        private void BuildUi()
        {
            if (hudCanvas == null)
            {
                GameObject canvasObject = new GameObject("Security Escape UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                hudCanvas = canvasObject.GetComponent<Canvas>();
                hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
            }

            Transform existing = hudCanvas.transform.Find("Security Escape UI");
            if (existing != null)
                Destroy(existing.gameObject);

            GameObject uiRoot = new GameObject("Security Escape UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            uiRoot.transform.SetParent(hudCanvas.transform, false);
            Canvas childCanvas = uiRoot.GetComponent<Canvas>();
            childCanvas.overrideSorting = true;
            childCanvas.sortingOrder = 900;
            RectTransform rootRect = uiRoot.GetComponent<RectTransform>();
            Stretch(rootRect);

            escapePanel = CreatePanel(rootRect, "Terrible Escape Panel", new Vector2(0.5f, 0.8f), new Vector2(500f, 200f), new Color(1f, 0.88f, 0.05f));
            escapePanel.localRotation = Quaternion.Euler(0f, 0f, -1.3f);
            titleText = CreateText(escapePanel, "GET OFF ME!!!", 36, new Vector2(0f, 62f), new Vector2(455f, 54f), Color.red, FontStyles.Bold, -2f);
            instructionText = CreateText(escapePanel, "PRESS A AND D OR ELSE", 23, new Vector2(0f, 20f), new Vector2(460f, 40f), Color.black, FontStyles.Bold, 1.4f);

            RectTransform barBack = CreatePanel(escapePanel, "Black progress bar outline", new Vector2(0.5f, 0.5f), new Vector2(344f, 32f), Color.black);
            barBack.anchoredPosition = new Vector2(0f, -24f);
            RectTransform bar = CreatePanel(barBack, "Ugly red yellow green bar", new Vector2(0.5f, 0.5f), new Vector2(330f, 18f), new Color(0.1f, 0.9f, 0.18f));
            progressFill = bar.GetComponent<Image>();
            progressFill.type = Image.Type.Filled;
            progressFill.fillMethod = Image.FillMethod.Horizontal;
            progressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            progressFill.fillAmount = 0f;
            progressText = CreateText(escapePanel, "ESCAPE: 0%", 17, new Vector2(8f, -62f), new Vector2(470f, 34f), new Color(0.1f, 0.18f, 0.1f), FontStyles.Bold, -0.4f);
            rebindHintText = CreateText(escapePanel, "TAB: REBIND LEFT     SHIFT + TAB: REBIND RIGHT", 12, new Vector2(0f, -87f), new Vector2(470f, 22f), Color.black, FontStyles.Bold, 1.1f);

            failurePanel = CreatePanel(rootRect, "Bad Failure Screen", new Vector2(0.5f, 0.5f), new Vector2(700f, 420f), Color.black);
            failurePanel.localRotation = Quaternion.Euler(0f, 0f, 1.6f);
            RectTransform failureInner = CreatePanel(failurePanel, "Ugly failure yellow", new Vector2(0.5f, 0.5f), new Vector2(680f, 400f), new Color(1f, 0.76f, 0.02f));
            failureTitleText = CreateText(failureInner, "KICKED OUT OF TESCO", 46, new Vector2(0f, 105f), new Vector2(640f, 90f), Color.red, FontStyles.Bold, -1.2f);
            failureScoreText = CreateText(failureInner, "CHAOS SCORE: 0 / 100", 28, new Vector2(0f, 25f), new Vector2(620f, 95f), Color.black, FontStyles.Bold, 1f);

            RectTransform retryRect = CreatePanel(failureInner, "TRY AGAIN BUTTON", new Vector2(0.5f, 0.5f), new Vector2(240f, 62f), new Color(0.15f, 0.85f, 0.12f));
            retryRect.anchoredPosition = new Vector2(0f, -118f);
            retryButton = retryRect.gameObject.AddComponent<Button>();
            retryButton.targetGraphic = retryRect.GetComponent<Image>();
            retryButton.onClick.AddListener(RetryShopping);
            CreateText(retryRect, "TRY AGAIN!!!", 26, Vector2.zero, new Vector2(230f, 58f), Color.black, FontStyles.Bold, -1.5f);

            successPanel = CreatePanel(rootRect, "Ridiculous Escape Success", new Vector2(0.5f, 0.55f), new Vector2(510f, 128f), new Color(1f, 0.93f, 0.05f));
            successPanel.localRotation = Quaternion.Euler(0f, 0f, -3.4f);
            CreateText(successPanel, "ESCAPED?!?!", 44, new Vector2(0f, 27f), new Vector2(490f, 55f), Color.red, FontStyles.Bold, 2.7f);
            CreateText(successPanel, "I WAS LETTING YOU GO!", 22, new Vector2(0f, -28f), new Vector2(480f, 34f), Color.black, FontStyles.Bold, -1.5f);

            escapePanel.gameObject.SetActive(false);
            failurePanel.gameObject.SetActive(false);
            successPanel.gameObject.SetActive(false);
        }

        private RectTransform CreatePanel(Transform parent, string objectName, Vector2 anchor, Vector2 size, Color color)
        {
            GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.sizeDelta = size;
            Image image = panelObject.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            Outline outline = panelObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;
            return rect;
        }

        private TMP_Text CreateText(Transform parent, string value, float fontSize, Vector2 position, Vector2 size, Color color, FontStyles style, float rotation)
        {
            GameObject textObject = new GameObject(value, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, rotation);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            text.outlineWidth = 0.18f;
            text.outlineColor = Color.black;
            TMP_FontAsset homemadeFont = Resources.Load<TMP_FontAsset>("Fonts/Childs Journal SDF Final");
            if (homemadeFont != null)
                text.font = homemadeFont;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void BeginRebind(InputAction action, bool isLeft)
        {
            if (action == null)
                return;
            DisposeRebind();
            activeRebind = action.PerformInteractiveRebinding()
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(operation =>
                {
                    operation.Dispose();
                    activeRebind = null;
                    SaveBindingOverride(action, isLeft ? LeftBindingPreference : RightBindingPreference);
                    if (isLeft)
                        leftEscapeKey = ReadBoundKey(action, leftEscapeKey);
                    else
                        rightEscapeKey = ReadBoundKey(action, rightEscapeKey);
                    UpdateEscapeUi();
                })
                .OnCancel(operation =>
                {
                    operation.Dispose();
                    activeRebind = null;
                });
            ((UnityEngine.InputSystem.InputActionRebindingExtensions.RebindingOperation)activeRebind).Start();
        }

        private void DisposeRebind()
        {
            activeRebind?.Dispose();
            activeRebind = null;
        }

        private static string BuildKeyboardPath(Key key)
        {
            string name = key == Key.None ? "a" : key.ToString();
            return "<Keyboard>/" + char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static Key ReadBoundKey(InputAction action, Key fallback)
        {
            string path = action.GetBindingDisplayString();
            if (Enum.TryParse(path, true, out Key key))
                return key;
            return fallback;
        }

        private static void LoadBindingOverride(InputAction action, string preference)
        {
            if (action == null || !PlayerPrefs.HasKey(preference))
                return;
            try { action.LoadBindingOverridesFromJson(PlayerPrefs.GetString(preference)); }
            catch (Exception exception) { Debug.LogWarning("Could not restore escape key binding: " + exception.Message); }
        }

        private static void SaveBindingOverride(InputAction action, string preference)
        {
            PlayerPrefs.SetString(preference, action.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        private void PlayClipOrFallback(AudioClip clip, float fallbackFrequency)
        {
            if (audioSource == null)
                return;
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
                return;
            }

            const int sampleRate = 22050;
            const float duration = 0.32f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * i / sampleCount);
                float frequency = fallbackFrequency * (1f - 0.28f * t / duration);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.28f;
            }
            AudioClip beep = AudioClip.Create("Temporary Security Escape Beep", sampleCount, 1, sampleRate, false);
            beep.SetData(samples, 0);
            audioSource.PlayOneShot(beep);
            Destroy(beep, duration + 0.5f);
        }

        private void RestoreCursor()
        {
            if (!cursorStateSaved)
                return;
            Cursor.lockState = previousCursorLockState;
            Cursor.visible = previousCursorVisible;
            cursorStateSaved = false;
        }
    }
}
