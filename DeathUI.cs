using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DeathUI : MonoBehaviour
{
    public static DeathUI Instance;
    
    [Header("Death UI Elements")]
    [SerializeField] private GameObject deathPanel;
    [SerializeField] private TextMeshProUGUI deathMessageText;
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private Button respawnButton; // Yeni buton
    
    private Coroutine countdownCoroutine;
    private DeathSystem currentDeathSystem; // DeathSystem referansı
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            
            // Başlangıçta paneli gizle
            if (deathPanel != null)
            {
                deathPanel.SetActive(false);
            }
            
            // Respawn buton event'i
            if (respawnButton != null)
            {
                respawnButton.onClick.AddListener(OnRespawnButtonClicked);
                respawnButton.interactable = false; // Başlangıçta disabled
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void ShowDeathUI(float respawnTime, DeathSystem deathSystem)
    {
        currentDeathSystem = deathSystem;
        
        if (deathPanel != null)
        {
            deathPanel.SetActive(true);
            
            if (deathMessageText != null)
            {
                deathMessageText.text = "ÖLDÜNÜZ!";
            }
            
            // Butonu disable et
            if (respawnButton != null)
            {
                respawnButton.interactable = false;
            }
            
            if (countdownCoroutine != null)
            {
                StopCoroutine(countdownCoroutine);
            }
            
            countdownCoroutine = StartCoroutine(CountdownTimer(respawnTime));
        }
    }
    
    public void HideDeathUI()
    {
        if (deathPanel != null)
        {
            deathPanel.SetActive(false);
        }
        
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
            countdownCoroutine = null;
        }
        
        currentDeathSystem = null;
    }
    
// Force close - respawn işleminden sonra çağrılır
public void ForceHideDeathUI()
{
    
    if (deathPanel != null)
    {
        deathPanel.SetActive(false);
    }
    
    if (countdownCoroutine != null)
    {
        StopCoroutine(countdownCoroutine);
        countdownCoroutine = null;
    }
    
    currentDeathSystem = null;
}
    
    private void OnRespawnButtonClicked()
    {
        if (currentDeathSystem != null)
        {
            currentDeathSystem.TriggerManualRespawn();
        }
    }
    
    private IEnumerator CountdownTimer(float totalTime)
    {
        float timeLeft = totalTime;
        
        while (timeLeft > 0)
        {
            if (countdownText != null)
            {
                countdownText.text = $"Dirilme süresi: {timeLeft:F1} saniye";
            }
            
            timeLeft -= Time.deltaTime;
            yield return null;
        }
        
        // Timer doldu - butonu aktif et
        if (countdownText != null)
        {
            countdownText.text = "DİRİLMEYE HAZIR!";
        }
        
        if (respawnButton != null)
        {
            respawnButton.interactable = true;
        }
    }
    
    private void Update()
    {
        // DeathSystem'in CanRespawn durumunu kontrol et
        if (currentDeathSystem != null && respawnButton != null)
        {
            // Network'ten CanRespawn durumunu al
            bool canRespawn = currentDeathSystem.CanRespawn;
            
            // Buton state'ini güncelle (sadece timer dolduğunda aktif olsun)
            if (canRespawn && !respawnButton.interactable)
            {
                respawnButton.interactable = true;
                if (countdownText != null)
                {
                    countdownText.text = "DİRİLMEYE HAZIR!";
                }
            }
        }
    }
}