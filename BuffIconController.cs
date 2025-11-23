using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class BuffIconController : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image timerFill;
    [SerializeField] private TextMeshProUGUI timerText;
    
    private string buffId;
    private float totalDuration;
    private float remainingTime;
    private bool isActive = false;
    
    public System.Action OnBuffExpired;
    
    private void Awake()
    {
        // Component'leri otomatik bul
        if (iconImage == null)
            iconImage = GetComponent<Image>();
        
        if (timerFill == null)
        {
            Transform fillTransform = transform.Find("TimerFill");
            if (fillTransform != null)
                timerFill = fillTransform.GetComponent<Image>();
        }
        
        if (timerText == null)
        {
            Transform textTransform = transform.Find("TimerText");
            if (textTransform != null)
                timerText = textTransform.GetComponent<TextMeshProUGUI>();
        }
    }
    
    public void Initialize(string id, Sprite icon, float duration)
    {
        buffId = id;
        totalDuration = duration;
        remainingTime = duration;
        isActive = true;
        
        if (iconImage != null)
            iconImage.sprite = icon;
        
        if (timerFill != null)
            timerFill.fillAmount = 1f;
        
        UpdateTimerDisplay();
        StartCoroutine(BuffTimerCoroutine());
    }
    
    public void UpdateDuration(float newDuration)
    {
        totalDuration = newDuration;
        remainingTime = newDuration;
        
        if (timerFill != null)
            timerFill.fillAmount = 1f;
        
        UpdateTimerDisplay();
    }
    
    private IEnumerator BuffTimerCoroutine()
    {
        while (isActive && remainingTime > 0)
        {
            yield return new WaitForSeconds(0.1f);
            
            remainingTime -= 0.1f;
            
            if (remainingTime <= 0)
            {
                remainingTime = 0;
                isActive = false;
                OnBuffExpired?.Invoke();
                break;
            }
            
            UpdateTimerDisplay();
        }
    }
    
    private void UpdateTimerDisplay()
    {
        // Timer fill güncelle
        if (timerFill != null && totalDuration > 0)
        {
            timerFill.fillAmount = remainingTime / totalDuration;
        }
        
        // Timer text güncelle
        if (timerText != null)
        {
            if (remainingTime > 1f)
            {
                timerText.text = Mathf.Ceil(remainingTime).ToString();
            }
            else
            {
                timerText.text = ""; // Son saniyede text'i gizle
            }
        }
    }
    
    private void OnDestroy()
    {
        isActive = false;
    }
}