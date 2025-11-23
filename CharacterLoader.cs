// Path: Assets/Game/Scripts/CharacterLoader.cs

using UnityEngine;
using Fusion;
using System.IO;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using System.Collections;
using System.Collections.Generic;


public class CharacterLoader : NetworkBehaviour
{
    private PlayerStats playerStats;

    [Header("Character Loading")]

    private string characterSavePath => Path.Combine(Application.dataPath, "CharacterData");
    private string characterJsonPath => Path.Combine(characterSavePath, "character.json");

// DEÄžÄ°ÅžTÄ°RÄ°LECEK METOD
public override void Spawned()
{
    playerStats = GetComponent<PlayerStats>();

    // HER PLAYER Ä°Ã‡Ä°N CHARACTER YÃœKLE - Merkezi sistemle
    StartCoroutine(LoadCharacterWithDelay());
}

private IEnumerator LoadCharacterWithDelay()
{
    yield return new WaitForEndOfFrame();

    // Server modunda görsel yükleme atla
    if (IsServerMode())
    {
        yield break;
    }

    if (Object.HasInputAuthority)
    {
        // LOCAL PLAYER - Load ve broadcast yap
        yield return StartCoroutine(LoadCharacterFromManager());
        SyncCharacterToNetwork();
    }
    else
    {
        // REMOTE PLAYER - Sync request gönder
        RequestCharacterSyncFromOwner();
    }
}

// YENİ METOD
private void RequestCharacterSyncFromOwner()
{
    Character4D character = GetComponent<Character4D>();
    if (character != null)
    {
        character.RequestCharacterSyncRPC(Runner.LocalPlayer);
    }
}


private IEnumerator LoadCharacterFromManager()
{
    string nickname = GetPlayerNickname();
    
    if (string.IsNullOrEmpty(nickname))
    {
        Debug.LogError("[CharacterLoader] Nickname bulunamadı!");
        // DEFAULT KALDIR - Exception fırlat
        throw new System.Exception("No nickname found for character loading");
    }

    // CharacterDataManager ile karakter verilerini al
    bool isLoaded = false;
    CharacterData characterData = null;
    System.Exception loadError = null;

    CharacterDataManager.LoadCharacterData(nickname).ContinueWith(task =>
    {
        isLoaded = true;
        if (task.IsFaulted)
        {
            loadError = task.Exception;
        }
        else if (task.IsCompleted)
        {
            characterData = task.Result;
        }
    }, System.Threading.Tasks.TaskScheduler.FromCurrentSynchronizationContext());

    // İşlem bitene kadar bekle
    while (!isLoaded)
    {
        yield return null;
    }

    if (loadError != null)
    {
        Debug.LogError($"[CharacterLoader] Character loading error: {loadError.Message}");
        // DEFAULT KALDIR - Exception fırlat
        throw new System.Exception($"Character loading failed: {loadError.Message}");
    }
    
    if (characterData == null)
    {
        // DEFAULT KALDIR - Exception fırlat
        throw new System.Exception($"No character data found for {nickname}! Login validation failed.");
    }

    bool applySuccess = CharacterDataManager.ApplyCharacterData(GetComponent<Character4D>(), characterData);
    
    if (!applySuccess)
    {
        throw new System.Exception("Failed to apply character data");
    }

    // Karakter yükleme tamamlandı
    if (LoadingManager.Instance != null)
    {
        LoadingManager.Instance.CompleteStep("CharacterLoading");
    }
}
private void SyncCharacterToNetwork()
{
    Character4D character = GetComponent<Character4D>();
    if (character != null)
    {
        try
        {
            character.SendCharacterDataInChunks();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CharacterLoader] Character sync error: {e.Message}");
        }
    }
}
    public async void SaveCharacterJson(string json)
    {
        try
        {
            string nickname = GetPlayerNickname();
            if (string.IsNullOrEmpty(nickname))
            {
                Debug.LogError("[CharacterLoader] Cannot save - no nickname");
                return;
            }

            // Race'i belirle (prefab/gameobject name'den)
            PlayerRace race = PlayerRace.Human;
            if (gameObject.name.Contains("Goblin"))
            {
                race = PlayerRace.Goblin;
            }

            // CharacterDataManager ile kaydet - AWAIT EKLE
            await CharacterDataManager.SaveCharacterData(nickname, race, json);
            
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CharacterLoader] Save error: {e.Message}");
        }
    }
private string GetPlayerNickname()
{
    // NetworkManager'dan al
    if (NetworkManager.Instance != null)
    {
        return NetworkManager.Instance.playerNickname;
    }

    // PlayerPrefs'ten fallback
    return PlayerPrefs.GetString("CurrentUser", "");
}

private bool IsServerMode()
{
    if (Application.isEditor) return false;

    string[] args = System.Environment.GetCommandLineArgs();
    return System.Array.Exists(args, arg => arg == "-server" || arg == "-batchmode");
}
}