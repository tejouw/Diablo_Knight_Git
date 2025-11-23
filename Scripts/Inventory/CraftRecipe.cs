// Path: Assets/Game/Scripts/CraftRecipe.cs

using UnityEngine;
using System.Collections.Generic;

public enum IngredientType
{
    Item,      // CraftInventory'den item
    Gold,      // PlayerStats.Coins'den
    Potion     // PlayerStats.PotionCount'dan
}

[System.Serializable]
public class CraftIngredient
{
    public IngredientType type;
    public ItemData item;           // type = Item ise gerekli
    public int amount;             // Gerekli miktar
    
    [Header("Display Info")]
    public string displayName;     // Gold/Potion için görünen isim
    
    public string GetDisplayName()
    {
        switch (type)
        {
            case IngredientType.Item:
                return item != null ? item.itemName : "Unknown Item";
            case IngredientType.Gold:
                return "Gold";
            case IngredientType.Potion:
                return "Health Potion";
            default:
                return displayName;
        }
    }
}

[System.Serializable]
public class CraftRecipe
{
    [Header("Recipe Info")]
    public string recipeName;
    public ItemData resultItem;                    // Üretilecek item
    public int resultAmount = 1;                   // Üretilecek miktar
    
    [Header("Required Ingredients")]
    public List<CraftIngredient> ingredients = new List<CraftIngredient>();
    
    public bool CanCraft(CraftInventorySystem craftInventory, PlayerStats playerStats)
    {
        foreach (var ingredient in ingredients)
        {
            if (!HasEnoughIngredient(ingredient, craftInventory, playerStats))
            {
                return false;
            }
        }
        return true;
    }
    
    private bool HasEnoughIngredient(CraftIngredient ingredient, CraftInventorySystem craftInventory, PlayerStats playerStats)
    {
        switch (ingredient.type)
        {
            case IngredientType.Item:
                return craftInventory.HasItem(ingredient.item.itemId, ingredient.amount);
                
            case IngredientType.Gold:
                return playerStats.Coins >= ingredient.amount;
                
            case IngredientType.Potion:
                return playerStats.PotionCount >= ingredient.amount;
                
            default:
                return false;
        }
    }
}