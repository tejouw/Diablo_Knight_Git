using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;
using System;
using Random = UnityEngine.Random;
using Assets.FantasyMonsters.Common.Scripts;
using System.Linq;
public struct CoreData : INetworkStruct
{
public byte MonsterTypeByte;
public int Rarity;
public float Health;
public float MaxHealth;
public int MonsterLevel;
public int State;
public NetworkBool IsDead;
public float RotationY;
public MonsterRarity GetRarity() => (MonsterRarity)Rarity;
public MonsterState GetState() => (MonsterState)State;
public void SetRarity(MonsterRarity rarity) => Rarity = (int)rarity;
public void SetState(MonsterState state) => State = (int)state;
public void SetHealth(float health, float maxHealth)
{
Health = Mathf.Clamp(health, 0f, maxHealth);
MaxHealth = maxHealth;
IsDead = Health <= 0f;
}
public void SetRotationY(float rotationY) => RotationY = rotationY;
public string GetMonsterType()
{
string result = MonsterTypeMapping.GetMonsterTypeString(MonsterTypeByte);
return (result == "Unknown" && MonsterTypeByte != MonsterTypeMapping.UNKNOWN_MONSTER_TYPE) ? "Generic" : result;
}
public void SetMonsterType(string monsterType)
{
byte typeByte = MonsterTypeMapping.GetMonsterTypeByte(monsterType);
MonsterTypeByte = typeByte;
}
public void Initialize(string monsterType, MonsterRarity rarity, float maxHealth, int level)
{
SetMonsterType(monsterType);
SetRarity(rarity);
Health = maxHealth;
MaxHealth = maxHealth;
MonsterLevel = level;
SetState(MonsterState.Idle);
IsDead = false;
RotationY = 0f;
}
public bool IsHealthy() => Health > 0f && !IsDead;
public float GetHealthPercentage() => MaxHealth > 0f ? Health / MaxHealth : 0f;
}
public struct CombatData : INetworkStruct
{
public PlayerRef TargetingPlayer;
public NetworkBool IsAttacking;
public float AttackStartTime;
public NetworkBool IsKnockedBack;
public byte CurrentAnimationByte;
public bool IsCurrentlyAttacking(double currentSimTime, float maxAttackDuration = 2.0f)
=> IsAttacking && (currentSimTime - AttackStartTime) <= maxAttackDuration;
public void StartAttack(float startTime, string animation = "Attack")
{
IsAttacking = true;
AttackStartTime = startTime;
CurrentAnimationByte = AnimationMapping.GetAnimationByte(animation);
}
public void EndAttack()
{
IsAttacking = false;
AttackStartTime = 0f;
CurrentAnimationByte = AnimationMapping.NONE_ANIMATION;
}
public string GetCurrentAnimation() => AnimationMapping.GetAnimationString(CurrentAnimationByte);
public void SetTargetPlayer(PlayerRef player) => TargetingPlayer = player;
public void ClearTarget() => TargetingPlayer = PlayerRef.None;
public void SetKnockback(bool isKnockedBack) => IsKnockedBack = isKnockedBack;
public void ClearExpiredAttack(double currentSimTime, float maxDuration = 2.0f)
{
if (IsAttacking && (currentSimTime - AttackStartTime) > maxDuration)
EndAttack();
}
}
public struct TauntData : INetworkStruct
{
public NetworkBool IsTaunted;
public float EndTime;
public NetworkId TaunterId;
public bool IsActive(float currentTime) => IsTaunted && currentTime < EndTime;
public void Clear()
{
IsTaunted = false;
EndTime = 0f;
TaunterId = default(NetworkId);
}
public void Set(float endTime, NetworkId taunterId)
{
IsTaunted = true;
EndTime = endTime;
TaunterId = taunterId;
}
}
public struct DebuffData : INetworkStruct
{
public float DamageMultiplier;
public float DamageEndTime;
public float SlowMultiplier;
public float SlowEndTime;
public float AccuracyMultiplier;
public float AccuracyEndTime;
public bool HasActiveDamageDebuff(float currentTime) => currentTime < DamageEndTime;
public bool HasActiveSlowDebuff(float currentTime) => currentTime < SlowEndTime;
public bool HasActiveAccuracyDebuff(float currentTime) => currentTime < AccuracyEndTime;
public float GetCurrentDamageMultiplier(float currentTime)
=> HasActiveDamageDebuff(currentTime) ? DamageMultiplier : 1f;
public float GetCurrentSlowMultiplier(float currentTime)
=> HasActiveSlowDebuff(currentTime) ? SlowMultiplier : 1f;
public float GetCurrentAccuracyMultiplier(float currentTime)
=> HasActiveAccuracyDebuff(currentTime) ? AccuracyMultiplier : 1f;
public void SetDamageDebuff(float multiplier, float endTime)
{
DamageMultiplier = multiplier;
DamageEndTime = endTime;
}
public void SetSlowDebuff(float multiplier, float endTime)
{
SlowMultiplier = multiplier;
SlowEndTime = endTime;
}
public void SetAccuracyDebuff(float multiplier, float endTime)
{
AccuracyMultiplier = multiplier;
AccuracyEndTime = endTime;
}
public void ClearExpiredDebuffs(float currentTime)
{
if (!HasActiveDamageDebuff(currentTime))
{
DamageMultiplier = 1f;
DamageEndTime = 0f;
}
if (!HasActiveSlowDebuff(currentTime))
{
SlowMultiplier = 1f;
SlowEndTime = 0f;
}
if (!HasActiveAccuracyDebuff(currentTime))
{
AccuracyMultiplier = 1f;
AccuracyEndTime = 0f;
}
}
}
public class MonsterBehaviour : NetworkBehaviour, IInterestEnter, IInterestExit
{
[Networked, HideInInspector] public float NetworkedRotationY { get; set; }
[Networked, HideInInspector] public Vector2 NetworkedPosition { get; set; }
[Networked, HideInInspector] public ref CombatData NetworkCombatData => ref MakeRef<CombatData>();
[Networked, HideInInspector] public ref DebuffData NetworkDebuffData => ref MakeRef<DebuffData>();
[Networked, HideInInspector] public ref TauntData NetworkTauntData => ref MakeRef<TauntData>();
[Networked, HideInInspector] public ref CoreData NetworkCoreData => ref MakeRef<CoreData>();
[Header("Auto Scaling")]
[SerializeField] public bool useAutoScaling = false;
[SerializeField] private int _monsterLevel = 1;
private const float LEVEL1_HP = 25f;
private const float LEVEL1_DAMAGE = 1.5f;
private const float LEVEL1_DEFENSE = 2.5f;
private const float LEVEL1_MOVE_SPEED = 1.5f;
private const float LEVEL1_XP = 25f;
private const int LEVEL1_MIN_COIN = 5;
private const int LEVEL1_MAX_COIN = 12;
private const float LEVEL1_DETECTION_RANGE = 4f;
private const float LEVEL1_ATTACK_RANGE = 1.5f;
private const float LEVEL1_ATTACK_COOLDOWN = 1.2f;
[Header("Monster Identity")]
[SerializeField] public string monsterType;
[SerializeField, HideInInspector] private string displayName;
[Header("AI Settings")]
[SerializeField, HideInInspector] private MonsterState currentState = MonsterState.Idle;
[SerializeField] private float patrolRadius = 10f;
[SerializeField] private float leashRange = 15f;
[SerializeField] private float patrolWaitTime = 3f;
[SerializeField] private bool isAggressive = true;
[SerializeField] private bool nightAggressive = false;
[SerializeField] private bool isPatrolling = true;
[SerializeField, HideInInspector] private float _chaseSpeedMultiplier = 1.5f;
[Header("Movement Settings")]
[SerializeField] private float moveSpeed = 2f;
[SerializeField] private float detectionRange = 5f;
[SerializeField] private float attackRange = 2f;
[Range(0.1f, 5f)]
[SerializeField] private float attackCooldown = 1f;
[Header("Combat Stats")]
[SerializeField] private float baseDamage = 10f;
[SerializeField] private float maxHealth = 50f;
[SerializeField] private float baseDefense = 5f;
[SerializeField] private float xpValue = 50f;
private Monster fantasyMonster;
private MonsterBehaviourUI monsterUI;
private UIManager uiManager;
private SpawnArea spawnArea;
private NetworkTransform networkTransform;
private Rigidbody2D rb;
private MonsterLootSystem lootSystem;
private Transform bodyTransform;
private ChangeDetector _changeDetector;
private bool isDead = false;
private bool isInitialized = false;
private bool isSpawned = false;
private bool isVisibleToLocalPlayer = false;
private float currentHealth;
private int lastKillerActorNumber;
private float patrolIdleStartTime = 0f;
private float currentPatrolIdleDuration = 0f;
private bool isInPatrolIdle = false;
private GameObject currentTaunter;
private Vector2 spawnPosition;
private Vector2 currentPatrolTarget;
private float lastStateChangeTime;
private PlayerRef targetingPlayer = PlayerRef.None;
private Transform targetPlayer;
private PlayerStats targetPlayerStats;
private float nextAttackTime = 0f;
private MonsterRarity monsterRarity = MonsterRarity.Normal;
private float combatMultiplier = 1f;
private float speedMultiplier = 1f;
private float sizeMultiplier = 1f;
private Vector3 baseScale;
private float baseMaxHealth;
private float baseBaseDamage;
private float baseBaseDefense;
private float baseMoveSpeed;
private float baseXpValue;
private int frameCounter = 0;
private Vector2 lastPosition;
private float stuckCheckTimer = 0f;
private const float STUCK_CHECK_INTERVAL = 2f;
private const float STUCK_DISTANCE_THRESHOLD = 0.5f;
private bool lastDirectionRight = false;
private Coroutine slowDebuffCoroutine;
private Coroutine damageDebuffCoroutine;
private Coroutine accuracyDebuffCoroutine;
private bool wasNightLastCheck = false;
public bool IsDead => NetworkCoreData.IsDead;
public MonsterRarity Rarity => NetworkCoreData.GetRarity();
public string RarityPrefix => NetworkCoreData.GetRarity() == MonsterRarity.Magic ? "(Magic) " :
NetworkCoreData.GetRarity() == MonsterRarity.Rare ? "(Rare) " : "";
public string MonsterType => NetworkCoreData.GetMonsterType();
public string DisplayName => string.IsNullOrEmpty(displayName) ? NetworkCoreData.GetMonsterType() : displayName;
public float CurrentHealth => NetworkCoreData.Health;
public float MaxHealth => NetworkCoreData.MaxHealth;
public float CurrentAccuracy
{
get
{
float baseAccuracy = 1f;
baseAccuracy *= NetworkDebuffData.GetCurrentAccuracyMultiplier(Time.time);
return Mathf.Clamp(baseAccuracy, 0f, 1f);
}
}
public bool IsInitialized => isInitialized;
public bool IsSpawned => isSpawned;
public bool IsVisibleToLocalPlayer => isVisibleToLocalPlayer;
public float chaseSpeedMultiplier
{
get => _chaseSpeedMultiplier;
set => _chaseSpeedMultiplier = value;
}
public int monsterLevel
{
get => _monsterLevel;
set
{
_monsterLevel = value;
if (useAutoScaling && Application.isPlaying)
ApplyAutoScaling();
}
}
public void InterestEnter(PlayerRef player)
{
if (Runner.LocalPlayer != player || !Runner.GetVisible()) return;
isVisibleToLocalPlayer = true;
if (_changeDetector == null)
_changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
MonsterState currentNetworkState = NetworkCoreData.GetState();
currentState = currentNetworkState;
if (fantasyMonster != null)
{
fantasyMonster.SetState(currentNetworkState);
monsterUI?.UpdateHeadSprite(currentNetworkState);
}
if (monsterUI != null)
{
monsterUI.gameObject.SetActive(true);
monsterUI.UpdateHealthUI(CurrentHealth, MaxHealth);
MonsterRarity rarity = NetworkCoreData.GetRarity();
if (rarity != MonsterRarity.Normal)
{
Color rarityColor = MonsterBehaviourUI.GetRarityTintColor(rarity);
monsterUI.ApplyColorToAllSprites(rarityColor);
}
}
foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
if (renderer != null) renderer.enabled = true;
var animator = GetComponent<Animator>();
if (animator != null) animator.enabled = true;
}
public void InterestExit(PlayerRef player)
{
if (Runner.LocalPlayer != player || !Runner.GetVisible()) return;
isVisibleToLocalPlayer = false;
_changeDetector = null;
if (monsterUI != null)
monsterUI.gameObject.SetActive(false);
foreach (var renderer in GetComponentsInChildren<SpriteRenderer>())
if (renderer != null) renderer.enabled = false;
var animator = GetComponent<Animator>();
if (animator != null) animator.enabled = false;
}
private void Awake()
{
fantasyMonster = GetComponent<Monster>();
monsterUI = GetComponent<MonsterBehaviourUI>();
networkTransform = GetComponent<NetworkTransform>();
rb = GetComponent<Rigidbody2D>();
uiManager = FindFirstObjectByType<UIManager>();
lootSystem = GetComponent<MonsterLootSystem>();
bodyTransform = transform.Find("Body");
if (bodyTransform == null)
Debug.LogWarning($"[{gameObject.name}] Body transform bulunamadı! Children: {string.Join(", ", GetComponentsInChildren<Transform>().Select(t => t.name))}");
baseScale = transform.localScale;
if (baseScale.magnitude < 0.1f)
{
baseScale = Vector3.one;
transform.localScale = baseScale;
}
StoreBaseStats();
currentHealth = maxHealth;
}
public override void Spawned()
{
if (Runner != null && Object != null)
{
if (Object.HasStateAuthority)
{
Runner.SetIsSimulated(Object, true);
if (rb != null)
{
rb.bodyType = RigidbodyType2D.Dynamic;
rb.mass = 1f;
rb.linearDamping = 2f;
rb.freezeRotation = true;
}
}
else
{
Runner.SetIsSimulated(Object, false);
if (rb != null)
{
rb.bodyType = RigidbodyType2D.Kinematic;
rb.simulated = false;
}
Collider2D col = GetComponent<Collider2D>();
if (col != null)
{
col.enabled = true;
col.isTrigger = true;
}
}
}
InitializeFantasyComponents();
if (Object.HasStateAuthority)
{
NetworkCoreData.SetHealth(currentHealth, maxHealth);
NetworkCoreData.SetMonsterType(monsterType);
NetworkCoreData.SetRarity(monsterRarity);
NetworkCoreData.MonsterLevel = monsterLevel;
}
_changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
isSpawned = true;
if (MonsterManager.Instance != null)
MonsterManager.Instance.RegisterMonster(this);
if (!Object.HasStateAuthority)
InterestEnter(Runner.LocalPlayer);
}
public override void Despawned(NetworkRunner runner, bool hasState)
{
if (!Object.HasStateAuthority)
InterestExit(runner.LocalPlayer);
isSpawned = false;
if (MonsterManager.Instance != null)
MonsterManager.Instance.UnregisterMonster(this);
}
private void Start()
{
currentHealth = maxHealth;
if (Object.HasStateAuthority && rb != null)
rb.bodyType = RigidbodyType2D.Dynamic;
if (fantasyMonster != null)
fantasyMonster.SetState(MonsterState.Idle);
}
private void OnDestroy()
{
isSpawned = false;
if (MonsterManager.Instance != null)
MonsterManager.Instance.UnregisterMonster(this);
if (Object != null && Object.HasStateAuthority)
{
MonsterSpawner spawner = FindAnyObjectByType<MonsterSpawner>();
if (spawner != null)
spawner.UnregisterMonster(Object.Id);
}
if (targetingPlayer != PlayerRef.None && Object != null && Object.HasInputAuthority)
uiManager?.HideTargetInfo();
if (transform.parent != null && transform.parent.childCount == 1)
{
string containerName = transform.parent.name;
Transform monstersTransform = transform.parent.parent;
}
if (slowDebuffCoroutine != null) StopCoroutine(slowDebuffCoroutine);
if (damageDebuffCoroutine != null) StopCoroutine(damageDebuffCoroutine);
if (accuracyDebuffCoroutine != null) StopCoroutine(accuracyDebuffCoroutine);
}
public void ApplyAutoScaling()
{
if (monsterLevel <= 0) monsterLevel = 1;
maxHealth = Mathf.Ceil(LEVEL1_HP * GetStatMultiplier(monsterLevel, 0.25f));
baseDamage = Mathf.Ceil(LEVEL1_DAMAGE * GetStatMultiplier(monsterLevel, 0.035f));
baseDefense = Mathf.Ceil(LEVEL1_DEFENSE * GetStatMultiplier(monsterLevel, 0.03f));
moveSpeed = LEVEL1_MOVE_SPEED * GetStatMultiplier(monsterLevel, 0.0005f);
xpValue = Mathf.Ceil(LEVEL1_XP * GetStatMultiplier(monsterLevel, 0.08f));
float coinMultiplier = GetStatMultiplier(monsterLevel, 0.025f);
int minCoin = Mathf.RoundToInt(LEVEL1_MIN_COIN * coinMultiplier);
int maxCoin = Mathf.RoundToInt(LEVEL1_MAX_COIN * coinMultiplier);
if (lootSystem != null)
lootSystem.UpdateCoinRange(minCoin, maxCoin);
detectionRange = LEVEL1_DETECTION_RANGE * GetStatMultiplier(monsterLevel, 0.01f);
attackRange = LEVEL1_ATTACK_RANGE * GetStatMultiplier(monsterLevel, 0.005f);
attackCooldown = LEVEL1_ATTACK_COOLDOWN * GetCooldownMultiplier(monsterLevel);
useAutoScaling = true;
currentHealth = maxHealth;
StoreBaseStats();
}
public void ResetToManualStats() => useAutoScaling = false;
private float GetStatMultiplier(int level, float growthRate)
{
    float baseMultiplier = 1f + (level - 1) * (level - 1) * growthRate;
    // 1. seviyeden sonraki artışları %30 daha güçlü yap
    float levelIncrease = baseMultiplier - 1f;
    return 1f + (levelIncrease * 1.3f);
}
private float GetCooldownMultiplier(int level)
{
float baseReduction = 1f;
float reductionRate = 0.008f;
float multiplier = baseReduction - (level - 1) * (level - 1) * reductionRate;
return Mathf.Max(0.4f, multiplier);
}
private void StoreBaseStats()
{
baseMaxHealth = maxHealth;
baseBaseDamage = baseDamage;
baseBaseDefense = baseDefense;
baseMoveSpeed = moveSpeed;
baseXpValue = xpValue;
}
private void InitializeFantasyComponents()
{
try
{
if (fantasyMonster != null)
{
fantasyMonster.OnEvent += HandleMonsterEvent;
var stateHandlers = fantasyMonster.Animator.GetBehaviours<StateHandler>();
var deathHandler = stateHandlers.FirstOrDefault(i => i.Name == "Death");
if (deathHandler != null)
deathHandler.StateExit.AddListener(OnDeathAnimationComplete);
if (Object.HasStateAuthority)
{
fantasyMonster.SetState(MonsterState.Idle);
RPC_SyncMonsterState((int)MonsterState.Idle, true);
}
isInitialized = true;
}
}
catch (System.Exception) { }
}
public void InitializeMonster(string type, SpawnArea area)
{
monsterType = type;
spawnArea = area;
spawnPosition = transform.position;
if (!isInitialized)
InitializeFantasyComponents();
}
public void Initialize(MonsterRarity rarity, Color tintColor, float combatMult, float speedMult, float sizeMult, int level = 1)
{
monsterRarity = rarity;
combatMultiplier = combatMult;
speedMultiplier = speedMult;
sizeMultiplier = sizeMult;
monsterLevel = level;
baseDamage = baseBaseDamage * combatMultiplier;
maxHealth = baseMaxHealth * combatMultiplier;
currentHealth = maxHealth;
baseDefense = baseBaseDefense * combatMultiplier;
moveSpeed = baseMoveSpeed * speedMultiplier;
xpValue = baseXpValue * combatMultiplier;
if (lootSystem != null)
{
int calculatedMin = Mathf.RoundToInt(LEVEL1_MIN_COIN * combatMultiplier);
int calculatedMax = Mathf.RoundToInt(LEVEL1_MAX_COIN * combatMultiplier);
lootSystem.UpdateCoinRange(calculatedMin, calculatedMax);
}
Vector3 finalScale = baseScale * sizeMultiplier;
if (finalScale.magnitude < 0.1f)
finalScale = Vector3.one * sizeMultiplier;
transform.localScale = finalScale;
if (monsterUI != null)
monsterUI.ApplyColorToAllSprites(tintColor);
if (Object.HasStateAuthority)
NetworkCoreData.Initialize(monsterType, rarity, maxHealth, level);
byte monsterTypeByte = MonsterTypeMapping.GetMonsterTypeByte(monsterType);
RPC_SyncMonsterProperties(monsterTypeByte, (int)rarity,
new float[] { tintColor.r, tintColor.g, tintColor.b, tintColor.a },
combatMult, speedMult, sizeMult, monsterLevel);
}
private void UpdateAIState()
{
if (!Runner.IsServer || isDead || !isInitialized) return;
switch (currentState)
{
case MonsterState.Idle: HandleIdleState(); break;
case MonsterState.Walk: HandlePatrolState(); break;
case MonsterState.Run: HandleChaseState(); break;
case MonsterState.Ready: HandleAttackState(); break;
}
}
private void HandleIdleState()
{
if (!Runner.IsServer) return;
FindNearestPlayer();
if (targetPlayer != null)
{
isInPatrolIdle = false;
ChangeMonsterState(MonsterState.Run);
return;
}
if (fantasyMonster != null)
fantasyMonster.SetState(GetEffectiveAggressiveness() ? MonsterState.Ready : MonsterState.Idle);
if (isInPatrolIdle)
{
if (Time.time - patrolIdleStartTime >= currentPatrolIdleDuration)
{
isInPatrolIdle = false;
ChangeMonsterState(MonsterState.Walk);
}
return;
}
if (isPatrolling && Time.time - lastStateChangeTime > patrolWaitTime)
ChangeMonsterState(MonsterState.Walk);
}
private void HandlePatrolState()
{
if (!Runner.IsServer) return;
if (!isPatrolling)
{
ChangeMonsterState(MonsterState.Idle);
return;
}
if (currentPatrolTarget == Vector2.zero)
{
SetNewPatrolTarget();
return;
}
float distanceToTarget = Vector2.Distance(transform.position, currentPatrolTarget);
float arrivalTolerance = speedMultiplier > 1.2f ? 1.2f : 0.8f;
if (distanceToTarget > arrivalTolerance)
{
Vector2 direction = (currentPatrolTarget - (Vector2)transform.position).normalized;
if (rb != null)
rb.linearVelocity = direction * GetCurrentMoveSpeed();
UpdateMonsterDirection(direction);
if (fantasyMonster != null)
fantasyMonster.SetState(MonsterState.Walk);
}
else
{
if (rb != null)
rb.linearVelocity = Vector2.zero;
currentPatrolTarget = Vector2.zero;
if (!isInPatrolIdle)
{
isInPatrolIdle = true;
patrolIdleStartTime = Time.time;
currentPatrolIdleDuration = Random.Range(1f, 3f);
}
ChangeMonsterState(MonsterState.Idle);
}
if (distanceToTarget > arrivalTolerance)
{
FindNearestPlayer();
if (targetPlayer != null && GetEffectiveAggressiveness())
{
isInPatrolIdle = false;
ChangeMonsterState(MonsterState.Run);
}
}
}
private void HandleChaseState()
{
if (!Runner.IsServer) return;
if (targetPlayer == null)
{
ChangeMonsterState(MonsterState.Walk);
return;
}
UpdateMonsterDirection(targetPlayer.position);
float distanceToTarget = Vector2.Distance(transform.position, targetPlayer.position);
float distanceToSpawn = Vector2.Distance(transform.position, spawnPosition);
PlayerStats targetStats = targetPlayer.GetComponent<PlayerStats>();
if (targetStats != null && targetStats.IsDead)
{
ClearTarget();
ChangeMonsterState(MonsterState.Walk);
return;
}
if (distanceToSpawn > leashRange)
{
if (targetingPlayer != PlayerRef.None)
SetTargetingPlayer(false, targetingPlayer);
ChangeMonsterState(MonsterState.Walk);
return;
}
if (distanceToTarget <= attackRange * 0.9f)
{
ChangeMonsterState(MonsterState.Ready);
return;
}
if (fantasyMonster != null)
fantasyMonster.SetState(MonsterState.Run);
Vector2 direction = ((Vector2)targetPlayer.position - (Vector2)transform.position).normalized;
if (rb != null)
rb.linearVelocity = direction * GetCurrentMoveSpeed() * chaseSpeedMultiplier;
}
private void HandleAttackState()
{
if (!Runner.IsServer) return;
if (targetPlayer == null)
{
ChangeMonsterState(MonsterState.Walk);
return;
}
UpdateMonsterDirection(targetPlayer.position);
float distanceToTarget = Vector2.Distance(transform.position, targetPlayer.position);
float distanceToSpawn = Vector2.Distance(transform.position, spawnPosition);
if (distanceToSpawn > leashRange)
{
if (NetworkCombatData.TargetingPlayer != PlayerRef.None)
SetTargetingPlayer(false, NetworkCombatData.TargetingPlayer);
ChangeMonsterState(MonsterState.Walk);
return;
}
PlayerStats targetStats = targetPlayer.GetComponent<PlayerStats>();
if (targetStats != null && targetStats.IsDead)
{
ClearTarget();
ChangeMonsterState(MonsterState.Walk);
return;
}
bool isInAttackRange = IsInAttackRange(targetPlayer.transform);
if (isInAttackRange)
{
if (rb != null)
rb.linearVelocity = Vector2.zero;
if (Time.time >= nextAttackTime)
{
NetworkCombatData.StartAttack((float)Runner.SimulationTime, "Attack");
RPC_PlayAttackAnimation();
DealDamageToPlayer();
nextAttackTime = Time.time + attackCooldown;
}
}
else if (distanceToTarget <= detectionRange)
{
ChangeMonsterState(MonsterState.Run);
}
else
{
ChangeMonsterState(MonsterState.Walk);
}
}
private void ChangeMonsterState(MonsterState newState)
{
if (!Object.HasStateAuthority) return;
if (currentState == newState || !isInitialized) return;
if (Time.time - lastStateChangeTime < 0.5f) return;
try
{
MonsterState oldState = currentState;
currentState = newState;
lastStateChangeTime = Time.time;
NetworkCoreData.SetState(newState);
if (fantasyMonster != null)
{
monsterUI?.UpdateHeadSprite(newState);
fantasyMonster.SetState(newState);
RPC_SyncMonsterState((int)newState, true);
}
if (newState == MonsterState.Walk)
{
if (NetworkCombatData.TargetingPlayer != PlayerRef.None)
SetTargetingPlayer(false, NetworkCombatData.TargetingPlayer);
}
if (NetworkCombatData.TargetingPlayer != PlayerRef.None && Object.HasInputAuthority)
{
if (uiManager != null && (newState == MonsterState.Walk || newState == MonsterState.Idle))
uiManager.HideTargetInfo();
}
}
catch (System.Exception) { }
}
private void FindNearestPlayer()
{
if (!Runner.IsServer) return;
if (IsTaunted())
{
GameObject taunter = GetTaunter();
if (taunter != null)
{
float distanceToTaunter = Vector2.Distance(transform.position, taunter.transform.position);
if (distanceToTaunter <= leashRange)
{
targetPlayer = taunter.transform;
targetPlayerStats = taunter.GetComponent<PlayerStats>();
UpdateMonsterDirection(targetPlayer.position);
return;
}
}
NetworkTauntData.Clear();
currentTaunter = null;
}
if (PlayerManager.Instance == null) return;
List<PlayerManager.PlayerData> nearbyPlayers = PlayerManager.Instance.GetPlayersNear(transform.position, detectionRange + 2f);
float closestDistance = float.MaxValue;
Transform closestPlayer = null;
PlayerStats closestPlayerStats = null;
foreach (var playerData in nearbyPlayers)
{
if (playerData.transform == null || playerData.playerStats.IsDead) continue;
float distance = Vector2.Distance(transform.position, playerData.transform.position);
if (!GetEffectiveAggressiveness())
{
bool isAttacking = IsBeingAttackedBy(playerData.transform.gameObject);
if (!isAttacking) continue;
}
if (distance < closestDistance && distance <= detectionRange && HasLineOfSight(playerData.transform.position))
{
closestDistance = distance;
closestPlayer = playerData.transform;
closestPlayerStats = playerData.playerStats;
}
}
targetPlayer = closestPlayer;
targetPlayerStats = closestPlayerStats;
if (targetPlayer != null) UpdateMonsterDirection(targetPlayer.position);
}
private bool IsBeingAttackedBy(GameObject player)
{
WeaponSystem weaponSystem = player.GetComponent<WeaponSystem>();
if (weaponSystem != null && weaponSystem.CurrentTarget == gameObject)
return true;
return targetingPlayer != PlayerRef.None &&
player.GetComponent<NetworkObject>()?.InputAuthority == targetingPlayer;
}
private void ClearTarget()
{
if (targetPlayer != null)
{
SetTargetingPlayer(false, NetworkCombatData.TargetingPlayer);
targetPlayer = null;
}
}
private void SetNewPatrolTarget()
{
if (!Runner.IsServer) return;
float adjustedPatrolRadius = patrolRadius * (speedMultiplier > 1.2f ? 1.5f : 1.0f);
for (int attempt = 0; attempt < 10; attempt++)
{
float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
float randomRadius = Random.Range(adjustedPatrolRadius * 0.3f, adjustedPatrolRadius);
Vector2 offset = new Vector2(
Mathf.Cos(randomAngle) * randomRadius,
Mathf.Sin(randomAngle) * randomRadius
);
Vector2 potentialTarget = spawnPosition + offset;
float checkRadius = sizeMultiplier > 1.2f ? 0.8f : 0.5f;
bool hasObstacle = Physics2D.OverlapCircle(potentialTarget, checkRadius,
LayerMask.GetMask("Obstacles", "Monster", "Wall")) != null;
if (!hasObstacle)
{
currentPatrolTarget = potentialTarget;
Vector2 newDirection = (currentPatrolTarget - (Vector2)transform.position).normalized;
UpdateMonsterDirection(newDirection);
return;
}
}
float fallbackAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
float fallbackDistance = adjustedPatrolRadius * 0.5f;
Vector2 fallbackOffset = new Vector2(
Mathf.Cos(fallbackAngle) * fallbackDistance,
Mathf.Sin(fallbackAngle) * fallbackDistance
);
currentPatrolTarget = spawnPosition + fallbackOffset;
Vector2 direction = (currentPatrolTarget - (Vector2)transform.position).normalized;
UpdateMonsterDirection(direction);
}
public void SetTargetingPlayer(bool targeting, PlayerRef playerRef)
{
try
{
if (targeting && playerRef == PlayerRef.None) return;
if (targeting)
NetworkCombatData.SetTargetPlayer(playerRef);
else
NetworkCombatData.ClearTarget();
if (!targeting && playerRef != PlayerRef.None && Object.HasInputAuthority)
uiManager?.HideTargetInfo();
}
catch (System.Exception)
{
NetworkCombatData.ClearTarget();
}
}
public bool IsTargetingPlayer(GameObject player)
{
try
{
if (NetworkCombatData.TargetingPlayer == PlayerRef.None || player == null) return false;
var playerNetworkObject = player.GetComponent<NetworkObject>();
if (playerNetworkObject == null) return false;
if (currentState == MonsterState.Walk ||
currentState == MonsterState.Idle ||
currentState == MonsterState.Custom)
return false;
return playerNetworkObject.InputAuthority == NetworkCombatData.TargetingPlayer;
}
catch (System.Exception)
{
return false;
}
}
private void UpdateMonsterDirection(Vector2 targetDirection)
{
bool shouldFaceRight = targetDirection.x > 0;
if (Object.HasStateAuthority)
{
float targetY = shouldFaceRight ? 180f : 0f;
if (Mathf.Abs(NetworkedRotationY - targetY) > 1f)
NetworkedRotationY = targetY;
}
}
private void UpdateMonsterDirection(Vector3 targetPosition)
{
bool shouldFaceRight = targetPosition.x > transform.position.x;
if (Object.HasStateAuthority)
{
float targetY = shouldFaceRight ? 180f : 0f;
if (Mathf.Abs(NetworkedRotationY - targetY) > 1f)
NetworkedRotationY = targetY;
}
}
private bool cachedLineOfSight = true;
private float lastLineOfSightCheck = 0f;
private const float LINE_OF_SIGHT_CHECK_INTERVAL = 0.5f;
private bool HasLineOfSight(Vector2 targetPos)
{
if (!Runner.IsServer) return cachedLineOfSight;
if (Time.time - lastLineOfSightCheck < LINE_OF_SIGHT_CHECK_INTERVAL)
return cachedLineOfSight;
lastLineOfSightCheck = Time.time;
Vector2 direction = targetPos - (Vector2)transform.position;
int layerMask = LayerMask.GetMask("Obstacles", "Wall");
RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, detectionRange, layerMask);
cachedLineOfSight = hit.collider == null;
return cachedLineOfSight;
}
private void CheckAndAvoidCollisions()
{
if (!Object.HasStateAuthority || !Runner.IsServer || isDead) return;
Vector2 currentVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
Vector2 currentDirection = currentVelocity.normalized;
if (currentDirection.magnitude < 0.1f && currentState == MonsterState.Walk && currentPatrolTarget != Vector2.zero)
{
Vector2 randomDirection = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
if (rb != null)
rb.linearVelocity = randomDirection * GetCurrentMoveSpeed();
UpdateMonsterDirection(randomDirection);
SetNewPatrolTarget();
return;
}
float checkDistance = 2.0f + (sizeMultiplier * 0.5f);
int layerMask = ~(1 << 8);
RaycastHit2D forwardHit = Physics2D.Raycast(transform.position, currentDirection, checkDistance, layerMask);
if (forwardHit.collider != null && !IsObstacleIgnorable(forwardHit.collider))
{
HandleObstacleAvoidance(currentDirection);
return;
}
Vector2 rightDirection = Quaternion.Euler(0, 0, 45f) * currentDirection;
Vector2 leftDirection = Quaternion.Euler(0, 0, -45f) * currentDirection;
RaycastHit2D rightHit = Physics2D.Raycast(transform.position, rightDirection, checkDistance * 0.7f, layerMask);
RaycastHit2D leftHit = Physics2D.Raycast(transform.position, leftDirection, checkDistance * 0.7f, layerMask);
if ((rightHit.collider != null && !IsObstacleIgnorable(rightHit.collider)) ||
(leftHit.collider != null && !IsObstacleIgnorable(leftHit.collider)))
{
HandleObstacleAvoidance(currentDirection);
}
}
private bool IsObstacleIgnorable(Collider2D collider)
{
if (collider == null) return true;
if (collider.gameObject.layer == 8) return true;
if (collider.transform == transform) return true;
if (collider.CompareTag("Loot")) return true;
if (collider.GetComponent<DroppedLoot>() != null) return true;
if (collider.GetComponent<CoinDrop>() != null) return true;
return false;
}
private bool IsValidTarget(Collider2D collider)
{
if (collider == null) return false;
if (collider.transform == transform) return false;
if (collider.gameObject.layer == 8) return true;
if (collider.CompareTag("Loot")) return true;
if (collider.GetComponent<DroppedLoot>() != null) return true;
if (collider.GetComponent<CoinDrop>() != null) return true;
return false;
}
private void HandleObstacleAvoidance(Vector2 currentDirection)
{
float avoidAngle = Random.Range(90f, 180f);
bool turnRight = Random.value > 0.5f;
Vector2 newDirection = Quaternion.Euler(0, 0, turnRight ? avoidAngle : -avoidAngle) * currentDirection;
RaycastHit2D checkNewDirection = Physics2D.Raycast(transform.position, newDirection, 1.5f);
if (checkNewDirection.collider != null && !IsValidTarget(checkNewDirection.collider))
newDirection = Quaternion.Euler(0, 0, turnRight ? -avoidAngle : avoidAngle) * currentDirection;
if (rb != null)
rb.linearVelocity = newDirection * GetCurrentMoveSpeed();
UpdateMonsterDirection(newDirection);
if (currentState == MonsterState.Walk)
SetNewPatrolTarget();
}
private float GetCurrentMoveSpeed()
{
float currentSpeed = moveSpeed;
currentSpeed *= NetworkDebuffData.GetCurrentSlowMultiplier(Time.time);
return currentSpeed;
}
private void HandleMonsterEvent(string eventName)
{
if (!Object.HasStateAuthority) return;
try
{
switch (eventName)
{
case "AttackStart": break;
case "AttackPoint": DealDamageToPlayer(); break;
case "AttackEnd": nextAttackTime = Time.time + attackCooldown; break;
}
}
catch (System.Exception) { }
}
private bool IsInAttackRange(Transform target)
{
if (target == null) return false;
float distance = Vector2.Distance(transform.position, target.position);
return distance <= attackRange;
}
private void DealDamageToPlayer()
{
if (!Runner.IsServer) return;
if (targetPlayerStats == null) return;
if (!IsInAttackRange(targetPlayer.transform))
return;
if (!HasLineOfSight(targetPlayer.position))
return;
if (!RollAccuracyCheck())
{
ShowMissEffectRPC(targetPlayer.position);
return;
}
float finalDamage = CalculateDamage();
var targetNetworkObject = targetPlayer.GetComponent<NetworkObject>();
if (targetNetworkObject != null)
RPC_DealDamageToPlayer(targetNetworkObject.Id, finalDamage);
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void ShowMissEffectRPC(Vector3 position)
{
DamagePopup.Create(position + Vector3.up, 0f, DamagePopup.DamageType.Miss);
}
private float CalculateDamage()
{
float damage = baseDamage;
damage *= NetworkDebuffData.GetCurrentDamageMultiplier(Time.time);
return damage;
}
public void TakeDamageFromServer(float damage, PlayerRef attacker, bool isCritical = false)
{
if (!Object.HasStateAuthority || isDead) return;
if (Runner == null || !Runner.IsRunning)
return;
try
{
monsterUI?.TriggerHitEffect(isCritical);
float damageReduction = baseDefense / (100f + baseDefense);
float finalDamage = damage * (1f - damageReduction);
if (isCritical)
finalDamage *= 1.5f;
float calculatedHealth = currentHealth - finalDamage;
currentHealth = Mathf.Max(0, calculatedHealth);
NetworkCoreData.SetHealth(currentHealth, maxHealth);
RPC_UpdateHealth(currentHealth);
if (!isAggressive && attacker != PlayerRef.None)
{
GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
foreach (var player in players)
{
var networkObj = player.GetComponent<NetworkObject>();
if (networkObj != null && networkObj.InputAuthority == attacker)
{
targetPlayer = player.transform;
targetPlayerStats = player.GetComponent<PlayerStats>();
if (calculatedHealth > 0 && currentState != MonsterState.Run && currentState != MonsterState.Ready)
ChangeMonsterState(MonsterState.Run);
break;
}
}
}
if (calculatedHealth <= 0 && !isDead)
{
lastKillerActorNumber = attacker != PlayerRef.None ? attacker.PlayerId : 0;
Die(attacker);
}
}
catch (System.Exception) { }
}
private void Die(PlayerRef killer)
{
if (isDead) return;
try
{
isDead = true;
currentHealth = 0;
if (Object.HasStateAuthority)
{
NetworkCoreData.SetHealth(0f, maxHealth);
NetworkCoreData.SetState(MonsterState.Death);
if (GetComponent<Collider2D>() != null)
GetComponent<Collider2D>().enabled = false;
if (rb != null)
{
rb.bodyType = RigidbodyType2D.Kinematic;
rb.linearVelocity = Vector2.zero;
rb.angularVelocity = 0f;
}
if (fantasyMonster != null)
fantasyMonster.SetState(MonsterState.Death);
RPC_SyncDeathState(killer);
MonsterSpawner spawner = FindAnyObjectByType<MonsterSpawner>();
if (spawner != null)
spawner.UnregisterMonster(Object.Id);
StartCoroutine(DestroyAfterDelay());
}
}
catch (Exception) { }
}
private void OnDeathAnimationComplete()
{
if (!Object.HasStateAuthority) return;
try
{
if (!isDead)
{
isDead = true;
var collider = GetComponent<Collider2D>();
if (collider != null) collider.enabled = false;
if (rb != null)
rb.bodyType = RigidbodyType2D.Kinematic;
}
}
catch (System.Exception) { }
}
public void SetKnockbackState(bool isKnockedBack)
{
if (!Object.HasStateAuthority) return;
NetworkCombatData.SetKnockback(isKnockedBack);
}
public void ApplyAccuracyDebuff(float reductionPercent, float duration)
{
if (!Object.HasStateAuthority) return;
float multiplier = 1f - (reductionPercent / 100f);
NetworkDebuffData.SetAccuracyDebuff(multiplier, Time.time + duration);
RPC_SyncAccuracyDebuff(multiplier, duration);
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_SyncAccuracyDebuff(float multiplier, float duration)
{
if (accuracyDebuffCoroutine != null)
StopCoroutine(accuracyDebuffCoroutine);
accuracyDebuffCoroutine = StartCoroutine(AccuracyDebuffCoroutine(duration));
}
private IEnumerator AccuracyDebuffCoroutine(float duration)
{
yield return new WaitForSeconds(duration);
if (Object != null && Object.IsValid && Object.HasStateAuthority)
NetworkDebuffData.SetAccuracyDebuff(1f, 0f);
accuracyDebuffCoroutine = null;
}
public bool RollAccuracyCheck()
{
if (!Runner.IsServer) return true;
float accuracyChance = CurrentAccuracy;
float roll = Random.value;
return roll <= accuracyChance;
}
public void ApplySlowDebuff(float slowPercent, float duration)
{
if (!Object.HasStateAuthority) return;
float multiplier = 1f - (slowPercent / 100f);
NetworkDebuffData.SetSlowDebuff(multiplier, Time.time + duration);
RPC_SyncSlowDebuff(multiplier, duration);
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_SyncSlowDebuff(float multiplier, float duration)
{
if (slowDebuffCoroutine != null)
StopCoroutine(slowDebuffCoroutine);
slowDebuffCoroutine = StartCoroutine(SlowDebuffCoroutine(duration));
}
private IEnumerator SlowDebuffCoroutine(float duration)
{
yield return new WaitForSeconds(duration);
if (Object != null && Object.IsValid && Object.HasStateAuthority)
NetworkDebuffData.SetSlowDebuff(1f, 0f);
slowDebuffCoroutine = null;
}
private void UpdateNightAggressiveness()
{
if (!nightAggressive || !Runner.IsServer) return;
GlobalLightController controller = GlobalLightController.GetInstance();
if (controller == null) return;
bool isCurrentlyNight = GlobalLightController.IsNight;
if (isCurrentlyNight != wasNightLastCheck)
{
wasNightLastCheck = isCurrentlyNight;
isAggressive = !isCurrentlyNight;
if (!isCurrentlyNight && targetPlayer != null)
{
ClearTarget();
ChangeMonsterState(MonsterState.Walk);
}
}
}
private bool GetEffectiveAggressiveness()
{
return isAggressive;
}
public void ApplyDamageDebuff(float reductionPercent, float duration)
{
if (!Object.HasStateAuthority) return;
float multiplier = 1f - (reductionPercent / 100f);
NetworkDebuffData.SetDamageDebuff(multiplier, Time.time + duration);
RPC_SyncDamageDebuff(multiplier, duration);
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_SyncDamageDebuff(float multiplier, float duration)
{
if (damageDebuffCoroutine != null)
StopCoroutine(damageDebuffCoroutine);
damageDebuffCoroutine = StartCoroutine(DamageDebuffCoroutine(duration));
}
private IEnumerator DamageDebuffCoroutine(float duration)
{
yield return new WaitForSeconds(duration);
if (Object != null && Object.IsValid && Object.HasStateAuthority)
NetworkDebuffData.SetDamageDebuff(1f, 0f);
damageDebuffCoroutine = null;
}
public void ManagedRender(int distanceTier, int currentFrame)
{
if (!isSpawned || Object == null || !Object.IsValid) return;
if (!isVisibleToLocalPlayer)
{
if (Runner.IsServer && currentFrame % 30 == 0)
PerformServerOnlyUpdates();
return;
}
UpdateRotation();
switch (distanceTier)
{
case 0: PerformFullRender(); break;
case 1: if (currentFrame % 2 == 0) PerformReducedRender(); break;
}
}
private void PerformReducedRender() => UpdateNetworkChanges();
private void PerformServerOnlyUpdates()
{
if (!Runner.IsServer) return;
if (_changeDetector != null)
{
foreach (var propertyName in _changeDetector.DetectChanges(this))
{
switch (propertyName)
{
case nameof(NetworkCoreData):
if (NetworkCoreData.IsDead && !isDead)
{
isDead = true;
if (fantasyMonster != null)
fantasyMonster.SetState(MonsterState.Death);
}
break;
}
}
}
}
private void PerformFullRender() => UpdateNetworkChanges();
private void UpdateRotation()
{
if (bodyTransform == null) return;
// NetworkedRotationY: 0 = sola bakıyor, 180 = sağa bakıyor
// localScale.x: 1 = sola bakıyor, -1 = sağa bakıyor
float targetScaleX = NetworkedRotationY > 90f ? -1f : 1f;
Vector3 scale = bodyTransform.localScale;
if (Mathf.Abs(scale.x - targetScaleX) > 0.01f)
{
scale.x = targetScaleX;
bodyTransform.localScale = scale;
}
}
private void UpdateNetworkChanges()
{
if (_changeDetector != null)
{
foreach (var propertyName in _changeDetector.DetectChanges(this))
{
switch (propertyName)
{
case nameof(NetworkCoreData):
if (Mathf.Abs(currentHealth - NetworkCoreData.Health) > 0.5f)
{
currentHealth = NetworkCoreData.Health;
monsterUI?.UpdateHealthUI(currentHealth, NetworkCoreData.MaxHealth);
}
if (currentState != NetworkCoreData.GetState())
{
currentState = NetworkCoreData.GetState();
if (fantasyMonster != null)
{
fantasyMonster.SetState(currentState);
monsterUI?.UpdateHeadSprite(currentState);
}
}
if (NetworkCoreData.IsDead && !isDead)
{
isDead = true;
if (fantasyMonster != null)
{
fantasyMonster.SetState(MonsterState.Death);
monsterUI?.UpdateHeadSprite(MonsterState.Death);
}
}
break;
}
}
}
}
public override void Render()
{
if (!Runner.IsServer) return;
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_SyncMonsterState(int stateInt, bool forceUpdate = false)
{
try
{
MonsterState newState = (MonsterState)stateInt;
if (forceUpdate)
{
currentState = newState;
if (fantasyMonster != null)
{
monsterUI?.UpdateHeadSprite(newState);
fantasyMonster.SetState(newState);
}
return;
}
if (currentState != newState)
{
currentState = newState;
if (fantasyMonster != null)
{
monsterUI?.UpdateHeadSprite(newState);
fantasyMonster.SetState(newState);
}
}
}
catch (System.Exception) { }
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_PlayAttackAnimation()
{
try
{
if (fantasyMonster != null)
fantasyMonster.Attack();
}
catch (System.Exception) { }
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_UpdateHealth(float newHealth)
{
if (isDead) return;
currentHealth = Mathf.Clamp(newHealth, 0, maxHealth);
NetworkCoreData.SetHealth(currentHealth, maxHealth);
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_SyncDeathState(PlayerRef killerRef)
{
try
{
if (fantasyMonster != null)
{
currentState = MonsterState.Death;
monsterUI?.UpdateHeadSprite(MonsterState.Death);
fantasyMonster.SetState(MonsterState.Death);
}
try
{
if (!string.IsNullOrEmpty(monsterType))
RPC_GiveXPToKiller(killerRef, xpValue);
}
catch (Exception) { }
try
{
if (!string.IsNullOrEmpty(monsterType) && QuestManager.Instance != null)
QuestManager.Instance.HandleMonsterDeath(monsterType, transform.position, killerRef);
}
catch (Exception) { }
try
{
if (Object.HasStateAuthority && lootSystem != null)
lootSystem.HandleLootDrop(killerRef, transform.position);
}
catch (Exception) { }
try
{
if (NetworkCombatData.TargetingPlayer != PlayerRef.None && Object.HasInputAuthority && uiManager != null)
uiManager.HideTargetInfo();
}
catch (Exception) { }
}
catch (Exception) { }
}
[Rpc(RpcSources.All, RpcTargets.All)]
private void RPC_DealDamageToPlayer(NetworkId targetObjectId, float damageAmount)
{
var targetObject = Runner.FindObject(targetObjectId);
if (targetObject != null)
{
PlayerStats targetStats = targetObject.GetComponent<PlayerStats>();
if (targetStats != null)
{
targetStats.TakeDamage(damageAmount);
DamagePopup.Create(targetObject.transform.position + Vector3.up,
damageAmount, DamagePopup.DamageType.Received);
}
}
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_GiveXPToKiller(PlayerRef killerRef, float xpAmount)
{
try
{
if (Runner.LocalPlayer != killerRef) return;
GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
foreach (GameObject player in players)
{
NetworkObject netObj = player.GetComponent<NetworkObject>();
if (netObj != null && netObj.HasInputAuthority)
{
PlayerStats playerStats = player.GetComponent<PlayerStats>();
if (playerStats != null)
{
playerStats.GainXP(xpAmount);
return;
}
}
}
}
catch (Exception) { }
}
public void SyncStateToNewClient(PlayerRef newClient)
{
if (!Object.HasStateAuthority) return;
try
{
byte currentAnimationByte = fantasyMonster != null ?
AnimationMapping.GetAnimationByte(fantasyMonster.GetCurrentAnimation()) :
AnimationMapping.NONE_ANIMATION;
RPC_SyncStateToNewClient(
newClient,
(int)currentState,
transform.position,
transform.eulerAngles.y,
currentHealth,
maxHealth,
currentAnimationByte
);
}
catch (System.Exception) { }
}
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_SyncStateToNewClient(
PlayerRef targetClient,
int stateInt,
Vector2 position,
float yRotation,
float health,
float maxHp,
byte currentAnimationByte)
{
if (Runner.LocalPlayer != targetClient) return;
try
{
currentState = (MonsterState)stateInt;
currentHealth = health;
maxHealth = maxHp;
transform.position = position;
if (bodyTransform != null)
{
float targetScaleX = yRotation > 90f ? -1f : 1f;
Vector3 scale = bodyTransform.localScale;
scale.x = targetScaleX;
bodyTransform.localScale = scale;
}
if (fantasyMonster != null)
{
string currentAnimation = AnimationMapping.GetAnimationString(currentAnimationByte);
if (!string.IsNullOrEmpty(currentAnimation))
fantasyMonster.SyncAnimation(currentAnimation);
else
fantasyMonster.SetState(currentState);
monsterUI?.UpdateHeadSprite(currentState);
}
monsterUI?.UpdateHealthUI(currentHealth, maxHealth);
}
catch (System.Exception) { }
}
[Rpc(RpcSources.All, RpcTargets.All)]
private void RPC_SyncMonsterProperties(byte monsterTypeByte, int rarityInt, float[] colorValues, float combatMult, float speedMult, float sizeMult, int level)
{
monsterType = MonsterTypeMapping.GetMonsterTypeString(monsterTypeByte);
monsterRarity = (MonsterRarity)rarityInt;
monsterLevel = level;
combatMultiplier = combatMult;
speedMultiplier = speedMult;
sizeMultiplier = sizeMult;
baseDamage = baseBaseDamage * combatMultiplier;
maxHealth = baseMaxHealth * combatMultiplier;
currentHealth = maxHealth;
baseDefense = baseBaseDefense * combatMultiplier;
moveSpeed = baseMoveSpeed * speedMultiplier;
Vector3 finalScale = baseScale * sizeMultiplier;
if (finalScale.magnitude < 0.1f)
finalScale = Vector3.one * sizeMultiplier;
transform.localScale = finalScale;
Color syncColor = new Color(colorValues[0], colorValues[1], colorValues[2], colorValues[3]);
if (monsterUI != null)
monsterUI.ApplyColorToAllSprites(syncColor);
xpValue = baseXpValue * combatMultiplier;
if (!Object.HasStateAuthority)
NetworkCoreData.Initialize(monsterType, (MonsterRarity)rarityInt, maxHealth, level);
monsterUI?.UpdateHealthUI(currentHealth, maxHealth);
}
private void CheckStuckState()
{
if (!Object.HasStateAuthority || !Runner.IsServer) return;
stuckCheckTimer += Runner.DeltaTime;
if (stuckCheckTimer >= STUCK_CHECK_INTERVAL)
{
float distanceMoved = Vector2.Distance(transform.position, lastPosition);
if (distanceMoved < STUCK_DISTANCE_THRESHOLD &&
(currentState == MonsterState.Walk || currentState == MonsterState.Run) &&
rb != null)
{
Vector2 escapeDirection = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
rb.linearVelocity = escapeDirection * GetCurrentMoveSpeed() * 1.5f;
UpdateMonsterDirection(escapeDirection);
SetNewPatrolTarget();
}
lastPosition = transform.position;
stuckCheckTimer = 0f;
}
}
public override void FixedUpdateNetwork()
{
if (!Object.HasStateAuthority || !Runner.IsServer || isDead || !isInitialized) return;
frameCounter++;
if (frameCounter % 10 != 0) return;
Vector2 currentVelocity = rb != null ? rb.linearVelocity : Vector2.zero;
bool needsCollisionCheck = (currentState == MonsterState.Walk || currentState == MonsterState.Run) &&
rb != null &&
currentVelocity.magnitude > 0.5f;
if (needsCollisionCheck)
CheckAndAvoidCollisions();
if (frameCounter % 30 == 0)
{
CheckStuckState();
UpdateNightAggressiveness();
}
CheckAndUpdateRotation();
if (NetworkCombatData.IsAttacking)
{
bool shouldEndAttack = false;
if (!NetworkCombatData.IsCurrentlyAttacking(Runner.SimulationTime))
shouldEndAttack = true;
if (fantasyMonster != null)
{
string currentAnim = fantasyMonster.GetCurrentAnimation();
string networkAnim = NetworkCombatData.GetCurrentAnimation();
if (!string.IsNullOrEmpty(networkAnim) &&
currentAnim != networkAnim &&
currentAnim != "Attack")
{
shouldEndAttack = true;
}
}
if (shouldEndAttack)
NetworkCombatData.EndAttack();
}
UpdateAIState();
NetworkCoreData.SetHealth(currentHealth, maxHealth);
NetworkCoreData.SetState(currentState);
NetworkedPosition = transform.position;
if (Object.HasStateAuthority)
{
NetworkCombatData.ClearExpiredAttack(Runner.SimulationTime);
NetworkDebuffData.ClearExpiredDebuffs(Time.time);
CheckAndUpdateRotation();
}
}
private void CheckAndUpdateRotation()
{
Vector2 velocity = rb != null ? rb.linearVelocity : Vector2.zero;
if (velocity.magnitude < 0.5f) return;
bool isMovingRight = velocity.x > 0f;
if (isMovingRight != lastDirectionRight)
{
lastDirectionRight = isMovingRight;
NetworkedRotationY = isMovingRight ? 180f : 0f;
}
}
private void OnCollisionEnter2D(Collision2D collision)
{
if (!Object.HasStateAuthority || isDead || rb == null) return;
if (!Runner.IsServer) return;
if (collision.gameObject.layer == 8)
return;
if (IsValidTarget(collision.collider))
return;
Vector2 normal = collision.contacts[0].normal;
Vector2 currentVelocity = rb.linearVelocity.normalized;
if (currentVelocity.magnitude < 0.1f)
{
float randomAngle = Random.Range(0f, 360f);
Vector2 randomDirection = new Vector2(
Mathf.Cos(randomAngle * Mathf.Deg2Rad),
Mathf.Sin(randomAngle * Mathf.Deg2Rad)
);
rb.linearVelocity = randomDirection * GetCurrentMoveSpeed();
UpdateMonsterDirection(randomDirection);
}
else
{
Vector2 reflectDirection = Vector2.Reflect(currentVelocity, normal);
float randomAngle = Random.Range(-45f, 45f);
reflectDirection = Quaternion.Euler(0, 0, randomAngle) * reflectDirection;
rb.linearVelocity = reflectDirection * GetCurrentMoveSpeed();
UpdateMonsterDirection(reflectDirection);
}
if (currentState == MonsterState.Walk)
Invoke("SetNewPatrolTarget", 0.3f);
}
private IEnumerator DestroyAfterDelay()
{
yield return new WaitForSeconds(2f);
if (Object.HasStateAuthority && gameObject != null)
Runner.Despawn(Object);
}
public void ApplyTaunt(GameObject taunter, float duration)
{
if (!Object.HasStateAuthority) return;
var taunterNetObj = taunter.GetComponent<NetworkObject>();
if (taunterNetObj != null)
{
NetworkTauntData.Set(Time.time + duration, taunterNetObj.Id);
currentTaunter = taunter;
}
}
public bool IsTaunted()
{
if (!NetworkTauntData.IsActive(Time.time))
{
if (NetworkTauntData.IsTaunted)
{
NetworkTauntData.Clear();
currentTaunter = null;
}
return false;
}
return NetworkTauntData.IsTaunted;
}
public GameObject GetTaunter()
{
if (!IsTaunted()) return null;
if (currentTaunter != null) return currentTaunter;
if (Runner != null)
{
NetworkObject taunterObj = Runner.FindObject(NetworkTauntData.TaunterId);
if (taunterObj != null)
{
currentTaunter = taunterObj.gameObject;
return currentTaunter;
}
}
return null;
}
}
