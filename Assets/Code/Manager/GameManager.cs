using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using ToJam26.Gameplay.Player;

namespace ToJam26.Gameplay.Manager
{
    public class GameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerManager playerManager;
        [SerializeField] private CuttingGameManager cuttingGameManager;

        [Header("Match Settings")]
        [SerializeField] private int requiredPlayerCount = 2;
        [SerializeField] private int maxRounds = 3;
        [SerializeField] private int roundsToWin = 2;
        [SerializeField] private bool autoStartWhenEnoughPlayers = true;
        [SerializeField] private int restartSceneIndex = 0;
        [SerializeField] private float restartInputDelaySeconds = 1f;

        [Header("Round Settings")]
        [SerializeField] private float roundDurationSeconds = 60f;
        [SerializeField] private float roundStartDelaySeconds = 1.5f;
        [SerializeField] private float roundEndDelaySeconds = 2f;
        [SerializeField] private float fallThresholdY = -10f;

        [Header("Debug")]
        [SerializeField] private bool debugLogs = true;

        private const float VolumeTieThreshold = 0.0001f;
        private const float MassTieThreshold = 0.0001f;

        private readonly List<ScaleController> matchPlayers = new();
        private readonly Dictionary<ScaleController, int> roundWins = new();

        private Coroutine roundFlowRoutine;
        private float remainingRoundTime;
        private int currentRoundNumber;
        private bool matchStarted;
        private bool matchEnded;
        private bool roundActive;
        private bool resolvingRound;
        private float restartInputUnlockTime;

        public float RemainingRoundTime => remainingRoundTime;
        public int CurrentRoundNumber => currentRoundNumber;
        public bool IsRoundActive => roundActive;
        public bool IsMatchEnded => matchEnded;
        public float FallThresholdY => fallThresholdY;

        private void Awake()
        {
            if (playerManager == null)
                playerManager = FindAnyObjectByType<PlayerManager>();

            if (cuttingGameManager == null)
                cuttingGameManager = FindAnyObjectByType<CuttingGameManager>();
        }

        private void OnEnable()
        {
            if (playerManager != null)
                playerManager.PlayerJoined += HandlePlayerJoined;

            RegisterExistingPlayers();

            if (autoStartWhenEnoughPlayers)
                TryStartMatch();
        }

        private void OnDisable()
        {
            if (playerManager != null)
                playerManager.PlayerJoined -= HandlePlayerJoined;

            foreach (ScaleController player in matchPlayers)
            {
                if (player != null)
                    player.OnEliminated -= HandlePlayerEliminated;
            }

            matchPlayers.Clear();
            roundWins.Clear();
            StopRoundFlowRoutine();
        }

        private void Update()
        {
            if (matchEnded)
            {
                HandleMatchEndRestartInput();
                return;
            }

            if (!roundActive || resolvingRound)
                return;

            remainingRoundTime = Mathf.Max(0f, remainingRoundTime - Time.deltaTime);
            if (remainingRoundTime <= 0f)
            {
                ResolveRoundByTimeout();
                return;
            }

            CheckForRingOuts();
        }

        public void StartMatch()
        {
            if (!HasEnoughPlayers())
            {
                if (debugLogs)
                    Debug.LogWarning($"[GameManager] Cannot start match. Waiting for {requiredPlayerCount} players.", this);

                return;
            }

            StopRoundFlowRoutine();

            matchStarted = true;
            matchEnded = false;
            roundActive = false;
            resolvingRound = false;
            currentRoundNumber = 0;
            remainingRoundTime = roundDurationSeconds;
            restartInputUnlockTime = 0f;

            ResetScores();
            BeginNextRound();
        }

        public int GetRoundsWon(ScaleController player)
        {
            if (player == null || !roundWins.TryGetValue(player, out int wins))
                return 0;

            return wins;
        }

        private void HandlePlayerJoined(PlayerInput playerInput)
        {
            RegisterPlayer(playerInput);

            if (autoStartWhenEnoughPlayers)
                TryStartMatch();
        }

        private void RegisterExistingPlayers()
        {
            if (playerManager == null)
                return;

            foreach (PlayerInput playerInput in playerManager.Players)
                RegisterPlayer(playerInput);
        }

