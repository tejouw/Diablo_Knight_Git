// Path: Assets/Game/Scripts/BotController.cs

using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;

public class BotController : NetworkBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float minIdleTime = 1f;
    [SerializeField] private float maxIdleTime = 2f;
    [SerializeField] private float minMoveTime = 2f;
    [SerializeField] private float maxMoveTime = 10f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float spawnRadius = 100f; // Spawn noktasından maksimum uzaklaşma mesafesi
    [Range(0.1f, 5f)]    
    [Header("Combat Settings")]
    [SerializeField] private float detectEnemyRange = 8f; // Düşman algılama mesafesi
    [SerializeField] private float attackRange = 2.5f; // Saldırı mesafesi
    [SerializeField] private float attackDelay = 1.5f; // Saldırılar arası bekleme süresi
    [SerializeField] private float fleeHealthPercent = 0.3f; // Kaçmaya başlama can yüzdesi
    [SerializeField] private float fleeDistance = 10f; // Kaçma mesafesi
    [Header("AI Settings")]
[SerializeField] private BotState currentState = BotState.Idle;
[SerializeField] private float patrolRadius = 100f;          // Devriye yarıçapı
[SerializeField] private float leashRange = 25f;           // Maksimum takip mesafesi
[SerializeField] private float patrolWaitTime = 1f;        // Devriye noktalarında bekleme süresi
[SerializeField] private bool isAggressive = true;         // Saldırgan mı?
[SerializeField] private bool isPatrolling = true;         // Devriye geziyor mu?
[SerializeField] private float obstacleDetectionDistance = 1.0f;  // Engel algılama mesafesi
[SerializeField] private LayerMask obstacleLayerMask; 

private bool isDead = false;
private Vector2 spawnPosition;
private Vector2 targetPosition;
private Vector2 moveDirection;
    
    
    private Rigidbody2D rb;
    [Networked] public BotState CurrentState { get; set; } = BotState.Idle;
[Networked] public Vector2 TargetPosition { get; set; }
[Networked] public Vector2 MoveDirection { get; set; }
[Networked] public NetworkId TargetEnemyId { get; set; }
[Networked] public TickTimer BehaviorTimer { get; set; }
[Networked] public TickTimer AttackTimer { get; set; }
[Networked] public float LastStateChangeTime { get; set; }
    private Character4D character4D;
    private PlayerStats playerStats;
    private WeaponSystem weaponSystem;
    private bool isInitialized = false;
    
public enum BotState
{
    Idle,
    Moving,
    Attacking,
    Fleeing,
    Returning,
    // Running durumunu ekleyelim
    Running
}
private void Awake()
{
    
    // Başlangıçta devre dışı bırak, SetBotFlagRPC ile aktifleştirilecek
    if (enabled)
    {
        enabled = false;
    }
}
    public override void Spawned()
    {

        if (isInitialized) return;

        // Component referanslarını al
        rb = GetComponent<Rigidbody2D>();
        character4D = GetComponent<Character4D>();
        playerStats = GetComponent<PlayerStats>();
        weaponSystem = GetComponent<WeaponSystem>();

        spawnPosition = transform.position;
        TargetPosition = transform.position;

        isInitialized = true;

        if (Object.HasStateAuthority)
        {
            BehaviorTimer = TickTimer.CreateFromSeconds(Runner, 1f);

            // *** BOT HP'Sİ DÜZELTİLMESİ ***
            StartCoroutine(InitializeBotStats());

            CurrentState = BotState.Idle;
            LastStateChangeTime = (float)Runner.SimulationTime;
            SetRandomMovementTarget();

        }
    }
private IEnumerator InitializeBotStats()
{
    // PlayerStats'ın initialize olmasını bekle
    int attempts = 0;
    while ((playerStats == null || playerStats.CurrentHP <= 0) && attempts < 50)
    {
        yield return new WaitForSeconds(0.1f);
        attempts++;
        
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();
    }
    
    if (playerStats != null && playerStats.CurrentHP <= 0)
    {
        
        // Server'da HP'yi düzelt
        if (Object.HasStateAuthority)
        {
            playerStats.SetHealthOnServer(playerStats.MaxHP);
        }
    }
    
}

