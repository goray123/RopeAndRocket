using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraPivot;
    private MomentumSystem momentumSystem;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 100f;
    [SerializeField] private float lowSpeedAcceleration = 2f;
    [SerializeField] private float highSpeedAcceleration = 5f;
    [SerializeField] private float accelerationThreshold = 50f;
    [SerializeField] private float jumpHeight = 20f;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private float groundCheckDistance = 0.6f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.5f;
    [SerializeField] private float maxLookAngle = 90f;

    private Vector2 moveInput;
    private Vector2 lookInput;
    private float verticalLookAngle;
    private bool jumpInput;
    [SerializeField] private bool isGrounded;

    private void Awake()
    {
        if (cameraPivot == null)
        {
            Debug.LogError("Camera Pivot is required on PlayerMovement.");
            enabled = false;
            return;
        }

        if (groundMask == 0)
        {
            groundMask = ~0;
        }

        momentumSystem = GetComponent<MomentumSystem>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        ApplyLook();
    }

    private void FixedUpdate()
    {
        UpdateGroundedState();
        momentumSystem.SetGrounded(isGrounded);

        if (jumpInput)
        {
            TryJump();
            jumpInput = false;
        }

        ApplyMovement();
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnLook(InputValue value)
    {
        lookInput = value.Get<Vector2>();
    }

    public void OnJump()
    {   
        Debug.Log("Jump input received");
        jumpInput = true;
    }

    private void ApplyLook()
    {
        Vector3 yawRotation = new Vector3(0f, lookInput.x * mouseSensitivity, 0f);
        transform.Rotate(yawRotation);

        verticalLookAngle -= lookInput.y * mouseSensitivity;
        verticalLookAngle = Mathf.Clamp(verticalLookAngle, -maxLookAngle, maxLookAngle);
        cameraPivot.localRotation = Quaternion.Euler(verticalLookAngle, 0f, 0f);
    }

    private void ApplyMovement()
    {
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 desiredDirection = forward * moveInput.y + right * moveInput.x;
        float inputMagnitude = Mathf.Clamp01(moveInput.magnitude);

        if (inputMagnitude < 0.01f)
        {
            return;
        }

        Vector3 desiredVelocity = desiredDirection.normalized * moveSpeed * inputMagnitude;
        float currentSpeed = momentumSystem.CurrentHorizontalSpeed;
        float acceleration = currentSpeed < accelerationThreshold ? lowSpeedAcceleration : highSpeedAcceleration;
        Vector3 currentVelocity = momentumSystem.CurrentHorizontalVelocity;
        Vector3 force = (desiredVelocity - currentVelocity) * acceleration;
        momentumSystem.AddMovementForce(force);
    }

    private void TryJump()
    {
        if (isGrounded)
        {
            float gravity = Physics.gravity.y;
            float jumpVelocity = Mathf.Sqrt(-2f * gravity * jumpHeight);
            momentumSystem.RequestJump(jumpVelocity);
        }
    }

    private void UpdateGroundedState()
    {
        Vector3 origin = transform.position;
        isGrounded = Physics.Raycast(origin, Vector3.down, transform.localScale.y * 0.5f + groundCheckDistance, groundMask);
        Debug.DrawRay(origin, Vector3.down * (transform.localScale.y * 0.5f + groundCheckDistance), isGrounded ? Color.green : Color.red);
    }
}