        private void RegisterPlayer(PlayerInput playerInput)
        {
            if (playerInput == null || matchPlayers.Count >= requiredPlayerCount)
                return;

            ScaleController scaleController = playerInput.GetComponent<ScaleController>();
            if (scaleController == null || matchPlayers.Contains(scaleController))
                return;

            matchPlayers.Add(scaleController);
            roundWins[scaleController] = 0;
            scaleController.OnEliminated += HandlePlayerEliminated;

            if (debugLogs)
                Debug.Log($"[GameManager] Registered player {scaleController.name}", scaleController);
        }

        private void TryStartMatch()
        {
            if (matchStarted || matchEnded || !HasEnoughPlayers())
                return;

            StartMatch();
        }

        private bool HasEnoughPlayers()
        {
            return matchPlayers.Count >= requiredPlayerCount;
        }

        private void BeginNextRound()
        {
            if (matchEnded)
                return;

            currentRoundNumber++;
            if (currentRoundNumber > maxRounds)
            {
                EndMatch(DetermineMatchLeader(), "Maximum rounds reached.");
                return;
            }

            StopRoundFlowRoutine();
            roundFlowRoutine = StartCoroutine(BeginRoundRoutine());
        }

        private IEnumerator BeginRoundRoutine()
        {
            resolvingRound = true;
            roundActive = false;

            if (playerManager != null)
                playerManager.SetPlayersGameplayEnabled(false);

            if (cuttingGameManager != null)
                cuttingGameManager.ClearDetachedPieces();

            RespawnMatchPlayers();

            if (debugLogs)
                Debug.Log($"[GameManager] Preparing round {currentRoundNumber}.", this);

            if (roundStartDelaySeconds > 0f)
                yield return new WaitForSeconds(roundStartDelaySeconds);

            remainingRoundTime = roundDurationSeconds;
            roundActive = true;
            resolvingRound = false;

            if (playerManager != null)
                playerManager.SetPlayersGameplayEnabled(true);

            if (debugLogs)
                Debug.Log($"[GameManager] Round {currentRoundNumber} started.", this);
        }

        private void CheckForRingOuts()
        {
            List<ScaleController> fallenPlayers = null;

            foreach (ScaleController player in matchPlayers)
            {
                if (player == null || player.IsEliminated || !player.gameObject.activeInHierarchy)
                    continue;

                if (player.transform.position.y >= fallThresholdY)
                    continue;

                fallenPlayers ??= new List<ScaleController>();
                fallenPlayers.Add(player);
            }

            if (fallenPlayers == null || fallenPlayers.Count == 0)
                return;

            if (fallenPlayers.Count == 1)
            {
                fallenPlayers[0].Eliminate();
                return;
            }

            resolvingRound = true;
            foreach (ScaleController fallenPlayer in fallenPlayers)
            {
                if (fallenPlayer != null && !fallenPlayer.IsEliminated)
                    fallenPlayer.Eliminate();
            }

            FinalizeRound(DetermineVolumeWinner(), "Multiple players fell out of the arena.");
        }

        private void HandlePlayerEliminated(ScaleController eliminatedPlayer)
        {
            if (!roundActive || resolvingRound || matchEnded)
                return;

            ScaleController winner = GetRemainingPlayer(eliminatedPlayer);
            if (winner == null)
                winner = DetermineVolumeWinner();

            FinalizeRound(winner, $"{eliminatedPlayer.name} was eliminated.");
        }

        private void ResolveRoundByTimeout()
        {
            FinalizeRound(DetermineVolumeWinner(), "Round timer expired.");
        }

        private void FinalizeRound(ScaleController winner, string reason)
        {
            if (matchEnded)
                return;

            roundActive = false;
            resolvingRound = true;

            if (playerManager != null)
                playerManager.SetPlayersGameplayEnabled(false);

            if (winner != null && roundWins.ContainsKey(winner))
                roundWins[winner]++;

            if (debugLogs)
            {
                string winnerName = winner != null ? winner.name : "<none>";
                Debug.Log($"[GameManager] Round {currentRoundNumber} winner: {winnerName}. Reason: {reason}", this);
            }

            if (winner != null && roundWins[winner] >= roundsToWin)
            {
                EndMatch(winner, "Reached required round wins.");
                return;
            }

            if (currentRoundNumber >= maxRounds)
            {
                EndMatch(DetermineMatchLeader(), "Best-of-three completed.");
                return;
            }

            StopRoundFlowRoutine();
            roundFlowRoutine = StartCoroutine(AdvanceToNextRoundRoutine());
        }

