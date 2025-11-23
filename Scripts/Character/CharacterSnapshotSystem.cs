// Path: Assets/Game/Scripts/CharacterSnapshotSystem.cs

using UnityEngine;
using UnityEngine.UI;
using Fusion;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using System.Collections;

public class CharacterSnapshotSystem : MonoBehaviour
{
    public static CharacterSnapshotSystem Instance { get; private set; }

    [Header("UI Reference")]
    [SerializeField] private RawImage characterPreviewImage;

    [Header("Snapshot Settings")]
    [SerializeField] private int snapshotResolution = 512;
    [SerializeField] private float cameraDistance = 3f;
    [SerializeField] private float orthographicSize = 2f;
    [SerializeField] private float debounceTime = 0.3f;
    
    [Header("Performance")]
    [SerializeField] private float maxWaitTime = 5f;
    [SerializeField] private float checkInterval = 0.1f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;
    [SerializeField] private bool useFallbackPlaceholder = true;

    private Texture2D currentSnapshot;
    private bool isCapturingSnapshot = false;
    private Coroutine debounceCoroutine;
    private bool isSubscribed = false;
    private bool isInitialized = false;
    private int characterPreviewLayer = -1; // Cache layer index
    private bool hasAttemptedSnapshot = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Layer validation (kritik!)
        characterPreviewLayer = LayerMask.NameToLayer("CharacterPreview");
        if (characterPreviewLayer == -1)
        {
            Debug.LogError("[CharacterSnapshot] CRITICAL: 'CharacterPreview' layer not found! Please add it in Project Settings > Tags and Layers");
            enabled = false; // Sistemi devre dışı bırak
            return;
        }

        LogDebug($"Initialized. Layer: {characterPreviewLayer}, Resolution: {snapshotResolution}");

        // System diagnostics - HER ZAMAN log'la (debug mode olmasa da)
        LogSystemDiagnostics();

