using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PotionUI : MonoBehaviour
{
    [Header("Potion UI")]
    [SerializeField] private Button potionButton;
    [SerializeField] private TextMeshProUGUI potionCountTextTMP;
    [SerializeField] private Text potionCountText;
    [SerializeField] private Image potionImage;
    [SerializeField] private Sprite potionSprite;

    private PlayerStats playerStats;

public void Initialize(PlayerStats stats)
{
    playerStats = stats;

    if (playerStats != null)
    {
        string playerName = playerStats.GetPlayerDisplayName();
        
        // Event'e subscribe ol
        playerStats.OnPotionCountChanged += UpdatePotionUI;
        
        // İlk değeri güncelle
        UpdatePotionUI(playerStats.PotionCount);

        // Button listener'ı kur
        if (potionButton != null)
        {
            potionButton.onClick.RemoveAllListeners();
            potionButton.onClick.AddListener(() =>
            {
                if (playerStats != null)
                {
                    playerStats.UsePotion();
                }
            });
        }
    }
}

    private void OnDestroy()
    {
        // Event'ten unsubscribe ol
        if (playerStats != null)
        {
            playerStats.OnPotionCountChanged -= UpdatePotionUI;
        }
    }

private void UpdatePotionUI(int count)
{
    
    string potionText = $"x{count}";
    
    if (potionCountTextTMP != null)
    {
        potionCountTextTMP.text = potionText;
    }
    
    if (potionCountText != null)
    {
        potionCountText.text = potionText;
    }

    if (potionButton != null)
    {
        potionButton.interactable = count > 0;
    }

    if (potionImage != null && potionSprite != null)
    {
        potionImage.sprite = potionSprite;
    }
}
}