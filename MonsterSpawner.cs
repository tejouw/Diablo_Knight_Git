using UnityEngine;
using Fusion;
using System.Collections.Generic;
using UnityEngine.Tilemaps;
using System.Linq;

#region Data Structures & Enums
public enum SubAreaNumber
{
    SubArea1 = 1, SubArea2 = 2, SubArea3 = 3, SubArea4 = 4, SubArea5 = 5,
    SubArea6 = 6, SubArea7 = 7, SubArea8 = 8, SubArea9 = 9
}

public enum MonsterRarity { Normal, Magic, Rare }
public enum SpawnAreaType { PointRadius, AreaBased, MultiplePoints }

[System.Serializable]
public class SpawnPoint
{
    public string pointName = "Spawn Point";
    public Vector2 position;
    public float spawnRadius = 4f;
    public int maxMonstersInPoint = 4;
    [System.NonSerialized] public float lastSpawnTime = 0f;
    [System.NonSerialized] public bool isInitialSpawnComplete = false;
    
    public bool IsPositionInRange(Vector2 targetPosition)
    {
        return Vector2.Distance(position, targetPosition) <= spawnRadius;
    }
}

[System.Serializable]
public class SpawnArea
{
    public Vector2 position;
    public float spawnRadius = 4f;
    public string monsterType;
    public int maxMonstersInArea = 4;
    public float lastSpawnTime = 0f;

    public bool IsPointSafeForSpawn(Vector2 spawnPos)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float safeDistance = 1.5f;
        foreach (GameObject player in players)
        {
            if (player != null && Vector2.Distance(player.transform.position, spawnPos) < safeDistance)
                return false;
        }
        return true;
    }
}

[System.Serializable]
public class AreaWithMaxMonsters
{
    public AreaData area;
    [Range(1, 30)] public int maxMonstersInThisArea = 4;
    [SerializeField] private List<int> selectedSubAreas = new List<int>();
    [System.NonSerialized] public bool isInitialSpawnComplete = false;
    
    public bool IsValid => area != null;
    public List<int> SelectedSubAreas 
    { 
        get 
        { 
            if (selectedSubAreas == null || selectedSubAreas.Count == 0)
                return new List<int> { 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            return selectedSubAreas; 
        }
        set { selectedSubAreas = value; }
    }
    
    public void AddSubArea(int subAreaNumber)
    {
        if (subAreaNumber >= 1 && subAreaNumber <= 9 && !selectedSubAreas.Contains(subAreaNumber))
            selectedSubAreas.Add(subAreaNumber);
    }
    
    public void RemoveSubArea(int subAreaNumber) { selectedSubAreas.Remove(subAreaNumber); }
    public void ClearSubAreas() { selectedSubAreas.Clear(); }
    public bool HasSubArea(int subAreaNumber) { return selectedSubAreas.Contains(subAreaNumber); }
}

[System.Serializable]
public class MonsterSpawnData
{
    public string categoryName = "Default Category";
    [SerializeField] private bool isEnabled = true;
    public string monsterName = "Monster";
    public GameObject monsterPrefab;
    public float respawnTime = 30f;
    public int maxMonstersInArea = 4;
    [Range(0f, 100f)] public float magicChance = 0f;
    [Range(0f, 100f)] public float rareChance = 0f;
    public SpawnAreaType spawnAreaType = SpawnAreaType.PointRadius;
    public Vector2 spawnPoint;
    public float spawnRadius = 4f;
    public List<AreaWithMaxMonsters> spawnAreasWithLimits = new List<AreaWithMaxMonsters>();
    public List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
    [System.NonSerialized] public float lastSpawnTime = 0f;
    [System.NonSerialized] public bool isInitialSpawnComplete = false;
    
    public bool IsEnabled => isEnabled;
    public void SetEnabled(bool value) { isEnabled = value; }
    
