using Fusion;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class NearbyPlayerItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    [SerializeField] private TextMeshProUGUI playerLevelText;
    [SerializeField] private Button inviteButton;

    private PlayerRef playerRef;
    private PartyUIManager partyUIManager;

    public void Setup(PlayerRef playerRef, string playerName, int playerLevel, PartyUIManager uiManager)
    {
        this.playerRef = playerRef;
        this.partyUIManager = uiManager;

        if (playerNameText != null)
            playerNameText.text = playerName;
        
        if (playerLevelText != null)
            playerLevelText.text = $"Seviye {playerLevel}";

        if (inviteButton != null)
            inviteButton.onClick.AddListener(OnInvitePressed);
    }

    private void OnInvitePressed()
    {
        if (partyUIManager != null)
        {
            partyUIManager.OnInviteButtonPressed(playerRef);
        }
    }
}