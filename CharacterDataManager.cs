using UnityEngine;
using System.Threading.Tasks;
using System;
using Fusion;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;

public class CharacterDataManager : NetworkBehaviour
{
    public static CharacterDataManager Instance;

    [Header("Settings")]
    [SerializeField] private bool debugLogs = true;
    
    private const string CHARACTER_KEY_FORMAT = "CharacterData_{0}_{1}";
    private const string RACE_KEY_FORMAT = "PlayerRace_{0}";
    
    private static string cachedCharacterJson = "";
    private static string cachedNickname = "";
    private static PlayerRace? cachedRace = null;
    private static bool isDataLoaded = false;

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

    public static async Task<bool> SaveCharacterData(string nickname, PlayerRace race, string characterJson)
    {
        if (string.IsNullOrEmpty(nickname) || string.IsNullOrEmpty(characterJson))
        {
            Debug.LogError("[CharacterDataManager] Invalid save parameters");
            return false;
        }

        try
        {
            string characterKey = string.Format(CHARACTER_KEY_FORMAT, nickname, race);
            string raceKey = string.Format(RACE_KEY_FORMAT, nickname);
            
            PlayerPrefs.SetString(characterKey, characterJson);
            PlayerPrefs.SetString(raceKey, race.ToString());
            PlayerPrefs.SetString("Nickname", nickname);
            PlayerPrefs.SetString("CurrentUser", nickname);
            PlayerPrefs.Save();

            cachedCharacterJson = characterJson;
            cachedNickname = nickname;
            cachedRace = race;
            isDataLoaded = true;

            if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
            {
                await FirebaseManager.Instance.SaveCharacterDataByRace(nickname, race, characterJson);
                await FirebaseManager.Instance.SavePlayerRace(nickname, race);
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterDataManager] Save error: {e.Message}");
            return false;
        }
    }

    public static async Task<CharacterData> LoadCharacterData(string nickname)
    {
        if (string.IsNullOrEmpty(nickname))
        {
            Debug.LogError("[CharacterDataManager] Invalid nickname");
            return null;
        }

        if (isDataLoaded && cachedNickname == nickname && !string.IsNullOrEmpty(cachedCharacterJson) && cachedRace.HasValue)
        {
            return new CharacterData
            {
                nickname = nickname,
                race = cachedRace.Value,
                characterJson = cachedCharacterJson,
                source = DataSource.Cache
            };
        }

        string raceKeyLocal = string.Format(RACE_KEY_FORMAT, nickname);
        if (PlayerPrefs.HasKey(raceKeyLocal))
        {
            string raceString = PlayerPrefs.GetString(raceKeyLocal);
            if (System.Enum.TryParse<PlayerRace>(raceString, out PlayerRace localRace))
            {
                string characterKeyLocal = string.Format(CHARACTER_KEY_FORMAT, nickname, localRace);
                if (PlayerPrefs.HasKey(characterKeyLocal))
                {
                    string localCharacterJson = PlayerPrefs.GetString(characterKeyLocal);
                    if (!string.IsNullOrEmpty(localCharacterJson))
                    {
                        cachedCharacterJson = localCharacterJson;
                        cachedNickname = nickname;
                        cachedRace = localRace;
                        isDataLoaded = true;

                        _ = Task.Run(async () => await SyncToFirebaseBackground(nickname, localRace, localCharacterJson));

                        return new CharacterData
                        {
                            nickname = nickname,
                            race = localRace,
                            characterJson = localCharacterJson,
                            source = DataSource.PlayerPrefs
                        };
                    }
                }
            }
        }

        try
        {
            if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
            {
                PlayerRace? firebaseRace = await FirebaseManager.Instance.LoadPlayerRace(nickname);
                
                if (firebaseRace.HasValue)
                {
                    string firebaseCharacterJson = await FirebaseManager.Instance.LoadCharacterDataByRace(nickname, firebaseRace.Value);
                    
                    if (!string.IsNullOrEmpty(firebaseCharacterJson))
                    {
                        string characterKey = string.Format(CHARACTER_KEY_FORMAT, nickname, firebaseRace.Value);
                        string raceKey = string.Format(RACE_KEY_FORMAT, nickname);
                        
                        PlayerPrefs.SetString(characterKey, firebaseCharacterJson);
                        PlayerPrefs.SetString(raceKey, firebaseRace.Value.ToString());
                        PlayerPrefs.Save();

                        cachedCharacterJson = firebaseCharacterJson;
                        cachedNickname = nickname;
                        cachedRace = firebaseRace.Value;
                        isDataLoaded = true;

                        return new CharacterData
                        {
                            nickname = nickname,
                            race = firebaseRace.Value,
                            characterJson = firebaseCharacterJson,
                            source = DataSource.Firebase
                        };
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterDataManager] Firebase error: {e.Message}");
        }

        return null;
    }

    private static async Task SyncToFirebaseBackground(string nickname, PlayerRace race, string characterJson)
    {
        try
        {
            if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsReady)
            {
                await FirebaseManager.Instance.SaveCharacterDataByRace(nickname, race, characterJson);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterDataManager] Background sync error: {e.Message}");
        }
    }

    public static async Task<bool> HasCharacterData(string nickname)
    {
        var data = await LoadCharacterData(nickname);
        return data != null;
    }

    public static async Task<PlayerRace?> GetPlayerRace(string nickname)
    {
        var data = await LoadCharacterData(nickname);
        return data?.race;
    }

    public static async Task<PlayerRace?> GetPlayerRaceQuick(string nickname)
    {
        try
        {
            if (FirebaseManager.Instance?.IsReady == true)
            {
                var snapshot = await FirebaseManager.Instance.LoadDataFromPath($"players/{nickname}/race");
                if (snapshot != null && snapshot.Exists)
                {
                    string raceString = snapshot.Value.ToString();
                    if (System.Enum.TryParse<PlayerRace>(raceString, out PlayerRace race))
                    {
                        return race;
                    }
                }
            }
            
            string raceKey = "PlayerRace_" + nickname;
            if (PlayerPrefs.HasKey(raceKey))
            {
                string raceString = PlayerPrefs.GetString(raceKey);
                if (System.Enum.TryParse<PlayerRace>(raceString, out PlayerRace race))
                {
                    return race;
                }
            }
            
            return null;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[CharacterDataManager] GetPlayerRaceQuick error: {e.Message}");
            return null;
        }
    }

    public static async Task<PlayerRace?> GetPlayerRaceByNickname(string nickname)
    {
        return await GetPlayerRaceQuick(nickname);
    }

    public static void ClearCache()
    {
        cachedCharacterJson = "";
        cachedNickname = "";
        cachedRace = null;
        isDataLoaded = false;
    }

    public static bool ApplyCharacterData(Character4D character4D, CharacterData data)
    {
        if (character4D == null || data == null || string.IsNullOrEmpty(data.characterJson))
        {
            Debug.LogError("[CharacterDataManager] Invalid apply parameters");
            return false;
        }

        try
        {
            character4D.FromJson(data.characterJson, silent: true);
            character4D.Initialize();
            character4D.SetDirection(Vector2.down);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[CharacterDataManager] Apply error: {e.Message}");
            return false;
        }
    }

    public override void Spawned()
    {
        if (Object.HasInputAuthority && debugLogs)
        {
        }
    }
}

[System.Serializable]
public class CharacterData
{
    public string nickname;
    public PlayerRace race;
    public string characterJson;
    public DataSource source;
}

public enum DataSource
{
    Cache,
    Firebase,
    PlayerPrefs,
    Default
}