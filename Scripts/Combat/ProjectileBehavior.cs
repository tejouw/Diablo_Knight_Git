using UnityEngine;
using Fusion;
using System.Collections;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;

public class ProjectileBehavior : NetworkBehaviour
{
    #region NETWORKED PROPERTIES
    [Networked] public Vector3 TargetPosition { get; set; }
    [Networked] public float Damage { get; set; }
    [Networked] public PlayerRef Attacker { get; set; }
    [Networked] public bool IsDestroyed { get; set; }
    [Networked] public int ArrowSpriteIndex { get; set; } = -1;
    [Networked] public bool IsInitialized { get; set; }
    [Networked] public float ProjectileSpeed { get; set; } = 5f; // YENİ
    [Networked] public NetworkId TargetNetworkId { get; set; } // YENİ - Homing için
    #endregion

    #region PRIVATE FIELDS
    private const float BASE_PROJECTILE_SPEED = 8f; // Base hız
    private float maxLifetime = 3f; // Lifetime artırıldı
    private float lifetime = 0f;
    private bool isMoving = false;
    private bool isLocalInitialized = false;
    
    private TrailRenderer trailRenderer;
    private SpriteRenderer spriteRenderer;
    private NetworkObject networkObject;
private double lastCollisionTime = 0f; // float yerine double
    

    private Vector3 currentDirection;
    #endregion

    #region UNITY LIFECYCLE
private void Awake()
{
    networkObject = GetComponent<NetworkObject>();
    if (networkObject == null) return;

    if (!IsServerMode())
    {
        CreateBasicVisuals();
    }

    // Collider'ı kontrol et, yoksa ekle
    CircleCollider2D collider = GetComponent<CircleCollider2D>();
    if (collider == null)
    {
        collider = gameObject.AddComponent<CircleCollider2D>();
    }
    collider.radius = 0.35f; // Biraz daha büyük
    collider.isTrigger = true;
}
private bool IsServerMode()
{
    // Editor'da iken HER ZAMAN client mode
    if (Application.isEditor)
    {
        return false;
    }
    
    // Build'de command line args kontrolü
    string[] args = System.Environment.GetCommandLineArgs();
    bool hasServerArg = System.Array.Exists(args, arg => 
        arg == "-server" || 
        arg == "-batchmode" || 
        arg == "-nographics" ||
        arg.StartsWith("-room"));
    
    if (hasServerArg)
    {
        return true;
    }
    
    // Build'de define kontrolü
#if UNITY_SERVER || DEDICATED_SERVER || SERVER_BUILD
    return true;
#else
    return false;
#endif
}

public override void Spawned()
{
    ApplyVisualSettings();
    // Coroutine kaldırıldı, artık gerek yok
}
    #endregion

    #region INITIALIZATION


public void SetData(Vector3 target, float dmg, PlayerRef attacker, int spriteIndex = -1, NetworkId targetId = default(NetworkId))
{
    if (Object.HasStateAuthority)
    {
        TargetPosition = target;
        Damage = dmg;
        Attacker = attacker;
        ArrowSpriteIndex = spriteIndex;
        TargetNetworkId = targetId;
        
        ProjectileSpeed = CalculateProjectileSpeed(attacker);
        
        IsInitialized = true;
        isLocalInitialized = true;
        isMoving = true;
        
        currentDirection = (target - transform.position).normalized;
        
        // Arrow sprite sync - hemen burada yap
        if (spriteIndex >= 0 && !IsServerMode())
        {
            ApplyArrowSprite(spriteIndex);
        }
        else if (!IsServerMode() && spriteRenderer != null)
        {
            spriteRenderer.sprite = CreateDefaultArrowSprite();
            ApplyVisualSettings();
        }
        
        UpdateRotation();
    }
}
    
    private float CalculateProjectileSpeed(PlayerRef attacker)
    {
        float finalSpeed = BASE_PROJECTILE_SPEED;
        
        // Attacker'ın PlayerStats'ını bul
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.InputAuthority == attacker)
            {
                PlayerStats playerStats = player.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    // Attack speed multiplier
                    float attackSpeedMultiplier = playerStats.FinalAttackSpeed;
                    
                    // Projectile speed item bonusu
                    float projectileSpeedBonus = GetProjectileSpeedBonus(player);
                    
                    finalSpeed = BASE_PROJECTILE_SPEED * attackSpeedMultiplier * (1f + projectileSpeedBonus / 100f);
                }
                break;
            }
        }
        
        return Mathf.Clamp(finalSpeed, 3f, 20f); // Min-max sınır
    }
    