public override void FixedUpdateNetwork()
{
    if (!isInitialized) 
    {
        Debug.LogWarning("[BotController] FixedUpdateNetwork - henüz initialize edilmedi");
        return;
    }

    if (Object.HasStateAuthority)
    {
        // Her 5 saniyede bir durum log'u
        if (Time.fixedTime % 5f < 0.02f)
        {
        }
        
        HandleBotBehavior();
        UpdateCharacterAnimations();

        if (BehaviorTimer.Expired(Runner))
        {
            HandleBotStateMachine();
            BehaviorTimer = TickTimer.CreateFromSeconds(Runner, 0.1f);
        }
    }
}
private void HandleBotStateMachine()
{
    if (!isPatrolling) 
    {
        return;
    }
    
    
    if (CurrentState == BotState.Idle)
    {
        float idleTime = Random.Range(minIdleTime, maxIdleTime);
        float elapsed = (float)Runner.SimulationTime - LastStateChangeTime;
        
        
        if (elapsed >= idleTime)
        {
            if (Random.value < 0.7f && playerStats != null && !playerStats.IsDead)
            {
                SetRandomMovementTarget();
                CurrentState = BotState.Moving;
                LastStateChangeTime = (float)Runner.SimulationTime;
            }
            else
            {
            }
        }
    }
    else if (CurrentState == BotState.Moving && 
             (float)Runner.SimulationTime - LastStateChangeTime >= Random.Range(minMoveTime, maxMoveTime))
    {
        CurrentState = BotState.Idle;
        LastStateChangeTime = (float)Runner.SimulationTime;
        rb.linearVelocity = Vector2.zero;
    }
}

public void ConfigureBehaviorSettings(
    float newMoveSpeed,
    float newDetectionRange,
    float newAttackRange,
    float newPatrolRadius,
    float newLeashRange,
    float newPatrolWaitTime,
    float newFleeHealthPercent,
    float newAttackDelay,
    bool newIsAggressive,
    bool newIsPatrolling)
{
    moveSpeed = newMoveSpeed;
    detectEnemyRange = newDetectionRange;
    attackRange = newAttackRange;
    patrolRadius = newPatrolRadius;
    leashRange = newLeashRange;
    patrolWaitTime = newPatrolWaitTime;
    fleeHealthPercent = newFleeHealthPercent;
    attackDelay = newAttackDelay;
    isAggressive = newIsAggressive;
    isPatrolling = newIsPatrolling;
    
    // Engel algılama layer'ını ayarla
    obstacleLayerMask = LayerMask.GetMask("Obstacles", "Wall");   

}
 
