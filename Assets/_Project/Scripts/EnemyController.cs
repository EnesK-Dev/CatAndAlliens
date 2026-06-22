using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Yapay Zeka Ayarlari")]
    [SerializeField] private float moveSpeed = 3f; 

    [Header("Can Ayarlari")]
    [SerializeField] private float maxHealth;

    [Header("Gorsel Solma Ayarlari")]
    [SerializeField] private float minAlphaLimit;

    [Header("Lazer Saldiri Ayarlari")]
    [SerializeField] private float laserDamage = 1f;
    [SerializeField] private float chargeDuration = 1.5f; 
    [SerializeField] private float laserDuration = 0.5f;  
    [SerializeField] private float minAttackCooldown = 3f; 
    [SerializeField] private float maxAttackCooldown = 7f; 
    [SerializeField] private LayerMask obstacleLayers;   
    [SerializeField] private float eliteMoveSpeed = 1.5f;

    [Header("Varyasyon Ayarlari")]
    [Range(0f, 1f)] [SerializeField] private float laserChance = 0.4f; 

    private LineRenderer lineRenderer;
    private bool isAttacking = false;
    private float nextAttackTime;
    private bool canUseLaser = false;

    private Transform playerTransform; 
    private Rigidbody2D rb;
    private Vector2 moveDirection;
    private float currentHealth;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        InitializeComponents();
        InitializeHealthSystem();
        CalculateNextAttackTime();
        DetermineEnemyVariation();
        FindTargetPlayer();
    }

    void Update()
    {
        if (playerTransform == null) return;

        if (isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            CalculateTrackingDirection();
            HandleAttackTiming();
        }
    }

    void FixedUpdate()
    {
        MoveTowardsPlayer();
    }

    private void InitializeComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        lineRenderer = GetComponent<LineRenderer>();
    }

    private void InitializeHealthSystem()
    {
        currentHealth = maxHealth;
    }

    private void CalculateNextAttackTime()
    {
        nextAttackTime = Time.time + Random.Range(minAttackCooldown, maxAttackCooldown);
    }

    private void DetermineEnemyVariation()
    {
        if (Random.value <= laserChance)
        {
            canUseLaser = true;
            moveSpeed = eliteMoveSpeed;
            if (spriteRenderer != null)
            {
                Color eliteColor;
                if (ColorUtility.TryParseHtmlString("#0F490F", out eliteColor))
                {
                    spriteRenderer.color = eliteColor;
                }
            }
        }
        else
        {
            canUseLaser = false;
        }
    }

    private void HandleAttackTiming()
    {
        if (canUseLaser && Time.time >= nextAttackTime)
        {
            StartCoroutine(LaserAttackRoutine());
        }
    }

    private void FindTargetPlayer()
    {
        player targetPlayer = Object.FindFirstObjectByType<player>();
        if (targetPlayer != null)
        {
            playerTransform = targetPlayer.transform;
        }
    }

    private void CalculateTrackingDirection()
    {
        Vector2 direction = (playerTransform.position - transform.position);
        moveDirection = direction.normalized;
    }

    private void MoveTowardsPlayer()
    {
        if (isAttacking || playerTransform == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        rb.linearVelocity = moveDirection * moveSpeed;
    }

    public void TakeDamage(float damageAmount)
    {
        currentHealth -= damageAmount;
    
    // CANIN EKSİYE DÜŞMESİNİ ENGELLİYORUZ:
    // currentHealth eğer 0'ın altına inerse otomatik olarak 0'a eşitlenecek.
        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth); 
    
        UpdateVisualAlpha();

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void UpdateVisualAlpha()
    {
        if (spriteRenderer != null)
        {
            float healthPercentage = currentHealth / maxHealth;
            Color newColor = spriteRenderer.color;
            newColor.a = Mathf.Lerp(minAlphaLimit, 1f, healthPercentage);
            spriteRenderer.color = newColor;
        }
    }

    private System.Collections.IEnumerator LaserAttackRoutine()
    {
        isAttacking = true;
        
        if (lineRenderer != null)
        {
            lineRenderer.enabled = true;
            lineRenderer.startWidth = 0.02f; 
            lineRenderer.endWidth = 0.02f;
            lineRenderer.startColor = Color.red;
            lineRenderer.endColor = Color.red;
        }

        float chargeTimer = 0f;
        while (chargeTimer < chargeDuration)
        {
            UpdateLaserPositions();
            chargeTimer += Time.deltaTime;
            yield return null;
        }

        if (lineRenderer != null)
        {
            lineRenderer.startWidth = 0.4f; 
            lineRenderer.endWidth = 0.4f;
        }

        Vector2 fireDirection = moveDirection; 
        RaycastHit2D hit = Physics2D.Raycast(transform.position, fireDirection, Mathf.Infinity, obstacleLayers);
        
        if (hit.collider != null)
        {
            player cat = hit.collider.GetComponent<player>();
            if (cat != null)
            {
                Debug.Log("KEDİ LAZER YEDİ!");
            }
        }

        yield return new WaitForSeconds(laserDuration);

        if (lineRenderer != null) lineRenderer.enabled = false;
        
        CalculateNextAttackTime();
        isAttacking = false;
    }

    private void UpdateLaserPositions()
    {
        if (lineRenderer == null) return;

        RaycastHit2D hit = Physics2D.Raycast(transform.position, moveDirection, Mathf.Infinity, obstacleLayers);
        lineRenderer.SetPosition(0, transform.position); 

        if (hit.collider != null)
        {
            lineRenderer.SetPosition(1, hit.point); 
        }
        else
        {
            lineRenderer.SetPosition(1, transform.position + (Vector3)moveDirection * 50f);
        }
    }

    private void Die()
    {
        Debug.Log(gameObject.name + " ÖLDÜ!");
        Destroy(gameObject); 
    }
}