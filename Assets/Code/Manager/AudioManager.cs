using System.Collections;
using UnityEngine;
using ToJam26.Gameplay.Equipment;
using ToJam26.Gameplay.Player;

namespace ToJam26.Gameplay.Manager
{
    public class AudioManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager gameManager;

        [Header("Clips")]
        [SerializeField] private AudioClip sliceClip;
        [SerializeField] private AudioClip hitFleshClip;
        [SerializeField] private AudioClip koClip;
        [SerializeField] private AudioClip ambienceClip;
        [SerializeField] private AudioClip crowdCheerClip;

        [Header("Mix")]
        [SerializeField] private float sfxVolume = 1f;
        [SerializeField] private float ambienceVolume = 0.35f;
        [SerializeField] private float ambienceDuckedVolume = 0.12f;
        [SerializeField] private float crowdCheerVolume = 0.9f;
        [SerializeField] private float crowdFadeInSeconds = 0.2f;
        [SerializeField] private float crowdFadeOutSeconds = 0.45f;
        [SerializeField] private float ambienceFadeSeconds = 0.35f;

        [Header("Behaviour")]
        [SerializeField] private bool playAmbienceOnEnable = true;
        [SerializeField] private bool debugLogs = false;

        private AudioSource ambienceSource;
        private AudioSource crowdSource;
        private AudioSource sfxSource;
        private Coroutine crowdRoutine;

        private void Reset()
        {
            if (gameManager == null)
                gameManager = GetComponent<GameManager>();
        }

        private void Awake()
        {
            if (gameManager == null)
                gameManager = GetComponent<GameManager>();

            if (gameManager == null)
                gameManager = FindAnyObjectByType<GameManager>();

            EnsureAudioSources();
        }

        private void OnEnable()
        {
            Subscribe();

            if (playAmbienceOnEnable)
                StartAmbience();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            PlayerController.AttackStarted -= HandleAttackStarted;
            PlayerController.AttackStarted += HandleAttackStarted;

            KnifeBlade.HitResolved -= HandleKnifeHitResolved;
            KnifeBlade.HitResolved += HandleKnifeHitResolved;

            if (gameManager == null)
                return;

            gameManager.RoundScored -= HandleRoundScored;
            gameManager.LobbyEntered -= HandleLobbyEntered;

            gameManager.RoundScored += HandleRoundScored;
            gameManager.LobbyEntered += HandleLobbyEntered;
        }

        private void Unsubscribe()
        {
            PlayerController.AttackStarted -= HandleAttackStarted;
            KnifeBlade.HitResolved -= HandleKnifeHitResolved;

            if (gameManager == null)
                return;

            gameManager.RoundScored -= HandleRoundScored;
            gameManager.LobbyEntered -= HandleLobbyEntered;
        }

        private void EnsureAudioSources()
        {
            if (ambienceSource == null)
                ambienceSource = CreateChildSource("Ambience Source", loop: true, spatialBlend: 0f, priority: 180);

            if (crowdSource == null)
                crowdSource = CreateChildSource("Crowd Source", loop: false, spatialBlend: 0f, priority: 160);

            if (sfxSource == null)
                sfxSource = CreateChildSource("SFX Source", loop: false, spatialBlend: 0f, priority: 128);

            ambienceSource.clip = ambienceClip;
            ambienceSource.volume = ambienceVolume;
            crowdSource.volume = 0f;
            sfxSource.volume = 1f;
        }

        private AudioSource CreateChildSource(string sourceName, bool loop, float spatialBlend, int priority)
        {
            Transform existing = transform.Find(sourceName);
            GameObject sourceObject = existing != null ? existing.gameObject : new GameObject(sourceName);
            sourceObject.transform.SetParent(transform, false);

            AudioSource source = sourceObject.GetComponent<AudioSource>();
            if (source == null)
                source = sourceObject.AddComponent<AudioSource>();

            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatialBlend;
            source.priority = priority;
            source.dopplerLevel = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 5f;
            source.maxDistance = 30f;
            return source;
        }

        private void StartAmbience()
        {
            EnsureAudioSources();

            ambienceSource.clip = ambienceClip;
            ambienceSource.volume = ambienceVolume;

            if (ambienceClip == null || ambienceSource.isPlaying)
                return;

            ambienceSource.Play();
        }

