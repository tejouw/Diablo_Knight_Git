using UnityEngine;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Collections;

public class ColossusStanceExecutor : BaseSkillExecutor
{
    public override string SkillId => "colossus_stance";
    
    private const float DAMAGE_MULTIPLIER = 3.0f; // %300 damage
    private const float AOE_RADIUS = 4f; // 2 tile = ~4 units
    private const float TAUNT_DURATION = 0.5f; // 0.5 saniye taunt
    private const float SELF_DAMAGE_REDUCTION = 40f; // %40 damage reduction
    private const float ALLY_DAMAGE_REDUCTION = 20f; // %20 damage reduction
    private const float BUFF_DURATION = 4f; // 4 saniye buff süresi
    
    public override void Execute(GameObject caster, SkillInstance skillInstance)
    {
        // Client-side sadece stance animation
        var character4D = caster.GetComponent<Character4D>();
        if (character4D != null)
        {
            character4D.AnimationManager.Cast(); // Stance animation
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
            Debug.LogError("[ColossusStance-SERVER] Missing components!");
            return;
        }
        
        Vector3 casterPosition = caster.transform.position;
        
        // Find all targets in AOE
        var targetsInRange = FindTargetsAndAlliesInCircle(caster, casterPosition, AOE_RADIUS);
        var enemies = targetsInRange.enemies;
        var allies = targetsInRange.allies;
        
        // Calculate damage
        float baseDamage = playerStats.FinalDamage * DAMAGE_MULTIPLIER;
        
        // Apply damage to enemies
        foreach (var enemy in enemies)
        {
            ApplyDamageToTarget(caster, enemy, baseDamage, skillSystem);
            ApplyTauntToTarget(enemy, caster, TAUNT_DURATION);
        }
        
        // Apply self damage reduction buff
        ApplySelfDamageReductionBuff(caster);
        
        // Apply ally protection buffs
        ApplyAllyProtectionBuffs(allies);
        
        // Collect positions for VFX
        Vector3[] enemyPositions = new Vector3[enemies.Count];
        for (int i = 0; i < enemies.Count; i++)
        {
            enemyPositions[i] = enemies[i].transform.position;
        }
        
        Vector3[] allyPositions = new Vector3[allies.Count];
        for (int i = 0; i < allies.Count; i++)
        {
            allyPositions[i] = allies[i].transform.position;
        }
        
        // Execute VFX on all clients
        skillSystem.ExecuteColossusStanceRPC(casterPosition, enemyPositions, allyPositions, enemies.Count);
    }
    
    private (List<GameObject> allies, List<GameObject> enemies) FindTargetsAndAlliesInCircle(GameObject caster, Vector3 center, float radius)
    {
        return SkillTargetingUtils.FindAlliesAndEnemiesInCircle(center, radius, caster);
    }
    
    private void ApplyTauntToTarget(GameObject target, GameObject taunter, float duration)
    {
        // Monster taunt
        if (target.CompareTag("Monster"))
        {
            var monsterBehaviour = target.GetComponent<MonsterBehaviour>();
            if (monsterBehaviour != null)
            {
                monsterBehaviour.ApplyTaunt(taunter, duration);
            }
            return;
        }
        
        // Player taunt (PvP)
        if (target.CompareTag("Player"))
        {
            var pvpSystem = target.GetComponent<PVPSystem>();
            if (pvpSystem != null)
            {
                pvpSystem.ApplyTaunt(taunter, duration);
            }
        }
    }
    
    private void ApplySelfDamageReductionBuff(GameObject caster)
    {
        var tempBuff = caster.GetComponent<TemporaryBuffSystem>();
        if (tempBuff == null)
        {
            tempBuff = caster.gameObject.AddComponent<TemporaryBuffSystem>();
        }
        
        tempBuff.ApplyDamageReductionBuff(SELF_DAMAGE_REDUCTION, BUFF_DURATION);
    }
    
    private void ApplyAllyProtectionBuffs(List<GameObject> allies)
    {
        foreach (GameObject ally in allies)
        {
            var tempBuff = ally.GetComponent<TemporaryBuffSystem>();
            if (tempBuff == null)
            {
                tempBuff = ally.gameObject.AddComponent<TemporaryBuffSystem>();
            }
            
            tempBuff.ApplyDamageReductionBuff(ALLY_DAMAGE_REDUCTION, BUFF_DURATION);
        }
    }
    
