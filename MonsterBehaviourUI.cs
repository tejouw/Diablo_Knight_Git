// Path: Assets/Game/Scripts/MonsterBehaviourUI.cs

using UnityEngine;
using Fusion;
using System.Collections;
using Assets.FantasyMonsters.Common.Scripts;
using System;
using System.Collections.Generic;


public class MonsterBehaviourUI : NetworkBehaviour
{
    #region REFERENCES
    private MonsterBehaviour monsterBehaviour;
    private Monster fantasyMonster;
    private SpriteRenderer[] spriteRenderers;
    private MonsterHealthUI healthUI;
    private Dictionary<SpriteRenderer, Color> originalColors = new Dictionary<SpriteRenderer, Color>();
    #endregion

    #region VISUAL EFFECTS DATA
    [Header("Visual Effects")]
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField] private Color criticalFlashColor = new Color(1f, 0.8f, 0.1f, 1f);
   
    private Color originalColor;
    private string[] originalSortingLayers;
    private int[] originalSortingOrders;
    private bool sortingValuesStored = false;
    private Coroutine flashCoroutine;
    private Coroutine shakeCoroutine;
    #endregion

    #region NETWORK PROPERTIES
    [Networked] public bool NetworkIsFlashing { get; set; }
    [Networked] public float NetworkFlashStartTime { get; set; }
    #endregion

    #region INITIALIZATION
    private void Awake()
    {
        monsterBehaviour = GetComponent<MonsterBehaviour>();
        fantasyMonster = GetComponent<Monster>();
        healthUI = GetComponent<MonsterHealthUI>();
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        
        if (spriteRenderers.Length > 0)
        {
            originalColor = spriteRenderers[0].color;
        }
    }

    public override void Spawned()
    {
        if (spriteRenderers.Length > 0)
        {
            StoreSortingValues();
        }
    }
    #endregion

    #region HEAD SPRITE MANAGEMENT
    public void UpdateHeadSprite(MonsterState state)
    {
        if (fantasyMonster == null) return;
        
        try
        {
            switch (state)
            {
                case MonsterState.Run:
                case MonsterState.Ready:
                    fantasyMonster.SetHead(1); // HeadAngry
                    break;
                    
                case MonsterState.Idle:
                case MonsterState.Walk:
                    fantasyMonster.SetHead(0); // HeadNormal
                    break;
                    
                case MonsterState.Death:
                    fantasyMonster.SetHead(2); // HeadDead
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MonsterBehaviourUI] Error updating head sprite: {e.Message}");
        }
    }
    #endregion
#region COLOR & RARITY MANAGEMENT
public void ApplyColorToAllSprites(Color tintColor)
{
    SpriteRenderer[] allSprites = GetComponentsInChildren<SpriteRenderer>(true);
    
    foreach (SpriteRenderer sprite in allSprites)
    {
        if (sprite != null)
        {
            sprite.color = tintColor;
            
            // Original color dictionary'ye de kaydet
            if (!originalColors.ContainsKey(sprite))
            {
                originalColors[sprite] = tintColor;
            }
            else
            {
                originalColors[sprite] = tintColor;
            }
        }
    }
}

public static Color GetRarityTintColor(MonsterRarity rarity)
{
    switch (rarity)
    {
        case MonsterRarity.Magic:
            return new Color(0.5f, 0.8f, 1f, 1f);
        case MonsterRarity.Rare:
            return new Color(1f, 0.8f, 0.2f, 1f);
        default:
            return Color.white;
    }
}

public void ApplyRarityVisuals(MonsterRarity rarity)
{
    Color rarityColor = GetRarityTintColor(rarity);
    ApplyColorToAllSprites(rarityColor);
}

public void ApplyVisualRotation(float yRotation)
{
    Vector3 currentRotation = transform.eulerAngles;
    currentRotation.y = yRotation;
    transform.rotation = Quaternion.Euler(currentRotation);
}
#endregion
    #region HEALTH UI MANAGEMENT
    public void UpdateHealthUI(float currentHealth, float maxHealth)
    {
        if (healthUI != null)
        {
            //DUZELTILMELI! // healthUI.UpdateHealth(currentHealth, maxHealth);
        }
    }

    public void SyncHealthUI(float newHealth, float maxHealth)
    {
        UpdateHealthUI(newHealth, maxHealth);
    }
    #endregion

    #region VISUAL EFFECTS SYSTEM
