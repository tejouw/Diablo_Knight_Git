using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[System.Serializable]
public class EditorDecorationData
{
    [Header("Decoration Settings")]
    public string decorationName = "Decoration";
    public GameObject decorationPrefab;
    public int maxDecorationsInArea = 10;
    public float minDistanceBetweenDecorations = 2f;
    public bool enabledForSpawn = true;
}

[System.Serializable]
public class EditorAreaSpawnData
{
    [Header("Area Settings")]
    public AreaData spawnArea;
    public bool enabledForSpawn = true;
    public bool showAreaGizmo = true;
    
    [Header("Decorations in this Area")]
    public List<EditorDecorationData> decorations = new List<EditorDecorationData>();
}

public class EditorDecorationSpawner : MonoBehaviour
{
    #region Configuration
    
    [Header("Area Spawn Configuration")]
    [SerializeField] private List<EditorAreaSpawnData> areaSpawnList = new List<EditorAreaSpawnData>();
    
    [Header("Container Settings")]
    [SerializeField] public Transform decorationsContainer;
    [SerializeField] private bool createContainerIfMissing = true;
    [SerializeField] private string containerName = "Editor_Decorations";
    
    [Header("Spawn Settings")]
    [SerializeField] private float obstacleCheckRadius = 0.5f;
    [SerializeField] private int maxSpawnAttempts = 50;
    [SerializeField] private bool organizeByAreaAndType = true;
    
    // Runtime data for editor
    [System.NonSerialized]
    private Dictionary<string, List<GameObject>> spawnedDecorations = new Dictionary<string, List<GameObject>>();
    
    // Public property for editor access
    public List<EditorAreaSpawnData> AreaSpawnList => areaSpawnList;
    
    #endregion
    
    #region Public Methods (Editor'dan çağrılacak)
    
    public void SpawnAllDecorations()
    {
#if UNITY_EDITOR
        if (!ValidateSettings()) return;
        
        InitializeContainer();
        InitializeSpawnTracking();
        
        foreach (var areaData in areaSpawnList)
        {
            if (!areaData.enabledForSpawn) continue;
            SpawnDecorationsForArea(areaData);
        }
        
        Debug.Log($"[EditorDecorationSpawner] All decorations spawned successfully!");
#endif
    }
    
    public void SpawnSpecificArea(int areaIndex)
    {
#if UNITY_EDITOR
        if (areaIndex < 0 || areaIndex >= areaSpawnList.Count) return;
        if (!ValidateSettings()) return;
        
        var areaData = areaSpawnList[areaIndex];
        if (!areaData.enabledForSpawn) return;
        
        InitializeContainer();
        InitializeSpawnTracking();
        
        SpawnDecorationsForArea(areaData);
        
        string areaName = areaData.spawnArea != null ? areaData.spawnArea.areaName : $"Area {areaIndex}";
        Debug.Log($"[EditorDecorationSpawner] Area {areaName} spawned!");
#endif
    }
    
public void ClearAllDecorations()
{
#if UNITY_EDITOR
    if (decorationsContainer == null) return;
    
    Undo.RegisterCompleteObjectUndo(decorationsContainer, "Clear All Decorations");
    
    // Container'ın tüm child'larını sil - GameObject listesi kullan
    List<GameObject> childrenToDelete = new List<GameObject>();
    for (int i = 0; i < decorationsContainer.childCount; i++)
    {
        childrenToDelete.Add(decorationsContainer.GetChild(i).gameObject);
    }
    
    foreach (GameObject child in childrenToDelete)
    {
        if (child != null)
        {
            Undo.DestroyObjectImmediate(child);
        }
    }
    
    spawnedDecorations.Clear();
    
    Debug.Log($"[EditorDecorationSpawner] All decorations cleared!");
#endif
}
    
public void ClearSpecificArea(int areaIndex)
{
#if UNITY_EDITOR
    if (areaIndex < 0 || areaIndex >= areaSpawnList.Count) return;
    if (decorationsContainer == null) return;
    
    var areaData = areaSpawnList[areaIndex];
    string areaName = areaData.spawnArea != null ? areaData.spawnArea.areaName : $"Area {areaIndex}";
    
    Undo.RegisterCompleteObjectUndo(decorationsContainer, $"Clear Area {areaName}");
    
    // Bu area'nın tüm decoration'larını bul ve sil - GameObject listesi kullan
    List<GameObject> toDelete = new List<GameObject>();
    
    foreach (Transform child in decorationsContainer.GetComponentsInChildren<Transform>())
    {
        if (child == decorationsContainer) continue;
        
        if (child.name.Contains(areaName))
        {
            toDelete.Add(child.gameObject);
        }
    }
    
    foreach (GameObject target in toDelete)
    {
        if (target != null)
        {
            Undo.DestroyObjectImmediate(target);
        }
    }
    
    // Tracking'den de temizle
    if (areaData.decorations != null)
    {
        foreach (var decoration in areaData.decorations)
        {
            string decorationKey = GetDecorationKey(areaData.spawnArea, decoration);
            if (spawnedDecorations.ContainsKey(decorationKey))
            {
                spawnedDecorations[decorationKey].Clear();
            }
        }
    }
    
    Debug.Log($"[EditorDecorationSpawner] Area {areaName} cleared!");
#endif
}
    