        private void HandleAttackStarted()
        {
            PlayOneShot(sliceClip, sfxVolume);
        }

        private void HandleKnifeHitResolved(bool isKoHit)
        {
            PlayOneShot(hitFleshClip, sfxVolume);

            if (isKoHit)
                PlayOneShot(koClip, sfxVolume);
        }

        private void HandleRoundScored(ScaleController winner, int winnerScore)
        {
            if (debugLogs)
            {
                string winnerName = winner != null ? winner.name : "<none>";
                Debug.Log($"[AudioManager] Round scored by {winnerName}. Score: {winnerScore}", this);
            }

            if (crowdRoutine != null)
                StopCoroutine(crowdRoutine);

            crowdRoutine = StartCoroutine(PlayCrowdCheerRoutine());
        }

        private void HandleLobbyEntered()
        {
            if (crowdRoutine != null)
            {
                StopCoroutine(crowdRoutine);
                crowdRoutine = null;
            }

            EnsureAudioSources();
            crowdSource.Stop();
            crowdSource.volume = 0f;
            StartAmbience();
        }

        private IEnumerator PlayCrowdCheerRoutine()
        {
            EnsureAudioSources();
            StartAmbience();

            if (crowdCheerClip == null)
            {
                yield return FadeAmbienceTo(ambienceDuckedVolume, ambienceFadeSeconds);
                yield return FadeAmbienceTo(ambienceVolume, ambienceFadeSeconds);
                crowdRoutine = null;
                yield break;
            }

            crowdSource.Stop();
            crowdSource.clip = crowdCheerClip;
            crowdSource.volume = 0f;
            crowdSource.Play();

            float transitionSeconds = Mathf.Max(crowdFadeInSeconds, ambienceFadeSeconds, 0.01f);
            float elapsed = 0f;
            float ambienceStartVolume = ambienceSource.volume;

            while (elapsed < transitionSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / transitionSeconds);
                ambienceSource.volume = Mathf.Lerp(ambienceStartVolume, ambienceDuckedVolume, t);
                crowdSource.volume = Mathf.Lerp(0f, crowdCheerVolume, Mathf.Clamp01(elapsed / Mathf.Max(crowdFadeInSeconds, 0.01f)));
                yield return null;
            }

            ambienceSource.volume = ambienceDuckedVolume;
            crowdSource.volume = crowdCheerVolume;

            float sustainSeconds = Mathf.Max(0f, crowdCheerClip.length - crowdFadeInSeconds - crowdFadeOutSeconds);
            if (sustainSeconds > 0f)
                yield return new WaitForSecondsRealtime(sustainSeconds);

            transitionSeconds = Mathf.Max(crowdFadeOutSeconds, ambienceFadeSeconds, 0.01f);
            elapsed = 0f;
            float crowdStartVolume = crowdSource.volume;
            ambienceStartVolume = ambienceSource.volume;

            while (elapsed < transitionSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / transitionSeconds);
                crowdSource.volume = Mathf.Lerp(crowdStartVolume, 0f, t);
                ambienceSource.volume = Mathf.Lerp(ambienceStartVolume, ambienceVolume, Mathf.Clamp01(elapsed / Mathf.Max(ambienceFadeSeconds, 0.01f)));
                yield return null;
            }

            crowdSource.Stop();
            crowdSource.volume = 0f;
            ambienceSource.volume = ambienceVolume;
            crowdRoutine = null;
        }

        private IEnumerator FadeAmbienceTo(float targetVolume, float durationSeconds)
        {
            EnsureAudioSources();

            float elapsed = 0f;
            float startVolume = ambienceSource.volume;
            float duration = Mathf.Max(durationSeconds, 0.01f);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                ambienceSource.volume = Mathf.Lerp(startVolume, targetVolume, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            ambienceSource.volume = targetVolume;
        }

        private void PlayOneShot(AudioClip clip, float volumeScale)
        {
            EnsureAudioSources();

            if (clip == null)
                return;

            sfxSource.PlayOneShot(clip, volumeScale);
        }
    }
}
