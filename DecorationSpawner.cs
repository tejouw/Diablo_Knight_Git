using UnityEngine;
using Fusion;
using System.Collections.Generic;

[System.Serializable]
public class DecorationSpawnData
{
    [Header("Decoration Settings")]
    public string decorationName = "Decoration";
    public GameObject decorationPrefab;
    public AreaData spawnArea;
    public int maxDecorationsInArea = 10;
    public float minDistanceBetweenDecorations = 2f;
    
    [Header("Spawn Settings")]
    public bool spawnOnStart = true;
    public float respawnCheckInterval = 5f; // Gelecekte yok edilenler için
    
    // Runtime data
    [System.NonSerialized]
    public float lastSpawnCheck = 0f;
}

public class DecorationSpawner : NetworkBehaviour
{
    #region Configuration & Setup
    
    [Header("Decoration Spawn Configuration")]
    [SerializeField] private List<DecorationSpawnData> decorationSpawnList = new List<DecorationSpawnData>();
    
    [Header("Spawner Settings")]
    [SerializeField] private float checkInterval = 5f; // 5 saniyeye çıkar
    [SerializeField] private float obstacleCheckRadius = 0.5f;
    
    // Networked Properties
    [Networked] public TickTimer SpawnCheckTimer { get; set; }
    
    // Private Fields
    private bool isInitialized = false;
    private Dictionary<string, List<GameObject>> spawnedDecorations = new Dictionary<string, List<GameObject>>();
    
    #endregion
    
    #region Network Lifecycle
    
    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            InitializeSpawner();
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        // SADECE server çalıştırır ve daha az sıklıkta
        if (!isInitialized || !Object.HasStateAuthority) return;

