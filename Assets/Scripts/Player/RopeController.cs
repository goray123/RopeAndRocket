using System;
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

    [Header("Tuning")]
    [Tooltip("앵커 발사 속도")][SerializeField, Range(1f, 150f)] private float anchorSpeed = 120f;
    [Tooltip("최대 연결 거리")][SerializeField, Range(1f, 100f)] private float maxGrappleDistance = 50f;
    [Tooltip("앵커 충돌 반경")][SerializeField, Range(0.01f, 0.5f)] private float anchorRadius = 0.08f;
    [Tooltip("SpringJoint 탄성")][SerializeField, Range(0f, 200f)] private float spring = 120f;
    [Tooltip("SpringJoint 감쇠")][SerializeField, Range(0f, 50f)] private float damper = 20f;
    [Tooltip("최대 거리 비율")][SerializeField, Range(0.1f, 1f)] private float maxDistanceRatio = 0.9f;
    [Tooltip("최소 거리 비율")][SerializeField, Range(0f, 1f)] private float minDistanceRatio = 0.35f;
    [Tooltip("앵커 가속도")][SerializeField, Range(0f, 150f)] private float pullAcceleration = 90f;
    [Tooltip("가속 중단 거리")][SerializeField, Range(0.1f, 5f)] private float pullStopDistance = 1.2f;
    [Tooltip("로프 폭")][SerializeField, Range(0.005f, 0.2f)] private float ropeWidth = 0.05f;
    [SerializeField, Range(1f, 30f)] private float handRotationSpeed = 14f;

    private enum RopeState
    {
        Ready,
        Firing,
        Connected,
    }

    private RopeState state;
    private InputAction grappleAction;
    private InputAction jumpAction;
    private Vector3 anchorPosition;
    private Vector3 anchorVelocity;
    private float travelDistance;
    private SpringJoint springJoint;
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

        if (playerInput == null)
        {
            Debug.LogError($"{nameof(RopeController)} requires a PlayerInput component on {name}.", this);
        }
        else
        {
            grappleAction = playerInput.actions.FindAction("Grapple");
            jumpAction = playerInput.actions.FindAction("Jump");
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
            grappleAction.performed += HandleGrapplePerformed;
            grappleAction.canceled += HandleGrappleCanceled;
            grappleAction.Enable();
        }

        if (jumpAction != null)
        {
            jumpAction.performed += HandleJumpPerformed;
            jumpAction.canceled += HandleJumpCanceled;
            jumpAction.Enable();
        }

        ResetInputState();
    }

    private void OnDisable()
    {
        if (grappleAction != null)
        {
            grappleAction.performed -= HandleGrapplePerformed;
            grappleAction.canceled -= HandleGrappleCanceled;
            grappleAction.Disable();
        }

        if (jumpAction != null)
        {
            jumpAction.performed -= HandleJumpPerformed;
            jumpAction.canceled -= HandleJumpCanceled;
            jumpAction.Disable();
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
            ApplyPullForce();
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

    private void HandleGrapplePerformed(InputAction.CallbackContext context)
    {
        if (context.performed && !grappleHeld)
        {
            grappleHeld = true;
            BeginGrapple();
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

    private void HandleJumpPerformed(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            pullHeld = true;
        }
    }

    private void HandleJumpCanceled(InputAction.CallbackContext context)
    {
        if (context.canceled)
        {
            pullHeld = false;
        }
    }

    private void BeginGrapple()
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
        anchorVelocity = cameraTransform.forward * anchorSpeed;
        travelDistance = 0f;
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
        if (travelDistance >= maxGrappleDistance)
        {
            ReleaseRope();
        }
    }

    private void ConnectToHit(RaycastHit hit)
    {
        state = RopeState.Connected;
        anchorPosition = hit.point;
        anchorVelocity = Vector3.zero;

        if (springJoint == null)
        {
            springJoint = gameObject.AddComponent<SpringJoint>();
        }

        springJoint.connectedAnchor = anchorPosition;
        springJoint.anchor = Vector3.zero;
        springJoint.autoConfigureConnectedAnchor = false;
        springJoint.spring = spring;
        springJoint.damper = damper;
        springJoint.enableCollision = false;

        float initialDistance = Vector3.Distance(transform.position, anchorPosition);
        float maxDistance = initialDistance * maxDistanceRatio;
        float minDistance = initialDistance * minDistanceRatio;
        springJoint.maxDistance = maxDistance;
        springJoint.minDistance = minDistance;
        springJoint.connectedBody = null;

        UpdateRopeVisual();
    }

    private void ApplyPullForce()
    {
        if (!pullHeld || springJoint == null)
        {
            return;
        }

        Vector3 toAnchor = (anchorPosition - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, anchorPosition);
        if (distance <= pullStopDistance)
        {
            return;
        }

        playerRigidbody.AddForce(toAnchor * pullAcceleration, ForceMode.Acceleration);
    }

    private void ReleaseRope()
    {
        if (springJoint != null)
        {
            Destroy(springJoint);
            springJoint = null;
        }

        state = RopeState.Ready;
        anchorPosition = Vector3.zero;
        anchorVelocity = Vector3.zero;
        travelDistance = 0f;
        ropeLine.enabled = false;

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

        if (hit.collider == playerRigidbody.GetComponent<Collider>())
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

    private void OnValidate()
    {
        minDistanceRatio = Mathf.Clamp(minDistanceRatio, 0f, 1f);
        maxDistanceRatio = Mathf.Clamp(maxDistanceRatio, 0.1f, 1f);
        if (minDistanceRatio > maxDistanceRatio)
        {
            minDistanceRatio = maxDistanceRatio;
        }

        anchorSpeed = Mathf.Clamp(anchorSpeed, 1f, 150f);
        maxGrappleDistance = Mathf.Clamp(maxGrappleDistance, 1f, 100f);
        anchorRadius = Mathf.Clamp(anchorRadius, 0.01f, 0.5f);
        spring = Mathf.Clamp(spring, 0f, 200f);
        damper = Mathf.Clamp(damper, 0f, 50f);
        pullAcceleration = Mathf.Clamp(pullAcceleration, 0f, 150f);
        pullStopDistance = Mathf.Clamp(pullStopDistance, 0.1f, 5f);
        ropeWidth = Mathf.Clamp(ropeWidth, 0.005f, 0.2f);
    }
}
