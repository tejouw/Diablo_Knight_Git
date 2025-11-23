// Path: Assets/Game/Scripts/CraftSystem.cs

using UnityEngine;
using Fusion;

public class CraftSystem : NetworkBehaviour
{
    private PlayerStats playerStats;
    private CraftInventorySystem craftInventory;
    private InventorySystem inventory;
    
    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        craftInventory = GetComponent<CraftInventorySystem>();
        inventory = GetComponent<InventorySystem>();
    }
    
    private CraftRecipe pendingRecipe; // Bekleyen craft recipe

    public void RequestCraft(CraftRecipe recipe)
    {

        if (!Object.HasInputAuthority)
        {
            Debug.LogError("[CraftSystem] No InputAuthority! Cannot craft.");
            return;
        }

        if (recipe == null || recipe.resultItem == null)
        {
            Debug.LogError($"[CraftSystem] Recipe or resultItem is NULL! recipe={recipe}, resultItem={recipe?.resultItem}");
            return;
        }

        // Client-side validation
        if (!recipe.CanCraft(craftInventory, playerStats))
        {
            ChatManager.Instance?.ShowCraftFailedMessage("Insufficient materials!");
            return;
        }

        if (!inventory.HasEmptySlot())
        {
            ChatManager.Instance?.ShowInventoryFullMessage();
            return;
        }

        // Recipe'yi sakla - server onaylarsa tüketeceğiz
        pendingRecipe = recipe;

        // Server'a craft isteği gönder
        // Gold değerini de gönder (NetworkCoins sync sorunu için)
        string serialized = SerializeIngredients(recipe.ingredients);
        int clientCoins = playerStats.Coins;
        RPC_RequestCraft(recipe.resultItem.itemId, recipe.resultAmount, serialized, clientCoins);
    }

    // Sadece Item tipindeki malzemeleri tüketir (client-side)
    private bool ConsumeItemIngredients(System.Collections.Generic.List<CraftIngredient> ingredients)
    {
        foreach (var ingredient in ingredients)
        {
            if (ingredient.type == IngredientType.Item)
            {
                if (!craftInventory.ConsumeItems(ingredient.item.itemId, ingredient.amount))
                {
                    Debug.LogError($"[CraftSystem] Failed to consume item: {ingredient.item.itemId} x{ingredient.amount}");
                    return false;
                }
            }
        }
        return true;
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_RequestCraft(string resultItemId, int resultAmount, string serializedIngredients, int clientCoins)
    {

        // Server validation
        CraftRecipe serverRecipe = DeserializeRecipe(resultItemId, resultAmount, serializedIngredients);
        if (serverRecipe == null)
        {
            Debug.LogError("[CraftSystem-Server] DeserializeRecipe returned NULL!");
            return;
        }

        // Gold ve Potion için validasyon (client'tan gelen coin değerini kullan)
        if (!CanCraftGoldAndPotionsWithClientCoins(serverRecipe, clientCoins))
        {
            RPC_CraftResult(false, "Yetersiz altın veya iksir!");
            return;
        }

        if (!inventory.HasEmptySlot())
        {
            RPC_CraftResult(false, "Envanter dolu!");
            return;
        }

        // Sonuç itemını üret ve inventory'ye ekle
        ItemData resultItem = ItemDatabase.Instance.GetItemById(resultItemId);
        if (resultItem != null)
        {
            ItemData craftedItem = resultItem.CreateExactCopy();
            bool addSuccess = inventory.TryAddItem(craftedItem, resultAmount);

            if (addSuccess)
            {
                RPC_CraftResult(true, $"Successfully crafted {craftedItem.itemName}!");
            }
            else
            {
                Debug.LogError("[CraftSystem-Server] TryAddItem failed!");
                RPC_CraftResult(false, "Failed to add item to inventory!");
            }
        }
        else
        {
            Debug.LogError($"[CraftSystem-Server] Result item not found in database! itemId={resultItemId}");
            RPC_CraftResult(false, "Result item not found!");
        }
    }

    // Client'tan gelen coin değeriyle validasyon
    private bool CanCraftGoldAndPotionsWithClientCoins(CraftRecipe recipe, int clientCoins)
    {
        foreach (var ingredient in recipe.ingredients)
        {
            switch (ingredient.type)
            {
                case IngredientType.Gold:
                    if (clientCoins < ingredient.amount)
                    {
                        return false;
                    }
                    break;
                case IngredientType.Potion:
                    if (playerStats.PotionCount < ingredient.amount)
                    {
                        return false;
                    }
                    break;
            }
        }
        return true;
    }

    // Server-side: Sadece Gold ve Potion kontrolü
    private bool CanCraftGoldAndPotions(CraftRecipe recipe, PlayerStats stats)
    {

        foreach (var ingredient in recipe.ingredients)
        {
            switch (ingredient.type)
            {
                case IngredientType.Gold:
                    // Direkt NetworkCoins'i kontrol et
                    int serverGold = stats.NetworkCoins;
                    if (serverGold < ingredient.amount)
                    {
                        return false;
                    }
                    break;
                case IngredientType.Potion:
                    if (stats.PotionCount < ingredient.amount)
                    {
                        return false;
                    }
                    break;
                // Item tipini kontrol etmiyoruz - client zaten tüketti
            }
        }
        return true;
    }

    // Server-side: Sadece Gold ve Potion tüket
    private bool ConsumeGoldAndPotions(System.Collections.Generic.List<CraftIngredient> ingredients)
    {
        foreach (var ingredient in ingredients)
        {
            switch (ingredient.type)
            {
                case IngredientType.Gold:
                    if (playerStats.Coins < ingredient.amount) return false;
                    playerStats.AddCoins(-ingredient.amount);
                    break;

                case IngredientType.Potion:
                    if (playerStats.PotionCount < ingredient.amount) return false;
                    for (int i = 0; i < ingredient.amount; i++)
                    {
                        playerStats.ConsumePotionForCraft();
                    }
                    break;

                // Item tipini tüketmiyoruz - client zaten tüketti
            }
        }
        return true;
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void RPC_CraftResult(bool success, string message)
    {
        if (success)
        {

            // Başarılı - şimdi malzemeleri tüket
            if (pendingRecipe != null)
            {
                // Item malzemelerini tüket
                bool itemsConsumed = ConsumeItemIngredients(pendingRecipe.ingredients);
                if (itemsConsumed)
                {
                }

                // Gold ve Potion tüket
                foreach (var ingredient in pendingRecipe.ingredients)
                {
                    switch (ingredient.type)
                    {
                        case IngredientType.Gold:
                            playerStats.AddCoins(-ingredient.amount);
                            break;
                        case IngredientType.Potion:
                            for (int i = 0; i < ingredient.amount; i++)
                            {
                                playerStats.ConsumePotionForCraft();
                            }
                            break;
                    }
                }

                pendingRecipe = null;
            }

            ChatManager.Instance?.ShowCraftSuccessMessage(message);

            // ItemInfoPanel'i kapat
            ItemInfoPanel itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
            if (itemInfoPanel != null)
            {
                itemInfoPanel.ClosePanel();
            }
        }
        else
        {
            pendingRecipe = null; // Başarısız, recipe'yi temizle
            ChatManager.Instance?.ShowCraftFailedMessage(message);
        }
    }
    
    
    private string SerializeIngredients(System.Collections.Generic.List<CraftIngredient> ingredients)
    {
        // Basit serialization - JSON kullanabilirsin
        string result = "";
        foreach (var ingredient in ingredients)
        {
            string itemId = ingredient.item != null ? ingredient.item.itemId : "";
            result += $"{(int)ingredient.type}|{itemId}|{ingredient.amount};";
        }
        return result;
    }
    
    private CraftRecipe DeserializeRecipe(string resultItemId, int resultAmount, string serializedIngredients)
    {
        ItemData resultItem = ItemDatabase.Instance.GetItemById(resultItemId);
        if (resultItem == null) return null;
        
        CraftRecipe recipe = new CraftRecipe();
        recipe.resultItem = resultItem;
        recipe.resultAmount = resultAmount;
        recipe.ingredients = new System.Collections.Generic.List<CraftIngredient>();
        
        string[] ingredientStrings = serializedIngredients.Split(';');
        foreach (string ingredientStr in ingredientStrings)
        {
            if (string.IsNullOrEmpty(ingredientStr)) continue;
            
            string[] parts = ingredientStr.Split('|');
            if (parts.Length != 3) continue;
            
            CraftIngredient ingredient = new CraftIngredient();
            ingredient.type = (IngredientType)int.Parse(parts[0]);
            if (!string.IsNullOrEmpty(parts[1]))
            {
                ingredient.item = ItemDatabase.Instance.GetItemById(parts[1]);
            }
            ingredient.amount = int.Parse(parts[2]);
            
            recipe.ingredients.Add(ingredient);
        }
        
        return recipe;
    }
}