using UnityEngine;
using UnityEngine.UI;
using Fusion;
using TMPro;

public class QuestCompass : MonoBehaviour
{
    [Header("Compass References")]
    [SerializeField] private GameObject compassPanel;
    [SerializeField] private RectTransform compassArrow;
    [SerializeField] private TextMeshProUGUI distanceText;
    [SerializeField] private float updateInterval = 0.2f;
    
    private Vector2 targetLocation;
    private Transform playerTransform;
private bool isActive = false;
public bool IsActive => isActive;
    private float nextUpdateTime;
    
    private bool lastVisibilityState = false;
    private Vector2 lastTargetLocation = Vector2.zero;
    
    public static QuestCompass Instance;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Başlangıçta kapalı
        if (compassPanel != null)
        {
            compassPanel.SetActive(false);
            lastVisibilityState = false;
        }
    }
    
    public void ShowCompass(Vector2 target)
    {
        // Değişiklik yoksa SetActive çağırma
        if (isActive && lastVisibilityState && Vector2.Distance(lastTargetLocation, target) < 0.1f)
        {
            return;
        }
        
        targetLocation = target;
        lastTargetLocation = target;
        isActive = true;
        
        // Sadece state gerçekten değiştiyse SetActive çağır
        if (!lastVisibilityState && compassPanel != null)
        {
            compassPanel.SetActive(true);
            lastVisibilityState = true;
        }
    }
    
    public void HideCompass()
    {
        // Zaten kapalıysa SetActive çağırma
        if (!isActive && !lastVisibilityState)
        {
            return;
        }
        
        isActive = false;
        
        // Sadece state gerçekten değiştiyse SetActive çağır
        if (lastVisibilityState && compassPanel != null)
        {
            compassPanel.SetActive(false);
            lastVisibilityState = false;
        }
    }
    
    private void Update()
    {
        if (!isActive || Time.time < nextUpdateTime) return;
        
        FindLocalPlayer();
        
        if (playerTransform != null)
        {
            UpdateCompass();
            nextUpdateTime = Time.time + updateInterval;
        }
    }
    
    private void FindLocalPlayer()
    {
        if (playerTransform != null) return;
        
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            var networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.HasInputAuthority)
            {
                playerTransform = player.transform;
                break;
            }
        }
    }
    
    private void UpdateCompass()
    {
        if (playerTransform == null || compassArrow == null) return;
        
        Vector2 direction = (targetLocation - (Vector2)playerTransform.position).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        
        compassArrow.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        
        float distance = Vector2.Distance(playerTransform.position, targetLocation);
        if (distanceText != null)
        {
            distanceText.text = $"{distance:F0}m";
        }
        
        // Hedefe çok yakınsa pusulayi gizle
        if (distance <= 6f)
        {
            HideCompass();
        }
    }
}