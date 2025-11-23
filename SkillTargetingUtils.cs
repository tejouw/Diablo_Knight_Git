using UnityEngine;
using System.Collections.Generic;

public static class SkillTargetingUtils
{
    public static List<GameObject> FindTargetsInCircle(Vector3 center, float radius, GameObject caster)
    {
        List<GameObject> targets = new List<GameObject>();

        // Check monsters - Use MonsterManager with fallback
        if (MonsterManager.Instance != null)
        {
            var allMonsters = MonsterManager.Instance.GetAllActiveMonsters();
            foreach (var monsterBehaviour in allMonsters)
            {
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                float distance = Vector2.Distance(center, monsterBehaviour.transform.position);
                if (distance <= radius)
                {
                    targets.Add(monsterBehaviour.gameObject);
                }
            }
        }
        else
        {
            // Fallback
            GameObject[] allMonsters = GameObject.FindGameObjectsWithTag("Monster");
            foreach (GameObject monster in allMonsters)
            {
                if (monster == null) continue;

                var monsterBehaviour = monster.GetComponent<MonsterBehaviour>();
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                float distance = Vector2.Distance(center, monster.transform.position);
                if (distance <= radius)
                {
                    targets.Add(monster);
                }
            }
        }
        
        // Check players in PvP
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in allPlayers)
        {
            if (player == caster) continue;
            
            if (!IsValidPvPTarget(caster, player)) continue;
            
            float distance = Vector2.Distance(center, player.transform.position);
            if (distance <= radius)
            {
                targets.Add(player);
            }
        }
        
