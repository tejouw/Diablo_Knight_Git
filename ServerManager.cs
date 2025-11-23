using UnityEngine;
using System.Linq;
using System.IO;
using System.Collections;
using Fusion;
public class ServerManager : MonoBehaviour
{
    public static ServerManager Instance;

    [Header("Server Settings")]
    [SerializeField] private float statusReportInterval = 60f; // Her dakika
    [SerializeField] private long maxLogSizeBytes = 100 * 1024 * 1024; // 100MB
    [SerializeField] private float logCheckInterval = 3600f; // 1 saat

    [Header("Recovery Settings")]
    [SerializeField] private int maxReconnectAttempts = 3;
    [SerializeField] private float reconnectDelay = 5f;
    [SerializeField] private float healthCheckInterval = 30f;

    private float lastStatusReport;
    private int connectedPlayers = 0;
    private int currentReconnectAttempt = 0;
    private bool isRecovering = false;
    private Coroutine recoveryCoroutine;
    private Coroutine healthCheckCoroutine;

    // Log filtreleme için
    private static readonly string[] filteredLogKeywords = new string[]
    {
        "was not found in the", // TMP font karakterleri
        "Can't find sprite", // Sprite bulunamama hataları
        "SpriteCollection", // Sprite koleksiyon hataları
        "Character chunking error", // Character chunk hataları
        "Bangers SDF", // Bangers font hatası
        "MedievalSharp", // Medieval font hatası
        "\\u0130 was not found", // Turkish İ karakteri hatası
        "Unicode character \\u25A1", // Replacement character hatası
        "not found in collection", // Sprite collection hataları
        "Sprite '", // Sprite bulunamama hataları (Front, Back, etc.)
        "skipping (server mode)" // Server mode skip logları
    };
    
