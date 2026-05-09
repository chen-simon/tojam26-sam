using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

public class PlayerManager : MonoBehaviour
{
    private List<PlayerInput> players = new List<PlayerInput>();
    [SerializeField]
    private List<Transform> startingPoints;
    [SerializeField]
    private List<LayerMask> playerLayers;
    
    [SerializeField] private Transform playersParent;

    private PlayerInputManager playerInputManager;

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
        players.Add(player);

        player.transform.parent = playersParent;
        player.gameObject.name = "P" + players.Count.ToString();

        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        player.transform.position = startingPoints[players.Count - 1].position;
        if (cc) cc.enabled = true;

        //convert layer mask (bit) to an integer
        int layerToAdd = (int)Mathf.Log(playerLayers[players.Count - 1].value, 2);
        int channel = 1 << (players.Count - 1);

        //set the layer
        var vcam = player.GetComponentInChildren<CinemachineCamera>();
        vcam.gameObject.layer = layerToAdd;
        vcam.OutputChannel = (OutputChannels)channel;

        player.GetComponentInChildren<CinemachineBrain>().ChannelMask = (OutputChannels)channel;

        var cam = player.GetComponentInChildren<Camera>();
        foreach (var mask in playerLayers)
            cam.cullingMask &= ~mask.value;
        cam.cullingMask |= 1 << layerToAdd;
    }
}

