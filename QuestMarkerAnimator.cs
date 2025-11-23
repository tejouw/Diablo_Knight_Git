using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class QuestMarkerAnimator : MonoBehaviour
{
    [Header("Animation Settings")]
    [SerializeField] private float bounceHeight = 3f;
    [SerializeField] private float bounceSpeed = 1f;
    [SerializeField] private float pulseSpeed = 1.5f;
    [SerializeField] private float glowIntensity = 0.3f;
    
    private RectTransform rectTransform;
    private Image image;
    private Vector2 originalPosition;
    private Vector3 originalScale;
    private Color originalColor;
    
    private Coroutine animationCoroutine;
    
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        image = GetComponent<Image>();
        
        if (rectTransform != null)
        {
            originalPosition = rectTransform.anchoredPosition;
            originalScale = rectTransform.localScale;
        }
        
        if (image != null)
        {
            originalColor = image.color;
        }
    }
    
    private void OnEnable()
    {
        StartAnimation();
    }
    
    private void OnDisable()
    {
        StopAnimation();
    }
    
    public void StartAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }
        
        animationCoroutine = StartCoroutine(AnimateMarker());
    }
    
    public void StopAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
        
        // Orijinal değerlere dön
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = originalPosition;
            rectTransform.localScale = originalScale;
        }
        
        if (image != null)
        {
            image.color = originalColor;
        }
    }
    
    private IEnumerator AnimateMarker()
    {
        float time = 0f;
        
        while (true)
        {
            time += Time.deltaTime;
            
            // Bounce animasyonu (yukarı aşağı)
            float bounceOffset = Mathf.Sin(time * bounceSpeed) * bounceHeight;
            Vector2 newPos = originalPosition + Vector2.up * bounceOffset;
            
            // Pulse animasyonu (büyüme küçülme)
            float scaleMultiplier = 1f + (Mathf.Sin(time * pulseSpeed) * 0.1f);
            Vector3 newScale = originalScale * scaleMultiplier;
            
            // Glow efekti (parlaklık)
            float glowMultiplier = 1f + (Mathf.Sin(time * pulseSpeed * 2f) * glowIntensity);
            Color newColor = originalColor * glowMultiplier;
            newColor.a = originalColor.a; // Alpha'yı koru
            
            // Animasyonları uygula
            if (rectTransform != null)
            {
                rectTransform.anchoredPosition = newPos;
                rectTransform.localScale = newScale;
            }
            
            if (image != null)
            {
                image.color = newColor;
            }
            
            yield return null;
        }
    }
    
    // Farklı quest türleri için farklı animasyon stilleri
    public void SetAnimationStyle(QuestMarkerStyle style)
    {
        switch (style)
        {
            case QuestMarkerStyle.Available:
                bounceHeight = 2f;
                bounceSpeed = 1f;
                pulseSpeed = 1.2f;
                glowIntensity = 0.2f;
                break;
                
            case QuestMarkerStyle.Active:
                bounceHeight = 3f;
                bounceSpeed = 2f;
                pulseSpeed = 2f;
                glowIntensity = 0.4f;
                break;
                
            case QuestMarkerStyle.Completed:
                bounceHeight = 4f;
                bounceSpeed = 2.5f;
                pulseSpeed = 2.5f;
                glowIntensity = 0.5f;
                break;
        }
        
        // Animasyonu yeniden başlat
        if (gameObject.activeInHierarchy)
        {
            StartAnimation();
        }
    }
}

public enum QuestMarkerStyle
{
    Available,
    Active, 
    Completed
}