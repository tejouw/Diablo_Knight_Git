using UnityEngine;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Collections;

public class RainOfArrowsExecutor : BaseSkillExecutor
{
    public override string SkillId => "rain_of_arrows";
    
    private const float DAMAGE_MULTIPLIER = 3.0f; // %300 damage
    private const float AOE_RADIUS = 4f; // 2 tile çapında
    private const float CAST_RANGE = 6f; // Oyuncunun 6 unit önünde
    private const float ARROW_COUNT = 6f;
    private const float TOTAL_DURATION = 1.5f; // 1.5 saniye toplam süre
    private const float SLOW_PERCENT = 20f; // %20 yavaşlatma
    private const float SLOW_DURATION = 1f; // 1 saniye slow
    private const float INDIVIDUAL_ARROW_RADIUS = 1.5f; // Her okun kendi AOE'si
    
    public override void Execute(GameObject caster, SkillInstance skillInstance)
    {
        // Client-side sadece casting animation
        var character4D = caster.GetComponent<Character4D>();
        if (character4D != null)
        {
            character4D.AnimationManager.ShotBow(); // Casting animation
        }
    }
    
    public void ExecuteOnServer(GameObject caster, SkillInstance skillInstance, SkillSystem skillSystem)
    {
        if (!skillSystem.Object.HasStateAuthority) return;
        
        // Server-side cooldown set
        skillInstance.lastUsedTime = Time.time;
        
        var playerStats = GetPlayerStats(caster);
        var character4D = caster.GetComponent<Character4D>();
        
        if (playerStats == null || character4D == null)
        {
            return;
        }
        
        // Calculate AOE center position
        Vector3 aoeCenter = CalculateAOECenter(caster.transform.position, character4D.Direction);
        
        // Calculate damage
        float baseDamage = playerStats.FinalDamage * DAMAGE_MULTIPLIER;
        
        // Pre-calculate all arrow positions for network sync
        Vector3[] arrowPositions = GenerateArrowPositions(aoeCenter, ARROW_COUNT);
        
        // Start the rain of arrows coroutine
        skillSystem.StartCoroutine(ExecuteRainOfArrowsCoroutine(caster, baseDamage, aoeCenter, arrowPositions, skillSystem));
        
        // Execute VFX on all clients immediately (area indicator)
        skillSystem.ExecuteRainOfArrowsRPC(aoeCenter, arrowPositions, character4D.Direction);
    }
    
    private Vector3 CalculateAOECenter(Vector3 casterPosition, Vector2 direction)
    {
        // Oyuncunun önünde CAST_RANGE kadar uzakta AOE merkezi
        Vector3 targetCenter = casterPosition + (Vector3)(direction.normalized * CAST_RANGE);
        
        // Optional: Obstacle check for AOE center
        return targetCenter;
    }
    
    private Vector3[] GenerateArrowPositions(Vector3 aoeCenter, float arrowCount)
    {
        Vector3[] positions = new Vector3[(int)arrowCount];
        
        for (int i = 0; i < arrowCount; i++)
        {
            // Random position within AOE circle
            float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float randomRadius = Random.Range(0f, AOE_RADIUS * 0.8f); // %80'i kullan overlap için
            
            Vector3 randomOffset = new Vector3(
                Mathf.Cos(randomAngle) * randomRadius,
                Mathf.Sin(randomAngle) * randomRadius,
                0f
            );
            
            positions[i] = aoeCenter + randomOffset;
        }
        
        return positions;
    }
    
    private IEnumerator ExecuteRainOfArrowsCoroutine(GameObject caster, float baseDamage, Vector3 aoeCenter, Vector3[] arrowPositions, SkillSystem skillSystem)
    {
        float arrowInterval = TOTAL_DURATION / ARROW_COUNT;
        
        for (int i = 0; i < arrowPositions.Length; i++)
        {
            // Her ok için damage uygula
            ApplyArrowDamage(caster, arrowPositions[i], baseDamage, skillSystem);
            
            // Arrow impact VFX
            skillSystem.ExecuteArrowImpactRPC(arrowPositions[i], i);
            
            yield return new WaitForSeconds(arrowInterval);
        }
    }
    
