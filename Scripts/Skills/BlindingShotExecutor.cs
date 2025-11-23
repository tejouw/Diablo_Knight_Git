using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Fusion;
public class BlindingShotExecutor : BaseSkillExecutor
{
    public override string SkillId => "blinding_shot";

    private const float ACCURACY_DEBUFF_PERCENT = 30f; // %30 accuracy reduction
    private const float SLOW_DEBUFF_PERCENT = 20f; // %20 movement speed reduction
    private const float DEBUFF_DURATION = 1f; // 1 saniye
    private const float PROJECTILE_SPEED = 12f; // Hızlı projectile
    private const float SKILL_RANGE = 10f; // 10 unit menzil
    private const float DAMAGE_MULTIPLIER = 1f;
    public override void Execute(GameObject caster, SkillInstance skillInstance)
    {
        // Client-side sadece animation
        var character4D = caster.GetComponent<Character4D>();
        if (character4D != null)
        {
            character4D.AnimationManager.ShotBow();
        }
    }

    // BlindingShotExecutor.cs - ExecuteOnServer metodunda ApplyBlindingDebuffs çağrısını güncelle

// BlindingShotExecutor.cs - Bu metodu değiştir
public void ExecuteOnServer(GameObject caster, SkillInstance skillInstance, SkillSystem skillSystem)
{
    if (!skillSystem.Object.HasStateAuthority) return;

    // Server-side cooldown set
    skillInstance.lastUsedTime = Time.time;

    var playerStats = GetPlayerStats(caster);
    var character4D = caster.GetComponent<Character4D>();

    if (playerStats == null || character4D == null)
    {
        Debug.LogError("[BlindingShot-SERVER] Missing components!");
        return;
    }

    // Accuracy check for caster
    if (!playerStats.RollAccuracyCheck())
    {
        // Caster missed, show miss effect
        skillSystem.ExecuteBlindingShotMissRPC(caster.transform.position, character4D.Direction);
        return;
    }

    // Find nearest target
    GameObject target = FindNearestTarget(caster);

    if (target == null)
    {
        // No target found, but still show projectile to other clients
        skillSystem.ExecuteBlindingShotRPC(
            caster.transform.position,
            character4D.Direction,
            false, // hitTarget = false
            Vector3.zero // targetPos = zero
        );
        return;
    }

    // Calculate direction to target
    Vector2 directionToTarget = (target.transform.position - caster.transform.position).normalized;
    float distanceToTarget = Vector2.Distance(caster.transform.position, target.transform.position);

    // Calculate projectile travel time
    float travelTime = distanceToTarget / PROJECTILE_SPEED;

    // Start coroutine to apply damage after projectile travel time
    skillSystem.StartCoroutine(ApplyBlindingDamageAfterDelay(caster, target, skillSystem, travelTime));

    // Execute VFX on all clients
    skillSystem.ExecuteBlindingShotRPC(
        caster.transform.position,
        directionToTarget,
        true,
        target.transform.position
    );
}
    private System.Collections.IEnumerator ApplyBlindingDamageAfterDelay(GameObject caster, GameObject target, SkillSystem skillSystem, float delay)
    {
        yield return new WaitForSeconds(delay);

        // Target hala valid mi kontrol et
        if (target == null) yield break;

        if (target.CompareTag("Monster"))
        {
            var monster = target.GetComponent<MonsterBehaviour>();
            if (monster == null || monster.IsDead) yield break;
        }
        else if (target.CompareTag("Player"))
        {
            var playerStats = target.GetComponent<PlayerStats>();
            if (playerStats == null || playerStats.IsDead) yield break;
        }

        // Apply damage and debuffs now
        ApplyBlindingDebuffs(target, caster, skillSystem);
    }

    private GameObject FindNearestTarget(GameObject caster)
    {
        var character4D = caster.GetComponent<Character4D>();
        Vector2 direction = character4D?.Direction ?? Vector2.down;

        return SkillTargetingUtils.FindClosestTarget(caster.transform.position, SKILL_RANGE, caster, direction, 0.7f);
    }
    private void ApplyBlindingDebuffs(GameObject target, GameObject caster, SkillSystem skillSystem)
    {
        // Önce damage uygula
        var playerStats = GetPlayerStats(caster);
        if (playerStats != null)
        {
            float damage = playerStats.FinalDamage * DAMAGE_MULTIPLIER;
            ApplyDamageToTarget(caster, target, damage, skillSystem);
        }

        // Sonra debuff'ları uygula
        if (target.CompareTag("Player"))
        {
            var tempBuff = target.GetComponent<TemporaryBuffSystem>();
            if (tempBuff == null)
            {
                tempBuff = target.gameObject.AddComponent<TemporaryBuffSystem>();
            }

            // Apply both debuffs
            tempBuff.ApplyAccuracyDebuff(ACCURACY_DEBUFF_PERCENT, DEBUFF_DURATION);
            tempBuff.ApplySlowDebuff(SLOW_DEBUFF_PERCENT, DEBUFF_DURATION);
        }
        else if (target.CompareTag("Monster"))
        {
            var monsterBehaviour = target.GetComponent<MonsterBehaviour>();
            if (monsterBehaviour != null)
            {
                // For monsters, use damage debuff as accuracy debuff
                monsterBehaviour.ApplyAccuracyDebuff(ACCURACY_DEBUFF_PERCENT, DEBUFF_DURATION);
                monsterBehaviour.ApplySlowDebuff(SLOW_DEBUFF_PERCENT, DEBUFF_DURATION);
            }
        }
    }

