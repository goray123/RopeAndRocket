using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private PlayerInput playerInput;

    [Header("Camera")]
    [SerializeField, Range(0f, 2f)] private float sensitivityX = 0.1f;
    [SerializeField, Range(0f, 2f)] private float sensitivityY = 0.1f;
    [SerializeField, Range(-90f, 0f)] private float pitchMin = -85f;
    [SerializeField, Range(0f, 90f)] private float pitchMax = 85f;

    private Vector2 lookInput;
    private float yaw;
    private float pitch;
    private InputAction lookAction;

    private void Awake()
    {
        if (playerInput == null)
        {
            Debug.LogError($"{nameof(PlayerCameraController)} requires a PlayerInput component on {name}.", this);
        }
        
        lookAction = playerInput.actions.FindAction("Look");

        if (cameraPivot == null)
        {
            Debug.LogError($"{nameof(PlayerCameraController)} requires a Camera Pivot transform on {name}.", this);
        }

        yaw = transform.eulerAngles.y;
        if (cameraPivot != null)
        {
            pitch = cameraPivot.localEulerAngles.x;
            if (pitch > 180f)
            {
                pitch -= 360f;
            }
        }
    }

    private void OnEnable()
    {
        if (lookAction != null)
        {
            lookAction.performed += HandleLook;
            lookAction.canceled += HandleLook;
            lookAction.Enable();
        }

        ResetInputState();
    }

    private void OnDisable()
    {
        if (lookAction != null)
        {
            lookAction.performed -= HandleLook;
            lookAction.canceled -= HandleLook;
            lookAction.Disable();
        }

        ResetInputState();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (!IsReady())
        {
            return;
        }

        ApplyLook();
    }

    private void ApplyLook()
    {
        yaw += lookInput.x * sensitivityX;
        pitch = Mathf.Clamp(pitch - lookInput.y * sensitivityY, pitchMin, pitchMax);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraPivot != null)
        {
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }
    }

    private void HandleLook(InputAction.CallbackContext context)
    {
        lookInput = context.ReadValue<Vector2>();
    }

    private void ResetInputState()
    {
        lookInput = Vector2.zero;
    }

    private bool IsReady()
    {
        return playerInput != null && playerInput.enabled && cameraPivot != null;
    }

    private void OnValidate()
    {
        sensitivityX = Mathf.Clamp(sensitivityX, 0f, 2f);
        sensitivityY = Mathf.Clamp(sensitivityY, 0f, 2f);
        pitchMin = Mathf.Clamp(pitchMin, -90f, 0f);
        pitchMax = Mathf.Clamp(pitchMax, 0f, 90f);

        if (pitchMin > pitchMax)
        {
            float temp = pitchMin;
            pitchMin = pitchMax;
            pitchMax = temp;
        }
    }
}