public void TriggerHitEffect(bool isCritical = false)
{
    if (!Object.HasStateAuthority) return;

    // Eğer zaten flash çalışıyorsa, interrupt et
    if (NetworkIsFlashing)
    {
        return; // Concurrent flash'ları engelle
    }

    NetworkIsFlashing = true;
    NetworkFlashStartTime = (float)Runner.SimulationTime;
    
    RPC_ApplyHitEffects(isCritical);
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_ApplyHitEffects(bool isCritical)
{
    // Mevcut flash'ı güvenli şekilde durdur
    if (flashCoroutine != null)
    {
        StopCoroutine(flashCoroutine);
        RestoreVisualState(); // Güvenli restore
    }
    
    if (!sortingValuesStored)
    {
        StoreSortingValues();
    }
    
    flashCoroutine = StartCoroutine(FlashEffect(isCritical));
}

    private IEnumerator FlashEffect(bool isCritical)
    {
        // Original state'i kaydet
        StoreCurrentVisualState();

        Color flashCol = isCritical ? criticalFlashColor : flashColor;

        // Flash color uygula
        ApplyFlashColor(flashCol);

        // Sorting layer'ı UI'ye çek
        ApplySortingLayerOverride();

        yield return new WaitForSeconds(flashDuration);

        // Original state'i restore et
        RestoreVisualState();

        // Network state temizle (sadece server'da)
        if (Object.HasStateAuthority)
        {
            NetworkIsFlashing = false;
        }

        flashCoroutine = null;
    }
    private void StoreCurrentVisualState()
    {
        // Mevcut renkleri kaydet - sadece ilk kez için
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                // Eğer daha önce kaydedilmemişse, o anki rengi kaydet
                if (!originalColors.ContainsKey(spriteRenderers[i]))
                {
                    // Monster initialize edilmişse rarity rengini kullan
                    if (monsterBehaviour != null && monsterBehaviour.Rarity != MonsterRarity.Normal)
                    {
                        Color rarityColor = GetRarityColor(monsterBehaviour.Rarity);
                        originalColors[spriteRenderers[i]] = rarityColor;
                    }
                    else
                    {
                        originalColors[spriteRenderers[i]] = spriteRenderers[i].color;
                    }
                }
            }
        }
    }
private Color GetRarityColor(MonsterRarity rarity)
{
    switch (rarity)
    {
        case MonsterRarity.Magic:
            return new Color(0.5f, 0.8f, 1f, 1f);
        case MonsterRarity.Rare:
            return new Color(1f, 0.8f, 0.2f, 1f);
        default:
            return Color.white;
    }
}
    private void ApplyFlashColor(Color flashColor)
    {
        foreach (var renderer in spriteRenderers)
        {
            if (renderer != null)
            {
                renderer.color = flashColor;
            }
        }
    }

private void ApplySortingLayerOverride()
{
    foreach (var renderer in spriteRenderers)
    {
        if (renderer != null)
        {
            renderer.sortingLayerName = "UI";
            renderer.sortingOrder = 10;
        }
    }
}

private void RestoreVisualState()
{
    // Renkleri restore et
    foreach (var kvp in originalColors)
    {
        if (kvp.Key != null)
        {
            kvp.Key.color = kvp.Value;
        }
    }
    
    // Sorting values'ları restore et
    RestoreSortingValues();
}

public override void Render()
{
    // Sadece kritik flash state değişikliklerini handle et
    if (!Runner.IsServer)
    {
        bool shouldBeFlashing = NetworkIsFlashing;
        bool isCurrentlyFlashing = flashCoroutine != null;
        
        // Sadece state değiştiğinde action al
        if (shouldBeFlashing && !isCurrentlyFlashing)
        {
            if (!sortingValuesStored)
            {
                StoreSortingValues();
            }
            flashCoroutine = StartCoroutine(FlashEffect(false));
        }
        else if (!shouldBeFlashing && isCurrentlyFlashing)
        {
            StopCoroutine(flashCoroutine);
            RestoreVisualState();
            flashCoroutine = null;
        }
    }
}
    #endregion

    #region SORTING VALUES MANAGEMENT
    private void StoreSortingValues()
    {
        if (sortingValuesStored) return;
        
        originalSortingLayers = new string[spriteRenderers.Length];
        originalSortingOrders = new int[spriteRenderers.Length];
        
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                originalSortingLayers[i] = spriteRenderers[i].sortingLayerName;
                originalSortingOrders[i] = spriteRenderers[i].sortingOrder;
            }
        }
        
        sortingValuesStored = true;
    }

    private void RestoreSortingValues()
    {
        if (!sortingValuesStored) return;
        
        for (int i = 0; i < spriteRenderers.Length && i < originalSortingLayers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].sortingLayerName = originalSortingLayers[i];
                spriteRenderers[i].sortingOrder = originalSortingOrders[i];
            }
        }
    }
    #endregion



    #region CLEANUP
private void OnDestroy()
{
    if (flashCoroutine != null)
    {
        StopCoroutine(flashCoroutine);
        RestoreVisualState(); // Güvenli cleanup
    }
    
    if (shakeCoroutine != null)
    {
        StopCoroutine(shakeCoroutine);
    }
    
    // Dictionary'yi temizle
    originalColors.Clear();
}
    #endregion
}