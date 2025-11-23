using UnityEngine;
using System.Collections;

public class HitEffectSelfDestruct : MonoBehaviour
{
    private bool isInitialized = false;
    
    public void Initialize(bool isCritical)
    {
        if (isInitialized) return;
        isInitialized = true;
        
        StartCoroutine(AnimateAndDestroy(isCritical));
    }
    
    private IEnumerator AnimateAndDestroy(bool isCritical)
    {
        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer == null)
        {
            Destroy(gameObject);
            yield break;
        }
        
        float duration = isCritical ? 0.4f : 0.3f;
        float maxScale = isCritical ? 2.5f : 1.8f;
        Vector3 startScale = Vector3.one * 0.3f;
        Color startColor = renderer.color;
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (renderer == null) break;
            
            float t = elapsed / duration;
            
            // Scale animasyonu
            float scale = Mathf.Lerp(0.3f, maxScale, t);
            transform.localScale = new Vector3(scale, scale, 1f);
            
            // Fade out
            Color color = startColor;
            color.a = Mathf.Lerp(startColor.a, 0f, t);
            renderer.color = color;
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        Destroy(gameObject);
    }
}