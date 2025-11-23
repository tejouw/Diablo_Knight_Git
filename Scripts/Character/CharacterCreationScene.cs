// Path: Assets/Game/Scripts/CharacterCreationScene.cs

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Fusion;
using Assets.HeroEditor4D.Common.Scripts.EditorScripts;

public class CharacterCreationScene : NetworkBehaviour
{
    [Header("Scene References")]
    [SerializeField] private CharacterEditor characterEditor;
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Text statusText;
    [SerializeField] private Button finishButton;
    [SerializeField] private Button randomizeButton;
    [SerializeField] private string mainGameScene = "MainGame";


public override void Spawned()
{
    if (!Runner.IsConnectedToServer)
    {
        SceneManager.LoadScene("Login");
        return;
    }

    InitializeUI();
    ShowCharacterCreation();
}
    private void InitializeUI()
    {
        if (finishButton != null)
        {
            finishButton.onClick.AddListener(OnFinishButtonClicked);
        }

        if (randomizeButton != null)
        {
            randomizeButton.onClick.AddListener(OnRandomizeButtonClicked);
        }
        
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }
    }

    private void ShowCharacterCreation()
    {
        // CharacterEditor'ı başlat/hazırla
        if (characterEditor != null)
        {
            // İhtiyaç duyulursa burada editörü konfigüre et
            // Örneğin varsayılan bir karakter yükle veya rastgele oluştur
            characterEditor.Randomize(); // İlk açılışta rastgele bir karakter oluştur
        }
    }

    private void OnRandomizeButtonClicked()
    {
        if (characterEditor != null)
        {
            characterEditor.Randomize();
        }
    }

    private async void OnFinishButtonClicked()
    {
        if (characterEditor == null || characterEditor.Character == null)
        {
            Debug.LogError("Character Editor veya Character bulunamadı!");
            return;
        }

        loadingPanel.SetActive(true);
        if (statusText != null) statusText.text = "Karakter kaydediliyor...";

        try
        {
            string characterJson = characterEditor.Character.ToJson();
            string nickname = GetPlayerNickname();

            if (string.IsNullOrEmpty(nickname))
            {
                if (statusText != null) statusText.text = "Kullanıcı adı bulunamadı!";
                loadingPanel.SetActive(false);
                return;
            }

            // Irkı sahne ismine göre belirle
            PlayerRace race = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Human")
                ? PlayerRace.Human
                : PlayerRace.Goblin;

            // CharacterDataManager ile kaydet
            bool saveSuccess = await CharacterDataManager.SaveCharacterData(nickname, race, characterJson);

            if (saveSuccess)
            {
                if (statusText != null) statusText.text = "Karakter kaydedildi!";

                LoadMainGame();
            }
            else
            {
                if (statusText != null) statusText.text = "Karakter kaydedilemedi!";
                loadingPanel.SetActive(false);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CharacterCreationScene] Save error: {e.Message}");
            if (statusText != null) statusText.text = "Karakter kaydedilemedi!";
            loadingPanel.SetActive(false);
        }
    }

private string GetPlayerNickname()
{
    if (Object.HasInputAuthority && Runner != null && Runner.IsConnectedToServer)
    {
        return Runner.LocalPlayer.ToString();
    }
    
    // Fallback
    string nickname = PlayerPrefs.GetString("CurrentUser", "");
    if (string.IsNullOrEmpty(nickname))
    {
        nickname = PlayerPrefs.GetString("Nickname", "Player");
    }
    
    return nickname;
}

    private void LoadMainGame()
    {
        if (loadingPanel != null && statusText != null)
        {
            loadingPanel.SetActive(true);
            statusText.text = "Oyun yükleniyor...";
        }

        // Ana oyun sahnesine geç
        SceneManager.LoadScene(mainGameScene);
    }
    }