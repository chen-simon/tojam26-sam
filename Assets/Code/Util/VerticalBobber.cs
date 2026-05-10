using UnityEngine;

public class VerticalBobber : MonoBehaviour
{
    [Header("Heights (relative to spawn)")]
    public float lowOffset = 0f;
    public float highOffset = 2f;

    [Header("Timing")]
    public float moveTime = 0.3f;
    public float pauseTime = 1f;
    public float pauseRandomRange = 0f;
    public float startOffset = 0f;

    private Vector3 _spawnPos;
    private Vector3 _targetPos;
    private Vector3 _startPos;
    private float _moveT;
    private float _pauseTimer;
    private bool _goingUp = true;
    private bool _moving = false;
    private bool _offsetting = true;

    void Start()
    {
        _spawnPos = transform.position;
        _startPos = transform.position;
        _targetPos = _spawnPos + Vector3.up * highOffset;
        _pauseTimer = startOffset;
    }

    void Update()
    {
        if (_offsetting)
        {
            _pauseTimer -= Time.deltaTime;
            if (_pauseTimer <= 0f)
            {
                _offsetting = false;
                _moving = true;
                _moveT = 0f;
            }
            return;
        }

        if (_moving)
        {
            _moveT += Time.deltaTime / moveTime;
            transform.position = Vector3.Lerp(_startPos, _targetPos, Mathf.Clamp01(_moveT));

            if (_moveT >= 1f)
            {
                transform.position = _targetPos;
                _moving = false;
                _pauseTimer = pauseTime + Random.Range(-pauseRandomRange, pauseRandomRange);
            }
        }
        else
        {
            _pauseTimer -= Time.deltaTime;
            if (_pauseTimer <= 0f)
            {
                _goingUp = !_goingUp;
                _startPos = transform.position;
                _targetPos = _spawnPos + Vector3.up * (_goingUp ? highOffset : lowOffset);
                _moveT = 0f;
                _moving = true;
            }
        }
    }
}
