using UnityEngine;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Fusion;
public class CleaveStrikeExecutor : BaseSkillExecutor
{
    public override string SkillId => "cleave_strike";
    
    private const float DAMAGE_MULTIPLIER = 1.5f; // %150 damage
    private const float SINGLE_TARGET_BONUS = 1.2f; // %20 bonus for single target
    private const float CONE_ANGLE = 180f; 
private const float SKILL_RANGE = 8f; // 4'ten 8'e çıkar    
public override void Execute(GameObject caster, SkillInstance skillInstance)
{
    // SADECE animation - VFX artık ExecuteClientImmediate'de
    var character4D = caster.GetComponent<Character4D>();
    if (character4D != null)
    {
        character4D.AnimationManager.Slash(true);
    }
}

    public void ExecuteOnServer(GameObject caster, SkillInstance skillInstance, SkillSystem skillSystem)
    {

        // ✅ SADECE BURADA COOLDOWN SET ET
        skillInstance.lastUsedTime = Time.time;

        var playerStats = GetPlayerStats(caster);
        var character4D = caster.GetComponent<Character4D>();

        if (playerStats == null || character4D == null)
        {
            Debug.LogError($"[CleaveStrike-SERVER] Missing components!");
            return;
        }

        // Damage calculation...
        float baseDamage = playerStats.FinalDamage * DAMAGE_MULTIPLIER;

        // Find targets in cone
        List<GameObject> targetsInCone = FindTargetsInCone(caster, character4D);

        // Single target bonus
        if (targetsInCone.Count == 1)
        {
            baseDamage *= SINGLE_TARGET_BONUS;
        }

        // Apply damage (only on server)
        if (skillSystem.Object.HasStateAuthority)
        {
            foreach (var target in targetsInCone)
            {
                ApplyDamageToTarget(caster, target, baseDamage, skillSystem);
            }
        }
        else
        {
            Debug.LogWarning("[CleaveStrike-SERVER] No state authority!");
        }

        // Execute visual effects on all clients
        skillSystem.ExecuteSkillVFXRPC(SkillId, caster.transform.position, character4D.Direction, targetsInCone.Count);

    }
// Bu metodu CleaveStrikeExecutor.cs'e EKLE
public override void ExecuteClientImmediate(GameObject caster, SkillInstance skillInstance)
{
    // Sadece client-side animation + immediate VFX (damage yok)
    var character4D = caster.GetComponent<Character4D>();
    if (character4D != null)
    {
        character4D.AnimationManager.Slash(true);
    }
    
    // Immediate local VFX spawn
    SpawnWeaponTrailVFX(caster.transform.position, character4D?.Direction ?? Vector2.down);
    SpawnGroundEffectVFX(caster.transform.position, character4D?.Direction ?? Vector2.down);
    
    // Client-side cooldown set (server ile sync olacak)
    skillInstance.lastUsedTime = Time.time;
}
private List<GameObject> FindTargetsInCone(GameObject caster, Character4D character4D)
{
    Vector3 origin = caster.transform.position;
    Vector2 direction = character4D.Direction;
    
    return SkillTargetingUtils.FindTargetsInCone(origin, direction, CONE_ANGLE, SKILL_RANGE, caster);
}
    
public void ExecuteVFX(GameObject caster, Vector3 position, Vector2 direction, int targetCount)
{
    var character4D = caster.GetComponent<Character4D>();
    
    // Character animation
    if (character4D != null)
    {
        character4D.AnimationManager.Slash(true);
    }
    
    // ✅ Skill'i atan client ise duplicate VFX spawn etme
    var networkObj = caster.GetComponent<NetworkObject>();
    bool isLocalPlayer = networkObj != null && networkObj.HasInputAuthority;
    
    if (!isLocalPlayer)
    {
        // Sadece remote clientlar için VFX spawn et
        SpawnWeaponTrailVFX(position, direction);
        SpawnGroundEffectVFX(position, direction);
    }
}
    
    private void SpawnWeaponTrailVFX(Vector3 position, Vector2 direction)
    {
        var prefab = Resources.Load<GameObject>("VFX/CleaveStrike_WeaponTrail");
        if (prefab != null)
        {
            Vector3 effectPosition = position + new Vector3(direction.x * 0.5f, direction.y * 0.5f + 0.5f, 0);
            GameObject effect = Object.Instantiate(prefab, effectPosition, Quaternion.identity);
            
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            effect.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }
    }
    
private void SpawnGroundEffectVFX(Vector3 position, Vector2 direction)
{
    var prefab = Resources.Load<GameObject>("VFX/CleaveStrike_GroundEffect");
    if (prefab != null)
    {
        // DÜZELTME: Preview ile aynı pozisyon offset'i
        Vector3 effectPosition = position + (Vector3)(direction * 0.5f);
        GameObject effect = Object.Instantiate(prefab, effectPosition, Quaternion.identity);
        
        // DÜZELTME: Preview ile aynı rotation hesabı
        float angle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
        effect.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        
        // DÜZELTME: Skill range'e göre scale ayarla
        ScaleEffectToSkillArea(effect);
        
        // Auto-destroy (mevcut)
        Object.Destroy(effect, 2f);
    }
}

private void ScaleEffectToSkillArea(GameObject effect)
{
    // CONE_ANGLE ve SKILL_RANGE bilgilerini kullanarak tam boyut hesapla
    
    // Cone'un en geniş noktasındaki genişlik
    float coneWidthAtMaxRange = 2f * SKILL_RANGE * Mathf.Tan((CONE_ANGLE / 2f) * Mathf.Deg2Rad);
    
    // Prefab'ın mevcut boyutunu al
    Bounds effectBounds = GetEffectBounds(effect);
    
    if (effectBounds.size.magnitude > 0.1f)
    {
        // X ve Y ekseni için ayrı scale hesapla
        float scaleX = coneWidthAtMaxRange / effectBounds.size.x;
        float scaleY = SKILL_RANGE / effectBounds.size.y;
        
        // Ortalama scale kullan veya minimum al
        float finalScale = Mathf.Min(scaleX, scaleY);
        finalScale = Mathf.Clamp(finalScale, 0.3f, 4f);
        
        effect.transform.localScale = Vector3.one * finalScale;
    }
    else
    {
        // Fallback scale
        effect.transform.localScale = Vector3.one * (SKILL_RANGE / 4f);
    }
}

private Bounds GetEffectBounds(GameObject effect)
{
    Bounds bounds = new Bounds();
    bool hasBounds = false;
    
    // Sprite renderer'ları kontrol et
    SpriteRenderer[] renderers = effect.GetComponentsInChildren<SpriteRenderer>();
    foreach (SpriteRenderer renderer in renderers)
    {
        if (renderer.sprite != null)
        {
            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
    }
    
    // Particle system'leri kontrol et
    ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>();
    foreach (ParticleSystem ps in particles)
    {
        var shape = ps.shape;
        if (shape.enabled)
        {
            Vector3 shapeSize = shape.scale;
            Bounds particleBounds = new Bounds(ps.transform.position, shapeSize);
            
            if (!hasBounds)
            {
                bounds = particleBounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(particleBounds);
            }
        }
    }
    
    return bounds;
}
}