using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Fusion;
using System.Linq;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class AreaSystem : MonoBehaviour // NetworkBehaviour değil
{
    public static AreaSystem Instance;
    [Header("Sub-Area Visualization")]
[SerializeField] private bool showSubAreas = true;
[SerializeField] private bool showSubAreaNumbers = true;
    [Header("Area Settings")]
    [SerializeField] private List<AreaData> allAreas = new List<AreaData>();
    [SerializeField] private float checkInterval = 0.5f;
    
[Header("Editor Visualization")]
[SerializeField] private bool showAreasInEditor = true;
[SerializeField] private bool showAreaNames = true;
[SerializeField] private bool showAreaInfo = true;  // YENİ
[SerializeField] private bool showMonsterInfo = true;  // YENİ
[SerializeField] private bool showAreaFill = true;
[SerializeField] private bool showPlayerHighlight = true;
    private Transform playerTransform;
    private AreaData currentArea;
    private AreaData previousArea;
    private HashSet<string> visitedAreas = new HashSet<string>();
    [Header("Monster Tracking")]
[SerializeField] private MonsterSpawner monsterSpawner;
    public AreaData CurrentArea => currentArea;
    public event System.Action<AreaData> OnAreaChanged;
    public event System.Action<AreaData> OnNewAreaDiscovered;
    
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
        // Area check coroutine başlat
        StartCoroutine(AreaCheckCoroutine());
    }

    private IEnumerator AreaCheckCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            
            if (playerTransform == null)
            {
                FindLocalPlayer();
                continue;
            }
            
            CheckCurrentArea();
        }
    }
    
    private void FindLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject player in players)
        {
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                if (networkObject.HasInputAuthority)
                {
                    playerTransform = player.transform;
                    break;
                }
            }
        }
        
        if (playerTransform == null)
        {
        }
    }
    
    private void CheckCurrentArea()
    {
        Vector2 playerPosition = playerTransform.position;
        AreaData newArea = null;
        
        // Hangi alanda olduğunu kontrol et
        foreach (AreaData area in allAreas)
        {
            if (area.IsPositionInArea(playerPosition))
            {
                newArea = area;
                break;
            }
        }
        
        if (newArea == null)
        {
        }
        
        // Alan değişti mi kontrol et
        if (newArea != currentArea)
        {
            previousArea = currentArea;
            currentArea = newArea;
            
            if (currentArea != null)
            {
                // Yeni alan keşfedildi mi?
                bool isNewDiscovery = !visitedAreas.Contains(currentArea.areaName);
                
                if (isNewDiscovery)
                {
                    visitedAreas.Add(currentArea.areaName);
                    OnNewAreaDiscovered?.Invoke(currentArea);
                    
                    // UI'ya bildir - yeni keşif
                    if (AreaNotificationUI.Instance != null)
                    {
                        AreaNotificationUI.Instance.ShowAreaDiscoveryNotification(currentArea);
                    }
                    else
                    {
                        Debug.LogError("[AreaSystem] AreaNotificationUI.Instance is null!");
                    }
                }
                else
                {
                    // Daha önce ziyaret edilmiş alan
                    if (AreaNotificationUI.Instance != null)
                    {
                        AreaNotificationUI.Instance.ShowAreaEnteredNotification(currentArea);
                    }
                    else
                    {
                        Debug.LogError("[AreaSystem] AreaNotificationUI.Instance is null!");
                    }
                }
            }
            
            // Alan değişikliğini bildir
            OnAreaChanged?.Invoke(currentArea);
            
            // UIManager'a güncel alanı bildir
            if (AreaNotificationUI.Instance != null)
            {
                AreaNotificationUI.Instance.UpdateCurrentArea(currentArea);
            }
            else
            {
                Debug.LogError("[AreaSystem] AreaNotificationUI.Instance is null for UpdateCurrentArea!");
            }
        }
    }
    
    public bool HasVisitedArea(string areaName)
    {
        return visitedAreas.Contains(areaName);
    }
    
    public List<string> GetVisitedAreas()
    {
        return new List<string>(visitedAreas);
    }

#if UNITY_EDITOR
private void OnDrawGizmos()
{
    if (!showAreasInEditor) return;
    
    if (allAreas == null || allAreas.Count == 0) return;
    
    Color[] areaColors = {
        Color.red, Color.green, Color.blue, Color.yellow, Color.magenta, Color.cyan,
        new Color(1f, 0.5f, 0f), new Color(0.5f, 0f, 1f), new Color(0f, 1f, 0.5f), new Color(1f, 0f, 0.5f)
    };
    
    for (int i = 0; i < allAreas.Count; i++)
    {
        AreaData area = allAreas[i];
        if (area == null) continue;
        
        Color areaColor = areaColors[i % areaColors.Length];
        
        DrawAreaOutline(area, areaColor);
        
        if (showAreaFill)
        {
            DrawAreaFill(area, new Color(areaColor.r, areaColor.g, areaColor.b, 0.1f));
        }
        
        if (showAreaNames)
        {
            DrawAreaLabel(area, areaColor);
        }
        
        // Sub-area'ları çiz
        if (showSubAreas)
        {
            DrawSubAreas(area, areaColor);
        }
    }
    
    if (showPlayerHighlight && Application.isPlaying && currentArea != null && playerTransform != null)
    {
        DrawCurrentAreaHighlight();
    }
}

