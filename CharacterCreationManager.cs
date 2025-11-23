// Path: Assets/Game/Scripts/CharacterCreationManager.cs

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;
using Fusion; // using Photon.Pun; yerine
using Assets.HeroEditor4D.Common.Scripts.EditorScripts;
using Fusion.Sockets; 

public class CharacterCreationManager : NetworkBehaviour
{
    public static CharacterCreationManager Instance;

    [Header("Character Creation")]
    [SerializeField] private CharacterEditor characterEditor;
    [SerializeField] private GameObject characterCreationUI;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button randomizeButton;
    [SerializeField] private string mainGameScene = "MainGame";

    [Header("UI Elements")]
    [SerializeField] private Text statusText;
    [SerializeField] private Button backButton;

    private bool hasCharacter = false;
    private string playerNickname;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

// Start metodunda ekleme:
    private void Start()
    {
        // Loading manager varsa ve aktifse iptal et
        if (LoadingManager.Instance != null && LoadingManager.Instance.loadingPanel.activeInHierarchy)
        {
            LoadingManager.Instance.CancelLoading();
        }
        
        loadingPanel.SetActive(true);
        characterCreationUI.SetActive(false);

        // Butonlara event listener ekleme
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(SaveCharacter);
        }

        if (randomizeButton != null)
        {
            randomizeButton.onClick.AddListener(RandomizeCharacter);
        }

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackButtonClicked);
        }

        // Kullanıcı adını al - NetworkRunner kullanarak
        if (Runner != null && Runner.LocalPlayer != null)
        {
            playerNickname = Runner.LocalPlayer.ToString(); // Fusion'da player nickname alımı
        }
        
        if (string.IsNullOrEmpty(playerNickname))
        {
            playerNickname = PlayerPrefs.GetString("CurrentUser", "");
            if (string.IsNullOrEmpty(playerNickname))
            {
                playerNickname = "Player_" + Random.Range(1000, 9999);
            }
        }

        // Kullanıcının daha önce karakteri var mı kontrol et
        CheckExistingCharacter();
    }

private async void CheckExistingCharacter()
{
    try
    {
        // Firebase'in hazır olmasını bekle
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            ShowCharacterCreation();
            return;
        }

        // CharacterDataManager ile kontrol et
        bool hasCharacter = await CharacterDataManager.HasCharacterData(playerNickname);
        
        if (hasCharacter)
        {
            hasCharacter = true;
            LoadMainGame();
        }
        else
        {
            ShowCharacterCreation();
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Karakter kontrolü sırasında hata: {e.Message}");
        ShowCharacterCreation();
    }
}

    private void ShowCharacterCreation()
    {
        loadingPanel.SetActive(false);
        characterCreationUI.SetActive(true);
        
        // Editörü hazırla
        if (characterEditor != null)
        {
            // İhtiyaç duyulursa burada editörü konfigüre et
        }
    }

    private void RandomizeCharacter()
    {
        if (characterEditor != null)
        {
            characterEditor.Randomize();
        }
    }