        HidePreviewImage();
    }

    private void LogSystemDiagnostics()
    {
        Debug.Log($"[CharacterSnapshot] === SYSTEM DIAGNOSTICS ===");
        Debug.Log($"[CharacterSnapshot] Device: {SystemInfo.deviceModel}");
        Debug.Log($"[CharacterSnapshot] OS: {SystemInfo.operatingSystem}");
        Debug.Log($"[CharacterSnapshot] GPU: {SystemInfo.graphicsDeviceName}");
        Debug.Log($"[CharacterSnapshot] Graphics API: {SystemInfo.graphicsDeviceType}");
        Debug.Log($"[CharacterSnapshot] Graphics Memory: {SystemInfo.graphicsMemorySize} MB");
        Debug.Log($"[CharacterSnapshot] Max Texture Size: {SystemInfo.maxTextureSize}");
        Debug.Log($"[CharacterSnapshot] ARGB32 Supported: {SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32)}");
        Debug.Log($"[CharacterSnapshot] Default Format Supported: {SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Default)}");
        Debug.Log($"[CharacterSnapshot] Mobile Platform: {Application.isMobilePlatform}");
        Debug.Log($"[CharacterSnapshot] ===========================");
    }

    // DEĞİŞTİ: HeadSnapshot mantığını kopyaladık
    private void Start()
    {
        if (IsServerMode())
        {
            gameObject.SetActive(false);
            return;
        }
        
        // YENI: Hemen snapshot almaya başla (HeadSnapshot gibi)
        StartCoroutine(TakeSnapshotWhenReady());
    }

    // YENİ METOD: HeadSnapshot'tan uyarlandı
    private IEnumerator TakeSnapshotWhenReady()
    {
        if (isCapturingSnapshot) yield break;
        isCapturingSnapshot = true;

        LogDebug("Starting snapshot capture process...");

        float waitTime = 0f;
        GameObject localPlayer = null;

        // Player hazır olana kadar bekle
        while (localPlayer == null && waitTime < maxWaitTime)
        {
            localPlayer = FindLocalPlayer();

            if (localPlayer != null)
            {
                if (IsCharacterReady(localPlayer))
                {
                    LogDebug($"Character ready! Player: {localPlayer.name}");

                    // Snapshot al
                    yield return new WaitForEndOfFrame();
                    yield return StartCoroutine(CaptureSnapshotCoroutine(localPlayer));

                    // Equipment event'ine subscribe ol (ek güncellemeler için)
                    if (!isSubscribed)
                    {
                        StartCoroutine(SubscribeToEquipmentChanges(localPlayer));
                    }

                    isInitialized = true;
                    break;
                }
                else
                {
                    LogDebug("Player found but character not ready, waiting...");
                    localPlayer = null; // Character hazır değilse tekrar bekle
                }
            }

            yield return new WaitForSeconds(checkInterval);
            waitTime += checkInterval;
        }

        if (localPlayer == null)
        {
            // RETRY: HeadSnapshot gibi 2 saniye sonra tekrar dene
            if (!hasAttemptedSnapshot)
            {
                Debug.LogWarning("[CharacterSnapshot] Max wait time reached, retrying in 2 seconds");
                yield return new WaitForSeconds(2f);
                isCapturingSnapshot = false;
                StartCoroutine(TakeSnapshotWhenReady());
            }
            else
            {
                // 2. deneme de başarısızsa placeholder göster
                Debug.LogError("[CharacterSnapshot] Failed to capture snapshot after retry");
                if (useFallbackPlaceholder && characterPreviewImage != null)
                {
                    ShowFallbackPlaceholder();
                }
                isCapturingSnapshot = false;
            }
        }
        else
        {
            isCapturingSnapshot = false;
        }
    }

    // YENİ METOD: Basit player bulma (HeadSnapshot'tan)
    private GameObject FindLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            NetworkObject networkObj = player.GetComponent<NetworkObject>();
            if (networkObj != null && networkObj.IsValid && networkObj.HasInputAuthority)
            {
                return player;
            }
        }
        return null;
    }

    // YENİ METOD: Detaylı character hazırlık kontrolü (HeadSnapshot'tan uyarlandı)
    private bool IsCharacterReady(GameObject player)
    {
        // NetworkObject kontrol
        NetworkObject networkObj = player.GetComponent<NetworkObject>();
        if (networkObj == null || !networkObj.IsValid || !networkObj.HasInputAuthority)
            return false;

        // Character4D kontrol
        Character4D character4D = player.GetComponent<Character4D>();
        if (character4D == null || character4D.Front == null)
            return false;

        // Character appearance yüklenmiş mi kontrol
        Transform upperBody = character4D.Front.transform.Find("UpperBody");
        if (upperBody == null)
            return false;

        // En az bir renderer aktif mi kontrol
        SpriteRenderer[] renderers = character4D.Front.GetComponentsInChildren<SpriteRenderer>(true);
        bool hasActiveRenderer = false;
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.enabled && renderer.sprite != null)
            {
                hasActiveRenderer = true;
                break;
            }
        }

        return hasActiveRenderer;
    }

    // DEĞİŞTİ: Equipment subscription artık opsiyonel (sadece ek güncellemeler için)
    private IEnumerator SubscribeToEquipmentChanges(GameObject localPlayer)
    {
        if (isSubscribed) yield break;
        
        yield return new WaitForSeconds(0.5f);
        
        if (localPlayer == null) yield break;

        EquipmentSystem equipmentSystem = localPlayer.GetComponent<EquipmentSystem>();
        if (equipmentSystem != null)
        {
            equipmentSystem.OnEquipmentChanged += OnEquipmentChangedDebounced;
            isSubscribed = true;
        }
    }

    private void OnEquipmentChangedDebounced(ItemData item, EquipmentSlotType slotType)
    {
        // Equipment değiştiğinde debounced update
        if (debounceCoroutine != null)
        {
            StopCoroutine(debounceCoroutine);
        }
        
        debounceCoroutine = StartCoroutine(DebounceSnapshotCapture());
    }

    private IEnumerator DebounceSnapshotCapture()
    {
        yield return new WaitForSeconds(debounceTime);
        
        if (!isCapturingSnapshot)
        {
            RefreshSnapshot();
        }
        
        debounceCoroutine = null;
    }

    // DEĞİŞTİ: Player parametresi opsiyonel, bulamazsa tekrar ara
    private IEnumerator CaptureSnapshotCoroutine(GameObject localPlayer = null)
    {
        if (isCapturingSnapshot && localPlayer != null) yield break;

        isCapturingSnapshot = true;

        // Player yoksa bul
        if (localPlayer == null)
        {
            localPlayer = FindLocalPlayer();

            if (localPlayer == null || !IsCharacterReady(localPlayer))
            {
                Debug.LogWarning("[CharacterSnapshot] Player not ready for snapshot");
                isCapturingSnapshot = false;
                yield break;
            }
        }

        Character4D character4D = localPlayer.GetComponent<Character4D>();
        if (character4D == null || character4D.Front == null)
        {
            Debug.LogWarning("[CharacterSnapshot] Character4D not found");
            isCapturingSnapshot = false;
            yield break;
        }

        // Front character aktif olduğundan emin ol
        bool frontWasActive = character4D.Front.gameObject.activeSelf;
        character4D.Front.gameObject.SetActive(true);

        yield return new WaitForEndOfFrame();

        // Geçici kamera sistemi
        GameObject tempCameraObj = null;
        Camera snapshotCamera = null;
        RenderTexture renderTexture = null;

        try
        {
            tempCameraObj = new GameObject("TempSnapshotCamera");
            snapshotCamera = tempCameraObj.AddComponent<Camera>();

            snapshotCamera.clearFlags = CameraClearFlags.SolidColor;
            snapshotCamera.backgroundColor = Color.clear;
            snapshotCamera.orthographic = true;
            snapshotCamera.orthographicSize = orthographicSize;
            snapshotCamera.cullingMask = 1 << characterPreviewLayer;
            snapshotCamera.depth = 200;
            snapshotCamera.enabled = false;

            Vector3 playerPos = localPlayer.transform.position;
            tempCameraObj.transform.position = playerPos + Vector3.back * cameraDistance;
            tempCameraObj.transform.LookAt(playerPos);

            // Character'ı geçici layer'a al
            var originalLayers = new System.Collections.Generic.Dictionary<Transform, int>();
            SetCharacterLayer(character4D.Front.transform, characterPreviewLayer, originalLayers);

            // RenderTexture oluştur - PLATFORM UYUMLU FORMAT
            RenderTextureFormat format = GetBestRenderTextureFormat();
            int antiAliasing = GetBestAntiAliasing();

            LogDebug($"Creating RenderTexture: Format={format}, AA={antiAliasing}, Size={snapshotResolution}");

            renderTexture = new RenderTexture(snapshotResolution, snapshotResolution, 16, format);
            renderTexture.antiAliasing = antiAliasing;

            if (!renderTexture.Create())
            {
                Debug.LogError("[CharacterSnapshot] Failed to create RenderTexture!");
                RestoreCharacterLayers(originalLayers);
                yield break;
            }

            snapshotCamera.targetTexture = renderTexture;

            // Render et
            snapshotCamera.Render();

            // Texture2D'ye çevir
            RenderTexture.active = renderTexture;

            if (currentSnapshot != null)
            {
                Destroy(currentSnapshot);
            }

            // Texture format da platform uyumlu
            TextureFormat textureFormat = SystemInfo.SupportsTextureFormat(TextureFormat.ARGB32)
                ? TextureFormat.ARGB32
                : TextureFormat.RGBA32;

            currentSnapshot = new Texture2D(snapshotResolution, snapshotResolution, textureFormat, false);
            currentSnapshot.ReadPixels(new Rect(0, 0, snapshotResolution, snapshotResolution), 0, 0);
            currentSnapshot.Apply();

            LogDebug($"Snapshot captured! Size: {currentSnapshot.width}x{currentSnapshot.height}");

            // UI'ya uygula
            if (characterPreviewImage != null)
            {
                characterPreviewImage.texture = currentSnapshot;

                if (currentSnapshot != null)
                {
                    ShowPreviewImage();
                }
            }
            else
            {
                Debug.LogWarning("[CharacterSnapshot] characterPreviewImage is null!");
            }

            // Cleanup
            RestoreCharacterLayers(originalLayers);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CharacterSnapshot] Exception during snapshot: {e.Message}\n{e.StackTrace}");

            // Hata durumunda fallback placeholder göster
            if (useFallbackPlaceholder && characterPreviewImage != null && currentSnapshot == null)
            {
                ShowFallbackPlaceholder();
            }
        }
        finally
        {
            // Her durumda cleanup yap
            if (!frontWasActive && character4D.Front != null)
                character4D.Front.gameObject.SetActive(false);

            RenderTexture.active = null;

            if (renderTexture != null)
            {
                renderTexture.Release();
                Destroy(renderTexture);
            }

            if (tempCameraObj != null)
            {
                Destroy(tempCameraObj);
            }

            hasAttemptedSnapshot = true;
            isCapturingSnapshot = false;
        }
    }

    private void ShowFallbackPlaceholder()
    {
        // Basit gri placeholder texture oluştur
        int placeholderSize = 256;
        Texture2D placeholder = new Texture2D(placeholderSize, placeholderSize, TextureFormat.RGB24, false);

        // Gradient efekti
        Color topColor = new Color(0.3f, 0.3f, 0.35f);
        Color bottomColor = new Color(0.2f, 0.2f, 0.25f);

        for (int y = 0; y < placeholderSize; y++)
        {
            float t = (float)y / placeholderSize;
            Color lineColor = Color.Lerp(bottomColor, topColor, t);

            for (int x = 0; x < placeholderSize; x++)
            {
                placeholder.SetPixel(x, y, lineColor);
            }
        }

        placeholder.Apply();

        if (characterPreviewImage != null)
        {
            characterPreviewImage.texture = placeholder;
            ShowPreviewImage();
        }

        Debug.LogWarning("[CharacterSnapshot] Using fallback placeholder - snapshot failed");
    }

    private void SetCharacterLayer(Transform parent, int newLayer, System.Collections.Generic.Dictionary<Transform, int> originalLayers)
    {
        originalLayers[parent] = parent.gameObject.layer;
        parent.gameObject.layer = newLayer;

        foreach (Transform child in parent)
        {
            SetCharacterLayer(child, newLayer, originalLayers);
        }
    }

    private void RestoreCharacterLayers(System.Collections.Generic.Dictionary<Transform, int> originalLayers)
    {
        foreach (var kvp in originalLayers)
        {
            if (kvp.Key != null)
            {
                kvp.Key.gameObject.layer = kvp.Value;
            }
        }
    }

    private void HidePreviewImage()
    {
        if (characterPreviewImage != null)
        {
            CanvasGroup canvasGroup = characterPreviewImage.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = characterPreviewImage.gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 0f;
        }
    }

