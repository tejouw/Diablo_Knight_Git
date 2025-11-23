using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Fusion;
using System.Collections;

public class MinimapController : MonoBehaviour
{
    [Header("Minimap Settings")]
    [SerializeField] private RawImage minimapDisplay;
    [SerializeField] private RectTransform minimapMask;
    [SerializeField] private float viewRadius = 50f;
    [SerializeField] private Texture2D staticMinimapTexture; // Inspector'dan atanacak statik minimap
    
    [Header("Icon Settings")]
    [SerializeField] private GameObject iconPrefab;
    [SerializeField] private Transform iconContainer;
    [SerializeField] private Color playerColor = Color.blue;
    [SerializeField] private Color monsterColor = Color.red;
    [SerializeField] private Color npcColor = Color.green;
    [SerializeField] private float iconSize = 10f;
    [SerializeField] private int iconPoolSize = 100;
    
    [Header("Coordinate Display")]
    [SerializeField] private TextMeshProUGUI coordinateText;
    
    [Header("Performance Settings")]
    [SerializeField] private float iconUpdateInterval = 0.5f;
    [SerializeField] private float coordinateUpdateInterval = 1f;
    [SerializeField] private float objectRefreshInterval = 3f;
    [SerializeField] private float maxIconDistance = 40f; // viewRadius'tan küçük
    
    // Core Components
    private Transform playerTransform;
    private RectTransform minimapRect;
    private Vector2 minimapSize;
    private float minimapScale;
    
    // Object Tracking - Event Based
    private Dictionary<int, MinimapIcon> activeIcons = new Dictionary<int, MinimapIcon>();
    private Queue<MinimapIcon> iconPool = new Queue<MinimapIcon>();
    private Dictionary<int, GameObject> trackedObjects = new Dictionary<int, GameObject>(); // GameObject referansları
    private HashSet<int> visibleThisFrame = new HashSet<int>(); // Bu frame'de görülen objeler
    
    // Spatial Hash for Performance
    private Dictionary<Vector2Int, HashSet<GameObject>> spatialGrid = new Dictionary<Vector2Int, HashSet<GameObject>>();
    private readonly int gridSize = 25; // 25x25 world units per cell
    
    // Update Timers
    private float nextIconUpdate;
    private float nextCoordinateUpdate;
    private float nextObjectRefresh;
    
    // Performance Tracking
    private readonly Queue<MinimapObjectData> pendingUpdates = new Queue<MinimapObjectData>();
    private readonly int maxUpdatesPerFrame = 10;
    
    private struct MinimapObjectData
    {
        public int id;
        public Vector2 position;
        public MinimapObjectType type;
        public bool isActive;
    }
    
    private enum MinimapObjectType
    {
        Player,
        Monster,
        NPC,
        QuestGiver
    }
    
    private class MinimapIcon
    {
        public GameObject iconObject;
        public RectTransform rectTransform;
        public Image image;
        public int trackedObjectId;
        public bool isActive;
        public MinimapObjectType type;
        
        public void SetActive(bool active)
        {
            isActive = active;
            iconObject.SetActive(active);
        }
        
        public void UpdatePosition(Vector2 position)
        {
            rectTransform.anchoredPosition = position;
        }
        
        public void SetColor(Color color)
        {
            image.color = color;
        }
    }

    private void Start()
    {
        if (!ValidateComponents()) return;
        
        InitializeMinimapSystem();
        SetupEventListeners();
        
        StartCoroutine(OptimizedUpdateCoroutine());
    }
    
    private bool ValidateComponents()
    {
        if (minimapDisplay == null || iconContainer == null)
        {
            Debug.LogError("[MinimapController] Essential UI components missing!");
            enabled = false;
            return false;
        }
        
        return true;
    }
    
    private void InitializeMinimapSystem()
    {
        // Static minimap setup
        if (staticMinimapTexture != null)
        {
            minimapDisplay.texture = staticMinimapTexture;
        }
        else
        {
            // Fallback: Create simple colored texture
            CreateFallbackTexture();
        }
        
        // Minimap dimensions
        minimapRect = minimapDisplay.rectTransform;
        minimapSize = minimapRect.sizeDelta;
        minimapScale = minimapSize.x / (viewRadius * 2f);
        
        // Icon pool initialization
        CreateIconPool();
        
        // Setup mask
        if (minimapMask != null && minimapMask.GetComponent<Mask>())
        {
            minimapMask.GetComponent<Mask>().showMaskGraphic = false;
        }
    }
    
