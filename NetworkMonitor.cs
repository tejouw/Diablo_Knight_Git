// Path: Assets/Game/Scripts/NetworkMonitor.cs

using UnityEngine;
using TMPro;
using Fusion;
using System.Linq;

public class NetworkMonitor : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI networkInfoText;
    
    [Header("Settings")]
    [SerializeField] private float updateInterval = 1f; // Saniyede bir güncelle
    [SerializeField] private bool showFPSInfo = true;
    [SerializeField] private bool showNetworkInfo = true;
    
    private NetworkRunner runner;
    private float lastUpdateTime;
    
    // FPS Tracking
    private float deltaTime = 0.0f;
    private float fpsUpdateTimer = 0f;
    private float currentFPS = 0f;
    
    private void Awake()
    {
        // Performance ayarları - FPSTestScript'den taşındı
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        
        // Çözünürlük ayarı (isteğe bağlı - test için)
        // Screen.SetResolution(1200, 540, true);
    }
    
    private void Start()
    {
        if (networkInfoText == null)
        {
            networkInfoText = GetComponent<TextMeshProUGUI>();
        }
        
        if (networkInfoText == null)
        {
            Debug.LogError("[NetworkMonitor] TextMeshProUGUI component bulunamadı!");
            gameObject.SetActive(false);
            return;
        }
        
        // NetworkManager'dan runner referansını al
        StartCoroutine(WaitForNetworkManager());
    }
    
    private System.Collections.IEnumerator WaitForNetworkManager()
    {
        while (NetworkManager.Instance == null || NetworkManager.Instance.Runner == null)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        runner = NetworkManager.Instance.Runner;
    }
    
    private void Update()
    {
        // FPS hesaplama - FPSTestScript'den taşındı
        if (showFPSInfo)
        {
            deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
            
            fpsUpdateTimer += Time.unscaledDeltaTime;
            if (fpsUpdateTimer >= 1f)
            {
                currentFPS = 1.0f / deltaTime;
                fpsUpdateTimer = 0f;
            }
        }
        
        // Network ve FPS bilgilerini güncelle
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            UpdateDisplayInfo();
            lastUpdateTime = Time.time;
        }
    }
    
private void UpdateDisplayInfo()
{
    string info = "";
    
    // FPS Bilgileri
    if (showFPSInfo)
    {
        float msec = deltaTime * 1000.0f;
        float realTimeFPS = 1.0f / deltaTime; // ANLIK FPS - gerçek zamanlı hesaplanan
        
        info += "=== PERFORMANCE ===\n";
        
        // FPS renk kodlaması
        string fpsColor = realTimeFPS >= 55 ? "green" : realTimeFPS >= 30 ? "yellow" : "red";
        
        info += $"<color={fpsColor}>Current FPS: {realTimeFPS:0.} ({msec:0.0}ms)</color>\n";
        info += $"Resolution: {Screen.width}x{Screen.height}\n";
        info += "\n";
    }
    
    // Network Bilgileri (aynı kalıyor)
    if (showNetworkInfo)
    {
        info += "=== NETWORK ===\n";
        
        if (runner == null || !runner.IsRunning)
        {
            info += "<color=red>Status: Disconnected</color>\n";
        }
        else
        {
            string connectionColor = runner.IsConnectedToServer ? "green" : "red";
            string connectionStatus = runner.IsConnectedToServer ? "Connected" : "Disconnected";
            string gameMode = runner.GameMode.ToString();
            info += $"<color={connectionColor}>Status: {connectionStatus}</color>\n";
            info += $"Mode: {gameMode}\n";
            
            int playerCount = runner.ActivePlayers.Count();
            info += $"Players: {playerCount}\n";
            
            if (runner.IsClient)
            {
                float ping = (float)(runner.GetPlayerRtt(runner.LocalPlayer) * 1000.0);
                string pingColor = ping < 50 ? "green" : ping < 100 ? "yellow" : "red";
                info += $"<color={pingColor}>Ping: {ping:F0}ms</color>\n";
            }
            else
            {
                info += "Ping: Server\n";
            }
        }
    }
    
    networkInfoText.text = info;
}
    
    private void OnValidate()
    {
        if (networkInfoText == null)
        {
            networkInfoText = GetComponent<TextMeshProUGUI>();
        }
    }
}