    #endregion
    
    #region Private Methods
    
    private bool ValidateSettings()
    {
        if (areaSpawnList == null || areaSpawnList.Count == 0)
        {
            Debug.LogWarning("[EditorDecorationSpawner] No area spawn data configured!");
            return false;
        }
        
        return true;
    }
    
private void InitializeContainer()
{
    if (decorationsContainer == null && createContainerIfMissing)
    {
        GameObject containerObj = GameObject.Find(containerName);
        
        if (containerObj == null)
        {
            containerObj = new GameObject(containerName);
#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(containerObj, "Create Decorations Container");
#endif
        }
        
        decorationsContainer = containerObj.transform;
    }
}
    
    private void InitializeSpawnTracking()
    {
        if (spawnedDecorations == null)
            spawnedDecorations = new Dictionary<string, List<GameObject>>();
        else
            spawnedDecorations.Clear();
        
        foreach (var areaData in areaSpawnList)
        {
            if (areaData.spawnArea == null) continue;
            
            foreach (var decorationData in areaData.decorations)
            {
                if (decorationData.decorationPrefab == null) continue;
                
                string decorationKey = GetDecorationKey(areaData.spawnArea, decorationData);
                spawnedDecorations[decorationKey] = new List<GameObject>();
                
                // Mevcut dekorasyonları say
                CountExistingDecorationsOfType(areaData.spawnArea, decorationData);
            }
        }
    }
    
    private string GetDecorationKey(AreaData area, EditorDecorationData decoration)
    {
        return $"{decoration.decorationName}_{area.areaName}";
    }
    
    private void SpawnDecorationsForArea(EditorAreaSpawnData areaData)
    {
        if (areaData.spawnArea == null)
        {
            Debug.LogWarning("[EditorDecorationSpawner] Area data has no spawn area assigned!");
            return;
        }
        
        Transform areaContainer = GetOrCreateAreaContainer(areaData.spawnArea);
        
        foreach (var decorationData in areaData.decorations)
        {
            if (!decorationData.enabledForSpawn) continue;
            if (decorationData.decorationPrefab == null) continue;
            
            string decorationKey = GetDecorationKey(areaData.spawnArea, decorationData);
            int currentCount = spawnedDecorations[decorationKey].Count;
            int needToSpawn = decorationData.maxDecorationsInArea - currentCount;
            
            if (needToSpawn <= 0)
            {
                Debug.Log($"[EditorDecorationSpawner] {decorationData.decorationName} already at max capacity in {areaData.spawnArea.areaName}");
                continue;
            }
            
            Transform typeContainer = GetOrCreateTypeContainer(areaContainer, decorationData.decorationName);
            
            for (int i = 0; i < needToSpawn; i++)
            {
                Vector2 spawnPosition = FindValidSpawnPosition(areaData.spawnArea, decorationData);
                if (spawnPosition == Vector2.zero) 
                {
                    Debug.LogWarning($"[EditorDecorationSpawner] Could not find valid position for {decorationData.decorationName} in {areaData.spawnArea.areaName}");
                    continue;
                }
                
                SpawnDecoration(areaData.spawnArea, decorationData, spawnPosition, typeContainer);
            }
        }
    }
    
private Transform GetOrCreateAreaContainer(AreaData area)
{
    if (!organizeByAreaAndType) return decorationsContainer;
    
    Transform areaContainer = FindChildByName(decorationsContainer, area.areaName);
    if (areaContainer == null)
    {
        GameObject areaObj = new GameObject(area.areaName);
        areaObj.transform.SetParent(decorationsContainer);
#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(areaObj, $"Create Area Container {area.areaName}");
#endif
        areaContainer = areaObj.transform;
    }
    
    return areaContainer;
}

private Transform GetOrCreateTypeContainer(Transform areaContainer, string typeName)
{
    if (!organizeByAreaAndType) return areaContainer;
    
    Transform typeContainer = FindChildByName(areaContainer, typeName);
    if (typeContainer == null)
    {
        GameObject typeObj = new GameObject(typeName);
        typeObj.transform.SetParent(areaContainer);
#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(typeObj, $"Create Type Container {typeName}");
#endif
        typeContainer = typeObj.transform;
    }
    
    return typeContainer;
}
    
