using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerWalking : MonoBehaviour
{
    [Header("Walking")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float gravity   = -20f;

    [Header("Sprint")]
    [SerializeField] private float sprintSpeed      = 9f;
    [SerializeField] private float sprintAcceleration = 6f; // скорость разгона до бега

    [Header("Jump")]
    [SerializeField] private float jumpHeight   = 1.5f;
    [SerializeField] private float jumpCooldown = 0.2f;

    [Header("Slope")]
    [SerializeField] private float slopeSlideFriction = 0.3f;
    [SerializeField] private float groundRayLength    = 0.5f;
    [SerializeField] private float slopeBlockAngle    = 75f;

    private CharacterController cc;
    private StaminaSystem       staminaSystem;

    private Vector3 velocity;
    private float   lastJumpTime = -10f;
    private float   currentSpeed;

    public bool  IsGrounded => cc.isGrounded;
    public float Gravity    => gravity;

    private void Awake()
    {
        cc            = GetComponent<CharacterController>();
        staminaSystem = GetComponent<StaminaSystem>();
        currentSpeed  = walkSpeed;
    }

    // ── Вызывается из ClimbingManager ────────────────────────

    public void Tick()
    {
        bool onSlope = GetSlopeHit(out RaycastHit hit, out float angle);

        HandleSprint();
        HandleMovement(angle);
        if (onSlope) HandleSlope(hit, angle);

        cc.Move(velocity * Time.deltaTime);
    }

    public void ApplyGravityOnly()
    {
        velocity.x = 0f;
        velocity.z = 0f;
        velocity.y = IsGrounded ? -2f : velocity.y + gravity * Time.deltaTime;
        cc.Move(velocity * Time.deltaTime);
    }

    public void ResetVelocity() => velocity = Vector3.zero;

    public void TryJump()
    {
        if (!IsGrounded) return;
        if (Time.time - lastJumpTime < jumpCooldown) return;

        velocity.y   = Mathf.Sqrt(2f * Mathf.Abs(gravity) * jumpHeight);
        lastJumpTime = Time.time;
    }

    // ── Приватные ─────────────────────────────────────────────

    private void HandleSprint()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        bool isMoving    = new Vector2(velocity.x, velocity.z).magnitude > 0.1f;
        bool wantsSprint = kb.leftShiftKey.isPressed
                        && IsGrounded
                        && isMoving
                        && staminaSystem != null
                        && !staminaSystem.IsExhausted;

        if (wantsSprint)
        {
            // Плавный разгон до бега
            currentSpeed = Mathf.MoveTowards(currentSpeed, sprintSpeed, sprintAcceleration * Time.deltaTime);
        }
        else
        {
            // Резкое торможение до ходьбы
            currentSpeed = walkSpeed;
        }

        staminaSystem?.SetSprinting(wantsSprint);
    }

    private void HandleMovement(float slopeAngle)
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.spaceKey.wasPressedThisFrame) TryJump();

        // Полная блокировка на очень крутом склоне
        if (slopeAngle > slopeBlockAngle)
        {
            velocity.x  = 0f;
            velocity.z  = 0f;
            velocity.y += gravity * Time.deltaTime;
            return;
        }

        float h = 0f, v = 0f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h = -1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h =  1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v =  1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v = -1f;

        Vector3 moveDir = transform.forward * v + transform.right * h;
        moveDir.y = 0f;

        if (moveDir.magnitude > 0.1f)
        {
            velocity.x = moveDir.normalized.x * currentSpeed;
            velocity.z = moveDir.normalized.z * currentSpeed;
        }
        else
        {
            velocity.x = 0f;
            velocity.z = 0f;
        }

        if (IsGrounded && velocity.y < 0f)
            velocity.y = -2f;
        else if (!IsGrounded)
            velocity.y += gravity * Time.deltaTime;
    }

    private void HandleSlope(RaycastHit hit, float angle)
    {
        float slideStrength = (angle - cc.slopeLimit) / (90f - cc.slopeLimit);
        float slideSpeed    = slopeSlideFriction * slideStrength;

        velocity.x += hit.normal.x * -gravity * slideSpeed * Time.deltaTime;
        velocity.z += hit.normal.z * -gravity * slideSpeed * Time.deltaTime;

        Debug.DrawRay(hit.point, hit.normal * 2f, Color.blue);
    }

    private bool GetSlopeHit(out RaycastHit hit, out float angle)
    {
        bool onGround = Physics.SphereCast(
            transform.position, cc.radius,
            Vector3.down, out hit,
            cc.height / 2f + groundRayLength
        );

        angle = onGround ? Vector3.Angle(hit.normal, Vector3.up) : 0f;
        return onGround && angle > cc.slopeLimit;
    }

    // ── Gizmos ────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (cc == null) return;

        float rayLength = cc.height / 2f + groundRayLength;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.down * rayLength, cc.radius);
        Gizmos.DrawRay(transform.position, Vector3.down * rayLength);

        RaycastHit hit;
        if (!Physics.SphereCast(transform.position, cc.radius, Vector3.down, out hit, rayLength)) return;

        float angle = Vector3.Angle(hit.normal, Vector3.up);

        if (angle > slopeBlockAngle)    Gizmos.color = Color.red;
        else if (angle > cc.slopeLimit) Gizmos.color = Color.yellow;
        else                            Gizmos.color = Color.green;

        Gizmos.DrawWireSphere(hit.point, 0.1f);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(hit.point, hit.normal * 2f);

#if UNITY_EDITOR
        Handles.Label(hit.point + Vector3.up * 0.3f, $"{angle:F0}°");
#endif
    }
}