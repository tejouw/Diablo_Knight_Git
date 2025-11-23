using UnityEngine;
using System.Collections;
using Fusion;

public class LocalProjectile : MonoBehaviour
{
    private Vector3 targetPosition;
    private float speed; // Dinamik hız
    private float maxLifetime = 3f;
    private float lifetime = 0f;
    private bool isMoving = false;
    private Vector3 currentDirection;
    private TrailRenderer trailRenderer;
    private SpriteRenderer spriteRenderer;

    // Hız hesaplama için gerekli
    private PlayerRef projectileOwner;

public void Initialize(Vector3 startPos, Vector3 targetPos, Sprite arrowSprite = null, PlayerRef owner = default)
{
    transform.position = startPos;
    targetPosition = targetPos;
    projectileOwner = owner;
    
    speed = CalculateLocalProjectileSpeed();
    
    CreateVisuals(arrowSprite);
    
    // Sadece başlangıç direction'ını hesapla - homing yok
    currentDirection = (targetPosition - transform.position).normalized;
    
    UpdateRotation();
    isMoving = true;
}

    private float CalculateLocalProjectileSpeed()
    {
        const float BASE_PROJECTILE_SPEED = 8f;
        float finalSpeed = BASE_PROJECTILE_SPEED;
        
        // Owner'ın PlayerStats'ını bul
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.InputAuthority == projectileOwner)
            {
                PlayerStats playerStats = player.GetComponent<PlayerStats>();
                if (playerStats != null)
                {
                    // Attack speed multiplier
                    float attackSpeedMultiplier = playerStats.FinalAttackSpeed;
                    
                    // Projectile speed item bonusu
                    float projectileSpeedBonus = GetLocalProjectileSpeedBonus(player);
                    
                    finalSpeed = BASE_PROJECTILE_SPEED * attackSpeedMultiplier * (1f + projectileSpeedBonus / 100f);
                }
                break;
            }
        }
        
        return Mathf.Clamp(finalSpeed, 3f, 20f); // Server ile aynı sınır
    }
    
    private float GetLocalProjectileSpeedBonus(GameObject player)
    {
        float totalBonus = 0f;
        
        EquipmentSystem equipmentSystem = player.GetComponent<EquipmentSystem>();
        if (equipmentSystem != null)
        {
            var allEquipment = equipmentSystem.GetAllEquippedItems();
            
            foreach (var slotPair in allEquipment)
            {
                foreach (var item in slotPair.Value)
                {
                    if (item != null)
                    {
                        totalBonus += item.GetStatValue(StatType.ProjectileSpeed);
                    }
                }
            }
        }
        
        return totalBonus;
    }

private void Update()
{
    if (!isMoving) return;
    
    lifetime += Time.deltaTime;
    if (lifetime >= maxLifetime)
    {
        DestroyProjectile();
        return;
    }
    
    // Düz hareket
    transform.position += currentDirection * speed * Time.deltaTime;
    UpdateRotation();
    
    // Hedef mesafe kontrolü - daha küçük threshold
    float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
    if (distanceToTarget < 0.4f) // 0.5f'den 0.4f'ye
    {
        DestroyProjectile();
        return;
    }
}

    private void UpdateRotation()
    {
        if (currentDirection.magnitude > 0.01f)
        {
            float angle = Mathf.Atan2(currentDirection.y, currentDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle - 90f, Vector3.forward);
        }
    }

    private void CreateVisuals(Sprite arrowSprite)
    {
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sortingLayerName = "UI";
        spriteRenderer.sortingOrder = 5;

        if (arrowSprite != null)
        {
            spriteRenderer.sprite = arrowSprite;
        }
        else
        {
            spriteRenderer.sprite = CreateDefaultArrowSprite();
        }

        Material defaultMaterial = new Material(Shader.Find("Sprites/Default"));
        spriteRenderer.material = defaultMaterial;

        trailRenderer = gameObject.AddComponent<TrailRenderer>();
        trailRenderer.startWidth = 0.08f;
        trailRenderer.endWidth = 0.02f;
        trailRenderer.time = 0.2f;
        trailRenderer.sortingLayerName = "UI";
        trailRenderer.sortingOrder = 4;

        Material trailMaterial = new Material(Shader.Find("Sprites/Default"));
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.yellow, 0.0f), new GradientColorKey(Color.red, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        trailRenderer.colorGradient = gradient;
        trailRenderer.material = trailMaterial;
    }

    private Sprite CreateDefaultArrowSprite()
    {
        int width = 8;
        int height = 16;
        Texture2D texture = new Texture2D(width, height);
        Color[] colors = new Color[width * height];
        
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Color pixelColor = Color.clear;
                
                if (y > height * 0.7f)
                {
                    float centerX = width / 2f;
                    float tipY = height - 1;
                    float distance = Mathf.Abs(x - centerX);
                    float maxDistance = (tipY - y) * 0.8f;
                    
                    if (distance <= maxDistance)
                    {
                        pixelColor = new Color(0.8f, 0.6f, 0.2f, 1f);
                    }
                }
                else if (x >= width * 0.4f && x < width * 0.6f)
                {
                    pixelColor = new Color(0.6f, 0.4f, 0.2f, 1f);
                }
                
                colors[y * width + x] = pixelColor;
            }
        }
        
        texture.SetPixels(colors);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 32f);
    }

    private void DestroyProjectile()
    {
        isMoving = false;
        Destroy(gameObject);
    }
}