// Path: Assets/Game/Scripts/CraftNPC.cs

using UnityEngine;
using System.Collections.Generic;

public class CraftNPC : BaseNPC
{
    [Header("Craft Settings")]
    [SerializeField] private List<CraftRecipe> availableRecipes = new List<CraftRecipe>();
    
    private bool isCraftPanelOpen = false;
    public bool IsCraftPanelOpen => isCraftPanelOpen;
    
    public List<CraftRecipe> GetAvailableRecipes()
    {
        return new List<CraftRecipe>(availableRecipes);
    }
    
    public override void HandleNPCTypeInteraction()
    {
        GameObject gameUI = GameObject.Find("GameUI");
        if (gameUI != null)
        {
            Transform infoPanelManagerTransform = gameUI.transform.Find("InfoPanelManager");
            if (infoPanelManagerTransform != null)
            {
                InfoPanelManager infoPanelManager = infoPanelManagerTransform.GetComponent<InfoPanelManager>();
                if (infoPanelManager != null)
                {
                    isCraftPanelOpen = true;
                    infoPanelManager.ShowCraftNPCPanels();
                }
            }
        }
    }
    protected override void Awake()
{
    // Base awake'i çağır
    base.Awake();
    
    // NPC türünü Craft olarak ayarla
    npcType = NPCType.Craft;
}
    protected override void CheckPlayerDistance()
    {
        bool wasInRange = isPlayerInRange;
        base.CheckPlayerDistance();

        if (wasInRange && !isPlayerInRange && isCraftPanelOpen)
        {
            CloseCraftPanel();
        }
    }
    
    public void CloseCraftPanel()
    {
        isCraftPanelOpen = false;
        
        // ItemInfoPanel'i craft modundan çıkar
        var itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
        if (itemInfoPanel != null)
        {
            itemInfoPanel.SetCraftMode(false);
            itemInfoPanel.ClosePanel();
        }
    }
}