        private IEnumerator AdvanceToNextRoundRoutine()
        {
            if (roundEndDelaySeconds > 0f)
                yield return new WaitForSeconds(roundEndDelaySeconds);

            resolvingRound = false;
            BeginNextRound();
        }

        private void EndMatch(ScaleController winner, string reason)
        {
            StopRoundFlowRoutine();

            matchEnded = true;
            matchStarted = false;
            roundActive = false;
            resolvingRound = false;
            restartInputUnlockTime = Time.unscaledTime + Mathf.Max(0f, restartInputDelaySeconds);

            if (debugLogs)
            {
                string winnerName = winner != null ? winner.name : "<none>";
                Debug.Log($"[GameManager] Match winner: {winnerName}. Reason: {reason}", this);
            }
        }

        private void RespawnMatchPlayers()
        {
            if (playerManager == null)
                return;

            foreach (ScaleController player in matchPlayers)
            {
                if (player == null)
                    continue;

                PlayerInput playerInput = player.GetComponent<PlayerInput>();
                if (playerInput != null)
                    playerManager.RespawnPlayer(playerInput);
            }
        }

        private void ResetScores()
        {
            List<ScaleController> players = new(matchPlayers);
            foreach (ScaleController player in players)
            {
                if (player == null)
                    continue;

                roundWins[player] = 0;
            }
        }

        private ScaleController DetermineMatchLeader()
        {
            ScaleController leadingPlayer = null;
            int bestWins = int.MinValue;

            foreach (ScaleController player in matchPlayers)
            {
                if (player == null || !roundWins.TryGetValue(player, out int wins))
                    continue;

                if (wins > bestWins)
                {
                    bestWins = wins;
                    leadingPlayer = player;
                    continue;
                }

                if (wins == bestWins)
                    leadingPlayer = BreakTie(leadingPlayer, player);
            }

            return leadingPlayer;
        }

        private ScaleController DetermineVolumeWinner()
        {
            ScaleController leadingPlayer = null;

            foreach (ScaleController player in matchPlayers)
            {
                if (player == null)
                    continue;

                leadingPlayer = BreakTie(leadingPlayer, player);
            }

            return leadingPlayer;
        }

        private ScaleController BreakTie(ScaleController currentLeader, ScaleController challenger)
        {
            if (challenger == null)
                return currentLeader;

            if (currentLeader == null)
                return challenger;

            float currentLeaderVolume = currentLeader.GetCurrentVolume();
            float challengerVolume = challenger.GetCurrentVolume();
            if (challengerVolume > currentLeaderVolume + VolumeTieThreshold)
                return challenger;

            if (currentLeaderVolume > challengerVolume + VolumeTieThreshold)
                return currentLeader;

            if (challenger.CurrentMass > currentLeader.CurrentMass + MassTieThreshold)
                return challenger;

            if (currentLeader.CurrentMass > challenger.CurrentMass + MassTieThreshold)
                return currentLeader;

            if (challenger.CurrentScale > currentLeader.CurrentScale)
                return challenger;

            return currentLeader;
        }

        private ScaleController GetRemainingPlayer(ScaleController eliminatedPlayer)
        {
            foreach (ScaleController player in matchPlayers)
            {
                if (player == null || player == eliminatedPlayer || player.IsEliminated)
                    continue;

                return player;
            }

            return null;
        }

        private void StopRoundFlowRoutine()
        {
            if (roundFlowRoutine == null)
                return;

            StopCoroutine(roundFlowRoutine);
            roundFlowRoutine = null;
        }

        private void HandleMatchEndRestartInput()
        {
            if (Time.unscaledTime < restartInputUnlockTime)
                return;

            if (!HasAnyRestartInputThisFrame())
                return;

            if (playerManager != null)
                playerManager.SendPlayersToLobby();
        }

        private static bool HasAnyRestartInputThisFrame()
        {
            foreach (InputDevice device in InputSystem.devices)
            {
                if (device == null)
                    continue;

                foreach (InputControl control in device.allControls)
                {
                    if (control is not ButtonControl button)
                        continue;

                    if (button.synthetic || button.noisy)
                        continue;

                    if (button.wasPressedThisFrame)
                        return true;
                }
            }

            return false;
        }
    }
}
