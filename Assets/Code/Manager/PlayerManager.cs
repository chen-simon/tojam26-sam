using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Cinemachine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using ToJam26.Gameplay.Player;

public class PlayerManager : MonoBehaviour
{
    private readonly List<PlayerInput> players = new List<PlayerInput>();
    private readonly Dictionary<PlayerInput, PlayerCameraController> playerCameraControllers = new();
    private readonly Dictionary<PlayerInput, CinemachineCamera> playerVirtualCameras = new();
    private readonly Dictionary<PlayerInput, Camera> playerOutputCameras = new();
    private readonly Dictionary<PlayerInput, CinemachineBrain> playerCameraBrains = new();
    private readonly Dictionary<PlayerInput, List<InputDevice>> playerPairedDevices = new();

    [SerializeField]
    private List<Transform> startingPoints;
    [SerializeField]
    private List<Transform> lobbySpawns;
    [SerializeField]
    private List<LayerMask> playerLayers;

    [SerializeField] private Transform playersParent;
    [SerializeField] private List<Animator> playerHitUIAnimators;

    [Header("Connect UI")]
    [SerializeField] private Image p1ConnectImage;
    [SerializeField] private Image p2ConnectImage;

    [Header("Camera Transitions")]
    [SerializeField] private Transform cameraRigsParent;
    [SerializeField] private float joinCameraReframeDuration = 0.75f;
    [SerializeField] private float roundStartCameraReframeDuration = 1f;
    [SerializeField] private float lobbyCameraReframeDuration = 0.8f;

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
        CachePlayerDevices(player);
        player.neverAutoSwitchControlSchemes = true;

        player.transform.parent = playersParent;
        player.gameObject.name = "P" + players.Count.ToString();
        int playerIndex = players.Count - 1;

        CacheAndDetachCameraRig(player);
        EnsurePlayerInputActive(player);

        Transform spawnPoint = GetLobbySpawnPoint(playerIndex) ?? GetStartingPoint(playerIndex);
        MovePlayerToPoint(player, spawnPoint, true, joinCameraReframeDuration);

        //convert layer mask (bit) to an integer
        int layerToAdd = (int)Mathf.Log(playerLayers[playerIndex].value, 2);
        int channel = 1 << playerIndex;

        //set the layer
        CinemachineCamera vcam = GetCachedVirtualCamera(player);
        if (vcam != null)
        {
            vcam.gameObject.layer = layerToAdd;
            vcam.OutputChannel = (OutputChannels)channel;
        }

        CinemachineBrain brain = GetCachedCameraBrain(player);
        if (brain != null)
            brain.ChannelMask = (OutputChannels)channel;

        Camera cam = GetCachedOutputCamera(player);
        if (cam != null)
        {
            foreach (LayerMask mask in playerLayers)
                cam.cullingMask &= ~mask.value;

            cam.cullingMask |= 1 << layerToAdd;
        }

        if (player.TryGetComponent<PlayerController>(out PlayerController playerController))
        {
            playerController.SetGameplayEnabled(true);
            if (playerIndex < playerHitUIAnimators.Count && playerHitUIAnimators[playerIndex] != null)
                playerController.SetHitUIAnimator(playerHitUIAnimators[playerIndex]);
        }

        if (playerIndex == 0 && p1ConnectImage != null)
            p1ConnectImage.gameObject.SetActive(false);
        else if (playerIndex == 1 && p2ConnectImage != null)
            p2ConnectImage.gameObject.SetActive(false);

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

        EnsurePlayerInputActive(player);

        MovePlayerToPoint(player, GetStartingPoint(playerIndex), true, roundStartCameraReframeDuration);

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

            EnsurePlayerInputActive(player);

            Transform spawn = GetLobbySpawnPoint(i) ?? GetStartingPoint(i);
            MovePlayerToPoint(player, spawn, true, lobbyCameraReframeDuration);

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

    private void MovePlayerToPoint(PlayerInput player, Transform spawnPoint, bool resetScale, float cameraReframeDuration)
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