    public bool IsPositionInSpawnArea(Vector2 position)
    {
        switch (spawnAreaType)
        {
            case SpawnAreaType.PointRadius:
                return Vector2.Distance(position, spawnPoint) <= spawnRadius;
            case SpawnAreaType.AreaBased:
                return spawnAreasWithLimits.Any(areaLimit => 
                    areaLimit != null && areaLimit.IsValid && areaLimit.area.IsPositionInArea(position));
            case SpawnAreaType.MultiplePoints:
                return spawnPoints.Any(point => point != null && point.IsPositionInRange(position));
            default:
                return false;
        }
    }
}

public static class MonsterRaritySettings
{
    public const float MAGIC_COMBAT_MULTIPLIER = 2f;
    public const float MAGIC_SPEED_MULTIPLIER = 1.2f;
    public const float MAGIC_SIZE_MULTIPLIER = 1.15f;
    public static readonly Color MAGIC_TINT = new Color(0.5f, 0.8f, 1f, 1f);
    public const float RARE_COMBAT_MULTIPLIER = 4f;
    public const float RARE_SPEED_MULTIPLIER = 1.3f;
    public const float RARE_SIZE_MULTIPLIER = 1.3f;
    public static readonly Color RARE_TINT = new Color(1f, 0.8f, 0.2f, 1f);
}
#endregion

public class MonsterSpawner : NetworkBehaviour
{
    #region Network Properties
    [Networked] public bool IsInitialized { get; set; }
    [Networked] public int TotalMonstersSpawned { get; set; }
    [Networked] public TickTimer MainSpawnTimer { get; set; }
    #endregion

    #region Monster Registry
    [System.Serializable]
    public class MonsterRegistryEntry
    {
        public NetworkId monsterId;
        public string monsterType;
        public Vector2 position;
        public string locationName;
        public SpawnAreaType spawnType;
        public float spawnTime;

        public MonsterRegistryEntry(NetworkId id, string type, Vector2 pos, string location, SpawnAreaType spawnAreaType)
        {
            monsterId = id;
            monsterType = type;
            position = pos;
            locationName = location;
            spawnType = spawnAreaType;
            spawnTime = Time.time;
        }
    }

    private Dictionary<NetworkId, MonsterRegistryEntry> monsterRegistry = new Dictionary<NetworkId, MonsterRegistryEntry>();
    private Dictionary<string, List<NetworkId>> monstersByType = new Dictionary<string, List<NetworkId>>();
    private Dictionary<string, List<NetworkId>> monstersByLocation = new Dictionary<string, List<NetworkId>>();

    private int GetActiveMonsterCount()
    {
        if (!Runner.IsServer) return 0;
        return monsterRegistry.Count;
    }

    public void RegisterMonster(NetworkId monsterId, string monsterType, Vector2 position, string locationName, SpawnAreaType spawnType)
    {
        if (!Runner.IsServer) return;
        string cleanType = monsterType.Replace("(Magic) ", "").Replace("(Rare) ", "");
        var entry = new MonsterRegistryEntry(monsterId, cleanType, position, locationName, spawnType);
        monsterRegistry[monsterId] = entry;
        if (!monstersByType.ContainsKey(cleanType))
            monstersByType[cleanType] = new List<NetworkId>();
        monstersByType[cleanType].Add(monsterId);
        if (!string.IsNullOrEmpty(locationName))
        {
            if (!monstersByLocation.ContainsKey(locationName))
                monstersByLocation[locationName] = new List<NetworkId>();
            monstersByLocation[locationName].Add(monsterId);
        }
    }

    public void UnregisterMonster(NetworkId monsterId)
    {
        if (!Runner.IsServer) return;
        if (monsterRegistry.TryGetValue(monsterId, out var entry))
        {
            if (monstersByType.ContainsKey(entry.monsterType))
            {
                monstersByType[entry.monsterType].Remove(monsterId);
                if (monstersByType[entry.monsterType].Count == 0)
                    monstersByType.Remove(entry.monsterType);
            }
            if (!string.IsNullOrEmpty(entry.locationName) && monstersByLocation.ContainsKey(entry.locationName))
            {
                monstersByLocation[entry.locationName].Remove(monsterId);
                if (monstersByLocation[entry.locationName].Count == 0)
                    monstersByLocation.Remove(entry.locationName);
            }
            monsterRegistry.Remove(monsterId);
        }
    }

