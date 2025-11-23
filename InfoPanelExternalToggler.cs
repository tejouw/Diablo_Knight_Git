using UnityEngine;
using UnityEngine.UI;

public class InfoPanelExternalToggler : MonoBehaviour
{
    [Header("Button Reference")]
    [SerializeField] private Button toggleButton;
    
    private void Start()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.AddListener(OnToggleButtonClicked);
        }
        
        // Sürekli kontrol et
        InvokeRepeating(nameof(CheckButtonState), 0f, 0.1f);
    }
    
    private void CheckButtonState()
    {
        if (toggleButton == null) return;
        
        // InfoPanelManager.Instance varsa button'u aktif et
        bool shouldBeActive = InfoPanelManager.Instance != null;
        
        if (toggleButton.interactable != shouldBeActive)
        {
            toggleButton.interactable = shouldBeActive;
            
            if (shouldBeActive)
            {
            }
        }
    }
    
    private void OnToggleButtonClicked()
    {
        if (InfoPanelManager.Instance != null)
        {
            InfoPanelManager.Instance.ToggleInfoPanel();
        }
        else
        {
        }
    }
    
    private void OnDestroy()
    {
        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(OnToggleButtonClicked);
        }
        
        CancelInvoke();
    }
}