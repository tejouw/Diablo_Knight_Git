using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;

public class PVPSystem : NetworkBehaviour
{
[Header("PVP Settings")]
    [SerializeField] private float pvpDamageMultiplier = 0.8f;

[Networked] public bool NetworkIsTaunted { get; set; }
[Networked] public float NetworkTauntEndTime { get; set; }
[Networked] public NetworkId NetworkTaunterId { get; set; }
[Networked] public bool IsInPVPZone { get; set; }

// ✅ Local flag ekle - client için immediate response
public bool localPVPStatus = false;
private float lastLogTime = 0f;

private AreaSystem areaSystem;
private WeaponSystem weaponSystem;
private PlayerStats playerStats;
private bool wasInPVPZone = false;
    private GameObject currentTaunter;
    public static event System.Action<PlayerStats, bool> OnPlayerPVPStatusChanged;

public override void Spawned()
{
    
    // ✅ Local flag'i initialize et
    localPVPStatus = false;
    
    // Component referanslarını al
    weaponSystem = GetComponent<WeaponSystem>();
    playerStats = GetComponent<PlayerStats>();
    
    if (Object.HasInputAuthority)
    {
        StartCoroutine(InitializeAreaSystemDelayed());
    }
    else
    {
    }
}

private System.Collections.IEnumerator InitializeAreaSystemDelayed()
{
    // AreaSystem'in initialize olmasını bekle
    int attempts = 0;
    while (AreaSystem.Instance == null && attempts < 50)
    {
        yield return new WaitForSeconds(0.1f);
        attempts++;
    }
    
    areaSystem = AreaSystem.Instance;
    if (areaSystem != null)
    {
        areaSystem.OnAreaChanged += OnAreaChanged;
        
        // Mevcut alan durumunu kontrol et
        if (areaSystem.CurrentArea != null)
        {
            OnAreaChanged(areaSystem.CurrentArea);
        }
    }
    else
    {
        Debug.LogError("[PVP] AreaSystem bulunamadı!");
    }
}

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Event'leri temizle
        if (areaSystem != null)
        {
            areaSystem.OnAreaChanged -= OnAreaChanged;
        }
    }

    private void OnAreaChanged(AreaData area)
    {
        if (!Object.HasInputAuthority) return;

        // ✅ GÜNCELLEME: Alan adı yerine isPVPEnabled kontrolü
        bool newPVPStatus = area != null && area.isPVPEnabled;
        
        // ✅ Local flag'i hemen güncelle
        localPVPStatus = newPVPStatus;
        
        // ✅ Networked property'yi de hemen güncelle (InputAuthority için)
        IsInPVPZone = newPVPStatus;
        
        // Server'a bildir - RPC başarı kontrolü ekle
        RequestPVPStatusChangeRPC(newPVPStatus);
        
        // UI bildirimi
        if (newPVPStatus != wasInPVPZone)
        {
            wasInPVPZone = newPVPStatus;
            if (newPVPStatus)
            {
                // PVP zona girdi
            }
            else
            {
                // PVP zondan çıktı
            }
        }
    }
