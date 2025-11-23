// Path: Assets/Game/Scripts/DroppedLootUI.cs

using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class DroppedLootUI : MonoBehaviour
{
    #region SERIALIZED REFERENCES (Inspector'dan bağlanacak)
    [Header("Visual References")]
    [SerializeField] private GameObject visualContainer;
    [SerializeField] private SpriteRenderer itemIconRenderer;
    [SerializeField] private GameObject shadowObject;
    [SerializeField] private TextMeshPro lootText;
    [SerializeField] private SpriteRenderer textBackground;
    #endregion

    #region VISUAL SETTINGS
    [Header("Text Settings")]
    [SerializeField] private TMP_FontAsset goldFont;
    [SerializeField] private TMP_FontAsset itemFont;
    [SerializeField] private Color goldTextColor = Color.yellow;
    
    [Header("Gold Sprites")]
    [SerializeField] private Sprite smallGoldBagSprite;
    [SerializeField] private Sprite mediumGoldBagSprite;
    [SerializeField] private Sprite largeGoldBagSprite;
    
    [Header("Animation Settings")]
    [SerializeField] private float floatHeight = 0.07f;
    [SerializeField] private float floatSpeed = 0.75f;
    [SerializeField] private bool useFloatAnimation = true;
    
    [Header("Shadow Settings")]
    [SerializeField] private float shadowScale = 0.7f;
    
    [Header("Scale Settings")]
    [SerializeField] private Vector3 itemScale = new Vector3(0.25f, 0.25f, 1f);
    [SerializeField] private Vector3 craftItemScale = new Vector3(0.3f, 0.3f, 1f);
    [SerializeField] private Vector3 goldScale = new Vector3(0.2f, 0.2f, 1f);
    [SerializeField] private Vector3 potionScale = new Vector3(0.2f, 0.2f, 1f);
    #endregion

    #region PRIVATE FIELDS
    private DroppedLoot droppedLoot;
    private Coroutine floatAnimationCoroutine;
    #endregion

    #region INITIALIZATION
    public void Initialize()
    {
        droppedLoot = GetComponent<DroppedLoot>();
        if (droppedLoot == null)
        {
            Debug.LogError($"[DroppedLootUI-{GetInstanceID()}] DroppedLoot component not found!");
            return;
        }

        // ✅ Validation - Prefab references
        if (!ValidateReferences())
        {
            Debug.LogError($"[DroppedLootUI-{GetInstanceID()}] Missing prefab references! Check Inspector.");
            return;
        }

        // ✅ Float animation başlat
        if (useFloatAnimation)
        {
            StartFloatAnimation();
        }

        // ✅ Instant authorization check (NO COROUTINE)
        if (IsAuthorizedToSeeVisuals() && HasValidData())
        {
            RefreshAllVisuals();
        }
        else
        {
            HideAllVisuals();
        }
    }

    private bool ValidateReferences()
    {
        bool isValid = true;

        if (visualContainer == null)
        {
            Debug.LogError("[DroppedLootUI] visualContainer reference missing!");
            isValid = false;
        }
        if (itemIconRenderer == null)
        {
            Debug.LogError("[DroppedLootUI] itemIconRenderer reference missing!");
            isValid = false;
        }
        if (shadowObject == null)
        {
            Debug.LogError("[DroppedLootUI] shadowObject reference missing!");
            isValid = false;
        }
        if (lootText == null)
        {
            Debug.LogError("[DroppedLootUI] lootText reference missing!");
            isValid = false;
        }
        if (textBackground == null)
        {
            Debug.LogError("[DroppedLootUI] textBackground reference missing!");
            isValid = false;
        }

        return isValid;
    }

    private bool HasValidData()
    {
        if (droppedLoot == null) return false;
        
        int coinAmount = droppedLoot.GetCoinAmount();
        int itemCount = droppedLoot.GetDroppedItems()?.Count ?? 0;
        
        return coinAmount > 0 || itemCount > 0;
    }

    private bool IsAuthorizedToSeeVisuals()
    {
        if (droppedLoot == null) return false;
        if (droppedLoot.Runner == null || !droppedLoot.Runner.IsRunning) return false;
        
        for (int i = 0; i < droppedLoot.AuthorizedPlayers.Length; i++)
        {
            if (droppedLoot.AuthorizedPlayers[i] == droppedLoot.Runner.LocalPlayer)
            {
                return true;
            }
        }
        
        return false;
    }

    private void RefreshAllVisuals()
    {
        if (droppedLoot != null)
        {
            UpdateVisuals(droppedLoot.GetCoinAmount(), 0, droppedLoot.GetDroppedItems());
        }
    }
    #endregion

    #region VISUAL UPDATES
    public void UpdateVisuals(int coinAmount, int potionAmount, List<ItemData> items)
    {
        UpdateText(coinAmount, potionAmount, items);

        if (coinAmount > 0)
        {
            UpdateGoldVisuals(coinAmount);
        }
        else if (potionAmount > 0)
        {
            UpdatePotionVisuals();
        }
        else if (items != null && items.Count > 0 && items[0] != null)
        {
            UpdateItemVisuals(items[0]);
        }
        else
        {
            HideAllVisuals();
        }

        bool shouldShowShadow = coinAmount > 0 || potionAmount > 0 || (items != null && items.Count > 0 && items[0] != null);
        UpdateShadowVisibility(shouldShowShadow);
    }

    private void UpdateText(int coinAmount, int potionAmount, List<ItemData> items)
    {
        if (lootText == null) return;

        if (coinAmount > 0)
        {
            lootText.text = $"{coinAmount} Altın";
            lootText.color = goldTextColor;
            if (goldFont != null) lootText.font = goldFont;
        }
        else if (potionAmount > 0)
        {
            lootText.text = "Can İksiri";
            lootText.color = Color.red;
            if (itemFont != null) lootText.font = itemFont;
        }
        else if (items.Count > 0 && items[0] != null)
        {
            var item = items[0];
            Color textColor = GetRarityColor(item.Rarity);
            lootText.text = item.GetDisplayName();
            lootText.color = textColor;
            if (itemFont != null) lootText.font = itemFont;
        }
        else
        {
            lootText.text = "";
        }
    }

    private Color GetRarityColor(GameItemRarity rarity)
    {
        return rarity switch
        {
            GameItemRarity.Magic => new Color(0, 0.5f, 1f),
            GameItemRarity.Rare => new Color(0.8f, 0.2f, 0.8f),
            _ => Color.white
        };
    }

    private void UpdateGoldVisuals(int amount)
    {
        if (itemIconRenderer == null) return;
        
        Sprite goldSprite;
        Vector3 scale;
        
        if (amount < 100)
        {
            goldSprite = smallGoldBagSprite;
            scale = goldScale * 1f;
        }
        else if (amount < 500)
        {
            goldSprite = mediumGoldBagSprite;
            scale = goldScale * 1.5f;
        }
        else
        {
            goldSprite = largeGoldBagSprite;
            scale = goldScale * 2f;
        }
        
        itemIconRenderer.sprite = goldSprite;
        itemIconRenderer.transform.localScale = scale;
        itemIconRenderer.color = Color.yellow;
        itemIconRenderer.enabled = true;
    }

    private void UpdatePotionVisuals()
    {
        if (itemIconRenderer == null) return;
        
        Sprite potionSprite = Resources.Load<Sprite>("Items/HealthPotion");
        itemIconRenderer.sprite = potionSprite;
        itemIconRenderer.transform.localScale = potionScale;
        itemIconRenderer.enabled = true;
    }

private void UpdateItemVisuals(ItemData item)
{
    if (itemIconRenderer == null || item == null) return;
    
    if (item.itemIcon == null)
    {
        // Fallback sprite
        Sprite fallbackSprite = Resources.Load<Sprite>("Sprites/DefaultItem");
        itemIconRenderer.sprite = fallbackSprite;
    }
    else
    {
        itemIconRenderer.sprite = item.itemIcon;
    }
    
    // YENİ - Collectible için scale ayarla
    Vector3 scaleToUse;
    if (item.IsCollectible())
    {
        scaleToUse = new Vector3(0.5f, 0.5f, 1f);
    }
    else
    {
        scaleToUse = item.IsCraftItem() ? craftItemScale : itemScale;
    }
    
    itemIconRenderer.transform.localScale = scaleToUse;
    itemIconRenderer.enabled = true;
}

    private void HideAllVisuals()
    {
        if (itemIconRenderer != null)
        {
            itemIconRenderer.enabled = false;
        }
    }

    private void UpdateShadowVisibility(bool hasLoot)
    {
        if (shadowObject != null)
        {
            shadowObject.SetActive(hasLoot);
        }
    }

    public void ForceRefreshVisuals()
    {
        if (droppedLoot != null)
        {
            UpdateVisuals(droppedLoot.GetCoinAmount(), 0, droppedLoot.GetDroppedItems());
        }
    }
    #endregion

    #region ANIMATION
    private void StartFloatAnimation()
    {
        if (floatAnimationCoroutine != null)
        {
            StopCoroutine(floatAnimationCoroutine);
        }
        
        floatAnimationCoroutine = StartCoroutine(FloatAnimation());
    }

    private IEnumerator FloatAnimation()
    {
        if (!useFloatAnimation || visualContainer == null) yield break;
        
        float time = 0f;
        float randomOffset = Random.Range(0f, 2f * Mathf.PI);
        
        while (true)
        {
            time += Time.deltaTime * floatSpeed;
            float yOffset = Mathf.Sin(time + randomOffset) * floatHeight;
            
            if (itemIconRenderer != null)
            {
                itemIconRenderer.transform.localPosition = new Vector3(0, 0.1f + yOffset, 0);
            }
            
            UpdateShadowScale(yOffset);
            yield return null;
        }
    }

    private void UpdateShadowScale(float yOffset)
    {
        if (shadowObject == null) return;
        
        float shadowScaleMultiplier = 1f - (yOffset / (floatHeight * 3f));
        float adjustedScale = shadowScale * 0.33f;
        shadowObject.transform.localScale = new Vector3(
            adjustedScale * shadowScaleMultiplier,
            adjustedScale * 0.3f * shadowScaleMultiplier,
            1f
        );
    }
    #endregion

    #region CLEANUP
    public void Cleanup()
    {
        if (floatAnimationCoroutine != null)
        {
            StopCoroutine(floatAnimationCoroutine);
            floatAnimationCoroutine = null;
        }
    }
    #endregion
}

public class LookAtCamera : MonoBehaviour
{
    private Camera mainCamera;
    
    private void Start()
    {
        mainCamera = Camera.main;
    }
    
    private void LateUpdate()
    {
        if (mainCamera != null)
        {
            transform.forward = mainCamera.transform.forward;
        }
    }
}