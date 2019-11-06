using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlayerControl : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Sharpness for the movement when grounded, a low value will make the player accelerate and decelerate slowly, a high value will do the opposite")]
    public float movementSharpnessOnGround = 15;

    [Header("General")]
    [Tooltip("Force applied downward when in the air")]
    public float gravityDownForce = 20f;
    [Tooltip("Physic layers checked to consider the player grounded")]
    public LayerMask groundCheckLayers = -1;
    [Tooltip("distance from the bottom of the character controller capsule to test for grounded")]
    public float groundCheckDistance = 0.05f;
    public float speed = 10.0f;
    public float jumpSpeed = 8.0f;

    [Header("Stance")]
    [Tooltip("Ratio (0-1) of the character height where the camera will be at")]
    public float cameraHeightRatio = 0.9f;
    [Tooltip("Height of character when standing")]
    public float capsuleHeightStanding = 1.8f;
    [Tooltip("Height of character when crouching")]
    public float capsuleHeightCrouching = 0.9f;
    [Tooltip("Speed of crouching transitions")]
    public float crouchingSharpness = 10f;

    public UnityAction<bool> onStanceChanged;

    CharacterController m_Controller;
    public Camera playerCamera;
    Vector3 characterMove = Vector3.zero;
    Vector3 m_GroundNormal;
    float m_TargetCharacterHeight;
    float m_CameraVerticalAngle = 0f;
    float maxSpeedCrouchedRatio = 0.5f;
    private float vertical;
    private float horizontal;

    public bool isCrouching { get; private set; }
    public bool isGrounded { get; private set; }

    // Use this for initialization
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        m_Controller = GetComponent<CharacterController>();
        m_Controller.enableOverlapRecovery = true;

        SetCrouchingState(false, true);
        UpdateCharacterHeight(true);
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown("escape"))
        {
            // turn on the cursor
            Cursor.lockState = CursorLockMode.None;
        }

        bool wasGrounded = isGrounded;
        GroundCheck();

        if (Input.GetButtonDown("Crouch"))
        {
            SetCrouchingState(!isCrouching, false);
        }

        UpdateCharacterHeight(false);
        HandleMovement();
    }

    public void HandleMovement()
    {
        // horizontal character rotation
        {
            if (CanProcessInput())
            {
                // rotate the transform with the input speed around its local Y axis
                transform.Rotate(new Vector3(0f, (Input.GetAxisRaw("Mouse X")), 0f), Space.Self);
            }
        }
        // vertical camera rotation
        {
            if (CanProcessInput())
            {
                // add vertical inputs to the camera's vertical angle
                m_CameraVerticalAngle += Input.GetAxisRaw("Mouse Y") * -1f;

                // limit the camera's vertical angle to min/max
                m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -89f, 89f);

                // apply the vertical angle as a local rotation to the camera transform along its right axis (makes it pivot up and down)
                playerCamera.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
            }
        }

        vertical = Input.GetAxis("Vertical");
        horizontal = Input.GetAxis("Horizontal");
        Vector3 moveInput = new Vector3(horizontal, 0f, vertical);
        Vector3 worldVelocity = transform.TransformVector(moveInput);

        if (isGrounded)
        {
            Vector3 velocity = worldVelocity * speed;
            if (isCrouching)
            {
                velocity = worldVelocity * maxSpeedCrouchedRatio;
            }
            characterMove = velocity;
        }
        else
        {
            // add air acceleration
            // velocity += worldVelocity * accelerationSpeedInAir * Time.deltaTime;

            // limit air speed to a maximum, but only horizontally
            float verticalVelocity = characterMove.y;
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(characterMove, Vector3.up);
            horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, 1f);
            characterMove = horizontalVelocity + (Vector3.up * verticalVelocity);

            // apply the gravity to the velocity
            characterMove += Vector3.down * gravityDownForce * Time.deltaTime;
        }

        //move.y -= gravityDownForce * Time.deltaTime;

        m_Controller.Move(characterMove * Time.deltaTime);
    }

    public bool CanProcessInput()
    {
        return Cursor.lockState == CursorLockMode.Locked;
    }

    bool SetCrouchingState(bool crouched, bool ignoreObstructions)
    {
        if (crouched)
        {
            m_TargetCharacterHeight = capsuleHeightCrouching;
        }
        else
        {
            if (!ignoreObstructions)
            {
                Collider[] standingOverlaps = Physics.OverlapCapsule(
                    GetCapsuleBottomHemisphere(),
                    GetCapsuleTopHemisphere(capsuleHeightStanding),
                    m_Controller.radius,
                    -1,
                QueryTriggerInteraction.Ignore);
                foreach (Collider c in standingOverlaps)
                {
                    if (c != m_Controller)
                    {
                        return false;
                    }
                }
            }

            m_TargetCharacterHeight = capsuleHeightStanding;
        }

        if (onStanceChanged != null)
        {
            onStanceChanged.Invoke(crouched);
        }

        isCrouching = crouched;
        return true;
    }

    void GroundCheck()
    {
        // Make sure that the ground check distance while already in air is very small, to prevent suddenly snapping to ground
        float chosenGroundCheckDistance = isGrounded ? (m_Controller.skinWidth + groundCheckDistance) : 0.07f;

        // reset values before the ground check
        isGrounded = false;
        m_GroundNormal = Vector3.up;

        // only try to detect ground if it's been a short amount of time since last jump; otherwise we may snap to the ground instantly after we try jumping
        if (Time.time >= 0f + 0.02f)
        {
            // if we're grounded, collect info about the ground normal with a downward capsule cast representing our character capsule
            if (Physics.CapsuleCast(GetCapsuleBottomHemisphere(), GetCapsuleTopHemisphere(m_Controller.height), m_Controller.radius, Vector3.down, out RaycastHit hit, chosenGroundCheckDistance, groundCheckLayers, QueryTriggerInteraction.Ignore))
            {
                // storing the upward direction for the surface found
                m_GroundNormal = hit.normal;

                // Only consider this a valid ground hit if the ground normal goes in the same direction as the character up
                // and if the slope angle is lower than the character controller's limit
                if (Vector3.Dot(hit.normal, transform.up) > 0f &&
                    IsNormalUnderSlopeLimit(m_GroundNormal))
                {
                    isGrounded = true;

                    // handle snapping to the ground
                    if (hit.distance > m_Controller.skinWidth)
                    {
                        m_Controller.Move(Vector3.down * hit.distance);
                    }
                }
            }
        }
    }

    void UpdateCharacterHeight(bool force)
    {
        // Update height instantly
        if (force)
        {
            m_Controller.height = m_TargetCharacterHeight;
            m_Controller.center = Vector3.up * m_Controller.height * 0.5f;
            playerCamera.transform.localPosition = Vector3.up * m_TargetCharacterHeight * cameraHeightRatio;
            //m_Actor.aimPoint.transform.localPosition = m_Controller.center;
        }
        // Update smooth height
        else if (m_Controller.height != m_TargetCharacterHeight)
        {
            // resize the capsule and adjust camera position
            m_Controller.height = Mathf.Lerp(m_Controller.height, m_TargetCharacterHeight, crouchingSharpness * Time.deltaTime);
            m_Controller.center = Vector3.up * m_Controller.height * 0.5f;
            playerCamera.transform.localPosition = Vector3.Lerp(playerCamera.transform.localPosition, Vector3.up * m_TargetCharacterHeight * cameraHeightRatio, crouchingSharpness * Time.deltaTime);
            //m_Actor.aimPoint.transform.localPosition = m_Controller.center;
        }
    }

    Vector3 GetCapsuleBottomHemisphere()
    {
        return transform.position + (transform.up * m_Controller.radius);
    }

    // Gets the center point of the top hemisphere of the character controller capsule    
    Vector3 GetCapsuleTopHemisphere(float atHeight)
    {
        return transform.position + (transform.up * (atHeight - m_Controller.radius));
    }
    bool IsNormalUnderSlopeLimit(Vector3 normal)
    {
        return Vector3.Angle(transform.up, normal) <= m_Controller.slopeLimit;
    }
}
