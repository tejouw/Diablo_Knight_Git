using UnityEngine;

public class LocalPlayerManager : MonoBehaviour
{
    public static LocalPlayerManager Instance { get; private set; }
    
    [Header("Cached References")]
    public PlayerStats LocalPlayerStats { get; private set; }
    public ClassSystem LocalClassSystem { get; private set; }
    public SkillSystem LocalSkillSystem { get; private set; }
    
    // Events
    public System.Action<PlayerStats> OnLocalPlayerFound;
    
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

public void SetLocalPlayer(PlayerStats playerStats)
{
    if (playerStats == null) 
    {
        Debug.LogError($"[LocalPlayerManager.SetLocalPlayer] Called with NULL at {Time.time:F2}s");
        return;
    }
    
    
    LocalPlayerStats = playerStats;
    LocalClassSystem = playerStats.GetComponent<ClassSystem>();
    LocalSkillSystem = playerStats.GetComponent<SkillSystem>();
    
    OnLocalPlayerFound?.Invoke(playerStats);
}

    public void ClearLocalPlayer()
    {
        LocalPlayerStats = null;
        LocalClassSystem = null;
        LocalSkillSystem = null;
    }
}