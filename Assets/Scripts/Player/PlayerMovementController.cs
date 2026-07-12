using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerMovementController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private PlayerInput playerInput;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 12f;
    [SerializeField] private float groundAcceleration = 90f;
    [SerializeField] private float groundDeceleration = 120f;
    [SerializeField] private float airAcceleration = 35f;
    [SerializeField] private float jumpVelocity = 10f;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private float gravityScaleNormal = 1f;
    [SerializeField] private float gravityScaleFastFall = 2.8f;

    private Vector2 moveInput;
    private bool jumpRequested;
    private bool grounded;
    private InputAction moveAction;
    private InputAction jumpAction;

    public bool Grounded => grounded;

    private void Awake()
    {
        moveAction = playerInput.actions.FindAction("Move");
        jumpAction = playerInput.actions.FindAction("Jump");
    }

    private void OnEnable()
    {
        if (moveAction != null)
        {
            moveAction.performed += HandleMove;
            moveAction.canceled += HandleMove;
            moveAction.Enable();
        }

        if (jumpAction != null)
        {
            jumpAction.started += HandleJumpStarted;
            jumpAction.Enable();
        }

        ResetInputState();
    }

    private void OnDisable()
    {
        if (moveAction != null)
        {
            moveAction.performed -= HandleMove;
            moveAction.canceled -= HandleMove;
            moveAction.Disable();
        }

        if (jumpAction != null)
        {
            jumpAction.started -= HandleJumpStarted;
            jumpAction.Disable();
        }

        ResetInputState();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void FixedUpdate()
    {
        if (!IsReady()) return;

        grounded = CheckGrounded();
        ApplyGravity();
        ProcessMovement();
        ProcessJump();
    }

    private void ApplyGravity()
    {
        bool fastFallPressed = Keyboard.current != null && Keyboard.current.leftCtrlKey.isPressed;
        float gravityMultiplier = fastFallPressed ? gravityScaleFastFall : gravityScaleNormal;
        Vector3 velocity = rb.linearVelocity;
        velocity.y += -9.81f * gravityMultiplier * Time.fixedDeltaTime;
        rb.linearVelocity = velocity;
    }

    private void ProcessMovement()
    {
        Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        Vector3 moveDirection = GetMoveDirection();

        if (moveDirection.sqrMagnitude > 0.0001f)
        {
            Vector3 targetVelocity = moveDirection * moveSpeed;
            Vector3 velocityDelta = targetVelocity - horizontalVelocity;
            float acceleration = grounded ? groundAcceleration : airAcceleration;

            if (velocityDelta.sqrMagnitude > 0.0001f)
            {
                Vector3 accelerationVector = velocityDelta.normalized * (acceleration * Time.fixedDeltaTime);
                if (accelerationVector.magnitude >= velocityDelta.magnitude)
                {
                    horizontalVelocity = targetVelocity;
                }
                else
                {
                    horizontalVelocity += accelerationVector;
                }
            }
        }
        else if (grounded)
        {
            if (horizontalVelocity.sqrMagnitude > 0.0001f)
            {
                Vector3 decelerationVector = horizontalVelocity.normalized * (groundDeceleration * Time.fixedDeltaTime);
                if (decelerationVector.magnitude >= horizontalVelocity.magnitude)
                {
                    horizontalVelocity = Vector3.zero;
                }
                else
                {
                    horizontalVelocity -= decelerationVector;
                }
            }
        }

        Vector3 nextVelocity = rb.linearVelocity;
        nextVelocity.x = horizontalVelocity.x;
        nextVelocity.z = horizontalVelocity.z;
        rb.linearVelocity = nextVelocity;
    }

    private void ProcessJump()
    {
        if (!jumpRequested || !grounded || rb == null) return;

        jumpRequested = false;
        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        rb.linearVelocity = velocity;
        rb.AddForce(Vector3.up * jumpVelocity, ForceMode.VelocityChange);
    }

    private void HandleMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        moveInput = Vector2.ClampMagnitude(input, 1f);
    }

    private void HandleJumpStarted(InputAction.CallbackContext context)
    {
        if (context.started)
        {
            jumpRequested = true;
        }
    }

    private void ResetInputState()
    {
        moveInput = Vector2.zero;
        jumpRequested = false;
    }

    private bool IsReady()
    {
        return rb != null && groundCheck != null && playerInput != null && playerInput.enabled;
    }

    private Vector3 GetMoveDirection()
    {
        if (moveInput.sqrMagnitude <= 0.0001f) return Vector3.zero;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        return (forward * moveInput.y + right * moveInput.x).normalized;
    }

    private bool CheckGrounded()
    {
        if (groundCheck == null) return false;

        Collider[] colliders = Physics.OverlapSphere(groundCheck.position, groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);
        foreach (Collider collider in colliders)
        {
            if (collider == null || IsOwnCollider(collider)) continue;

            return true; 
        }

        return false;
    }

    private bool IsOwnCollider(Collider collider)
    {
        Collider[] ownColliders = GetComponentsInChildren<Collider>();
        foreach (Collider ownCollider in ownColliders)
        {
            if (ownCollider == collider) return true;
        }

        return false;
    }
}