        if (SpawnCheckTimer.ExpiredOrNotRunning(Runner))
        {
            SpawnCheckTimer = TickTimer.CreateFromSeconds(Runner, checkInterval);
            PerformSpawnCheck();
        }
    }
    
    #endregion
    
    #region Initialization
    
    private void InitializeSpawner()
    {
        InitializeDecorationLists();
        isInitialized = true;
        SpawnCheckTimer = TickTimer.CreateFromSeconds(Runner, 0f); // İlk spawn hemen
        
    }
    
    private void InitializeDecorationLists()
    {
        if (spawnedDecorations == null)
            spawnedDecorations = new Dictionary<string, List<GameObject>>();
        else
            spawnedDecorations.Clear();

        foreach (var spawnData in decorationSpawnList)
        {
            if (spawnData.decorationPrefab == null || spawnData.spawnArea == null) continue;
            
            string decorationKey = GetDecorationKey(spawnData);
            spawnedDecorations[decorationKey] = new List<GameObject>();
            
            // Mevcut dekorasyonları kaydet
            CountExistingDecorationsOfType(spawnData);
        }
    }
    
    private string GetDecorationKey(DecorationSpawnData spawnData)
    {
        return $"{spawnData.decorationName}_{spawnData.spawnArea.areaName}";
    }
    
    #endregion
    
    #region Spawn Logic
    
    private void PerformSpawnCheck()
    {
        // Her 10 saniyede bir cleanup yap, her check'te değil
        if ((float)Runner.SimulationTime % 10f < checkInterval)
        {
            CleanupAllNullDecorations();
        }
        
        foreach (var spawnData in decorationSpawnList)
        {
            if (!CanSpawnDecoration(spawnData)) continue;
            
            Vector2 spawnPosition = FindValidSpawnPosition(spawnData);
            if (spawnPosition == Vector2.zero) continue;
            
            SpawnDecoration(spawnData, spawnPosition);
        }
    }
    
    private bool CanSpawnDecoration(DecorationSpawnData spawnData)
    {
        // Prefab ve area kontrolü
        if (spawnData.decorationPrefab == null || spawnData.spawnArea == null) return false;
        
        // Decoration sayısı kontrolü
        string decorationKey = GetDecorationKey(spawnData);
        if (!spawnedDecorations.ContainsKey(decorationKey))
        {
            spawnedDecorations[decorationKey] = new List<GameObject>();
        }
        
        int currentCount = spawnedDecorations[decorationKey].Count;
        if (currentCount >= spawnData.maxDecorationsInArea) return false;
        
        return true;
    }
    
    private void CleanupAllNullDecorations()
    {
        foreach (var kvp in spawnedDecorations)
        {
            kvp.Value.RemoveAll(decoration => decoration == null);
        }
    }
    
    private void CleanupNullDecorations(string decorationKey)
    {
        if (!spawnedDecorations.ContainsKey(decorationKey)) return;
        
        spawnedDecorations[decorationKey].RemoveAll(decoration => decoration == null);
    }
    
    #endregion
    
    #region Position Finding
    
    private Vector2 FindValidSpawnPosition(DecorationSpawnData spawnData)
    {
        const int maxAttempts = 50;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            Vector2 candidatePosition = GetRandomPositionInArea(spawnData.spawnArea);
            
            if (IsPositionValidForSpawn(candidatePosition, spawnData))
            {
                return candidatePosition;
            }
        }
        
        return Vector2.zero; // Uygun pozisyon bulunamadı
    }
    
    private Vector2 GetRandomPositionInArea(AreaData area)
    {
        float randomX = Random.Range(area.bottomLeftCorner.x, area.topRightCorner.x);
        float randomY = Random.Range(area.bottomLeftCorner.y, area.topRightCorner.y);
        
        return new Vector2(randomX, randomY);
    }
    
    private bool IsPositionValidForSpawn(Vector2 position, DecorationSpawnData spawnData)
    {
        // Area içinde mi kontrol et
        if (!spawnData.spawnArea.IsPositionInArea(position)) return false;
        
        // Engel kontrolü
        if (IsPositionBlocked(position)) return false;
        
        // Diğer decorasyonlardan mesafe kontrolü
        if (IsPositionTooCloseToOtherDecorations(position, spawnData)) return false;
        
        return true;
    }
    
    private bool IsPositionBlocked(Vector2 position)
    {
        Collider2D obstacle = Physics2D.OverlapCircle(position, obstacleCheckRadius, 
            LayerMask.GetMask("Obstacles", "Wall", "Default"));
        return obstacle != null;
    }
    
    private bool IsPositionTooCloseToOtherDecorations(Vector2 position, DecorationSpawnData spawnData)
    {
        string decorationKey = GetDecorationKey(spawnData);
        if (!spawnedDecorations.ContainsKey(decorationKey)) return false;
        
        foreach (GameObject decoration in spawnedDecorations[decorationKey])
        {
            if (decoration == null) continue;
            
            float distance = Vector2.Distance(decoration.transform.position, position);
            if (distance < spawnData.minDistanceBetweenDecorations)
            {
                return true;
            }
        }
        
        // Diğer tipteki decorasyonlarla da mesafe kontrolü
        foreach (var kvp in spawnedDecorations)
        {
            if (kvp.Key == decorationKey) continue; // Aynı tip, zaten kontrol edildi
            
            foreach (GameObject decoration in kvp.Value)
            {
                if (decoration == null) continue;
                
                float distance = Vector2.Distance(decoration.transform.position, position);
                if (distance < 1f) // Genel minimum mesafe
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    #endregion
    
    #region Decoration Creation
    
    private void SpawnDecoration(DecorationSpawnData spawnData, Vector2 spawnPosition)
    {
        try
        {
            // SPAWN DELAY EKLE - Server'da her frame spawn etmesin
            if ((float)Runner.SimulationTime - spawnData.lastSpawnCheck < 1f)
            {
                return;
            }
            spawnData.lastSpawnCheck = (float)Runner.SimulationTime;
            
            NetworkObject decorationPrefab = Resources.Load<NetworkObject>("Decorations/" + spawnData.decorationPrefab.name);
            
            if (decorationPrefab == null)
            {
                Debug.LogError($"[DecorationSpawner] Decoration prefab not found in Resources/Decorations: {spawnData.decorationPrefab.name}");
                return;
            }
            
            NetworkObject decoration = Runner.Spawn(decorationPrefab, spawnPosition, Quaternion.identity);
            
            if (decoration != null)
            {
                ConfigureSpawnedDecoration(decoration, spawnData);
                OrganizeDecorationInHierarchy(decoration, spawnData);
                
                // Spawned decorations listesine ekle
                string decorationKey = GetDecorationKey(spawnData);
                spawnedDecorations[decorationKey].Add(decoration.gameObject);
                
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[DecorationSpawner] Spawn error for {spawnData.decorationName}: {e.Message}");
        }
    }
    
    private void ConfigureSpawnedDecoration(NetworkObject decoration, DecorationSpawnData spawnData)
    {
        // Decoration tag ayarla
        decoration.gameObject.tag = "Decoration";
        
        // Layer ayarla
        decoration.gameObject.layer = LayerMask.NameToLayer("Default");
        
        // Network ayarları - Client prediction kapatılsın
        if (decoration.TryGetComponent<NetworkTransform>(out var netTransform))
        {
            // Decoration'lar hareket etmeyecek, interpolation kapat
        }
    }
    
    #endregion
    
    #region Decoration Organization
    
    private void OrganizeDecorationInHierarchy(NetworkObject decoration, DecorationSpawnData spawnData)
    {
        // SADECE isim değiştir, parent değiştirme - Fusion NetworkObject'lerde problem çıkarabiliyor
        string newName = $"{spawnData.decorationName}_{spawnData.spawnArea.areaName}";
        decoration.gameObject.name = newName;
        
        // Parent ayarlama yerine sadece hierarchy'de organize etme
        // Gelecekte gerekirse transform.SetParent çok dikkatli kullanılabilir
    }
    
    private Transform GetOrCreateDecorationsContainer()
    {
        Transform decorationsContainer = GameObject.Find("Decorations")?.transform;
        
        if (decorationsContainer == null)
        {
            GameObject decorationsObj = new GameObject("Decorations");
            decorationsContainer = decorationsObj.transform;
        }
        
        return decorationsContainer;
    }
    
    private Transform GetOrCreateAreaContainer(Transform parent, string areaName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == areaName)
                return child;
        }
        
        GameObject areaObj = new GameObject(areaName);
        areaObj.transform.SetParent(parent);
        return areaObj.transform;
    }
    
    private Transform GetOrCreateTypeContainer(Transform parent, string typeName)
    {
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
    
    #region Utility Methods
    
    private void CountExistingDecorationsOfType(DecorationSpawnData spawnData)
    {
        string decorationKey = GetDecorationKey(spawnData);
        
        if (!spawnedDecorations.ContainsKey(decorationKey))
        {
            spawnedDecorations[decorationKey] = new List<GameObject>();
        }
        
        // Mevcut dekorasyonları ara
        GameObject[] allDecorations = GameObject.FindGameObjectsWithTag("Decoration");
        
        foreach (GameObject decoration in allDecorations)
        {
            // Area kontrolü - decoration'ın bulunduğu pozisyon
            Vector2 decorationPos = decoration.transform.position;
            if (spawnData.spawnArea.IsPositionInArea(decorationPos))
            {
                // Prefab ismi kontrolü
                if (decoration.name.Contains(spawnData.decorationPrefab.name))
                {
                    if (!spawnedDecorations[decorationKey].Contains(decoration))
                    {
                        spawnedDecorations[decorationKey].Add(decoration);
                    }
                }
            }
        }
        
    }
    
    public void OnDecorationDestroyed(GameObject decoration, string areaName, string decorationType)
    {
        if (!Object.HasStateAuthority) return;
        
        string decorationKey = $"{decorationType}_{areaName}";
        
        if (spawnedDecorations.ContainsKey(decorationKey))
        {
            spawnedDecorations[decorationKey].Remove(decoration);
        }
    }
    
    public void ForceSpawnCheck()
    {
        if (!Object.HasStateAuthority) return;
        
        PerformSpawnCheck();
    }
    
    #endregion
    
    #region Debug & Gizmos
    
    private void OnDrawGizmosSelected()
    {
        if (decorationSpawnList == null) return;
        
        foreach (var spawnData in decorationSpawnList)
        {
            if (spawnData.spawnArea == null) continue;
            
            DrawAreaGizmo(spawnData);
        }
    }
    
    private void DrawAreaGizmo(DecorationSpawnData spawnData)
    {
        Gizmos.color = Color.blue;
        
        Vector3 center = new Vector3(spawnData.spawnArea.AreaCenter.x, spawnData.spawnArea.AreaCenter.y, 0);
        Vector3 size = new Vector3(spawnData.spawnArea.AreaSize.x, spawnData.spawnArea.AreaSize.y, 0);
        
        Gizmos.DrawWireCube(center, size);
        
        // Area ismini göster
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(center + Vector3.up * 2, 
            $"{spawnData.spawnArea.areaName}\n{spawnData.decorationName}\nMax: {spawnData.maxDecorationsInArea}");
        #endif
    }
    
    #endregion
}