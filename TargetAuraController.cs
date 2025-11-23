using UnityEngine;

public class TargetAuraController : MonoBehaviour
{
    public GameObject TargetMonster { get; private set; }
    
    private Vector3 offset = Vector3.zero;
    
    public void SetTarget(GameObject target)
    {
        TargetMonster = target;
        
        // Target'ın altında konumlan
        if (target != null)
        {
            Collider2D targetCollider = target.GetComponent<Collider2D>();
            if (targetCollider != null)
            {
                offset = Vector3.down * (targetCollider.bounds.size.y / 2f);
            }
            
            UpdatePosition();
        }
    }
    
    private void Update()
    {
        UpdatePosition();
    }
    
    private void UpdatePosition()
    {
        if (TargetMonster != null)
        {
            transform.position = TargetMonster.transform.position + offset;
        }
        else
        {
            // Target yoksa kendini yok et
            Destroy(gameObject);
        }
    }
}