private void DrawSubAreas(AreaData area, Color baseColor)
{
    Color subAreaColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
    Gizmos.color = subAreaColor;
    
    for (int subAreaNumber = 1; subAreaNumber <= 9; subAreaNumber++)
    {
        Vector2 bottomLeft = area.GetSubAreaBottomLeft(subAreaNumber);
        Vector2 topRight = area.GetSubAreaTopRight(subAreaNumber);
        
        Vector3 bl = new Vector3(bottomLeft.x, bottomLeft.y, 0);
        Vector3 tl = new Vector3(bottomLeft.x, topRight.y, 0);
        Vector3 tr = new Vector3(topRight.x, topRight.y, 0);
        Vector3 br = new Vector3(topRight.x, bottomLeft.y, 0);
        
        // Sub-area outline çiz
        Gizmos.DrawLine(bl, tl);
        Gizmos.DrawLine(tl, tr);
        Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl);
        
        // Sub-area numarasını göster
        if (showSubAreaNumbers)
        {
            Vector3 center = new Vector3(area.GetSubAreaCenter(subAreaNumber).x, area.GetSubAreaCenter(subAreaNumber).y, 0);
            
#if UNITY_EDITOR
            UnityEditor.Handles.Label(center, subAreaNumber.ToString(), new GUIStyle()
            {
                normal = { textColor = baseColor },
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            });
#endif
        }
    }
}
    
    private void DrawAreaOutline(AreaData area, Color color)
    {
        Gizmos.color = color;
        
        Vector3 bottomLeft = new Vector3(area.bottomLeftCorner.x, area.bottomLeftCorner.y, 0);
        Vector3 topLeft = new Vector3(area.TopLeftCorner.x, area.TopLeftCorner.y, 0);
        Vector3 topRight = new Vector3(area.topRightCorner.x, area.topRightCorner.y, 0);
        Vector3 bottomRight = new Vector3(area.BottomRightCorner.x, area.BottomRightCorner.y, 0);
        
        // Dikdörtgen çiz
        Gizmos.DrawLine(bottomLeft, topLeft);     // Sol kenar
        Gizmos.DrawLine(topLeft, topRight);       // Üst kenar  
        Gizmos.DrawLine(topRight, bottomRight);   // Sağ kenar
        Gizmos.DrawLine(bottomRight, bottomLeft); // Alt kenar
        
        // Köşe noktaları
        Gizmos.DrawWireSphere(bottomLeft, 0.5f);
        Gizmos.DrawWireSphere(topRight, 0.5f);
    }
    
    private void DrawAreaFill(AreaData area, Color color)
    {
        Gizmos.color = color;
        
        Vector3 center = new Vector3(area.AreaCenter.x, area.AreaCenter.y, 0);
        Vector3 size = new Vector3(area.AreaSize.x, area.AreaSize.y, 0.1f);
        
        Gizmos.DrawCube(center, size);
    }
    
    private void DrawAreaLabel(AreaData area, Color color)
    {
        Vector3 labelPosition = new Vector3(area.AreaCenter.x, area.AreaCenter.y + area.AreaSize.y * 0.3f, 0);
        
        GUIStyle style = new GUIStyle();
        style.normal.textColor = color;
        style.fontSize = 14;
        style.fontStyle = FontStyle.Bold;
        style.alignment = TextAnchor.MiddleCenter;
        
        Handles.Label(labelPosition, area.areaName, style);
    }
    
    private void DrawCurrentAreaHighlight()
    {
        // Mevcut alanı vurgula
        Gizmos.color = Color.white;
        
        Vector3 center = new Vector3(currentArea.AreaCenter.x, currentArea.AreaCenter.y, 0);
        Vector3 size = new Vector3(currentArea.AreaSize.x + 2f, currentArea.AreaSize.y + 2f, 0.1f);
        
        Gizmos.DrawWireCube(center, size);
        
        // Oyuncu pozisyonunu göster
        if (playerTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(playerTransform.position, 1f);
        }
    }

