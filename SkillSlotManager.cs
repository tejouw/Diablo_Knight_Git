using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Fusion;

public class SkillSlotManager : MonoBehaviour
{
    [Header("Skill Slot UI")]
    [SerializeField] private Button skill1Button;
    [SerializeField] private Button skill2Button;
    [SerializeField] private Button skill3Button;
    
    [Header("Skill Slot Images")]
    [SerializeField] private Image skill1Icon;
    [SerializeField] private Image skill2Icon;
    [SerializeField] private Image skill3Icon;
    
    [Header("Cooldown Overlays")]
    [SerializeField] private Image skill1Cooldown;
    [SerializeField] private Image skill2Cooldown;
    [SerializeField] private Image skill3Cooldown;
    
    [Header("Cooldown Texts")]
    [SerializeField] private Text skill1CooldownText;
    [SerializeField] private Text skill2CooldownText;
    [SerializeField] private Text skill3CooldownText;

    [Header("Locked Skill Settings")]
[SerializeField] private Sprite lockedSkillIcon; // Inspector'dan atanacak
    
    private SkillSystem skillSystem;
    private Dictionary<SkillSlot, float> lastUpdateTime = new Dictionary<SkillSlot, float>();

    private void Start()
    {
        // Player'ın skill sistemini bul
        StartCoroutine(FindSkillSystemWhenReady());

        // Başlangıçta cooldown overlay'leri gizle
        SetCooldownVisibility(SkillSlot.Skill1, false);
        SetCooldownVisibility(SkillSlot.Skill2, false);
        SetCooldownVisibility(SkillSlot.Skill3, false);

        // YENİ: Başlangıçta locked icon'ları göster
        ShowLockedIcons();
    }
private void ShowLockedIcons()
{
    if (lockedSkillIcon == null) return;
    
    // Tüm slot'lara locked icon ver
    if (skill1Icon != null)
    {
        skill1Icon.sprite = lockedSkillIcon;
        skill1Icon.color = new Color(0.5f, 0.5f, 0.5f, 1f);
    }
    
    if (skill2Icon != null)
    {
        skill2Icon.sprite = lockedSkillIcon;
        skill2Icon.color = new Color(0.5f, 0.5f, 0.5f, 1f);
    }
    
    if (skill3Icon != null)
    {
        skill3Icon.sprite = lockedSkillIcon;
        skill3Icon.color = new Color(0.5f, 0.5f, 0.5f, 1f);
    }
    
    // Button'ları deaktif et
    if (skill1Button != null) skill1Button.interactable = false;
    if (skill2Button != null) skill2Button.interactable = false;
    if (skill3Button != null) skill3Button.interactable = false;
}

// FindSkillSystemWhenReady metodunu daha uzun bekletme süresi ile güncelle
private System.Collections.IEnumerator FindSkillSystemWhenReady()
{
    float startTime = Time.time;
    
    while (skillSystem == null)
    {
        GameObject localPlayer = FindLocalPlayer();
        if (localPlayer != null)
        {
            skillSystem = localPlayer.GetComponent<SkillSystem>();
            if (skillSystem != null)
            {
                skillSystem.OnSkillEquipped += OnSkillEquipped;
                
                // ClassSystem event'ine de subscribe ol
                var classSystem = localPlayer.GetComponent<ClassSystem>();
                if (classSystem != null)
                {
                    classSystem.OnClassChanged += OnClassChanged;
                }
                
                break;
            }
        }
        
        // 10 saniye timeout (5'ten 10'a çıkar)
        if (Time.time - startTime > 10f)
        {
            break;
        }
        
        yield return new WaitForSeconds(0.5f);
    }
}

// OnClassChanged metodunu değiştir - sadece UI state'i güncelle
private void OnClassChanged(ClassType newClass)
{
    string playerName = "Unknown";
    if (skillSystem != null)
    {
        var playerStats = skillSystem.GetComponent<PlayerStats>();
        playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";
    }
    
    
    bool hasClass = newClass != ClassType.None;
    
    
    if (!hasClass)
    {
        // Class yok - tüm slot'ları kilitle
        UpdateSlotForClassChange(SkillSlot.Skill1, false);
        UpdateSlotForClassChange(SkillSlot.Skill2, false);
        UpdateSlotForClassChange(SkillSlot.Skill3, false);
    }
    else
    {
        // Class var - skill sync'ini bekle, OnSkillEquipped event'inde güncellenecek
        
        // Geçici olarak slot'ları unlock et ama skill olmadan
        StartCoroutine(DelayedSlotCheck(hasClass));
    }
}

// DelayedSlotCheck metodunu daha uzun yap ve sessizleştir
private System.Collections.IEnumerator DelayedSlotCheck(bool hasClass)
{
    yield return new WaitForSeconds(1f); // 0.5'ten 1'e çıkar - Network sync'ini bekle
    
    string playerName = "Unknown";
    if (skillSystem != null)
    {
        var playerStats = skillSystem.GetComponent<PlayerStats>();
        playerName = playerStats?.GetPlayerDisplayName() ?? "Unknown";
    }
    
    // Şimdi slot'ları kontrol et
    UpdateSlotForClassChange(SkillSlot.Skill1, hasClass);
    UpdateSlotForClassChange(SkillSlot.Skill2, hasClass);
    UpdateSlotForClassChange(SkillSlot.Skill3, hasClass);
}

// UpdateSlotForClassChange metodunu güvenli hale getir ve log kaldır
private void UpdateSlotForClassChange(SkillSlot slot, bool hasClass)
{
    if (skillSystem == null) return;
    
    Image iconImage = GetSkillIcon(slot);
    Button button = GetSkillButton(slot);
    
    if (iconImage == null || button == null) return;

    string skillId = skillSystem.GetEquippedSkillId(slot);
    
    if (!string.IsNullOrEmpty(skillId))
    {
        var instance = skillSystem.GetSkillInstance(skillId);
        if (instance?.skillData != null)
        {
            if (hasClass)
            {
                // Class var - skill'i aktif et
                iconImage.sprite = instance.skillData.skillIcon;
                iconImage.color = Color.white;
                button.interactable = true;
            }
            else
            {
                // Class yok - skill'i kilitle
                iconImage.sprite = lockedSkillIcon != null ? lockedSkillIcon : instance.skillData.skillIcon;
                iconImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                button.interactable = false;
            }
        }
    }
    // Else kısmında log yok - initialization sırasında normal
}
public void RefreshAllSkillIcons()
{
    if (skillSystem == null) return;
    
    RefreshSkillIcon(SkillSlot.Skill1);
    RefreshSkillIcon(SkillSlot.Skill2);
    RefreshSkillIcon(SkillSlot.Skill3);
}

private void RefreshSkillIcon(SkillSlot slot)
{
    string skillId = skillSystem.GetEquippedSkillId(slot);
    if (!string.IsNullOrEmpty(skillId))
    {
        var instance = skillSystem.GetSkillInstance(skillId);
        if (instance != null)
        {
            OnSkillEquipped(slot, instance);
        }
    }
}
    