    private void ApplyArrowDamage(GameObject caster, Vector3 arrowPosition, float damage, SkillSystem skillSystem)
    {
        // Find targets in individual arrow AOE
        List<GameObject> targetsInArrowArea = FindTargetsInCircleArea(arrowPosition, INDIVIDUAL_ARROW_RADIUS, caster);
        
        foreach (var target in targetsInArrowArea)
        {
            // Apply damage
            ApplyDamageToTarget(caster, target, damage, skillSystem);
            
            // Apply slow debuff
            ApplySlowDebuffToTarget(target);
        }
    }
private List<GameObject> FindTargetsInCircleArea(Vector3 center, float radius, GameObject caster)
{
    return SkillTargetingUtils.FindTargetsInCircle(center, radius, caster);
}
    
    private void ApplySlowDebuffToTarget(GameObject target)
    {
        // Monster slow
        if (target.CompareTag("Monster"))
        {
            var monsterBehaviour = target.GetComponent<MonsterBehaviour>();
            if (monsterBehaviour != null)
            {
                monsterBehaviour.ApplySlowDebuff(SLOW_PERCENT, SLOW_DURATION);
            }
            return;
        }
        
        // Player slow
        if (target.CompareTag("Player"))
        {
            var tempBuff = target.GetComponent<TemporaryBuffSystem>();
            if (tempBuff == null)
            {
                tempBuff = target.gameObject.AddComponent<TemporaryBuffSystem>();
            }
            
            if (tempBuff != null)
            {
                tempBuff.ApplySlowDebuff(SLOW_PERCENT, SLOW_DURATION);
            }
        }
    }
    
    public void ExecuteVFX(GameObject caster, Vector3 aoeCenter, Vector3[] arrowPositions, Vector2 direction)
    {
        var character4D = caster.GetComponent<Character4D>();
        
        // Character animation
        if (character4D != null)
        {
            character4D.AnimationManager.ShotBow();
        }
        
        // Spawn area indicator VFX
        SpawnAreaIndicatorVFX(aoeCenter);
        
        // Start arrow rain VFX sequence
        caster.GetComponent<SkillSystem>()?.StartCoroutine(ArrowRainVFXSequence(arrowPositions));
    }
    
    public void ExecuteArrowImpactVFX(Vector3 position, int arrowIndex)
    {
        // Spawn individual arrow impact
        SpawnArrowImpactVFX(position);
        SpawnGroundDustVFX(position);
    }
    
private void SpawnAreaIndicatorVFX(Vector3 center)
{
    var prefab = Resources.Load<GameObject>("VFX/RainOfArrows_AreaIndicator");
    if (prefab != null)
    {
        GameObject indicator = Object.Instantiate(prefab, center, Quaternion.identity);
        
        // Physics interaction'ını devre dışı bırak
        SetVFXNonInteractive(indicator);
        
        // Scale to AOE size
        indicator.transform.localScale = Vector3.one * (AOE_RADIUS / 2f);
        
        Object.Destroy(indicator, TOTAL_DURATION + 0.5f);
    }
}
    