    private Transform FindChildByName(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
        }
        return null;
    }
    
    private Vector2 FindValidSpawnPosition(AreaData area, EditorDecorationData decorationData)
    {
        for (int attempt = 0; attempt < maxSpawnAttempts; attempt++)
        {
            Vector2 candidatePosition = GetRandomPositionInArea(area);
            
            if (IsPositionValidForSpawn(candidatePosition, area, decorationData))
            {
                return candidatePosition;
            }
        }
        
        return Vector2.zero;
    }
    
    private Vector2 GetRandomPositionInArea(AreaData area)
    {
        float randomX = Random.Range(area.bottomLeftCorner.x, area.topRightCorner.x);
        float randomY = Random.Range(area.bottomLeftCorner.y, area.topRightCorner.y);
        
        return new Vector2(randomX, randomY);
    }
    
    private bool IsPositionValidForSpawn(Vector2 position, AreaData area, EditorDecorationData decorationData)
    {
        // Area içinde mi kontrol et
        if (!area.IsPositionInArea(position)) return false;
        
        // Engel kontrolü
        if (IsPositionBlocked(position)) return false;
        
        // Diğer decorasyonlardan mesafe kontrolü
        if (IsPositionTooCloseToOtherDecorations(position, area, decorationData)) return false;
        
        return true;
    }
    
    private bool IsPositionBlocked(Vector2 position)
    {
        Collider2D obstacle = Physics2D.OverlapCircle(position, obstacleCheckRadius, 
            LayerMask.GetMask("Obstacles", "Wall", "Default"));
        return obstacle != null;
    }
    
    private bool IsPositionTooCloseToOtherDecorations(Vector2 position, AreaData area, EditorDecorationData decorationData)
    {
        string decorationKey = GetDecorationKey(area, decorationData);
        if (!spawnedDecorations.ContainsKey(decorationKey)) return false;
        
        // Aynı tipten decorasyonlarla mesafe kontrolü
        foreach (GameObject decoration in spawnedDecorations[decorationKey])
        {
            if (decoration == null) continue;
            
            float distance = Vector2.Distance(decoration.transform.position, position);
            if (distance < decorationData.minDistanceBetweenDecorations)
            {
                return true;
            }
        }
        
        // Diğer tiplerle genel minimum mesafe kontrolü
        foreach (var kvp in spawnedDecorations)
        {
            if (kvp.Key == decorationKey) continue;
            
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
    
private void SpawnDecoration(AreaData area, EditorDecorationData decorationData, Vector2 spawnPosition, Transform parent)
{
    GameObject decorationInstance = Instantiate(decorationData.decorationPrefab, spawnPosition, Quaternion.identity, parent);
    
    // İsim ayarla
    decorationInstance.name = $"{decorationData.decorationName}_{area.areaName}_{spawnedDecorations[GetDecorationKey(area, decorationData)].Count}";
    
    // Tag ayarla
    decorationInstance.tag = "Decoration";
    
#if UNITY_EDITOR
    // Undo kaydı
    Undo.RegisterCreatedObjectUndo(decorationInstance, $"Spawn {decorationData.decorationName}");
#endif
    
    // Tracking'e ekle
    string decorationKey = GetDecorationKey(area, decorationData);
    spawnedDecorations[decorationKey].Add(decorationInstance);
}
    
    private void CountExistingDecorationsOfType(AreaData area, EditorDecorationData decorationData)
    {
        string decorationKey = GetDecorationKey(area, decorationData);
        
        if (!spawnedDecorations.ContainsKey(decorationKey))
        {
            spawnedDecorations[decorationKey] = new List<GameObject>();
        }
        
        if (decorationsContainer == null) return;
        
        // Container'daki mevcut decorasyonları say
        foreach (Transform child in decorationsContainer.GetComponentsInChildren<Transform>())
        {
            if (child == decorationsContainer) continue;
            
            Vector2 decorationPos = child.transform.position;
            if (area.IsPositionInArea(decorationPos))
            {
                if (child.name.Contains(decorationData.decorationName) && 
                    child.name.Contains(area.areaName))
                {
                    if (!spawnedDecorations[decorationKey].Contains(child.gameObject))
                    {
                        spawnedDecorations[decorationKey].Add(child.gameObject);
                    }
                }
            }
        }
    }
    
    #endregion
    
    #region Gizmos
    
    private void OnDrawGizmosSelected()
    {
        if (areaSpawnList == null) return;
        
        foreach (var areaData in areaSpawnList)
        {
            if (areaData.spawnArea == null || !areaData.showAreaGizmo) continue;
            
            DrawAreaGizmo(areaData);
        }
    }
    
    private void DrawAreaGizmo(EditorAreaSpawnData areaData)
    {
        Gizmos.color = areaData.enabledForSpawn ? Color.green : Color.gray;
        
        Vector3 center = new Vector3(areaData.spawnArea.AreaCenter.x, areaData.spawnArea.AreaCenter.y, 0);
        Vector3 size = new Vector3(areaData.spawnArea.AreaSize.x, areaData.spawnArea.AreaSize.y, 0);
        
        Gizmos.DrawWireCube(center, size);
        
#if UNITY_EDITOR
        int totalDecorations = 0;
        int enabledDecorations = 0;
        foreach (var decoration in areaData.decorations)
        {
            if (decoration.enabledForSpawn)
            {
                enabledDecorations++;
                totalDecorations += decoration.maxDecorationsInArea;
            }
        }
        
        UnityEditor.Handles.Label(center + Vector3.up * 2, 
            $"{areaData.spawnArea.areaName}\nTypes: {enabledDecorations}/{areaData.decorations.Count}\nMax Total: {totalDecorations}");
#endif
    }
    
    #endregion
}