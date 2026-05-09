using UnityEngine;

public class DestroyAfterSeconds : MonoBehaviour
{
    [SerializeField] private float lifeTime = 3f;
    private float timeElapsed = 0f;

    void Update()
    {
        timeElapsed += Time.deltaTime;
        if (timeElapsed > lifeTime)
            Destroy(gameObject);
    }
}