private float GetProjectileSpeedBonus(GameObject player)
{
    float totalBonus = 0f;
    
    EquipmentSystem equipmentSystem = player.GetComponent<EquipmentSystem>();
    if (equipmentSystem != null)
    {
        var allEquipment = equipmentSystem.GetAllEquippedItems();
        
        // Dictionary'yi doğru şekilde iterate et
        foreach (var slotPair in allEquipment)
        {
            foreach (var item in slotPair.Value)
            {
                if (item != null)
                {
                    totalBonus += item.GetStatValue(StatType.ProjectileSpeed);
                }
            }
        }
    }
    
    return totalBonus;
}
    #endregion

    #region HOMING SYSTEM

    private void UpdateRotation()
    {
        if (currentDirection.magnitude > 0.01f)
        {
            float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
        }
    }
    #endregion

    #region NETWORK UPDATE
public override void FixedUpdateNetwork()
{
    if (IsDestroyed || !isMoving || !isLocalInitialized) return;
    
    if (Object.HasStateAuthority)
    {
        lifetime += Runner.DeltaTime;
        if (lifetime >= maxLifetime)
        {
            RequestDestroy();
            return;
        }
        
        // Hedef mesafe kontrolü ÖNCE
        float distanceToTarget = Vector3.Distance(transform.position, TargetPosition);
        if (distanceToTarget < 0.4f)
        {
            RequestDestroy();
            return;
        }
        
        // Hareket
        transform.position += currentDirection * ProjectileSpeed * Runner.DeltaTime;
        UpdateRotation();
    }
}
    
    public override void Render()
    {
        // Client-side smooth interpolation
        if (!Object.HasStateAuthority && isMoving && isLocalInitialized)
        {
            // Smooth rotation for clients
            if (currentDirection.magnitude > 0.01f)
            {
                float targetAngle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg - 90f;
                float currentAngle = transform.eulerAngles.z;
                float smoothAngle = Mathf.LerpAngle(currentAngle, targetAngle, Time.deltaTime * 10f);
                transform.rotation = Quaternion.AngleAxis(smoothAngle, Vector3.forward);
            }
        }
    }
    #endregion

    #region VISUAL CREATION
    private void CreateBasicVisuals()
    {
        // Sprite Renderer ekle
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingLayerName = "UI";
            spriteRenderer.sortingOrder = 5;
            spriteRenderer.sprite = CreateDefaultArrowSprite();
            Material defaultMaterial = new Material(Shader.Find("Sprites/Default"));
            spriteRenderer.material = defaultMaterial;
        }

        // Trail effect ekle
        if (trailRenderer == null)
        {
            trailRenderer = gameObject.AddComponent<TrailRenderer>();
            trailRenderer.startWidth = 0.08f;
            trailRenderer.endWidth = 0.02f;
            trailRenderer.time = 0.2f; // Uzatıldı
            trailRenderer.sortingLayerName = "UI";
            trailRenderer.sortingOrder = 4;

            Material trailMaterial = new Material(Shader.Find("Sprites/Default"));
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.yellow, 0.0f), new GradientColorKey(Color.red, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            trailRenderer.colorGradient = gradient;
            trailRenderer.material = trailMaterial;
        }
    }
    
private void ApplyVisualSettings()
{
    // DEĞİŞİKLİK: Server'da visual'lar zaten yok, null check ekle
    if (IsServerMode()) return;
    
    bool hideVisuals = Attacker == Runner.LocalPlayer; // DÜZELTME: InputAuthority yerine Attacker kontrolü
    
    if (spriteRenderer != null)
    {
        spriteRenderer.enabled = !hideVisuals;
    }
    
    if (trailRenderer != null)
    {
        trailRenderer.enabled = !hideVisuals;
    }
}

    private Sprite CreateDefaultArrowSprite()
    {
        int width = 8;
        int height = 16;
        Texture2D texture = new Texture2D(width, height);
        Color[] colors = new Color[width * height];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Color pixelColor = Color.clear;
                
                if (y > height * 0.7f)
                {
                    float centerX = width / 2f;
                    float tipY = height - 1;
                    float distance = Mathf.Abs(x - centerX);
                    float maxDistance = (tipY - y) * 0.8f;
                    
                    if (distance <= maxDistance)
                    {
                        pixelColor = new Color(0.8f, 0.6f, 0.2f, 1f);
                    }
                }
                else if (x >= width * 0.4f && x < width * 0.6f)
                {
                    pixelColor = new Color(0.6f, 0.4f, 0.2f, 1f);
                }
                
                colors[y * width + x] = pixelColor;
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
    }
    
private void ApplyArrowSprite(int spriteIndex)
{
    // DEĞİŞİKLİK: Server'da visual'lar yok
    if (IsServerMode()) return;
    
    if (spriteIndex < 0) return;
        
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.InputAuthority == Attacker)
            {
                Character4D playerChar4D = player.GetComponent<Character4D>();
                if (playerChar4D != null)
                {
                    Character currentCharacter = playerChar4D.Active;
                    if (currentCharacter != null && currentCharacter.CompositeWeapon != null && 
                        currentCharacter.CompositeWeapon.Count > spriteIndex)
                    {
                        Sprite arrowSprite = currentCharacter.CompositeWeapon[spriteIndex];
                        if (spriteRenderer != null && arrowSprite != null)
                        {
                            spriteRenderer.sprite = arrowSprite;
                            ApplyVisualSettings();
                            return;
                        }
                    }
                }
                break;
            }
        }
        
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = CreateDefaultArrowSprite();
            ApplyVisualSettings();
        }
    }
    #endregion

    #region COLLISION DETECTION
