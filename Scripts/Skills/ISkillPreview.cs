using UnityEngine;

public interface ISkillPreview
{
    string SkillId { get; }
    void ShowPreview(GameObject caster, Vector2 direction);
    void HidePreview();
    void UpdatePreview(GameObject caster, Vector2 direction);
    bool IsPreviewActive { get; }
}

public abstract class BaseSkillPreview : MonoBehaviour, ISkillPreview
{
    public abstract string SkillId { get; }
    public bool IsPreviewActive { get; protected set; }
    
    protected GameObject previewObject;
    protected SpriteRenderer previewRenderer;
    
    public virtual void ShowPreview(GameObject caster, Vector2 direction)
    {
        if (previewObject == null)
        {
            CreatePreviewObject();
        }
        
        IsPreviewActive = true;
        previewObject.SetActive(true);
        UpdatePreview(caster, direction);
    }
    
    public virtual void HidePreview()
    {
        IsPreviewActive = false;
        if (previewObject != null)
        {
            previewObject.SetActive(false);
        }
    }
    
    public abstract void UpdatePreview(GameObject caster, Vector2 direction);
    protected abstract void CreatePreviewObject();
    
    protected virtual void OnDestroy()
    {
        if (previewObject != null)
        {
            Destroy(previewObject);
        }
    }
}