    private int GetMonsterCountByType(string monsterType)
    {
        if (!Runner.IsServer) return 0;
        if (!monstersByType.ContainsKey(monsterType))
            return 0;
        return monstersByType[monsterType].Count;
    }

    private int GetMonsterCountInLocationByType(string locationName, string monsterType)
    {
        if (!Runner.IsServer) return 0;
        int count = 0;
        foreach (var entry in monsterRegistry.Values)
        {
            if (entry.locationName == locationName && entry.monsterType == monsterType)
                count++;
        }
        return count;
    }

    private int GetMonsterCountInLocation(string locationName)
    {
        if (!Runner.IsServer) return 0;
        if (!monstersByLocation.ContainsKey(locationName))
            return 0;
        return monstersByLocation[locationName].Count;
    }

    private int GetMonsterCountByLocationPrefix(string locationPrefix)
    {
        if (!Runner.IsServer) return 0;
        int count = 0;
        foreach (var entry in monsterRegistry.Values)
        {
            if (entry.locationName.StartsWith(locationPrefix))
                count++;
        }
        return count;
    }
    #endregion

    #region Configuration
    [SerializeField] private int maxTotalMonsters = 0;
    [SerializeField] private List<MonsterSpawnData> monsterSpawnList = new List<MonsterSpawnData>();
    [SerializeField] private float playerSafeDistance = 2f;
    [SerializeField] private float monsterSpacingDistance = 1f;
    [SerializeField] private Tilemap[] obstacleTilemaps;
    private Tilemap[] cachedTilemaps;
    private Transform serverHierarchyContainer;
    #endregion

    #region Fusion Lifecycle
    public override void Spawned()
    {
        if (Runner.IsServer)
            InitializeServerSpawner();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Runner.IsServer || !IsInitialized) return;
        if (MainSpawnTimer.ExpiredOrNotRunning(Runner))
        {
            MainSpawnTimer = TickTimer.CreateFromSeconds(Runner, 0.5f);
            PerformServerSpawnCheck();
        }
    }
    #endregion

    #region Initialization
    private void InitializeServerSpawner()
    {
        if (!Runner.IsServer) return;
        InitializeCachedTilemaps();
        InitializeServerHierarchy();
        ValidateMonsterTypeMappings();
        IsInitialized = true;
        MainSpawnTimer = TickTimer.CreateFromSeconds(Runner, 0f);
    }

    private void ValidateMonsterTypeMappings()
    {
#if UNITY_EDITOR
        List<string> usedTypes = new List<string>();
        foreach (var spawnData in monsterSpawnList)
        {
            if (!string.IsNullOrEmpty(spawnData.monsterName))
                usedTypes.Add(spawnData.monsterName);
        }
        MonsterTypeMapping.ValidateMonsterTypes(usedTypes.ToArray());
#endif
    }

    private void InitializeCachedTilemaps()
    {
        if (!Runner.IsServer) return;
        if (obstacleTilemaps != null && obstacleTilemaps.Length > 0)
        {
            var validTilemaps = obstacleTilemaps
                .Where(tm => tm != null && LayerMask.LayerToName(tm.gameObject.layer) == "Obstacles")
                .ToArray();
            cachedTilemaps = validTilemaps;
        }
        else
        {
            cachedTilemaps = FindObjectsByType<Tilemap>(FindObjectsSortMode.None)
                .Where(tm => tm != null && LayerMask.LayerToName(tm.gameObject.layer) == "Obstacles")
                .ToArray();
        }
    }

    private void InitializeServerHierarchy()
    {
        if (!Runner.IsServer) return;
        GameObject existingContainer = GameObject.Find("Monsters");
        if (existingContainer != null)
            serverHierarchyContainer = existingContainer.transform;
        else
        {
            GameObject newContainer = new GameObject("Monsters");
            serverHierarchyContainer = newContainer.transform;
        }
    }
    #endregion

    #region Main Spawn Logic

