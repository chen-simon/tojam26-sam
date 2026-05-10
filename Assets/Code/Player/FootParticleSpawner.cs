using UnityEngine;

public class FootParticleSpawner : MonoBehaviour
{
    [SerializeField] private Transform leftFoot;
    [SerializeField] private Transform rightFoot;
    [SerializeField] private GameObject particlePrefab;
    [SerializeField] private float spawnHeight = 0.1f;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private float spawnSpeed = 2f;

    private bool leftWasAbove = true;
    private bool rightWasAbove = true;

    void Update()
    {
        float speed = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z).magnitude;

        bool leftIsAbove = (leftFoot.position.y - transform.position.y) > spawnHeight;
        bool rightIsAbove = (rightFoot.position.y - transform.position.y) > spawnHeight;

        if (speed >= spawnSpeed)
        {
            if (!leftIsAbove && leftWasAbove)
                Instantiate(particlePrefab, leftFoot.position, Quaternion.identity);

            if (!rightIsAbove && rightWasAbove)
                Instantiate(particlePrefab, rightFoot.position, Quaternion.identity);
        }

        leftWasAbove = leftIsAbove;
        rightWasAbove = rightIsAbove;
    }
}
