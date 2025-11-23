// Path: Assets/Game/Scripts/RecipeManager.cs

using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RecipeManager : MonoBehaviour
{
    public static RecipeManager Instance { get; private set; }

    [Header("Craft NPCs in Scene")]
    [SerializeField] private List<CraftNPC> craftNPCs = new List<CraftNPC>();

    [Header("Auto-Collected Recipes (Read Only)")]
    [SerializeField] private List<CraftRecipe> allRecipes = new List<CraftRecipe>();

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

    private void Start()
    {
        CollectRecipesFromNPCs();
    }

    private void CollectRecipesFromNPCs()
    {
        allRecipes.Clear();
        HashSet<string> addedRecipeNames = new HashSet<string>(); // Duplicate önleme


        foreach (var craftNPC in craftNPCs)
        {
            if (craftNPC == null)
            {
                Debug.LogWarning("[RecipeManager] Found null CraftNPC in list!");
                continue;
            }

            var npcRecipes = craftNPC.GetAvailableRecipes();

            foreach (var recipe in npcRecipes)
            {
                if (recipe != null && !addedRecipeNames.Contains(recipe.recipeName))
                {
                    allRecipes.Add(recipe);
                    addedRecipeNames.Add(recipe.recipeName);

                    // Recipe'nin ingredient'larını da logla
                    foreach (var ingredient in recipe.ingredients)
                    {
                        if (ingredient.type == IngredientType.Item && ingredient.item != null)
                        {
                        }
                    }
                }
            }
        }

    }

    public List<CraftRecipe> GetRecipesUsingItem(string itemId)
    {

        if (allRecipes.Count == 0)
        {
            Debug.LogWarning("[RecipeManager] No recipes available! Did CollectRecipesFromNPCs run?");
            return new List<CraftRecipe>();
        }

        List<CraftRecipe> foundRecipes = new List<CraftRecipe>();

        foreach (var recipe in allRecipes)
        {
            foreach (var ingredient in recipe.ingredients)
            {
                if (ingredient.type == IngredientType.Item &&
                    ingredient.item != null &&
                    ingredient.item.itemId == itemId)
                {
                    foundRecipes.Add(recipe);
                    break; // Aynı tarifi birden fazla kez ekleme
                }
            }
        }

        return foundRecipes;
    }

    // NPC'nin sahip olduğu tarifleri al
    public List<CraftRecipe> GetRecipesForNPC(List<CraftRecipe> npcRecipes)
    {
        return npcRecipes.Where(npcRecipe =>
            allRecipes.Any(globalRecipe => globalRecipe.recipeName == npcRecipe.recipeName)
        ).ToList();
    }

    // Tüm tarifleri al
    public List<CraftRecipe> GetAllRecipes()
    {
        return new List<CraftRecipe>(allRecipes);
    }

    // Runtime'da yeni NPC ekleme (isteğe bağlı)
    public void AddCraftNPC(CraftNPC newNPC)
    {
        if (newNPC != null && !craftNPCs.Contains(newNPC))
        {
            craftNPCs.Add(newNPC);

            // Yeni NPC'nin tariflerini ekle
            var npcRecipes = newNPC.GetAvailableRecipes();
            foreach (var recipe in npcRecipes)
            {
                if (recipe != null && !allRecipes.Any(r => r.recipeName == recipe.recipeName))
                {
                    allRecipes.Add(recipe);
                }
            }
        }
    }

    // Inspector'da manuel refresh için
    [ContextMenu("Refresh Recipes from NPCs")]
    private void RefreshRecipes()
    {
        CollectRecipesFromNPCs();
    }

    // Scene'deki tüm CraftNPC'leri otomatik bul (isteğe bağlı)
    [ContextMenu("Auto-Find CraftNPCs in Scene")]
    private void AutoFindCraftNPCs()
    {
        craftNPCs.Clear();
        CraftNPC[] foundNPCs = FindObjectsByType<CraftNPC>(FindObjectsSortMode.None);
        craftNPCs.AddRange(foundNPCs);


        // Tarifleri de hemen topla
        CollectRecipesFromNPCs();
    }
    public CraftNPC GetNPCForRecipe(CraftRecipe targetRecipe)
{
    if (targetRecipe == null) return null;
    
    foreach (var craftNPC in craftNPCs)
    {
        if (craftNPC == null) continue;
        
        var npcRecipes = craftNPC.GetAvailableRecipes();
        foreach (var recipe in npcRecipes)
        {
            if (recipe != null && recipe.recipeName == targetRecipe.recipeName)
            {
                return craftNPC;
            }
        }
    }
    
    return null;
}
    [ContextMenu("Debug Current State")]
private void DebugCurrentState()
{
    
    foreach (var recipe in allRecipes)
    {
        foreach (var ingredient in recipe.ingredients)
        {
            if (ingredient.type == IngredientType.Item && ingredient.item != null)
            {
            }
        }
    }
}
}