    private void PerformServerSpawnCheck()
    {
        if (!Runner.IsServer) return;
        int maxSpawnsThisFrame = 4;
        int currentSpawnAttempts = 0;
        var prioritizedSpawnData = monsterSpawnList
            .Where(sd => sd.monsterPrefab != null && sd.IsEnabled)
            .OrderBy(sd => GetSpawnPriority(sd))
            .ToList();
        foreach (var spawnData in prioritizedSpawnData)
        {
            if (currentSpawnAttempts >= maxSpawnsThisFrame)
                break;
            if (ProcessSpawnData(spawnData))
                currentSpawnAttempts++;
        }
    }

    private float GetSpawnPriority(MonsterSpawnData spawnData)
    {
        if (!Runner.IsServer) return float.MaxValue;
        int currentCount = GetMonsterCountByType(spawnData.monsterName);
        int maxCount = GetMaxMonsterCountForSpawnData(spawnData);
        if (maxCount == 0) return float.MaxValue;
        return (float)currentCount / maxCount;
    }

    private bool ProcessSpawnData(MonsterSpawnData spawnData)
    {
        if (!Runner.IsServer) return false;
        if (maxTotalMonsters > 0 && GetActiveMonsterCount() >= maxTotalMonsters)
            return false;
        switch (spawnData.spawnAreaType)
        {
            case SpawnAreaType.PointRadius:
                return ProcessPointRadiusSpawn(spawnData);
            case SpawnAreaType.AreaBased:
                return ProcessAreaBasedSpawn(spawnData);
            case SpawnAreaType.MultiplePoints:
                return ProcessMultiplePointsSpawn(spawnData);
            default:
                return false;
        }
    }

    private bool ProcessPointRadiusSpawn(MonsterSpawnData spawnData)
    {
        if (!Runner.IsServer) return false;
        int currentCount = GetMonsterCountByType(spawnData.monsterName);
        int maxCount = spawnData.maxMonstersInArea;
        if (currentCount >= maxCount)
        {
            spawnData.isInitialSpawnComplete = true;
            return false;
        }
        if (spawnData.isInitialSpawnComplete)
        {
            float timeSinceLastSpawn = (float)Runner.SimulationTime - spawnData.lastSpawnTime;
            if (timeSinceLastSpawn < spawnData.respawnTime)
                return false;
        }
        Vector2 spawnPosition = FindValidSpawnPosition(spawnData);
        if (spawnPosition != Vector2.zero)
            return SpawnMonster(spawnData, spawnPosition, "MainSpawn");
        return false;
    }

