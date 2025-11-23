using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class MonsterDropInfo
{
    public string monsterName;
    public float dropChance;
    public List<Vector2> spawnLocations = new List<Vector2>(); // Mevcut - geriye uyumluluk için
    
    // Yeni alanlar
    public List<string> areaNames = new List<string>(); // Area based için area isimleri
    public SpawnAreaType spawnAreaType; // Hangi tip spawn area olduğu
}

public class CraftDropManager : MonoBehaviour
{
    public static CraftDropManager Instance { get; private set; }
    
    private Dictionary<string, List<MonsterDropInfo>> craftDropDatabase = new Dictionary<string, List<MonsterDropInfo>>();
    private bool isDatabaseInitialized = false;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        InitializeCraftDropDatabase();
    }
    
    private void InitializeCraftDropDatabase()
    {
        if (isDatabaseInitialized) return;
        
        craftDropDatabase.Clear();
        
        // Sahnedeki tüm MonsterSpawner'ları bul
        MonsterSpawner[] spawners = FindObjectsByType<MonsterSpawner>(FindObjectsSortMode.None);
        
        foreach (var spawner in spawners)
        {
            ProcessSpawnerData(spawner);
        }
        
        isDatabaseInitialized = true;
    }
    
    private void ProcessSpawnerData(MonsterSpawner spawner)
    {
        // MonsterSpawner'dan spawn data'larını al
        var spawnDataList = GetSpawnDataFromSpawner(spawner);
        
        foreach (var spawnData in spawnDataList)
        {
            ProcessMonsterSpawnData(spawnData);
        }
    }
    
    private List<MonsterSpawnData> GetSpawnDataFromSpawner(MonsterSpawner spawner)
    {
        // Reflection ile private monsterSpawnList'e eriş
        var field = spawner.GetType().GetField("monsterSpawnList", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        if (field != null)
        {
            var spawnList = field.GetValue(spawner) as List<MonsterSpawnData>;
            return spawnList ?? new List<MonsterSpawnData>();
        }
        
        return new List<MonsterSpawnData>();
    }

private void ProcessMonsterSpawnData(MonsterSpawnData spawnData)
{
    if (spawnData.monsterPrefab == null) return;

    MonsterBehaviour monsterBehaviour = spawnData.monsterPrefab.GetComponent<MonsterBehaviour>();
    if (monsterBehaviour == null) return;

    var craftDrops = GetCraftDropsFromMonster(monsterBehaviour);
    if (craftDrops == null || craftDrops.Count == 0) return;

    List<Vector2> spawnLocations = CalculateAllSpawnLocations(spawnData);
    List<string> areaNames = CalculateAreaNames(spawnData);

    if (spawnLocations.Count == 0) return;

    foreach (var dropData in craftDrops)
    {
        if (dropData.item == null || !dropData.item.IsCraftItem()) continue;

        string itemId = dropData.item.itemId;

        if (!craftDropDatabase.ContainsKey(itemId))
        {
            craftDropDatabase[itemId] = new List<MonsterDropInfo>();
        }

        var existingMonster = craftDropDatabase[itemId].FirstOrDefault(m => m.monsterName == spawnData.monsterName);

        if (existingMonster != null)
        {
            foreach (var location in spawnLocations)
            {
                if (!existingMonster.spawnLocations.Contains(location))
                {
                    existingMonster.spawnLocations.Add(location);
                }
            }

            foreach (var areaName in areaNames)
            {
                if (!string.IsNullOrEmpty(areaName) && !existingMonster.areaNames.Contains(areaName))
                {
                    existingMonster.areaNames.Add(areaName);
                }
            }
        }
        else
        {
            var monsterInfo = new MonsterDropInfo
            {
                monsterName = spawnData.monsterName,
                dropChance = dropData.dropChance,
                spawnLocations = new List<Vector2>(spawnLocations),
                areaNames = new List<string>(areaNames),
                spawnAreaType = spawnData.spawnAreaType
            };

            craftDropDatabase[itemId].Add(monsterInfo);
        }
    }
}
    // Craft item'ın drop lokasyonlarını formatlanmış string olarak al
public string GetFormattedDropLocations(string itemId)
{
    var dropInfos = GetDropInfoForCraftItem(itemId);
    if (dropInfos.Count == 0) return "Drop bilgisi bulunamadı";
    
    List<string> locationTexts = new List<string>();
    
    foreach (var dropInfo in dropInfos)
    {
        string locationText = "";
        
        switch (dropInfo.spawnAreaType)
        {
            case SpawnAreaType.AreaBased:
                // Area based için area isimlerini göster
                if (dropInfo.areaNames.Count > 0)
                {
                    locationText = $"{dropInfo.monsterName}: {string.Join(", ", dropInfo.areaNames)}";
                }
                else
                {
                    // Fallback olarak koordinatları göster
                    var coords = dropInfo.spawnLocations.Select(loc => $"({loc.x:F1}, {loc.y:F1})");
                    locationText = $"{dropInfo.monsterName}: {string.Join(", ", coords)}";
                }
                break;
                
            case SpawnAreaType.PointRadius:
                // Point radius için koordinatları göster
                var pointCoords = dropInfo.spawnLocations.Select(loc => $"({loc.x:F1}, {loc.y:F1})");
                locationText = $"{dropInfo.monsterName}: {string.Join(", ", pointCoords)}";
                break;
                
            case SpawnAreaType.MultiplePoints:
                // Multiple points için point isimlerini öncelikle göster
                if (dropInfo.areaNames.Count > 0)
                {
                    locationText = $"{dropInfo.monsterName}: {string.Join(", ", dropInfo.areaNames)}";
                }
                else
                {
                    // Fallback olarak koordinatları göster
                    var multiPointCoords = dropInfo.spawnLocations.Select(loc => $"({loc.x:F1}, {loc.y:F1})");
                    locationText = $"{dropInfo.monsterName}: {string.Join(", ", multiPointCoords)}";
                }
                break;
        }
        
        if (!string.IsNullOrEmpty(locationText))
        {
            locationTexts.Add(locationText);
        }
    }
    
    return string.Join("\n", locationTexts);
}

// Sadece area isimlerini al (UI için)
public List<string> GetAreaNamesForCraftItem(string itemId)
{
    var dropInfos = GetDropInfoForCraftItem(itemId);
    List<string> allAreaNames = new List<string>();
    
    foreach (var dropInfo in dropInfos)
    {
        if (dropInfo.spawnAreaType == SpawnAreaType.AreaBased || 
            dropInfo.spawnAreaType == SpawnAreaType.MultiplePoints)
        {
            allAreaNames.AddRange(dropInfo.areaNames);
        }
    }
    
    return allAreaNames.Distinct().ToList();
}

public List<Vector2> GetCoordinatesForCraftItem(string itemId)
{
    var dropInfos = GetDropInfoForCraftItem(itemId);
    List<Vector2> allCoordinates = new List<Vector2>();
    
    foreach (var dropInfo in dropInfos)
    {
        if (dropInfo.spawnAreaType == SpawnAreaType.PointRadius || 
            dropInfo.spawnAreaType == SpawnAreaType.MultiplePoints)
        {
            allCoordinates.AddRange(dropInfo.spawnLocations);
        }
    }
    
    return allCoordinates.Distinct().ToList();
}
private List<string> CalculateAreaNames(MonsterSpawnData spawnData)
{
    List<string> areaNames = new List<string>();

    switch (spawnData.spawnAreaType)
    {
        case SpawnAreaType.PointRadius:
            // Point radius için area name yok
            break;

        case SpawnAreaType.AreaBased:
            // YENİ SİSTEM: spawnAreasWithLimits kullan
            foreach (var areaWithLimit in spawnData.spawnAreasWithLimits)
            {
                if (areaWithLimit != null && areaWithLimit.IsValid && 
                    !string.IsNullOrEmpty(areaWithLimit.area.areaName))
                {
                    areaNames.Add(areaWithLimit.area.areaName);
                }
            }
            break;
            
        case SpawnAreaType.MultiplePoints:
            foreach (var point in spawnData.spawnPoints)
            {
                if (point != null && !string.IsNullOrEmpty(point.pointName))
                {
                    areaNames.Add(point.pointName);
                }
            }
            break;
    }

    return areaNames;
}
private List<Vector2> CalculateAllSpawnLocations(MonsterSpawnData spawnData)
{
    List<Vector2> locations = new List<Vector2>();
    
    switch (spawnData.spawnAreaType)
    {
        case SpawnAreaType.PointRadius:
            locations.Add(spawnData.spawnPoint);
            break;
            
        case SpawnAreaType.AreaBased:
            // YENİ SİSTEM: spawnAreasWithLimits kullan
            foreach (var areaWithLimit in spawnData.spawnAreasWithLimits)
            {
                if (areaWithLimit != null && areaWithLimit.IsValid)
                {
                    locations.Add(areaWithLimit.area.AreaCenter);
                }
            }
            break;
            
        case SpawnAreaType.MultiplePoints:
            foreach (var point in spawnData.spawnPoints)
            {
                if (point != null)
                {
                    locations.Add(point.position);
                }
            }
            break;
    }
    
    return locations;
}
    
private List<ItemDropData> GetCraftDropsFromMonster(MonsterBehaviour monsterBehaviour)
{
    // MonsterLootSystem component'ini al
    MonsterLootSystem lootSystem = monsterBehaviour.GetComponent<MonsterLootSystem>();
    
    if (lootSystem == null)
    {
        return new List<ItemDropData>();
    }
    
    // Reflection ile MonsterLootSystem'den craftMaterialDrops'ı al
    var field = lootSystem.GetType().GetField("craftMaterialDrops", 
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    
    if (field != null)
    {
        return field.GetValue(lootSystem) as List<ItemDropData>;
    }
    
    return new List<ItemDropData>();
}
    
    public List<MonsterDropInfo> GetDropInfoForCraftItem(string itemId)
    {
        if (craftDropDatabase.TryGetValue(itemId, out List<MonsterDropInfo> dropInfo))
        {
            return new List<MonsterDropInfo>(dropInfo);
        }
        
        return new List<MonsterDropInfo>();
    }
    
    public bool HasDropInfo(string itemId)
    {
        return craftDropDatabase.ContainsKey(itemId) && craftDropDatabase[itemId].Count > 0;
    }
    
    // Debug için
    [ContextMenu("Debug Database")]
    private void DebugDatabase()
    {
        
        foreach (var kvp in craftDropDatabase)
        {
            string itemId = kvp.Key;
            var monsters = kvp.Value;
            
            foreach (var monster in monsters)
            {
            }
        }
    }
    [ContextMenu("Test - Reinitialize Database")]
private void ReinitializeDatabase()
{
    isDatabaseInitialized = false;
    InitializeCraftDropDatabase();
    Debug.Log($"[CraftDropManager] Database reinitialized. Total items: {craftDropDatabase.Count}");
    
    foreach (var kvp in craftDropDatabase)
    {
        Debug.Log($"Item: {kvp.Key}, Monsters: {kvp.Value.Count}");
        foreach (var monster in kvp.Value)
        {
            Debug.Log($"  - {monster.monsterName} | Type: {monster.spawnAreaType} | Locations: {monster.spawnLocations.Count} | Areas: {monster.areaNames.Count}");
        }
    }
}
}