    private void Awake()
    {
        // Sadece server modunda çalış
        if (!IsServerMode())
        {
            Destroy(gameObject);
            return;
        }
        
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.wantsToQuit += OnApplicationWantsToQuit;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private bool IsServerMode()
    {
        if (Application.isEditor) return false;
        
        string[] args = System.Environment.GetCommandLineArgs();
        return System.Array.Exists(args, arg => arg == "-server" || arg == "-batchmode");
    }
    
    private void Start()
    {
        // Server build'de stack trace'leri devre dışı bırak
        Application.SetStackTraceLogType(UnityEngine.LogType.Log, StackTraceLogType.None);
        Application.SetStackTraceLogType(UnityEngine.LogType.Warning, StackTraceLogType.None);
        Application.SetStackTraceLogType(UnityEngine.LogType.Error, StackTraceLogType.None);
        Application.SetStackTraceLogType(UnityEngine.LogType.Assert, StackTraceLogType.None);
        Application.SetStackTraceLogType(UnityEngine.LogType.Exception, StackTraceLogType.ScriptOnly); // Exception'larda sadece script stack'i göster

        // Log filtreleme sistemini etkinleştir
        Application.logMessageReceived += FilterUnwantedLogs;

        Debug.Log("[SERVER] ServerManager initialized - Stack traces disabled, log filtering enabled");

        // İlk status log'u hemen at, sonra her dakika tekrarla
        InvokeRepeating(nameof(LogServerStatus), 1f, statusReportInterval);
        InvokeRepeating(nameof(RotateLogsIfNeeded), logCheckInterval, logCheckInterval);

        // Health check başlat
        healthCheckCoroutine = StartCoroutine(CloudHealthCheckRoutine());

        // KRİTİK: CloudConnectionLost event'ini dinle (Photon Cloud timeout'ları için)
        NetworkRunner.CloudConnectionLost += OnCloudConnectionLost;
    }
    
    /// <summary>
    /// İstenmeyen logları filtreler - UI/Sprite hataları server'da gereksiz
    /// KRİTİK: Exception ve Assert loglarını ASLA filtreleme! Sadece Warning ve Log.
    /// </summary>
    private void FilterUnwantedLogs(string logString, string stackTrace, UnityEngine.LogType type)
    {
        // KRİTİK: Exception ve Assert ASLA filtrelenmez!
        if (type == UnityEngine.LogType.Exception || type == UnityEngine.LogType.Assert)
        {
            // Exception'ları her zaman göster - production bug'ları kaçırma!
            return;
        }

        // Sadece Warning ve Log seviyesindeki gereksiz UI/Sprite loglarını filtrele
        if (type != UnityEngine.LogType.Warning && type != UnityEngine.LogType.Log)
        {
            return;
        }

        // Filtrelenecek keyword'leri kontrol et
        foreach (string keyword in filteredLogKeywords)
        {
            if (logString.Contains(keyword))
            {
                // Bu log'u bastır - Unity console'a yazma
                // Not: Bu sadece Warning/Log seviyesinde - Exception'lar korunuyor
                return;
            }
        }
    }

    private void LogServerStatus()
    {
        if (NetworkManager.Instance != null && NetworkManager.Instance.Runner != null)
        {
            var runner = NetworkManager.Instance.Runner;
            connectedPlayers = runner.ActivePlayers.Count();

            // Cloud bağlantı durumunu kontrol et
            bool isConnectedToCloud = runner.IsConnectedToServer;
            string connectionStatus = isConnectedToCloud ? "Connected" : "DISCONNECTED";
            string currentTime = System.DateTime.Now.ToString("HH:mm:ss");

            Debug.Log($"[SERVER STATUS] Time: {currentTime}, " +
                     $"Players: {connectedPlayers}, " +
                     $"Memory: {System.GC.GetTotalMemory(false) / 1024 / 1024}MB, " +
                     $"Cloud: {connectionStatus}");

            // Bağlantı kopmuşsa uyarı ver
            if (!isConnectedToCloud && runner.IsRunning)
            {
                Debug.LogWarning("[SERVER STATUS] WARNING: Server is running but NOT connected to Photon Cloud!");
            }
        }
        else
        {
            Debug.LogWarning("[SERVER STATUS] NetworkManager or Runner is NULL!");
        }
    }

    /// <summary>
    /// PHOTON CLOUD CONNECTION LOST EVENT HANDLER
    /// Bu callback Photon Cloud bağlantısı kesildiğinde otomatik tetiklenir
    /// Doküman referansı: satır 444-481
    /// </summary>
    private void OnCloudConnectionLost(NetworkRunner runner, Fusion.ShutdownReason reason, bool reconnecting)
    {
        Debug.LogError($"[SERVER] ===== CLOUD CONNECTION LOST =====");
        Debug.LogError($"[SERVER] Runner: {runner?.name ?? "null"}");
        Debug.LogError($"[SERVER] Reason: {reason}");
        Debug.LogError($"[SERVER] Is Photon Auto-Reconnecting: {reconnecting}");

        if (reconnecting)
        {
            // Photon otomatik yeniden bağlanmayı deniyor
            Debug.LogWarning("[SERVER] Photon is attempting automatic reconnection. Waiting 30s...");
            StartCoroutine(WaitForPhotonReconnection(runner, 30f));
        }
        else
        {
            // Kalıcı disconnect - manual recovery gerekli
            Debug.LogError("[SERVER] Permanent cloud disconnect detected! Starting manual recovery...");
            OnServerDisconnected($"CloudConnectionLost_{reason}");
        }
    }

    /// <summary>
    /// Photon'un otomatik reconnection denemesini bekler
    /// </summary>
    private IEnumerator WaitForPhotonReconnection(NetworkRunner runner, float timeout)
    {
        float elapsed = 0f;
        Debug.Log($"[SERVER] Waiting for Photon auto-reconnection (timeout: {timeout}s)...");

        while (elapsed < timeout)
        {
            // Bağlantı geri geldi mi kontrol et
            if (runner != null && runner.IsRunning && runner.IsConnectedToServer)
            {
                Debug.Log($"[SERVER] ✓ Photon auto-reconnection SUCCESSFUL after {elapsed:F1}s!");
                yield break; // ✓ Coroutine'de return yerine yield break
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Timeout - Photon başarısız oldu, manual recovery başlat
        Debug.LogError($"[SERVER] ✗ Photon auto-reconnection FAILED after {timeout}s timeout!");
        Debug.LogError("[SERVER] Starting manual recovery process...");
        OnServerDisconnected("PhotonAutoReconnectTimeout");
    }

    /// <summary>
    /// NetworkManager tarafından çağrılır when server disconnects from Photon Cloud
    /// </summary>
    public void OnServerDisconnected(string reason)
    {
        if (isRecovering)
        {
            Debug.Log($"[SERVER] Already in recovery mode. Ignoring duplicate disconnect notification.");
            return;
        }

        Debug.LogError($"[SERVER] ===== SERVER DISCONNECTED =====");
        Debug.LogError($"[SERVER] Reason: {reason}");
        Debug.LogError($"[SERVER] Starting recovery process...");

        // Recovery başlat
        if (recoveryCoroutine != null)
        {
            StopCoroutine(recoveryCoroutine);
        }
        recoveryCoroutine = StartCoroutine(ServerRecoveryRoutine(reason));
    }

    /// <summary>
    /// AAA Kalitesinde Server Recovery Routine
    /// Doküman: satır 10-11 - NetworkRunner sadece bir kez kullanılabilir
    /// Doküman: satır 914-943 - Exponential backoff with jitter
    /// </summary>
    private IEnumerator ServerRecoveryRoutine(string disconnectReason)
    {
        isRecovering = true;
        currentReconnectAttempt = 0;

        Debug.Log("[SERVER RECOVERY] ===== STARTING RECOVERY PROCESS =====");
        Debug.Log($"[SERVER RECOVERY] Disconnect Reason: {disconnectReason}");
        Debug.Log($"[SERVER RECOVERY] Max Attempts: {maxReconnectAttempts}");

        while (currentReconnectAttempt < maxReconnectAttempts)
        {
            currentReconnectAttempt++;
            Debug.Log($"[SERVER RECOVERY] ────────────────────────────────────");
            Debug.Log($"[SERVER RECOVERY] Attempt {currentReconnectAttempt}/{maxReconnectAttempts}...");

            // KRİTİK: Eski NetworkRunner'ı TAMAMEN YOK ET
            // Doküman: "NetworkRunner yalnızca bir kez kullanılabilir"
            if (NetworkManager.Instance != null && NetworkManager.Instance.Runner != null)
            {
                var oldRunner = NetworkManager.Instance.Runner;
                Debug.Log("[SERVER RECOVERY] Destroying old NetworkRunner instance...");

                // Proper shutdown - destroyGameObject ile
                if (oldRunner != null && oldRunner.IsRunning)
                {
                    Debug.Log("[SERVER RECOVERY] Shutting down runner...");
                    oldRunner.Shutdown(destroyGameObject: false);
                }

                yield return new WaitForSeconds(0.5f);

                // GameObject'i destroy et
                if (oldRunner != null && oldRunner.gameObject != null)
                {
                    Debug.Log("[SERVER RECOVERY] Destroying runner GameObject...");
                    Destroy(oldRunner.gameObject);
                }

                yield return new WaitForSeconds(0.5f);
            }

            // KRİTİK: YENİ NetworkRunner Instance Oluştur
            Debug.Log("[SERVER RECOVERY] Creating NEW NetworkRunner instance...");
            if (NetworkManager.Instance != null)
            {
                // NetworkManager'ı yeniden başlat
                NetworkManager.Instance.ResetConnection();

                // Kısa bekleme
                yield return new WaitForSeconds(0.5f);
            }
            else
            {
                Debug.LogError("[SERVER RECOVERY] NetworkManager is null! Fatal error.");
                break;
            }

            // Exponential backoff with jitter (Doküman: satır 914-943)
            float baseDelay = reconnectDelay;
            float exponentialDelay = baseDelay * Mathf.Pow(2, currentReconnectAttempt - 1);
            float jitter = UnityEngine.Random.Range(-0.5f, 0.5f) * exponentialDelay;
            float totalDelay = Mathf.Min(exponentialDelay + jitter, 30f); // Max 30s

            Debug.Log($"[SERVER RECOVERY] Waiting {totalDelay:F1}s before reconnection (base={baseDelay}s, exp={exponentialDelay:F1}s, jitter={jitter:F1}s)...");
            yield return new WaitForSeconds(totalDelay);

            // Yeniden bağlanmayı dene
            Debug.Log("[SERVER RECOVERY] Attempting to reconnect to Photon Cloud...");
            if (NetworkManager.Instance != null)
            {
                NetworkManager.Instance.ConnectToServer();
            }
            else
            {
                Debug.LogError("[SERVER RECOVERY] NetworkManager is null! Cannot reconnect.");
                break;
            }

            // Bağlantının kurulmasını bekle (progressive check)
            Debug.Log("[SERVER RECOVERY] Waiting for connection to establish...");
            float waitTime = 0f;
            float maxWaitTime = 10f;
            bool connectionSuccess = false;

            while (waitTime < maxWaitTime)
            {
                if (NetworkManager.Instance != null &&
                    NetworkManager.Instance.Runner != null &&
                    NetworkManager.Instance.Runner.IsRunning &&
                    NetworkManager.Instance.Runner.IsConnectedToServer)
                {
                    connectionSuccess = true;
                    break;
                }

                yield return new WaitForSeconds(0.5f);
                waitTime += 0.5f;

                if (waitTime % 2f == 0)
                {
                    Debug.Log($"[SERVER RECOVERY] Still waiting... ({waitTime:F0}s / {maxWaitTime:F0}s)");
                }
            }

            if (connectionSuccess)
            {
                Debug.Log($"[SERVER RECOVERY] ═════════════════════════════════════");
                Debug.Log($"[SERVER RECOVERY] ✓✓✓ SUCCESS! ✓✓✓");
                Debug.Log($"[SERVER RECOVERY] Reconnected on attempt {currentReconnectAttempt}/{maxReconnectAttempts}");
                Debug.Log($"[SERVER RECOVERY] Connection established after {waitTime:F1}s");
                Debug.Log($"[SERVER RECOVERY] ═════════════════════════════════════");

                currentReconnectAttempt = 0;
                isRecovering = false;
                yield break;
            }

            Debug.LogWarning($"[SERVER RECOVERY] ✗ Attempt {currentReconnectAttempt} FAILED (timeout after {maxWaitTime}s)");
        }

        // Tüm denemeler başarısız oldu
        Debug.LogError($"[SERVER RECOVERY] ═════════════════════════════════════");
        Debug.LogError($"[SERVER RECOVERY] ✗✗✗ TOTAL FAILURE ✗✗✗");
        Debug.LogError($"[SERVER RECOVERY] All {maxReconnectAttempts} attempts exhausted");
        Debug.LogError($"[SERVER RECOVERY] Original disconnect reason: {disconnectReason}");
        Debug.LogError($"[SERVER RECOVERY] Shutting down application...");
        Debug.LogError($"[SERVER RECOVERY] External process manager (systemd/supervisor) should restart the server");
        Debug.LogError($"[SERVER RECOVERY] ═════════════════════════════════════");

        isRecovering = false;

        // Log son durumu
        yield return new WaitForSeconds(1f);
        Debug.LogError("[SERVER RECOVERY] Initiating application quit...");

        // Uygulama kapatılıyor - systemd/supervisor yeniden başlatacak
        Application.Quit();
    }

    private IEnumerator CloudHealthCheckRoutine()
    {
        // İlk bağlantının kurulmasını bekle
        yield return new WaitForSeconds(10f);

        while (true)
        {
            yield return new WaitForSeconds(healthCheckInterval);

            if (isRecovering)
            {
                // Recovery sırasında health check yapma
                continue;
            }

            if (NetworkManager.Instance != null && NetworkManager.Instance.Runner != null)
            {
                var runner = NetworkManager.Instance.Runner;

                // Runner çalışıyor ama cloud'a bağlı değilse
                if (runner.IsRunning && !runner.IsConnectedToServer)
                {
                    Debug.LogError("[SERVER HEALTH CHECK] Detected disconnected state! Runner is running but not connected to cloud.");
                    OnServerDisconnected("HealthCheck_CloudDisconnected");
                }
            }
            else
            {
                Debug.LogWarning("[SERVER HEALTH CHECK] NetworkManager or Runner is null!");
            }
        }
    }

    private bool OnApplicationWantsToQuit()
    {
        Debug.Log("[SERVER] Shutting down gracefully...");
        
        if (NetworkManager.Instance != null && NetworkManager.Instance.Runner != null)
        {
            NetworkManager.Instance.Runner.Shutdown();
        }
        
        return true;
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        // Server hiçbir zaman pause olmamalı
        if (pauseStatus)
        {
            Debug.LogWarning("[SERVER] Application pause detected - this should not happen on dedicated server!");
        }
    }

    private void RotateLogsIfNeeded()
    {
        try
        {
            // Unity log dosyasının yolunu bul
            string logPath = Path.Combine(Application.persistentDataPath, "Player.log");

            // Dosya yoksa çık
            if (!File.Exists(logPath))
            {
                return;
            }

            // Dosya boyutunu kontrol et
            FileInfo fileInfo = new FileInfo(logPath);
            if (fileInfo.Length > maxLogSizeBytes)
            {
                // Eski log'u yedekle
                string backupPath = logPath + "." + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".old";
                File.Move(logPath, backupPath);

                Debug.Log($"[SERVER] Log rotated: {fileInfo.Length / 1024 / 1024}MB -> {backupPath}");

                // Eski backup'ları temizle (son 3 backup'ı tut)
                CleanOldLogBackups(Path.GetDirectoryName(logPath));
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SERVER] Log rotation error: {e.Message}");
        }
    }

    private void CleanOldLogBackups(string directory)
    {
        try
        {
            var backups = Directory.GetFiles(directory, "Player.log.*.old")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Skip(3) // Son 3'ü sakla
                .ToList();

            foreach (var backup in backups)
            {
                backup.Delete();
                Debug.Log($"[SERVER] Deleted old log backup: {backup.Name}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SERVER] Backup cleanup error: {e.Message}");
        }
    }

    private void OnDestroy()
    {
        // Event cleanup - KRİTİK: Memory leak önleme
        NetworkRunner.CloudConnectionLost -= OnCloudConnectionLost;
        Application.logMessageReceived -= FilterUnwantedLogs;

        // InvokeRepeating cleanup
        CancelInvoke(nameof(LogServerStatus));
        CancelInvoke(nameof(RotateLogsIfNeeded));

        // Coroutine cleanup
        if (recoveryCoroutine != null)
        {
            StopCoroutine(recoveryCoroutine);
        }
        if (healthCheckCoroutine != null)
        {
            StopCoroutine(healthCheckCoroutine);
        }
    }
}