    private GameObject FindLocalPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (var player in players)
        {
            var networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.HasInputAuthority)
                return player;
        }
        return null;
    }
    
    private void Update()
    {
        if (skillSystem == null) return;
        
        UpdateCooldownDisplay(SkillSlot.Skill1);
        UpdateCooldownDisplay(SkillSlot.Skill2);
        UpdateCooldownDisplay(SkillSlot.Skill3);
    }
    
    private void UpdateCooldownDisplay(SkillSlot slot)
    {
        string skillId = skillSystem.GetEquippedSkillId(slot);
        if (string.IsNullOrEmpty(skillId)) return;
        
        var instance = skillSystem.GetSkillInstance(skillId);
        if (instance == null) return;
        
        float remainingCooldown = instance.GetRemainingCooldown(Time.time);
        bool isOnCooldown = remainingCooldown > 0f;
        
        SetCooldownVisibility(slot, isOnCooldown);
        
        if (isOnCooldown)
        {
            float maxCooldown = instance.skillData?.baseCooldown ?? 1f;
            var levelData = instance.skillData?.GetDataForLevel(instance.currentLevel);
            if (levelData != null)
            {
                maxCooldown *= levelData.cooldownMultiplier;
            }
            
            float fillAmount = remainingCooldown / maxCooldown;
            SetCooldownFill(slot, fillAmount);
            SetCooldownText(slot, Mathf.Ceil(remainingCooldown).ToString());
        }
    }