private void OnTriggerEnter2D(Collider2D other)
{
    if (!Object.HasStateAuthority || IsDestroyed || !isLocalInitialized) return;
    
    // Collision cooldown KALDIRILDI - artık her collision işlenir
    // Ama aynı frame'de tekrar collision olmasını engelle
    if (Runner.SimulationTime - lastCollisionTime < Runner.DeltaTime * 2)
        return;
    
    lastCollisionTime = (float)Runner.SimulationTime;

    Transform checkTransform = other.transform;
    while (checkTransform.parent != null && checkTransform.GetComponent<NetworkObject>() == null)
    {
        checkTransform = checkTransform.parent;
    }

    NetworkObject targetNetObj = checkTransform.GetComponent<NetworkObject>();
    if (targetNetObj == null) return;
    if (targetNetObj.InputAuthority == Attacker) return;

    MonsterBehaviour monster = checkTransform.GetComponent<MonsterBehaviour>();
    if (monster != null && !monster.IsDead)
    {
        HandleMonsterHit(monster, other.bounds.center);
        return;
    }

    PlayerStats playerStats = checkTransform.GetComponent<PlayerStats>();
    PVPSystem targetPVP = checkTransform.GetComponent<PVPSystem>();
    
    if (playerStats != null && !playerStats.IsDead && targetPVP != null && targetPVP.IsInPVPZone)
    {
        HandlePlayerHit(targetNetObj, playerStats, other.bounds.center);
        return;
    }
}

    private void HandleMonsterHit(MonsterBehaviour monster, Vector3 hitPoint)
    {
        bool isCritical = Random.value < 0.15f;
        monster.TakeDamageFromServer(Damage, Attacker, isCritical);
        ShowProjectileHitEffectRPC(hitPoint, isCritical);
        ShowDamagePopupRPC(hitPoint, Damage, isCritical ? 1 : 0);
        RequestDestroy();
    }

    private void HandlePlayerHit(NetworkObject targetNetObj, PlayerStats targetStats, Vector3 hitPoint)
    {
        bool isCritical = Random.value < 0.15f;
        float pvpDamage = Damage * 0.8f;
        targetStats.TakeDamage(pvpDamage, isPVPDamage: true);
        targetStats.TriggerFlashEffectFromServer();
        
        PlayerController targetController = targetNetObj.GetComponent<PlayerController>();
        if (targetController != null)
        {
            targetController.TriggerHitAnimationFromServer();
        }
        
        ShowProjectileHitEffectRPC(hitPoint, isCritical);
        ShowDamagePopupRPC(hitPoint, pvpDamage, isCritical ? 1 : 0);
        RequestDestroy();
    }
    #endregion

    #region DESTRUCTION
    private void RequestDestroy()
    {
        if (IsDestroyed) return;
        
        if (Object.HasStateAuthority)
        {
            IsDestroyed = true;
            DestroyRPC();
        }
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void DestroyRPC()
    {
        if (trailRenderer != null) trailRenderer.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        
        IsDestroyed = true;
        isMoving = false;
        
        if (Object.HasStateAuthority)
        {
            StartCoroutine(DestroyAfterDelay());
        }
    }
    
    private IEnumerator DestroyAfterDelay()
    {
        yield return new WaitForSeconds(0.2f);
        
        try
        {
            if (Runner != null && Object != null)
            {
                Runner.Despawn(Object);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Projectile destroy hatası: {e.Message}");
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ShowProjectileHitEffectRPC(Vector3 position, bool isCritical)
    {
        ShowProjectileHitEffect(position, isCritical);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ShowDamagePopupRPC(Vector3 position, float damageAmount, int damageType)
    {
        DamagePopup.Create(position + Vector3.up, damageAmount, (DamagePopup.DamageType)damageType);
    }

    private void ShowProjectileHitEffect(Vector3 position, bool isCritical)
    {
        GameObject hitEffect = new GameObject("ProjectileHitEffect");
        hitEffect.transform.position = position;

        SpriteRenderer sr = hitEffect.AddComponent<SpriteRenderer>();
        sr.sprite = CreateHitEffectSprite();
        sr.color = isCritical ? new Color(1f, 0.8f, 0.2f, 0.9f) : new Color(1f, 1f, 1f, 0.8f);
        sr.sortingLayerName = "UI";
        sr.sortingOrder = 10;

        hitEffect.AddComponent<HitEffectSelfDestruct>().Initialize(isCritical);
    }

    private Sprite CreateHitEffectSprite()
    {
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
                float maxDistance = size / 2f;
                
                float alpha = distance < maxDistance ? 
                    Mathf.Clamp01(1f - distance/maxDistance) : 0f;
                
                colors[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
    #endregion
}