using UnityEngine;

public class RocketProjectile : MonoBehaviour
{
    private Vector3 direction;
    private float speed;
    private float maxDistance;
    private float explosionRadius;
    private float upwardModifier;
    private float explosionForcePlayer;
    private Rigidbody playerRigidbody;
    private GameObject explosionPrefab;
    private Vector3 spawnPosition;

    public void Initialize(Vector3 launchDirection, float rocketSpeed, float distanceLimit, float radius, float upward, float playerForce, Rigidbody playerRb, GameObject prefab)
    {
        if (launchDirection.sqrMagnitude < 0.0001f)
        {
            launchDirection = Vector3.forward;
        }

        direction = launchDirection.normalized;
        speed = rocketSpeed;
        maxDistance = distanceLimit;
        explosionRadius = radius;
        upwardModifier = upward;
        explosionForcePlayer = playerForce;
        playerRigidbody = playerRb;
        explosionPrefab = prefab;
        spawnPosition = transform.position;
    }

    private void Update()
    {
        if (direction.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning("[RocketProjectile] 방향이 비어 있어 forward로 보정합니다.");
            direction = Vector3.forward;
        }

        transform.position += direction * speed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        Debug.Log($"[RocketProjectile] 이동 중: 위치={transform.position}, 방향={direction}");

        if (Vector3.Distance(spawnPosition, transform.position) >= maxDistance)
        {
            Debug.Log("[RocketProjectile] 최대 사거리 도달로 폭발합니다.");
            Explode();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null)
        {
            return;
        }

        Debug.Log($"[RocketProjectile] 트리거 충돌: {other.name}");
        Explode();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider == null)
        {
            return;
        }

        Debug.Log($"[RocketProjectile] 물리 충돌: {collision.collider.name}");
        Explode();
    }

    private void Explode()
    {
        Debug.Log($"[RocketProjectile] 폭발 처리: 위치={transform.position}");

        if (explosionPrefab != null)
        {
            Instantiate(explosionPrefab, transform.position, Quaternion.identity);
        }

        if (playerRigidbody != null)
        {
            Vector3 explosionDirection = (playerRigidbody.position - transform.position).normalized;
            if (explosionDirection.sqrMagnitude < 0.0001f)
            {
                explosionDirection = Vector3.up;
            }

            playerRigidbody.AddExplosionForce(explosionForcePlayer, transform.position, explosionRadius, upwardModifier, ForceMode.Impulse);
        }

        Destroy(gameObject);
    }
}
