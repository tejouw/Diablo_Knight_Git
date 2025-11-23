using UnityEngine;
using System.Collections.Generic;

public class DepthFadePrefab : MonoBehaviour
{
    [Header("Fade Settings")]
    [SerializeField] private float fadedAlpha = 0.5f;
    [SerializeField] private float fadeSpeed = 5f;
    [SerializeField] private Vector2 triggerSize = new Vector2(2f, 2f);
    
    private SpriteRenderer[] spriteRenderers;
    private Dictionary<SpriteRenderer, Color> originalColors;
    private HashSet<GameObject> objectsBehind;
    private float currentAlpha = 1f;
    
    private void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Dictionary<SpriteRenderer, Color>();
        objectsBehind = new HashSet<GameObject>();
        
        foreach (var sr in spriteRenderers)
        {
            if (sr != null)
            {
                originalColors[sr] = sr.color;
            }
        }
        
        
        SetupTrigger();
    }
    
    private void SetupTrigger()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        collider.isTrigger = true;
        collider.size = triggerSize;
        
    }
    
private void OnTriggerStay2D(Collider2D other)
{
    
    // Parent'ı bul
    GameObject targetObject = other.gameObject;
    Transform parentTransform = other.transform.parent;
    
    if (parentTransform != null)
    {
        
        bool parentIsPlayer = parentTransform.CompareTag("Player");
        bool parentIsMonster = parentTransform.GetComponent<MonsterBehaviour>() != null;
        
        
        if (parentIsPlayer || parentIsMonster)
        {
            targetObject = parentTransform.gameObject;
        }
    }

    
    if (!IsCharacterObject(targetObject))
    {
        return;
    }
    
    // Y pozisyon karşılaştırması
    bool isBehind = targetObject.transform.position.y > transform.position.y;
    bool isInSet = objectsBehind.Contains(targetObject);
    
    
    if (isBehind && !isInSet)
    {
        objectsBehind.Add(targetObject);
    }
    else if (!isBehind && isInSet)
    {
        objectsBehind.Remove(targetObject);
    }
}
    
private void OnTriggerExit2D(Collider2D other)
{
    // Parent'ı bul (OnTriggerStay2D ile aynı mantık)
    GameObject targetObject = other.gameObject;
    if (other.transform.parent != null && 
        (other.transform.parent.CompareTag("Player") || other.transform.parent.GetComponent<MonsterBehaviour>() != null))
    {
        targetObject = other.transform.parent.gameObject;
    }
    
    if (objectsBehind.Remove(targetObject))
    {
    }
    else
    {
    }
}
    
private bool IsCharacterObject(GameObject obj)
{
    // Direkt kontrol
    bool isPlayer = obj.CompareTag("Player");
    bool isMonster = obj.GetComponent<MonsterBehaviour>() != null;
    
    // Parent'tan kontrol (child parçalar için)
    if (!isPlayer && !isMonster)
    {
        Transform parent = obj.transform.parent;
        if (parent != null)
        {
            isPlayer = parent.CompareTag("Player");
            isMonster = parent.GetComponent<MonsterBehaviour>() != null;
            

        }
    }

    return isPlayer || isMonster;
}
    
    private void Update()
    {
        float targetAlpha = objectsBehind.Count > 0 ? fadedAlpha : 1f;
        
        if (Mathf.Abs(currentAlpha - targetAlpha) > 0.01f)
        {
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
            
            foreach (var sr in spriteRenderers)
            {
                if (sr != null && originalColors.ContainsKey(sr))
                {
                    Color originalColor = originalColors[sr];
                    sr.color = new Color(originalColor.r, originalColor.g, originalColor.b, currentAlpha);
                }
            }
        }
    }
    
    private void OnDestroy()
    {
        foreach (var kvp in originalColors)
        {
            if (kvp.Key != null)
            {
                kvp.Key.color = kvp.Value;
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, triggerSize);
    }
}