        PlayerCameraController camController = GetCachedCameraController(player);
        if (camController != null)
            camController.ReframeToSpawn(spawnPoint, cameraReframeDuration);

        if (player.TryGetComponent<PlayerController>(out PlayerController playerController))
            playerController.DisableAttackHitbox();
    }

    private void CacheAndDetachCameraRig(PlayerInput player)
    {
        if (player == null)
            return;

        if (!playerCameraControllers.TryGetValue(player, out PlayerCameraController camController) || camController == null)
        {
            CinemachineCamera virtualCamera = player.GetComponentInChildren<CinemachineCamera>(true);
            Camera outputCamera = player.GetComponentInChildren<Camera>(true);
            CinemachineBrain brain = player.GetComponentInChildren<CinemachineBrain>(true);
            camController = player.GetComponentInChildren<PlayerCameraController>(true);

            if (camController != null)
                playerCameraControllers[player] = camController;

            if (virtualCamera != null)
                playerVirtualCameras[player] = virtualCamera;

            if (outputCamera != null)
                playerOutputCameras[player] = outputCamera;

            if (brain != null)
                playerCameraBrains[player] = brain;
        }

        Transform rigParent = cameraRigsParent != null ? cameraRigsParent : transform;

        CinemachineCamera cachedVirtualCamera = GetCachedVirtualCamera(player);
        if (cachedVirtualCamera != null && cachedVirtualCamera.transform.parent != rigParent)
            cachedVirtualCamera.transform.SetParent(rigParent, true);

        Camera cachedOutputCamera = GetCachedOutputCamera(player);
        if (cachedOutputCamera != null && cachedOutputCamera.transform.parent != rigParent)
            cachedOutputCamera.transform.SetParent(rigParent, true);
    }

    private PlayerCameraController GetCachedCameraController(PlayerInput player)
    {
        playerCameraControllers.TryGetValue(player, out PlayerCameraController controller);
        return controller;
    }

    private CinemachineCamera GetCachedVirtualCamera(PlayerInput player)
    {
        playerVirtualCameras.TryGetValue(player, out CinemachineCamera virtualCamera);
        return virtualCamera;
    }

    private Camera GetCachedOutputCamera(PlayerInput player)
    {
        playerOutputCameras.TryGetValue(player, out Camera outputCamera);
        return outputCamera;
    }

    private CinemachineBrain GetCachedCameraBrain(PlayerInput player)
    {
        playerCameraBrains.TryGetValue(player, out CinemachineBrain brain);
        return brain;
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

    private void EnsurePlayerInputActive(PlayerInput player)
    {
        if (player == null)
            return;

        if (!player.enabled)
            player.enabled = true;

        RestorePlayerDevices(player);
        player.ActivateInput();

        if (player.actions == null)
            return;

        player.actions.Enable();

        if (player.user.valid)
            player.user.AssociateActionsWithUser(player.actions);

        InputActionMap playerActionMap = player.actions.FindActionMap("Player", throwIfNotFound: false);
        if (playerActionMap != null && !playerActionMap.enabled)
            playerActionMap.Enable();

        if (player.currentActionMap == null || player.currentActionMap != playerActionMap)
            player.SwitchCurrentActionMap("Player");
    }

    private void CachePlayerDevices(PlayerInput player)
    {
        if (player == null)
            return;

        List<InputDevice> devices = new();
        foreach (InputDevice device in player.devices)
        {
            if (device != null)
                devices.Add(device);
        }

        playerPairedDevices[player] = devices;
    }

    private void RestorePlayerDevices(PlayerInput player)
    {
        if (player == null || !playerPairedDevices.TryGetValue(player, out List<InputDevice> cachedDevices))
            return;

        if (player.devices.Count > 0 || cachedDevices.Count == 0)
            return;

        if (player.user.valid)
            player.user.UnpairDevices();

        foreach (InputDevice device in cachedDevices)
        {
            if (device == null)
                continue;

            if (player.user.valid)
                InputUser.PerformPairingWithDevice(device, player.user);
            else
                InputUser.PerformPairingWithDevice(device);
        }
    }
}

