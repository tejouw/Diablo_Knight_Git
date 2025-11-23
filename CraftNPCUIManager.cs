// Path: Assets/Game/Scripts/CraftNPCUIManager.cs

using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class CraftNPCUIManager : MonoBehaviour
{
[Header("UI References")]
[SerializeField] private GameObject craftNPCPanel;
[SerializeField] private Transform recipesContainer;
[SerializeField] private GameObject recipeSlotPrefab;
[SerializeField] private GameObject materialRecipeHeader;
[SerializeField] private TextMeshProUGUI npcLocationText; // YENİ EKLEME - Inspector'da atanacak
    
    private CraftNPC currentCraftNPC;
    private List<GameObject> recipeSlots = new List<GameObject>();
    
public void Initialize(CraftNPC craftNPC)
{
    currentCraftNPC = craftNPC;
    
    // NPC ile etkileşimde location text'i gizle
    HideNPCLocationText();
    
    // NPC ile etkileşimde header'ı NPC moduna çevir
    if (materialRecipeHeader != null && craftNPC != null)
    {
        materialRecipeHeader.SetActive(true);
        TextMeshProUGUI headerText = materialRecipeHeader.GetComponent<TextMeshProUGUI>();
        if (headerText != null)
        {
            headerText.text = $"{craftNPC.NPCName}'in Tarifleri:";
        }
        materialRecipeHeader.transform.SetAsFirstSibling();
    }
    
    RefreshRecipeSlots();
}
    
private void RefreshRecipeSlots()
{
    if (currentCraftNPC == null) return;
    
    // Önceki slotları temizle
    foreach (GameObject slot in recipeSlots)
    {
        if (slot != null) Destroy(slot);
    }
    recipeSlots.Clear();
    
    // NPC modunda header zaten Initialize'da ayarlandı, sadece recipes'i göster
    var recipes = currentCraftNPC.GetAvailableRecipes();
    for (int i = 0; i < recipes.Count; i++)
    {
        CreateRecipeSlot(recipes[i], i);
    }
}
    
private void CreateRecipeSlot(CraftRecipe recipe, int index)
{
    if (recipeSlotPrefab == null || recipesContainer == null) return;
    
    GameObject slotObj = Instantiate(recipeSlotPrefab, recipesContainer);
    recipeSlots.Add(slotObj);
    
    // UI bileşenlerini bul
    Transform iconTransform = slotObj.transform.Find("ItemIcon");
    
    Image itemIcon = iconTransform?.GetComponent<Image>();
    Button slotButton = slotObj.GetComponent<Button>();
    
    // Recipe bilgilerini ayarla
    if (recipe.resultItem != null)
    {
        if (itemIcon != null) itemIcon.sprite = recipe.resultItem.itemIcon;
    }
    
    // Click eventi ekle
    if (slotButton != null)
    {
        slotButton.onClick.AddListener(() => OnRecipeSlotClicked(recipe));
    }
    
    // CraftRecipeSlot component'i sadece yoksa ekle
    CraftRecipeSlot recipeSlot = slotObj.GetComponent<CraftRecipeSlot>();
    if (recipeSlot == null)
    {
        recipeSlot = slotObj.AddComponent<CraftRecipeSlot>();
    }
    recipeSlot.Initialize(recipe, index);
}
public void InitializeForMaterialsMode()
{
    currentCraftNPC = null; // NPC yok
    
    // Materials mode'da location text'i gizle (recipe tıklanınca gösterilecek)
    HideNPCLocationText();
    
    // Materials mode'da header'ı gizle (materyale tıklanınca gösterilecek)
    if (materialRecipeHeader != null)
    {
        materialRecipeHeader.SetActive(false);
    }
    
    RefreshRecipeSlotsForMaterialsMode();
}
private void RefreshRecipeSlotsForMaterialsMode()
{
    // Önceki slotları temizle
    foreach (GameObject slot in recipeSlots)
    {
        if (slot != null) Destroy(slot);
    }
    recipeSlots.Clear();
    
    // Header'ı gizle
    if (materialRecipeHeader != null)
    {
        materialRecipeHeader.SetActive(false);
    }
}
public void ShowMaterialRecipes(List<CraftRecipe> recipes, string materialName)
{
    // Önceki slotları temizle
    foreach (GameObject slot in recipeSlots)
    {
        if (slot != null) Destroy(slot);
    }
    recipeSlots.Clear();
    
    if (recipes.Count == 0)
    {
        // Header'ı gizle
        if (materialRecipeHeader != null)
        {
            materialRecipeHeader.SetActive(false);
        }
        
        return;
    }
    
    // Materials mode'da header'ı materyale göre güncelle
    UpdateMaterialRecipeHeader(materialName, recipes.Count);
    
    // Her tarif için slot oluştur
    for (int i = 0; i < recipes.Count; i++)
    {
        CreateMaterialRecipeSlot(recipes[i], i);
    }
}


private void UpdateMaterialRecipeHeader(string materialName, int recipeCount)
{
    if (materialRecipeHeader == null) return;
    
    // Header'ı aktif et
    materialRecipeHeader.SetActive(true);
    
    // Text component'ini bul ve güncelle
    TextMeshProUGUI headerText = materialRecipeHeader.GetComponent<TextMeshProUGUI>();
    if (headerText != null)
    {
        headerText.text = $"{materialName} kullanan tarifler: ({recipeCount})";
    }
    
    // Header'ı recipes container'ın ilk sırasına taşı
    materialRecipeHeader.transform.SetAsFirstSibling();
}

private void CreateMaterialRecipeSlot(CraftRecipe recipe, int index)
{
    if (recipeSlotPrefab == null || recipesContainer == null) return;
    
    GameObject slotObj = Instantiate(recipeSlotPrefab, recipesContainer);
    recipeSlots.Add(slotObj);
    
    // UI bileşenlerini bul
    Transform iconTransform = slotObj.transform.Find("ItemIcon");
    
    Image itemIcon = iconTransform?.GetComponent<Image>();
    Button slotButton = slotObj.GetComponent<Button>();
    
    // Recipe bilgilerini ayarla
    if (recipe.resultItem != null)
    {
        if (itemIcon != null) itemIcon.sprite = recipe.resultItem.itemIcon;
    }
    
    // ÖNEMLI: CraftRecipeSlot'un OnPointerClick'ini devre dışı bırak
    CraftRecipeSlot recipeSlot = slotObj.GetComponent<CraftRecipeSlot>();
    if (recipeSlot != null)
    {
        // CraftRecipeSlot'u devre dışı bırak (materials mode için)
        recipeSlot.enabled = false;
    }
    
    // Sadece Button click eventi kullan - Materials mode için
    if (slotButton != null)
    {
        slotButton.onClick.RemoveAllListeners();
        slotButton.onClick.AddListener(() => OnMaterialRecipeClicked(recipe));
    }
    
    // CraftRecipeSlot component'ini yeniden initialize et
    if (recipeSlot == null)
    {
        recipeSlot = slotObj.AddComponent<CraftRecipeSlot>();
    }
    recipeSlot.Initialize(recipe, index);
}

    private void OnMaterialRecipeClicked(CraftRecipe recipe)
    {
        // Recipe bilgisini göster
        ItemInfoPanel itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
        if (itemInfoPanel != null)
        {
            itemInfoPanel.ShowRecipeInfoOnly(recipe);
        }

        // NPC lokasyon bilgisini göster
        UpdateNPCLocationText(recipe);
    }
private void UpdateNPCLocationText(CraftRecipe recipe)
{
    if (npcLocationText == null || RecipeManager.Instance == null) return;
    
    CraftNPC npcForRecipe = RecipeManager.Instance.GetNPCForRecipe(recipe);
    if (npcForRecipe != null)
    {
        Vector3 npcPosition = npcForRecipe.transform.position;
        npcLocationText.text = $"NPC: {npcForRecipe.NPCName}\nKoordinat: ({npcPosition.x:F1}, {npcPosition.y:F1})";
        npcLocationText.gameObject.SetActive(true);
    }
    else
    {
        npcLocationText.gameObject.SetActive(false);
    }
}

private void HideNPCLocationText()
{
    if (npcLocationText != null)
    {
        npcLocationText.gameObject.SetActive(false);
    }
}

    private void OnRecipeSlotClicked(CraftRecipe recipe)
    {
        ItemInfoPanel itemInfoPanel = FindFirstObjectByType<ItemInfoPanel>();
        if (itemInfoPanel != null)
        {
            itemInfoPanel.ShowRecipeInfo(recipe);
        }
    }
}