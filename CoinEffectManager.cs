// Path: Assets/Game/Scripts/CoinEffectManager.cs

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Fusion;

public class CoinEffectManager : MonoBehaviour
{
    [Header("Coin Effect Settings")]
    [SerializeField] private Transform coinTargetPosition; // Sağ üst köşedeki coin text
    [SerializeField] private float animationDuration = 1f;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Coin Sprites")]
    [SerializeField] private Sprite smallGoldSprite;
    [SerializeField] private Sprite mediumGoldSprite;
    [SerializeField] private Sprite largeGoldSprite;
    
    private Camera mainCamera;
    private Canvas uiCanvas;
    private List<Coroutine> activeCoinAnimations = new List<Coroutine>();
    
    public static CoinEffectManager Instance { get; private set; }
    
private void Awake()
{
    // Singleton pattern - ama DontDestroyOnLoad kullanma
    if (Instance == null)
    {
        Instance = this;
    }
    else
    {
        Destroy(gameObject);
        return;
    }
    
    mainCamera = Camera.main;
    
    // UI Canvas'ı daha doğru şekilde bul
    uiCanvas = FindUICanvas();
    
    // Coin target position'ı otomatik bul
    if (coinTargetPosition == null)
    {
        FindCoinTargetPosition();
    }
}

// Yeni metod ekle
private Canvas FindUICanvas()
{
    // Önce bu objenin parent'larında canvas ara
    Canvas parentCanvas = GetComponentInParent<Canvas>();
    if (parentCanvas != null)
    {
        return parentCanvas;
    }
    
    // Bulamazsa scene'deki ilk Canvas'ı al
    Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
    foreach (Canvas canvas in canvases)
    {
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return canvas;
        }
    }
    
    // Hala bulamazsa herhangi bir Canvas
    if (canvases.Length > 0)
    {
        return canvases[0];
    }
    
    Debug.LogError("[CoinEffectManager] No UI Canvas found!");
    return null;
}
    
    private void FindCoinTargetPosition()
    {
        // Coin text'ini manuel olarak bul - deprecated warning düzeltildi
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains("Coin") && obj.GetComponent<Text>() != null)
            {
                coinTargetPosition = obj.transform;
                break;
            }
        }
        
        // Bulamazsa sağ üst köşeye manuel pozisyon ayarla
        if (coinTargetPosition == null)
        {
            GameObject targetObj = new GameObject("CoinTargetPosition");
            targetObj.transform.SetParent(transform.parent); // Parent'a ekle
            RectTransform targetRect = targetObj.AddComponent<RectTransform>();
            targetRect.anchorMin = new Vector2(1, 1);
            targetRect.anchorMax = new Vector2(1, 1);
            targetRect.anchoredPosition = new Vector2(-100, -50);
            coinTargetPosition = targetObj.transform;
        }
    }

    public void PlayCoinEffect(Vector3 worldPosition, int coinAmount)
    {
        if (mainCamera == null || uiCanvas == null)
        {
            Debug.LogError("[CoinEffectManager] Missing camera or canvas references");
            return;
        }

        // Direkt animasyonu başlat, gecikme yok
        StartCoinEffect(worldPosition, coinAmount);
    }