private async void SaveCharacter()
{
    bool loadingCompleted = false;
    string characterJson = "";
    PlayerRace race = PlayerRace.Human;
    
    try
    {
        if (characterEditor == null || characterEditor.Character == null)
        {
            Debug.LogError("Character Editor veya Character null!");
            return;
        }

        loadingPanel.SetActive(true);
        statusText.text = "Karakter kaydediliyor...";

        // Karakteri JSON olarak al
        characterJson = characterEditor.Character.ToJson();

        // Kullanıcı adını al
        if (Runner != null && Runner.LocalPlayer != null)
        {
            playerNickname = Runner.LocalPlayer.ToString();
        }
        
        if (string.IsNullOrEmpty(playerNickname))
        {
            playerNickname = PlayerPrefs.GetString("CurrentUser", "");
            if (string.IsNullOrEmpty(playerNickname))
            {
                playerNickname = "Player_" + UnityEngine.Random.Range(1000, 9999);
            }
        }

        // Irkı sahne ismine göre belirle
        race = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Human")
            ? PlayerRace.Human
            : PlayerRace.Goblin;

        // CharacterDataManager ile kaydet
        bool saveSuccess = await CharacterDataManager.SaveCharacterData(playerNickname, race, characterJson);
        
        if (saveSuccess)
        {
            statusText.text = "Kaydedildi! Oyun yükleniyor...";
            loadingCompleted = true;
        }
        else
        {
            statusText.text = "Kaydetme hatası!";
            loadingPanel.SetActive(false);
            return;
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Karakter kaydı sırasında hata: {e.Message}");
        loadingPanel.SetActive(false);
        statusText.text = "Karakter kaydedilemedi!";
        return;
    }
    
    if (loadingCompleted)
    {
        // NetworkRunner bağlantısını kapat
        if (Runner != null && Runner.IsConnectedToServer)
        {
            await Runner.Shutdown();
            
            int attempts = 0;
            while (Runner != null && Runner.IsConnectedToServer && attempts < 30)
            {
                await Task.Delay(100);
                attempts++;
            }
        }

        // AutoConnect flag'i ayarla
        PlayerPrefs.SetString("AutoConnect", "true");
        PlayerPrefs.Save();

        await Task.Delay(500);
        LoadMainGame();
    }
}
        public override void Spawned()
    {
        if (Object.HasInputAuthority)
        {
            // Local player için initialization
        }
    }
private string GetPlayerNickname()
{
    // *** DEĞİŞTİ: Önce PlayerPrefs'ten al ***
    string nickname = PlayerPrefs.GetString("Nickname", "");
    
    if (string.IsNullOrEmpty(nickname))
    {
        nickname = PlayerPrefs.GetString("CurrentUser", "");
    }
    
    // *** DEĞİŞTİ: Runner.LocalPlayer kullanma - PlayerRef ID'si döner ***
    // Runner kontrolünü kaldırdık
    
    if (string.IsNullOrEmpty(nickname))
    {
        Debug.LogWarning("[CharacterCreationManager] Nickname bulunamadı, fallback kullanılıyor");
        nickname = "Player_" + UnityEngine.Random.Range(1000, 9999);
    }
    
    return nickname;
}

private async Task SaveCharacterToFirebase(string characterJson)
    {
        if (FirebaseManager.Instance == null || !FirebaseManager.Instance.IsReady)
        {
            string characterKey = $"CharacterData_{playerNickname}";
            PlayerPrefs.SetString(characterKey, characterJson);
            PlayerPrefs.Save();
            return;
        }

        // Irkı belirle (sahne ismine göre)
        PlayerRace race = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Human")
            ? PlayerRace.Human
            : PlayerRace.Goblin;

        // Firebase'e irkına özgü kaydet
        await FirebaseManager.Instance.SaveCharacterDataByRace(playerNickname, race, characterJson);
    }

    private async void LoadMainGame()
    {
        loadingPanel.SetActive(true);
        statusText.text = "Oyuncu verisi yükleniyor...";

        // PROFESSIONAL MMORPG PATTERN: Pre-load Firebase data before entering game
        try
        {
            // Ensure PlayerDataSession exists and persists across scenes
            if (PlayerDataSession.Instance == null)
            {
                GameObject sessionObj = new GameObject("PlayerDataSession");
                sessionObj.AddComponent<PlayerDataSession>();
                DontDestroyOnLoad(sessionObj); // CRITICAL: Persist across scene changes
            }

            // Load player data from Firebase into session cache
            bool success = await PlayerDataSession.Instance.LoadPlayerDataFromFirebase(playerNickname);

            // ALSO pre-load character appearance data
            var characterData = await CharacterDataManager.LoadCharacterData(playerNickname);

            if (!success)
            {
                // Firebase load failed - DO NOT allow game start
                if (statusText != null)
                {
                    statusText.text = "Veri yüklenemedi. Lütfen tekrar deneyin.";
                }

                loadingPanel.SetActive(false);
                Debug.LogError("[CharacterCreationManager] Failed to load player data, cannot enter game");
                return;
            }

            statusText.text = "Oyun yükleniyor...";

            // Data ready, now load the game scene
            SceneManager.LoadScene(mainGameScene);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CharacterCreationManager] Error loading player data: {e.Message}");
            if (statusText != null)
            {
                statusText.text = "Hata oluştu. Lütfen tekrar deneyin.";
            }
            loadingPanel.SetActive(false);
        }
    }

    private void OnBackButtonClicked()
    {
        // Geri düğmesine basıldığında login ekranına dön veya uygun bir işlem yap
        // Örneğin:
        if (hasCharacter)
        {
            LoadMainGame();
        }
        else
        {
            // Login sahnesine dön veya uyarı göster
        }
    }
}