private void InitializeBot()
{
    if (isInitialized) return;
    
    rb = GetComponent<Rigidbody2D>();
    character4D = GetComponent<Character4D>();
    playerStats = GetComponent<PlayerStats>();
    weaponSystem = GetComponent<WeaponSystem>();
    
    spawnPosition = transform.position;
    TargetPosition = transform.position;
    
    PlayerController playerController = GetComponent<PlayerController>();
    if (playerController != null)
    {
        Destroy(playerController);
    }
    
    isInitialized = true;
    
    if (Object.HasStateAuthority)
    {
        BehaviorTimer = TickTimer.CreateFromSeconds(Runner, 1f);
    }
}
private void HandleBotBehavior()
{
    // Her 3 saniyede bir detaylı durum log'u
    if (Time.fixedTime % 3f < 0.02f) 
    {
    }
    
    // HP kontrolü (mevcut kod aynı)
    bool shouldFlee = false;
    if (playerStats != null && playerStats.MaxHP > 0)
    {
        float healthPercent = playerStats.CurrentHP / playerStats.MaxHP;
        shouldFlee = healthPercent <= fleeHealthPercent && CurrentState != BotState.Fleeing;
    }
    
    if (shouldFlee)
    {
        SetFleeingState();
        return;
    }
    
    switch (CurrentState)
    {
        case BotState.Idle:
            CheckForEnemies();
            break;
            
        case BotState.Moving:
            MoveTowardsTarget();
            CheckForEnemies();
            break;
            
        case BotState.Attacking:
            UpdateAttackBehavior();
            break;
            
        case BotState.Fleeing:
            UpdateFleeingBehavior();
            break;
            
        case BotState.Returning:
            ReturnToSpawn();
            break;
    }
}

    
private void CheckForEnemies()
{
    if (playerStats != null && playerStats.IsDead)
        return;
        
    if (!isAggressive)
        return;
            
    GameObject[] enemies = GameObject.FindGameObjectsWithTag("Monster");
    float closestDistance = detectEnemyRange;
    GameObject closestEnemy = null;
    
    foreach (GameObject enemy in enemies)
    {
        if (enemy == null) continue;
        
        MonsterBehaviour MonsterBehaviour = enemy.GetComponent<MonsterBehaviour>();
        if (MonsterBehaviour == null || MonsterBehaviour.CurrentHealth <= 0) continue;
        
        float distance = Vector2.Distance(transform.position, enemy.transform.position);
        if (distance < closestDistance)
        {
            closestDistance = distance;
            closestEnemy = enemy;
        }
    }
    
    if (closestEnemy != null)
    {
        NetworkObject enemyNetObj = closestEnemy.GetComponent<NetworkObject>();
        if (enemyNetObj != null)
        {
            TargetEnemyId = enemyNetObj.Id;
            CurrentState = BotState.Attacking;
            LastStateChangeTime = (float)Runner.SimulationTime;
        }
    }
}
private GameObject GetTargetEnemyFromId()
{
    if (TargetEnemyId == default(NetworkId)) return null;
    
    if (Runner.TryFindObject(TargetEnemyId, out NetworkObject netObj))
    {
        return netObj.gameObject;
    }
    return null;
}
private void UpdateAttackBehavior()
{
    GameObject targetEnemy = GetTargetEnemyFromId();
    if (targetEnemy == null)
    {
        CurrentState = BotState.Idle;
        LastStateChangeTime = (float)Runner.SimulationTime;
        return;
    }
    
    MonsterBehaviour MonsterBehaviour = targetEnemy.GetComponent<MonsterBehaviour>();
    if (MonsterBehaviour == null || MonsterBehaviour.CurrentHealth <= 0)
    {
        TargetEnemyId = default(NetworkId);
        CurrentState = BotState.Idle;
        LastStateChangeTime = (float)Runner.SimulationTime;
        return;
    }
    
    float distance = Vector2.Distance(transform.position, targetEnemy.transform.position);
    
    if (distance <= attackRange)
    {
        UpdateCharacterDirection((targetEnemy.transform.position - transform.position).normalized);
        rb.linearVelocity = Vector2.zero;
        
        if (AttackTimer.Expired(Runner) && weaponSystem != null)
        {
            PerformAttack();
            AttackTimer = TickTimer.CreateFromSeconds(Runner, attackDelay);
        }
    }
    else if (distance > detectEnemyRange * 1.5f)
    {
        TargetEnemyId = default(NetworkId);
        CurrentState = BotState.Idle;
        LastStateChangeTime = (float)Runner.SimulationTime;
    }
    else
    {
        Vector2 direction = (targetEnemy.transform.position - transform.position).normalized;
        rb.linearVelocity = direction * moveSpeed;
        UpdateCharacterDirection(direction);
    }
}
    
private void PerformAttack()
{
    if (weaponSystem != null)
    {
        weaponSystem.TryAttack();
        SyncBotAttackRPC();
    }
}
    
private void SetFleeingState()
{
    GameObject targetEnemy = GetTargetEnemyFromId();
    Vector2 fleeDirection = (transform.position - (targetEnemy != null ? targetEnemy.transform.position : FindNearestEnemy())).normalized;
    TargetPosition = (Vector2)transform.position + fleeDirection * fleeDistance;
    MoveDirection = fleeDirection;
    
    CurrentState = BotState.Fleeing;
    LastStateChangeTime = (float)Runner.SimulationTime;
}
    
    private Vector2 FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Monster");
        Vector2 averagePosition = Vector2.zero;
        int count = 0;
        
        foreach (GameObject enemy in enemies)
        {
            if (enemy != null && Vector2.Distance(transform.position, enemy.transform.position) < detectEnemyRange * 2)
            {
                averagePosition += (Vector2)enemy.transform.position;
                count++;
            }
        }
        
        return count > 0 ? averagePosition / count : Random.insideUnitCircle.normalized * 10f;
    }
    