    private bool ProcessAreaBasedSpawn(MonsterSpawnData spawnData)
    {
        if (!Runner.IsServer) return false;
        bool anySpawned = false;
        foreach (var areaWithLimit in spawnData.spawnAreasWithLimits)
        {
            if (areaWithLimit?.IsValid != true) 
                continue;
            AreaData area = areaWithLimit.area;
            int currentCount = GetMonsterCountInLocationByType(area.areaName, spawnData.monsterName);
            int maxCount = areaWithLimit.maxMonstersInThisArea;
            if (currentCount >= maxCount)
            {
                areaWithLimit.isInitialSpawnComplete = true;
                continue;
            }
            if (areaWithLimit.isInitialSpawnComplete)
            {
                float timeSinceLastSpawn = (float)Runner.SimulationTime - spawnData.lastSpawnTime;
                if (timeSinceLastSpawn < spawnData.respawnTime)
                    continue;
            }
            Vector2 spawnPosition = FindValidSpawnPositionInArea(area, areaWithLimit.SelectedSubAreas);
            if (spawnPosition != Vector2.zero)
            {
                if (SpawnMonster(spawnData, spawnPosition, area.areaName))
                {
                    anySpawned = true;
                    break;
                }
            }
        }
        return anySpawned;
    }

private bool ProcessMultiplePointsSpawn(MonsterSpawnData spawnData)
{
    if (!Runner.IsServer) return false;

    // Tüm pointler için ortak location prefix
    string sharedLocationPrefix = $"{spawnData.monsterName}_MultiPoint";

    // Toplam monster sayısını kontrol et
    int totalCurrentCount = GetMonsterCountByLocationPrefix(sharedLocationPrefix);
    int totalMaxCount = spawnData.maxMonstersInArea;

    if (totalCurrentCount >= totalMaxCount)
    {
        spawnData.isInitialSpawnComplete = true;
        return false;
    }

    // Respawn zamanı kontrolü
    if (spawnData.isInitialSpawnComplete)
    {
        float timeSinceLastSpawn = (float)Runner.SimulationTime - spawnData.lastSpawnTime;
        if (timeSinceLastSpawn < spawnData.respawnTime)
            return false;
    }

    // Geçerli spawn pointleri listele - sadece limitine ulaşmayanlar
    List<SpawnPoint> availablePoints = new List<SpawnPoint>();
    List<int> availablePointIndices = new List<int>();

    for (int i = 0; i < spawnData.spawnPoints.Count; i++)
    {
        SpawnPoint spawnPoint = spawnData.spawnPoints[i];
        if (spawnPoint != null)
        {
            // Her point için unique location name
            string pointLocationName = $"{sharedLocationPrefix}_Point{i}";
            int pointCurrentCount = GetMonsterCountInLocation(pointLocationName);

            // Bu point henüz limitine ulaşmamışsa ekle
            if (pointCurrentCount < spawnPoint.maxMonstersInPoint)
            {
                availablePoints.Add(spawnPoint);
                availablePointIndices.Add(i);
            }
        }
    }

    if (availablePoints.Count == 0)
        return false;

    // Rastgele bir available point seç
    int randomIndex = Random.Range(0, availablePoints.Count);
    SpawnPoint selectedPoint = availablePoints[randomIndex];
    int selectedPointIndex = availablePointIndices[randomIndex];
    string selectedPointLocationName = $"{sharedLocationPrefix}_Point{selectedPointIndex}";

    // Seçilen pointte spawn et
    Vector2 spawnPosition = FindValidSpawnPositionInPoint(selectedPoint);
    if (spawnPosition != Vector2.zero)
    {
        if (SpawnMonster(spawnData, spawnPosition, selectedPointLocationName))
        {
            spawnData.lastSpawnTime = (float)Runner.SimulationTime;
            spawnData.isInitialSpawnComplete = true;
            return true;
        }
    }

    return false;
}
    #endregion

    #region Monster Creation
    private bool SpawnMonster(MonsterSpawnData spawnData, Vector2 spawnPosition, string locationName)
    {
        if (!Runner.IsServer) return false;
        try
        {
            MonsterRarity rarity = DetermineMonsterRarity(spawnData);
            NetworkObject monsterPrefab = spawnData.monsterPrefab.GetComponent<NetworkObject>();
            if (monsterPrefab == null)
                return false;
            NetworkObject monster = Runner.Spawn(monsterPrefab, spawnPosition, Quaternion.identity);
            if (monster != null)
            {
                ConfigureSpawnedMonster(monster, spawnData, rarity, locationName);
                OrganizeMonsterInServerHierarchy(monster, spawnData, locationName);
                spawnData.lastSpawnTime = (float)Runner.SimulationTime;
                TotalMonstersSpawned++;
                return true;
            }
        }
        catch (System.Exception ) { }
        return false;
    }

    private void ConfigureSpawnedMonster(NetworkObject monster, MonsterSpawnData spawnData, MonsterRarity rarity, string locationName)
    {
        if (!Runner.IsServer) return;
        MonsterBehaviour behavior = monster.GetComponent<MonsterBehaviour>();
        if (behavior == null) return;
        int monsterLevel = behavior.monsterLevel;
        GetRaritySettings(rarity, out Color tintColor, out float combatMult, out float speedMult, out float sizeMult);
        string monsterName = spawnData.monsterName;
        SpawnArea tempSpawnArea = CreateTempSpawnArea(spawnData);
        behavior.InitializeMonster(monsterName, tempSpawnArea);
        behavior.Initialize(rarity, tintColor, combatMult, speedMult, sizeMult, monsterLevel);
        RegisterMonster(monster.Id, monsterName, monster.transform.position, locationName, spawnData.spawnAreaType);
    }

    private void OrganizeMonsterInServerHierarchy(NetworkObject monster, MonsterSpawnData spawnData, string locationName)
    {
        if (!Runner.IsServer) return;
        if (serverHierarchyContainer == null) return;
        Transform locationContainer = GetOrCreateLocationContainer(locationName);
        Transform typeContainer = GetOrCreateTypeContainer(locationContainer, spawnData.monsterName);
        monster.transform.SetParent(typeContainer);
    }