        return targets;
    }
    
    public static List<GameObject> FindTargetsInLine(Vector3 startPos, Vector3 endPos, float lineWidth, GameObject caster)
    {
        List<GameObject> targets = new List<GameObject>();

        // Check monsters - Use MonsterManager with fallback
        if (MonsterManager.Instance != null)
        {
            var allMonsters = MonsterManager.Instance.GetAllActiveMonsters();
            foreach (var monsterBehaviour in allMonsters)
            {
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                if (IsTargetOnLinePath(startPos, endPos, monsterBehaviour.transform.position, lineWidth))
                {
                    targets.Add(monsterBehaviour.gameObject);
                }
            }
        }
        else
        {
            // Fallback
            GameObject[] allMonsters = GameObject.FindGameObjectsWithTag("Monster");
            foreach (GameObject monster in allMonsters)
            {
                if (monster == null) continue;

                var monsterBehaviour = monster.GetComponent<MonsterBehaviour>();
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                if (IsTargetOnLinePath(startPos, endPos, monster.transform.position, lineWidth))
                {
                    targets.Add(monster);
                }
            }
        }
        
        // Check players in PvP
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in allPlayers)
        {
            if (player == caster) continue;
            
            if (!IsValidPvPTarget(caster, player)) continue;
            
            if (IsTargetOnLinePath(startPos, endPos, player.transform.position, lineWidth))
            {
                targets.Add(player);
            }
        }
        
        return targets;
    }
    
    public static List<GameObject> FindTargetsInCone(Vector3 origin, Vector2 direction, float coneAngle, float range, GameObject caster)
    {
        List<GameObject> targets = new List<GameObject>();

        // Check monsters - Use MonsterManager with fallback to FindGameObjectsWithTag
        if (MonsterManager.Instance != null)
        {
            var allMonsters = MonsterManager.Instance.GetAllActiveMonsters();

            foreach (var monsterBehaviour in allMonsters)
            {
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                float distance = Vector2.Distance(origin, monsterBehaviour.transform.position);
                if (distance > range) continue;

                Vector2 targetDirection = (monsterBehaviour.transform.position - origin).normalized;
                float angle = Vector2.Angle(direction, targetDirection);

                if (angle <= coneAngle / 2f)
                {
                    if (HasLineOfSight(origin, monsterBehaviour.transform.position))
                    {
                        targets.Add(monsterBehaviour.gameObject);
                    }
                }
            }
        }
        else
        {
            // Fallback: MonsterManager yok, eski yöntemi kullan
            GameObject[] allMonsters = GameObject.FindGameObjectsWithTag("Monster");

            foreach (GameObject monster in allMonsters)
            {
                if (monster == null) continue;

                var monsterBehaviour = monster.GetComponent<MonsterBehaviour>();
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                float distance = Vector2.Distance(origin, monster.transform.position);
                if (distance > range) continue;

                Vector2 targetDirection = (monster.transform.position - origin).normalized;
                float angle = Vector2.Angle(direction, targetDirection);

                if (angle <= coneAngle / 2f)
                {
                    if (HasLineOfSight(origin, monster.transform.position))
                    {
                        targets.Add(monster);
                    }
                }
            }
        }
        
        // Check players in PvP
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in allPlayers)
        {
            if (player == caster) continue;
            
            if (!IsValidPvPTarget(caster, player)) continue;
            
            float distance = Vector2.Distance(origin, player.transform.position);
            if (distance > range) continue;
            
            Vector2 targetDirection = (player.transform.position - origin).normalized;
            float angle = Vector2.Angle(direction, targetDirection);
            
            if (angle <= coneAngle / 2f)
            {
                if (HasLineOfSight(origin, player.transform.position))
                {
                    targets.Add(player);
                }
            }
        }
        
        return targets;
    }

    // SkillTargetingUtils.cs - FindClosestTarget metodunu düzelt
    public static GameObject FindClosestTarget(Vector3 origin, float range, GameObject caster, Vector2? preferredDirection = null, float directionTolerance = 0.7f)
    {
        GameObject closestTarget = null;
        float minDistance = float.MaxValue;

        // Hem monster hem de player'ları aynı anda kontrol et - mesafe bazında

        // Monster kontrolü - Use MonsterManager with fallback
        if (MonsterManager.Instance != null)
        {
            var allMonsters = MonsterManager.Instance.GetAllActiveMonsters();
            foreach (var monsterBehaviour in allMonsters)
            {
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                float distance = Vector2.Distance(origin, monsterBehaviour.transform.position);
                if (distance > range) continue;

                // Direction check if specified
                if (preferredDirection.HasValue)
                {
                    Vector2 toTarget = (monsterBehaviour.transform.position - origin).normalized;
                    float dotProduct = Vector2.Dot(preferredDirection.Value.normalized, toTarget);
                    if (dotProduct < directionTolerance) continue;
                }

                if (!HasLineOfSight(origin, monsterBehaviour.transform.position)) continue;

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestTarget = monsterBehaviour.gameObject;
                }
            }
        }
        else
        {
            // Fallback
            GameObject[] allMonsters = GameObject.FindGameObjectsWithTag("Monster");
            foreach (GameObject monster in allMonsters)
            {
                if (monster == null) continue;

                var monsterBehaviour = monster.GetComponent<MonsterBehaviour>();
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                float distance = Vector2.Distance(origin, monster.transform.position);
                if (distance > range) continue;

                if (preferredDirection.HasValue)
                {
                    Vector2 toTarget = (monster.transform.position - origin).normalized;
                    float dotProduct = Vector2.Dot(preferredDirection.Value.normalized, toTarget);
                    if (dotProduct < directionTolerance) continue;
                }

                if (!HasLineOfSight(origin, monster.transform.position)) continue;

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestTarget = monster;
                }
            }
        }

        // Player kontrolü (PVP zone'daysa)
        var casterPvP = caster.GetComponent<PVPSystem>();
        if (casterPvP != null && casterPvP.CanAttackPlayers())
        {
            GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject player in allPlayers)
            {
                if (player == caster) continue;

                if (!IsValidPvPTarget(caster, player)) continue;

                float distance = Vector2.Distance(origin, player.transform.position);
                if (distance > range) continue;

                if (preferredDirection.HasValue)
                {
                    Vector2 toTarget = (player.transform.position - origin).normalized;
                    float dotProduct = Vector2.Dot(preferredDirection.Value.normalized, toTarget);
                    if (dotProduct < directionTolerance) continue;
                }

                if (!HasLineOfSight(origin, player.transform.position)) continue;

                // Mesafe monster'dan daha yakınsa player'ı seç
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestTarget = player;
                }
            }
        }

        return closestTarget;
    }