// OnSkillEquipped metodunu daha güvenli yap
private void OnSkillEquipped(SkillSlot slot, SkillInstance instance)
{
    if (instance?.skillData == null) 
    {
        return; // Log kaldır - initialization sırasında normal
    }

    Image iconImage = GetSkillIcon(slot);
    Button button = GetSkillButton(slot);

    if (iconImage != null && button != null)
    {
        var classSystem = skillSystem?.GetComponent<ClassSystem>();
        bool hasClass = classSystem?.NetworkPlayerClass != ClassType.None;

        if (hasClass)
        {
            // Normal skill icon göster
            iconImage.sprite = instance.skillData.skillIcon;
            iconImage.color = Color.white;
            button.interactable = true;
        }
        else
        {
            // Locked icon göster
            iconImage.sprite = lockedSkillIcon != null ? lockedSkillIcon : instance.skillData.skillIcon;
            iconImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
            button.interactable = false;
        }
    }
}

// YENİ METOD: Skill equip'i geciktirerek tekrar dene
private System.Collections.IEnumerator RetrySkillEquipAfterDelay(SkillSlot slot, SkillInstance instance, float delay)
{
    yield return new WaitForSeconds(delay);
    
    string playerName = skillSystem?.GetComponent<PlayerStats>()?.GetPlayerDisplayName() ?? "Unknown";
    
    var classSystem = skillSystem?.GetComponent<ClassSystem>();
    if (classSystem != null)
    {
        ClassType currentClass = classSystem.NetworkPlayerClass;
        
        if (currentClass != ClassType.None)
        {
            // Tekrar dene
            OnSkillEquipped(slot, instance);
        }
        else
        {
            Debug.LogWarning($"[SkillSlotManager-{playerName}] RetrySkillEquip failed - still no class");
        }
    }
}

    
    private Image GetSkillIcon(SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Skill1 => skill1Icon,
            SkillSlot.Skill2 => skill2Icon,
            SkillSlot.Skill3 => skill3Icon,
            _ => null
        };
    }
    
    private Button GetSkillButton(SkillSlot slot)
    {
        return slot switch
        {
            SkillSlot.Skill1 => skill1Button,
            SkillSlot.Skill2 => skill2Button,
            SkillSlot.Skill3 => skill3Button,
            _ => null
        };
    }
    
    private void SetCooldownVisibility(SkillSlot slot, bool visible)
    {
        Image cooldownOverlay = slot switch
        {
            SkillSlot.Skill1 => skill1Cooldown,
            SkillSlot.Skill2 => skill2Cooldown,
            SkillSlot.Skill3 => skill3Cooldown,
            _ => null
        };
        
        Text cooldownText = slot switch
        {
            SkillSlot.Skill1 => skill1CooldownText,
            SkillSlot.Skill2 => skill2CooldownText,
            SkillSlot.Skill3 => skill3CooldownText,
            _ => null
        };
        
        if (cooldownOverlay != null)
            cooldownOverlay.gameObject.SetActive(visible);
        
        if (cooldownText != null)
            cooldownText.gameObject.SetActive(visible);
    }
    
    private void SetCooldownFill(SkillSlot slot, float fillAmount)
    {
        Image cooldownOverlay = slot switch
        {
            SkillSlot.Skill1 => skill1Cooldown,
            SkillSlot.Skill2 => skill2Cooldown,
            SkillSlot.Skill3 => skill3Cooldown,
            _ => null
        };
        
        if (cooldownOverlay != null)
            cooldownOverlay.fillAmount = fillAmount;
    }
    
    private void SetCooldownText(SkillSlot slot, string text)
    {
        Text cooldownText = slot switch
        {
            SkillSlot.Skill1 => skill1CooldownText,
            SkillSlot.Skill2 => skill2CooldownText,
            SkillSlot.Skill3 => skill3CooldownText,
            _ => null
        };
        
        if (cooldownText != null)
            cooldownText.text = text;
    }
}