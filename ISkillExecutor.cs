using UnityEngine;
using System.Collections.Generic;

public interface ISkillExecutor
{
    bool CanExecute(GameObject caster, SkillInstance skillInstance);
    void Execute(GameObject caster, SkillInstance skillInstance);
    void ExecuteClientImmediate(GameObject caster, SkillInstance skillInstance); // YENİ
    string SkillId { get; }
}

public abstract class BaseSkillExecutor : ISkillExecutor
{
    public abstract string SkillId { get; }
    
public virtual bool CanExecute(GameObject caster, SkillInstance skillInstance)
{
    // Basic validation
    if (caster == null || skillInstance == null) 
    {
        return false;
    }
    
    // Death check - YENÄ°: GÃ¼venli metod kullan
    var deathSystem = caster.GetComponent<DeathSystem>();
    if (deathSystem != null && deathSystem.GetSafeDeathStatus()) 
    {
        return false;
    }
    
    // SkillData check
    if (skillInstance.skillData == null)
    {
        return false;
    }
    
    return true;
}
    
    public abstract void Execute(GameObject caster, SkillInstance skillInstance);
    
    // YENİ: Virtual metod - child class'lar override edebilir
    public virtual void ExecuteClientImmediate(GameObject caster, SkillInstance skillInstance)
    {
        // Default: sadece animation + VFX, damage yok
        Execute(caster, skillInstance);
    }
    
    protected PlayerStats GetPlayerStats(GameObject caster)
    {
        return caster.GetComponent<PlayerStats>();
    }
    
    protected PVPSystem GetPVPSystem(GameObject caster)
    {
        return caster.GetComponent<PVPSystem>();
    }
    
// BaseSkillExecutor.cs - Bu metodu değiştir
protected bool IsValidTarget(GameObject caster, GameObject target)
{
    if (target == caster) return false;
    
    // Check for monsters
    if (target.CompareTag("Monster"))
    {
        var monster = target.GetComponent<MonsterBehaviour>();
        return monster != null && !monster.IsDead;
    }
    
    // Check for players in PvP
    if (target.CompareTag("Player"))
    {
        var pvpSystem = caster.GetComponent<PVPSystem>();
        var targetPvpSystem = target.GetComponent<PVPSystem>();
        var targetStats = target.GetComponent<PlayerStats>();
        
        if (pvpSystem != null && targetPvpSystem != null && targetStats != null)
        {
            // Güvenli PVP status kontrolü
            bool casterCanAttack = pvpSystem.CanAttackPlayers();
            bool targetInPVP = targetPvpSystem.GetSafePVPStatus();
            bool targetAlive = !targetStats.IsDead;
            
            return casterCanAttack && targetInPVP && targetAlive;
        }
    }
    
    return false;
}
    
    protected bool HasLineOfSight(Vector3 origin, Vector3 target)
    {
        Vector2 direction = (target - origin).normalized;
        float distance = Vector2.Distance(origin, target);
        
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, LayerMask.GetMask("Obstacles"));
        return hit.collider == null;
    }
    
// DEĞİŞTİRİLEN METOD
protected void ApplyDamageToTarget(GameObject caster, GameObject target, float damage, SkillSystem skillSystem)
{
    if (!skillSystem.Object.HasStateAuthority) return;
    
    // Rastgele varyasyon uygula (0.8-1.2 arası)
    var casterStats = GetPlayerStats(caster);
    if (casterStats != null)
    {
        damage = casterStats.ApplyDamageVariation(damage);
    }
    
    // YENI - Accuracy check
    if (casterStats != null && !casterStats.RollAccuracyCheck())
    {
        // Miss - show miss effect via RPC
        skillSystem.ShowSkillDamagePopupRPC(target.transform.position + Vector3.up, 0f, (int)DamagePopup.DamageType.Miss);
        return;
    }
    
    // Damage monsters
    if (target.CompareTag("Monster"))
    {
        var monster = target.GetComponent<MonsterBehaviour>();
        if (monster != null)
        {
            monster.TakeDamageFromServer(damage, skillSystem.Object.InputAuthority, false);
        }
    }
    
    // Damage players in PvP
    if (target.CompareTag("Player"))
    {
        var targetStats = target.GetComponent<PlayerStats>();
        if (targetStats != null)
        {
            // Damage uygula
            targetStats.TakeDamage(damage, isPVPDamage: true);
            
            // YENI: Visual efektleri tetikle
            targetStats.TriggerFlashEffectFromServer();
            
            // YENI: Hit animasyonu tetikle
            var targetController = target.GetComponent<PlayerController>();
            if (targetController != null)
            {
                targetController.TriggerHitAnimationFromServer();
            }
        }
    }
    
    // Show skill damage popup via RPC - 2x büyüklükte olacak SkillDamage type
    skillSystem.ShowSkillDamagePopupRPC(target.transform.position + Vector3.up, damage, (int)DamagePopup.DamageType.SkillDamage);
}
}