    // BlindingShotExecutor.cs - ExecuteVFX metodunu güncelle

// BlindingShotExecutor.cs - Bu metodu değiştir
public void ExecuteVFX(GameObject caster, Vector3 startPos, Vector2 direction, bool hitTarget, Vector3 targetPos)
{
    var character4D = caster.GetComponent<Character4D>();

    // Character animation
    if (character4D != null)
    {
        character4D.AnimationManager.ShotBow();
    }

    // Target varsa karakterin yönünü hedefe çevir
    if (hitTarget && targetPos != Vector3.zero)
    {
        Vector2 directionToTarget = (targetPos - startPos).normalized;

        // Character4D direction'ını güncelle
        if (character4D != null)
        {
            if (Mathf.Abs(directionToTarget.x) > Mathf.Abs(directionToTarget.y))
            {
                character4D.SetDirection(directionToTarget.x > 0 ? Vector2.right : Vector2.left);
            }
            else
            {
                character4D.SetDirection(directionToTarget.y > 0 ? Vector2.up : Vector2.down);
            }
        }

        // Projectile direction'ını güncelle
        direction = directionToTarget;
    }

    // ✅ Skill'i atan client ise duplicate projectile spawn etme
    var networkObj = caster.GetComponent<NetworkObject>();
    bool isLocalPlayer = networkObj != null && networkObj.HasInputAuthority;
    
    if (!isLocalPlayer)
    {
        // Sadece remote clientlar için projectile spawn et
        SpawnBlindingShotProjectile(startPos, direction, hitTarget, targetPos);
    }

    // Hit effects her zaman spawn et
    if (hitTarget)
    {
        SpawnBlindingHitEffect(targetPos);
        SpawnBlindingBlurEffect(targetPos);
    }
}

    public void ExecuteMissVFX(GameObject caster, Vector3 startPos, Vector2 direction)
    {
        var character4D = caster.GetComponent<Character4D>();

        // Character animation
        if (character4D != null)
        {
            character4D.AnimationManager.ShotBow();
        }

        // Show miss effect on caster
        DamagePopup.Create(startPos + Vector3.up, 0f, DamagePopup.DamageType.Miss);
    }

    private void SpawnBlindingShotProjectile(Vector3 startPos, Vector2 direction, bool hitTarget, Vector3 targetPos)
    {

        var vfxPrefab = Resources.Load<GameObject>("VFX/BlindingShot_Projectile");
        if (vfxPrefab != null)
        {

            Vector3 spawnPosition = startPos + Vector3.up * 0.5f;
            GameObject vfxProjectile = Object.Instantiate(vfxPrefab, spawnPosition, Quaternion.identity);


            // Direction'a göre rotation ayarla
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            vfxProjectile.transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);

            // VFX'e movement component ekle
            BlindingShotProjectileMovement movement = vfxProjectile.GetComponent<BlindingShotProjectileMovement>();
            if (movement == null)
            {
                movement = vfxProjectile.AddComponent<BlindingShotProjectileMovement>();
            }
            else
            {
            }

            // Initialize parametrelerini doğru sırada gönder
            movement.Initialize(direction, PROJECTILE_SPEED, hitTarget, targetPos);

            // Otomatik yok et
            Object.Destroy(vfxProjectile, 3f);
        }
        else
        {
            Debug.LogError("[BlindingShot] BlindingShot_Projectile prefab not found in Resources/VFX/!");
        }
    }

    private void SpawnBlindingHitEffect(Vector3 position)
    {
        // Beyaz parıltı + yıldız efekti
        var prefab = Resources.Load<GameObject>("VFX/BlindingShot_HitEffect");
        if (prefab != null)
        {
            GameObject effect = Object.Instantiate(prefab, position, Quaternion.identity);
            Object.Destroy(effect, 2f);
        }
        else
        {
            Debug.LogError("[BlindingShot] BlindingShot_HitEffect prefab not found in Resources/VFX/!");
        }
    }
    private void SpawnBlindingBlurEffect(Vector3 position)
    {
        // Hedefin etrafında bulanıklık halkası
        var prefab = Resources.Load<GameObject>("VFX/BlindingShot_BlurRing");
        if (prefab != null)
        {
            GameObject blurEffect = Object.Instantiate(prefab, position, Quaternion.identity);
            Object.Destroy(blurEffect, DEBUFF_DURATION + 0.5f); // Debuff süresinden biraz uzun
        }
        else
        {
            Debug.LogError("[BlindingShot] BlindingShot_BlurRing prefab not found in Resources/VFX/!");
        }
    }

    private void CreateFallbackBlindingEffect(Vector3 position)
    {
        GameObject effectObj = new GameObject("BlindingEffect");
        effectObj.transform.position = position;

        // Beyaz flash effect
        SpriteRenderer sr = effectObj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateFlashSprite();
        sr.color = Color.white;
        sr.sortingLayerName = "UI";
        sr.sortingOrder = 10;

        // Flash animation
        effectObj.AddComponent<BlindingFlashEffect>();

        Object.Destroy(effectObj, 1f);
    }

    private Sprite CreateBlindingShotSprite()
    {
        // Gri renkte özel ok sprite'ı oluştur
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
                        pixelColor = new Color(0.7f, 0.7f, 0.8f, 1f); // Gri-mavi ton
                    }
                }
                else if (x >= width * 0.4f && x < width * 0.6f)
                {
                    pixelColor = new Color(0.5f, 0.5f, 0.6f, 1f); // Koyu gri
                }

                colors[y * width + x] = pixelColor;
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
    }

    private Sprite CreateFlashSprite()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] colors = new Color[size * size];

        Vector2 center = new Vector2(size / 2, size / 2);

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);
                float maxDistance = size / 2f;

                float alpha = distance < maxDistance ?
                    Mathf.Clamp01(1f - distance / maxDistance) : 0f;

                colors[y * size + x] = new Color(1f, 1f, 1f, alpha * 0.8f);
            }
        }

        texture.SetPixels(colors);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
