using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

public class SimpleLoginManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject loginScreen;
    [SerializeField] private GameObject loginPanel;
    [SerializeField] private InputField nicknameInput;
    [SerializeField] private Button loginButton;
    [SerializeField] private Text errorText;
    [SerializeField] private GameObject loadingIcon;

    [Header("Settings")]
    [SerializeField] private int minNicknameLength = 3;
    [SerializeField] private int maxNicknameLength = 16;

    [Header("Race Selection")]
    [SerializeField] private RaceSelectionManager raceSelectionManager;

    private FirebaseManager firebaseManager;
    private NetworkManager networkManager;
    private bool isLoading = false;

    private void Awake()
    {
        if (IsServerMode())
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        if (IsServerMode()) return;

        if (LoadingManager.Instance?.loadingPanel.activeInHierarchy == true)
            LoadingManager.Instance.CancelLoading();

        ForceDeactivateGameUI();
        InitializeComponents();
        SetupUI();
        LoadSavedNickname();
        CheckAutoConnect();
    }

    private bool IsServerMode()
    {
        if (Application.isEditor) return false;
        string[] args = System.Environment.GetCommandLineArgs();
        return System.Array.Exists(args, arg => arg == "-server" || arg == "-batchmode");
    }

    private void InitializeComponents()
    {
        firebaseManager = FirebaseManager.Instance;
        networkManager = FindAnyObjectByType<NetworkManager>();
    }

    private void SetupUI()
    {
        if (loadingIcon != null) loadingIcon.SetActive(false);
        if (errorText != null) errorText.text = "";
        
        nicknameInput.contentType = InputField.ContentType.Standard;
        nicknameInput.characterLimit = maxNicknameLength;
        nicknameInput.onValueChanged.AddListener(OnInputValueChanged);
        
        loginButton.onClick.AddListener(OnLoginButtonClick);
    }

    private void ForceDeactivateGameUI()
    {
        GameObject gameUI = GameObject.Find("GameUI") ?? GameObject.Find("GameUI(Clone)");
        if (gameUI != null) gameUI.SetActive(false);
        
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            Transform gameUITransform = canvas.transform.Find("GameUI");
            if (gameUITransform != null)
            {
                gameUITransform.gameObject.SetActive(false);
                break;
            }
        }
    }

    private void LoadSavedNickname()
    {
        string savedNickname = PlayerPrefs.GetString("Nickname", "");
        if (!string.IsNullOrEmpty(savedNickname))
            nicknameInput.text = savedNickname;
    }

    private void CheckAutoConnect()
    {
        if (PlayerPrefs.GetString("SkipLoginPanel", "false") == "true")
        {
            GameObject loginPanelCerceve = GameObject.Find("LoginPanelCerceve");
            if (loginPanelCerceve != null) loginPanelCerceve.SetActive(false);
            PlayerPrefs.DeleteKey("SkipLoginPanel");
            PlayerPrefs.Save();
        }

        if (PlayerPrefs.GetString("AutoConnect", "false") == "true")
        {
            PlayerPrefs.SetString("AutoConnect", "false");
            PlayerPrefs.Save();
            
            string savedNickname = PlayerPrefs.GetString("Nickname", "");
            if (!string.IsNullOrEmpty(savedNickname))
            {
                nicknameInput.text = savedNickname;
                OnLoginButtonClick();
            }
        }
    }

    private void OnInputValueChanged(string value)
    {
        if (errorText != null) errorText.text = "";
        
        if (value.Contains(" "))
        {
            nicknameInput.text = value.Replace(" ", "");
            return;
        }

        loginButton.interactable = value.Length >= minNicknameLength && !isLoading;
    }

    private async void OnLoginButtonClick()
    {
        if (isLoading) return;

        string nickname = nicknameInput.text.Trim();
        if (string.IsNullOrEmpty(nickname) || nickname.Length < minNicknameLength)
        {
            ShowError($"Kullanıcı adı en az {minNicknameLength} karakter olmalıdır!");
            return;
        }

        isLoading = true;
        SetLoadingState(true);
        LoadingManager.Instance?.StartLoading();

        try
        {
            if (!await WaitForFirebaseReady())
            {
                ShowError("Firebase bağlantısı kurulamadı!");
                return;
            }

            if (!await HandleUserAccount(nickname))
            {
                ShowError("Hesap oluşturulamadı. Lütfen farklı bir nickname deneyin!");
                return;
            }

            PlayerPrefs.SetString("Nickname", nickname);
            PlayerPrefs.SetString("CurrentUser", nickname);
            PlayerPrefs.Save();

            PlayerRace? playerRace = await ValidatePlayerRace(nickname);
            if (!playerRace.HasValue)
            {
                HandleLoginFailure();
                LoadingManager.Instance?.CancelLoading();
                raceSelectionManager?.ShowRaceSelection(nickname);
                return;
            }

            bool hasCharacter = await ValidatePlayerCharacter(nickname, playerRace.Value);
            PlayerPrefs.SetString($"PlayerRace_{nickname}", playerRace.Value.ToString());
            PlayerPrefs.Save();

            if (!hasCharacter)
            {
                string targetScene = playerRace.Value == PlayerRace.Human ? "HumanCharacterCreation" : "GoblinCharacterCreation";
                SceneManager.LoadScene(targetScene);
            }
            else
            {
                // PROFESSIONAL MMORPG PATTERN: Pre-load Firebase data before connecting
                await ConnectToGameAsync(nickname);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Login error: {e.Message}");
            ShowError("Bir hata oluştu. Lütfen tekrar deneyin!");
            HandleLoginFailure();
        }
    }

    private async Task<bool> WaitForFirebaseReady()
    {
        if (firebaseManager?.IsReady == true) return true;

        float timeout = 2f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (firebaseManager?.IsReady == true) return true;
            await Task.Delay(50);
            elapsed += 0.05f;
            LoadingManager.Instance?.CompleteStep("FirebaseConnection");
        }

        return false;
    }

    private async Task<bool> HandleUserAccount(string nickname)
    {
        try
        {
            var userData = await firebaseManager.LoadUserData(nickname);
            bool isNewUser = userData == null;

            if (isNewUser)
            {
                bool created = await firebaseManager.CreateUserAccount(nickname);
                if (!created) return false;
            }

            LoadingManager.Instance?.CompleteStep("UserAccount");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"HandleUserAccount error: {e.Message}");
            return false;
        }
    }

    private async Task<PlayerRace?> ValidatePlayerRace(string nickname)
    {
        try
        {
            PlayerRace? race = await CharacterDataManager.GetPlayerRace(nickname);
            LoadingManager.Instance?.CompleteStep("CharacterValidation");
            return race;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Race validation error: {e.Message}");
            return null;
        }
    }

    private async Task<bool> ValidatePlayerCharacter(string nickname, PlayerRace race)
    {
        try
        {
            return await CharacterDataManager.HasCharacterData(nickname);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Character validation error: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// PROFESSIONAL MMORPG PATTERN: Pre-load Firebase data before connecting to game
    /// </summary>
    private async Task ConnectToGameAsync(string nickname)
    {
        try
        {
            // Ensure PlayerDataSession exists and persists
            if (PlayerDataSession.Instance == null)
            {
                GameObject sessionObj = new GameObject("PlayerDataSession");
                sessionObj.AddComponent<PlayerDataSession>();
                DontDestroyOnLoad(sessionObj);
            }

            // Pre-load player data from Firebase into session cache
            if (LoadingManager.Instance != null)
            {
                LoadingManager.Instance.CompleteStep("CharacterValidation");
            }

            bool success = await PlayerDataSession.Instance.LoadPlayerDataFromFirebase(nickname);

            // ALSO pre-load character appearance data (for HeadPreview, sprites, etc.)
            var characterData = await CharacterDataManager.LoadCharacterData(nickname);

            if (!success)
            {
                // Firebase load failed - DO NOT connect
                Debug.LogError("[SimpleLoginManager] Failed to pre-load player data!");
                ShowError("Oyuncu verisi yüklenemedi. Lütfen tekrar deneyin.");
                HandleLoginFailure();
                LoadingManager.Instance?.CancelLoading();
                return;
            }

            // Now connect to game with pre-loaded data
            if (networkManager == null)
                networkManager = FindAnyObjectByType<NetworkManager>();

            if (networkManager != null)
            {
                networkManager.playerNickname = nickname;

                if (loginScreen != null) loginScreen.SetActive(false);
                if (loginPanel != null) loginPanel.SetActive(false);

                LoadingManager.Instance?.CompleteStep("UITransition");
                networkManager.ConnectToServer();
            }
            else
            {
                ShowError("Bağlantı hatası! NetworkManager bulunamadı.");
                HandleLoginFailure();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[SimpleLoginManager] ConnectToGameAsync error: {e.Message}");
            ShowError("Bağlantı hatası! Lütfen tekrar deneyin.");
            HandleLoginFailure();
        }
    }

    /// <summary>
    /// DEPRECATED: Use ConnectToGameAsync() instead
    /// </summary>
    private void ConnectToGame(string nickname)
    {
        Debug.LogWarning("[SimpleLoginManager] ConnectToGame() is deprecated, use ConnectToGameAsync() instead");
        _ = ConnectToGameAsync(nickname);
    }

    private void HandleLoginFailure()
    {
        SetLoadingState(false);
        isLoading = false;
        LoadingManager.Instance?.CancelLoading();
        
        if (loginScreen != null) loginScreen.SetActive(true);
        if (loginPanel != null) loginPanel.SetActive(true);
    }

    public void OnConnectionFailed()
    {
        SetLoadingState(false);
        isLoading = false;
        LoadingManager.Instance?.CancelLoading();

        if (loginScreen != null) loginScreen.SetActive(true);
        if (loginPanel != null) loginPanel.SetActive(true);

        ShowError("Sunucuya bağlanılamadı! Lütfen tekrar deneyin.");
    }

    public void OnFinalConnectionFailure()
    {
        SetLoadingState(false);
        isLoading = false;
        LoadingManager.Instance?.CancelLoading();

        if (loginScreen != null) loginScreen.SetActive(true);
        if (loginPanel != null) loginPanel.SetActive(true);

        ShowError("Sunucuya bağlanılamadı! Lütfen daha sonra tekrar deneyin.");
    }

    private void ShowError(string message)
    {
        if (errorText != null)
        {
            errorText.text = message;
            errorText.color = Color.red;
        }
    }

    private void SetLoadingState(bool loading)
    {
        if (loadingIcon != null) loadingIcon.SetActive(loading);
        if (loginButton != null) loginButton.interactable = !loading;
        if (nicknameInput != null) nicknameInput.interactable = !loading;
    }
}