public static List<GameObject> FindAllTargetsInRange(Vector3 origin, float range, GameObject caster)
{
    List<GameObject> targets = new List<GameObject>();

    // Monster kontrolü - Use MonsterManager with fallback
    if (MonsterManager.Instance != null)
    {
        var allMonsters = MonsterManager.Instance.GetAllActiveMonsters();
        foreach (var monsterBehaviour in allMonsters)
        {
            if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

            float distance = Vector2.Distance(origin, monsterBehaviour.transform.position);
            if (distance <= range && HasLineOfSight(origin, monsterBehaviour.transform.position))
            {
                targets.Add(monsterBehaviour.gameObject);
            }
        }
    }
    else
    {
        // Fallback
        GameObject[] allMonsters = GameObject.FindGameObjectsWithTag("Monster");
        foreach (GameObject monster in allMonsters)
        {
            if (monster == null) continue;

            var monsterBehaviour = monster.GetComponent<MonsterBehaviour>();
            if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

            float distance = Vector2.Distance(origin, monster.transform.position);
            if (distance <= range && HasLineOfSight(origin, monster.transform.position))
            {
                targets.Add(monster);
            }
        }
    }
    
    // Player kontrolü (PVP zone'daysa)
    var casterPvP = caster.GetComponent<PVPSystem>();
    if (casterPvP != null && casterPvP.CanAttackPlayers())
    {
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in allPlayers)
        {
            if (player == caster) continue;
            
            if (!IsValidPvPTarget(caster, player)) continue;
            
            float distance = Vector2.Distance(origin, player.transform.position);
            if (distance <= range && HasLineOfSight(origin, player.transform.position))
            {
                targets.Add(player);
            }
        }
    }
    
    // Mesafeye göre sırala (en yakından en uzağa)
    targets.Sort((a, b) => {
        float distA = Vector2.Distance(origin, a.transform.position);
        float distB = Vector2.Distance(origin, b.transform.position);
        return distA.CompareTo(distB);
    });
    
    return targets;
}

public static GameObject FindNearestTargetInDirection(Vector3 origin, Vector2 direction, float range, GameObject caster)
{
    return FindClosestTarget(origin, range, caster, direction, 0.7f);
}
    
    public static (List<GameObject> allies, List<GameObject> enemies) FindAlliesAndEnemiesInCircle(Vector3 center, float radius, GameObject caster)
    {
        List<GameObject> allies = new List<GameObject> { caster };
        List<GameObject> enemies = new List<GameObject>();

        // Find allied players
        GameObject[] allPlayers = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in allPlayers)
        {
            if (player == caster) continue;

            float distance = Vector2.Distance(center, player.transform.position);
            if (distance <= radius)
            {
                if (IsAlly(caster, player))
                {
                    allies.Add(player);
                }
                else if (IsEnemy(caster, player))
                {
                    enemies.Add(player);
                }
            }
        }

        // Find enemy monsters - Use MonsterManager with fallback
        if (MonsterManager.Instance != null)
        {
            var allMonsters = MonsterManager.Instance.GetAllActiveMonsters();

            foreach (var monsterBehaviour in allMonsters)
            {
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                float distance = Vector2.Distance(center, monsterBehaviour.transform.position);
                if (distance <= radius)
                {
                    enemies.Add(monsterBehaviour.gameObject);
                }
            }
        }
        else
        {
            // Fallback
            GameObject[] allMonsters = GameObject.FindGameObjectsWithTag("Monster");

            foreach (GameObject monster in allMonsters)
            {
                if (monster == null) continue;

                var monsterBehaviour = monster.GetComponent<MonsterBehaviour>();
                if (monsterBehaviour == null || monsterBehaviour.IsDead) continue;

                float distance = Vector2.Distance(center, monster.transform.position);
                if (distance <= radius)
                {
                    enemies.Add(monster);
                }
            }
        }

        return (allies, enemies);
    }
    
    private static bool IsTargetOnLinePath(Vector3 startPos, Vector3 endPos, Vector3 targetPos, float lineWidth)
    {
        Vector3 lineDirection = (endPos - startPos).normalized;
        float lineLength = Vector3.Distance(startPos, endPos);
        
        Vector3 toTarget = targetPos - startPos;
        float projectionLength = Vector3.Dot(toTarget, lineDirection);
        
        if (projectionLength < 0 || projectionLength > lineLength)
            return false;
        
        Vector3 projectionPoint = startPos + lineDirection * projectionLength;
        float perpendicularDistance = Vector3.Distance(targetPos, projectionPoint);
        
        return perpendicularDistance <= lineWidth;
    }
    
