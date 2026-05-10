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
        [SerializeField] private AudioClip bgmIntroClip;
        [SerializeField] private AudioClip countdownClip;
        [SerializeField] private AudioClip battleBgmClip;

        [Header("Mix")]
        [SerializeField] private float sfxVolume = 1f;
        [SerializeField] private float bgmVolume = 0.7f;
        [SerializeField] private float ambienceVolume = 0.35f;
        [SerializeField] private float ambienceDuckedVolume = 0.12f;
        [SerializeField] private float crowdCheerVolume = 0.9f;
        [SerializeField] private float hitCrowdCheerVolume = 0.2f;
        [SerializeField] private float musicTransitionSeconds = 0.2f;
        [SerializeField] private float crowdFadeInSeconds = 0.2f;
        [SerializeField] private float crowdFadeOutSeconds = 0.45f;
        [SerializeField] private float ambienceFadeSeconds = 0.35f;

        [Header("Behaviour")]
        [SerializeField] private bool playAmbienceOnEnable = true;
        [SerializeField] private bool debugLogs = false;

        private AudioSource ambienceSource;
        private AudioSource crowdSource;
        private AudioSource sfxSource;
        private AudioSource musicSource;
        private AudioSource countdownSource;
        private AudioSource crowdHitSource;
        private Coroutine crowdRoutine;
        private Coroutine musicRoutine;

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

            gameManager.RoundIntroStarted -= HandleRoundIntroStarted;
            gameManager.RoundScored -= HandleRoundScored;
            gameManager.RoundPreparationStarted -= HandleRoundPreparationStarted;
            gameManager.RoundStarted -= HandleRoundStarted;
            gameManager.LobbyEntered -= HandleLobbyEntered;

            gameManager.RoundIntroStarted += HandleRoundIntroStarted;
            gameManager.RoundScored += HandleRoundScored;
            gameManager.RoundPreparationStarted += HandleRoundPreparationStarted;
            gameManager.RoundStarted += HandleRoundStarted;
            gameManager.LobbyEntered += HandleLobbyEntered;
        }

        private void Unsubscribe()
        {
            PlayerController.AttackStarted -= HandleAttackStarted;
            KnifeBlade.HitResolved -= HandleKnifeHitResolved;

            if (gameManager == null)
                return;

            gameManager.RoundIntroStarted -= HandleRoundIntroStarted;
            gameManager.RoundScored -= HandleRoundScored;
            gameManager.RoundPreparationStarted -= HandleRoundPreparationStarted;
            gameManager.RoundStarted -= HandleRoundStarted;
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

            if (musicSource == null)
                musicSource = CreateChildSource("Music Source", loop: true, spatialBlend: 0f, priority: 140);

            if (countdownSource == null)
                countdownSource = CreateChildSource("Countdown Source", loop: false, spatialBlend: 0f, priority: 145);

            if (crowdHitSource == null)
                crowdHitSource = CreateChildSource("Crowd Hit Source", loop: false, spatialBlend: 0f, priority: 150);

            ambienceSource.clip = ambienceClip;
            ambienceSource.volume = ambienceVolume;
            crowdSource.volume = 0f;
            sfxSource.volume = 1f;
            musicSource.volume = bgmVolume;
            countdownSource.volume = bgmVolume;
            crowdHitSource.volume = 1f;
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

        private void HandleRoundIntroStarted(int roundNumber, int maxRounds)
        {
            PlayMusic(bgmIntroClip, loop: false);
        }

        private void HandleRoundPreparationStarted(int roundNumber, int maxRounds)
        {
            PlayCountdownOverlay();
        }

        private void HandleRoundStarted(int roundNumber, int maxRounds, float roundDuration)
        {
            StopCountdownOverlay();
            PlayMusic(battleBgmClip, loop: true);
        }

        private void HandleKnifeHitResolved(bool isKoHit)
        {
            PlayOneShot(hitFleshClip, sfxVolume);
            PlayCrowdHitCheer();

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
            StopCountdownOverlay();
            StopMusic();
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

        private void PlayCrowdHitCheer()
        {
            EnsureAudioSources();

            if (crowdCheerClip == null || crowdHitSource == null)
                return;

            crowdHitSource.PlayOneShot(crowdCheerClip, hitCrowdCheerVolume);
        }

        private void PlayCountdownOverlay()
        {
            EnsureAudioSources();

            if (countdownSource == null || countdownClip == null)
                return;

            countdownSource.Stop();
            countdownSource.clip = countdownClip;
            countdownSource.loop = false;
            countdownSource.volume = bgmVolume;
            countdownSource.Play();
        }

        private void StopCountdownOverlay()
        {
            EnsureAudioSources();

            if (countdownSource == null)
                return;

            countdownSource.Stop();
            countdownSource.clip = null;
            countdownSource.volume = bgmVolume;
        }

        private void PlayMusic(AudioClip clip, bool loop)
        {
            EnsureAudioSources();

            if (musicRoutine != null)
                StopCoroutine(musicRoutine);

            musicRoutine = StartCoroutine(PlayMusicRoutine(clip, loop));
        }

        private IEnumerator PlayMusicRoutine(AudioClip clip, bool loop)
        {
            if (musicSource == null)
                yield break;

            float fadeSeconds = Mathf.Max(0.01f, musicTransitionSeconds);
            float startVolume = musicSource.volume;

            if (musicSource.isPlaying && startVolume > 0f)
            {
                float elapsed = 0f;
                while (elapsed < fadeSeconds)
                {
                    elapsed += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(elapsed / fadeSeconds));
                    yield return null;
                }
            }

            if (clip == null)
            {
                musicSource.Stop();
                musicSource.clip = null;
                musicSource.volume = bgmVolume;
                musicRoutine = null;
                yield break;
            }

            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.volume = 0f;
            musicSource.Play();

            float fadeInElapsed = 0f;
            while (fadeInElapsed < fadeSeconds)
            {
                fadeInElapsed += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(0f, bgmVolume, Mathf.Clamp01(fadeInElapsed / fadeSeconds));
                yield return null;
            }

            musicSource.volume = bgmVolume;
            musicRoutine = null;
        }

        private void StopMusic()
        {
            EnsureAudioSources();

            if (musicRoutine != null)
            {
                StopCoroutine(musicRoutine);
                musicRoutine = null;
            }

            if (musicSource == null)
                return;

            musicSource.Stop();
            musicSource.clip = null;
            musicSource.volume = bgmVolume;
        }
    }
}
