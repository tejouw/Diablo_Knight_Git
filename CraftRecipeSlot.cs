// Path: Assets/Game/Scripts/CraftRecipeSlot.cs

using UnityEngine;
using UnityEngine.EventSystems;

public class CraftRecipeSlot : MonoBehaviour, IPointerClickHandler
{
    private CraftRecipe recipe;
    private int slotIndex;
    
    public CraftRecipe GetRecipe() => recipe;
    public int GetSlotIndex() => slotIndex;
    
    public void Initialize(CraftRecipe craftRecipe, int index)
    {
        recipe = craftRecipe;
        slotIndex = index;
    }
    
    public void OnPointerClick(PointerEventData eventData)
    {
        if (recipe == null) return;
        
        ItemInfoPanel itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
        if (itemInfoPanel != null)
        {
            itemInfoPanel.ShowRecipeInfo(recipe);
        }
    }
}