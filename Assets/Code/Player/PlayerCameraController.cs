using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.Serialization;

// Requires CinemachineOrbitalFollow on the same virtual camera GameObject.
// Remove CinemachineInputAxisController if present — axes are driven here.
// OrbitalFollow Heading should be WorldSpace; horizontal axis is absolute world yaw.
[RequireComponent(typeof(CinemachineOrbitalFollow))]
public class PlayerCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterController player;
    [SerializeField] private PlayerInput playerInput;

    [Header("Manual Look")]
    [FormerlySerializedAs("lookSensitivityH")]
    [SerializeField] private float gamepadLookSensitivityH = 180f;
    [FormerlySerializedAs("lookSensitivityV")]
    [SerializeField] private float gamepadLookSensitivityV = 90f;
    [SerializeField] private float mouseLookSensitivityH = 60f;
    [SerializeField] private float mouseLookSensitivityV = 30f;

    [Header("Camera Auto-Return")]
    [Tooltip("Maximum horizontal return rate at full speed.")]
    [SerializeField] private float horizontalReturnSpeed = 0.5f;
    [Tooltip("Maximum vertical return rate at full speed.")]
    [SerializeField] private float verticalReturnSpeed = 1f;
    [Tooltip("Match PlayerController.moveSpeed — used to scale return rate by actual speed.")]
    [SerializeField] private float referenceSpeed = 1f;
    [Tooltip("Resting vertical angle while moving.")]
    [SerializeField] private float defaultVertical = 30f;
    [Tooltip("Vertical angle when camera must arc overhead.")]
    [SerializeField] private float maxElevationAngle = 50f;

    [Header("Transition Framing")]
    [SerializeField] private float defaultReframeDuration = 0.75f;
    [SerializeField] private float anchorPositionSmoothFactor = 0.45f;
    [SerializeField] private float anchorRotationSharpness = 8f;

    private CinemachineOrbitalFollow _orbital;
    private CinemachineCamera _virtualCamera;
    private InputAction _lookAction;
    private InputAction _toggleLookAction;
    private bool _isFreeLook = false;
    private Transform _followAnchor;
    private Vector3 _followAnchorVelocity;
    private float _horizontalAxisVelocity;
    private float _verticalAxisVelocity;
    private float _reframeTimer;
    private float _reframeDuration;
    private float _reframeTargetHorizontal;
    private float _reframeTargetVertical;

    public bool IsFreeLook => _isFreeLook;

    void Awake()
    {
        _orbital = GetComponent<CinemachineOrbitalFollow>();
        _virtualCamera = GetComponent<CinemachineCamera>();
        EnsureFollowAnchor();
    }

    void Start()
    {
        EnsureFollowAnchor();

        if (player == null || _orbital == null)
            return;

        if (_reframeTimer <= 0f)
        {
            _orbital.HorizontalAxis.Value = player.transform.eulerAngles.y;
            _orbital.VerticalAxis.Value = defaultVertical;
            SnapAnchorToPlayer();
        }
    }

    void OnEnable()
    {
        if (playerInput != null && playerInput.actions != null)
        {
            _lookAction = playerInput.actions.FindAction("Look", throwIfNotFound: false);
            _toggleLookAction = playerInput.actions.FindAction("ToggleLook", throwIfNotFound: false);

            if (_lookAction == null)
                Debug.LogWarning("[PlayerCameraController] PlayerInput is missing a 'Look' action.", this);

            if (_toggleLookAction == null)
                Debug.LogWarning("[PlayerCameraController] PlayerInput is missing a 'ToggleLook' action.", this);
            else
                _toggleLookAction.performed += HandleToggleLook;
        }
        else
        {
            Debug.LogWarning("[PlayerCameraController] PlayerInput has no assigned actions asset.", this);
        }
    }

    void OnDisable()
    {
        if (_toggleLookAction != null)
            _toggleLookAction.performed -= HandleToggleLook;

        _lookAction = null;
        _toggleLookAction = null;
    }

    private void HandleToggleLook(InputAction.CallbackContext context)
    {
        _isFreeLook = !_isFreeLook;
    }

    public void SetSpawnOrientation(Transform spawnPoint)
    {
        if (_orbital == null || spawnPoint == null)
            return;

        _reframeTimer = 0f;
        _orbital.HorizontalAxis.Value = spawnPoint.eulerAngles.y;
        _orbital.VerticalAxis.Value = defaultVertical;

        if (_followAnchor != null)
            _followAnchor.rotation = spawnPoint.rotation;
    }

    public void ReframeToSpawn(Transform spawnPoint, float duration = -1f)
    {
        if (_orbital == null || player == null || spawnPoint == null)
            return;

        EnsureFollowAnchor();

        _reframeDuration = duration > 0f ? duration : defaultReframeDuration;
        _reframeTimer = Mathf.Max(0.01f, _reframeDuration);
        _reframeTargetHorizontal = spawnPoint.eulerAngles.y;
        _reframeTargetVertical = defaultVertical;
    }

    void Update()
    {
        UpdateFollowAnchor();

        Vector2 look = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;

        if (look.sqrMagnitude > 0.01f)
        {
            float lookSensitivityH = GetHorizontalLookSensitivity();
            float lookSensitivityV = GetVerticalLookSensitivity();

            _orbital.HorizontalAxis.Value += look.x * lookSensitivityH * Time.deltaTime;
            _orbital.VerticalAxis.Value = Mathf.Clamp(
                _orbital.VerticalAxis.Value - look.y * lookSensitivityV * Time.deltaTime,
                _orbital.VerticalAxis.Range.x,
                _orbital.VerticalAxis.Range.y
            );
        }

        if (!_isFreeLook)
            return;

        Vector3 flatVel = player.velocity;
        flatVel.y = 0f;
        float speed    = flatVel.magnitude;
        bool isLooking = look.sqrMagnitude > 0.01f;

        if (speed > 0.001f && !isLooking)
        {
            // Use velocity direction rather than character facing — facing lags movement
            // by a few frames and causes circular orbit when walking backward.
            float behindH = Mathf.Atan2(flatVel.x, flatVel.z) * Mathf.Rad2Deg;

            float hDiff       = Mathf.Abs(Mathf.DeltaAngle(_orbital.HorizontalAxis.Value, behindH));
            float inFrontness = Mathf.Pow(hDiff / 180f, 4);
            float targetV     = Mathf.Lerp(defaultVertical, maxElevationAngle, inFrontness);

            float vDiff = Mathf.Abs(Mathf.DeltaAngle(_orbital.VerticalAxis.Value, defaultVertical)) / 180f;
            float vAdjustmentFactor = vDiff * 6.25f + 0.025f;

            float hLerpT = horizontalReturnSpeed * Mathf.Clamp01(speed / referenceSpeed) * (1 - inFrontness) * Time.deltaTime;
            float vLerpT = verticalReturnSpeed * Mathf.Clamp01(speed / referenceSpeed) * vAdjustmentFactor * Time.deltaTime;

            _orbital.HorizontalAxis.Value = Mathf.LerpAngle(_orbital.HorizontalAxis.Value, behindH, hLerpT);
            _orbital.VerticalAxis.Value   = Mathf.Lerp(_orbital.VerticalAxis.Value, targetV, vLerpT);
        }
    }

    private void UpdateFollowAnchor()
    {
        if (player == null || _followAnchor == null)
            return;

        if (_reframeTimer <= 0f)
        {
            SnapAnchorToPlayer();
            return;
        }

        _reframeTimer = Mathf.Max(0f, _reframeTimer - Time.deltaTime);
        float smoothTime = Mathf.Max(0.01f, _reframeDuration * anchorPositionSmoothFactor);

        _followAnchor.position = Vector3.SmoothDamp(
            _followAnchor.position,
            player.transform.position,
            ref _followAnchorVelocity,
            smoothTime);

        float rotationLerp = 1f - Mathf.Exp(-anchorRotationSharpness * Time.deltaTime / Mathf.Max(0.01f, _reframeDuration));
        _followAnchor.rotation = Quaternion.Slerp(_followAnchor.rotation, player.transform.rotation, rotationLerp);

        if (_orbital != null)
        {
            _orbital.HorizontalAxis.Value = Mathf.SmoothDampAngle(
                _orbital.HorizontalAxis.Value,
                _reframeTargetHorizontal,
                ref _horizontalAxisVelocity,
                smoothTime);

            _orbital.VerticalAxis.Value = Mathf.SmoothDamp(
                _orbital.VerticalAxis.Value,
                _reframeTargetVertical,
                ref _verticalAxisVelocity,
                smoothTime);
        }

        if (_reframeTimer > 0f)
            return;

        SetSpawnOrientationFromCurrentState();
        SnapAnchorToPlayer();
    }

    private void EnsureFollowAnchor()
    {
        bool createdAnchor = false;
        if (_followAnchor == null)
        {
            GameObject anchorObject = new($"{name} Follow Anchor");
            _followAnchor = anchorObject.transform;
            createdAnchor = true;
        }

        if (createdAnchor && player != null)
            _followAnchor.SetPositionAndRotation(player.transform.position, player.transform.rotation);

        if (_virtualCamera != null)
            _virtualCamera.Follow = _followAnchor;
    }

    private void SnapAnchorToPlayer()
    {
        if (player == null || _followAnchor == null)
            return;

        _followAnchor.position = player.transform.position;
        _followAnchor.rotation = player.transform.rotation;
        _followAnchorVelocity = Vector3.zero;
    }

    private void SetSpawnOrientationFromCurrentState()
    {
        if (_orbital == null)
            return;

        _orbital.HorizontalAxis.Value = _reframeTargetHorizontal;
        _orbital.VerticalAxis.Value = _reframeTargetVertical;
        _horizontalAxisVelocity = 0f;
        _verticalAxisVelocity = 0f;
    }

    private float GetHorizontalLookSensitivity()
    {
        return IsUsingMouseLook() ? mouseLookSensitivityH : gamepadLookSensitivityH;
    }

    private float GetVerticalLookSensitivity()
    {
        return IsUsingMouseLook() ? mouseLookSensitivityV : gamepadLookSensitivityV;
    }

    private bool IsUsingMouseLook()
    {
        if (playerInput != null && !string.IsNullOrEmpty(playerInput.currentControlScheme))
            return playerInput.currentControlScheme == "Keyboard&Mouse";

        InputControl activeControl = _lookAction != null ? _lookAction.activeControl : null;
        return activeControl != null && activeControl.device is Mouse;
    }

    private void OnDestroy()
    {
        if (_followAnchor != null)
            Destroy(_followAnchor.gameObject);
    }
}
