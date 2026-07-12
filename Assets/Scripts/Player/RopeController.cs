using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class RopeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody playerRigidbody;
    [SerializeField] private Transform ropeOrigin;
    [SerializeField] private Transform handCube;
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private LineRenderer ropeLine;
    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private LayerMask grappleMask = ~0;

    [Header("Momentum Tuning")]
    [Tooltip("로프 발사 속도")][SerializeField, Range(10f, 200f)] private float fireSpeed = 70f;
    [Tooltip("최대 연결 거리")][SerializeField, Range(1f, 50f)] private float maxGrappleDistance = 30f;
    [Tooltip("앵커 충돌 반경")][SerializeField, Range(0.01f, 0.5f)] private float anchorRadius = 0.08f;
    [Tooltip("로프 길이 변경 속도")][SerializeField, Range(0.1f, 8f)] private float lengthChangeSpeed = 3f;
    [Tooltip("스페이스바 당김 추진력")][SerializeField] private float pullImpulse = 12f;
    [Tooltip("스윙 시 모멘텀 생성 강도")][SerializeField, Range(0.1f, 20f)] private float swingAssistStrength = 5f;
    [Tooltip("접선 속도 유지 비율")][SerializeField, Range(0.1f, 1f)] private float momentumRetention = 0.98f;
    [Tooltip("반경 방향 감쇠")][SerializeField, Range(0f, 1f)] private float radialDamping = 0.15f;
    [Tooltip("로프 폭")][SerializeField, Range(0.005f, 0.2f)] private float ropeWidth = 0.05f;
    [SerializeField, Range(1f, 30f)] private float handRotationSpeed = 12f;

    private enum RopeState
    {
        Ready,
        Firing,
        Connected,
    }

    private RopeState state;
    private InputAction grappleAction;
    private Vector3 anchorPosition;
    private Vector3 anchorVelocity;
    private float travelDistance;
    private float ropeLength;
    private bool grappleHeld;
    private bool pullHeld;

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

        if (playerRigidbody == null)
        {
            Debug.LogError($"{nameof(RopeController)} requires a Rigidbody on {name}.", this);
        }

        if (playerInput != null)
        {
            grappleAction = playerInput.actions.FindAction("Grapple");
        }
        else
        {
            Debug.LogError($"{nameof(RopeController)} requires a PlayerInput component on {name}.", this);
        }

        if (ropeLine == null)
        {
            ropeLine = GetComponent<LineRenderer>();
        }

        if (ropeLine == null)
        {
            ropeLine = gameObject.AddComponent<LineRenderer>();
        }

        ropeLine.positionCount = 2;
        ropeLine.startWidth = ropeWidth;
        ropeLine.endWidth = ropeWidth;
        ropeLine.useWorldSpace = true;
        ropeLine.enabled = false;
    }

    private void OnEnable()
    {
        if (grappleAction != null)
        {
            grappleAction.started += HandleGrappleStarted;
            grappleAction.canceled += HandleGrappleCanceled;
            grappleAction.Enable();
        }

        ResetInputState();
    }

    private void OnDisable()
    {
        if (grappleAction != null)
        {
            grappleAction.started -= HandleGrappleStarted;
            grappleAction.canceled -= HandleGrappleCanceled;
            grappleAction.Disable();
        }

        ResetInputState();
        ReleaseRope();
    }

    private void FixedUpdate()
    {
        if (!IsReady())
        {
            return;
        }

        if (state == RopeState.Firing)
        {
            AdvanceAnchor();
        }
        else if (state == RopeState.Connected)
        {
            UpdateRopeLength();
            ApplyPullInput();
            PreserveMomentum();
            EnforceRopeLength();
        }

        UpdateHandMotion();
        UpdateRopeVisual();
    }

    private void Update()
    {
        if (state == RopeState.Ready || state == RopeState.Firing || state == RopeState.Connected)
        {
            UpdateHandMotion();
            UpdateRopeVisual();
        }
    }

    private void HandleGrappleStarted(InputAction.CallbackContext context)
    {
        if (!context.started)
        {
            return;
        }

        grappleHeld = true;

        if (state == RopeState.Ready)
        {
            FireRope();
            return;
        }

        if (state == RopeState.Connected)
        {
            return;
        }
    }

    private void HandleGrappleCanceled(InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            grappleHeld = false;
            ReleaseRope();
        }
    }

    private void FireRope()
    {
        if (state != RopeState.Ready)
        {
            return;
        }

        if (cameraTransform == null || ropeOrigin == null || playerRigidbody == null)
        {
            Debug.LogError($"{nameof(RopeController)} has missing references.", this);
            return;
        }

        state = RopeState.Firing;
        anchorPosition = cameraTransform.position;
        anchorVelocity = cameraTransform.forward * fireSpeed;
        travelDistance = 0f;
        ropeLength = 0f;
        ropeLine.enabled = true;
        ropeLine.SetPosition(0, ropeOrigin.position);
        ropeLine.SetPosition(1, anchorPosition);
    }

    private void AdvanceAnchor()
    {
        Vector3 nextPosition = anchorPosition + anchorVelocity * Time.fixedDeltaTime;
        Vector3 direction = nextPosition - anchorPosition;

        if (direction.sqrMagnitude > 0.0001f)
        {
            RaycastHit hit;
            if (Physics.SphereCast(anchorPosition, anchorRadius, direction.normalized, out hit, direction.magnitude, grappleMask, QueryTriggerInteraction.Ignore))
            {
                if (IsValidHit(hit))
                {
                    ConnectToHit(hit);
                    return;
                }

                ReleaseRope();
                return;
            }
        }

        anchorPosition = nextPosition;
        travelDistance += direction.magnitude;
    }

    private void ConnectToHit(RaycastHit hit)
    {
        state = RopeState.Connected;
        anchorPosition = hit.point;
        anchorVelocity = Vector3.zero;
        ropeLength = Vector3.Distance(playerRigidbody.position, anchorPosition);
        ropeLine.enabled = true;
        Debug.Log($"[Rope] connected to {hit.collider.name}, distance={ropeLength}");
        UpdateRopeVisual();
    }

    private void UpdateRopeLength()
    {
        if (Mouse.current == null)
        {
            return;
        }

        float scrollDelta = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollDelta) < 0.0001f)
        {
            return;
        }

        float delta = scrollDelta > 0f ? -lengthChangeSpeed * Time.fixedDeltaTime : lengthChangeSpeed * Time.fixedDeltaTime;
        ropeLength = Mathf.Max(0.1f, ropeLength + delta);
    }

    private void ApplyPullInput()
    {
        bool spaceHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
        if (!spaceHeld)
        {
            pullHeld = false;
            Debug.Log("[Rope] space released");
            return;
        }

        pullHeld = true;
        Debug.Log($"[Rope] space held, state={state}, position={playerRigidbody.position}, anchor={anchorPosition}");

        if (playerInput != null)
        {
            InputAction jumpAction = playerInput.actions.FindAction("Jump");
            if (jumpAction != null)
            {
                jumpAction.Disable();
                Debug.Log("[Rope] jump action disabled for rope pull");
            }
        }

        Vector3 toAnchor = anchorPosition - playerRigidbody.position;
        float distance = toAnchor.magnitude;
        Debug.Log($"[Rope] distance to anchor={distance}, pullImpulse={pullImpulse}");

        if (distance < 0.0001f)
        {
            Debug.Log("[Rope] distance too small, skip pull");
            return;
        }

        Vector3 pullDirection = toAnchor / distance;
        playerRigidbody.AddForce(pullDirection * pullImpulse, ForceMode.Acceleration);
        Debug.Log($"[Rope] applied force={pullDirection * pullImpulse}");
    }

    private void PreserveMomentum()
    {
        Vector3 toAnchor = anchorPosition - playerRigidbody.position;
        float distance = toAnchor.magnitude;

        if (distance < 0.0001f)
        {
            ReleaseRope();
            return;
        }

        Vector3 ropeDirection = toAnchor / distance;
        Vector3 tangentVelocity = Vector3.ProjectOnPlane(playerRigidbody.linearVelocity, ropeDirection);
        Vector3 radialVelocity = Vector3.Project(playerRigidbody.linearVelocity, ropeDirection);

        Vector2 swingInput = GetSwingInput();
        Vector3 swingForward = GetSwingForward(ropeDirection);
        Vector3 swingRight = Vector3.Cross(ropeDirection, swingForward).normalized;

        if (swingRight.sqrMagnitude < 0.0001f)
        {
            swingRight = Vector3.Cross(ropeDirection, Vector3.forward).normalized;
        }

        Vector3 desiredTangent = (swingForward * swingInput.y) + (swingRight * swingInput.x);
        if (desiredTangent.sqrMagnitude < 0.0001f)
        {
            desiredTangent = tangentVelocity.sqrMagnitude > 0.0001f ? tangentVelocity.normalized : swingForward;
        }

        Vector3 retainedVelocity = tangentVelocity * momentumRetention;
        Vector3 inputBoost = desiredTangent.normalized * swingAssistStrength * Time.fixedDeltaTime * Mathf.Max(0.25f, Mathf.Abs(swingInput.y));
        inputBoost += desiredTangent.normalized * swingAssistStrength * Time.fixedDeltaTime * 0.35f * swingInput.x;

        Vector3 nextVelocity = radialVelocity * (1f - radialDamping) + retainedVelocity + inputBoost;
        playerRigidbody.linearVelocity = nextVelocity;
    }

    private void EnforceRopeLength()
    {
        Vector3 toAnchor = anchorPosition - playerRigidbody.position;
        float distance = toAnchor.magnitude;

        if (distance <= ropeLength)
        {
            return;
        }

        Vector3 ropeDirection = toAnchor / distance;
        Vector3 correctedPosition = anchorPosition - (ropeDirection * ropeLength);
        playerRigidbody.position = correctedPosition;

        Vector3 radialVelocity = Vector3.Project(playerRigidbody.linearVelocity, ropeDirection);
        Vector3 tangentVelocity = Vector3.ProjectOnPlane(playerRigidbody.linearVelocity, ropeDirection);
        playerRigidbody.linearVelocity = tangentVelocity + (radialVelocity * 0.25f);
    }

    private Vector2 GetSwingInput()
    {
        if (playerInput == null)
        {
            return Vector2.zero;
        }

        InputAction moveAction = playerInput.actions.FindAction("Move");
        if (moveAction == null)
        {
            return Vector2.zero;
        }

        return Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
    }

    private Vector3 GetSwingForward(Vector3 ropeDirection)
    {
        if (cameraTransform != null)
        {
            Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, ropeDirection).normalized;
            if (cameraForward.sqrMagnitude > 0.0001f)
            {
                return cameraForward;
            }
        }

        Vector3 fallback = Vector3.Cross(ropeDirection, Vector3.up);
        if (fallback.sqrMagnitude < 0.0001f)
        {
            fallback = Vector3.Cross(ropeDirection, Vector3.right);
        }

        return fallback.normalized;
    }

    private void ReleaseRope()
    {
        if (playerInput != null)
        {
            InputAction jumpAction = playerInput.actions.FindAction("Jump");
            if (jumpAction != null)
            {
                jumpAction.Enable();
                Debug.Log("[Rope] jump action re-enabled");
            }
        }

        state = RopeState.Ready;
        anchorPosition = Vector3.zero;
        anchorVelocity = Vector3.zero;
        travelDistance = 0f;
        ropeLength = 0f;
        ropeLine.enabled = false;
        Debug.Log("[Rope] released");

        if (handCube != null)
        {
            handCube.localRotation = Quaternion.identity;
        }
    }

    private bool IsValidHit(RaycastHit hit)
    {
        if (hit.collider == null || hit.collider.isTrigger)
        {
            return false;
        }

        if (playerRigidbody != null && hit.collider == playerRigidbody.GetComponent<Collider>())
        {
            return false;
        }

        return ((1 << hit.collider.gameObject.layer) & grappleMask.value) != 0;
    }

    private void UpdateHandMotion()
    {
        Transform rotationTarget = handCube != null ? handCube : ropeOrigin;
        if (rotationTarget == null)
        {
            return;
        }

        Vector3 targetDirection = GetHandTargetDirection();
        if (targetDirection.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Vector3.up);
        rotationTarget.rotation = Quaternion.Slerp(rotationTarget.rotation, targetRotation, handRotationSpeed * Time.deltaTime);
    }

    private Vector3 GetHandTargetDirection()
    {
        if (state == RopeState.Firing)
        {
            return anchorVelocity.normalized;
        }

        if (state == RopeState.Connected)
        {
            Vector3 referencePosition = ropeOrigin != null ? ropeOrigin.position : transform.position;
            return (anchorPosition - referencePosition).normalized;
        }

        return Vector3.zero;
    }

    private void UpdateRopeVisual()
    {
        if (!ropeLine.enabled || ropeOrigin == null)
        {
            return;
        }

        ropeLine.SetPosition(0, ropeOrigin.position);
        ropeLine.SetPosition(1, anchorPosition);
        ropeLine.startWidth = ropeWidth;
        ropeLine.endWidth = ropeWidth;
    }

    private bool IsReady()
    {
        return playerRigidbody != null && ropeOrigin != null && cameraTransform != null && ropeLine != null && playerInput != null && playerInput.enabled;
    }

    private void ResetInputState()
    {
        grappleHeld = false;
        pullHeld = false;
    }
}