private void StartCoinEffect(Vector3 worldPosition, int coinAmount)
{
    if (uiCanvas == null)
    {
        Debug.LogError("[CoinEffectManager] UI Canvas is null!");
        return;
    }
    
    // World pozisyonunu screen pozisyonuna çevir
    Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);
    
    // Screen pozisyonunu UI pozisyonuna çevir
    Vector2 uiPosition;
    RectTransform canvasRect = uiCanvas.transform as RectTransform;
    
    // Canvas'ın render mode'una göre farklı dönüşüm yap
    if (uiCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, 
            screenPosition, 
            null, // Overlay mode'da camera null olmalı
            out uiPosition);
    }
    else
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, 
            screenPosition, 
            uiCanvas.worldCamera ?? mainCamera, 
            out uiPosition);
    }
    
    // Coin efekti başlat
    Coroutine coinAnimation = StartCoroutine(AnimateCoin(uiPosition, coinAmount));
    activeCoinAnimations.Add(coinAnimation);
    
}

    private IEnumerator AnimateCoin(Vector2 startPosition, int coinAmount)
    {
        // Bu coroutine'in referansını al
        Coroutine currentCoroutine = null;
        foreach (var anim in activeCoinAnimations)
        {
            if (anim != null)
            {
                currentCoroutine = anim;
                break;
            }
        }
        
        // Coin UI objesi oluştur
        GameObject coinUI = CreateCoinUI(startPosition, coinAmount);
        RectTransform coinRect = coinUI.GetComponent<RectTransform>();
        Image coinImage = coinUI.GetComponent<Image>();
        
        // Başlangıç değerleri
        Vector2 targetPos = coinTargetPosition.position;
        Vector2 currentPos = startPosition;
        Vector3 startScale = Vector3.one * 3f;
        Vector3 endScale = Vector3.one * 0.3f;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / animationDuration;
            float curveValue = movementCurve.Evaluate(t);
            
            // Pozisyon animasyonu
            coinRect.anchoredPosition = Vector2.Lerp(currentPos, targetPos, curveValue);
            
            // Scale animasyonu
            coinUI.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            
            // Alpha animasyonu (son %20'de fade out)
            if (t > 0.8f)
            {
                float fadeT = (t - 0.8f) / 0.2f;
                Color color = coinImage.color;
                color.a = Mathf.Lerp(1f, 0f, fadeT);
                coinImage.color = color;
            }
            
            yield return null;
        }
        
        // Animasyon bitti, coin'i oyuncuya ekle - TEK SEFER
        AddCoinToPlayer(coinAmount);
        
        // UI objesini yok et
        if (coinUI != null)
        {
            Destroy(coinUI);
        }
        
        // Sadece bu coroutine'i listeden çıkar
        if (currentCoroutine != null)
        {
            activeCoinAnimations.Remove(currentCoroutine);
        }
        
    }
    
    private GameObject CreateCoinUI(Vector2 position, int coinAmount)
    {
        GameObject coinObj = new GameObject("CoinEffect");
        coinObj.transform.SetParent(uiCanvas.transform, false);
        
        // RectTransform ayarla
        RectTransform rectTransform = coinObj.AddComponent<RectTransform>();
        rectTransform.anchoredPosition = position;
        rectTransform.sizeDelta = new Vector2(64, 64);
        
        // Image component ekle
        Image imageComponent = coinObj.AddComponent<Image>();
        imageComponent.sprite = GetCoinSprite(coinAmount);
        imageComponent.color = Color.yellow;
        
        return coinObj;
    }
    
    private Sprite GetCoinSprite(int amount)
    {
        if (amount < 100 && smallGoldSprite != null)
            return smallGoldSprite;
        else if (amount < 500 && mediumGoldSprite != null)
            return mediumGoldSprite;
        else if (largeGoldSprite != null)
            return largeGoldSprite;
        
        // Fallback: Resources'dan yükle
        return Resources.Load<Sprite>("Items/SmallGoldBag");
    }
    
    private void AddCoinToPlayer(int coinAmount)
    {
        // Local player'ı bul ve coin ekle - TEK SEFER KONTROL
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.HasInputAuthority)
            {
                PlayerStats playerStats = player.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    playerStats.AddCoins(coinAmount);

                    // Coin notification göster
                    if (FragmentNotificationUI.Instance != null)
                    {
                        Sprite coinSprite = GetCoinSprite(coinAmount);
                        FragmentNotificationUI.Instance.ShowFragmentNotification(
                            "Gold",
                            coinAmount,
                            coinSprite
                        );
                    }

                    return; // ÖNEMLI: Tek sefer ekledikten sonra çık
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        // Instance'ı temizle
        if (Instance == this)
        {
            Instance = null;
        }
        
        // Aktif animasyonları durdur
        foreach (Coroutine animation in activeCoinAnimations)
        {
            if (animation != null)
            {
                StopCoroutine(animation);
            }
        }
        activeCoinAnimations.Clear();
    }
}