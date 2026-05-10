using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using ToJam26.Gameplay.Manager;
using ToJam26.Gameplay.Player;
using UnityEngine;
using UnityEngine.UI;

namespace ToJam26.Gameplay.Interface
{
    public class ScoreBoardController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameManager gameManager;
        [SerializeField] private PlayerManager playerManager;
        [SerializeField] private RectTransform scoreBoardRoot;
        [SerializeField] private GameObject scoreStatPrefab;
        [SerializeField] private RectTransform p1ScoreRoot;
        [SerializeField] private RectTransform p2ScoreRoot;

        [Header("Animation")]
        [SerializeField] private string scoreAnimationStateName = "scored";
        [SerializeField] private float scoreAnimationDuration = 0.5f;
        [SerializeField] private float scorePopupDuration = 0.16f;
        [SerializeField] private float scorePopupStartScaleMultiplier = 0.55f;
        [SerializeField] private float iconFlyDuration = 0.55f;
        [SerializeField] private float centerScale = 3f;
        [SerializeField] private float landedScale = 1f;
        [SerializeField] private float iconSpacing = 92f;
        [SerializeField] private Ease scorePopupEase = Ease.OutBack;
        [SerializeField] private Ease iconMoveEase = Ease.OutCubic;
        [SerializeField] private Ease iconScaleEase = Ease.OutBack;
        [SerializeField] private int scorePopupPunchVibrato = 1;
        [SerializeField] private float scorePopupPunchElasticity = 0.85f;

        private readonly List<GameObject> spawnedIcons = new();
        private Coroutine scoreRoutine;

        private void Reset()
        {
            AutoAssignReferences();
        }

        private void Awake()
        {
            AutoAssignReferences();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (gameManager == null)
                return;

            gameManager.RoundScored -= HandleRoundScored;
            gameManager.LobbyEntered -= HandleLobbyEntered;

            gameManager.RoundScored += HandleRoundScored;
            gameManager.LobbyEntered += HandleLobbyEntered;
        }

        private void Unsubscribe()
        {
            if (gameManager == null)
                return;

            gameManager.RoundScored -= HandleRoundScored;
            gameManager.LobbyEntered -= HandleLobbyEntered;
        }

        private void AutoAssignReferences()
        {
            if (gameManager == null)
                gameManager = FindAnyObjectByType<GameManager>();

            if (playerManager == null)
                playerManager = FindAnyObjectByType<PlayerManager>();

            if (scoreBoardRoot == null)
                scoreBoardRoot = transform as RectTransform;

            if (p1ScoreRoot == null)
            {
                Transform child = transform.Find("P1 Score");
                if (child != null)
                    p1ScoreRoot = child as RectTransform;
            }

            if (p2ScoreRoot == null)
            {
                Transform child = transform.Find("P2 Score");
                if (child != null)
                    p2ScoreRoot = child as RectTransform;
            }
        }

        private void HandleRoundScored(ScaleController winner, int winnerScore)
        {
            if (winner == null || winnerScore <= 0)
                return;

            if (scoreRoutine != null)
                StopCoroutine(scoreRoutine);

            scoreRoutine = StartCoroutine(PlayScoreSequence(winner, winnerScore));
        }

        private void HandleLobbyEntered()
        {
            if (scoreRoutine != null)
            {
                StopCoroutine(scoreRoutine);
                scoreRoutine = null;
            }

            ClearSpawnedIcons();
        }

        private IEnumerator PlayScoreSequence(ScaleController winner, int winnerScore)
        {
            AutoAssignReferences();

            if (scoreBoardRoot == null || scoreStatPrefab == null)
            {
                scoreRoutine = null;
                yield break;
            }

            int playerSlot = ResolvePlayerSlot(winner);
            if (playerSlot < 0)
            {
                scoreRoutine = null;
                yield break;
            }

            GameObject scoreStatObject = CreateScoreStatObject(playerSlot, winnerScore - 1);
            if (scoreStatObject == null)
            {
                scoreRoutine = null;
                yield break;
            }

            RectTransform scoreStatRoot = scoreStatObject.GetComponent<RectTransform>();
            Image scoreStatImage = scoreStatObject.GetComponent<Image>();
            Animator scoreStatAnimator = scoreStatObject.GetComponent<Animator>();
            if (scoreStatRoot == null || scoreStatImage == null || scoreStatAnimator == null)
            {
                spawnedIcons.Remove(scoreStatObject);
                Destroy(scoreStatObject);
                scoreRoutine = null;
                yield break;
            }

            scoreStatAnimator.enabled = false;
            scoreStatRoot.SetAsLastSibling();
            scoreStatRoot.anchorMin = new Vector2(0.5f, 0.5f);
            scoreStatRoot.anchorMax = new Vector2(0.5f, 0.5f);
            scoreStatRoot.pivot = new Vector2(0.5f, 0.5f);
            scoreStatRoot.anchoredPosition = Vector2.zero;
            scoreStatRoot.localScale = Vector3.one * centerScale;

            Tween popupTween = scoreStatRoot
                .DOPunchScale(
                    Vector3.one * (centerScale * Mathf.Max(0f, scorePopupStartScaleMultiplier)),
                    scorePopupDuration,
                    Mathf.Max(1, scorePopupPunchVibrato),
                    Mathf.Clamp01(scorePopupPunchElasticity))
                .SetUpdate(true)
                .SetTarget(scoreStatRoot.gameObject);

            yield return popupTween.WaitForCompletion();

            yield return PlayScoreAnimationOnce(scoreStatAnimator);
            yield return MoveScoreStatToSlot(scoreStatRoot, playerSlot, winnerScore - 1);

            scoreRoutine = null;
        }