private void OnDrawGizmosSelected()
{
    // Bu da toggle'a bağlı olsun
    if (!showAreasInEditor) return;
    
    if (allAreas == null) return;
    
    foreach (AreaData area in allAreas)
    {
        if (area == null) continue;
        
        // Area ortası pozisyon
        Vector3 centerPosition = new Vector3(area.AreaCenter.x, area.AreaCenter.y, 0);
        
        // Sadece gerekli bilgileri al
        string monsterConfigInfo = "";
        float sizeValue = 0f;
        
        if (showMonsterInfo)
        {
            monsterConfigInfo = GetMonsterConfigForArea(area);
        }
        
        if (showAreaInfo)
        {
            sizeValue = (area.AreaSize.x * area.AreaSize.y) / 100f;
        }
        
        // Bilgi gösterilecekse label çiz
        if (showAreaInfo || showMonsterInfo)
        {
            string coloredInfo = GetColoredAreaInfo(area, sizeValue, monsterConfigInfo);
            
            GUIStyle infoStyle = new GUIStyle();
            infoStyle.normal.textColor = Color.white;
            infoStyle.fontSize = 11;
            infoStyle.fontStyle = FontStyle.Bold;
            infoStyle.alignment = TextAnchor.MiddleCenter;
            infoStyle.richText = true; // Rich text desteği
            
            Handles.Label(centerPosition, coloredInfo, infoStyle);
        }
    }
}

private string GetColoredAreaInfo(AreaData area, float sizeValue, string monsterConfigInfo)
{
    System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // Area bilgileri - showAreaInfo toggle'ına bağlı
        if (showAreaInfo)
        {
            // Area adı - Beyaz ve büyük
            sb.AppendLine($"<color=#FFFFFF><size=12><b>{area.areaName}</b></size></color>");

            // Genişlik ve Yükseklik - Açık gri
            sb.AppendLine($"<color=#CCCCCC><size=9>Width: {area.AreaSize.x:F1}</size></color>");
            sb.AppendLine($"<color=#CCCCCC><size=9>Height: {area.AreaSize.y:F1}</size></color>");

            // Size - Sarı
            sb.AppendLine($"<color=#FFFF00><size=10><b>Size: {sizeValue:F1}</b></size></color>");
        
float perArea = sizeValue / 9f;
int roundedPerArea = (int)Math.Round(perArea, MidpointRounding.AwayFromZero);

sb.AppendLine($"<color=#FFA500><size=9>Her bir alan: {roundedPerArea}</size></color>");
    }
    
    // Monster bilgileri - showMonsterInfo toggle'ına bağlı
    if (showMonsterInfo)
    {
        sb.Append(monsterConfigInfo);
    }
    
    return sb.ToString();
}

private string GetMonsterConfigForArea(AreaData area)
{
    // MonsterSpawner'ı bul
    MonsterSpawner spawner = FindFirstObjectByType<MonsterSpawner>();
    
    if (spawner == null)
    {
        return "<color=#FF6666><size=9>No MonsterSpawner found</size></color>";
    }
    
    // Bu area için konfigüre edilmiş monster'ları bul
    List<MonsterConfigInfo> monsterConfigs = GetConfiguredMonstersForArea(spawner, area);
    
    if (monsterConfigs.Count == 0)
    {
        return "<color=#FF9966><size=9>No monsters configured</size></color>";
    }
    
    int totalConfiguredMonsters = monsterConfigs.Sum(config => config.maxCount);
    
    System.Text.StringBuilder sb = new System.Text.StringBuilder();
    
    // Monster başlığı - Yeşil ve kalın
    sb.AppendLine($"<color=#66FF66><size=11><b>Configured Monsters: {totalConfiguredMonsters}</b></size></color>");
    
    // Her monster türü - Farklı renkler - SYNTAX DÜZELTİLDİ
    string[] monsterColors = { "#66DDFF", "#FF66DD", "#DDFF66", "#FF9966", "#9966FF", "#66FFDD" };
    int colorIndex = 0;
    
    foreach (var config in monsterConfigs.OrderByDescending(x => x.maxCount))
    {
        string color = monsterColors[colorIndex % monsterColors.Length];
        sb.AppendLine($"<color={color}><size=10>  • {config.monsterName}: <b>{config.maxCount}</b></size></color>");
        colorIndex++;
    }
    
    return sb.ToString().TrimEnd();
}

private List<MonsterConfigInfo> GetConfiguredMonstersForArea(MonsterSpawner spawner, AreaData area)
{
    List<MonsterConfigInfo> configs = new List<MonsterConfigInfo>();
    
    // Reflection ile private field'a erişim
    var monsterSpawnListField = typeof(MonsterSpawner).GetField("monsterSpawnList", 
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
    
    if (monsterSpawnListField == null) return configs;
    
    var monsterSpawnList = monsterSpawnListField.GetValue(spawner) as List<MonsterSpawnData>;
    
    if (monsterSpawnList == null) return configs;
    
    foreach (var spawnData in monsterSpawnList)
    {
        if (spawnData.spawnAreaType == SpawnAreaType.AreaBased)
        {
            foreach (var areaLimit in spawnData.spawnAreasWithLimits)
            {
                if (areaLimit?.IsValid == true && areaLimit.area == area)
                {
                    configs.Add(new MonsterConfigInfo
                    {
                        monsterName = spawnData.categoryName,
                        maxCount = areaLimit.maxMonstersInThisArea
                    });
                    break;
                }
            }
        }
    }
    
    return configs;
}

[System.Serializable]
private class MonsterConfigInfo
{
    public string monsterName;
    public int maxCount;
}


#endif
}