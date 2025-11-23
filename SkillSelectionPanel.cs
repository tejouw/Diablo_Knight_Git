using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SkillSelectionPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject selectionPanelRoot;
    [SerializeField] private Button[] skillOptionButtons = new Button[2]; // 2 alternatif skill
    [SerializeField] private Image[] skillOptionIcons = new Image[2];

    
    [Header("Visual Settings")]
    [SerializeField] private float animationDuration = 0.3f;
    [SerializeField] private float buttonSpacing = 80f;
    [SerializeField] private Color alternativeSkillColor = Color.white;
    
    private SkillSystem skillSystem;
    private SkillSlot currentSlot;
    private Vector3 originPosition;
    private bool isVisible = false;
    private int[] alternativeIndices = new int[2]; // Aktif olmayan skill'lerin index'leri
    private Vector3[] originalScales = new Vector3[2];
    public System.Action<int> OnSkillSelected;
    
    private void Awake()
    {
        // Panel'i başta gizle
        if (selectionPanelRoot != null)
        {
            selectionPanelRoot.SetActive(false);
        }
        
        // YENİ: Original scale'leri kaydet
        for (int i = 0; i < 2; i++)
        {
            originalScales[i] = skillOptionButtons[i].transform.localScale;
        }
        
        // 2 button event'lerini bağla
        for (int i = 0; i < 2; i++)
        {
            int index = i; // Closure için
            skillOptionButtons[i].onClick.AddListener(() => SelectSkill(index));
        }
    }
    
    public void Initialize(SkillSystem system)
    {
        skillSystem = system;
    }
    
    public void ShowSelectionPanel(SkillSlot slot, Vector3 buttonPosition)
    {
        if (skillSystem == null || isVisible) return;
        
        currentSlot = slot;
        originPosition = buttonPosition;
        
        UpdateSkillOptions();
        PositionPanel();
        
        selectionPanelRoot.SetActive(true);
        StartCoroutine(AnimateIn());
        
        isVisible = true;
    }
    
    public void HideSelectionPanel()
    {
        if (!isVisible) return;
        
        StartCoroutine(AnimateOut());
        isVisible = false;
    }
    
    private void UpdateSkillOptions()
    {
        string[] skillSet = skillSystem.GetSkillSetForSlot(currentSlot);
        int activeIndex = skillSystem.GetActiveIndexForSlot(currentSlot);
        
        // Aktif olmayan skill'lerin index'lerini bul
        int alternativeCount = 0;
        for (int i = 0; i < 3; i++)
        {
            if (i != activeIndex && alternativeCount < 2)
            {
                alternativeIndices[alternativeCount] = i;
                alternativeCount++;
            }
        }
        
        // 2 alternatif skill'i UI'da göster
        for (int i = 0; i < 2; i++)
        {
            int skillIndex = alternativeIndices[i];
            string skillId = skillSet[skillIndex];
            
            if (!string.IsNullOrEmpty(skillId))
            {
                var skillData = SkillDatabase.Instance?.GetSkillById(skillId);
                if (skillData != null)
                {
                    skillOptionIcons[i].sprite = skillData.skillIcon;
                    skillOptionIcons[i].color = Color.white;
                    skillOptionButtons[i].interactable = true;
                }
                else
                {
                    // Skill data bulunamadı
                    skillOptionIcons[i].sprite = null;
                    skillOptionIcons[i].color = new Color(1f, 1f, 1f, 0.3f);
                    skillOptionButtons[i].interactable = false;
                }
            }
            else
            {
                // Boş slot
                skillOptionIcons[i].sprite = null;
                skillOptionIcons[i].color = new Color(1f, 1f, 1f, 0.3f);
                skillOptionButtons[i].interactable = false;
            }
        }
    }
    