private void UpdateFleeingBehavior()
{
    float elapsed = Runner.SimulationTime - LastStateChangeTime;
    
    if (elapsed > 5f)
    {
        if (playerStats != null && playerStats.CurrentHP / playerStats.MaxHP > fleeHealthPercent * 1.5f)
        {
            CurrentState = BotState.Idle;
            LastStateChangeTime = (float)Runner.SimulationTime;
            return;
        }
    }
    
    MoveTowardsTarget();
    
    if (Vector2.Distance(transform.position, spawnPosition) > spawnRadius * 1.5f)
    {
        CurrentState = BotState.Returning;
        LastStateChangeTime = (float)Runner.SimulationTime;
    }
}
    
private void ReturnToSpawn()
{
    Vector2 direction = (spawnPosition - (Vector2)transform.position).normalized;
    rb.linearVelocity = direction * moveSpeed;
    
    UpdateCharacterDirection(direction);
    
    if (Vector2.Distance(transform.position, spawnPosition) < 0.5f)
    {
        transform.position = spawnPosition;
        rb.linearVelocity = Vector2.zero;
        
        CurrentState = BotState.Idle;
        LastStateChangeTime = (float)Runner.SimulationTime;
    }
}

    
private void MoveTowardsTarget()
{
    Vector2 currentPos = transform.position;
    float distanceToTarget = Vector2.Distance(currentPos, TargetPosition);
    
    if (distanceToTarget < 0.5f)
    {
        CurrentState = BotState.Idle;
        LastStateChangeTime = (float)Runner.SimulationTime;
        rb.linearVelocity = Vector2.zero;
        return;
    }
    
    // *** SPEED SORUNU ÇÖZÜMÜ - PlayerStats hazır değilse base değer kullan ***
    float speed = (playerStats != null && playerStats.MoveSpeed > 0) ? 
        playerStats.MoveSpeed : moveSpeed; // Base moveSpeed kullan
    
    Vector2 direction = (TargetPosition - currentPos).normalized;
    
    
    if (rb != null)
    {
        rb.linearVelocity = direction * speed;
    }
    
    UpdateCharacterDirection(direction);
}
private void FixedUpdate()
{
    // Sadece eski FixedUpdate mantığını koru
    if (!Object.HasStateAuthority || isDead || !isInitialized) return;
    
    if (rb != null && rb.linearVelocity.magnitude > 0.1f)
    {
        CheckForObstacles();
    }
    
    HandleBotBehavior();
}
private void CheckForObstacles()
{
    if (!Object.HasStateAuthority) return;
    
    RaycastHit2D hit = Physics2D.Raycast(transform.position, MoveDirection, 
                                         obstacleDetectionDistance, obstacleLayerMask);
    
    Debug.DrawRay(transform.position, MoveDirection * obstacleDetectionDistance, Color.red, 0.1f);
    
    if (hit.collider != null)
    {
        float avoidAngle = Random.Range(90f, 180f);
        bool turnRight = Random.value > 0.5f;
        
        Vector2 avoidDirection = Quaternion.Euler(0, 0, turnRight ? avoidAngle : -avoidAngle) * MoveDirection;
        
        Vector2 newTarget = (Vector2)transform.position + avoidDirection.normalized * Random.Range(2f, 4f);
        
        if (Vector2.Distance(newTarget, spawnPosition) > leashRange)
        {
            newTarget = spawnPosition + (newTarget - spawnPosition).normalized * leashRange * 0.8f;
        }
        
        TargetPosition = newTarget;
        MoveDirection = (TargetPosition - (Vector2)transform.position).normalized;
        
        if (rb != null)
        {
            rb.linearVelocity = MoveDirection * moveSpeed;
        }
    }
}

