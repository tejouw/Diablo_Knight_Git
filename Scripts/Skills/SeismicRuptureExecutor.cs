// Assets/Game/Scripts/SeismicRuptureExecutor.cs - YENİ DOSYA

using UnityEngine;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Fusion.Addons.Physics;
using System.Collections;


public class SeismicRuptureExecutor : BaseSkillExecutor
{
    public override string SkillId => "seismic_rupture";
    
    private const float DAMAGE_MULTIPLIER = 3.0f; // %300 damage
    private const float CONE_ANGLE = 120f; 
    private const float SKILL_RANGE = 6f; // 3 tile = ~6 units
    private const float KNOCKBACK_FORCE = 2f; // 1 tile knockback
    private const float SLOW_PERCENT = 25f; // %25 yavaşlatma
    private const float SLOW_DURATION = 2f; // 2 saniye
    
    public override void Execute(GameObject caster, SkillInstance skillInstance)
    {
        // Client-side sadece VFX
        var character4D = caster.GetComponent<Character4D>();
        if (character4D != null)
        {
            character4D.AnimationManager.HeavySlash1H(); // Ground slam animation
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
    
    // Calculate damage
    float baseDamage = playerStats.FinalDamage * DAMAGE_MULTIPLIER;
    
    // Find targets in cone
    List<GameObject> targetsInCone = FindTargetsInCone(caster, character4D);
    
    
    // Apply damage and effects
    foreach (var target in targetsInCone)
    {
        ApplyDamageToTarget(caster, target, baseDamage, skillSystem);
        ApplyKnockbackToTarget(target, caster.transform.position);
        ApplySlowDebuffToTarget(target);
    }
    
    // Execute VFX on all clients
    skillSystem.ExecuteSeismicRuptureRPC(
        caster.transform.position, 
        character4D.Direction, 
        targetsInCone.Count,
        GetTargetPositions(targetsInCone)
    );
}
private List<GameObject> FindTargetsInCone(GameObject caster, Character4D character4D)
{
    Vector3 origin = caster.transform.position;
    Vector2 direction = character4D.Direction;
    
    return SkillTargetingUtils.FindTargetsInCone(origin, direction, CONE_ANGLE, SKILL_RANGE, caster);
}

    private void ApplyKnockbackToTarget(GameObject target, Vector3 casterPosition)
    {
        Vector2 knockbackDirection = (target.transform.position - casterPosition).normalized;


        // Monster knockback
        if (target.CompareTag("Monster"))
        {
            var networkRB = target.GetComponent<NetworkRigidbody2D>();
            var monsterBehaviour = target.GetComponent<MonsterBehaviour>();

            if (networkRB != null && networkRB.Rigidbody != null && monsterBehaviour != null)
            {

                // Temporarily stop monster AI movement
                monsterBehaviour.StartCoroutine(DisableMonsterMovementTemporarily(monsterBehaviour, 0.5f));


                // Apply stronger knockback force
                Vector2 knockbackForce = knockbackDirection * (KNOCKBACK_FORCE * 5f); // 5x stronger
                networkRB.Rigidbody.AddForce(knockbackForce, ForceMode2D.Impulse);

            }
        }

        // Player knockback
        if (target.CompareTag("Player"))
        {
            var networkController = target.GetComponent<NetworkCharacterController>();
            var networkRB = target.GetComponent<NetworkRigidbody2D>();

            if (networkController != null && networkRB != null && networkRB.Rigidbody != null)
            {

                // Disable movement temporarily
                networkController.SetMovementEnabled(false);

                // Apply stronger knockback force  
                Vector2 knockbackForce = knockbackDirection * (KNOCKBACK_FORCE * 8f); // 8x stronger for players
                networkRB.Rigidbody.AddForce(knockbackForce, ForceMode2D.Impulse);


                // Re-enable movement after knockback
                var skillSystem = target.GetComponent<SkillSystem>();
                if (skillSystem != null)
                {
                    skillSystem.StartCoroutine(ReEnableMovementAfterDelay(networkController, 0.5f));
                }
            }
        }
    }
private IEnumerator DisableMonsterMovementTemporarily(MonsterBehaviour monster, float duration)
{
    // Store original state
    var originalState = monster.NetworkCoreData.GetState();
    
    
    // Monster'ın AI'sını geçici olarak durdur (implementation MonsterBehaviour'da olacak)
    monster.SetKnockbackState(true);
    
    yield return new WaitForSeconds(duration);
    
    // Re-enable monster movement
    monster.SetKnockbackState(false);
    
}
    
    private System.Collections.IEnumerator ReEnableMovementAfterDelay(NetworkCharacterController controller, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (controller != null)
        {
            controller.SetMovementEnabled(true);
        }
    }
// SeismicRuptureExecutor.cs - ApplySlowDebuffToTarget METHOD'UNU DEĞİŞTİR

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
    
    private Vector3[] GetTargetPositions(List<GameObject> targets)
    {
        Vector3[] positions = new Vector3[targets.Count];
        for (int i = 0; i < targets.Count; i++)
        {
            positions[i] = targets[i].transform.position;
        }
        return positions;
    }
    
    public void ExecuteVFX(GameObject caster, Vector3 position, Vector2 direction, int targetCount, Vector3[] targetPositions)
    {
        var character4D = caster.GetComponent<Character4D>();
        
        // Character animation
        if (character4D != null)
        {
            character4D.AnimationManager.HeavySlash1H();
        }
        
        // VFX spawning
        SpawnGroundCrackVFX(position, direction);
        SpawnDebrisVFX(position, direction);
        SpawnScreenShakeVFX();
        
        // Target hit effects
        foreach (Vector3 targetPos in targetPositions)
        {
            SpawnKnockbackVFX(targetPos);
        }
    }
    
    private void SpawnGroundCrackVFX(Vector3 position, Vector2 direction)
    {
        var prefab = Resources.Load<GameObject>("VFX/SeismicRupture_GroundCracks");
        if (prefab != null)
        {
            Vector3 effectPosition = position + (Vector3)(direction * 2f); // Biraz önde
            GameObject effect = Object.Instantiate(prefab, effectPosition, Quaternion.identity);
            
            // Direction'a göre rotation
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            effect.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            
            // Scale to cone area
            effect.transform.localScale = Vector3.one * (SKILL_RANGE / 4f);
            
            Object.Destroy(effect, 3f);
        }
    }
    
    private void SpawnDebrisVFX(Vector3 position, Vector2 direction)
    {
        var prefab = Resources.Load<GameObject>("VFX/SeismicRupture_StoneDebris");
        if (prefab != null)
        {
            GameObject debris = Object.Instantiate(prefab, position, Quaternion.identity);
            Object.Destroy(debris, 2f);
        }
    }
    
    private void SpawnKnockbackVFX(Vector3 position)
    {
        var prefab = Resources.Load<GameObject>("VFX/SeismicRupture_KnockbackEffect");
        if (prefab != null)
        {
            GameObject effect = Object.Instantiate(prefab, position, Quaternion.identity);
            Object.Destroy(effect, 1f);
        }
    }
    
    private void SpawnScreenShakeVFX()
    {
        // Screen shake implementation - CameraShake component'i varsa
        // CameraShake.Instance?.Shake(0.3f, 0.5f);
    }
}