private void PositionPanel()
{
    // Canvas tipine göre pozisyonlama
    Canvas parentCanvas = GetComponentInParent<Canvas>();
    
    if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
    {
        // Screen space overlay için direkt screen position kullan
        RectTransform panelRect = selectionPanelRoot.GetComponent<RectTransform>();
        panelRect.position = originPosition + Vector3.up * buttonSpacing;
        
        // Skill button'larını pozisyonla
        for (int i = 0; i < 2; i++)
        {
            RectTransform buttonRect = skillOptionButtons[i].GetComponent<RectTransform>();
            Vector3 buttonPos = panelRect.position + Vector3.up * (buttonSpacing * (1 - i));
            buttonRect.position = buttonPos;
        }
    }
    else
    {
        // World space canvas için mevcut kodu kullan
        Vector3 panelPosition = originPosition + Vector3.up * buttonSpacing;
        selectionPanelRoot.transform.position = panelPosition;
        
        for (int i = 0; i < 2; i++)
        {
            Vector3 buttonPos = panelPosition + Vector3.up * (buttonSpacing * (1 - i));
            skillOptionButtons[i].transform.position = buttonPos;
        }
    }
}
    
private IEnumerator AnimateIn()
{
    // Başlangıç scale'i 0 yap
    for (int i = 0; i < skillOptionButtons.Length; i++)
    {
        skillOptionButtons[i].transform.localScale = Vector3.zero;
    }
    
    // Her button'u sırayla animate et
    for (int i = 0; i < skillOptionButtons.Length; i++)
    {
        StartCoroutine(AnimateButtonIn(skillOptionButtons[i], i * 0.1f));
    }
    
    yield return new WaitForSeconds(animationDuration + 0.2f);
}
    
private IEnumerator AnimateButtonIn(Button button, float delay)
{
    yield return new WaitForSeconds(delay);
    
    // YENİ: Bu button'ın original scale'ini bul
    Vector3 targetScale = Vector3.one; // Default fallback
    for (int i = 0; i < skillOptionButtons.Length; i++)
    {
        if (skillOptionButtons[i] == button)
        {
            targetScale = originalScales[i];
            break;
        }
    }
    
    float elapsed = 0f;
    
    while (elapsed < animationDuration)
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / animationDuration;
        
        // Elastic ease out - original scale'e göre hesapla
        float scaleMultiplier = Mathf.Lerp(0f, 1f, progress);
        button.transform.localScale = targetScale * scaleMultiplier;
        
        yield return null;
    }
    
    // YENİ: Original scale'e geri döndür
    button.transform.localScale = targetScale;
}
    
private IEnumerator AnimateOut()
{
    float elapsed = 0f;
    
    while (elapsed < animationDuration)
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / animationDuration;
        
        for (int i = 0; i < 2; i++)
        {
            // Original scale'den 0'a
            Vector3 fromScale = originalScales[i];
            float scaleMultiplier = Mathf.Lerp(1f, 0f, progress);
            skillOptionButtons[i].transform.localScale = fromScale * scaleMultiplier;
        }
        
        yield return null;
    }
    
    selectionPanelRoot.SetActive(false);
}
    
    private void SelectSkill(int buttonIndex)
    {
        if (skillSystem == null || buttonIndex < 0 || buttonIndex >= 2) return;
        
        // Button index'ini gerçek skill index'ine çevir
        int realSkillIndex = alternativeIndices[buttonIndex];
        
        // Skill rotation yap
        skillSystem.RotateSkillInSlot(currentSlot, realSkillIndex);
        
        // Panel'i kapat
        HideSelectionPanel();
        
        // Event'i tetikle
        OnSkillSelected?.Invoke(realSkillIndex);
    }
    
    private void Update()
    {
        // Panel açıkken background'a dokunulursa kapat
        if (isVisible && Input.GetMouseButtonDown(0))
        {
            // Panel area'sının dışında mı?
            bool clickedOutside = true;
            for (int i = 0; i < 2; i++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(
                    skillOptionButtons[i].GetComponent<RectTransform>(), 
                    Input.mousePosition, 
                    Camera.main))
                {
                    clickedOutside = false;
                    break;
                }
            }
            
            if (clickedOutside)
            {
                HideSelectionPanel();
            }
        }
    }
}