    private Transform GetOrCreateLocationContainer(string locationName)
    {
        if (!Runner.IsServer) return null;
        foreach (Transform child in serverHierarchyContainer)
        {
            if (child.name == locationName)
                return child;
        }
        GameObject locationObj = new GameObject(locationName);
        locationObj.transform.SetParent(serverHierarchyContainer);
        return locationObj.transform;
    }

    private Transform GetOrCreateTypeContainer(Transform parent, string typeName)
    {
        if (!Runner.IsServer) return null;
        foreach (Transform child in parent)
        {
            if (child.name == typeName)
                return child;
        }
        GameObject typeObj = new GameObject(typeName);
        typeObj.transform.SetParent(parent);
        return typeObj.transform;
    }
    #endregion

    #region Position Validation
    private Vector2 FindValidSpawnPosition(MonsterSpawnData spawnData)
    {
        const int maxAttempts = 15;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 candidatePosition = GetRandomPositionInSpawnArea(spawnData);
            if (IsPositionValidForSpawn(candidatePosition))
                return candidatePosition;
        }
        return Vector2.zero;
    }

    private Vector2 FindValidSpawnPositionInArea(AreaData area, List<int> selectedSubAreas = null)
    {
        const int maxAttempts = 15;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 candidatePosition = GetRandomPositionInAreaWithSubAreas(area, selectedSubAreas);
            if (IsPositionValidForSpawn(candidatePosition))
                return candidatePosition;
        }
        return Vector2.zero;
    }

    private Vector2 GetRandomPositionInAreaWithSubAreas(AreaData area, List<int> selectedSubAreas = null)
    {
        if (selectedSubAreas == null || selectedSubAreas.Count == 0)
            return GetRandomPositionInArea(area);
        int randomIndex = Random.Range(0, selectedSubAreas.Count);
        int selectedSubArea = selectedSubAreas[randomIndex];
        return GetRandomPositionInSubArea(area, selectedSubArea);
    }

    private Vector2 GetRandomPositionInSubArea(AreaData area, int subAreaNumber)
    {
        Vector2 subAreaBottomLeft = area.GetSubAreaBottomLeft(subAreaNumber);
        Vector2 subAreaTopRight = area.GetSubAreaTopRight(subAreaNumber);
        float randomX = Random.Range(subAreaBottomLeft.x, subAreaTopRight.x);
        float randomY = Random.Range(subAreaBottomLeft.y, subAreaTopRight.y);
        return new Vector2(randomX, randomY);
    }

    private Vector2 FindValidSpawnPositionInPoint(SpawnPoint spawnPoint)
    {
        const int maxAttempts = 15;
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 candidatePosition = GetRandomPositionInPoint(spawnPoint);
            if (IsPositionValidForSpawn(candidatePosition))
                return candidatePosition;
        }
        return Vector2.zero;
    }

    private Vector2 GetRandomPositionInSpawnArea(MonsterSpawnData spawnData)
    {
        switch (spawnData.spawnAreaType)
        {
            case SpawnAreaType.PointRadius:
                return GetRandomPositionInCircle(spawnData.spawnPoint, spawnData.spawnRadius);
            case SpawnAreaType.AreaBased:
                if (spawnData.spawnAreasWithLimits.Count > 0 && spawnData.spawnAreasWithLimits[0]?.IsValid == true)
                    return GetRandomPositionInArea(spawnData.spawnAreasWithLimits[0].area);
                return Vector2.zero;
            default:
                return Vector2.zero;
        }
    }

    private Vector2 GetRandomPositionInArea(AreaData area)
    {
        float randomX = Random.Range(area.bottomLeftCorner.x, area.topRightCorner.x);
        float randomY = Random.Range(area.bottomLeftCorner.y, area.topRightCorner.y);
        return new Vector2(randomX, randomY);
    }

    private Vector2 GetRandomPositionInPoint(SpawnPoint spawnPoint)
    {
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float randomRadius = Random.Range(0.2f, 0.9f) * spawnPoint.spawnRadius;
        return spawnPoint.position + new Vector2(Mathf.Cos(randomAngle) * randomRadius, Mathf.Sin(randomAngle) * randomRadius);
    }

    private Vector2 GetRandomPositionInCircle(Vector2 center, float radius)
    {
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float randomRadius = Random.Range(0.2f, 0.9f) * radius;
        return center + new Vector2(Mathf.Cos(randomAngle) * randomRadius, Mathf.Sin(randomAngle) * randomRadius);
    }

    private bool IsPositionValidForSpawn(Vector2 position)
    {
        return IsPositionSafeFromPlayers(position) && !IsPositionOnObstacleTile(position) && !IsPositionTooCloseToMonsters(position);
    }

    private bool IsPositionSafeFromPlayers(Vector2 position)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        return players.All(player => player == null || Vector2.Distance(player.transform.position, position) >= playerSafeDistance);
    }

    private bool IsPositionTooCloseToMonsters(Vector2 position)
    {
        Collider2D[] existingMonsters = Physics2D.OverlapCircleAll(position, monsterSpacingDistance, LayerMask.GetMask("Enemy"));
        return existingMonsters.Length > 0;
    }

    private bool IsPositionOnObstacleTile(Vector2 worldPosition)
    {
        if (cachedTilemaps == null || cachedTilemaps.Length == 0)
            return false;
        return cachedTilemaps.Any(tilemap => tilemap != null && tilemap.GetTile(tilemap.WorldToCell(worldPosition)) != null);
    }
    #endregion

    #region Monster Configuration
    private MonsterRarity DetermineMonsterRarity(MonsterSpawnData spawnData)
    {
        float rareRoll = Random.Range(0f, 100f);
        if (rareRoll <= spawnData.rareChance)
            return MonsterRarity.Rare;
        float magicRoll = Random.Range(0f, 100f);
        if (magicRoll <= spawnData.magicChance)
            return MonsterRarity.Magic;
        return MonsterRarity.Normal;
    }

    private void GetRaritySettings(MonsterRarity rarity, out Color tintColor, out float combatMult, out float speedMult, out float sizeMult)
    {
        switch (rarity)
        {
            case MonsterRarity.Magic:
                tintColor = MonsterRaritySettings.MAGIC_TINT;
                combatMult = MonsterRaritySettings.MAGIC_COMBAT_MULTIPLIER;
                speedMult = MonsterRaritySettings.MAGIC_SPEED_MULTIPLIER;
                sizeMult = MonsterRaritySettings.MAGIC_SIZE_MULTIPLIER;
                break;
            case MonsterRarity.Rare:
                tintColor = MonsterRaritySettings.RARE_TINT;
                combatMult = MonsterRaritySettings.RARE_COMBAT_MULTIPLIER;
                speedMult = MonsterRaritySettings.RARE_SPEED_MULTIPLIER;
                sizeMult = MonsterRaritySettings.RARE_SIZE_MULTIPLIER;
                break;
            default:
                tintColor = Color.white;
                combatMult = 1f;
                speedMult = 1f;
                sizeMult = 1f;
                break;
        }
    }

    private SpawnArea CreateTempSpawnArea(MonsterSpawnData spawnData)
    {
        SpawnArea tempArea = new SpawnArea
        {
            monsterType = spawnData.monsterName,
            maxMonstersInArea = spawnData.maxMonstersInArea,
            lastSpawnTime = spawnData.lastSpawnTime
        };
        switch (spawnData.spawnAreaType)
        {
            case SpawnAreaType.PointRadius:
                tempArea.position = spawnData.spawnPoint;
                tempArea.spawnRadius = spawnData.spawnRadius;
                break;
            case SpawnAreaType.AreaBased:
                if (spawnData.spawnAreasWithLimits.Count > 0 && spawnData.spawnAreasWithLimits[0]?.IsValid == true)
                {
                    AreaData firstArea = spawnData.spawnAreasWithLimits[0].area;
                    tempArea.position = firstArea.AreaCenter;
                    tempArea.spawnRadius = Mathf.Max(firstArea.AreaSize.x, firstArea.AreaSize.y) / 2f;
                }
                else
                {
                    tempArea.position = Vector2.zero;
                    tempArea.spawnRadius = 4f;
                }
                break;
            case SpawnAreaType.MultiplePoints:
                if (spawnData.spawnPoints.Count > 0 && spawnData.spawnPoints[0] != null)
                {
                    SpawnPoint firstPoint = spawnData.spawnPoints[0];
                    tempArea.position = firstPoint.position;
                    tempArea.spawnRadius = firstPoint.spawnRadius;
                }
                else
                {
                    tempArea.position = Vector2.zero;
                    tempArea.spawnRadius = 4f;
                }
                break;
        }
        return tempArea;
    }

    public string GetMonsterLimitInfo()
    {
        if (Runner.IsServer)
        {
            int active = GetActiveMonsterCount();
            if (maxTotalMonsters > 0)
                return $"{active}/{maxTotalMonsters}";
            else
                return $"{active}/∞";
        }
        return "Client Mode";
    }

    public int GetMaxTotalMonsters() { return maxTotalMonsters; }
    public void SetMaxTotalMonsters(int limit)
    {
        if (Runner.IsServer)
            maxTotalMonsters = limit;
    }

