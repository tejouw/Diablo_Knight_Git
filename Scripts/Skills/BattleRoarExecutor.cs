using UnityEngine;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;

public class BattleRoarExecutor : BaseSkillExecutor
{
    public override string SkillId => "battle_roar";
    
    private const float AOE_RADIUS = 6f; // 3 tile = ~6 units
    private const float ENEMY_DAMAGE_REDUCTION = 20f; // %20 damage reduction
    private const float ALLY_ATTACK_SPEED_INCREASE = 1.1f; // %10 attack speed increase
    private const float EFFECT_DURATION = 3f; // 3 saniye
    
    public override void Execute(GameObject caster, SkillInstance skillInstance)
    {
        // Client-side sadece VFX
        var character4D = caster.GetComponent<Character4D>();
        if (character4D != null)
        {
            character4D.AnimationManager.Cast();
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
        Debug.LogError("[BattleRoar-SERVER] Missing components!");
        return;
    }
    
    Vector3 casterPosition = caster.transform.position;
    
    // Find targets in circle
    var targetsInRange = FindTargetsInCircle(caster, casterPosition, AOE_RADIUS);
    var allies = targetsInRange.allies;
    var enemies = targetsInRange.enemies;
    
    // Apply effects
    ApplyAllyBuffs(allies);
    ApplyEnemyDebuffs(enemies, skillSystem);
    
    // Collect positions for VFX
    Vector3[] allyPositions = new Vector3[allies.Count];
    for (int i = 0; i < allies.Count; i++)
    {
        allyPositions[i] = allies[i].transform.position;
    }
    
    Vector3[] enemyPositions = new Vector3[enemies.Count];
    for (int i = 0; i < enemies.Count; i++)
    {
        enemyPositions[i] = enemies[i].transform.position;
    }
    
    // Execute VFX on all clients with positions
    skillSystem.ExecuteBattleRoarRPC(casterPosition, allyPositions, enemyPositions);
}
private (List<GameObject> allies, List<GameObject> enemies) FindTargetsInCircle(GameObject caster, Vector3 center, float radius)
{
    return SkillTargetingUtils.FindAlliesAndEnemiesInCircle(center, radius, caster);
}

    
    private void ApplyAllyBuffs(List<GameObject> allies)
    {
        foreach (GameObject ally in allies)
        {
            var tempBuff = ally.GetComponent<TemporaryBuffSystem>();
            if (tempBuff == null)
            {
                tempBuff = ally.gameObject.AddComponent<TemporaryBuffSystem>();
            }
            
            tempBuff.ApplyAttackSpeedBuff(ALLY_ATTACK_SPEED_INCREASE, EFFECT_DURATION);
        }
    }
    
    private void ApplyEnemyDebuffs(List<GameObject> enemies, SkillSystem skillSystem)
    {
        foreach (GameObject enemy in enemies)
        {
            if (enemy.CompareTag("Monster"))
            {
                var monsterBehaviour = enemy.GetComponent<MonsterBehaviour>();
                if (monsterBehaviour != null)
                {
                    monsterBehaviour.ApplyDamageDebuff(ENEMY_DAMAGE_REDUCTION, EFFECT_DURATION);
                }
            }
            else if (enemy.CompareTag("Player"))
            {
                var tempBuff = enemy.GetComponent<TemporaryBuffSystem>();
                if (tempBuff == null)
                {
                    tempBuff = enemy.gameObject.AddComponent<TemporaryBuffSystem>();
                }
                
                tempBuff.ApplyDamageDebuff(ENEMY_DAMAGE_REDUCTION, EFFECT_DURATION);
            }
        }
    }

public void ExecuteVFX(GameObject caster, Vector3 position, Vector3[] allyPositions, Vector3[] enemyPositions)
{
    var character4D = caster.GetComponent<Character4D>();

    // Character animation
    if (character4D != null)
    {
        character4D.AnimationManager.Cast();
    }

    // Main wave VFX - karaktere bağla
    SpawnRoarWaveVFX(caster, position);
    SpawnScreenShakeVFX();

    // Ally buff VFX - pozisyonlardaki karakterleri bul ve VFX bağla
    foreach (Vector3 allyPos in allyPositions)
    {
        GameObject ally = FindCharacterAtPosition(allyPos, 1f);
        if (ally != null)
        {
            SpawnAllyBuffVFX(ally);
        }
    }

    // Enemy debuff VFX - pozisyonlardaki karakterleri bul ve VFX bağla
    foreach (Vector3 enemyPos in enemyPositions)
    {
        GameObject enemy = FindCharacterAtPosition(enemyPos, 1f);
        if (enemy != null)
        {
            SpawnEnemyDebuffVFX(enemy);
        }
    }
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
private void SpawnAllyBuffVFX(GameObject ally)
{
    var prefab = Resources.Load<GameObject>("VFX/BattleRoar_AllyBuff");
    if (prefab != null)
    {
        GameObject buffEffect = Object.Instantiate(prefab);
        
        // Ally'e bağla
        buffEffect.transform.SetParent(ally.transform);
        buffEffect.transform.localPosition = Vector3.zero;
        
        // Particle system'leri local space'e çevir
        ParticleSystem[] allParticles = buffEffect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allParticles)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ps.Clear();
            ps.Play();
        }
        
        Object.Destroy(buffEffect, EFFECT_DURATION);
    }
}
    
private void SpawnRoarWaveVFX(GameObject caster, Vector3 position)
{
    var prefab = Resources.Load<GameObject>("VFX/BattleRoar_Wave");
    if (prefab != null)
    {
        GameObject wave = Object.Instantiate(prefab, position, Quaternion.identity);
        
        // Scale to AOE radius
        float scale = AOE_RADIUS / 3f;
        wave.transform.localScale = Vector3.one * scale;
        
        // ÖNCE karaktere bağla
        wave.transform.SetParent(caster.transform);
        
        // SONRA tüm particle system'leri Local space'e çevir (nested olanlar dahil)
        ParticleSystem[] allParticles = wave.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allParticles)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            
            // Mevcut particle'ları temizle ve yeniden başlat
            ps.Clear();
            ps.Play();
        }
        
        Object.Destroy(wave, 2f);
    }
}
    
    private void SpawnAllyBuffVFX(GameObject caster, Vector3 position)
    {
        var prefab = Resources.Load<GameObject>("VFX/BattleRoar_AllyBuff");
        if (prefab != null)
        {
            GameObject buffEffect = Object.Instantiate(prefab, caster.transform.position, Quaternion.identity);
            buffEffect.transform.SetParent(caster.transform);
            Object.Destroy(buffEffect, EFFECT_DURATION);
        }
    }
    

private void SpawnEnemyDebuffVFX(GameObject enemy)
{
    var prefab = Resources.Load<GameObject>("VFX/BattleRoar_EnemyDebuff");
    if (prefab != null)
    {
        GameObject debuffEffect = Object.Instantiate(prefab);
        
        // Enemy'e bağla
        debuffEffect.transform.SetParent(enemy.transform);
        debuffEffect.transform.localPosition = Vector3.zero;
        
        // Particle system'leri local space'e çevir
        ParticleSystem[] allParticles = debuffEffect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allParticles)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ps.Clear();
            ps.Play();
        }
        
        Object.Destroy(debuffEffect, 1.5f);
    }
}
    
    private void SpawnScreenShakeVFX()
    {
        // Screen shake implementation - can be added later
        // CameraShake.Instance?.Shake(0.2f, 0.3f);
    }
}