using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class AreaNotificationUI : MonoBehaviour
{
    public static AreaNotificationUI Instance;
    
    [Header("Current Area Display")]
    [SerializeField] private TextMeshProUGUI currentAreaText;
    
    [Header("Area Notification Panel")]
    [SerializeField] private GameObject areaNotificationPanel;
    [SerializeField] private Text areaNotificationText;
    [SerializeField] private Text areaNotificationSubtext;
    
    [Header("Animation Settings")]
    [SerializeField] private float animationDuration = 0.8f;
    [SerializeField] private float displayDuration = 2f;
    [SerializeField] private float discoveryDisplayDuration = 3f;
    [SerializeField] private float exitAnimationDuration = 0.5f;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        // Başlangıçta notification panel'ini kapat
        if (areaNotificationPanel != null)
        {
            areaNotificationPanel.SetActive(false);
        }
        
        // Current area'yı unknown olarak başlat
        UpdateCurrentArea(null);
    }
    
    public void UpdateCurrentArea(AreaData area)
    {
        if (currentAreaText != null)
        {
            if (area != null)
            {
                currentAreaText.text = area.areaName;
            }
            else
            {
                currentAreaText.text = "Bilinmeyen Bölge";
            }
        }
    }
    
    public void ShowAreaDiscoveryNotification(AreaData area)
    {
        if (area == null) return;
        
        StartCoroutine(ShowAreaNotificationCoroutine(area, true));
    }
    
    public void ShowAreaEnteredNotification(AreaData area)
    {
        if (area == null) return;
        
        StartCoroutine(ShowAreaNotificationCoroutine(area, false));
    }
    
    private IEnumerator ShowAreaNotificationCoroutine(AreaData area, bool isDiscovery)
    {
        if (areaNotificationPanel == null || areaNotificationText == null) 
        {
            Debug.LogWarning("[AreaNotificationUI] Required UI components are missing!");
            yield break;
        }

        // Text'leri güncelle
        areaNotificationText.text = area.areaName;
        
        if (areaNotificationSubtext != null)
        {
            areaNotificationSubtext.text = isDiscovery ? "KEŞFEDİLDİ" : "GİRİŞ YAPILDI";
            areaNotificationSubtext.color = isDiscovery ? Color.yellow : Color.white;
        }
        
        
        // Panel'i aktif et
        areaNotificationPanel.SetActive(true);
        
        RectTransform panelRect = areaNotificationPanel.GetComponent<RectTransform>();
        
        // Başlangıç değerleri
        Vector2 startPos = new Vector2(0, Screen.height);
        Vector2 targetPos = Vector2.zero;
        Vector2 endPos = new Vector2(0, -Screen.height);
        
        Vector3 startScale = Vector3.zero;
        Vector3 targetScale = Vector3.one;
        Vector3 endScale = Vector3.zero;
        
        // Başlangıç konumunu ayarla
        panelRect.anchoredPosition = startPos;
        areaNotificationPanel.transform.localScale = startScale;
        
        // 1. Fase: Panel yukarıdan gelir ve büyür
        float elapsed = 0f;
        
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;
            float easedT = 1f - Mathf.Pow(1f - t, 3f); // Ease out cubic
            
            panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, easedT);
            areaNotificationPanel.transform.localScale = Vector3.Lerp(startScale, targetScale, easedT);
            
            yield return null;
        }
        
        // Tam pozisyona yerleştir
        panelRect.anchoredPosition = targetPos;
        areaNotificationPanel.transform.localScale = targetScale;
        
        // 2. Fase: Bekleme süresi (discovery için daha uzun)
        float waitTime = isDiscovery ? discoveryDisplayDuration : displayDuration;
        yield return new WaitForSeconds(waitTime);
        
        // 3. Fase: Panel küçülür ve aşağı gider
        elapsed = 0f;
        
        while (elapsed < exitAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / exitAnimationDuration;
            float easedT = Mathf.Pow(t, 3f); // Ease in cubic
            
            panelRect.anchoredPosition = Vector2.Lerp(targetPos, endPos, easedT);
            areaNotificationPanel.transform.localScale = Vector3.Lerp(targetScale, endScale, easedT);
            
            yield return null;
        }
        
        // Panel'i deaktif et
        areaNotificationPanel.SetActive(false);
    }
    
    // Eğer birden fazla notification aynı anda gelirse, öncekini durdur
    public void StopCurrentNotification()
    {
        StopAllCoroutines();
        
        if (areaNotificationPanel != null)
        {
            areaNotificationPanel.SetActive(false);
        }
    }
}