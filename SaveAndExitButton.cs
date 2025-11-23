// Assets/Game/Scripts/SaveCharacterButton.cs

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;
using Assets.HeroEditor4D.Common.Scripts.EditorScripts;
using Fusion;

public class SaveCharacterButton : MonoBehaviour
{
    [SerializeField] private CharacterEditor characterEditor;
    [SerializeField] private GameObject loadingIndicator;
    [SerializeField] private Text statusText;

    private void Start()
    {
        Button button = GetComponent<Button>();
        if (button != null && characterEditor != null)
        {
            button.onClick.AddListener(SaveCharacterAndReturn);
        }
        
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }
    }

    public void SaveCharacterAndReturn()
    {
        if (characterEditor == null || characterEditor.Character == null)
        {
            Debug.LogError("Character Editor veya Character referansı eksik!");
            return;
        }
        
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(true);
        }
        
        if (statusText != null)
        {
            statusText.text = "Karakter kaydediliyor...";
        }

        StartCoroutine(SaveCharacterProcess());
    }

// SaveCharacterButton.cs - SaveCharacterProcess metodunu düzelt

private IEnumerator SaveCharacterProcess()
{
    string characterJson = "";
    string nickname = "";
    PlayerRace race = PlayerRace.Human;
    NetworkManager networkManager = NetworkManager.Instance;
    bool validationPassed = false;
    
    try
    {
        // Karakter validasyonu
        if (characterEditor.Character == null || characterEditor.Character.Front == null || 
            characterEditor.Character.Front.SpriteCollection == null)
        {
            Debug.LogError("[SaveCharacterButton] Character validation failed!");
            if (statusText != null) statusText.text = "Karakter bulunamadı!";
            yield break;
        }

        // Karakteri JSON olarak al
        characterJson = characterEditor.Character.ToJson();
        
        // *** DEĞİŞTİ: PlayerPrefs'ten güncel nickname'i al ***
        nickname = PlayerPrefs.GetString("Nickname", "");
        
        // *** YENİ: Fallback - CurrentUser'dan da dene ***
        if (string.IsNullOrEmpty(nickname))
        {
            nickname = PlayerPrefs.GetString("CurrentUser", "");
        }
        
        // *** YENİ: NetworkManager'dan da dene ***
        if (string.IsNullOrEmpty(nickname) && networkManager != null)
        {
            nickname = networkManager.playerNickname;
        }
        
        // *** YENİ: Hala boşsa hata ver ***
        if (string.IsNullOrEmpty(nickname))
        {
            Debug.LogError("[SaveCharacterButton] Nickname bulunamadı!");
            if (statusText != null) statusText.text = "Kullanıcı adı bulunamadı!";
            if (loadingIndicator != null) loadingIndicator.SetActive(false);
            yield break;
        }
        
        // Irkı sahne ismine göre belirle
        race = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Human") 
            ? PlayerRace.Human 
            : PlayerRace.Goblin;
        
        if (statusText != null) statusText.text = "Karakter kaydediliyor...";
        
        validationPassed = true;
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[SaveCharacterButton] Character validation error: {e.Message}");
        
        if (statusText != null)
        {
            statusText.text = "Hata: " + e.Message;
        }
        
        if (loadingIndicator != null)
        {
            loadingIndicator.SetActive(false);
        }
        
        yield break;
    }

    if (!validationPassed) yield break;

    // Save işlemi
    bool saveCompleted = false;
    bool saveSuccess = false;
    System.Exception saveError = null;

    CharacterDataManager.SaveCharacterData(nickname, race, characterJson)
        .ContinueWith(task =>
        {
            saveCompleted = true;
            if (task.IsFaulted)
            {
                saveError = task.Exception;
            }
            else
            {
                saveSuccess = task.Result;
            }
        }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());

    while (!saveCompleted)
    {
        yield return null;
    }

    if (saveError != null)
    {
        Debug.LogError($"[SaveCharacterButton] Save error: {saveError.Message}");
        if (statusText != null) statusText.text = "Kaydetme hatası!";
        if (loadingIndicator != null) loadingIndicator.SetActive(false);
        yield break;
    }

    if (!saveSuccess)
    {
        if (statusText != null) statusText.text = "Karakter kaydedilemedi!";
        if (loadingIndicator != null) loadingIndicator.SetActive(false);
        yield break;
    }

    if (statusText != null) statusText.text = "Kaydedildi! Bağlantı kesiliyor...";
    
    // Network cleanup
    if (statusText != null) statusText.text = "Bağlantı kesiliyor...";
    
    if (networkManager != null && networkManager.Runner != null && networkManager.Runner.IsRunning)
    {
        networkManager.Runner.Shutdown();
        
        float waitTime = 0;
        while (networkManager.Runner.IsRunning && waitTime < 3f)
        {
            waitTime += 0.1f;
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    if (networkManager != null)
    {
        NetworkManager.Instance = null;
        Destroy(networkManager.gameObject);
    }
    
    NetworkObject[] networkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
    foreach (var netObj in networkObjects)
    {
        if (netObj != null)
        {
            Destroy(netObj.gameObject);
        }
    }
    
    if (statusText != null) statusText.text = "Login ekranına yönlendiriliyor...";
    
    PlayerPrefs.SetString("AutoConnect", "true");
    PlayerPrefs.Save();
    
    yield return new WaitForSeconds(0.5f);
    
    System.GC.Collect();
    
    SceneManager.LoadScene("MainGame");
}
}