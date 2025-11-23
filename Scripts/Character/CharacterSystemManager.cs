// Path: Assets/Game/Scripts/CharacterSystemManager.cs

using UnityEngine;
using UnityEngine.SceneManagement;
using Fusion;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Threading.Tasks;

/// <summary>
/// Bu sınıf, karakter oluşturma ve yükleme sistemini yönetir.
/// GameManager veya benzeri bir singleton sınıfı içinde de kullanılabilir.
/// </summary>
public class CharacterSystemManager : NetworkBehaviour
{
    public static CharacterSystemManager Instance;

    [Header("Character System Settings")]
    [SerializeField] private string characterCreationScene = "CharacterCreation";
    
    [Header("Character Check")]

    private string characterJsonData;

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
    /// Karakter verilerini kaydet
    /// </summary>
    public async void SaveCharacterData(string json, PlayerRace race)
    {
        if (string.IsNullOrEmpty(json)) return;
        
        try
        {
            string nickname = GetPlayerNickname();
            if (string.IsNullOrEmpty(nickname))
            {
                Debug.LogError("[CharacterSystemManager] Cannot save - no nickname");
                return;
            }

            // CharacterDataManager ile kaydet
            bool saveSuccess = await CharacterDataManager.SaveCharacterData(nickname, race, json);
            
            if (saveSuccess)
            {
                characterJsonData = json;
                // isCharacterLoaded = true; // KALDIR - artık kullanılmıyor
            }
            else
            {
                Debug.LogError("[CharacterSystemManager] Failed to save character data");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CharacterSystemManager] Save error: {e.Message}");
        }
    }
    public async Task<string> LoadCharacterDataAsync()
    {
        try
        {
            string nickname = GetPlayerNickname();
            if (string.IsNullOrEmpty(nickname))
            {
                Debug.LogError("[CharacterSystemManager] Cannot load - no nickname");
                return null;
            }

            // CharacterDataManager ile yükle
            CharacterData characterData = await CharacterDataManager.LoadCharacterData(nickname);
            
            if (characterData != null)
            {
                characterJsonData = characterData.characterJson;
                // isCharacterLoaded = true; // KALDIR - artık kullanılmıyor
                
                
                return characterJsonData;
            }
            else
            {
                return null;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CharacterSystemManager] Load error: {e.Message}");
            return null;
        }
    }
private string GetPlayerNickname()
{
    // NetworkManager'dan al
    if (NetworkManager.Instance != null)
    {
        return NetworkManager.Instance.GetLocalPlayerNickname();
    }
    
    // Yoksa PlayerPrefs'ten al
    return PlayerPrefs.GetString("Nickname", "Player");
}


    /// <summary>
    /// Karakter verilerini yükle
    /// </summary>


    /// <summary>
    /// Spawn edilen karaktere görünüm uygula
    /// </summary>
// CharacterSystemManager.cs - ApplyCharacterAppearance metodunu güncelle
public async void ApplyCharacterAppearance(GameObject playerObject)
{
    if (playerObject == null) return;

    Character4D character = playerObject.GetComponent<Character4D>();
    if (character == null)
    {
        Debug.LogError("[CharacterSystemManager] Character4D component not found!");
        return;
    }

    try
    {
        string nickname = GetPlayerNickname();
        if (string.IsNullOrEmpty(nickname))
        {
            throw new System.Exception("Cannot apply character - no nickname");
        }

        // CharacterDataManager ile karakter verilerini al
        CharacterData characterData = await CharacterDataManager.LoadCharacterData(nickname);
        
        if (characterData == null)
        {
            // RACE YOKSA EXCEPTION FIRLAT - DEFAULT YOK
            throw new System.Exception($"No character data found for {nickname}! This should never happen after login validation.");
        }

        // Karakter verilerini uygula
        bool applySuccess = CharacterDataManager.ApplyCharacterData(character, characterData);
        
        if (!applySuccess)
        {
            throw new System.Exception("Failed to apply character appearance");
        }

        NetworkObject networkObject = playerObject.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.HasInputAuthority)
        {
            character.SyncCharacterAppearance(networkObject);
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[CharacterSystemManager] Apply error: {e.Message}");
        // DEFAULT CHARACTER KALDIR - Exception propagate et
        throw;
    }
}
public override void Spawned()
{
    if (Object.HasInputAuthority)
    {
        // Local player için gerekli başlangıç işlemleri
    }
}

    /// <summary>
    /// Karakter oluşturma sahnesini aç
    /// </summary>
    public void OpenCharacterCreation()
    {
        SceneManager.LoadScene(characterCreationScene);
    }
}