public override void ExecuteClientImmediate(GameObject caster, SkillInstance skillInstance)
{
    // Immediate client-side animation + VFX (damage yok)
    var character4D = caster.GetComponent<Character4D>();
    if (character4D != null)
    {
        character4D.AnimationManager.ShotBow();
    }
    
    // Find target for VFX direction
    GameObject target = FindNearestTarget(caster);
    
    Vector2 direction = character4D?.Direction ?? Vector2.down;
    
    if (target != null)
    {
        direction = (target.transform.position - caster.transform.position).normalized;
        
        // Character direction güncelle
        if (character4D != null)
        {
            if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
            {
                character4D.SetDirection(direction.x > 0 ? Vector2.right : Vector2.left);
            }
            else
            {
                character4D.SetDirection(direction.y > 0 ? Vector2.up : Vector2.down);
            }
        }
    }
    
    // Immediate VFX spawn (local)
    SpawnBlindingShotProjectile(
        caster.transform.position, 
        direction, 
        target != null, 
        target != null ? target.transform.position : Vector3.zero
    );
    
    // YENİ: Client-side cooldown hemen başlat
    skillInstance.lastUsedTime = Time.time;
}
}


public class BlindingShotProjectileMovement : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private bool shouldHitTarget;
    private Vector3 targetPosition;
    private float lifetime = 0f;
    private float maxLifetime = 2f;
    private bool isHoming = false;
    
public void Initialize(Vector2 dir, float spd, bool hitTarget, Vector3 targetPos)
{
    direction = dir.normalized;
    speed = spd;
    shouldHitTarget = hitTarget;
    targetPosition = targetPos;
    isHoming = hitTarget && targetPos != Vector3.zero;
    
    
    // Rotation ayarla
    UpdateRotation();
}
    
private void Update()
{
    // Saniyede 1 kez log at
    if (Time.time % 1f < Time.deltaTime)
    {
    }
    
    lifetime += Time.deltaTime;
    
    if (lifetime >= maxLifetime)
    {
        Destroy(gameObject);
        return;
    }
    
    // Homing behavior - hedefe doğru yön güncelle
    if (isHoming && shouldHitTarget)
    {
        Vector2 toTarget = (targetPosition - transform.position).normalized;
        
        // Smooth direction change
        direction = Vector2.Lerp(direction, toTarget, Time.deltaTime * 5f).normalized;
        UpdateRotation();
    }
    
    // Hareket
    Vector3 movement = (Vector3)(direction * speed * Time.deltaTime);
    transform.position += movement;
    
    // Hedefe ulaştı mı kontrol et
    if (shouldHitTarget && Vector3.Distance(transform.position, targetPosition) < 0.3f)
    {
        Destroy(gameObject);
    }
}
    
    private void UpdateRotation()
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
    }
}

// Flash effect component
public class BlindingFlashEffect : MonoBehaviour
{
    private SpriteRenderer sr;
    private float duration = 0.5f;
    private float elapsed = 0f;
    
    private void Start()
    {
        sr = GetComponent<SpriteRenderer>();
    }
    
    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / duration;
        
        // Flash fade out
        Color color = sr.color;
        color.a = Mathf.Lerp(1f, 0f, progress);
        sr.color = color;
        
        // Scale pulse
        float scale = 1f + Mathf.Sin(progress * Mathf.PI * 3) * 0.3f;
        transform.localScale = Vector3.one * scale;
        
        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }
}