// SkillTargetingUtils.cs - Bu metodu değiştir
// SkillTargetingUtils.cs - Bu metodu tekrar düzelt
private static bool IsValidPvPTarget(GameObject caster, GameObject target)
{
    var pvpSystem = caster.GetComponent<PVPSystem>();
    var targetPvpSystem = target.GetComponent<PVPSystem>();
    var targetStats = target.GetComponent<PlayerStats>();
    
    if (pvpSystem == null || targetPvpSystem == null || targetStats == null)
    {
        return false;
    }
    
    // Güvenli kontroller
    bool casterCanAttack = pvpSystem.CanAttackPlayers();
    bool targetInPVP = targetPvpSystem.GetSafePVPStatus();
    bool targetAlive = !targetStats.IsDead; // Bu artık güvenli çünkü PlayerStats.IsDead güvenli metodu kullanıyor
    
    return casterCanAttack && targetInPVP && targetAlive;
}
    
    private static bool HasLineOfSight(Vector3 origin, Vector3 target)
    {
        Vector2 direction = (target - origin).normalized;
        float distance = Vector2.Distance(origin, target);
        
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, LayerMask.GetMask("Obstacles"));
        return hit.collider == null;
    }
    
private static bool IsAlly(GameObject caster, GameObject target)
{
    if (caster == target) return true;
    
    var casterStats = caster.GetComponent<PlayerStats>();
    var targetStats = target.GetComponent<PlayerStats>();
    
    if (casterStats == null || targetStats == null) return false;
    
    // Same party check
    if (PartyManager.Instance != null)
    {
        int casterPartyId = casterStats.CurrentPartyId;
        int targetPartyId = targetStats.CurrentPartyId;
        
        if (casterPartyId != -1 && casterPartyId == targetPartyId)
        {
            return true;
        }
    }
    
    // No PvP zone = allies
    var casterPvP = caster.GetComponent<PVPSystem>();
    var targetPvP = target.GetComponent<PVPSystem>();
    
    if (casterPvP != null && targetPvP != null)
    {
        // Güvenli PVP status kontrolü
        bool casterInPVP = casterPvP.GetSafePVPStatus();
        bool targetInPVP = targetPvP.GetSafePVPStatus();
        
        if (!casterInPVP || !targetInPVP)
        {
            return true;
        }
    }
    
    return false;
}
    
private static bool IsEnemy(GameObject caster, GameObject target)
{
    var casterPvP = caster.GetComponent<PVPSystem>();
    var targetPvP = target.GetComponent<PVPSystem>();
    var targetStats = target.GetComponent<PlayerStats>();
    
    if (casterPvP != null && targetPvP != null && targetStats != null)
    {
        // Güvenli PVP status kontrolü
        bool casterCanAttack = casterPvP.CanAttackPlayers();
        bool targetInPVP = targetPvP.GetSafePVPStatus();
        bool targetAlive = !targetStats.IsDead;
        
        return casterCanAttack && targetInPVP && targetAlive && !IsAlly(caster, target);
    }
    
    return false;
}
}