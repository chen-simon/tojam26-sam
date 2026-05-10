using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ToJam26.Gameplay.Manager;
using System.Collections.Generic;

namespace ToJam26.Gameplay.Interface
{
    public class HudController : MonoBehaviour
    {
        private static readonly int CountdownStartTrigger = Animator.StringToHash("Start");

        public event System.Action CountdownCompleted;

        [Header("References")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text roundText;
        [SerializeField] private GameObject timerRoot;
        [SerializeField] private GameObject instructionRoot;
        [SerializeField] private GameObject countdownRoot;
        [SerializeField] private Animator countdownAnimator;

        [Header("Display")]
        [SerializeField] private GameObject roundRoot;
        [SerializeField] private string roundLabelPrefix = "Round";
        [SerializeField] private bool hideCountdownWhenIdle = true;

        [Header("Lobby Blur")]
        [SerializeField] private bool enableLobbyBlur = true;
        [SerializeField] private float lobbyBlurStart = 0f;
        [SerializeField] private float lobbyBlurEnd = 2f;
        [SerializeField] private float lobbyBlurMaxRadius = 1f;

        private readonly Dictionary<int, bool> originalPostProcessingStates = new();
        private Volume lobbyBlurVolume;
        private VolumeProfile lobbyBlurProfile;
        private DepthOfField lobbyBlurDepthOfField;
        private HudCountdownAnimationRelay countdownRelay;
        private bool countdownPending;

        public bool CanPlayCountdown => countdownAnimator != null;

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void Awake()
        {
            if (gameManager == null)
                gameManager = FindAnyObjectByType<GameManager>();

            EnsureLobbyBlurSetup();
            AutoAssignReferences();
            RefreshDisplay();
        }

        private void OnEnable()
        {
            SubscribeToGameManager();
            RefreshDisplay();
        }

        private void OnDisable()
        {
            UnsubscribeFromGameManager();
        }

        private void SubscribeToGameManager()
        {
            if (gameManager == null)
                return;

            gameManager.RoundPreparationStarted -= HandleRoundPreparationStarted;
            gameManager.RoundStarted -= HandleRoundStarted;
            gameManager.RoundTimeChanged -= HandleRoundTimeChanged;
            gameManager.PlayerRequirementChanged -= HandlePlayerRequirementChanged;
            gameManager.MatchEnded -= HandleMatchEnded;
            gameManager.LobbyEntered -= HandleLobbyEntered;

            gameManager.RoundPreparationStarted += HandleRoundPreparationStarted;
            gameManager.RoundStarted += HandleRoundStarted;
            gameManager.RoundTimeChanged += HandleRoundTimeChanged;
            gameManager.PlayerRequirementChanged += HandlePlayerRequirementChanged;
            gameManager.MatchEnded += HandleMatchEnded;
            gameManager.LobbyEntered += HandleLobbyEntered;
        }

        private void UnsubscribeFromGameManager()
        {
            if (gameManager == null)
                return;

            gameManager.RoundPreparationStarted -= HandleRoundPreparationStarted;
            gameManager.RoundStarted -= HandleRoundStarted;
            gameManager.RoundTimeChanged -= HandleRoundTimeChanged;
            gameManager.PlayerRequirementChanged -= HandlePlayerRequirementChanged;
            gameManager.MatchEnded -= HandleMatchEnded;
            gameManager.LobbyEntered -= HandleLobbyEntered;
        }

        private void AutoAssignReferences()
        {
            if (timerText == null)
                timerText = FindText("Timer/Text (TMP)");

            if (timerRoot == null)
            {
                Transform timerTransform = transform.Find("Timer");
                if (timerTransform != null)
                    timerRoot = timerTransform.gameObject;
            }

            if (instructionRoot == null)
            {
                Transform instructionTransform = transform.Find("Instruction");
                if (instructionTransform != null)
                    instructionRoot = instructionTransform.gameObject;
            }

            if (roundText == null)
                roundText = FindText("Round/Text (TMP)");

            if (roundRoot == null)
            {
                Transform roundTransform = transform.Find("Round");
                if (roundTransform != null)
                    roundRoot = roundTransform.gameObject;
            }

            if (countdownRoot == null)
            {
                Transform countdownTransform = transform.Find("Count Down");
                if (countdownTransform != null)
                    countdownRoot = countdownTransform.gameObject;
            }

            if (countdownAnimator == null)
            {
                Transform countdownImageTransform = transform.Find("Count Down/Image");
                if (countdownImageTransform != null)
                    countdownAnimator = countdownImageTransform.GetComponent<Animator>();
            }

            if (countdownAnimator != null)
            {
                countdownRelay = countdownAnimator.GetComponent<HudCountdownAnimationRelay>();
                if (countdownRelay == null)
                    countdownRelay = countdownAnimator.gameObject.AddComponent<HudCountdownAnimationRelay>();

                countdownRelay.Initialize(this);
            }
        }

        private TMP_Text FindText(string relativePath)
        {
            Transform target = transform.Find(relativePath);
            if (target == null)
                return null;

            return target.GetComponent<TMP_Text>();
        }

        private void RefreshDisplay()
        {
            if (gameManager == null)
            {
                SetRoundVisible(false);
                SetTimerVisible(false);
                SetInstructionVisible(true);
                SetLobbyBlurActive(true);
                SetTimerLabel(0f);
                SetCountdownVisible(!hideCountdownWhenIdle);
                return;
            }

            int roundNumber = gameManager.CurrentRoundNumber;
            if (roundNumber > 0)
            {
                SetRoundVisible(true);
                SetTimerVisible(true);
                SetInstructionVisible(false);
                SetLobbyBlurActive(false);
                SetRoundLabel(roundNumber, gameManager.MaxRounds);
            }
            else
            {
                SetRoundVisible(false);
                SetTimerVisible(false);
                SetInstructionVisible(!gameManager.HasRequiredPlayers);
                SetLobbyBlurActive(!gameManager.HasRequiredPlayers);
            }

            SetTimerLabel(gameManager.RemainingRoundTime);
            SetCountdownVisible(false);
        }

        private void HandleRoundPreparationStarted(int roundNumber, int maxRounds)
        {
            SetRoundVisible(true);
            SetTimerVisible(true);
            SetInstructionVisible(false);
            SetLobbyBlurActive(false);
            SetRoundLabel(roundNumber, maxRounds);
            SetCountdownVisible(true);
            PlayCountdown();
        }

        private void HandleRoundStarted(int roundNumber, int maxRounds, float roundDuration)
        {
            SetRoundVisible(true);
            SetTimerVisible(true);
            SetInstructionVisible(false);
            SetLobbyBlurActive(false);
            SetRoundLabel(roundNumber, maxRounds);
            SetTimerLabel(roundDuration);
            SetCountdownVisible(false);
        }

        private void HandleRoundTimeChanged(float remainingTime)
        {
            SetTimerLabel(remainingTime);
        }

        private void HandlePlayerRequirementChanged(bool hasRequiredPlayers)
        {
            if (gameManager != null && gameManager.CurrentRoundNumber > 0)
                return;

            SetInstructionVisible(!hasRequiredPlayers);
            SetLobbyBlurActive(!hasRequiredPlayers);
        }

        private void HandleMatchEnded()
        {
            SetTimerVisible(true);
            SetInstructionVisible(false);
            SetLobbyBlurActive(false);
            SetCountdownVisible(false);
            SetTimerLabel(0f);
        }

        private void HandleLobbyEntered()
        {
            SetRoundVisible(false);
            SetTimerVisible(false);
            SetInstructionVisible(gameManager == null || !gameManager.HasRequiredPlayers);
            SetLobbyBlurActive(gameManager == null || !gameManager.HasRequiredPlayers);
            SetTimerLabel(0f);
            SetCountdownVisible(false);
        }

        private void SetRoundLabel(int roundNumber, int maxRounds)
        {
            if (roundText == null)
                return;

            roundText.text = $"{roundLabelPrefix} {roundNumber}/{maxRounds}";
        }

        private void SetRoundVisible(bool visible)
        {
            if (roundRoot == null)
                return;

            roundRoot.SetActive(visible);
        }

        private void SetTimerVisible(bool visible)
        {
            if (timerRoot == null)
                return;

            timerRoot.SetActive(visible);
        }

        private void SetInstructionVisible(bool visible)
        {
            if (instructionRoot == null)
                return;

            instructionRoot.SetActive(visible);
        }

        private void SetTimerLabel(float remainingTime)
        {
            if (timerText == null)
                return;

            int totalSeconds = Mathf.CeilToInt(Mathf.Max(0f, remainingTime));
            int minutes = totalSeconds / 60;
            int seconds = totalSeconds % 60;
            timerText.text = $"{minutes:00}:{seconds:00}";
        }

        private void SetCountdownVisible(bool visible)
        {
            if (countdownRoot == null)
                return;

            countdownRoot.SetActive(visible || !hideCountdownWhenIdle);
        }

        private void PlayCountdown()
        {
            if (countdownAnimator == null)
            {
                NotifyCountdownFinished();
                return;
            }

            countdownPending = true;
            countdownAnimator.speed = 1f;
            countdownAnimator.Rebind();
            countdownAnimator.Update(0f);
            countdownAnimator.ResetTrigger(CountdownStartTrigger);
            countdownAnimator.SetTrigger(CountdownStartTrigger);
        }

        public void NotifyCountdownFinished()
        {
            if (!countdownPending)
                return;

            countdownPending = false;
            CountdownCompleted?.Invoke();
        }

        private void EnsureLobbyBlurSetup()
        {
            if (!enableLobbyBlur || lobbyBlurVolume != null)
                return;

            GameObject blurObject = new("Lobby Blur Volume");
            blurObject.transform.SetParent(transform, false);

            lobbyBlurVolume = blurObject.AddComponent<Volume>();
            lobbyBlurVolume.isGlobal = true;
            lobbyBlurVolume.priority = 100f;
            lobbyBlurVolume.weight = 0f;

            lobbyBlurProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            lobbyBlurVolume.profile = lobbyBlurProfile;

            lobbyBlurDepthOfField = lobbyBlurProfile.Add<DepthOfField>(true);
            lobbyBlurDepthOfField.active = true;
            lobbyBlurDepthOfField.mode.Override(DepthOfFieldMode.Gaussian);
            lobbyBlurDepthOfField.gaussianStart.Override(lobbyBlurStart);
            lobbyBlurDepthOfField.gaussianEnd.Override(lobbyBlurEnd);
            lobbyBlurDepthOfField.gaussianMaxRadius.Override(lobbyBlurMaxRadius);
            lobbyBlurDepthOfField.highQualitySampling.Override(false);
        }

        private void SetLobbyBlurActive(bool active)
        {
            if (!enableLobbyBlur)
                return;

            EnsureLobbyBlurSetup();
            RefreshCameraPostProcessing(active);

            if (lobbyBlurVolume != null)
                lobbyBlurVolume.weight = active ? 1f : 0f;
        }

        private void RefreshCameraPostProcessing(bool active)
        {
            UniversalAdditionalCameraData[] cameraDataComponents =
                FindObjectsByType<UniversalAdditionalCameraData>(FindObjectsSortMode.None);

            foreach (UniversalAdditionalCameraData cameraData in cameraDataComponents)
            {
                if (cameraData == null || !cameraData.isActiveAndEnabled)
                    continue;

                int instanceId = cameraData.GetInstanceID();
                if (!originalPostProcessingStates.ContainsKey(instanceId))
                    originalPostProcessingStates[instanceId] = cameraData.renderPostProcessing;

                cameraData.renderPostProcessing = active || originalPostProcessingStates[instanceId];
            }
        }

        private void OnDestroy()
        {
            if (lobbyBlurProfile != null)
                Destroy(lobbyBlurProfile);
        }
    }
}
