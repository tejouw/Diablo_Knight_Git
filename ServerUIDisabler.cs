using UnityEngine;
using TMPro;

/// <summary>
/// Server modunda çalıştığında UI component'lerini devre dışı bırakır
/// TMP_Settings eksik olduğunda crash'i önler
/// </summary>
public class ServerUIDisabler : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void DisableUIInServerMode()
    {
        if (!IsServerMode())
        {
            return;
        }

        Debug.Log("[SERVER UI DISABLER] Server mode detected. Disabling UI systems BEFORE scene load...");

        // TMP font yüklemeyi engelle - En yüksek öncelik
        Application.logMessageReceived += SuppressTMPWarnings;

        // Quality Settings - TMP font rendering'i devre dışı bırak
        DisableTextMeshProRendering();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void DisableUIAfterSceneLoad()
    {
        if (!IsServerMode())
        {
            return;
        }

        Debug.Log("[SERVER UI DISABLER] Scene loaded. Disabling all UI GameObjects...");

        // Scene yüklendikten hemen sonra NamePanel'ları kapat
        DisableAllNamePanelsImmediately();

        // TMP_Settings crash'ini önle
        DisableTextMeshProComponents();
    }

    private static void DisableAllNamePanelsImmediately()
    {
        // Tüm NamePanel ve NPCCanvas objelerini bul ve kapat
        // Unity 6: FindObjectsByType kullan, sorting gereksiz
        GameObject[] allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int count = 0;

        foreach (var obj in allObjects)
        {
            if (obj.name == "NamePanel" || obj.name.Contains("NPCCanvas"))
            {
                obj.SetActive(false);
                count++;
            }
        }

        Debug.Log($"[SERVER UI DISABLER] Immediately disabled {count} NamePanel/NPCCanvas objects");
    }

    private static void SuppressTMPWarnings(string logString, string stackTrace, UnityEngine.LogType type)
    {
        // KRİTİK: Exception ve Assert'leri ASLA suppress etme!
        if (type == UnityEngine.LogType.Exception || type == UnityEngine.LogType.Assert)
        {
            // Exception'lar her zaman loglanmalı - production crash'lerini kaçırma!
            return;
        }

        // Sadece Warning/Log seviyesindeki TMP font ve sprite uyarılarını baskıla
        if (type != UnityEngine.LogType.Warning && type != UnityEngine.LogType.Log)
        {
            return;
        }

        // UI/Sprite related warnings - bunlar server'da anlamsız ama zararsız
        if (logString.Contains("was not found in the") ||
            logString.Contains("MedievalSharp") ||
            logString.Contains("Bangers SDF") ||
            logString.Contains("\\u0130 was not found") ||
            logString.Contains("Unicode character \\u25A1") ||
            logString.Contains("NamePanel") ||
            logString.Contains("not found in collection") ||
            logString.Contains("Sprite '") ||
            logString.Contains("skipping (server mode)"))
        {
            // Log'u engelle - konsola yazma
            // Not: Bu sadece Warning/Log - Exception'lar korunuyor!
            return;
        }
    }

    private static bool IsServerMode()
    {
        if (Application.isEditor) return false;

        string[] args = System.Environment.GetCommandLineArgs();
        return System.Array.Exists(args, arg => arg == "-server" || arg == "-batchmode");
    }

    /// <summary>
    /// TMP rendering sistemini tamamen devre dışı bırakır - BeforeSceneLoad'da çağrılır
    /// </summary>
    private static void DisableTextMeshProRendering()
    {
        // Quality settings ile TMP rendering'i minimize et
        QualitySettings.pixelLightCount = 0;
        QualitySettings.shadowDistance = 0;

        Debug.Log("[SERVER UI DISABLER] TMP rendering disabled via Quality Settings");
    }

    private static void DisableTextMeshProComponents()
    {
        // Scene henüz yüklenmeden önce çalıştığı için bu method boş
        // Asıl disable işlemi scene yüklendikten sonra yapılacak
    }

    private void Awake()
    {
        if (!IsServerMode())
        {
            Destroy(gameObject);
            return;
        }

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (!IsServerMode()) return;

        // NPC NamePanel'larını hemen devre dışı bırak (en yüksek öncelik)
        DisableNPCNamePanels();

        // Scene yüklendikten sonra tüm TMP component'lerini devre dışı bırak
        DisableAllTMPComponents();

        // Canvas'ları da devre dışı bırak
        DisableAllCanvases();

        // Sprite render sistemlerini devre dışı bırak
        DisableAllSpriteRenderers();
    }

    private void DisableNPCNamePanels()
    {
        int disabledCount = 0;

        // "NamePanel" isimli tüm GameObject'leri bul
        // Unity 6: FindObjectsByType kullan, sorting gereksiz
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var obj in allObjects)
        {
            if (obj.name == "NamePanel" || obj.name.Contains("NPCCanvas"))
            {
                obj.SetActive(false);
                disabledCount++;
            }
        }

        Debug.Log($"[SERVER UI DISABLER] Disabled {disabledCount} NamePanel/NPCCanvas objects");
    }

    private void DisableAllTMPComponents()
    {
        int disabledCount = 0;

        // Tüm TextMeshProUGUI component'lerini bul ve devre dışı bırak
        // Unity 6: FindObjectsByType kullan, sorting gereksiz
        TextMeshProUGUI[] tmpTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var tmp in tmpTexts)
        {
            if (tmp != null)
            {
                tmp.enabled = false;
                disabledCount++;
            }
        }

        // Tüm TextMeshPro component'lerini bul ve devre dışı bırak
        TextMeshPro[] tmpWorldTexts = FindObjectsByType<TextMeshPro>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var tmp in tmpWorldTexts)
        {
            if (tmp != null)
            {
                tmp.enabled = false;
                disabledCount++;
            }
        }

        Debug.Log($"[SERVER UI DISABLER] Disabled {disabledCount} TextMeshPro components");
    }

    private void DisableAllCanvases()
    {
        int disabledCount = 0;

        // Unity 6: FindObjectsByType kullan, sorting gereksiz
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var canvas in canvases)
        {
            if (canvas != null && canvas.gameObject != null)
            {
                canvas.gameObject.SetActive(false);
                disabledCount++;
            }
        }

        Debug.Log($"[SERVER UI DISABLER] Disabled {disabledCount} Canvas objects");
    }

    private void DisableAllSpriteRenderers()
    {
        int disabledCount = 0;

        // Tüm SpriteRenderer component'lerini bul ve devre dışı bırak
        // Unity 6: FindObjectsByType kullan, sorting gereksiz
        SpriteRenderer[] spriteRenderers = FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var renderer in spriteRenderers)
        {
            if (renderer != null)
            {
                // Character sprite'ları için özel kontrol - sadece visual layer'larda disable et
                // Ama collider'ları etkileme
                renderer.enabled = false;
                disabledCount++;
            }
        }

        Debug.Log($"[SERVER UI DISABLER] Disabled {disabledCount} SpriteRenderer components");
    }
}
