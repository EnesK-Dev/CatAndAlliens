using System.Collections;
using UnityEngine;

public class player : MonoBehaviour
{
    [Header("Hareket Ayarlari")]
    [SerializeField] private float moveSpeed; 

    [Header("Giris Bileseni")]
    [SerializeField] private FixedJoystick joystick; 

    [Header("Vampire Hunter Saldiri Ayarlari")]
    [SerializeField] private GameObject attackPointObject; 
    [SerializeField] private float attackOffset = 1f; 
    [SerializeField] private float attackDuration = 0.15f;
    [SerializeField] private float attackCooldown = 0.5f; 
    [SerializeField] private float autoAttackRange = 2f; 
    [SerializeField] private float playerDamage = 25f; // Karakterin vurus hasari
    [SerializeField] private float attackRadius = 0.5f; // Hasar alaninin yaricapi
    [SerializeField] private LayerMask enemyLayers; // Dusmanlarin bulundugu Layer

    [Header("Kamera Sallanti Ayarlari")]
    [SerializeField] private CameraShake cameraShake; 
    [SerializeField] private float shakeDuration; 
    [SerializeField] private float shakeMagnitude;

    private Rigidbody2D rb; 
    private Vector2 moveInput; 
    private Animator animator;
    
    private bool isCooldown = false; 

    void Start()
    {
        InitializeComponents();
    }

    void Update()
    {
        HandleMovementInput();
        UpdateAnimationState();
        HandleVampireHunterAttack();
    }

    void FixedUpdate()
    {
        MoveCharacterPhysics();
    }

    private void InitializeComponents()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void HandleMovementInput()
    {
        if (joystick != null)
        {
            moveInput = new Vector2(joystick.Horizontal, joystick.Vertical);
        }

        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }
    }

    private void UpdateAnimationState()
    {
        if (moveInput.sqrMagnitude > 0.1f)
        {   
            animator.SetBool("isRuning", true);
            animator.SetFloat("MoveX", moveInput.x);
            animator.SetFloat("MoveY", moveInput.y);
        }
        else
        {
            animator.SetBool("isRuning", false);
        }
    }

    private void MoveCharacterPhysics()
    {
        rb.linearVelocity = moveInput * moveSpeed;
    }

    private void HandleVampireHunterAttack()
    {
        if (isCooldown) return;

        Transform targetEnemy = GetClosestEnemy();

        if (targetEnemy != null)
        {
            Vector2 directionToEnemy = (targetEnemy.position - transform.position).normalized;
            StartCoroutine(VampireAttackRoutine(directionToEnemy));
        }
    }

    private Transform GetClosestEnemy()
    {
        // Sahnedeki her seyi aramak yerine sadece menzildeki enemyLayer'lari radarla tarar
        Collider2D[] enemiesInRange = Physics2D.OverlapCircleAll(transform.position, autoAttackRange, enemyLayers);
        
        Transform closestEnemy = null;
        float closestDistance = Mathf.Infinity;

        foreach (Collider2D enemyCollider in enemiesInRange)
        {
            EnemyController enemy = enemyCollider.GetComponent<EnemyController>();
            if (enemy == null) continue;

            float distanceToEnemy = Vector2.Distance(transform.position, enemy.transform.position);
            if (distanceToEnemy < closestDistance)
            {
                closestDistance = distanceToEnemy;
                closestEnemy = enemy.transform;
            }
        }

        return closestEnemy;
    }

    private IEnumerator VampireAttackRoutine(Vector2 targetDirection)
{
    isCooldown = true;

    // Düşmanı TEKRAR bul (coroutine başlamadan önce hareket etmiş olabilir)
    Transform targetEnemy = GetClosestEnemy();

    if (targetEnemy != null)
    {
        // Düşmanın anlık dünya konumunu al
        Vector3 enemyWorldPos = targetEnemy.position;
        
        // Düşmanın konumunu local koordinata çevir
        Vector3 localAttackPosition = transform.InverseTransformPoint(enemyWorldPos);
        
        // Maksimum mesafe sınırı (çok uzaktaki hedeflere anlamsız uzanmasın)
        float maxOffset = attackOffset * 1.5f;
        if (localAttackPosition.magnitude > maxOffset)
        {
            localAttackPosition = localAttackPosition.normalized * maxOffset;
        }
        
        localAttackPosition.z = 0f;
        attackPointObject.transform.localPosition = localAttackPosition;
    }
    else
    {
        // Düşman kaybolmuşsa varsayılan yön kullan
        attackPointObject.transform.localPosition = 
            new Vector3(targetDirection.x, targetDirection.y, 0f) * attackOffset;
    }

    attackPointObject.SetActive(true);

    // Hasar algılama (world pozisyonundan)
    Vector3 worldAttackPosition = attackPointObject.transform.position;
    Collider2D[] hitEnemies = Physics2D.OverlapCircleAll(worldAttackPosition, attackRadius, enemyLayers);

    foreach (Collider2D enemyCollider in hitEnemies)
    {
        EnemyController enemy = enemyCollider.GetComponent<EnemyController>();
        if (enemy != null)
        {
            enemy.TakeDamage(playerDamage);
        }
    }

    if (cameraShake != null)
    {
        cameraShake.TriggerShake(shakeDuration, shakeMagnitude);
    }

    yield return new WaitForSeconds(attackDuration);
    attackPointObject.SetActive(false);

    yield return new WaitForSeconds(attackCooldown);
    isCooldown = false;
}

    // Editör ekranında saldırı alanını görebilmek için çizim fonksiyonu
    private void OnDrawGizmosSelected()
    {
        if (attackPointObject != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPointObject.transform.position, attackRadius);
        }
    }
}