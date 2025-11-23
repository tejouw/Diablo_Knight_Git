using UnityEngine;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Fusion;

public class PiercingArrowExecutor : BaseSkillExecutor
{
    public override string SkillId => "piercing_arrow";
    
    private const float DAMAGE_MULTIPLIER = 1.5f; // %150 damage
    private const float SKILL_RANGE = 8f; // 4 tile = ~8 units
    private const float ATTACK_SPEED_BUFF = 1.25f; // %25 increase
    private const float BUFF_DURATION = 1f; // 1 saniye
    private const float PROJECTILE_SPEED = 15f; // Hızlı projectile
    private const float PROJECTILE_WIDTH = 1f; // Line genişliği
    
    public override void Execute(GameObject caster, SkillInstance skillInstance)
    {
        // Client-side charge animation
        var character4D = caster.GetComponent<Character4D>();
        if (character4D != null)
        {
            character4D.AnimationManager.ShotBow(); // Charge animation
        }
    }

    // PiercingArrowExecutor.cs - Bu metodu değiştir
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

        // Line targeting direction
        Vector2 direction = character4D.Direction;
        Vector3 startPosition = caster.transform.position;

        // ✅ HEMEN damage uygula - delay yok
        ApplyInstantPiercingDamage(caster, startPosition, direction, skillSystem);

        // ✅ Visual projectile spawn et (sadece görsel)
        skillSystem.ExecutePiercingArrowRPC(
            startPosition,
            direction,
            -1, // -1 = sadece visual projectile
            false
        );
    }
private void ApplyInstantPiercingDamage(GameObject caster, Vector3 startPosition, Vector2 direction, SkillSystem skillSystem)
{
    // Caster kontrol
    if (caster == null || skillSystem == null) return;

    var playerStats = GetPlayerStats(caster);
    if (playerStats == null) return;

    // Calculate damage
    float baseDamage = playerStats.FinalDamage * DAMAGE_MULTIPLIER;

    // Find targets in line
    List<GameObject> targetsInLine = FindTargetsInLine(caster, startPosition, direction);

    // Gerçek target pozisyonlarını topla
    Vector3[] targetPositions = new Vector3[targetsInLine.Count];

    // Apply damage to all targets ve pozisyonları kaydet
    for (int i = 0; i < targetsInLine.Count; i++)
    {
        var target = targetsInLine[i];
        ApplyDamageToTarget(caster, target, baseDamage, skillSystem);
        targetPositions[i] = target.transform.position;
    }

    // Attack speed buff if 2+ targets hit
    bool hasAttackSpeedBuff = false;
    if (targetsInLine.Count >= 2)
    {
        ApplyAttackSpeedBuff(caster);
        hasAttackSpeedBuff = true;
    }

    // ✅ Hit effects için VFX çağır
    skillSystem.ExecutePiercingArrowHitEffectsRPC(
        startPosition,
        direction,
        targetPositions,
        hasAttackSpeedBuff
    );
}
// PiercingArrowExecutor.cs - Bu metodu override keyword ile değiştir
public override void ExecuteClientImmediate(GameObject caster, SkillInstance skillInstance)
{
    // Hemen client-side animation başlat
    var character4D = caster.GetComponent<Character4D>();
    if (character4D != null)
    {
        character4D.AnimationManager.ShotBow();
    }
    
    // Hemen visual projectile spawn et (damage yok, sadece visual)
    Vector3 startPos = caster.transform.position;
    Vector2 direction = character4D != null ? character4D.Direction : Vector2.right;
    
    SpawnPiercingProjectile(startPos, direction, 0); // 0 = visual only
    
    // Client-side cooldown hemen başlat (server validation ile sync olacak)
    skillInstance.lastUsedTime = Time.time;
}