    public void ExecuteVFX(GameObject caster, Vector3 position, Vector3[] enemyPositions, Vector3[] allyPositions, int enemyCount)
    {
        var character4D = caster.GetComponent<Character4D>();
        
        // Character stance animation
        if (character4D != null)
        {
            character4D.AnimationManager.Cast();
        }
        
        // Main stance VFX - karaktere bağla
        SpawnStanceAuraVFX(caster, position);
        SpawnWeaponPlantVFX(caster, position);
        
        // Enemy taunt VFX
        foreach (Vector3 enemyPos in enemyPositions)
        {
            SpawnTauntEffectVFX(enemyPos);
        }
        
        // Ally protection VFX  
        foreach (Vector3 allyPos in allyPositions)
        {
            GameObject ally = FindCharacterAtPosition(allyPos, 1f);
            if (ally != null)
            {
                SpawnAllyProtectionVFX(ally);
            }
        }
        
        // Ground impact effect
        SpawnGroundImpactVFX(position);
    }
    
    private GameObject FindCharacterAtPosition(Vector3 position, float tolerance)
    {
        // Önce player'ları kontrol et
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in allPlayers)
        {
            if (player != null && Vector3.Distance(player.transform.position, position) <= tolerance)
            {
                return player;
            }
        }

        // Sonra monster'ları kontrol et - Use MonsterManager with fallback
        if (MonsterManager.Instance != null)
        {
            var allMonsters = MonsterManager.Instance.GetAllActiveMonsters();
            foreach (var monsterBehaviour in allMonsters)
            {
                if (monsterBehaviour != null && Vector3.Distance(monsterBehaviour.transform.position, position) <= tolerance)
                {
                    return monsterBehaviour.gameObject;
                }
            }
        }
        else
        {
            // Fallback
            GameObject[] allMonsters = GameObject.FindGameObjectsWithTag("Monster");
            foreach (GameObject monster in allMonsters)
            {
                if (monster != null && Vector3.Distance(monster.transform.position, position) <= tolerance)
                {
                    return monster;
                }
            }
        }

        return null;
    }
    
    private void SpawnStanceAuraVFX(GameObject caster, Vector3 position)
    {
        var prefab = Resources.Load<GameObject>("VFX/ColossusStance_Aura");
        if (prefab != null)
        {
            GameObject aura = Object.Instantiate(prefab);
            
            // Karaktere bağla
            aura.transform.SetParent(caster.transform);
            aura.transform.localPosition = Vector3.zero;
            
            // Particle system'leri local space'e çevir
            ParticleSystem[] allParticles = aura.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allParticles)
            {
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                ps.Clear();
                ps.Play();
            }
            
            Object.Destroy(aura, BUFF_DURATION);
        }
    }
    
    private void SpawnWeaponPlantVFX(GameObject caster, Vector3 position)
    {
        var prefab = Resources.Load<GameObject>("VFX/ColossusStance_WeaponPlant");
        if (prefab != null)
        {
            GameObject effect = Object.Instantiate(prefab, position, Quaternion.identity);
            Object.Destroy(effect, 1.5f);
        }
    }
    
    private void SpawnTauntEffectVFX(Vector3 position)
    {
        var prefab = Resources.Load<GameObject>("VFX/ColossusStance_TauntEffect");
        if (prefab != null)
        {
            GameObject effect = Object.Instantiate(prefab, position + Vector3.up * 2.5f, Quaternion.identity);
            Object.Destroy(effect, TAUNT_DURATION + 0.2f);
        }
    }
    
    private void SpawnAllyProtectionVFX(GameObject ally)
    {
        var prefab = Resources.Load<GameObject>("VFX/ColossusStance_AllyProtection");
        if (prefab != null)
        {
            GameObject protectionEffect = Object.Instantiate(prefab);
            
            // Ally'e bağla
            protectionEffect.transform.SetParent(ally.transform);
            protectionEffect.transform.localPosition = Vector3.zero;
            
            // Particle system'leri local space'e çevir
            ParticleSystem[] allParticles = protectionEffect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allParticles)
            {
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                ps.Clear();
                ps.Play();
            }
            
            Object.Destroy(protectionEffect, BUFF_DURATION);
        }
    }
    
    private void SpawnGroundImpactVFX(Vector3 position)
    {
        var prefab = Resources.Load<GameObject>("VFX/ColossusStance_GroundImpact");
        if (prefab != null)
        {
            GameObject impact = Object.Instantiate(prefab, position, Quaternion.identity);
            
            // Scale to AOE radius
            float scale = AOE_RADIUS / 2f;
            impact.transform.localScale = Vector3.one * scale;
            
            Object.Destroy(impact, 2f);
        }
    }
}