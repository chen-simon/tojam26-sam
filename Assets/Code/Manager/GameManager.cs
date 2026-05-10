using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using ToJam26.Gameplay.Player;

namespace ToJam26.Gameplay.Manager
{
    public class GameManager : MonoBehaviour
    {
        public event Action<int, int, float> RoundPreparationStarted;
        public event Action<int, int, float> RoundStarted;
        public event Action<float> RoundTimeChanged;
        public event Action<bool> PlayerRequirementChanged;
        public event Action MatchEnded;
        public event Action LobbyEntered;

        [Header("References")]
        [SerializeField] private PlayerManager playerManager;
        [SerializeField] private CuttingGameManager cuttingGameManager;

        [Header("Match Settings")]
        [SerializeField] private int requiredPlayerCount = 2;
        [SerializeField] private int maxRounds = 3;
        [SerializeField] private int roundsToWin = 2;
        [SerializeField] private bool useLobbyReadyFlow = true;
        [SerializeField] private List<LobbyReadyZone> lobbyReadyZones = new();
        [SerializeField] private bool autoStartWhenEnoughPlayers = true;

        [Header("Round Settings")]
        [SerializeField] private float roundDurationSeconds = 60f;
        [SerializeField] private float roundStartDelaySeconds = 1.5f;
        [SerializeField] private float roundEndDelaySeconds = 2f;
        [SerializeField] private float postMatchLobbyDelaySeconds = 2f;
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
        private bool matchCompleted;
        private bool roundActive;
        private bool resolvingRound;

        public float RemainingRoundTime => remainingRoundTime;
        public int CurrentRoundNumber => currentRoundNumber;
        public int MaxRounds => maxRounds;
        public bool HasRequiredPlayers => HasEnoughPlayers();
        public bool IsRoundActive => roundActive;
        public bool IsMatchEnded => matchCompleted;
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

            if (useLobbyReadyFlow)
                EnterLobbyState();
            else if (autoStartWhenEnoughPlayers)
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
            if (!matchStarted)
            {
                if (useLobbyReadyFlow)
                    TryStartMatchFromLobbyReady();

                return;
            }

            if (!roundActive || resolvingRound)
                return;

            remainingRoundTime = Mathf.Max(0f, remainingRoundTime - Time.deltaTime);
            RoundTimeChanged?.Invoke(remainingRoundTime);

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
            matchCompleted = false;
            roundActive = false;
            resolvingRound = false;
            currentRoundNumber = 0;
            remainingRoundTime = roundDurationSeconds;

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

            if (!useLobbyReadyFlow && autoStartWhenEnoughPlayers)
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
            PlayerRequirementChanged?.Invoke(HasEnoughPlayers());

            if (debugLogs)
                Debug.Log($"[GameManager] Registered player {scaleController.name}", scaleController);
        }

        private void TryStartMatch()
        {
            if (matchStarted || resolvingRound || !HasEnoughPlayers())
                return;

            StartMatch();
        }

        private void TryStartMatchFromLobbyReady()
        {
            if (!HasEnoughPlayers() || !AreAllReadyZonesOccupied())
                return;

            if (debugLogs)
                Debug.Log("[GameManager] All lobby ready zones are occupied. Starting match.", this);

            StartMatch();
        }

        private bool HasEnoughPlayers()
        {
            return matchPlayers.Count >= requiredPlayerCount;
        }

        private bool AreAllReadyZonesOccupied()
        {
            if (!useLobbyReadyFlow)
                return true;

            if (lobbyReadyZones == null || lobbyReadyZones.Count == 0)
                return false;

            HashSet<ScaleController> readyPlayers = new();
            foreach (LobbyReadyZone readyZone in lobbyReadyZones)
            {
                if (readyZone == null)
                    return false;

                ScaleController occupyingPlayer = readyZone.OccupyingPlayer;
                if (occupyingPlayer == null || !matchPlayers.Contains(occupyingPlayer))
                    return false;

                if (!readyPlayers.Add(occupyingPlayer))
                    return false;
            }

            return readyPlayers.Count >= requiredPlayerCount;
        }

        private void BeginNextRound()
        {
            if (!matchStarted)
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

            RoundPreparationStarted?.Invoke(currentRoundNumber, maxRounds, roundStartDelaySeconds);

            if (roundStartDelaySeconds > 0f)
                yield return new WaitForSeconds(roundStartDelaySeconds);

            remainingRoundTime = roundDurationSeconds;
            roundActive = true;
            resolvingRound = false;
            RoundStarted?.Invoke(currentRoundNumber, maxRounds, roundDurationSeconds);
            RoundTimeChanged?.Invoke(remainingRoundTime);

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
            if (!roundActive || resolvingRound || !matchStarted)
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
            if (!matchStarted)
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
            roundActive = false;
            resolvingRound = true;
            matchCompleted = true;
            RoundTimeChanged?.Invoke(0f);
            MatchEnded?.Invoke();

            if (playerManager != null)
                playerManager.SetPlayersGameplayEnabled(false);

            if (debugLogs)
            {
                string winnerName = winner != null ? winner.name : "<none>";
                Debug.Log($"[GameManager] Match winner: {winnerName}. Reason: {reason}", this);
            }

            roundFlowRoutine = StartCoroutine(ReturnToLobbyRoutine());
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

        private IEnumerator ReturnToLobbyRoutine()
        {
            if (postMatchLobbyDelaySeconds > 0f)
                yield return new WaitForSeconds(postMatchLobbyDelaySeconds);

            roundFlowRoutine = null;
            EnterLobbyState();
        }

        private void EnterLobbyState()
        {
            StopRoundFlowRoutine();

            matchStarted = false;
            matchCompleted = false;
            roundActive = false;
            resolvingRound = false;
            currentRoundNumber = 0;
            remainingRoundTime = 0f;
            PlayerRequirementChanged?.Invoke(HasEnoughPlayers());
            RoundTimeChanged?.Invoke(remainingRoundTime);
            LobbyEntered?.Invoke();

            ResetScores();

            if (cuttingGameManager != null)
                cuttingGameManager.ClearDetachedPieces();

            if (playerManager != null)
                playerManager.SendPlayersToLobby();

            if (lobbyReadyZones == null)
                return;

            foreach (LobbyReadyZone readyZone in lobbyReadyZones)
            {
                if (readyZone != null)
                    readyZone.ClearOccupants();
            }
        }
    }
}
