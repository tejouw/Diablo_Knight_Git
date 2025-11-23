// Path: Assets/Game/Scripts/RaceSelectionManager.cs

using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;

public class RaceSelectionManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject raceSelectionPanel;
    [SerializeField] private Button humanButton;
    [SerializeField] private Button goblinButton;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private Text statusText;

    [Header("Settings")]
    private string playerNickname;
    private bool isSelecting = false;

    private void Start()
    {
        // Panel başlangıçta kapalı olsun
        if (raceSelectionPanel != null)
        {
            raceSelectionPanel.SetActive(false);
        }

        // Button event'lerini ayarla
        if (humanButton != null)
        {
            humanButton.onClick.AddListener(() => OnRaceSelected(PlayerRace.Human));
        }

        if (goblinButton != null)
        {
            goblinButton.onClick.AddListener(() => OnRaceSelected(PlayerRace.Goblin));
        }

        // Loading'i gizle
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }
    }
public async void CheckPlayerCharacterAndShowRaceSelection(string nickname)
{
    playerNickname = nickname;
    
    if (statusText != null)
    {
        statusText.text = "Karakter kontrol ediliyor...";
    }
    
    try
    {
        // Firebase'den karakter bilgisi kontrol et
        bool hasCharacter = await CheckPlayerHasCharacter(nickname);
        
        if (!hasCharacter)
        {
            // Karakter yoksa ırk seçimi göster
            ShowRaceSelection(nickname);
            
            // LoginPanel'i kapat
            HideLoginPanel();
        }
        else
        {
            // Karakter varsa - PROFESSIONAL MMORPG PATTERN: Pre-load data before entering game
            if (statusText != null)
            {
                statusText.text = "Oyuncu verisi yükleniyor...";
            }

            // Ensure PlayerDataSession exists and persists across scenes
            if (PlayerDataSession.Instance == null)
            {
                GameObject sessionObj = new GameObject("PlayerDataSession");
                sessionObj.AddComponent<PlayerDataSession>();
                DontDestroyOnLoad(sessionObj); // CRITICAL: Persist across scene changes
            }

            // Load player data from Firebase into session cache
            bool success = await PlayerDataSession.Instance.LoadPlayerDataFromFirebase(nickname);

            // ALSO pre-load character appearance data
            var characterData = await CharacterDataManager.LoadCharacterData(nickname);

            if (!success)
            {
                Debug.LogError("[RaceSelectionManager] Failed to load player data, cannot enter game");
                if (statusText != null)
                {
                    statusText.text = "Veri yüklenemedi. Lütfen tekrar deneyin.";
                }
                return;
            }

            if (statusText != null)
            {
                statusText.text = "Oyuna giriş yapılıyor...";
            }

            // Data ready, now load the game
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainGame");
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[RaceSelectionManager] Karakter kontrol hatası: {e.Message}");
        
        // Hata durumunda ırk seçimi göster
        ShowRaceSelection(nickname);
        HideLoginPanel();
    }
}

// Yeni metod - Karakter varlığını kontrol et
private async Task<bool> CheckPlayerHasCharacter(string nickname)
{
    if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
    {
        return false;
    }
    
    try
    {
        var snapshot = await FirebaseManager.Instance.LoadDataFromPath($"players/{nickname}/race");
        return snapshot != null && snapshot.Exists;
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[RaceSelectionManager] Firebase karakter kontrol hatası: {e.Message}");
        return false;
    }
}

// Yeni metod - LoginPanel'i gizle
private void HideLoginPanel()
{
    // LoginPanel'i bul ve gizle
    GameObject loginPanel = GameObject.Find("LoginPanel");
    if (loginPanel != null)
    {
        loginPanel.SetActive(false);
    }
    else
    {
        // UIManager üzerinden de deneyebiliriz
        UIManager uiManager = UIManager.Instance;
        if (uiManager != null)
        {
            // UIManager'da HideLoginPanel metodu varsa çağır
            var methodInfo = uiManager.GetType().GetMethod("HideLoginPanel");
            if (methodInfo != null)
            {
                methodInfo.Invoke(uiManager, null);
            }
        }
    }
}

// RaceSelectionManager.cs - ShowRaceSelection metodunu güncelle
public void ShowRaceSelection(string nickname)
{
    // Validation ekle
    if (string.IsNullOrEmpty(nickname))
    {
        Debug.LogError("[RaceSelectionManager] Invalid nickname for race selection!");
        return;
    }
    
    playerNickname = nickname;
    
    if (raceSelectionPanel != null)
    {
        raceSelectionPanel.SetActive(true);
    }
    else
    {
        Debug.LogError("[RaceSelectionManager] raceSelectionPanel referansı null!");
        return;
    }
    
    SetButtonsInteractable(true);
    
    if (statusText != null)
    {
        statusText.text = "Irkınızı seçin";
    }
}

    public void HideRaceSelection()
    {
        raceSelectionPanel.SetActive(false);
    }

    private async void OnRaceSelected(PlayerRace selectedRace)
    {
        if (isSelecting) return;
        
        isSelecting = true;
        SetButtonsInteractable(false);
        
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(true);
        }

        if (statusText != null)
        {
            statusText.text = "Irk kaydediliyor...";
        }

        try
        {
            // Irkı kaydet
            await SaveRaceSelection(selectedRace);
            
            if (statusText != null)
            {
                statusText.text = "Karakter oluşturma ekranına yönlendiriliyor...";
            }

            // Karakter oluşturma sahnesine git
            string targetScene = selectedRace == PlayerRace.Human ? "HumanCharacterCreation" : "GoblinCharacterCreation";
            
            // Kısa bir bekleme
            await Task.Delay(500);
            
            UnityEngine.SceneManagement.SceneManager.LoadScene(targetScene);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Irk kaydetme hatası: {e.Message}");
            
            if (statusText != null)
            {
                statusText.text = "Hata oluştu, tekrar deneyin!";
            }
            
            SetButtonsInteractable(true);
        }
        finally
        {
            if (loadingIndicator != null)
            {
                loadingIndicator.SetActive(false);
            }
            
            isSelecting = false;
        }
    }

    private async Task SaveRaceSelection(PlayerRace race)
    {
        // PlayerPrefs'e kaydet
        string raceKey = "PlayerRace_" + playerNickname;
        PlayerPrefs.SetString(raceKey, race.ToString());
        PlayerPrefs.Save();

        // Firebase'e kaydet
        if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
        {
            try
            {
                await FirebaseManager.Instance.SavePlayerRace(playerNickname, race);
            }
            catch (System.Exception )
            {
                // Firebase hatası olsa bile devam et
            }
        }

    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (humanButton != null)
        {
            humanButton.interactable = interactable;
        }

        if (goblinButton != null)
        {
            goblinButton.interactable = interactable;
        }
    }
}