private int GetMaxMonsterCountForSpawnData(MonsterSpawnData spawnData)
{
    switch (spawnData.spawnAreaType)
    {
        case SpawnAreaType.PointRadius:
            return spawnData.maxMonstersInArea;
        case SpawnAreaType.AreaBased:
            return spawnData.spawnAreasWithLimits.Sum(a => a?.maxMonstersInThisArea ?? 0);
        case SpawnAreaType.MultiplePoints:
            return spawnData.maxMonstersInArea; // Değişti: artık toplam limit
        default:
            return spawnData.maxMonstersInArea;
    }
}
    #endregion

    #region Client UI Support
    public Dictionary<string, int> GetMonsterBreakdownForArea(string areaName)
    {
        Dictionary<string, int> breakdown = new Dictionary<string, int>();
        if (Runner.IsServer && monsterRegistry != null)
        {
            foreach (var entry in monsterRegistry.Values)
            {
                if (entry.locationName == areaName)
                {
                    if (!breakdown.ContainsKey(entry.monsterType))
                        breakdown[entry.monsterType] = 0;
                    breakdown[entry.monsterType]++;
                }
            }
        }
        return breakdown;
    }

    public int GetMaxMonstersForArea(string areaName)
    {
        int maxCount = 0;
        foreach (var spawnData in monsterSpawnList)
        {
            if (spawnData.spawnAreaType == SpawnAreaType.AreaBased)
            {
                foreach (var areaLimit in spawnData.spawnAreasWithLimits)
                {
                    if (areaLimit?.IsValid == true && areaLimit.area.areaName == areaName)
                        maxCount += areaLimit.maxMonstersInThisArea;
                }
            }
        }
        return maxCount;
    }
    #endregion

    #region Public API
    public int GetTotalMonstersSpawned() { return TotalMonstersSpawned; }
    public bool IsSpawnerInitialized() { return IsInitialized; }
    #endregion

    #region Editor Utilities
    [ContextMenu("Enable All Spawns")]
    private void EnableAllSpawns()
    {
        foreach (var spawnData in monsterSpawnList)
        {
            if (spawnData != null)
                spawnData.SetEnabled(true);
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Disable All Spawns")]
    private void DisableAllSpawns()
    {
        foreach (var spawnData in monsterSpawnList)
        {
            if (spawnData != null)
                spawnData.SetEnabled(false);
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Toggle All Spawns")]
    private void ToggleAllSpawns()
    {
        foreach (var spawnData in monsterSpawnList)
        {
            if (spawnData != null)
                spawnData.SetEnabled(!spawnData.IsEnabled);
        }
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    [ContextMenu("Show Spawn Status")]
    private void ShowSpawnStatus()
    {
        int enabledCount = monsterSpawnList.Count(sd => sd.IsEnabled);
        int disabledCount = monsterSpawnList.Count - enabledCount;
    }
    #endregion
}