    private IEnumerator ArrowRainVFXSequence(Vector3[] arrowPositions)
    {
        float arrowInterval = TOTAL_DURATION / ARROW_COUNT;
        
        for (int i = 0; i < arrowPositions.Length; i++)
        {
            // Spawn falling arrow VFX
            SpawnFallingArrowVFX(arrowPositions[i]);
            
            yield return new WaitForSeconds(arrowInterval);
        }
    }
    public override void ExecuteClientImmediate(GameObject caster, SkillInstance skillInstance)
{
    // Client-side sadece casting animation
    var character4D = caster.GetComponent<Character4D>();
    if (character4D != null)
    {
        character4D.AnimationManager.ShotBow();
    }
    
    // Client-side cooldown hemen başlat
    skillInstance.lastUsedTime = Time.time;
}
private void SpawnFallingArrowVFX(Vector3 position)
{
    var prefab = Resources.Load<GameObject>("VFX/RainOfArrows_FallingArrow");
    if (prefab != null)
    {
        // Start from sky position
        Vector3 skyPosition = position + Vector3.up * 10f;
        GameObject arrow = Object.Instantiate(prefab, skyPosition, Quaternion.identity);
        
        // Physics interaction'ını devre dışı bırak
        SetVFXNonInteractive(arrow);
        
        // Add falling movement component
        arrow.AddComponent<FallingArrowVFX>().Initialize(position, 0.25f);
        
        Object.Destroy(arrow, 2f);
    }
}
    
private void SpawnArrowImpactVFX(Vector3 position)
{
    var prefab = Resources.Load<GameObject>("VFX/RainOfArrows_ArrowImpact");
    if (prefab != null)
    {
        GameObject impact = Object.Instantiate(prefab, position, Quaternion.identity);
        
        // Physics interaction'ını devre dışı bırak
        SetVFXNonInteractive(impact);
        
        Object.Destroy(impact, 1f);
    }
}

private void SpawnGroundDustVFX(Vector3 position)
{
    var prefab = Resources.Load<GameObject>("VFX/RainOfArrows_GroundDust");
    if (prefab != null)
    {
        GameObject dust = Object.Instantiate(prefab, position, Quaternion.identity);
        
        // Physics interaction'ını devre dışı bırak
        SetVFXNonInteractive(dust);
        
        Object.Destroy(dust, 0.8f);
    }
}

// Yeni metod ekle - VFX'leri non-interactive yapar
private void SetVFXNonInteractive(GameObject vfxObject)
{
    // VFX layer'ına koy (Default layer 0'ı kullan ya da özel VFX layer oluştur)
    vfxObject.layer = 0; // Default layer - hiçbir şeyle collision yapmaz
    
    // Tüm child object'leri de aynı layer'a koy
    Transform[] allChildren = vfxObject.GetComponentsInChildren<Transform>(true);
    foreach (Transform child in allChildren)
    {
        child.gameObject.layer = 0;
    }
    
    // Collider'ları devre dışı bırak
    Collider2D[] colliders = vfxObject.GetComponentsInChildren<Collider2D>(true);
    foreach (Collider2D col in colliders)
    {
        col.enabled = false;
    }
    
    // Rigidbody'leri kinematic yap
    Rigidbody2D[] rigidbodies = vfxObject.GetComponentsInChildren<Rigidbody2D>(true);
    foreach (Rigidbody2D rb in rigidbodies)
    {
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = false;
    }
    
    // ParticleSystem collision'larını kapat
    ParticleSystem[] particles = vfxObject.GetComponentsInChildren<ParticleSystem>(true);
    foreach (ParticleSystem ps in particles)
    {
        var collision = ps.collision;
        collision.enabled = false;
    }
}
}

// Helper component for falling arrow VFX
public class FallingArrowVFX : MonoBehaviour
{
    private Vector3 targetPosition;
    private float fallDuration;
    private Vector3 startPosition;
    private float elapsed = 0f;
    
    public void Initialize(Vector3 target, float duration)
    {
        targetPosition = target;
        fallDuration = duration;
        startPosition = transform.position;
        
        // Point arrow downward
        transform.rotation = Quaternion.AngleAxis(-90f, Vector3.forward);
    }
    
    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / fallDuration;
        
        if (progress >= 1f)
        {
            transform.position = targetPosition;
            return;
        }
        
        // Lerp with gravity curve
        Vector3 currentPos = Vector3.Lerp(startPosition, targetPosition, progress);
        transform.position = currentPos;
    }
}