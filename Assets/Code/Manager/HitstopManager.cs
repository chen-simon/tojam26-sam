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
        [SerializeField] private float attackerHitstopAnimSpeed = 0.05f;
        [SerializeField] private float victimStunAnimSpeed = 1f;
        [SerializeField] private float baseShakeAmplitude = 1f;
        [SerializeField] private float attackerShakeAmplitudeScale = 0.7f;
        [SerializeField] private float attackerMinShakeAmplitude = 8f;
        [SerializeField] private float victimMinShakeAmplitude = 12f;

        private Coroutine activeHitstop;
        private Animator currentVictimAnimator;
        private ScaleController currentVictimScale;
        private PlayerCameraController currentAttackerCamera;

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

        public void TriggerHitstop(Animator victimAnimator, ScaleController victimScale, float knockbackMultiplier, PlayerCameraController attackerCamera = null)
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
            currentAttackerCamera = attackerCamera;
            activeHitstop = StartCoroutine(HitstopCoroutine(victimAnimator, victimScale, duration, knockbackMultiplier));
        }

        private IEnumerator HitstopCoroutine(Animator victimAnimator, ScaleController victimScale, float duration, float knockbackMultiplier)
        {
            PlayerController[] allPlayers = FindObjectsByType<PlayerController>(FindObjectsSortMode.None);
            foreach (PlayerController pc in allPlayers)
            {
                Animator anim = pc.GetAnimator();
                pc.SetAnimatorSpeed(anim == victimAnimator ? victimStunAnimSpeed : attackerHitstopAnimSpeed);
            }

            if (victimAnimator != null)
                victimAnimator.SetBool(AnimStun, true);

            if (victimScale != null)
                victimScale.PauseKnockback();

            SetCameraShake(true, knockbackMultiplier);

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
            currentAttackerCamera = null;
            activeHitstop = null;
        }

        private void SetCameraShake(bool enabled, float knockbackMultiplier = 1f)
        {
            PlayerCameraController[] cams = FindObjectsByType<PlayerCameraController>(FindObjectsSortMode.None);
            foreach (PlayerCameraController cam in cams)
            {
                CinemachineBasicMultiChannelPerlin noise =
                    cam.GetComponent<CinemachineBasicMultiChannelPerlin>();
                if (noise == null)
                    continue;

                bool isAttacker = cam == currentAttackerCamera;
                float scale = isAttacker ? attackerShakeAmplitudeScale : 1f;
                float minAmplitude = isAttacker ? attackerMinShakeAmplitude : victimMinShakeAmplitude;
                noise.AmplitudeGain = Mathf.Max(baseShakeAmplitude * knockbackMultiplier * scale, minAmplitude);
                noise.enabled = enabled;
            }
        }
    }
}
