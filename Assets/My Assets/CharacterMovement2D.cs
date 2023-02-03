using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Animator))]
public class CharacterMovement2D : MonoBehaviour
{
    [Header("Setting")]
    [Tooltip("Amount of force added when the player jumps.")]
    public float jumpForce = 400f;

    [Tooltip("Amount of movement speed added when the player walk.")]
    public float moveSpeed = 40f;

    [Tooltip("Amount of maxSpeed applied to crouching mov545ement. 1 = 100%")]
    [Range(0, 1)]
    public float crouchSpeed = .36f;

    [Tooltip(" How much to smooth out the movement")]
    [Range(0, .3f)]
    public float movementSmoothing = .05f;

    [Tooltip("Whether or not a player can steer while jumping;")]
    public bool airControl = false;

    [Tooltip(" A mask determining what is ground to the character")]
    public LayerMask whatIsGround;

    [Tooltip(" A position marking where to check for ceilings")]
    public Transform ceilingCheck;

    [Tooltip("A position marking where to check if the player is grounded.")]
    public Transform groundCheck;

    [Tooltip("A collider that will be disabled when crouching")]
    public Collider2D crouchDisableCollider;

    [Tooltip("A joystick controller")]
    public Joystick joystick;


    [Header("Gizmos")]
    public float groundedRadius = 0.2f; // Radius of the overlap circle to determine if grounded
    private bool m_Grounded;            // Whether or not the player is grounded.
    public float ceilingRadius = 0.25f; // Radius of the overlap circle to determine if the player can stand up
    private Rigidbody2D m_Rigidbody2D;
    private bool m_FacingRight = true;  // For determining which way the player is currently facing.
    private Vector3 m_Velocity = Vector3.zero;

    private bool isJumping = false;
    private bool isCrouch = false;
    private float horizontalMove = 0f;

    private Animator animator;

    [Header("Events")]
    public UnityEvent OnLandEvent;

    [System.Serializable]
    public class BoolEvent : UnityEvent<bool> { }

    public BoolEvent OnCrouchEvent;
    private bool m_wasCrouching = false;

    private void Awake()
    {
        m_Rigidbody2D = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(ceilingCheck.position, ceilingRadius);
        Gizmos.DrawSphere(groundCheck.position, groundedRadius);
    }

    #region Input Control

    private void Update()
    {
        horizontalMove = Input.GetAxisRaw("Horizontal") * moveSpeed;

        if (joystick.Horizontal >= 0.2f)
        {
            horizontalMove = moveSpeed;
        }
        else if (joystick.Horizontal <= -0.2f)
        {
            horizontalMove = -moveSpeed;
        }


        animator.SetFloat("Speed", Mathf.Abs(horizontalMove));

        if (Input.GetButtonDown("Jump"))
        {
            isJumping = true;
            animator.SetBool("IsJumping", true);
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            isCrouch = true;

        }
        else if (Input.GetKeyUp(KeyCode.S))
        {
            isCrouch = false;
        }

    }

    public void PlayerMoveControl()
    {

        Move(horizontalMove * Time.fixedDeltaTime, isCrouch, isJumping);
        isJumping = false;
    }

    #endregion Input Control

    public void OnLanding()
    {
        animator.SetBool("IsJumping", false);
    }

    public void OnCrouching(bool isCrouching)
    {
        animator.SetBool("IsCrouching", isCrouching);
    }

    private void FixedUpdate()
    {

        bool wasGrounded = m_Grounded;
        m_Grounded = false;

        // The player is grounded if a circlecast to the groundcheck position hits anything designated as ground
        // This can be done using layers instead but Sample Assets will not overwrite your project settings.
        Collider2D[] colliders = Physics2D.OverlapCircleAll(groundCheck.position, groundedRadius, whatIsGround);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i].gameObject != gameObject)
            {
                m_Grounded = true;
                if (!wasGrounded)
                    OnLandEvent.Invoke();
            }
        }

        PlayerMoveControl();
    }

    public void Move(float moveSpeed, bool crouch, bool jump)
    {
        // If crouching, check to see if the character can stand up
        if (!crouch)
        {

            // If the character has a ceiling preventing them from standing up, keep them crouching
            if (Physics2D.OverlapCircle(ceilingCheck.position, ceilingRadius, whatIsGround))
            {
                crouch = true;
            }
        }

        //only control the player if grounded or airControl is turned on
        if (m_Grounded || airControl)
        {
            // If crouching
            if (crouch)
            {
                if (!m_wasCrouching)
                {
                    m_wasCrouching = true;
                    OnCrouchEvent.Invoke(true);
                }

                // Reduce the speed by the crouchSpeed multiplier
                moveSpeed *= crouchSpeed;

                // Disable one of the colliders when crouching
                if (crouchDisableCollider != null)
                    crouchDisableCollider.enabled = false;
            }
            else
            {
                // Enable the collider when not crouching
                if (crouchDisableCollider != null)
                    crouchDisableCollider.enabled = true;

                if (m_wasCrouching)
                {
                    m_wasCrouching = false;
                    OnCrouchEvent.Invoke(false);
                }
            }

            if (m_wasCrouching == false)
            {
                // Move the character by finding the target velocity
                Vector3 targetVelocity = new Vector2(moveSpeed * 10f, m_Rigidbody2D.velocity.y);
                // And then smoothing it out and applying it to the character
                m_Rigidbody2D.velocity = Vector3.SmoothDamp(m_Rigidbody2D.velocity, targetVelocity, ref m_Velocity, movementSmoothing);
            }


            // If the input is moving the player right and the player is facing left...
            if (moveSpeed > 0 && !m_FacingRight)
            {
                // ... flip the player.
                Flip();
            }
            // Otherwise if the input is moving the player left and the player is facing right...
            else if (moveSpeed < 0 && m_FacingRight)
            {
                // ... flip the player.
                Flip();
            }
        }
        // If the player should jump...
        if (m_Grounded && jump)
        {
            // Add a vertical force to the player.
            m_Grounded = false;
            m_Rigidbody2D.AddForce(new Vector2(0f, jumpForce));
        }
    }

    private void Flip()
    {
        // Switch the way the player is labelled as facing.
        m_FacingRight = !m_FacingRight;

        //OLD
        //// Multiply the player's x local scale by -1.
        //Vector3 theScale = transform.localScale;
        //theScale.x *= -1;
        //transform.localScale = theScale;

        //New
        transform.Rotate(0f, 180, 0f);
    }
}

