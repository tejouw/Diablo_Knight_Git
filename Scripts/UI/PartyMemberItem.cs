using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PartyMemberItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playerLevelText;
    [SerializeField] private Image playerAvatarImage;

    [Header("Health UI")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private Text healthText;
    [SerializeField] private Text healthPercentText;

    private PlayerRef playerRef;
    private PlayerStats playerStats;

public void Setup(PlayerRef playerRef, string playerName, int playerLevel)
{
    this.playerRef = playerRef;
    
    
    if (playerNameText != null)
        playerNameText.text = playerName;
    
    if (playerLevelText != null)
        playerLevelText.text = $"Seviye {playerLevel}";

    // Avatar render isteme
    if (playerAvatarImage != null && PartyAvatarRenderer.Instance != null)
    {
        PartyAvatarRenderer.Instance.RenderPlayerAvatar(playerRef, SetAvatarTexture);
    }
    else
    {
        Debug.LogError($"[PartyMemberItem] Avatar render başarısız! playerAvatarImage null: {playerAvatarImage == null}, Instance null: {PartyAvatarRenderer.Instance == null}");
    }

    FindAndSetupPlayerStats();
}

private void FindAndSetupPlayerStats()
{
    NetworkObject[] allPlayers = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
    
    
    foreach (NetworkObject netObj in allPlayers)
    {
        if (netObj != null && netObj.IsValid && netObj.InputAuthority == playerRef)
        {
            playerStats = netObj.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                string playerName = playerStats.GetPlayerDisplayName();
                
                // Health event'ini dinle
                playerStats.OnNetworkHealthChanged += UpdateHealthUI;
                
                // ✅ DÜZELTME: İlk health değerini CurrentHP property'sinden al
                UpdateHealthUI(playerStats.CurrentHP);
                break;
            }
        }
    }
    
    if (playerStats == null)
    {
        Debug.LogError($"[PartyMemberItem] PlayerRef {playerRef} için PlayerStats bulunamadı!");
    }
}

private void UpdateHealthUI(float currentHealth)
{
    if (healthBarFill != null && healthText != null && playerStats != null)
    {
        // ✅ DÜZELTME: CurrentHP property'sini kullan (Network/local otomatik handle eder)
        float actualCurrentHealth = playerStats.CurrentHP;
        float maxHealth = playerStats.GetNetworkMaxHP();
        float healthPercent = maxHealth > 0 ? actualCurrentHealth / maxHealth : 0f;
        
        string playerName = playerStats.GetPlayerDisplayName();
        
        healthBarFill.fillAmount = healthPercent;
        
        healthText.text = $"{Mathf.FloorToInt(actualCurrentHealth)} / {Mathf.FloorToInt(maxHealth)}";
        
        if (healthPercentText != null)
        {
            healthPercentText.text = $"{Mathf.FloorToInt(healthPercent * 100)}%";
        }
    }
    else
    {
    }
}

    private void SetAvatarTexture(Texture2D avatarTexture)
    {
        if (playerAvatarImage != null && avatarTexture != null)
        {
            Sprite avatarSprite = Sprite.Create(avatarTexture, 
                new Rect(0, 0, avatarTexture.width, avatarTexture.height), 
                new Vector2(0.5f, 0.5f));
            playerAvatarImage.sprite = avatarSprite;
        }
    }

    private void OnDestroy()
    {
        // Event'ten unsubscribe ol
        if (playerStats != null)
        {
            playerStats.OnNetworkHealthChanged -= UpdateHealthUI;
        }
    }
}