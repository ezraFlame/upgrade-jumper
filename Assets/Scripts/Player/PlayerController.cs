using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour
{
    private Rigidbody2D rb;
    private Animator anim;
    public PlayerInput input
    {
        get; private set;
    }

    public Health health;

    public int gold
    {
        get; private set;
    } = 0;

    [Header("Shop")]
    public Shop currentShop;
    [SerializeField] private GameObject interactText;

    [Header("Movement")]
    [SerializeField] private float acceleration;
    [SerializeField] private float maxMoveSpeed;
    [SerializeField] private float groundLinearDrag;
    [SerializeField] private float airLinearDrag;
    public float moveX
    {
        get; private set;
    }
    public float moveY
    {
        get; private set;
    }
    private bool changeDir => (rb.velocity.x > 0f && moveX < 0f) || (rb.velocity.x < 0f && moveX > 0f);
    private bool canMove => !wallGrab && !interacting && !PauseManager.paused;
    private bool facingRight = true;
    private bool interacting = false;
    private bool canInteract = false;


    [Header("Jumping")]
    [SerializeField] private float jumpForce;
    [SerializeField] private float fallMultiplier;
    [SerializeField] private float lowJumpMultiplier;
    public int extraJumps
    {
        get; private set;
    }
    [SerializeField] private float hangTime;
    [SerializeField] private float jumpBufferTime;
    [SerializeField] private float jumpThreshold;
    private bool canJump => jumpBufferCounter > 0f && (hangTimeCounter > 0f || extraJumpsValue > 0f || (onWall && wallJumpUnlocked)) && canMove;
    public bool isJumping
    {
        get; private set;
    }

    public int extraJumpsValue
    {
        get; private set;
    }
    private float hangTimeCounter;
    private float jumpBufferCounter;

    [Header("Wall Movement")]
    [SerializeField] private float wallSlideModifier;
    [SerializeField] private float wallJumpXVelocityHaltDelay;
    public float maxStamina
    {
        get; private set;
    }
    public float staminaValue
    {
        get; private set;
    }
    private bool wallGrab => onWall && !onGround && input.actions["WallGrab"].IsPressed() && wallGrabUnlocked && staminaValue > 0;
    private bool wallSlide => onWall && !onGround && !input.actions["WallGrab"].IsPressed() && rb.velocity.y < 0f;


    [Header("Dashing")]
    [SerializeField] private float dashSpeed;
    [SerializeField] private float dashLength;
    [SerializeField] private float dashBufferTime;
    private float dashBufferCounter;
    private bool isDashing = false;
    public bool hasDashed
    {
        get; private set;
    }
    private bool canDash => dashBufferCounter > 0f && !hasDashed && dashUnlocked && canMove;

    [Header("Ground Collision")]
    [SerializeField] private float checkDistance;
    [SerializeField] private float groundCheckWidth;
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Transform feet;
    private bool onGround;

    [Header("Wall Collision")]
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private float wallCheckDistance;
    public bool onWall;
    public bool onRightWall;

    [Header("Particles")]
    [SerializeField] private GameObject jumpParticle;

    //Upgrades
    [Header("Upgrades")]
    [SerializeField] private bool wallGrabUnlocked;
    [SerializeField] private bool wallJumpUnlocked;
    [SerializeField] private bool dashUnlocked;

    private void Start()
    {
        GetComponentRefs();
    }

    private void GetComponentRefs()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        input = GetComponent<PlayerInput>();
        health = GetComponent<Health>();
    }

    private void Update()
    {
        if (input.actions["Pause"].WasPressedThisFrame())
        {
            PauseManager.Pause();
        }

        Vector2 moveInput = input.actions["Move"].ReadValue<Vector2>();
        if (moveInput.x > 0.5) moveX = 1;
        else if (moveInput.x < -0.5) moveX = -1;
        else moveX = 0;

        if (moveInput.y > 0.5) moveY = 1;
        else if (moveInput.y < -0.5) moveY = -1;
        else moveY = 0;

        if (moveY == 1 && canInteract && !interacting && currentShop != null)
        {
            interacting = true;
            currentShop.Show();
        }

        if (input.actions["Cancel"].WasPressedThisFrame())
        {
            if (interacting)
            {
                interacting = false;
                currentShop.Hide();
            }
            if (PauseManager.paused)
            {
                PauseManager.Pause();
            }
        }

        if (canInteract && !interacting)
        {
            interactText.SetActive(true);
        }
        else
        {
            interactText.SetActive(false);
        }

        if (input.actions["Jump"].WasPressedThisFrame()) jumpBufferCounter = jumpBufferTime;
        else jumpBufferCounter -= Time.deltaTime;

        if (input.actions["Dash"].WasPressedThisFrame()) dashBufferCounter = dashBufferTime;
        else dashBufferCounter -= Time.deltaTime;

        UpdateAnimations();
    }

    private void UpdateAnimations()
    {
        if (isDashing)
        {

        } else if (canMove)
        {
            if ((moveX < 0f && facingRight || moveX > 0f && !facingRight) && !wallGrab && !wallSlide)
            {
                Flip();
            }
            if (onGround)
            {
                anim.SetBool("IsGrounded", true);
                anim.SetBool("OnWall", false);
                anim.SetFloat("MoveX", Mathf.Abs(moveX));
            } else
            {
                anim.SetBool("IsGrounded", false);
            }

            if (isJumping)
            {
                anim.SetBool("IsJumping", true);
                anim.SetBool("OnWall", false);
                anim.SetFloat("MoveY", 0f);
            } else
            {
                anim.SetBool("IsJumping", false);

                if (onWall)
                {
                    anim.SetBool("OnWall", true);
                    anim.SetFloat("MoveY", 0f);
                }
            }
        }
    }

    public void Flip()
    {
        facingRight = !facingRight;
        transform.Rotate(0f, 180f, 0f);
    }

    private void FixedUpdate()
    {
        CheckCollisions();
        if (canDash) StartCoroutine(Dash(moveX, moveY));
        if (!isDashing)
        {
            if (canMove) Move();
            /*            else rb.velocity = Vector2.Lerp(rb.velocity, (new Vector2(moveX * maxMoveSpeed, rb.velocity.y)), .5f * Time.fixedDeltaTime);*/
            else rb.velocity = Vector3.zero;
            if (onGround)
            {
                ApplyGroundLinearDrag();
                RefreshDash();
                RefreshJumps();
                RefreshStamina();
            }
            else
            {
                ApplyAirLinearDrag();
                FallMultiplier();
                hangTimeCounter -= Time.fixedDeltaTime;
                if (onWall || rb.velocity.y < 0f) isJumping = false;
            }
            if (canJump)
            {
                if (onWall && wallJumpUnlocked && !onGround)
                {
                    if (onRightWall && moveX > 0f || !onRightWall && moveX < 0f)
                    {
                        StartCoroutine(NeutralWallJump());
                    }
                    else
                    {
                        WallJump();
                    }
                }
                else
                {
                    Jump(Vector2.up);
                }
            }

            if (!isJumping)
            {
                if (wallSlide) WallSlide();
                if (wallGrab) WallGrab();
                if (onWall) StickToWall();
            }
        }
    }

    public void RefreshJumps()
    {
        extraJumpsValue = extraJumps;
        hangTimeCounter = hangTime;
    }

    public void RefreshDash()
    {
        hasDashed = false;
    }

    public void RefreshStamina()
    {
        staminaValue = maxStamina;
    }

    private void Jump(Vector2 direction)
    {
        if (!onGround && !(onWall && wallJumpUnlocked) && !(hangTimeCounter > 0f))
            extraJumpsValue--;

        ApplyAirLinearDrag();
        rb.velocity = new Vector2(rb.velocity.x, 0f);
        rb.AddForce(direction * jumpForce, ForceMode2D.Impulse);
        hangTimeCounter = 0;
        jumpBufferCounter = 0f;
        isJumping = true;

        Instantiate(jumpParticle, transform.position, transform.rotation);
    }

    private void WallJump()
    {
        Vector2 jumpDirection = onRightWall ? Vector2.left : Vector2.right;
        Jump(Vector2.up + jumpDirection);
    }

    IEnumerator NeutralWallJump()
    {
        Vector2 jumpDirection = onRightWall ? Vector2.left : Vector2.right;
        Jump(Vector2.up + jumpDirection);
        yield return new WaitForSeconds(wallJumpXVelocityHaltDelay);
        rb.velocity = new Vector2(0f, rb.velocity.y);
    }

    private void Move()
    {
        rb.AddForce(new Vector2(moveX * acceleration, 0));
        if (Mathf.Abs(rb.velocity.x) > maxMoveSpeed)
        {
            rb.velocity = new Vector2(Mathf.Sign(rb.velocity.x) * maxMoveSpeed, rb.velocity.y);
        }
    }

    private void ApplyGroundLinearDrag()
    {
        if (Mathf.Abs(moveX) < 0.4f || changeDir)
        {
            rb.drag = groundLinearDrag;
        } else
        {
            rb.drag = 0f;
        }
    }

    private void ApplyAirLinearDrag()
    {
        rb.drag = airLinearDrag;
    }

    private void CheckCollisions()
    {
        onGround = Physics2D.BoxCast(feet.position, new Vector2(groundCheckWidth, checkDistance), 0, Vector2.down, 0f, groundLayer) && rb.velocity.y <= 0.1;

        onWall = Physics2D.Raycast(transform.position, Vector2.right, wallCheckDistance, wallLayer) ||
                 Physics2D.Raycast(transform.position, Vector2.left, wallCheckDistance, wallLayer);
        onRightWall = Physics2D.Raycast(transform.position, Vector2.right, wallCheckDistance, wallLayer);
    }

    private void FallMultiplier()
    {
        if (rb.velocity.y <= jumpThreshold)
        {
            rb.gravityScale = fallMultiplier;
        } else if (rb.velocity.y > 0 && !input.actions["Jump"].IsPressed())
        {
            rb.gravityScale = lowJumpMultiplier;
        } else
        {
            rb.gravityScale = 1f;
        }
    }

    private void WallGrab()
    {
        rb.gravityScale = 0f;
        rb.velocity = new Vector2(rb.velocity.x, 0f);
        staminaValue -= Time.deltaTime;
    }

    private void StickToWall()
    {
        if (onRightWall && moveX >= 0f)
        {
            rb.velocity = new Vector2(maxMoveSpeed, rb.velocity.y);
        } else if (!onRightWall && moveX <= 0f)
        {
            rb.velocity = new Vector2(-maxMoveSpeed, rb.velocity.y);
        }
    }

    public void WallSlide()
    {
        rb.velocity = new Vector2(rb.velocity.x, -maxMoveSpeed * wallSlideModifier);
        if (wallGrabUnlocked)
        {
            RefreshDash();
            RefreshJumps();
        }
    }

    IEnumerator Dash(float x, float y)
    {
        float dashStartTime = Time.time;
        hasDashed = true;
        isDashing = true;

        isJumping = false;
        rb.velocity = Vector2.zero;
        rb.gravityScale = 0f;
        rb.drag = 0f;
        Vector2 dir;
        if (x != 0f || y != 0f) dir = new Vector2(x, y);
        else
        {
            dir = facingRight ? Vector2.right : Vector2.left;
        }

        Instantiate(jumpParticle, transform.position, transform.rotation);

        while (Time.time < dashStartTime + dashLength)
        {
            rb.velocity = dir.normalized * dashSpeed;
            yield return null;
        }
        isDashing = false;
    }

    public void AddGold(int amount)
    {
        if (amount > 0)
        {
            gold += amount;
        } else
        {
            Debug.Log("You can't add negative gold!");
        }
    }

    public void BuyItem(ShopItem.ItemType type)
    {
        int cost = ShopItem.GetCost(type);
        Debug.Log(gold);
        if (gold >= cost)
        {
            gold -= cost;
        } else
        {
            return;
        }
        switch (type)
        {
            default:
            case ShopItem.ItemType.Jump:
                extraJumps++;
                break;
            case ShopItem.ItemType.WallGrab:
                wallGrabUnlocked = true;
                break;
        }
    }

    public void SetCanInteract(bool value)
    {
        canInteract = value;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector2(feet.position.x, Mathf.Lerp(feet.position.y, feet.position.y - checkDistance, .5f)), new Vector3(groundCheckWidth, checkDistance, 0));

        Gizmos.DrawLine(transform.position, transform.position + Vector3.right * wallCheckDistance);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.left * wallCheckDistance);
    }
}
