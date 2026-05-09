using UnityEngine;

public class Childer : MonoBehaviour
{
    [SerializeField] Transform target;

    void Start()
    {
        transform.SetParent(target);
    }
}