        private IEnumerator MoveScoreStatToSlot(RectTransform scoreStatRoot, int playerSlot, int scoreIndex)
        {
            if (scoreStatRoot == null)
                yield break;

            Vector3 targetPosition = GetTargetPosition(playerSlot, scoreIndex);

            Sequence flySequence = DOTween.Sequence();
            flySequence.SetTarget(scoreStatRoot.gameObject);
            flySequence.Join(scoreStatRoot.DOMove(targetPosition, iconFlyDuration).SetEase(iconMoveEase));
            flySequence.Join(scoreStatRoot.DOScale(landedScale, iconFlyDuration).SetEase(iconScaleEase));

            yield return flySequence.WaitForCompletion();
        }

        private Vector3 GetTargetPosition(int playerSlot, int scoreIndex)
        {
            RectTransform targetRoot = playerSlot == 0 ? p1ScoreRoot : p2ScoreRoot;
            if (targetRoot == null)
                return scoreBoardRoot != null ? scoreBoardRoot.position : Vector3.zero;

            float direction = playerSlot == 0 ? 1f : -1f;
            return targetRoot.position + new Vector3(iconSpacing * scoreIndex * direction, 0f, 0f);
        }

        private int ResolvePlayerSlot(ScaleController winner)
        {
            if (winner == null || playerManager == null)
                return -1;

            for (int index = 0; index < playerManager.Players.Count; index++)
            {
                if (playerManager.Players[index] == null)
                    continue;

                if (playerManager.Players[index].GetComponent<ScaleController>() == winner)
                    return index;
            }

            return -1;
        }

        private float GetScoreAnimationDuration()
        {
            if (scoreStatPrefab == null)
                return scoreAnimationDuration;

            Animator prefabAnimator = scoreStatPrefab.GetComponent<Animator>();
            if (prefabAnimator == null || prefabAnimator.runtimeAnimatorController == null)
                return scoreAnimationDuration;

            AnimationClip[] clips = prefabAnimator.runtimeAnimatorController.animationClips;
            foreach (AnimationClip clip in clips)
            {
                if (clip != null && clip.name == scoreAnimationStateName)
                    return clip.length;
            }

            return clips.Length > 0 && clips[0] != null ? clips[0].length : scoreAnimationDuration;
        }

        private IEnumerator PlayScoreAnimationOnce(Animator scoreStatAnimator)
        {
            if (scoreStatAnimator == null)
            {
                yield return new WaitForSecondsRealtime(scoreAnimationDuration);
                yield break;
            }

            scoreStatAnimator.enabled = true;
            scoreStatAnimator.Rebind();
            scoreStatAnimator.Update(0f);
            scoreStatAnimator.Play(scoreAnimationStateName, 0, 0f);
            scoreStatAnimator.Update(0f);

            float timeout = Mathf.Max(1f, GetScoreAnimationDuration() * 20f);
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                AnimatorStateInfo stateInfo = scoreStatAnimator.GetCurrentAnimatorStateInfo(0);
                if ((stateInfo.IsName(scoreAnimationStateName) || stateInfo.IsName($"Base Layer.{scoreAnimationStateName}")) &&
                    stateInfo.normalizedTime >= 1f)
                {
                    break;
                }

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            scoreStatAnimator.Update(0f);
            scoreStatAnimator.enabled = false;
        }

        private void ClearSpawnedIcons()
        {
            foreach (GameObject iconObject in spawnedIcons)
            {
                if (iconObject == null)
                    continue;

                DOTween.Kill(iconObject);
                Destroy(iconObject);
            }

            spawnedIcons.Clear();
        }

        private GameObject CreateScoreStatObject(int playerSlot, int scoreIndex)
        {
            if (scoreBoardRoot == null || scoreStatPrefab == null)
                return null;

            GameObject scoreStatObject = Instantiate(scoreStatPrefab, scoreBoardRoot);
            scoreStatObject.name = $"P{playerSlot + 1} Score Stat {scoreIndex + 1}";
            spawnedIcons.Add(scoreStatObject);
            return scoreStatObject;
        }
    }
}