private void SetRandomMovementTarget()
{
    
    // Mevcut konuma göre rastgele bir hedef belirle
    float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
    float distance = Random.Range(1f, patrolRadius);
    
    Vector2 offset = new Vector2(
        Mathf.Cos(angle) * distance,
        Mathf.Sin(angle) * distance
    );
    
    Vector2 potentialTarget = (Vector2)transform.position + offset;
    
    
    // Spawn noktasından çok uzaklaşmamasını sağla
    if (Vector2.Distance(potentialTarget, spawnPosition) > leashRange)
    {
        Vector2 toSpawn = (spawnPosition - (Vector2)transform.position).normalized;
        float randomAngle = Random.Range(-45f, 45f) * Mathf.Deg2Rad;
        
        Vector2 direction = new Vector2(
            toSpawn.x * Mathf.Cos(randomAngle) - toSpawn.y * Mathf.Sin(randomAngle),
            toSpawn.x * Mathf.Sin(randomAngle) + toSpawn.y * Mathf.Cos(randomAngle)
        );
        
        TargetPosition = (Vector2)transform.position + direction * Random.Range(1f, patrolRadius / 2);
    }
    else
    {
        TargetPosition = potentialTarget;
    }
    
    MoveDirection = (TargetPosition - (Vector2)transform.position).normalized;
    
    
    // Karakterin yönünü güncelle
    UpdateCharacterDirection(MoveDirection);
}
    
    private void UpdateCharacterDirection(Vector2 direction)
    {
        if (character4D == null) return;
        
        // Hareket yönüne göre karakter yönünü belirle
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            character4D.SetDirection(direction.x > 0 ? Vector2.right : Vector2.left);
        }
        else
        {
            character4D.SetDirection(direction.y > 0 ? Vector2.up : Vector2.down);
        }
    }
    
    private void UpdateCharacterAnimations()
    {
        if (character4D == null) return;
        
        if (rb != null && rb.linearVelocity .magnitude > 0.1f)
        {
            character4D.AnimationManager.SetState(CharacterState.Run);
        }
        else if (currentState == BotState.Attacking)
        {
            // Saldırı animasyonları WeaponSystem tarafından kontrol ediliyor
        }
        else
        {
            character4D.AnimationManager.SetState(CharacterState.Idle);
        }
    }
    

    
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncBotAttackRPC()
{
    if (character4D != null && weaponSystem != null)
    {
        if (weaponSystem.CurrentWeaponType == PlayerStats.WeaponType.Melee)
        {
            character4D.AnimationManager.Slash(true);
        }
        else
        {
            character4D.AnimationManager.ShotBow();
        }
    }
}


    public void SetBehaviorSettings(
    float newMoveSpeed,
    float newDetectionRange,
    float newAttackRange,
    float newPatrolRadius,
    float newLeashRange,
    float newPatrolWaitTime,
    float newFleeHealthPercent,
    float newAttackDelay,
    bool newIsAggressive,
    bool newIsPatrolling)
{
    moveSpeed = newMoveSpeed;
    detectEnemyRange = newDetectionRange;
    attackRange = newAttackRange;
    patrolRadius = newPatrolRadius;
    leashRange = newLeashRange;
    patrolWaitTime = newPatrolWaitTime;
    fleeHealthPercent = newFleeHealthPercent;
    attackDelay = newAttackDelay;
    isAggressive = newIsAggressive;
    isPatrolling = newIsPatrolling;
}
private void OnCollisionEnter2D(Collision2D collision)
{
    if (!Object.HasStateAuthority) return;
    
    if (!isDead && (CurrentState == BotState.Moving || CurrentState == BotState.Fleeing || 
                     CurrentState == BotState.Returning))
    {
        Vector2 normal = collision.contacts[0].normal;
        Vector2 reflectDirection = Vector2.Reflect(MoveDirection, normal);
        
        float randomAngle = Random.Range(-30f, 30f);
        reflectDirection = Quaternion.Euler(0, 0, randomAngle) * reflectDirection;
        
        Vector2 newTarget = (Vector2)transform.position + reflectDirection * Random.Range(2f, 4f);
        
        if (Vector2.Distance(newTarget, spawnPosition) > leashRange)
        {
            newTarget = spawnPosition + (newTarget - spawnPosition).normalized * leashRange * 0.8f;
        }
        
        TargetPosition = newTarget;
        MoveDirection = (TargetPosition - (Vector2)transform.position).normalized;
        
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }
        
        UpdateCharacterDirection(MoveDirection);
    }
}
}