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


            player.transform.position = startingPoints[players.Count - 1].position;

            //convert layer mask (bit) to an integer 
            int layerToAdd = (int)Mathf.Log(playerLayers[players.Count - 1].value, 2);

            //set the layer
            player.GetComponentInChildren<CinemachineCamera>().gameObject.layer = layerToAdd;
            var cam = player.GetComponentInChildren<Camera>();
            foreach (var mask in playerLayers)
                cam.cullingMask &= ~mask.value;
            cam.cullingMask |= 1 << layerToAdd;
        }
    }

