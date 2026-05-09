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
    private List<LayerMask> playerLayers;
    
    [SerializeField] private Transform playersParent;

    private PlayerInputManager playerInputManager;

    public delegate void PlayerJoinedDelegate(PlayerInput player);
    public event PlayerJoinedDelegate PlayerJoined;

    public IReadOnlyList<PlayerInput> Players => players;

    private void Awake()
    {
        playerInputManager = GetComponent<PlayerInputManager>();
    }

    private void OnEnable()
    {
        playerInputManager.onPlayerJoined += AddPlayer;
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

        if (players.Count >= startingPoints.Count || players.Count >= playerLayers.Count)
        {
            Debug.LogWarning("[PlayerManager] Not enough spawn points or player layers configured for another player.", this);
            return;
        }

        players.Add(player);

        player.transform.parent = playersParent;
        player.gameObject.name = "P" + players.Count.ToString();

        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        Transform startPoint = startingPoints[players.Count - 1];
        player.transform.SetPositionAndRotation(startPoint.position, startPoint.rotation);
        if (cc) cc.enabled = true;

        //convert layer mask (bit) to an integer
        int layerToAdd = (int)Mathf.Log(playerLayers[players.Count - 1].value, 2);
        int channel = 1 << (players.Count - 1);

        //set the layer
        var vcam = player.GetComponentInChildren<CinemachineCamera>();
        vcam.gameObject.layer = layerToAdd;
        vcam.OutputChannel = (OutputChannels)channel;

        player.GetComponentInChildren<CinemachineBrain>().ChannelMask = (OutputChannels)channel;

        if (vcam.TryGetComponent<PlayerCameraController>(out var camController))
            camController.SetSpawnOrientation(startPoint);

        var cam = player.GetComponentInChildren<Camera>();
        foreach (var mask in playerLayers)
            cam.cullingMask &= ~mask.value;
        cam.cullingMask |= 1 << layerToAdd;

        PlayerJoined?.Invoke(player);
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

        ScaleController scaleController = player.GetComponent<ScaleController>();
        scaleController?.ResetToOriginal();

        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController != null)
            characterController.enabled = false;

        Transform spawnPoint = startingPoints[playerIndex];
        player.transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);

        if (characterController != null)
            characterController.enabled = true;

        var vcam = player.GetComponentInChildren<CinemachineCamera>();
        if (vcam != null && vcam.TryGetComponent<PlayerCameraController>(out var camController))
            camController.SetSpawnOrientation(spawnPoint);

        PlayerController playerController = player.GetComponent<PlayerController>();
        playerController?.DisableAttackHitbox();
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
}

