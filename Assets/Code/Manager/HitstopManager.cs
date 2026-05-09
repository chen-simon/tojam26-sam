using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using ToJam26.Gameplay.Player;

namespace ToJam26.Gameplay.Manager
{
    public class HitstopManager : MonoBehaviour
    {
        public static HitstopManager Instance { get; private set; }

        [SerializeField] private float baseHitstopFrames = 4f;
        [SerializeField] private float maxHitstopFrames = 12f;
        [SerializeField] private float victimStunAnimSpeed = 1f;

        private Coroutine activeHitstop;
        private Animator currentVictimAnimator;
        private ScaleController currentVictimScale;

        private static readonly int AnimStun = Animator.StringToHash("Stun");

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void TriggerHitstop(Animator victimAnimator, ScaleController victimScale, float knockbackMultiplier)
        {
            float frames = Mathf.Min(baseHitstopFrames * Mathf.Max(knockbackMultiplier, 0.1f), maxHitstopFrames);
            float duration = frames / 60f;

            if (activeHitstop != null)
            {
                StopCoroutine(activeHitstop);
                ClearHitstopState();
            }

            currentVictimAnimator = victimAnimator;
            currentVictimScale = victimScale;
            activeHitstop = StartCoroutine(HitstopCoroutine(victimAnimator, victimScale, duration));
        }

        private IEnumerator HitstopCoroutine(Animator victimAnimator, ScaleController victimScale, float duration)
        {
            PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController pc in allPlayers)
            {
                Animator anim = pc.GetAnimator();
                pc.SetAnimatorSpeed(anim == victimAnimator ? victimStunAnimSpeed : 0f);
            }

            if (victimAnimator != null)
                victimAnimator.SetBool(AnimStun, true);

            if (victimScale != null)
                victimScale.PauseKnockback();

            SetCameraShake(true);

            yield return new WaitForSeconds(duration);

            ClearHitstopState();
        }

        private void ClearHitstopState()
        {
            PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController pc in allPlayers)
                pc.SetAnimatorSpeed(1f);

            if (currentVictimAnimator != null)
            {
                currentVictimAnimator.SetBool(AnimStun, false);
                currentVictimAnimator = null;
            }

            if (currentVictimScale != null)
            {
                currentVictimScale.ResumeKnockback();
                currentVictimScale = null;
            }

            SetCameraShake(false);
            activeHitstop = null;
        }

        private void SetCameraShake(bool enabled)
        {
            PlayerCameraController[] cams = FindObjectsByType<PlayerCameraController>(FindObjectsSortMode.None);
            foreach (PlayerCameraController cam in cams)
            {
                CinemachineBasicMultiChannelPerlin noise =
                    cam.GetComponent<CinemachineBasicMultiChannelPerlin>();
                if (noise != null)
                    noise.enabled = enabled;
            }
        }
    }
}