[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void RequestPVPStatusChangeRPC(bool newPVPStatus)
{
    
    if (!Runner.IsServer) 
    {
        Debug.LogError("[PVP] RPC server'a ulaşmadı!");
        return;
    }
    
    
    // Server networked property'yi set eder
    IsInPVPZone = newPVPStatus;
    localPVPStatus = newPVPStatus;
    
    // Tüm client'lara bildir
    SyncPVPStatusRPC(newPVPStatus);
    
    // ✅ Tüm diğer player'ları da kontrol et ve sync et
    StartCoroutine(SyncAllPlayersPVPStatus());
}
private System.Collections.IEnumerator SyncAllPlayersPVPStatus()
{
    yield return new WaitForEndOfFrame();
    
    GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
    
    foreach (GameObject player in allPlayers)
    {
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        PVPSystem pvpSystem = player.GetComponent<PVPSystem>();
        
        if (netObj != null && netObj.Runner != null && pvpSystem != null)
        {
            pvpSystem.SyncPVPStatusRPC(pvpSystem.IsInPVPZone);
        }
        
        yield return new WaitForSeconds(0.1f); // Her player arası kısa bekleme
    }
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncPVPStatusRPC(bool isPVPActive)
{
    
    // ✅ DÜZELTME: Herkes kendi IsInPVPZone'unu set edebilir
    IsInPVPZone = isPVPActive;
    localPVPStatus = isPVPActive;
    
    // Event'i tetikle
    if (playerStats != null)
    {
        OnPlayerPVPStatusChanged?.Invoke(playerStats, isPVPActive);
    }
}
public override void FixedUpdateNetwork()
{
    if (!Object.HasInputAuthority) return;

    // PVP durumu değişti mi kontrol et
    if (wasInPVPZone != IsInPVPZone)
    {
        wasInPVPZone = IsInPVPZone;
        
        if (weaponSystem != null)
        {
            // WeaponSystem'e PVP durumunu bildir
        }
    }
    
    // ❌ Periyodik kontrol kodunu kaldırın - artık gerekli değil
}
    private void SpawnPVPProjectile(Vector3 targetPos)
    {

        if (weaponSystem == null)
        {
            Debug.LogError("[PVP] WeaponSystem referansı yok!");
            return;
        }

        // Ranged animasyonu sync et
        SyncPVPRangedAnimationRPC(transform.position, targetPos);

        // Arrow sprite index'ini al
        Sprite arrowSprite = null;
        int arrowSpriteIndex = -1;
        var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
        if (character4D != null)
        {
            var currentCharacter = character4D.Active;
            if (currentCharacter != null && currentCharacter.CompositeWeapon != null &&
                currentCharacter.CompositeWeapon.Count > 0)
            {
                arrowSpriteIndex = 0;
                arrowSprite = currentCharacter.CompositeWeapon[0];
            }
        }

        bool isCritical = IsCriticalHit();
        float baseDamage = playerStats.GetDamageAmount(isCritical);
        float pvpDamage = baseDamage * pvpDamageMultiplier;

        // CLIENT: Hemen local görsel projectile oluştur (PVP için)
        CreateLocalPVPProjectile(transform.position + Vector3.up * 0.5f, targetPos, arrowSprite);

        // Server'a PVP projectile spawn talebi gönder
        RequestPVPProjectileSpawnRPC(transform.position, targetPos, pvpDamage, isCritical, arrowSpriteIndex);
    }
private void CreateLocalPVPProjectile(Vector3 spawnPos, Vector3 targetPos, Sprite arrowSprite)
{
    
    GameObject localProjectileObj = new GameObject("LocalPVPProjectile");
    localProjectileObj.transform.position = spawnPos;
    
    LocalProjectile localProjectile = localProjectileObj.AddComponent<LocalProjectile>();
    localProjectile.Initialize(spawnPos, targetPos, arrowSprite);
    
}
[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void RequestPVPProjectileSpawnRPC(Vector3 spawnPos, Vector3 targetPos, float damage, bool isCritical, int arrowSpriteIndex)
{
    if (!Runner.IsServer) return;
    
    GameObject projectilePrefab = Resources.Load<GameObject>("ProjectilePrefab");
    if (projectilePrefab == null)
    {
        Debug.LogError("[PVP] ProjectilePrefab not found in Resources!");
        return;
    }
    
    Vector3 finalSpawnPos = spawnPos + Vector3.up * 0.5f;
    
    NetworkObject projectileObj = Runner.Spawn(projectilePrefab, finalSpawnPos, Quaternion.identity, Object.InputAuthority);
    
    if (projectileObj != null)
    {
        ProjectileBehavior projectileBehavior = projectileObj.GetComponent<ProjectileBehavior>();
        if (projectileBehavior != null)
        {
            // SetData kullan
            projectileBehavior.SetData(targetPos, damage, Object.InputAuthority, arrowSpriteIndex);
        }
    }
}

[Rpc(RpcSources.InputAuthority, RpcTargets.All)]
private void SyncPVPRangedAnimationRPC(Vector3 attackerPos, Vector3 targetPos)
{
    var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
    if (character4D != null)
    {
        // Saldırı yönünü hesapla
        Vector2 direction = ((Vector2)(targetPos - attackerPos)).normalized;
        
        // Character4D'nin yönünü ayarla
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            character4D.SetDirection(direction.x > 0 ? Vector2.right : Vector2.left);
        }
        else
        {
            character4D.SetDirection(direction.y > 0 ? Vector2.up : Vector2.down);
        }
        
        // Yay çekme animasyonunu oynat
        character4D.AnimationManager.ShotBow();
        
    }
}
// Mevcut TryAttackPlayer metodunu tamamen değiştir:
public bool TryAttackPlayer()
{
    if (!Object.HasInputAuthority || !localPVPStatus) 
    {
        return false;
    }
    
    // SADECE DeathSystem kontrolü
    var deathSystem = GetComponent<DeathSystem>();
    if (deathSystem != null && deathSystem.IsDead) 
    {
        return false;
    }

    // Taunt kontrolü - taunt edilmişse sadece taunter'a saldırabilir
    if (IsTaunted())
    {
        GameObject taunter = GetTaunter();
        if (taunter != null)
        {
            return AttackSpecificTarget(taunter);
        }
        return false;
    }

    // Normal PvP targeting
    GameObject nearestPlayer = FindNearestPlayer();
    if (nearestPlayer == null) 
    {
        return false;
    }

    return AttackSpecificTarget(nearestPlayer);
}
// PVPSystem.cs - Bu metodu düzelt
public bool GetSafePVPStatus()
{
    if (!Object || !Object.IsValid)
    {
        return localPVPStatus; // Fallback to local status
    }

    try
    {
        // InputAuthority için local flag, diğerleri için network property
        return Object.HasInputAuthority ? localPVPStatus : IsInPVPZone;
    }
    catch (System.Exception)
    {
        return localPVPStatus; // Fallback to local status
    }
}
    public bool AttackSpecificPlayer(GameObject targetPlayer)
    {
        if (!Object.HasInputAuthority || !localPVPStatus)
        {
            return false;
        }

        var deathSystem = GetComponent<DeathSystem>();
        if (deathSystem != null && deathSystem.IsDead)
        {
            return false;
        }

        if (targetPlayer == null) return false;

        // Target'ın PVP zone'da olup olmadığını kontrol et
        PVPSystem targetPVP = targetPlayer.GetComponent<PVPSystem>();
        if (targetPVP == null) return false;

        bool targetInPVPZone = targetPVP.IsInPVPZone;
        bool targetLocalPVP = targetPVP.localPVPStatus;
        bool finalPVPStatus = targetInPVPZone || targetLocalPVP;

        if (!finalPVPStatus) return false;

        DeathSystem targetDeathSystem = targetPlayer.GetComponent<DeathSystem>();
        if (targetDeathSystem != null && targetDeathSystem.IsDead)
        {
            return false;
        }

        PlayerStats targetStats = targetPlayer.GetComponent<PlayerStats>();
        if (targetStats != null && targetStats.NetworkCurrentHP <= 0)
        {
            return false;
        }

        return AttackSpecificTarget(targetPlayer);
    }
    public bool AttackSpecificTargetById(NetworkId targetId, Vector3 targetPosition)
    {
        if (!Object.HasInputAuthority || !localPVPStatus)
        {
            return false;
        }

        var deathSystem = GetComponent<DeathSystem>();
        if (deathSystem != null && deathSystem.IsDead)
        {
            return false;
        }

        // NetworkId'den target'ı bul
        NetworkObject targetNetObj = Runner.FindObject(targetId);
        if (targetNetObj == null) return false;

        GameObject targetPlayer = targetNetObj.gameObject;
        if (!targetPlayer.CompareTag("Player")) return false;

        // PVP validasyonu
        PVPSystem targetPVP = targetPlayer.GetComponent<PVPSystem>();
        if (targetPVP == null) return false;

        bool targetInPVPZone = targetPVP.IsInPVPZone;
        bool targetLocalPVP = targetPVP.localPVPStatus;
        bool finalPVPStatus = targetInPVPZone || targetLocalPVP;

        if (!finalPVPStatus) return false;

        DeathSystem targetDeathSystem = targetPlayer.GetComponent<DeathSystem>();
        if (targetDeathSystem != null && targetDeathSystem.IsDead)
        {
            return false;
        }

        // Normal silah range'ini kullan
        float attackRange = weaponSystem.CurrentWeaponType == PlayerStats.WeaponType.Melee ?
            weaponSystem.MeleeAttackRange : weaponSystem.RangedAttackRange;

        float distanceToTarget = Vector2.Distance(transform.position, targetPosition);
        if (distanceToTarget > attackRange)
        {
            return false;
        }

        // Saldırı animasyonu ve efektleri
        var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
        if (character4D != null && weaponSystem != null)
        {
            // Saldırı yönünü hesapla
            Vector2 direction = ((Vector2)(targetPosition - transform.position)).normalized;
            character4D.SetDirection(direction);

            // Silah tipine göre animasyon - SPECIFIC TARGET kullan
            if (weaponSystem.CurrentWeaponType == PlayerStats.WeaponType.Melee)
            {
                SyncPVPMeleeAnimationRPC(transform.position, targetPosition);
                RequestPVPMeleeAttackRPC(targetId);
            }
            else
            {
                character4D.AnimationManager.ShotBow();
                SpawnPVPProjectileToTarget(targetId, targetPosition);
            }
        }

        return true;
    }
private void SpawnPVPProjectileToTarget(NetworkId targetId, Vector3 targetPosition)
{
    if (weaponSystem == null)
    {
        Debug.LogError("[PVP] WeaponSystem referansı yok!");
        return;
    }

    // Ranged animasyonu sync et
    SyncPVPRangedAnimationRPC(transform.position, targetPosition);

    // Arrow sprite index'ini al
    Sprite arrowSprite = null;
    int arrowSpriteIndex = -1;
    var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
    if (character4D != null)
    {
        var currentCharacter = character4D.Active;
        if (currentCharacter != null && currentCharacter.CompositeWeapon != null &&
            currentCharacter.CompositeWeapon.Count > 0)
        {
            arrowSpriteIndex = 0;
            arrowSprite = currentCharacter.CompositeWeapon[0];
        }
    }

    bool isCritical = IsCriticalHit();
    float baseDamage = playerStats.GetDamageAmount(isCritical);
    float pvpDamage = baseDamage * pvpDamageMultiplier;

    // CLIENT: Local görsel projectile oluştur
    CreateLocalPVPProjectile(transform.position + Vector3.up * 0.5f, targetPosition, arrowSprite);

    // Server'a PVP projectile spawn talebi gönder - SPECIFIC TARGET ile
    RequestPVPProjectileSpawnToTargetRPC(transform.position, targetPosition, pvpDamage, isCritical, arrowSpriteIndex, targetId);
}

[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
private void RequestPVPProjectileSpawnToTargetRPC(Vector3 spawnPos, Vector3 targetPos, float damage, bool isCritical, int arrowSpriteIndex, NetworkId specificTargetId)
{
    if (!Runner.IsServer) return;
    
    GameObject projectilePrefab = Resources.Load<GameObject>("ProjectilePrefab");
    if (projectilePrefab == null)
    {
        Debug.LogError("[PVP] ProjectilePrefab not found in Resources!");
        return;
    }
    
    Vector3 finalSpawnPos = spawnPos + Vector3.up * 0.5f;
    
    NetworkObject projectileObj = Runner.Spawn(projectilePrefab, finalSpawnPos, Quaternion.identity, Object.InputAuthority);
    
    if (projectileObj != null)
    {
        ProjectileBehavior projectileBehavior = projectileObj.GetComponent<ProjectileBehavior>();
        if (projectileBehavior != null)
        {
            // Mevcut SetData metodunu kullan - NetworkId ile specific target
            projectileBehavior.SetData(targetPos, damage, Object.InputAuthority, arrowSpriteIndex, specificTargetId);
        }
    }
}
// PVPSystem.cs - Bu metodu düzelt
public bool IsSafeToCheckPVP()
{
    return Object != null && Object.IsValid && Runner != null;
}
private bool AttackSpecificTarget(GameObject target)
{
    if (target == null) return false;
    
    // Normal silah range'ini kullan
    float attackRange = weaponSystem.CurrentWeaponType == PlayerStats.WeaponType.Melee ?
        weaponSystem.MeleeAttackRange : weaponSystem.RangedAttackRange;

    float distanceToTarget = Vector2.Distance(transform.position, target.transform.position);
    if (distanceToTarget > attackRange) 
    {
        return false;
    }

    // Saldırı animasyonu ve efektleri
    var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
    if (character4D != null && weaponSystem != null)
    {
        // Saldırı yönünü hesapla
        Vector2 direction = ((Vector2)(target.transform.position - transform.position)).normalized;
        character4D.SetDirection(direction);

        // Silah tipine göre animasyon
        if (weaponSystem.CurrentWeaponType == PlayerStats.WeaponType.Melee)
        {
            SyncPVPMeleeAnimationRPC(transform.position, target.transform.position);
            RequestPVPMeleeAttackRPC(target.GetComponent<NetworkObject>().Id);
        }
        else
        {
            character4D.AnimationManager.ShotBow();
            SpawnPVPProjectile(target.transform.position);
        }
    }

    return true;
}
private GameObject FindNearestPlayer()
{
    GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
    
    GameObject nearestPlayer = null;
    float minDistance = float.MaxValue;

    foreach (GameObject player in allPlayers)
    {
        if (player == gameObject) continue;

        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj == null || !netObj || netObj.gameObject == null || netObj.Runner == null) continue;

        PVPSystem otherPVP = player.GetComponent<PVPSystem>();
        if (otherPVP == null) continue;

        bool otherInPVPZone = otherPVP.IsInPVPZone;
        bool otherLocalPVP = otherPVP.localPVPStatus;
        bool finalPVPStatus = otherInPVPZone || otherLocalPVP;
        
        if (!finalPVPStatus) continue;

        // SADECE DeathSystem kontrolü - tek kaynak
        DeathSystem otherDeathSystem = player.GetComponent<DeathSystem>();
        if (otherDeathSystem != null && otherDeathSystem.IsDead)
        {
            continue;
        }

        // Network HP kontrolü
        PlayerStats otherStats = player.GetComponent<PlayerStats>();
        if (otherStats != null && otherStats.NetworkCurrentHP <= 0)
        {
            continue;
        }

        float distance = Vector2.Distance(transform.position, player.transform.position);
        
        if (distance < minDistance)
        {
            minDistance = distance;
            nearestPlayer = player;
        }
    }

    return nearestPlayer;
}

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RequestPVPMeleeAttackRPC(NetworkId targetPlayerId)
    {
        if (!Runner.IsServer) return;

        NetworkObject targetObject = Runner.FindObject(targetPlayerId);
        if (targetObject == null) return;

        PVPSystem targetPVP = targetObject.GetComponent<PVPSystem>();
        PlayerStats targetStats = targetObject.GetComponent<PlayerStats>();

        if (targetPVP == null || targetStats == null || !targetPVP.IsInPVPZone || targetStats.IsDead)
            return;

        float distance = Vector2.Distance(transform.position, targetObject.transform.position);
        if (distance > weaponSystem.MeleeAttackRange) return;

        // Damage hesapla ve uygula
        bool isCritical = IsCriticalHit();
        float baseDamage = playerStats.GetDamageAmount(isCritical);
        float pvpDamage = baseDamage * pvpDamageMultiplier;

        // Server damage uygular
        targetStats.TakeDamage(pvpDamage, isPVPDamage: true);

        // Visual effects
        targetStats.TriggerFlashEffectFromServer();

        PlayerController targetController = targetObject.GetComponent<PlayerController>();
        if (targetController != null)
        {
            targetController.TriggerHitAnimationFromServer();
        }

        // Hit efekti ve damage popup
        ShowPVPHitEffectRPC(targetObject.transform.position, isCritical);
        ShowPVPDamagePopupRPC(targetObject.transform.position, pvpDamage, isCritical);
    }
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void ShowPVPDamagePopupRPC(Vector3 position, float damageAmount, bool isCritical)
{
    DamagePopup.Create(position + Vector3.up, damageAmount, 
        isCritical ? DamagePopup.DamageType.Critical : DamagePopup.DamageType.Normal);
}



    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    private void SyncPVPMeleeAnimationRPC(Vector3 attackerPos, Vector3 targetPos)
    {
        var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
        if (character4D != null)
        {
            Vector2 direction = ((Vector2)(targetPos - attackerPos)).normalized;
            character4D.SetDirection(direction);
            character4D.AnimationManager.Slash(true); // PVP için her zaman two-handed
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ShowPVPHitEffectRPC(Vector3 position, bool isCritical)
    {
        // Basit hit efekti - WeaponSystem'dekine benzer
        GameObject hitEffect = new GameObject("PVPHitEffect");
        hitEffect.transform.position = position;
        
        SpriteRenderer sr = hitEffect.AddComponent<SpriteRenderer>();
        sr.sprite = CreatePVPHitSprite();
        sr.color = isCritical ? new Color(1f, 0.2f, 0.2f, 0.9f) : new Color(1f, 0.6f, 0.1f, 0.8f);
        sr.sortingLayerName = "UI";
        sr.sortingOrder = 10;
        
        // Self-destruct
        Destroy(hitEffect, 0.5f);
    }


    private bool IsCriticalHit()
    {
        return playerStats != null && Random.value < (playerStats.FinalCriticalChance / 100f);
    }

    private Sprite CreatePVPHitSprite()
    {
        // Basit PVP hit sprite oluştur
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] colors = new Color[size * size];
        
        Vector2 center = new Vector2(size/2, size/2);
        
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);
                float alpha = distance < size/2f ? Mathf.Clamp01(1f - distance/(size/2f)) : 0f;
                
                colors[y * size + x] = new Color(1f, 0.3f, 0.1f, alpha * alpha);
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
// Class'ın sonuna bu metodları EKLE:

public void ApplyTaunt(GameObject taunter, float duration)
{
    if (!Object.HasStateAuthority) return;
    
    NetworkIsTaunted = true;
    NetworkTauntEndTime = Time.time + duration;
    
    var taunterNetObj = taunter.GetComponent<NetworkObject>();
    if (taunterNetObj != null)
    {
        NetworkTaunterId = taunterNetObj.Id;
        currentTaunter = taunter;
        
        Debug.Log($"[PVP-{gameObject.name}] Taunted by {taunter.name} for {duration} seconds");
    }
}

public bool IsTaunted()
{
    if (Time.time >= NetworkTauntEndTime)
    {
        if (NetworkIsTaunted)
        {
            NetworkIsTaunted = false;
            NetworkTaunterId = default(NetworkId);
            currentTaunter = null;
        }
        return false;
    }
    return NetworkIsTaunted;
}

public GameObject GetTaunter()
{
    if (!IsTaunted()) return null;
    
    if (currentTaunter != null) return currentTaunter;
    
    if (Runner != null)
    {
        NetworkObject taunterObj = Runner.FindObject(NetworkTaunterId);
        if (taunterObj != null)
        {
            currentTaunter = taunterObj.gameObject;
            return currentTaunter;
        }
    }
    
    return null;
}
public bool CanAttackPlayers()
{
    if (!Object || !Object.IsValid)
    {
        return false;
    }

    try
    {
        // Log ekle - saniyede 1 kere
        if (Time.time - lastLogTime > 1f)
        {
            lastLogTime = Time.time;
        }
        
        bool result = Object.HasInputAuthority ? localPVPStatus : IsInPVPZone;
        return result;
    }
    catch (System.Exception)
    {
        return false;
    }
}

    // External sistemler için PVP durumu kontrolü
    public bool IsPlayerInPVPZone(GameObject player)
    {
        if (player == null) return false;
        
        PVPSystem otherPVP = player.GetComponent<PVPSystem>();
        return otherPVP != null && otherPVP.IsInPVPZone;
    }
}