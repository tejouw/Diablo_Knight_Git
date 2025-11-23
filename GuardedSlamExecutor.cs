using UnityEngine;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;

public class GuardedSlamExecutor : BaseSkillExecutor
{
    public override string SkillId => "guarded_slam";
    
    private const float DAMAGE_MULTIPLIER = 1.6f; // %160 damage
    private const float MELEE_RANGE = 4f; // Standard melee range
    private const float DAMAGE_REDUCTION = 10f; // %10 damage reduction
    private const float BUFF_DURATION = 3f; 
    public override void Execute(GameObject caster, SkillInstance skillInstance)
    {
        // Client-side sadece VFX
        var character4D = caster.GetComponent<Character4D>();
        if (character4D != null)
        {
            character4D.AnimationManager.HeavySlash1H();
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
        
        // Find closest target
        GameObject closestTarget = FindClosestTarget(caster);
        bool hitTarget = false;
        
        if (closestTarget != null)
        {
            // Calculate damage
            float damage = playerStats.FinalDamage * DAMAGE_MULTIPLIER;
            
            // Apply damage
            ApplyDamageToTarget(caster, closestTarget, damage, skillSystem);
            hitTarget = true;
        }
        
        // Apply damage reduction buff only if hit target
        if (hitTarget)
        {
            ApplyDamageReductionBuff(caster);
        }
        
        // Execute VFX on all clients
        skillSystem.ExecuteGuardedSlamRPC(caster.transform.position, character4D.Direction, hitTarget, closestTarget?.transform.position ?? Vector3.zero);
    }
    
private GameObject FindClosestTarget(GameObject caster)
{
    var character4D = caster.GetComponent<Character4D>();
    Vector2 direction = character4D?.Direction ?? Vector2.down;
    
    return SkillTargetingUtils.FindClosestTarget(caster.transform.position, MELEE_RANGE, caster, direction, 0.7f);
}
    private void ApplyDamageReductionBuff(GameObject caster)
    {
        var tempBuff = caster.GetComponent<TemporaryBuffSystem>();
        if (tempBuff == null)
        {
            tempBuff = caster.gameObject.AddComponent<TemporaryBuffSystem>();
        }
        
        tempBuff.ApplyDamageReductionBuff(DAMAGE_REDUCTION, BUFF_DURATION);
    }
    
    public void ExecuteVFX(GameObject caster, Vector3 position, Vector2 direction, bool hitTarget, Vector3 targetPosition)
    {
        var character4D = caster.GetComponent<Character4D>();
        
        // Character animation
        if (character4D != null)
        {
            character4D.AnimationManager.HeavySlash1H();
        }
        
        // VFX spawning
        if (hitTarget)
        {
            SpawnImpactVFX(targetPosition);
            SpawnDefenseAuraVFX(caster);
        }
        
        SpawnSlamEffectVFX(position, direction);
    }
    
    private void SpawnImpactVFX(Vector3 position)
    {
        var prefab = Resources.Load<GameObject>("VFX/GuardedSlam_Impact");
        if (prefab != null)
        {
            GameObject effect = Object.Instantiate(prefab, position, Quaternion.identity);
            Object.Destroy(effect, 1f);
        }
    }
    
    private void SpawnDefenseAuraVFX(GameObject caster)
    {
        var prefab = Resources.Load<GameObject>("VFX/GuardedSlam_DefenseAura");
        if (prefab != null)
        {
            GameObject effect = Object.Instantiate(prefab, caster.transform.position, Quaternion.identity);
            effect.transform.SetParent(caster.transform);
            Object.Destroy(effect, BUFF_DURATION);
        }
    }
    
    private void SpawnSlamEffectVFX(Vector3 position, Vector2 direction)
    {
        var prefab = Resources.Load<GameObject>("VFX/GuardedSlam_SlamEffect");
        if (prefab != null)
        {
            Vector3 effectPosition = position + (Vector3)(direction * 1f);
            GameObject effect = Object.Instantiate(prefab, effectPosition, Quaternion.identity);
            
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            effect.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            
            Object.Destroy(effect, 1.5f);
        }
    }
}