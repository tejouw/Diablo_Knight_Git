using System.Collections.Generic;
using UnityEngine;
using Fusion;

/// <summary>
/// Simplified MonsterManager using only Fusion AOI system
/// </summary>
public class MonsterManager : SimulationBehaviour
{
    public static MonsterManager Instance { get; private set; }

    #region PERFORMANCE SETTINGS
    [Header("Performance Settings")]
    [SerializeField] private int maxMonstersPerFrame = 20;
    [SerializeField] private int midRangeUpdateFrequency = 2;   // 2 frame'de bir
    #endregion

    #region MONSTER COLLECTIONS
    private List<MonsterBehaviour> allMonsters = new List<MonsterBehaviour>();
    private List<MonsterBehaviour> visibleMonsters = new List<MonsterBehaviour>();
    
    // Performans için cached listeler
    private List<MonsterBehaviour> closeRangeMonsters = new List<MonsterBehaviour>();
    private List<MonsterBehaviour> midRangeMonsters = new List<MonsterBehaviour>();
    #endregion

    #region PERFORMANCE TRACKING
    private int frameCounter = 0;
    private int processedMonstersThisFrame = 0;
    private const float CLOSE_RANGE_DISTANCE_SQR = 400f; // 20m squared
    #endregion

    #region INITIALIZATION
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    #endregion

    #region MONSTER REGISTRATION
    public bool IsMonsterRegistered(MonsterBehaviour monster)
    {
        if (monster == null || !monster.IsSpawned) return false;
        return allMonsters.Contains(monster);
    }

    public void RegisterMonster(MonsterBehaviour monster)
    {
        if (monster == null || !monster.IsSpawned || allMonsters.Contains(monster))
            return;
            
        allMonsters.Add(monster);
    }

    public void UnregisterMonster(MonsterBehaviour monster)
    {
        allMonsters.Remove(monster);
        visibleMonsters.Remove(monster);
        closeRangeMonsters.Remove(monster);
        midRangeMonsters.Remove(monster);
    }
    #endregion

    #region MAIN RENDER LOOP - FUSION AOI ONLY
    public override void Render()
    {
        if (allMonsters.Count == 0) return;

        frameCounter++;
        processedMonstersThisFrame = 0;

        // Sadece Fusion AOI'ye güvenerek visible monster'ları güncelle
        UpdateVisibleMonsters();
        
        // Visible monster'ları distance'a göre kategorize et
        CategorizeVisibleMonsters();
        
        // Process monsters based on distance categories
        ProcessMonsterCategories();
    }

private void UpdateVisibleMonsters()
{
    visibleMonsters.Clear();

    for (int i = 0; i < allMonsters.Count; i++)
    {
        var monster = allMonsters[i];
        if (monster == null || !monster.gameObject.activeInHierarchy) 
            continue;

        // ✅ Sadece initialized monster'ları kontrol et
        if (monster.IsInitialized && monster.IsVisibleToLocalPlayer)
        {
            visibleMonsters.Add(monster);
        }
    }
}

    private void CategorizeVisibleMonsters()
    {
        closeRangeMonsters.Clear();
        midRangeMonsters.Clear();

        // Local player pozisyonunu al
        Vector3 playerPos = GetLocalPlayerPosition();

        foreach (var monster in visibleMonsters)
        {
            float distanceSqr = (monster.transform.position - playerPos).sqrMagnitude;
            
            if (distanceSqr <= CLOSE_RANGE_DISTANCE_SQR)
            {
                closeRangeMonsters.Add(monster);
            }
            else
            {
                midRangeMonsters.Add(monster);
            }
        }
    }

    private void ProcessMonsterCategories()
    {
        // Close range monsters - her frame process et
        ProcessMonsterBatch(closeRangeMonsters, 0);

        // Mid range monsters - daha az sıklıkla process et
        if (frameCounter % midRangeUpdateFrequency == 0)
        {
            ProcessMonsterBatch(midRangeMonsters, 1);
        }
    }

    private void ProcessMonsterBatch(List<MonsterBehaviour> monsters, int tier)
    {
        int maxToProcess = Mathf.Min(maxMonstersPerFrame - processedMonstersThisFrame, monsters.Count);

        for (int i = 0; i < maxToProcess; i++)
        {
            var monster = monsters[i];
            if (monster != null && 
                monster.gameObject.activeInHierarchy &&
                monster.Object != null &&
                monster.Object.IsValid &&
                monster.IsSpawned &&
                monster.IsVisibleToLocalPlayer) // Fusion AOI double check
            {
                monster.ManagedRender(tier, frameCounter);
                processedMonstersThisFrame++;
            }
        }
    }

#region UTILITY METHODS - OPTIMIZED

private Vector3 cachedLocalPlayerPosition;
private float lastPlayerPositionUpdate = 0f;
private const float PLAYER_POSITION_UPDATE_INTERVAL = 0.2f;

private Vector3 GetLocalPlayerPosition()
{
    if (Time.time - lastPlayerPositionUpdate >= PLAYER_POSITION_UPDATE_INTERVAL)
    {
        UpdateLocalPlayerPosition();
        lastPlayerPositionUpdate = Time.time;
    }
    
    return cachedLocalPlayerPosition;
}

private void UpdateLocalPlayerPosition()
{
    GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
    
    foreach (GameObject player in players)
    {
        NetworkObject netObj = player.GetComponent<NetworkObject>();
        if (netObj != null && netObj.HasInputAuthority)
        {
            cachedLocalPlayerPosition = player.transform.position;
            return;
        }
    }
    
    cachedLocalPlayerPosition = Vector3.zero;
}

#endregion
    #endregion

    #region UTILITY METHODS
    public List<MonsterBehaviour> GetVisibleMonsters()
    {
        return new List<MonsterBehaviour>(visibleMonsters);
    }

    public int GetVisibleMonsterCount()
    {
        return visibleMonsters.Count;
    }

    public List<MonsterBehaviour> GetMonstersInRadius(Vector3 center, float radius)
    {
        var result = new List<MonsterBehaviour>();
        float radiusSqr = radius * radius;

        // Sadece visible monster'lar içinde ara - Fusion AOI'ye güven
        foreach (var monster in visibleMonsters)
        {
            if (monster != null)
            {
                float distSqr = (center - monster.transform.position).sqrMagnitude;
                if (distSqr <= radiusSqr)
                {
                    result.Add(monster);
                    if (result.Count >= 50) break; // Limit
                }
            }
        }

        return result;
    }

/// <summary>
/// Get all active monsters for skill preview and targeting systems
/// Returns ALL monsters that are alive and valid (not limited by AOI)
/// </summary>
public List<MonsterBehaviour> GetAllActiveMonsters()
{
    var result = new List<MonsterBehaviour>();

    // ✅ DÜZELTME: allMonsters kullan (tüm monster'lar, AOI'den bağımsız)
    foreach (var monster in allMonsters)
    {
        if (monster != null &&
            !monster.IsDead &&
            monster.gameObject.activeInHierarchy)
        {
            result.Add(monster);
        }
    }

    return result;
}

    /// <summary>
    /// Get all registered monsters (including non-visible ones)
    /// Useful for server-side operations
    /// </summary>
    public List<MonsterBehaviour> GetAllMonsters()
    {
        var result = new List<MonsterBehaviour>();

        foreach (var monster in allMonsters)
        {
            if (monster != null &&
                !monster.IsDead &&
                monster.gameObject.activeInHierarchy)
            {
                result.Add(monster);
            }
        }

        return result;
    }
    #endregion

    #region CLEANUP
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
    #endregion
}