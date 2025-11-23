using UnityEngine;
using Fusion;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Random = UnityEngine.Random;
using System.Collections.Generic;

public class WeaponSystem : NetworkBehaviour
{
    #region CONFIGURATION
    
    [Header("Core References")]
    private PlayerStats playerStats;
    private Character4D character4D;
    private Animator animator;
    private GameObject projectilePrefab;
    [SerializeField] private Canvas combatCanvas;
    
    [Header("Attack Ranges")]
    [SerializeField] private float meleeAttackRange = 4f;
    [SerializeField] private float rangedAttackRange = 8f;
    
    [Header("UI")]
    public UIManager uiManager;
    
    [Header("Performance")]
    [SerializeField] private int maxTargetsToCheck = 15;
    
    public event System.Action<PlayerStats.WeaponType> OnWeaponChanged;
    
    #endregion

    #region STATE
    
    private bool isMeleeAttacking = false;
    private bool isRangedAttacking = false;
    private bool isAttackButtonHeld = false;
    
    private float lastAttackTime = 0f;
    private float nextAttackTime = 0f;
    private float meleeAnimEndTime = 0f;
    private float rangedAnimEndTime = 0f;
    private float attackButtonHoldStartTime = 0f;
    private float lastAutoAttackTime = 0f;
    
    private const float HOLD_THRESHOLD = 0.5f;
    private const float meleeAttackAnimDuration = 0.5f;
    
    private PlayerStats.WeaponType currentWeaponType = PlayerStats.WeaponType.Melee;
    private GameObject currentTarget = null;
    
    private List<GameObject> nearbyMonsters = new List<GameObject>();
    private List<GameObject> nearbyPlayers = new List<GameObject>();
    private float lastSpatialUpdate = 0f;
    private const float SPATIAL_UPDATE_INTERVAL = 1f;
    private Vector2 lastPlayerPosition;
    private float positionChangeThreshold = 3f;
    
    private GameObject lastKnownTarget;
    private float lastTargetDistance;
    private float lastTargetCheck = 0f;
    private const float TARGET_RECHECK_INTERVAL = 0.5f;
    
    private Vector2 cachedPlayerPosition;
    private Vector2 cachedTargetPosition;
    private bool targetPositionCached = false;
    
    private int frameCounter = 0;
    private const int TARGETING_FRAME_SKIP = 30;
    
    #endregion

    #region PROPERTIES
    
    public bool IsMeleeAttacking => isMeleeAttacking;
    public bool IsRangedAttacking => isRangedAttacking;
    public PlayerStats.WeaponType CurrentWeaponType => currentWeaponType;
    public float MeleeAttackRange => meleeAttackRange;
    public float RangedAttackRange => rangedAttackRange;
    
    public GameObject CurrentTarget
    {
        get => currentTarget;
        private set
        {
            if (currentTarget != value)
            {
                if (currentTarget != null)
                {
                    MonsterBehaviour oldMonster = currentTarget.GetComponent<MonsterBehaviour>();
                    if (oldMonster != null) oldMonster.SetTargetingPlayer(false, PlayerRef.None);
                }

                currentTarget = value;

                if (currentTarget != null)
                {
                    MonsterBehaviour newMonster = currentTarget.GetComponent<MonsterBehaviour>();
                    if (newMonster != null) newMonster.SetTargetingPlayer(true, Object.InputAuthority);
                }
            }
        }
    }
    
    #endregion

