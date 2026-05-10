using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using ToJam26.Gameplay.Player;

public class PlayerManager : MonoBehaviour
{
    private List<PlayerInput> players = new List<PlayerInput>();
    [SerializeField]
    private List<Transform> startingPoints;
    [SerializeField]
    private List<Transform> lobbySpawns;
    [SerializeField]
    private List<LayerMask> playerLayers;
    
    [SerializeField] private Transform playersParent;

    private PlayerInputManager playerInputManager;

    public delegate void PlayerJoinedDelegate(PlayerInput player);
    public event PlayerJoinedDelegate PlayerJoined;

    public IReadOnlyList<PlayerInput> Players => players;
    private int MaxPlayers => Mathf.Min(startingPoints.Count, playerLayers.Count);

    private void Awake()
    {
        playerInputManager = GetComponent<PlayerInputManager>();
    }

    private void OnEnable()
    {
        playerInputManager.onPlayerJoined += AddPlayer;
        RefreshJoiningState();
    }

    private void OnDisable()
    {
        playerInputManager.onPlayerJoined -= AddPlayer;
    }

    public void AddPlayer(PlayerInput player)
    {
        if (player == null)
            return;

        if (players.Contains(player))
            return;

        if (players.Count >= MaxPlayers)
        {
            Debug.LogWarning("[PlayerManager] Rejected extra player join beyond max supported player count.", this);
            Destroy(player.gameObject);
            RefreshJoiningState();
            return;
        }

        players.Add(player);

        player.transform.parent = playersParent;
        player.gameObject.name = "P" + players.Count.ToString();
        int playerIndex = players.Count - 1;
        Transform spawnPoint = GetLobbySpawnPoint(playerIndex) ?? GetStartingPoint(playerIndex);
        MovePlayerToPoint(player, spawnPoint, true);

        //convert layer mask (bit) to an integer
        int layerToAdd = (int)Mathf.Log(playerLayers[playerIndex].value, 2);
        int channel = 1 << playerIndex;

        //set the layer
        var vcam = player.GetComponentInChildren<CinemachineCamera>();
        vcam.gameObject.layer = layerToAdd;
        vcam.OutputChannel = (OutputChannels)channel;

        player.GetComponentInChildren<CinemachineBrain>().ChannelMask = (OutputChannels)channel;

        if (vcam.TryGetComponent<PlayerCameraController>(out var camController) && spawnPoint != null)
            camController.SetSpawnOrientation(spawnPoint);

        var cam = player.GetComponentInChildren<Camera>();
        foreach (var mask in playerLayers)
            cam.cullingMask &= ~mask.value;
        cam.cullingMask |= 1 << layerToAdd;

        if (player.TryGetComponent<PlayerController>(out PlayerController playerController))
            playerController.SetGameplayEnabled(true);

        PlayerJoined?.Invoke(player);
        RefreshJoiningState();
    }

    public void RespawnAllPlayers()
    {
        foreach (PlayerInput player in players)
            RespawnPlayer(player);
    }

    public void RespawnPlayer(PlayerInput player)
    {
        if (player == null)
            return;

        int playerIndex = players.IndexOf(player);
        if (playerIndex < 0)
            return;

        RespawnPlayer(playerIndex);
    }

    public void RespawnPlayer(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Count || playerIndex >= startingPoints.Count)
            return;

        PlayerInput player = players[playerIndex];
        if (player == null)
            return;

        if (!player.gameObject.activeSelf)
            player.gameObject.SetActive(true);
        MovePlayerToPoint(player, GetStartingPoint(playerIndex), true);

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
            playerController.SetGameplayEnabled(false);
    }

    public void SendPlayersToLobby()
    {
        for (int i = 0; i < players.Count; i++)
        {
            PlayerInput player = players[i];
            if (player == null)
                continue;

            Transform spawn = GetLobbySpawnPoint(i) ?? GetStartingPoint(i);
            MovePlayerToPoint(player, spawn, true);

            if (player.TryGetComponent<PlayerController>(out PlayerController playerController))
                playerController.SetGameplayEnabled(true);
        }
    }

    public void SetPlayersGameplayEnabled(bool enabled)
    {
        foreach (PlayerInput player in players)
        {
            if (player == null)
                continue;

            PlayerController playerController = player.GetComponent<PlayerController>();
            if (playerController != null)
                playerController.SetGameplayEnabled(enabled);
        }
    }

    private Transform GetStartingPoint(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= startingPoints.Count)
            return null;

        return startingPoints[playerIndex];
    }

    private Transform GetLobbySpawnPoint(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= lobbySpawns.Count)
            return null;

        return lobbySpawns[playerIndex];
    }

    private void MovePlayerToPoint(PlayerInput player, Transform spawnPoint, bool resetScale)
    {
        if (player == null || spawnPoint == null)
            return;

        if (!player.gameObject.activeSelf)
            player.gameObject.SetActive(true);

        if (resetScale && player.TryGetComponent<ScaleController>(out ScaleController scaleController))
            scaleController.ResetToOriginal();

        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController != null)
            characterController.enabled = false;

        player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        if (characterController != null)
            characterController.enabled = true;

        var vcam = player.GetComponentInChildren<CinemachineCamera>();
        if (vcam != null && vcam.TryGetComponent<PlayerCameraController>(out var camController))
            camController.SetSpawnOrientation(spawnPoint);

        if (player.TryGetComponent<PlayerController>(out PlayerController playerController))
            playerController.DisableAttackHitbox();
    }

    private void RefreshJoiningState()
    {
        if (playerInputManager == null)
            return;

        if (players.Count >= MaxPlayers)
            playerInputManager.DisableJoining();
        else
            playerInputManager.EnableJoining();
    }
}