// ✅ YENİ METOD - Hit effects için ayrı - gerçek pozisyonlarla
public void ExecuteHitEffectsVFX(GameObject caster, Vector3 startPos, Vector2 direction, Vector3[] targetPositions, bool hasAttackSpeedBuff)
{
    // Attack speed buff VFX
    if (hasAttackSpeedBuff)
    {
        SpawnAttackSpeedBuffVFX(caster);
    }

    // ✅ Gerçek target pozisyonlarında hit effects spawn et
    foreach (Vector3 targetPos in targetPositions)
    {
        CreatePierceEffect(targetPos);
        CreateGroundCrackEffect(targetPos, direction);
    }

    // Final impact
    Vector3 finalPos = startPos + (Vector3)(direction * SKILL_RANGE);
    CreateImpactEffect(finalPos);
}
    
private List<GameObject> FindTargetsInLine(GameObject caster, Vector3 startPos, Vector2 direction)
{
    Vector3 endPos = startPos + (Vector3)(direction.normalized * SKILL_RANGE);
    return SkillTargetingUtils.FindTargetsInLine(startPos, endPos, PROJECTILE_WIDTH, caster);
}
   private void ApplyAttackSpeedBuff(GameObject caster)
    {
        var tempBuff = caster.GetComponent<TemporaryBuffSystem>();
        if (tempBuff == null)
        {
            tempBuff = caster.gameObject.AddComponent<TemporaryBuffSystem>();
        }
        
        tempBuff.ApplyAttackSpeedBuff(ATTACK_SPEED_BUFF, BUFF_DURATION);
    }


    // ✅ YENİ METOD - Hit effects için ayrı
    public void ExecuteHitEffectsVFX(GameObject caster, Vector3 startPos, Vector2 direction, int targetCount, bool hasAttackSpeedBuff)
    {
        // Attack speed buff VFX
        if (hasAttackSpeedBuff)
        {
            SpawnAttackSpeedBuffVFX(caster);
        }

        // ✅ Hit effect'lerini burada manuel spawn et çünkü projectile delay'li
        for (int i = 0; i < targetCount; i++)
        {
            // Target pozisyonlarını line üzerinde hesapla
            Vector3 targetPos = startPos + (Vector3)(direction * (2f + i * 1.5f));
            CreatePierceEffect(targetPos);
            CreateGroundCrackEffect(targetPos, direction);
        }

        // Final impact
        Vector3 finalPos = startPos + (Vector3)(direction * SKILL_RANGE);
        CreateImpactEffect(finalPos);
    }
private void CreatePierceEffect(Vector3 position)
{
    var prefab = Resources.Load<GameObject>("VFX/PiercingArrow_Pierce");
    if (prefab != null)
    {
        GameObject effect = Object.Instantiate(prefab, position, Quaternion.identity);
        Object.Destroy(effect, 1f);
    }
}

private void CreateGroundCrackEffect(Vector3 position, Vector2 direction)
{
    var prefab = Resources.Load<GameObject>("VFX/PiercingArrow_GroundCrack");
    if (prefab != null)
    {
        GameObject crack = Object.Instantiate(prefab, position, Quaternion.identity);
        
        // Direction'a göre crack orientation
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        crack.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        
        Object.Destroy(crack, 2f);
    }
}

private void CreateImpactEffect(Vector3 position)
{
    var prefab = Resources.Load<GameObject>("VFX/PiercingArrow_Impact");
    if (prefab != null)
    {
        GameObject impact = Object.Instantiate(prefab, position, Quaternion.identity);
        Object.Destroy(impact, 1f);
    }
}
    
