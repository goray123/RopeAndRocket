using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MomentumSystem : MonoBehaviour
{
    [Header("Momentum System")]
    [SerializeField] private float groundSpeedCap = 50f;
    [SerializeField] private float maxSpeedCap = 100f;
    [SerializeField] private float groundDrag = 100f;
    [SerializeField] private float airDrag = 10f;

    private Rigidbody body;
    private Vector3 accumulatedForce;
    private bool hasJumpRequest;
    private float jumpVelocity;
    private float currentSpeedCap;
    private float currentDrag;

    public float CurrentHorizontalSpeed => new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z).magnitude;
    public Vector3 CurrentHorizontalVelocity => new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        currentSpeedCap = groundSpeedCap;
        currentDrag = groundDrag;
    }

    private void FixedUpdate()
    {
        body.angularVelocity = Vector3.zero;
        Vector3 currentVelocity = body.linearVelocity;
        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        float verticalVelocity = currentVelocity.y;

        if (accumulatedForce.sqrMagnitude > 0f)
        {
            horizontalVelocity += accumulatedForce * Time.fixedDeltaTime;
        }
        else
        {
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, Vector3.zero, currentDrag * Time.fixedDeltaTime);
        }

        float cap = currentSpeedCap > 0f ? currentSpeedCap : maxSpeedCap;
        if (horizontalVelocity.magnitude > cap)
        {
            horizontalVelocity = horizontalVelocity.normalized * cap;
        }

        if (hasJumpRequest)
        {
            verticalVelocity = jumpVelocity;
            hasJumpRequest = false;
        }

        body.linearVelocity = new Vector3(horizontalVelocity.x, verticalVelocity, horizontalVelocity.z);
        accumulatedForce = Vector3.zero;
    }

    public void AddMovementForce(Vector3 force)
    {
        accumulatedForce += force;
    }

    public void RequestJump(float jumpVelocity)
    {
        hasJumpRequest = true;
        this.jumpVelocity = jumpVelocity;
    }

    public void SetGroundSpeedCap(float speed)
    {
        groundSpeedCap = speed;
        currentSpeedCap = Mathf.Clamp(currentSpeedCap, groundSpeedCap, maxSpeedCap);
    }

    public void SetBoostSpeedCap(float speed)
    {
        currentSpeedCap = Mathf.Clamp(speed, groundSpeedCap, maxSpeedCap);
    }

    public void ResetSpeedCap()
    {
        currentSpeedCap = groundSpeedCap;
    }

    public void SetGrounded(bool grounded)
    {
        currentDrag = grounded ? groundDrag : airDrag;
    }
}
