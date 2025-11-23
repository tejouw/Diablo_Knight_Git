using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

/// <summary>
/// Professional MMORPG Pattern: Pre-spawn data session management
/// Firebase data yüklenir ve cache'lenir, spawn anında hazır durumda olur
/// Bu pattern tüm büyük MMO'larda kullanılır (WoW, FF14, ESO)
/// </summary>
public class PlayerDataSession : MonoBehaviour
{
    public static PlayerDataSession Instance { get; private set; }

    // Session state
    private bool isDataReady = false;
    private bool isLoading = false;
    private Dictionary<string, object> cachedStats = null;
    private string cachedNickname = null;
    private float sessionStartTime = 0f;

    // Configuration
    private const int MAX_FIREBASE_RETRIES = 10;
    private const int RETRY_DELAY_MS = 300; // Reduced from 2000ms
    private const float SESSION_TIMEOUT = 30f; // 30 saniye max

    // Events
    public event Action OnDataLoadStarted;
    public event Action OnDataLoadCompleted;
    public event Action<string> OnDataLoadFailed;

    // Public properties
    public bool IsDataReady => isDataReady;
    public bool IsLoading => isLoading;
    public string CachedNickname => cachedNickname;

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

    /// <summary>
    /// Firebase'den player data'yı yükle ve cache'le
    /// Bu metod Character Select/Lobby'de çağrılmalı
    /// </summary>
    public async Task<bool> LoadPlayerDataFromFirebase(string nickname)
    {
        if (isLoading)
        {
            Debug.LogWarning("[PlayerDataSession] Already loading data, please wait");
            return false;
        }

        if (string.IsNullOrEmpty(nickname))
        {
            Debug.LogError("[PlayerDataSession] Cannot load data with empty nickname");
            OnDataLoadFailed?.Invoke("Nickname is required");
            return false;
        }

        isLoading = true;
        isDataReady = false;
        sessionStartTime = Time.time;
        cachedNickname = nickname;

        OnDataLoadStarted?.Invoke();

        try
        {
            // Step 1: Wait for Firebase initialization with optimized retry
            int retryCount = 0;
            while (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
            {
                if (retryCount >= MAX_FIREBASE_RETRIES)
                {
                    string error = $"Firebase connection failed after {MAX_FIREBASE_RETRIES} retries";
                    Debug.LogError($"[PlayerDataSession] {error}");
                    OnDataLoadFailed?.Invoke("Sunucuya bağlanılamadı. Lütfen internet bağlantınızı kontrol edin.");
                    isLoading = false;
                    return false;
                }

                // Session timeout check
                if (Time.time - sessionStartTime > SESSION_TIMEOUT)
                {
                    Debug.LogError("[PlayerDataSession] Session timeout exceeded");
                    OnDataLoadFailed?.Invoke("Bağlantı zaman aşımına uğradı. Lütfen tekrar deneyin.");
                    isLoading = false;
                    return false;
                }

                await Task.Delay(RETRY_DELAY_MS);
                retryCount++;
            }

            // Step 2: Load player data from Firebase
            cachedStats = await FirebaseManager.Instance.LoadUserData(nickname);

            if (cachedStats == null)
            {
                // New player - this is OK, will use default stats
                cachedStats = new Dictionary<string, object>();
            }

            // Step 3: Mark session as ready
            isDataReady = true;
            isLoading = false;

            OnDataLoadCompleted?.Invoke();

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerDataSession] Critical error during data load: {e.Message}\n{e.StackTrace}");
            OnDataLoadFailed?.Invoke("Veri yüklenirken hata oluştu. Lütfen tekrar deneyin.");
            isLoading = false;
            isDataReady = false;
            return false;
        }
    }

    /// <summary>
    /// Cache'lenmiş stats'ı al
    /// PlayerStats.Spawned() içinden çağrılır
    /// </summary>
    public Dictionary<string, object> GetCachedStats()
    {
        if (!isDataReady)
        {
            Debug.LogError("[PlayerDataSession] GetCachedStats called but data is not ready!");
            return null;
        }

        return cachedStats;
    }

    /// <summary>
    /// Session'ı temizle (logout, disconnect durumlarında)
    /// </summary>
    public void ClearSession()
    {
        isDataReady = false;
        isLoading = false;
        cachedStats = null;
        cachedNickname = null;
        sessionStartTime = 0f;
    }

    /// <summary>
    /// Data validation - spawn öncesi kontrol için
    /// </summary>
    public bool ValidateSessionForSpawn()
    {
        if (!isDataReady)
        {
            Debug.LogError("[PlayerDataSession] Session data not ready for spawn");
            return false;
        }

        if (string.IsNullOrEmpty(cachedNickname))
        {
            Debug.LogError("[PlayerDataSession] Nickname is missing");
            return false;
        }

        if (cachedStats == null)
        {
            Debug.LogError("[PlayerDataSession] Stats cache is null");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Debug info
    /// </summary>
    public string GetSessionInfo()
    {
        return $"Ready: {isDataReady}, Loading: {isLoading}, Nickname: {cachedNickname ?? "None"}, " +
               $"Stats Cached: {cachedStats != null}, Session Age: {(isDataReady ? (Time.time - sessionStartTime).ToString("F1") : "N/A")}s";
    }

    private void OnDestroy()
    {
        ClearSession();
    }
}