    private void CreateFallbackTexture()
    {
        Texture2D fallbackTexture = new Texture2D(256, 256, TextureFormat.RGB24, false);
        Color[] pixels = new Color[256 * 256];
        
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color(0.2f, 0.3f, 0.2f, 1f); // Dark green
        }
        
        fallbackTexture.SetPixels(pixels);
        fallbackTexture.Apply();
        minimapDisplay.texture = fallbackTexture;
    }
    
    private void CreateIconPool()
    {
        for (int i = 0; i < iconPoolSize; i++)
        {
            CreatePooledIcon();
        }
    }
    
    private void CreatePooledIcon()
    {
        GameObject iconObj = Instantiate(iconPrefab, iconContainer);
        iconObj.SetActive(false);
        
        MinimapIcon icon = new MinimapIcon
        {
            iconObject = iconObj,
            rectTransform = iconObj.GetComponent<RectTransform>(),
            image = iconObj.GetComponent<Image>(),
            isActive = false
        };
        
        // Set initial size
        icon.rectTransform.sizeDelta = Vector2.one * iconSize;
        
        iconPool.Enqueue(icon);
    }
    
    private void SetupEventListeners()
    {
        // Network events for object tracking
        if (NetworkManager.Instance != null && NetworkManager.Instance.Runner != null)
        {
            // Fusion callbacks will be handled in Update for now
            // In production, you'd want to use Fusion's proper callback system
        }
    }
    
    // Main optimized update coroutine
    private IEnumerator OptimizedUpdateCoroutine()
    {
        while (enabled)
        {
            // Find player if needed
            if (playerTransform == null)
            {
                FindLocalPlayer();
            }
            
            if (playerTransform != null)
            {
                // Process pending updates (batched)
                ProcessPendingUpdates();
                
                // Time-based updates
                if (Time.time >= nextIconUpdate)
                {
                    RefreshVisibleObjects();
                    UpdateIconPositions();
                    nextIconUpdate = Time.time + iconUpdateInterval;
                }
                
                if (Time.time >= nextCoordinateUpdate)
                {
                    UpdateCoordinates();
                    nextCoordinateUpdate = Time.time + coordinateUpdateInterval;
                }
                
                if (Time.time >= nextObjectRefresh)
                {
                    RefreshObjectCache();
                    nextObjectRefresh = Time.time + objectRefreshInterval;
                }
            }
            
            // 60 FPS friendly yield
            yield return new WaitForSeconds(0.016f);
        }
    }
    
    private void FindLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.HasInputAuthority)
            {
                playerTransform = player.transform;
                return;
            }
        }
    }
    
    private void ProcessPendingUpdates()
    {
        int processedThisFrame = 0;
        
        while (pendingUpdates.Count > 0 && processedThisFrame < maxUpdatesPerFrame)
        {
            MinimapObjectData data = pendingUpdates.Dequeue();
            ProcessObjectUpdate(data);
            processedThisFrame++;
        }
    }
    
    private void ProcessObjectUpdate(MinimapObjectData data)
    {
        if (data.isActive)
        {
            ShowObjectOnMinimap(data.id, data.position, data.type);
        }
        else
        {
            HideObjectFromMinimap(data.id);
        }
    }
    
    private void RefreshVisibleObjects()
    {
        if (playerTransform == null) return;
        
        Vector2 playerPos = playerTransform.position;
        visibleThisFrame.Clear();
        
        // Add player to visible objects
        int playerInstanceId = playerTransform.GetInstanceID();
        visibleThisFrame.Add(playerInstanceId);
        
        if (!activeIcons.ContainsKey(playerInstanceId))
        {
            QueueObjectUpdate(playerInstanceId, playerPos, MinimapObjectType.Player, true);
            trackedObjects[playerInstanceId] = playerTransform.gameObject;
        }
        
        // Get objects in spatial grid around player
        Vector2Int playerGridPos = WorldToGrid(playerPos);
        
        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                Vector2Int gridPos = new Vector2Int(playerGridPos.x + x, playerGridPos.y + y);
                if (spatialGrid.TryGetValue(gridPos, out HashSet<GameObject> objectsInGrid))
                {
                    foreach (GameObject obj in objectsInGrid)
                    {
                        if (obj == null || !obj.activeInHierarchy) continue;
                        
                        float distance = Vector2.Distance(playerPos, obj.transform.position);
                        if (distance <= maxIconDistance)
                        {
                            int objInstanceId = obj.GetInstanceID();
                            visibleThisFrame.Add(objInstanceId);
                            
                            // Sadece yeni objeler için icon oluştur
                            if (!activeIcons.ContainsKey(objInstanceId))
                            {
                                MinimapObjectType objType = GetObjectType(obj);
                                QueueObjectUpdate(objInstanceId, obj.transform.position, objType, true);
                                trackedObjects[objInstanceId] = obj;
                            }
                        }
                    }
                }
            }
        }
        
        // Artık görünmeyen objeleri kaldır
        var toRemove = new List<int>();
        foreach (var kvp in activeIcons)
        {
            if (!visibleThisFrame.Contains(kvp.Key))
            {
                toRemove.Add(kvp.Key);
            }
        }
        
        foreach (int id in toRemove)
        {
            QueueObjectUpdate(id, Vector2.zero, MinimapObjectType.Player, false);
            trackedObjects.Remove(id);
        }
    }
    
    private void QueueObjectUpdate(int objectId, Vector2 position, MinimapObjectType type, bool isActive)
    {
        pendingUpdates.Enqueue(new MinimapObjectData
        {
            id = objectId,
            position = position,
            type = type,
            isActive = isActive
        });
    }
    
    private void ShowObjectOnMinimap(int objectId, Vector2 worldPos, MinimapObjectType type)
    {
        if (activeIcons.ContainsKey(objectId)) return;
        
        MinimapIcon icon = GetPooledIcon();
        if (icon == null) return;
        
        // Setup icon
        icon.trackedObjectId = objectId;
        icon.type = type;
        icon.SetColor(GetColorForType(type));
        
        // Calculate minimap position
        Vector2 minimapPos = WorldToMinimapPosition(worldPos);
        icon.UpdatePosition(minimapPos);
        
        // Special sizing for player
        if (type == MinimapObjectType.Player)
        {
            icon.rectTransform.sizeDelta = Vector2.one * iconSize * 1.3f;
        }
        else
        {
            icon.rectTransform.sizeDelta = Vector2.one * iconSize;
        }
        
        icon.SetActive(true);
        activeIcons[objectId] = icon;
    }
    
    private void HideObjectFromMinimap(int objectId)
    {
        if (activeIcons.TryGetValue(objectId, out MinimapIcon icon))
        {
            icon.SetActive(false);
            ReturnIconToPool(icon);
            activeIcons.Remove(objectId);
        }
    }
    
    private void UpdateIconPositions()
    {
        if (playerTransform == null) return;
        
        Vector2 playerPos = playerTransform.position;
        
        // Only update positions of active icons
        foreach (var kvp in activeIcons)
        {
            MinimapIcon icon = kvp.Value;
            if (!icon.isActive) continue;
            
            // For player, keep centered
            if (icon.type == MinimapObjectType.Player)
            {
                icon.UpdatePosition(Vector2.zero);
                continue;
            }
            
            // For other objects, use cached reference
            if (trackedObjects.TryGetValue(kvp.Key, out GameObject obj) && 
                obj != null && obj.activeInHierarchy)
            {
                Vector2 minimapPos = WorldToMinimapPosition(obj.transform.position);
                icon.UpdatePosition(minimapPos);
            }
            else
            {
                // Object no longer exists, will be cleaned up in next refresh
                continue;
            }
        }
    }
    
    private Vector2 WorldToMinimapPosition(Vector2 worldPos)
    {
        if (playerTransform == null) return Vector2.zero;
        
        Vector2 relativePos = worldPos - (Vector2)playerTransform.position;
        Vector2 minimapPos = relativePos * minimapScale;
        
        // Clamp to minimap bounds
        float maxDistance = minimapSize.x / 2f - iconSize;
        if (minimapPos.magnitude > maxDistance)
        {
            minimapPos = minimapPos.normalized * maxDistance;
        }
        
        return minimapPos;
    }
    
    private void RefreshObjectCache()
    {
        if (playerTransform == null) return;
        
        // Clear spatial grid
        spatialGrid.Clear();
        
        // Re-populate spatial grid
        AddObjectsToSpatialGrid(GameObject.FindGameObjectsWithTag("Monster"));
        AddObjectsToSpatialGrid(GameObject.FindGameObjectsWithTag("NPC"));
        
        // Add quest givers
        DialogQuestGiver[] questGivers = FindObjectsByType<DialogQuestGiver>(FindObjectsSortMode.None);
        foreach (DialogQuestGiver qg in questGivers)
        {
            if (qg != null) AddObjectToSpatialGrid(qg.gameObject);
        }
        
        BaseNPC[] npcs = FindObjectsByType<BaseNPC>(FindObjectsSortMode.None);
        foreach (BaseNPC npc in npcs)
        {
            if (npc != null) AddObjectToSpatialGrid(npc.gameObject);
        }
        
        // Clean up dead references in trackedObjects
        var deadRefs = new List<int>();
        foreach (var kvp in trackedObjects)
        {
            if (kvp.Value == null)
            {
                deadRefs.Add(kvp.Key);
            }
        }
        
        foreach (int deadId in deadRefs)
        {
            trackedObjects.Remove(deadId);
            if (activeIcons.TryGetValue(deadId, out MinimapIcon deadIcon))
            {
                deadIcon.SetActive(false);
                ReturnIconToPool(deadIcon);
                activeIcons.Remove(deadId);
            }
        }
    }
    
    private void AddObjectsToSpatialGrid(GameObject[] objects)
    {
        foreach (GameObject obj in objects)
        {
            if (obj != null && obj.activeInHierarchy)
            {
                AddObjectToSpatialGrid(obj);
            }
        }
    }
    
    private void AddObjectToSpatialGrid(GameObject obj)
    {
        Vector2Int gridPos = WorldToGrid(obj.transform.position);
        
        if (!spatialGrid.TryGetValue(gridPos, out HashSet<GameObject> objectsInGrid))
        {
            objectsInGrid = new HashSet<GameObject>();
            spatialGrid[gridPos] = objectsInGrid;
        }
        
        objectsInGrid.Add(obj);
    }
    
    private Vector2Int WorldToGrid(Vector2 worldPos)
    {
        return new Vector2Int(
            Mathf.FloorToInt(worldPos.x / gridSize),
            Mathf.FloorToInt(worldPos.y / gridSize)
        );
    }
    
    private MinimapObjectType GetObjectType(GameObject obj)
    {
        if (obj.CompareTag("Monster")) return MinimapObjectType.Monster;
        if (obj.GetComponent<DialogQuestGiver>()) return MinimapObjectType.QuestGiver;
        if (obj.GetComponent<BaseNPC>()) return MinimapObjectType.NPC;
        return MinimapObjectType.NPC; // Default
    }
    
    private Color GetColorForType(MinimapObjectType type)
    {
        switch (type)
        {
            case MinimapObjectType.Player: return playerColor;
            case MinimapObjectType.Monster: return monsterColor;
            case MinimapObjectType.NPC: return npcColor;
            case MinimapObjectType.QuestGiver: return Color.yellow;
            default: return Color.white;
        }
    }
    
    private MinimapIcon GetPooledIcon()
    {
        if (iconPool.Count > 0)
        {
            return iconPool.Dequeue();
        }
        
        // Pool exhausted, create new one
        CreatePooledIcon();
        return iconPool.Count > 0 ? iconPool.Dequeue() : null;
    }
    
    private void ReturnIconToPool(MinimapIcon icon)
    {
        icon.SetActive(false);
        icon.trackedObjectId = 0;
        iconPool.Enqueue(icon);
    }
    
    private void UpdateCoordinates()
    {
        if (coordinateText != null && playerTransform != null)
        {
            Vector2 pos = playerTransform.position;
            coordinateText.text = $"X: {pos.x:F0}, Y: {pos.y:F0}";
        }
    }
    
    private void OnDestroy()
    {
        StopAllCoroutines();
        
        // Clean up tracked objects
        trackedObjects.Clear();
        visibleThisFrame.Clear();
        
        // Clean up any remaining icons
        foreach (var icon in activeIcons.Values)
        {
            if (icon.iconObject != null)
                DestroyImmediate(icon.iconObject);
        }
        activeIcons.Clear();
        
        // Clean up pool
        while (iconPool.Count > 0)
        {
            var icon = iconPool.Dequeue();
            if (icon.iconObject != null)
                DestroyImmediate(icon.iconObject);
        }
        
        // Clean up spatial grid
        spatialGrid.Clear();
    }
}