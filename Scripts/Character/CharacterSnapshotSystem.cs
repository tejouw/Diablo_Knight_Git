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

    // Mali GPU detection cache
    private bool? isMaliGPU = null;
    private bool IsMaliGPU
    {
        get
        {
            if (!isMaliGPU.HasValue)
            {
                string gpuName = SystemInfo.graphicsDeviceName.ToLower();
                string gpuVendor = SystemInfo.graphicsDeviceVendor.ToLower();
                isMaliGPU = gpuName.Contains("mali") || gpuVendor.Contains("arm");
            }
            return isMaliGPU.Value;
        }
    }

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

        // Layer validation (kritik!) - FALLBACK ADDED
        characterPreviewLayer = LayerMask.NameToLayer("CharacterPreview");
        if (characterPreviewLayer == -1)
        {
            Debug.LogWarning("[CharacterSnapshot] WARNING: 'CharacterPreview' layer not found!");

            // FALLBACK: UI layer kullan (HeadSnapshot gibi)
            characterPreviewLayer = LayerMask.NameToLayer("UI");

            if (characterPreviewLayer == -1)
            {
                Debug.LogError("[CharacterSnapshot] CRITICAL: Neither 'CharacterPreview' nor 'UI' layer found! System disabled.");
                enabled = false;
                return;
            }

            Debug.LogWarning($"[CharacterSnapshot] Using FALLBACK layer 'UI' (layer {characterPreviewLayer}) instead of CharacterPreview");
            Debug.LogWarning("[CharacterSnapshot] RECOMMENDED: Add 'CharacterPreview' layer in Project Settings > Tags and Layers for better isolation");
        }
        else
        {
            Debug.Log($"[CharacterSnapshot] Using 'CharacterPreview' layer (layer {characterPreviewLayer})");
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
        Debug.Log($"[CharacterSnapshot] GPU Vendor: {SystemInfo.graphicsDeviceVendor}");
        Debug.Log($"[CharacterSnapshot] GPU ID: {SystemInfo.graphicsDeviceID}");
        Debug.Log($"[CharacterSnapshot] MALI GPU DETECTED: {IsMaliGPU}"); // Mali detection
        Debug.Log($"[CharacterSnapshot] Graphics API: {SystemInfo.graphicsDeviceType}");
        Debug.Log($"[CharacterSnapshot] Graphics Memory: {SystemInfo.graphicsMemorySize} MB");
        Debug.Log($"[CharacterSnapshot] Max Texture Size: {SystemInfo.maxTextureSize}");
        Debug.Log($"[CharacterSnapshot] Shader Level: {SystemInfo.graphicsShaderLevel}");

        // RenderTexture format desteği
        Debug.Log($"[CharacterSnapshot] ARGB32: {SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32)}");
        Debug.Log($"[CharacterSnapshot] Default: {SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.Default)}");
        Debug.Log($"[CharacterSnapshot] RGB565: {SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB565)}");
        Debug.Log($"[CharacterSnapshot] ARGB4444: {SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB4444)}");
        Debug.Log($"[CharacterSnapshot] ARGB1555: {SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB1555)}");

        // Texture format desteği
        Debug.Log($"[CharacterSnapshot] Texture ARGB32: {SystemInfo.SupportsTextureFormat(TextureFormat.ARGB32)}");
        Debug.Log($"[CharacterSnapshot] Texture RGBA32: {SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32)}");
        Debug.Log($"[CharacterSnapshot] Texture RGB24: {SystemInfo.SupportsTextureFormat(TextureFormat.RGB24)}");

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
            // RETRY LOGIC: hasAttemptedSnapshot sayacı yerine retry counter kullan
            if (!hasAttemptedSnapshot)
            {
                Debug.LogWarning("[CharacterSnapshot] Max wait time reached (first attempt), retrying in 2 seconds...");
                hasAttemptedSnapshot = true;
                yield return new WaitForSeconds(2f);
                isCapturingSnapshot = false;
                StartCoroutine(TakeSnapshotWhenReady());
            }
            else
            {
                // 2. deneme de başarısızsa - SON ÇARE: Fallback placeholder göster
                Debug.LogError("[CharacterSnapshot] CRITICAL: Failed to find/initialize local player after all retries!");
                Debug.LogError("[CharacterSnapshot] This may indicate:");
                Debug.LogError("[CharacterSnapshot] 1. Network connection issues");
                Debug.LogError("[CharacterSnapshot] 2. Character4D component not initialized");
                Debug.LogError("[CharacterSnapshot] 3. Player tag missing or NetworkObject not setup correctly");

                if (useFallbackPlaceholder && characterPreviewImage != null)
                {
                    Debug.LogWarning("[CharacterSnapshot] Displaying fallback placeholder image");
                    ShowFallbackPlaceholder();
                }
                else
                {
                    Debug.LogError("[CharacterSnapshot] Cannot show fallback - useFallbackPlaceholder disabled or image null");
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
        if (player == null)
        {
            LogDebug("IsCharacterReady: player is null");
            return false;
        }

        // NetworkObject kontrol
        NetworkObject networkObj = player.GetComponent<NetworkObject>();
        if (networkObj == null)
        {
            LogDebug("IsCharacterReady: NetworkObject component not found");
            return false;
        }

        if (!networkObj.IsValid)
        {
            LogDebug("IsCharacterReady: NetworkObject is not valid");
            return false;
        }

        if (!networkObj.HasInputAuthority)
        {
            LogDebug("IsCharacterReady: NetworkObject does not have input authority");
            return false;
        }

        // Character4D kontrol
        Character4D character4D = player.GetComponent<Character4D>();
        if (character4D == null)
        {
            LogDebug("IsCharacterReady: Character4D component not found");
            return false;
        }

        if (character4D.Front == null)
        {
            LogDebug("IsCharacterReady: Character4D.Front is null");
            return false;
        }

        // Character appearance yüklenmiş mi kontrol
        Transform upperBody = character4D.Front.transform.Find("UpperBody");
        if (upperBody == null)
        {
            LogDebug("IsCharacterReady: UpperBody transform not found");
            return false;
        }

        // En az bir renderer aktif mi kontrol
        SpriteRenderer[] renderers = character4D.Front.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            LogDebug("IsCharacterReady: No SpriteRenderers found");
            return false;
        }

        bool hasActiveRenderer = false;
        int activeCount = 0;
        foreach (var renderer in renderers)
        {
            if (renderer != null && renderer.enabled && renderer.sprite != null)
            {
                hasActiveRenderer = true;
                activeCount++;
            }
        }

        if (!hasActiveRenderer)
        {
            LogDebug($"IsCharacterReady: No active renderers found (total renderers: {renderers.Length})");
            return false;
        }

        LogDebug($"IsCharacterReady: Character is READY ({activeCount}/{renderers.Length} active renderers)");
        return true;
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

            // MALI GPU FIX: Mali GPU'lar Depth clearFlags tercih eder
            if (IsMaliGPU)
            {
                snapshotCamera.clearFlags = CameraClearFlags.Depth;
                Debug.Log("[CharacterSnapshot] MALI: Using Depth clearFlags");
            }
            else
            {
                snapshotCamera.clearFlags = CameraClearFlags.SolidColor;
            }

            snapshotCamera.backgroundColor = Color.clear;
            snapshotCamera.orthographic = true;
            snapshotCamera.orthographicSize = orthographicSize;
            snapshotCamera.cullingMask = 1 << characterPreviewLayer;
            snapshotCamera.depth = 200;
            snapshotCamera.enabled = false;

            Vector3 playerPos = localPlayer.transform.position;
            Vector3 cameraPos = playerPos + Vector3.back * cameraDistance;
            tempCameraObj.transform.position = cameraPos;
            tempCameraObj.transform.LookAt(playerPos);

            // DEBUG: Kamera ve karakter pozisyonları
            Debug.Log($"[CharacterSnapshot] Camera Setup: PlayerPos={playerPos}, CameraPos={cameraPos}, Distance={cameraDistance}");
            Debug.Log($"[CharacterSnapshot] Camera LookAt target: {playerPos}");

            // Character'ı geçici layer'a al
            var originalLayers = new System.Collections.Generic.Dictionary<Transform, int>();
            int objectsLayerChanged = SetCharacterLayer(character4D.Front.transform, characterPreviewLayer, originalLayers);

            Debug.Log($"[CharacterSnapshot] Changed {objectsLayerChanged} objects to layer {characterPreviewLayer} for rendering");

            // RenderTexture oluştur - PLATFORM UYUMLU FORMAT + RETRY LOGIC
            RenderTextureFormat format = GetBestRenderTextureFormat();
            int antiAliasing = GetBestAntiAliasing();

            Debug.Log($"[CharacterSnapshot] Creating RenderTexture: Format={format}, AA={antiAliasing}, Size={snapshotResolution}");

            // MALI GPU FIX: Mali GPU'lar 24-bit depth buffer tercih eder
            int depthBuffer = IsMaliGPU ? 24 : 16;
            if (IsMaliGPU)
            {
                Debug.Log("[CharacterSnapshot] MALI: Using 24-bit depth buffer");
            }

            // RETRY: Önce tercih edilen format/AA ile dene
            renderTexture = new RenderTexture(snapshotResolution, snapshotResolution, depthBuffer, format);
            renderTexture.antiAliasing = antiAliasing;

            if (!renderTexture.Create())
            {
                Debug.LogWarning($"[CharacterSnapshot] Failed with Format={format}, AA={antiAliasing}. Retrying with AA=1...");

                // RETRY 1: AA'yı kaldır
                renderTexture.antiAliasing = 1;
                if (!renderTexture.Create())
                {
                    Debug.LogWarning($"[CharacterSnapshot] Failed with AA=1. Retrying with RGB565...");

                    // RETRY 2: En basit format dene (RGB565)
                    if (renderTexture != null)
                    {
                        renderTexture.Release();
                        Destroy(renderTexture);
                    }

                    renderTexture = new RenderTexture(snapshotResolution, snapshotResolution, depthBuffer, RenderTextureFormat.RGB565);
                    renderTexture.antiAliasing = 1;

                    if (!renderTexture.Create())
                    {
                        Debug.LogError("[CharacterSnapshot] CRITICAL: Failed to create RenderTexture with all formats!");
                        RestoreCharacterLayers(originalLayers);

                        // Hata durumunda fallback placeholder göster
                        if (useFallbackPlaceholder && characterPreviewImage != null)
                        {
                            ShowFallbackPlaceholder();
                        }

                        yield break;
                    }
                    else
                    {
                        Debug.Log("[CharacterSnapshot] SUCCESS with RGB565, AA=1");
                    }
                }
                else
                {
                    Debug.Log($"[CharacterSnapshot] SUCCESS with Format={format}, AA=1");
                }
            }
            else
            {
                Debug.Log($"[CharacterSnapshot] SUCCESS with Format={format}, AA={antiAliasing}");
            }

            snapshotCamera.targetTexture = renderTexture;

            // MALI GPU FIX: Explicit RenderTexture clear (garbage data önleme)
            if (IsMaliGPU)
            {
                RenderTexture previousActive = RenderTexture.active;
                RenderTexture.active = renderTexture;
                GL.Clear(true, true, Color.clear);
                RenderTexture.active = previousActive;
                Debug.Log("[CharacterSnapshot] MALI: Explicitly cleared RenderTexture to prevent garbage data");
            }

            // Render et
            snapshotCamera.Render();

            // VALIDATION: RenderTexture render edildi mi kontrol et
            if (renderTexture == null || !renderTexture.IsCreated())
            {
                Debug.LogError("[CharacterSnapshot] RenderTexture is null or not created after Render()!");
                RestoreCharacterLayers(originalLayers);
                yield break;
            }

            Debug.Log($"[CharacterSnapshot] Camera.Render() completed. RenderTexture: {renderTexture.width}x{renderTexture.height}, Format: {renderTexture.format}");

            // CRITICAL: Bazı mobil GPU'lar için RenderTexture.active atanmasından sonra frame bekle
            RenderTexture.active = renderTexture;

            // Mobil cihazlarda ReadPixels için ek frame bekle
            if (Application.isMobilePlatform)
            {
                yield return null; // Bir frame bekle

                // MALI GPU FIX: Mali GPU'lar için ek bir frame daha bekle
                if (IsMaliGPU)
                {
                    Debug.Log("[CharacterSnapshot] MALI: Waiting extra frame before ReadPixels");
                    yield return null; // Mali için ikinci frame
                }
            }

            if (currentSnapshot != null)
            {
                Destroy(currentSnapshot);
            }

            // Texture format da platform uyumlu - GENİŞLETİLDİ
            TextureFormat textureFormat;

            if (SystemInfo.SupportsTextureFormat(TextureFormat.ARGB32))
            {
                textureFormat = TextureFormat.ARGB32;
            }
            else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGBA32))
            {
                textureFormat = TextureFormat.RGBA32;
            }
            else if (SystemInfo.SupportsTextureFormat(TextureFormat.RGB24))
            {
                // Alpha olmadan dene (bazı mobil GPU'lar alpha istemez)
                textureFormat = TextureFormat.RGB24;
                Debug.LogWarning("[CharacterSnapshot] Using RGB24 (no alpha) - ARGB32/RGBA32 not supported");
            }
            else
            {
                // Son çare
                textureFormat = TextureFormat.RGBA32;
                Debug.LogWarning("[CharacterSnapshot] Forcing RGBA32 as fallback");
            }

            currentSnapshot = new Texture2D(snapshotResolution, snapshotResolution, textureFormat, false);

            try
            {
                currentSnapshot.ReadPixels(new Rect(0, 0, snapshotResolution, snapshotResolution), 0, 0);
                currentSnapshot.Apply();
                Debug.Log($"[CharacterSnapshot] Snapshot captured! Size: {currentSnapshot.width}x{currentSnapshot.height}, Format: {textureFormat}");
            }
            catch (System.Exception readPixelsException)
            {
                Debug.LogError($"[CharacterSnapshot] ReadPixels failed: {readPixelsException.Message}");
                Destroy(currentSnapshot);
                currentSnapshot = null;

                // ReadPixels başarısız olursa fallback placeholder göster
                if (useFallbackPlaceholder && characterPreviewImage != null)
                {
                    ShowFallbackPlaceholder();
                }

                RestoreCharacterLayers(originalLayers);
                yield break;
            }

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
            Debug.LogError($"[CharacterSnapshot] EXCEPTION during snapshot capture!");
            Debug.LogError($"[CharacterSnapshot] Exception Type: {e.GetType().Name}");
            Debug.LogError($"[CharacterSnapshot] Message: {e.Message}");
            Debug.LogError($"[CharacterSnapshot] StackTrace: {e.StackTrace}");

            // Detaylı cihaz bilgisi tekrar log'la
            Debug.LogError($"[CharacterSnapshot] Device Info: {SystemInfo.deviceModel}, GPU: {SystemInfo.graphicsDeviceName}, API: {SystemInfo.graphicsDeviceType}");

            // Hata durumunda fallback placeholder göster
            if (useFallbackPlaceholder && characterPreviewImage != null && currentSnapshot == null)
            {
                Debug.LogWarning("[CharacterSnapshot] Showing fallback placeholder due to exception");
                ShowFallbackPlaceholder();
            }
            else if (characterPreviewImage == null)
            {
                Debug.LogError("[CharacterSnapshot] Cannot show placeholder - characterPreviewImage is null!");
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

    private int SetCharacterLayer(Transform parent, int newLayer, System.Collections.Generic.Dictionary<Transform, int> originalLayers)
    {
        int count = 1; // Parent kendisi
        originalLayers[parent] = parent.gameObject.layer;
        parent.gameObject.layer = newLayer;

        foreach (Transform child in parent)
        {
            count += SetCharacterLayer(child, newLayer, originalLayers);
        }

        return count;
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

    // PLATFORM UYUMLULUK METODLARI - IMPROVED + MALI SPECIFIC
    private RenderTextureFormat GetBestRenderTextureFormat()
    {
        // Mali GPU SPECIAL CASE - Mali G6xx serisi bilinen sorunlar
        if (IsMaliGPU)
        {
            Debug.Log("[CharacterSnapshot] Mali GPU detected! Using Mali-optimized format priority");

            // Mali GPU'lar için özel format sıralaması
            RenderTextureFormat[] maliFormats = new RenderTextureFormat[]
            {
                RenderTextureFormat.Default,     // Mali Default format'ı tercih eder
                RenderTextureFormat.ARGB32,      // İkinci seçenek
                RenderTextureFormat.RGB565,      // Basit fallback
                RenderTextureFormat.ARGB4444,    // Düşük kalite
                RenderTextureFormat.ARGB1555     // Son çare
            };

            foreach (var format in maliFormats)
            {
                if (SystemInfo.SupportsRenderTextureFormat(format))
                {
                    Debug.Log($"[CharacterSnapshot] MALI: Selected {format}");
                    return format;
                }
            }
        }

        // Graphics API kontrolü - OpenGL ES 2.0 için özel davranış
        bool isLowEndMobile = Application.isMobilePlatform &&
                             (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2 ||
                              SystemInfo.graphicsMemorySize < 512);

        // Öncelik sırasıyla format dene (MOBİL ÖNCE)
        RenderTextureFormat[] preferredFormats;

        if (isLowEndMobile)
        {
            // Düşük seviye mobil cihazlar için basit formatlar
            preferredFormats = new RenderTextureFormat[]
            {
                RenderTextureFormat.RGB565,      // En basit, çoğu mobilde çalışır
                RenderTextureFormat.ARGB4444,    // Düşük kaliteli alpha
                RenderTextureFormat.Default,     // Sistem default
                RenderTextureFormat.ARGB1555,    // Alternatif düşük kalite
                RenderTextureFormat.R8           // Son çare grayscale
            };
        }
        else if (Application.isMobilePlatform)
        {
            // Normal mobil cihazlar
            preferredFormats = new RenderTextureFormat[]
            {
                RenderTextureFormat.ARGB32,      // Standart
                RenderTextureFormat.Default,     // Sistem default
                RenderTextureFormat.RGB565,      // Fallback basit
                RenderTextureFormat.ARGB4444,
                RenderTextureFormat.ARGBHalf
            };
        }
        else
        {
            // Desktop/Console
            preferredFormats = new RenderTextureFormat[]
            {
                RenderTextureFormat.ARGB32,
                RenderTextureFormat.Default,
                RenderTextureFormat.ARGBHalf,
                RenderTextureFormat.ARGB4444
            };
        }

        foreach (var format in preferredFormats)
        {
            if (SystemInfo.SupportsRenderTextureFormat(format))
            {
                Debug.Log($"[CharacterSnapshot] Selected RenderTextureFormat: {format} (GraphicsAPI: {SystemInfo.graphicsDeviceType})");
                return format;
            }
        }

        // Hiçbiri desteklenmiyorsa default kullan
        Debug.LogWarning("[CharacterSnapshot] No preferred RenderTextureFormat supported, using Default");
        return RenderTextureFormat.Default;
    }

    private int GetBestAntiAliasing()
    {
        // GÜVENLİK ÖNCE: Mobil cihazlarda AA sorunları yaygın, 1'den başla
        if (Application.isMobilePlatform)
        {
            // Düşük bellek cihazlar - AA kullanma
            if (SystemInfo.graphicsMemorySize < 512 ||
                SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLES2)
            {
                Debug.Log("[CharacterSnapshot] Low-end mobile: AA=1 (disabled)");
                return 1;
            }

            // Normal mobil - AA=2 dene, başarısız olursa 1
            Debug.Log("[CharacterSnapshot] Mobile platform: AA=2");
            return 2;
        }
        else
        {
            // Desktop - yüksek kalite
            Debug.Log("[CharacterSnapshot] Desktop platform: AA=4");
            return 4;
        }
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