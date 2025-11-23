using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using System.Linq;
using System;

public class PartyAvatarRenderer : MonoBehaviour
{
    public static PartyAvatarRenderer Instance { get; private set; }

    [Header("Avatar Settings")]
    [SerializeField] private Vector2 avatarSize = new Vector2(64, 64);
    
    private GameObject avatarContainer;
    private Camera avatarCamera;
    private Dictionary<PlayerRef, System.Action<Texture2D>> pendingRequests = new Dictionary<PlayerRef, System.Action<Texture2D>>();
    private Dictionary<PlayerRef, Texture2D> cachedAvatars = new Dictionary<PlayerRef, Texture2D>();

    private void Awake()
    {
        Instance = this;
        InitializeAvatarRenderer();
    }

    private void InitializeAvatarRenderer()
    {
        // Avatar container oluştur
        avatarContainer = new GameObject("PartyAvatarContainer");
        avatarContainer.transform.position = new Vector3(3000, 3000, 0); // UI'dan uzak bir pozisyon

        // Avatar kamerasını ayarla
        GameObject cameraObj = new GameObject("PartyAvatarCamera");
        cameraObj.transform.SetParent(avatarContainer.transform);
        avatarCamera = cameraObj.AddComponent<Camera>();
        avatarCamera.clearFlags = CameraClearFlags.SolidColor;
        avatarCamera.backgroundColor = Color.clear;
        avatarCamera.orthographic = true;
        avatarCamera.orthographicSize = 1f;
        avatarCamera.nearClipPlane = 0.5f;
        avatarCamera.farClipPlane = 4f;
        avatarCamera.transform.position = new Vector3(3000, 3000.3f, -2);
        avatarCamera.transform.LookAt(new Vector3(3000, 3000, 0));
        avatarCamera.cullingMask = 1 << LayerMask.NameToLayer("PartyAvatar");
        avatarCamera.enabled = false; // Manuel render yapacağız
    }

public void RenderPlayerAvatar(PlayerRef playerRef, System.Action<Texture2D> callback)
{
    
    // Cache'de var mı kontrol et
    if (cachedAvatars.TryGetValue(playerRef, out Texture2D cachedTexture))
    {
        callback?.Invoke(cachedTexture);
        return;
    }

    
    // Pending request ekle
    pendingRequests[playerRef] = callback;

    // Player'ın Character4D'sinden veri iste
    RequestPlayerCharacterData(playerRef);
}

private void RequestPlayerCharacterData(PlayerRef playerRef)
{
    
    // Retry mekanizması ile başlat
    StartCoroutine(RequestPlayerCharacterDataWithRetry(playerRef, maxRetries: 10, retryInterval: 0.5f));
}
private IEnumerator RequestPlayerCharacterDataWithRetry(PlayerRef playerRef, int maxRetries, float retryInterval)
{
    
    int attempts = 0;
    
    while (attempts < maxRetries)
    {
        attempts++;
        // Hedef player'ın Character4D'sini bul
        NetworkObject[] allPlayers = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        
        foreach (NetworkObject netObj in allPlayers)
        {
            if (netObj == null || !netObj.IsValid) continue;
            
            if (netObj.InputAuthority == playerRef)
            {
                Character4D character4D = netObj.GetComponent<Character4D>();
                if (character4D != null)
                {
                    // Character JSON'ı al
                    string characterJson = character4D.ToJson();
                    
                    if (!string.IsNullOrEmpty(characterJson))
                    {
                        // Başarılı - render et
                        RenderAvatarFromJson(playerRef, characterJson);
                        yield break; // Başarılı, coroutine'i bitir
                    }
                }
                break; // Player bulundu ama JSON boş, retry yap
            }
        }
        
        attempts++;
        
        if (attempts < maxRetries)
        {
            yield return new WaitForSeconds(retryInterval);
        }
    }
    
    // Max retry ulaşıldı, default avatar ver
    CreateDefaultAvatar(playerRef);
}
private IEnumerator WaitAndProcessCharacterData(PlayerRef playerRef, Character4D sourceCharacter)
{
    
    // Character sync'in gelmesi için bekle
    yield return new WaitForSeconds(1f);

    try
    {
        // Character JSON'ını al
        string characterJson = sourceCharacter.ToJson();
        
        
        if (!string.IsNullOrEmpty(characterJson))
        {
            RenderAvatarFromJson(playerRef, characterJson);
        }
        else
        {
            CreateDefaultAvatar(playerRef);
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[PartyAvatarRenderer] Character data processing error - PlayerRef: {playerRef}, Error: {e.Message}");
        CreateDefaultAvatar(playerRef);
    }
}

private void RenderAvatarFromJson(PlayerRef playerRef, string characterJson)
{
    StartCoroutine(CreateAvatarCoroutine(playerRef, characterJson));
}

private IEnumerator CreateAvatarCoroutine(PlayerRef playerRef, string characterJson)
{

    // Layer kontrolü
    int partyAvatarLayerIndex = LayerMask.NameToLayer("PartyAvatar");
    if (partyAvatarLayerIndex == -1)
    {
        partyAvatarLayerIndex = 0; // Default layer
    }
    
    // Her avatar için unique pozisyon hesapla
    float offsetX = (float)playerRef.PlayerId * 3f;
    
    // Player'ın ırkını tespit et
    PlayerRace playerRace = GetPlayerRace(playerRef);
    string prefabName = playerRace == PlayerRace.Human ? "HumanPlayerPrefab" : "GoblinPlayerPrefab";

    // Prefab'ı yükle
    GameObject prefab = Resources.Load<GameObject>(prefabName);
    if (prefab == null)
    {
        Debug.LogError($"[PartyAvatarRenderer] {prefabName} not found! - PlayerRef: {playerRef}");
        CreateDefaultAvatar(playerRef);
        yield break;
    }

    // Avatar objesi oluştur
    GameObject avatarObj = Instantiate(prefab);
    avatarObj.transform.position = new Vector3(3000 + offsetX, 3000 - 2.5f, 0);
    avatarObj.name = $"Avatar_{playerRef}";

    // Character4D component'ini al ve JSON'ı uygula
    Character4D avatarCharacter = avatarObj.GetComponent<Character4D>();
    if (avatarCharacter != null)
    {
        // Diğer direction'ları tamamen devre dışı bırak
        if (avatarCharacter.Back) avatarCharacter.Back.gameObject.SetActive(false);
        if (avatarCharacter.Left) avatarCharacter.Left.gameObject.SetActive(false);
        if (avatarCharacter.Right) avatarCharacter.Right.gameObject.SetActive(false);

        // JSON'ı uygula
        avatarCharacter.FromJson(characterJson, true);
        avatarCharacter.SetDirection(Vector2.down);
        
        if (avatarCharacter.AnimationManager != null)
        {
            avatarCharacter.AnimationManager.SetState(CharacterState.Idle);
        }
        
        avatarCharacter.Initialize();
    }

    // Birkaç frame bekle ki character tamamen yüklensin
    yield return new WaitForEndOfFrame();
    yield return new WaitForEndOfFrame();

    // HEAD FILTERING - Sadece kafa kısımlarını aktif et
    SpriteRenderer[] allRenderers = avatarObj.GetComponentsInChildren<SpriteRenderer>(true);
    int headPartsCount = 0;
    
    foreach (var renderer in allRenderers)
    {
        renderer.gameObject.layer = partyAvatarLayerIndex;
        
        // Sadece kafa kısımlarını göster
        bool isHeadPart = IsHeadPart(renderer.gameObject);
        renderer.enabled = isHeadPart;
        
        if (isHeadPart)
        {
            headPartsCount++;
        }
    }
    

    // KAMERA AYARLARINI GÜNCELLE
    Vector3 originalCameraPos = avatarCamera.transform.position;
    int originalCullingMask = avatarCamera.cullingMask;
    
    avatarCamera.transform.position = new Vector3(3000 + offsetX, 3000 - 1.2f, -1f);
    avatarCamera.cullingMask = 1 << partyAvatarLayerIndex;

    // Render texture oluştur ve render et
    RenderTexture renderTexture = new RenderTexture((int)avatarSize.x, (int)avatarSize.y, 32, RenderTextureFormat.ARGB32);
    avatarCamera.targetTexture = renderTexture;
    avatarCamera.Render();

    // Texture2D'ye çevir
    RenderTexture.active = renderTexture;
    Texture2D avatarTexture = new Texture2D((int)avatarSize.x, (int)avatarSize.y, TextureFormat.RGBA32, false);
    avatarTexture.ReadPixels(new Rect(0, 0, avatarSize.x, avatarSize.y), 0, 0);
    avatarTexture.Apply();
    RenderTexture.active = null;

    // KAMERA AYARLARINI ESKİ HALİNE GETİR
    avatarCamera.transform.position = originalCameraPos;
    avatarCamera.cullingMask = originalCullingMask;

    // Cleanup
    avatarCamera.targetTexture = null;
    renderTexture.Release();
    Destroy(renderTexture);
    Destroy(avatarObj);


    // Cache'e kaydet
    cachedAvatars[playerRef] = avatarTexture;

    // Callback'i çağır
    if (pendingRequests.TryGetValue(playerRef, out System.Action<Texture2D> callback))
    {
        callback?.Invoke(avatarTexture);
        pendingRequests.Remove(playerRef);
    }
}
[ContextMenu("Debug Cache")]
private void DebugCache()
{
    foreach (var kvp in cachedAvatars)
    {
    }
    
    foreach (var kvp in pendingRequests)
    {
    }
}

public void ClearCacheForDebug()
{
    ClearCache();
}
private bool IsHeadPart(GameObject obj)
{
    string name = obj.name.ToLower();
    
    // HEAD parçaları
    bool isHeadPart = name.Contains("head") || 
                     name.Contains("hair") || 
                     name.Contains("face") || 
                     name.Contains("eyes") || 
                     name.Contains("eyebrows") ||
                     name.Contains("mouth") ||
                     name.Contains("helmet") ||
                     name.Contains("ears") ||
                     name.Contains("beard") ||
                     name.Contains("makeup") ||
                     name.Contains("mask") ||
                     name.Contains("earrings");

    // BODY parçalarını kesinlikle exclude et
    bool isBodyPart = name.Contains("body") ||
                     name.Contains("armor") ||
                     name.Contains("weapon") ||
                     name.Contains("shield") ||
                     name.Contains("legs") ||
                     name.Contains("arms") ||
                     name.Contains("torso") ||
                     name.Contains("vest") ||
                     name.Contains("bracers") ||
                     name.Contains("leggings") ||
                     name.Contains("boots") ||
                     name.Contains("gloves");

    if (isBodyPart)
    {
        return false; // Body parçası ise kesinlikle false
    }

    // Parent kontrolü - sadece head parent'ları kabul et
    Transform parent = obj.transform.parent;
    if (parent != null)
    {
        string parentName = parent.name.ToLower();
        bool hasHeadParent = parentName.Contains("head") || 
                           parentName.Contains("hair") || 
                           parentName.Contains("face") || 
                           parentName.Contains("helmet");
        
        if (hasHeadParent)
        {
            return true;
        }
    }

    return isHeadPart;
}

    private PlayerRace GetPlayerRace(PlayerRef playerRef)
    {
        // Player'ın NetworkObject'ini bul
        NetworkObject[] allPlayers = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        
        foreach (NetworkObject netObj in allPlayers)
        {
            if (netObj != null && netObj.IsValid && netObj.InputAuthority == playerRef)
            {
                // Player object adından ırk çıkar
                string playerName = netObj.gameObject.name;
                if (playerName.Contains("Goblin"))
                {
                    return PlayerRace.Goblin;
                }
                break;
            }
        }
        
        return PlayerRace.Human; // Default
    }

    private void CreateDefaultAvatar(PlayerRef playerRef)
    {
        // 64x64 default avatar texture oluştur
        Texture2D defaultTexture = new Texture2D((int)avatarSize.x, (int)avatarSize.y, TextureFormat.RGBA32, false);
        Color[] colors = new Color[defaultTexture.width * defaultTexture.height];
        
        // Basit bir circle çiz
        Vector2 center = new Vector2(defaultTexture.width * 0.5f, defaultTexture.height * 0.5f);
        float radius = defaultTexture.width * 0.4f;
        
        for (int y = 0; y < defaultTexture.height; y++)
        {
            for (int x = 0; x < defaultTexture.width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= radius)
                {
                    colors[y * defaultTexture.width + x] = Color.gray;
                }
                else
                {
                    colors[y * defaultTexture.width + x] = Color.clear;
                }
            }
        }
        
        defaultTexture.SetPixels(colors);
        defaultTexture.Apply();

        // Cache'e kaydet
        cachedAvatars[playerRef] = defaultTexture;

        // Callback'i çağır
        if (pendingRequests.TryGetValue(playerRef, out System.Action<Texture2D> callback))
        {
            callback?.Invoke(defaultTexture);
            pendingRequests.Remove(playerRef);
        }
    }

    public void ClearCache()
    {
        foreach (var texture in cachedAvatars.Values)
        {
            if (texture != null)
            {
                Destroy(texture);
            }
        }
        cachedAvatars.Clear();
    }

    private void OnDestroy()
    {
        ClearCache();
        
        if (avatarContainer != null)
        {
            Destroy(avatarContainer);
        }
    }
}