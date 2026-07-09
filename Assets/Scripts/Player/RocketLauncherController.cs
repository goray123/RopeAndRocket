using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class RocketLauncherController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Transform firePoint;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private GameObject rocketPrefab;
    [SerializeField] private GameObject explosionPrefab;

    [Header("Tuning")]
    [Tooltip("로켓 속도")][SerializeField, Range(1f, 100f)] private float rocketSpeed = 40f;
    [Tooltip("최대 사거리")][SerializeField, Range(10f, 300f)] private float maxDistance = 100f;
    [Tooltip("발사 반동")][SerializeField, Range(0f, 100f)] private float fireRecoilForce = 15f;
    [Tooltip("폭발 힘(플레이어)")][SerializeField, Range(0f, 100f)] private float explosionForcePlayer = 25f;
    [Tooltip("폭발 반경")][SerializeField, Range(1f, 20f)] private float explosionRadius = 6f;
    [Tooltip("폭발 위쪽 보정")][SerializeField, Range(0f, 5f)] private float explosionUpwardModifier = 0f;
    [Tooltip("쿨타임")][SerializeField, Range(0f, 10f)] private float cooldown = 0.8f;

    private InputAction fireAction;
    private bool canFire = true;
    private bool fireRequested;
    private bool firePressedThisFrame;

    private void Awake()
    {
        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody>();
        }

        if (playerInput == null)
        {
            playerInput = GetComponent<PlayerInput>();
        }

        if (playerInput != null)
        {
            Debug.Log($"[RocketLauncher] PlayerInput 발견: {playerInput.name}");
            Debug.Log($"[RocketLauncher] 액션 맵 수: {playerInput.actions.actionMaps.Count}");
            fireAction = playerInput.actions.FindAction("Fire");
            if (fireAction != null)
            {
                Debug.Log("[RocketLauncher] Fire 액션 연결됨.");
            }
            else
            {
                Debug.LogError("[RocketLauncher] Fire 액션을 찾지 못했습니다. Input Actions 이름을 확인하세요.");
            }
        }

        if (playerRigidbody == null)
        {
            Debug.LogError($"{nameof(RocketLauncherController)} requires a Rigidbody on {name}.", this);
        }
    }

    private void OnEnable()
    {
        if (fireAction != null)
        {
            fireAction.started += HandleFirePerformed;
            fireAction.performed += HandleFirePerformed;
            fireAction.Enable();
            Debug.Log("[RocketLauncher] Fire 액션 활성화 완료.");
        }
        else
        {
            Debug.LogWarning("[RocketLauncher] Fire 액션이 없어 OnEnable에서 등록하지 못했습니다.");
        }
    }

    private void OnDisable()
    {
        if (fireAction != null)
        {
            fireAction.started -= HandleFirePerformed;
            fireAction.performed -= HandleFirePerformed;
            fireAction.Disable();
        }
    }

    private void Update()
    {
        firePressedThisFrame = fireAction != null && fireAction.WasPressedThisFrame();

        if (firePressedThisFrame && canFire)
        {
            Debug.Log("[RocketLauncher] 프레임 기준 Fire 입력 감지.");
            fireRequested = true;
        }

        if (fireRequested && canFire)
        {
            fireRequested = false;
            FireRocket();
        }
    }

    private void HandleFirePerformed(InputAction.CallbackContext context)
    {
        Debug.Log($"[RocketLauncher] 입력 콜백 수신: phase={context.phase}, value={context.ReadValue<float>()}");

        if (context.performed)
        {
            Debug.Log("[RocketLauncher] E 입력 감지됨.");
        }

        if (context.performed && canFire)
        {
            Debug.Log("[RocketLauncher] 발사 요청 처리 시작.");
            fireRequested = true;
        }
        else if (context.performed && !canFire)
        {
            Debug.Log("[RocketLauncher] 쿨타임 중이라 발사하지 않음.");
        }
    }

    private void FireRocket()
    {
        if (!TryResolveFirePoint())
        {
            Debug.LogError("[RocketLauncher] Fire Point를 찾거나 생성하지 못했습니다.");
            return;
        }

        if (playerRigidbody == null)
        {
            Debug.LogError("[RocketLauncher] 플레이어 Rigidbody가 연결되지 않았습니다.");
            return;
        }

        Vector3 launchDirection = GetLaunchDirection();
        Vector3 spawnPosition = firePoint.position + launchDirection * 0.2f;
        Debug.Log($"[RocketLauncher] 발사 시작 위치: {spawnPosition}, 방향: {launchDirection}");

        playerRigidbody.AddForce(-launchDirection * fireRecoilForce, ForceMode.Impulse);
        Debug.Log($"[RocketLauncher] 발사 반동 적용: {(-launchDirection * fireRecoilForce).magnitude}");

        if (rocketPrefab != null)
        {
            Debug.Log("[RocketLauncher] 로켓 프리팹을 사용해 발사합니다.");
            GameObject rocketInstance = Instantiate(rocketPrefab, spawnPosition, Quaternion.LookRotation(launchDirection));
            RocketProjectile projectile = rocketInstance.GetComponent<RocketProjectile>();
            if (projectile == null)
            {
                projectile = rocketInstance.AddComponent<RocketProjectile>();
            }

            projectile.Initialize(launchDirection, rocketSpeed, maxDistance, explosionRadius, explosionUpwardModifier, explosionForcePlayer, playerRigidbody, explosionPrefab);
        }
        else
        {
            Debug.Log("[RocketLauncher] 로켓 프리팹이 없어 임시 로켓을 생성합니다.");
            GameObject tempRocket = new GameObject("RocketProjectile");
            tempRocket.transform.position = spawnPosition;
            tempRocket.transform.rotation = Quaternion.LookRotation(launchDirection);
            RocketProjectile projectile = tempRocket.AddComponent<RocketProjectile>();
            projectile.Initialize(launchDirection, rocketSpeed, maxDistance, explosionRadius, explosionUpwardModifier, explosionForcePlayer, playerRigidbody, explosionPrefab);
        }

        canFire = false;
        StartCoroutine(CooldownRoutine());
    }

    private bool TryResolveFirePoint()
    {
        if (firePoint != null)
        {
            return true;
        }

        firePoint = transform.Find("FirePoint");
        if (firePoint == null)
        {
            GameObject firePointObject = new GameObject("FirePoint");
            firePointObject.transform.SetParent(transform, false);
            firePointObject.transform.localPosition = new Vector3(0f, 0.5f, 1.2f);
            firePoint = firePointObject.transform;
        }

        return firePoint != null;
    }

    private Vector3 GetLaunchDirection()
    {
        Vector3 direction = Vector3.forward;

        if (cameraTransform != null && cameraTransform.forward.sqrMagnitude > 0.0001f)
        {
            direction = cameraTransform.forward;
        }
        else if (Camera.main != null && Camera.main.transform.forward.sqrMagnitude > 0.0001f)
        {
            direction = Camera.main.transform.forward;
        }
        else if (transform.forward.sqrMagnitude > 0.0001f)
        {
            direction = transform.forward;
        }

        return direction.normalized;
    }

    private IEnumerator CooldownRoutine()
    {
        yield return new WaitForSeconds(cooldown);
        canFire = true;
    }

    private void OnValidate()
    {
        rocketSpeed = Mathf.Clamp(rocketSpeed, 1f, 100f);
        maxDistance = Mathf.Clamp(maxDistance, 10f, 300f);
        fireRecoilForce = Mathf.Clamp(fireRecoilForce, 0f, 100f);
        explosionForcePlayer = Mathf.Clamp(explosionForcePlayer, 0f, 100f);
        explosionRadius = Mathf.Clamp(explosionRadius, 1f, 20f);
        explosionUpwardModifier = Mathf.Clamp(explosionUpwardModifier, 0f, 5f);
        cooldown = Mathf.Clamp(cooldown, 0f, 10f);
    }
}