private void SpawnPiercingProjectile(Vector3 startPos, Vector2 direction, int targetCount)
{
    var prefab = Resources.Load<GameObject>("VFX/PiercingArrow_Projectile");
    if (prefab != null)
    {
        Vector3 spawnPosition = startPos + Vector3.up * 0.5f;
        GameObject projectile = Object.Instantiate(prefab, spawnPosition, Quaternion.identity);
        
        // Direction'a göre rotation
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        projectile.transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
        
        // Projectile movement component
        var movement = projectile.GetComponent<PiercingArrowMovement>();
        if (movement == null)
        {
            movement = projectile.AddComponent<PiercingArrowMovement>();
        }
        
        // ✅ Hep visual only yap - damage connection yok
        movement.Initialize(direction, PROJECTILE_SPEED, SKILL_RANGE, 0, true); // visual only = true
        
        // Auto-destroy
        Object.Destroy(projectile, 3f);
    }
}
    public void ExecuteVFX(GameObject caster, Vector3 startPos, Vector2 direction, int targetCount, bool hasAttackSpeedBuff)
{
    var character4D = caster.GetComponent<Character4D>();
    
    // Character charge animation
    if (character4D != null)
    {
        character4D.AnimationManager.ShotBow();
    }
    
    // ✅ Tüm clientlar için visual projectile spawn et
    var networkObj = caster.GetComponent<NetworkObject>();
    bool isLocalPlayer = networkObj != null && networkObj.HasInputAuthority;
    
    if (!isLocalPlayer)
    {
        // Sadece remote clientlar için projectile spawn et
        SpawnPiercingProjectile(startPos, direction, 0); // 0 = visual only
    }
}
    private void SpawnAttackSpeedBuffVFX(GameObject caster)
    {
        var prefab = Resources.Load<GameObject>("VFX/AttackSpeed_Buff");
        if (prefab != null)
        {
            GameObject buffEffect = Object.Instantiate(prefab);
            
            // Caster'a bağla
            buffEffect.transform.SetParent(caster.transform);
            buffEffect.transform.localPosition = Vector3.zero;
            
            // Particle systems'i local space'e çevir
            ParticleSystem[] allParticles = buffEffect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allParticles)
            {
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                ps.Clear();
                ps.Play();
            }
            
            Object.Destroy(buffEffect, BUFF_DURATION);
        }
    }
}

// Projectile movement component
// Projectile movement component
public class PiercingArrowMovement : MonoBehaviour
{
    private Vector2 direction;
    private float speed;
    private float maxRange;
    private float travelDistance = 0f;
    private bool isMoving = false;
    
// PiercingArrowExecutor.cs - PiercingArrowMovement class'ındaki bu metodu değiştir
public void Initialize(Vector2 dir, float spd, float range, int expectedTargets, bool visualOnly = false)
{
    direction = dir.normalized;
    speed = spd;
    maxRange = range;
    isMoving = true;
    
    // Visual only ise collision detection'ı kapat
    if (visualOnly)
    {
        var collider = GetComponent<Collider2D>();
        if (collider != null)
        {
            collider.enabled = false;
        }
    }
    
}

private void Update()
{
    if (!isMoving) return;

    
    // Move projectile
    Vector3 movement = (Vector3)(direction * speed * Time.deltaTime);
    transform.position += movement;
    travelDistance += movement.magnitude;
    
    // Check if reached max range
    if (travelDistance >= maxRange)
    {
        CreateImpactEffect();
        DestroyProjectile();
        return;
    }
}
    
private void OnTriggerEnter2D(Collider2D other)
{
    // ❌ VFX'leri kaldır - şimdi executor'da handle ediliyor
    
    // Sadece obstacle check kalsın
    if (other.CompareTag("Obstacle") || other.CompareTag("Wall"))
    {
        DestroyProjectile();
    }
}
    
    private void CreatePierceEffect(Vector3 position)
    {
        var prefab = Resources.Load<GameObject>("VFX/PiercingArrow_Pierce");
        if (prefab != null)
        {
            GameObject effect = Object.Instantiate(prefab, position, Quaternion.identity);
            Object.Destroy(effect, 1f);
        }
    }
    
    private void CreateGroundCrackEffect(Vector3 position)
    {
        var prefab = Resources.Load<GameObject>("VFX/PiercingArrow_GroundCrack");
        if (prefab != null)
        {
            GameObject crack = Object.Instantiate(prefab, position, Quaternion.identity);
            
            // Direction'a göre crack orientation
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            crack.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            
            Object.Destroy(crack, 2f);
        }
    }
    
    private void CreateImpactEffect()
    {
        var prefab = Resources.Load<GameObject>("VFX/PiercingArrow_Impact");
        if (prefab != null)
        {
            GameObject impact = Object.Instantiate(prefab, transform.position, Quaternion.identity);
            Object.Destroy(impact, 1f);
        }
    }
    
    private void DestroyProjectile()
    {
        isMoving = false;
        Destroy(gameObject);
    }
}