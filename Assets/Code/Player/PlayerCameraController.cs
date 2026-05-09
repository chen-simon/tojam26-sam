using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;

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
    [SerializeField] private float lookSensitivityH = 180f;
    [SerializeField] private float lookSensitivityV = 90f;

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

    private CinemachineOrbitalFollow _orbital;
    private InputAction _lookAction;
    private InputAction _toggleLookAction;
    private bool _isFreeLook = false;

    public bool IsFreeLook => _isFreeLook;

    void Awake()
    {
        _orbital = GetComponent<CinemachineOrbitalFollow>();
    }

    void Start()
    {
        _orbital.HorizontalAxis.Value = player.transform.eulerAngles.y;
        _orbital.VerticalAxis.Value = defaultVertical;
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
        _orbital.HorizontalAxis.Value = spawnPoint.eulerAngles.y;
        _orbital.VerticalAxis.Value = defaultVertical;
    }

    void Update()
    {
        Vector2 look = _lookAction != null ? _lookAction.ReadValue<Vector2>() : Vector2.zero;

        if (look.sqrMagnitude > 0.01f)
        {
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
}