    #region LIFECYCLE

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        animator = GetComponent<Animator>();
    }

    private void Start()
    {
        character4D = GetComponent<Character4D>();
        if (projectilePrefab == null)
        {
            projectilePrefab = Resources.Load<GameObject>("ProjectilePrefab");
            if (projectilePrefab == null) Debug.LogError("ProjectilePrefab not found in Resources!");
        }
    }

    public override void Spawned()
    {
        base.Spawned();
        if (!Object.HasInputAuthority) return;
        if (projectilePrefab == null) projectilePrefab = Resources.Load<GameObject>("ProjectilePrefab");
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasInputAuthority) return;

        UpdateAttackStates();

        if (frameCounter % 60 == 0)
        {
            if (currentTarget != null && Time.time - lastAttackTime > 3f) ClearTarget();
        }

        if (isAttackButtonHeld && Time.time - attackButtonHoldStartTime > HOLD_THRESHOLD)
        {
            bool isInAttackMode = true;
            if (CombatInitializer.Instance != null)
            {
                if (CombatInitializer.Instance.isNearItems || CombatInitializer.Instance.isNearNPC)
                    isInAttackMode = false;
            }

            if (isInAttackMode && Time.time >= lastAutoAttackTime + (1f / playerStats.FinalAttackSpeed))
            {
                TryAttackOptimized();
                lastAutoAttackTime = Time.time;
            }
        }
    }

    #endregion

    #region INPUT

    public void OnAttackButtonDown()
    {
        isAttackButtonHeld = true;
        attackButtonHoldStartTime = Time.time;
        InvalidatePositionCache();

        if (Time.time < nextAttackTime) return;

        if (lastKnownTarget != null && Time.time - lastTargetCheck < TARGET_RECHECK_INTERVAL)
        {
            if (IsTargetStillValidFast())
            {
                currentTarget = lastKnownTarget;
                AttackCurrentTarget();
                return;
            }
        }

        TryAttackImmediate();
    }

    public void OnAttackButtonUp()
    {
        isAttackButtonHeld = false;
    }

    public void ForceStopAttack()
    {
        isAttackButtonHeld = false;
        ClearTarget();
        InvalidatePositionCache();
    }

    #endregion

    #region ATTACK

    public void TryAttack()
    {
        TryAttackOptimized();
    }

    public void TryAttackOptimized()
    {
        if (Time.time < nextAttackTime) return;

        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats != null && stats.IsDead) return;

        if (lastKnownTarget != null && Time.time - lastTargetCheck < TARGET_RECHECK_INTERVAL)
        {
            if (IsTargetStillValidFast())
            {
                AttackCurrentTarget();
                return;
            }
            else
            {
                lastKnownTarget = null;
            }
        }

        frameCounter++;
        if (frameCounter % TARGETING_FRAME_SKIP != 0) return;

        UpdateSpatialCacheIfNeeded();

        GameObject nearestTarget = FindNearestTargetSpatial();
        if (nearestTarget == null || !IsInAttackRangeCached(nearestTarget))
        {
            ClearTarget();
            return;
        }

        lastKnownTarget = nearestTarget;
        currentTarget = nearestTarget;
        lastTargetCheck = Time.time;
        AttackCurrentTarget();
    }

    private void TryAttackImmediate()
    {
        if (Time.time < nextAttackTime) return;

        PlayerStats stats = GetComponent<PlayerStats>();
        if (stats != null && stats.IsDead) return;

        UpdateSpatialCache();

        GameObject nearestTarget = FindNearestTargetSpatial();
        if (nearestTarget == null || !IsInAttackRangeCached(nearestTarget))
        {
            ClearTarget();
            return;
        }

        lastKnownTarget = nearestTarget;
        currentTarget = nearestTarget;
        lastTargetCheck = Time.time;
        AttackCurrentTarget();
    }

    private void AttackCurrentTarget()
    {
        lastAttackTime = Time.time;
        nextAttackTime = Time.time + (1f / playerStats.FinalAttackSpeed);

        NetworkObject targetNetObj = currentTarget.GetComponent<NetworkObject>();
        if (targetNetObj == null) return;

        NetworkId targetNetworkId = targetNetObj.Id;
        Vector3 targetPosition = targetPositionCached ? cachedTargetPosition : currentTarget.transform.position;

        if (currentTarget.CompareTag("Monster"))
        {
            AttackMonsterWithTarget(targetNetworkId, targetPosition);
        }
        else if (currentTarget.CompareTag("Player"))
        {
            AttackPlayerWithTarget(targetNetworkId, targetPosition);
        }
    }

    private void AttackMonsterWithTarget(NetworkId targetId, Vector3 targetPosition)
    {
        SyncWeaponVisibilityRPC((int)currentWeaponType);

        var character = GetComponent<Character4D>();
        if (character != null)
        {
            if (currentWeaponType == PlayerStats.WeaponType.Melee)
            {
                SyncMeleeAnimationRPC(transform.position, targetPosition, (int)currentWeaponType);
                RequestMeleeAttackRPC(targetId);
            }
            else
            {
                character.AnimationManager.ShotBow();
                SpawnProjectileToTarget(targetId, targetPosition);
            }
        }
    }

    private void AttackPlayerWithTarget(NetworkId targetId, Vector3 targetPosition)
    {
        PVPSystem pvpSystem = GetComponent<PVPSystem>();
        if (pvpSystem != null) pvpSystem.AttackSpecificTargetById(targetId, targetPosition);
    }

    #endregion

    #region TARGETING

    private void UpdateSpatialCacheIfNeeded()
    {
        Vector2 currentPos = transform.position;
        float positionChange = Vector2.Distance(currentPos, lastPlayerPosition);
        
        bool needsUpdate = positionChange > positionChangeThreshold || Time.time - lastSpatialUpdate > SPATIAL_UPDATE_INTERVAL;
        
        if (needsUpdate)
        {
            UpdateSpatialCache();
            lastSpatialUpdate = Time.time;
            lastPlayerPosition = currentPos;
        }
    }

    private void UpdateSpatialCache()
    {
        nearbyMonsters.Clear();
        nearbyPlayers.Clear();

        if (MonsterManager.Instance == null) return;

        float searchRadius = (currentWeaponType == PlayerStats.WeaponType.Melee ? meleeAttackRange : rangedAttackRange) * 2f;

        var visibleMonsters = MonsterManager.Instance.GetMonstersInRadius(transform.position, searchRadius);
        int checkCount = Mathf.Min(visibleMonsters.Count, maxTargetsToCheck);

        for (int i = 0; i < checkCount; i++)
        {
            if (visibleMonsters[i] != null && !visibleMonsters[i].IsDead && visibleMonsters[i].IsVisibleToLocalPlayer)
            {
                nearbyMonsters.Add(visibleMonsters[i].gameObject);
            }
        }

        PVPSystem myPVP = GetComponent<PVPSystem>();
        if (myPVP != null && myPVP.localPVPStatus)
        {
            GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
            
            foreach (GameObject player in allPlayers)
            {
                if (player == gameObject) continue;
                
                NetworkObject netObj = player.GetComponent<NetworkObject>();
                if (netObj == null || !netObj.IsValid) continue;
                
                PVPSystem otherPVP = player.GetComponent<PVPSystem>();
                if (otherPVP == null) continue;
                
                bool otherInPVP = otherPVP.localPVPStatus || otherPVP.IsInPVPZone;
                if (!otherInPVP) continue;
                
                DeathSystem deathSys = player.GetComponent<DeathSystem>();
                if (deathSys != null && deathSys.IsDead) continue;
                
                float distance = Vector2.Distance(transform.position, player.transform.position);
                if (distance <= searchRadius) nearbyPlayers.Add(player);
            }
        }
    }

    private GameObject FindNearestTargetSpatial()
    {
        GameObject nearestTarget = null;
        float minDistanceSqr = float.MaxValue;
        float attackRange = currentWeaponType == PlayerStats.WeaponType.Melee ? meleeAttackRange : rangedAttackRange;
        float attackRangeSqr = attackRange * attackRange;
        Vector2 playerPos = transform.position;

        foreach (GameObject monster in nearbyMonsters)
        {
            if (monster == null) continue;

            MonsterBehaviour monsterBehaviour = monster.GetComponent<MonsterBehaviour>();
            if (monsterBehaviour == null || monsterBehaviour.IsDead || !monsterBehaviour.IsVisibleToLocalPlayer) continue;

            Vector2 monsterPos = monster.transform.position;
            float distanceSqr = (playerPos - monsterPos).sqrMagnitude;

            if (distanceSqr <= attackRangeSqr && distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                nearestTarget = monster;
            }
        }

        foreach (GameObject player in nearbyPlayers)
        {
            if (player == null) continue;
            
            DeathSystem deathSys = player.GetComponent<DeathSystem>();
            if (deathSys != null && deathSys.IsDead) continue;
            
            Vector2 otherPlayerPos = player.transform.position;
            float distanceSqr = (playerPos - otherPlayerPos).sqrMagnitude;

            if (distanceSqr <= attackRangeSqr && distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                nearestTarget = player;
            }
        }

        return nearestTarget;
    }

    public void ClearTarget()
    {
        if (currentTarget != null)
        {
            var MonsterBehaviour = currentTarget.GetComponent<MonsterBehaviour>();
            if (MonsterBehaviour != null) MonsterBehaviour.SetTargetingPlayer(false, PlayerRef.None);
            currentTarget = null;
        }
        
        lastKnownTarget = null;
        targetPositionCached = false;
        lastTargetCheck = 0f;
        nearbyPlayers.Clear();

        if (uiManager != null) uiManager.HideTargetInfo();
    }

    private bool IsTargetStillValidFast()
    {
        if (lastKnownTarget == null) return false;
        
        if (lastKnownTarget.CompareTag("Monster"))
        {
            MonsterBehaviour monster = lastKnownTarget.GetComponent<MonsterBehaviour>();
            if (monster == null || monster.IsDead) return false;
        }
        
        if (targetPositionCached)
        {
            float distanceSqr = (cachedPlayerPosition - cachedTargetPosition).sqrMagnitude;
            float attackRange = currentWeaponType == PlayerStats.WeaponType.Melee ? meleeAttackRange : rangedAttackRange;
            float attackRangeSqr = attackRange * attackRange;
            
            return distanceSqr <= attackRangeSqr;
        }
        
        return true;
    }

    private bool IsInAttackRangeCached(GameObject target)
    {
        if (target == null) return false;
        
        Vector2 playerPos = transform.position;
        Vector2 targetPos = target.transform.position;
        
        cachedPlayerPosition = playerPos;
        cachedTargetPosition = targetPos;
        targetPositionCached = true;
        
        float attackRange = currentWeaponType == PlayerStats.WeaponType.Melee ? meleeAttackRange : rangedAttackRange;
        float distanceSqr = (playerPos - targetPos).sqrMagnitude;
        float attackRangeSqr = attackRange * attackRange;
        
        lastTargetDistance = Mathf.Sqrt(distanceSqr);
        
        return distanceSqr <= attackRangeSqr;
    }

    private void InvalidatePositionCache()
    {
        targetPositionCached = false;
    }

    #endregion

    #region MELEE

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestMeleeAttackRPC(NetworkId targetMonsterId)
    {
        if (!Runner.IsServer) return;

        NetworkObject targetObject = Runner.FindObject(targetMonsterId);
        if (targetObject == null) return;

        MonsterBehaviour monster = targetObject.GetComponent<MonsterBehaviour>();
        if (monster == null || monster.IsDead) return;

        float distance = Vector2.Distance(transform.position, targetObject.transform.position);
        if (distance > meleeAttackRange) return;

        Vector2 direction = targetObject.transform.position - transform.position;
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance, LayerMask.GetMask("Obstacles"));
        if (hit.collider != null) return;

        Vector3 hitPosition = targetObject.transform.position;
        Collider2D monsterCollider = targetObject.GetComponent<Collider2D>();
        if (monsterCollider != null) hitPosition = monsterCollider.bounds.center;

        PlayerStats attackerStats = GetComponent<PlayerStats>();
        if (attackerStats != null && !attackerStats.RollAccuracyCheck())
        {
            ShowMeleeHitEffectRPC(hitPosition, false);
            ShowDamagePopupRPC(hitPosition, 0f, (int)DamagePopup.DamageType.Miss);
            return;
        }

        bool isCritical = IsCriticalHit();
        float damage = playerStats.GetDamageAmount(isCritical);

        monster.TakeDamageFromServer(damage, Object.InputAuthority, isCritical);
        ShowMeleeHitEffectRPC(hitPosition, isCritical);
        ShowDamagePopupRPC(hitPosition, damage, isCritical ? (int)DamagePopup.DamageType.Critical : (int)DamagePopup.DamageType.Normal);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ShowMeleeHitEffectRPC(Vector3 position, bool isCritical)
    {
        ShowSimpleMeleeHitEffect(position, isCritical);
    }

    private void ShowSimpleMeleeHitEffect(Vector3 position, bool isCritical)
    {
        GameObject impactPrefab = Resources.Load<GameObject>("Impact_Cut");
        if (impactPrefab == null)
        {
            Debug.LogError("[WeaponSystem] Impact_Cut prefab Resources klasöründe bulunamadı!");
            return;
        }

        GameObject hitEffect = Instantiate(impactPrefab, position, Quaternion.identity);
        Destroy(hitEffect, 2f);
    }

    #endregion

    #region RANGED

    public void SpawnProjectile(Vector3 targetPos)
    {
        if (projectilePrefab == null || !Object.HasInputAuthority) return;

        SyncRangedAnimationRPC(transform.position, targetPos);

        Sprite arrowSprite = null;
        int arrowSpriteIndex = -1;
        if (character4D != null)
        {
            Character currentCharacter = character4D.Active;
            if (currentCharacter != null && currentCharacter.CompositeWeapon != null && currentCharacter.CompositeWeapon.Count > 0)
            {
                arrowSpriteIndex = 0;
                arrowSprite = currentCharacter.CompositeWeapon[0];
            }
        }

        bool isCritical = IsCriticalHit();
        float damage = playerStats.GetDamageAmount(isCritical);
        NetworkId targetNetworkId = FindTargetNetworkId(targetPos);

        CreateLocalProjectile(transform.position + Vector3.up * 0.5f, targetPos, arrowSprite);
        RequestProjectileSpawnRPC(transform.position, targetPos, damage, isCritical, arrowSpriteIndex, targetNetworkId);
    }

    private NetworkId FindTargetNetworkId(Vector3 targetPos)
    {
        if (MonsterManager.Instance == null) return default(NetworkId);

        var nearbyMonsterBehaviours = MonsterManager.Instance.GetMonstersInRadius(targetPos, 2f);

        foreach (MonsterBehaviour monsterBehaviour in nearbyMonsterBehaviours)
        {
            if (monsterBehaviour != null && !monsterBehaviour.IsDead && monsterBehaviour.IsVisibleToLocalPlayer)
            {
                NetworkObject netObj = monsterBehaviour.GetComponent<NetworkObject>();
                if (netObj != null) return netObj.Id;
            }
        }
        return default(NetworkId);
    }

    public void SpawnProjectileToTarget(NetworkId targetId, Vector3 targetPosition)
    {
        if (projectilePrefab == null || !Object.HasInputAuthority) return;

        SyncRangedAnimationRPC(transform.position, targetPosition);

        Sprite arrowSprite = null;
        int arrowSpriteIndex = -1;
        if (character4D != null)
        {
            Character currentCharacter = character4D.Active;
            if (currentCharacter != null && currentCharacter.CompositeWeapon != null && currentCharacter.CompositeWeapon.Count > 0)
            {
                arrowSpriteIndex = 0;
                arrowSprite = currentCharacter.CompositeWeapon[0];
            }
        }

        bool isCritical = IsCriticalHit();
        float damage = playerStats.GetDamageAmount(isCritical);

        CreateLocalProjectile(transform.position + Vector3.up * 0.5f, targetPosition, arrowSprite);
        RequestProjectileSpawnToTargetRPC(transform.position, targetPosition, damage, isCritical, arrowSpriteIndex, targetId);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestProjectileSpawnRPC(Vector3 spawnPos, Vector3 targetPos, float damage, bool isCritical, int arrowSpriteIndex, NetworkId targetId = default(NetworkId))
    {
        if (!Runner.IsServer) return;
        if (projectilePrefab == null) projectilePrefab = Resources.Load<GameObject>("ProjectilePrefab");

        Vector3 finalSpawnPos = spawnPos + Vector3.up * 0.5f;
        NetworkObject projectileObj = Runner.Spawn(projectilePrefab, finalSpawnPos, Quaternion.identity, PlayerRef.None);

        if (projectileObj != null)
        {
            ProjectileBehavior projectileBehavior = projectileObj.GetComponent<ProjectileBehavior>();
            if (projectileBehavior != null) projectileBehavior.SetData(targetPos, damage, Object.InputAuthority, arrowSpriteIndex, targetId);
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestProjectileSpawnToTargetRPC(Vector3 spawnPos, Vector3 targetPos, float damage, bool isCritical, int arrowSpriteIndex, NetworkId specificTargetId)
    {
        if (!Runner.IsServer) return;
        if (projectilePrefab == null) projectilePrefab = Resources.Load<GameObject>("ProjectilePrefab");

        Vector3 finalSpawnPos = spawnPos + Vector3.up * 0.5f;
        NetworkObject projectileObj = Runner.Spawn(projectilePrefab, finalSpawnPos, Quaternion.identity, PlayerRef.None);

        if (projectileObj != null)
        {
            ProjectileBehavior projectileBehavior = projectileObj.GetComponent<ProjectileBehavior>();
            if (projectileBehavior != null) projectileBehavior.SetData(targetPos, damage, Object.InputAuthority, arrowSpriteIndex, specificTargetId);
        }
    }

    private void CreateLocalProjectile(Vector3 spawnPos, Vector3 targetPos, Sprite arrowSprite)
    {
        GameObject localProjectileObj = new GameObject("LocalProjectile");
        localProjectileObj.transform.position = spawnPos;

        LocalProjectile localProjectile = localProjectileObj.AddComponent<LocalProjectile>();
        localProjectile.Initialize(spawnPos, targetPos, arrowSprite, Object.InputAuthority);
    }

    #endregion

    #region ANIMATION

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void SyncWeaponVisibilityRPC(int weaponTypeInt)
    {
        var character = GetComponent<Character4D>();
        if (character == null) return;

        PlayerStats.WeaponType weaponType = (PlayerStats.WeaponType)weaponTypeInt;
        Character[] allParts = { character.Front, character.Back, character.Left, character.Right };

        foreach (var part in allParts)
        {
            if (part == null) continue;

            if (part.PrimaryWeaponRenderer != null)
            {
                part.PrimaryWeaponRenderer.enabled = true;
                Color meleeColor = part.PrimaryWeaponRenderer.color;
                meleeColor.a = weaponType == PlayerStats.WeaponType.Melee ? 1.0f : 0.5f;
                part.PrimaryWeaponRenderer.color = meleeColor;
            }

            if (part.BowRenderers != null)
            {
                part.BowRenderers.ForEach(r =>
                {
                    r.enabled = true;
                    Color rangedColor = r.color;
                    rangedColor.a = weaponType == PlayerStats.WeaponType.Ranged ? 1.0f : 0.5f;
                    r.color = rangedColor;
                });
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void SyncMeleeAnimationRPC(Vector3 attackerPos, Vector3 targetPos, int weaponType)
    {
        if (character4D != null)
        {
            Vector2 direction = ((Vector2)(targetPos - attackerPos)).normalized;

            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                character4D.SetDirection(direction.x > 0 ? Vector2.right : Vector2.left);
            else
                character4D.SetDirection(direction.y > 0 ? Vector2.up : Vector2.down);

            bool isTwoHanded = currentWeaponType == PlayerStats.WeaponType.Melee;
            character4D.AnimationManager.Slash(isTwoHanded);

            isMeleeAttacking = true;
            meleeAnimEndTime = Time.time + meleeAttackAnimDuration;
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void SyncRangedAnimationRPC(Vector3 attackerPos, Vector3 targetPos)
    {
        if (character4D != null)
        {
            Vector2 direction = ((Vector2)(targetPos - attackerPos)).normalized;

            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
                character4D.SetDirection(direction.x > 0 ? Vector2.right : Vector2.left);
            else
                character4D.SetDirection(direction.y > 0 ? Vector2.up : Vector2.down);

            character4D.AnimationManager.ShotBow();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ShowDamagePopupRPC(Vector3 position, float damage, int damageType)
    {
        DamagePopup.Create(position + Vector3.up, damage, (DamagePopup.DamageType)damageType);
    }

    private void UpdateAttackStates()
    {
        if (isMeleeAttacking && Time.time >= meleeAnimEndTime) isMeleeAttacking = false;
        if (isRangedAttacking && Time.time >= rangedAnimEndTime) isRangedAttacking = false;
    }

    #endregion

    #region WEAPON

    public void SwitchWeapon(PlayerStats.WeaponType newWeaponType)
    {
        if (!Object.HasInputAuthority) return;

        currentWeaponType = newWeaponType;
        OnWeaponChanged?.Invoke(currentWeaponType);
        SyncWeaponVisibilityRPC((int)newWeaponType);
        UpdateButtonVisuals();

        // UpdateAutoAttackButtonState çağrısı CombatInitializer'da forceUpdate ile yapılıyor
    }

    public void OnWeaponEquipped(ItemData weapon)
    {
        if (!Object.HasInputAuthority) return;

        if (weapon.GameItemType == GameItemType.MeleeWeapon2H)
            SwitchWeapon(PlayerStats.WeaponType.Melee);
        else if (weapon.GameItemType == GameItemType.CompositeWeapon)
            SwitchWeapon(PlayerStats.WeaponType.Ranged);
    }

    public void OnWeaponUnequipped(ItemData weapon)
    {
        if (!Object.HasInputAuthority) return;

        if ((weapon.GameItemType == GameItemType.MeleeWeapon2H && currentWeaponType == PlayerStats.WeaponType.Melee) ||
            (weapon.GameItemType == GameItemType.CompositeWeapon && currentWeaponType == PlayerStats.WeaponType.Ranged))
        {
            SwitchWeapon(currentWeaponType == PlayerStats.WeaponType.Melee ? PlayerStats.WeaponType.Ranged : PlayerStats.WeaponType.Melee);
        }
    }

    public void OnWeaponTypeChanged(PlayerStats.WeaponType newType)
    {
        if (!Object.HasInputAuthority)
        {
            currentWeaponType = newType;
            UpdateButtonVisuals();
        }
    }

    private void UpdateButtonVisuals()
    {
        CombatInitializer combatInit = CombatInitializer.Instance;
        if (combatInit != null) combatInit.UpdateWeaponButtonStates();
    }

    #endregion

    #region UTILITY

    private bool IsCriticalHit()
    {
        return Random.value < playerStats.FinalCriticalChance / 100f;
    }

    #endregion
}