private void ShowPreviewImage()
{
    if (characterPreviewImage != null)
    {
        CanvasGroup canvasGroup = characterPreviewImage.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = characterPreviewImage.gameObject.AddComponent<CanvasGroup>();
        }
        
        // Parent aktif değilse instant show, aktifse fade-in
        if (!characterPreviewImage.gameObject.activeInHierarchy)
        {
            // Parent deaktif - instant show
            canvasGroup.alpha = 1f;
        }
        else
        {
            // Parent aktif - fade-in
            StartCoroutine(FadeInPreviewImage(canvasGroup));
        }
    }
}

    private IEnumerator FadeInPreviewImage(CanvasGroup canvasGroup)
    {
        float fadeTime = 0.3f;
        float elapsed = 0f;
        
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeTime);
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
    }

    private bool IsServerMode()
    {
        if (Application.isEditor) return false;

        string[] args = System.Environment.GetCommandLineArgs();
        return System.Array.Exists(args, arg => arg == "-server" || arg == "-batchmode");
    }

    // PLATFORM UYUMLULUK METODLARI
    private RenderTextureFormat GetBestRenderTextureFormat()
    {
        // Öncelik sırasıyla format dene
        RenderTextureFormat[] preferredFormats = new RenderTextureFormat[]
        {
            RenderTextureFormat.ARGB32,
            RenderTextureFormat.Default,
            RenderTextureFormat.ARGBHalf,
            RenderTextureFormat.ARGB4444
        };

        foreach (var format in preferredFormats)
        {
            if (SystemInfo.SupportsRenderTextureFormat(format))
            {
                LogDebug($"Selected RenderTextureFormat: {format}");
                return format;
            }
        }

        // Hiçbiri desteklenmiyorsa default kullan
        Debug.LogWarning("[CharacterSnapshot] No preferred RenderTextureFormat supported, using Default");
        return RenderTextureFormat.Default;
    }

    private int GetBestAntiAliasing()
    {
        // Platform'a göre AA seviyesini belirle
        int[] antiAliasingLevels = new int[] { 2, 4, 8, 1 };

        foreach (int level in antiAliasingLevels)
        {
            // Mobilde düşük seviye tercih et
            if (Application.isMobilePlatform && level <= 2)
            {
                LogDebug($"Mobile platform detected, using AA={level}");
                return level;
            }

            // Desktop'ta yüksek seviye kullan
            if (!Application.isMobilePlatform && level > 1)
            {
                LogDebug($"Desktop platform, using AA={level}");
                return level;
            }
        }

        return 1; // Fallback: AA yok
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"[CharacterSnapshot] {message}");
        }
    }

    // Public metodlar
    public void RefreshSnapshot()
    {
        if (!isCapturingSnapshot && isInitialized)
        {
            StartCoroutine(CaptureSnapshotCoroutine());
        }
    }

    public Texture2D GetCurrentSnapshot()
    {
        return currentSnapshot;
    }

public void SetPreviewImage(RawImage previewImage)
{
    characterPreviewImage = previewImage;
    
    HidePreviewImage(); // Önce gizle
    
    // Mevcut snapshot varsa hemen göster
    if (currentSnapshot != null && previewImage != null)
    {
        previewImage.texture = currentSnapshot;
        ShowPreviewImage();
    }
}

    private void OnDestroy()
    {
        if (debounceCoroutine != null)
        {
            StopCoroutine(debounceCoroutine);
        }

        if (isSubscribed)
        {
            GameObject localPlayer = FindLocalPlayer();
            if (localPlayer != null)
            {
                EquipmentSystem equipmentSystem = localPlayer.GetComponent<EquipmentSystem>();
                if (equipmentSystem != null)
                {
                    equipmentSystem.OnEquipmentChanged -= OnEquipmentChangedDebounced;
                }
            }
            isSubscribed = false;
        }

        if (currentSnapshot != null)
        {
            Destroy(currentSnapshot);
            currentSnapshot = null;
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }
}