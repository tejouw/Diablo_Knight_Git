using UnityEngine;
using System.Collections;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using Fusion.Addons.Physics;

public class EvasiveRollExecutor : BaseSkillExecutor
{
    public override string SkillId => "evasive_roll";
    
    private const float ROLL_DISTANCE = 4f; // 2 tile = ~4 units
    private const float ROLL_DURATION = 0.4f; // Takla süresi
    private const float INVULNERABILITY_DURATION = 1f; // YENİ - 1 saniye ölümsüzlük
    private const float SPEED_BUFF_DURATION = 3f; // YENİ - 3 saniye hız artışı
    private const float SPEED_BUFF_MULTIPLIER = 1.2f; // %20 hız artışı
    private const float GHOST_SPAWN_INTERVAL = 0.06f; // Ghost trail için
    private const float GHOST_FADE_DURATION = 0.6f;
    private const float GHOST_ALPHA = 0.4f;
    private const float GHOST_SCALE_FACTOR = 0.8f;
    
    public override void Execute(GameObject caster, SkillInstance skillInstance)
    {
        // Client-side sadece animation başlat
        var character4D = caster.GetComponent<Character4D>();
        if (character4D != null)
        {
            character4D.AnimationManager.SetState(CharacterState.Run);
        }
    }
    
public void ExecuteOnServer(GameObject caster, SkillInstance skillInstance, SkillSystem skillSystem)
{
    if (!skillSystem.Object.HasStateAuthority) return;
    
    skillInstance.lastUsedTime = Time.time;
    
    var character4D = caster.GetComponent<Character4D>();
    if (character4D == null)
    {
        return;
    }
    
    // YENİ: Gerçek movement input'unu kontrol et
    Vector2 rollDirection;
    var playerController = caster.GetComponent<PlayerController>();
    
    if (playerController != null)
    {
        Vector2 realMovementInput = playerController.GetRealMovementInput();
        
        // Eğer hareket halindeyse gerçek input direction'ını kullan
        if (realMovementInput.magnitude > 0.1f)
        {
            rollDirection = realMovementInput.normalized;
        }
        else
        {
            // Duruyorsa Character4D direction'ını kullan
            rollDirection = character4D.Direction;
        }
    }
    else
    {
        // Fallback - Character4D direction
        rollDirection = character4D.Direction;
    }
    
    Vector3 startPosition = caster.transform.position;
    Vector3 rollEndpoint = CalculateRollEndpoint(startPosition, rollDirection);
    
    ApplyInvulnerability(caster);
    
    skillSystem.ExecuteEvasiveRollRPC(
        startPosition,
        rollEndpoint,
        rollDirection
    );
}
    
    private Vector3 CalculateRollEndpoint(Vector3 startPos, Vector2 direction)
    {
        Vector3 targetEndpoint = startPos + (Vector3)(direction * ROLL_DISTANCE);
        
        // Obstacle check - eğer yol bloklu ise mesafeyi kısalt
        RaycastHit2D hit = Physics2D.Raycast(startPos, direction, ROLL_DISTANCE, LayerMask.GetMask("Obstacles", "Wall"));
        if (hit.collider != null)
        {
            float safeDistance = hit.distance * 0.8f; // %80'i kadar git
            targetEndpoint = startPos + (Vector3)(direction * safeDistance);
        }
        
        return targetEndpoint;
    }
    
private void ApplyInvulnerability(GameObject caster)
{
    var tempBuff = caster.GetComponent<TemporaryBuffSystem>();
    if (tempBuff == null)
    {
        tempBuff = caster.gameObject.AddComponent<TemporaryBuffSystem>();
    }
    
    tempBuff.ApplyInvulnerability(INVULNERABILITY_DURATION);
}
    
    private void ApplySpeedBuff(GameObject caster)
    {
        var tempBuff = caster.GetComponent<TemporaryBuffSystem>();
        if (tempBuff == null)
        {
            tempBuff = caster.gameObject.AddComponent<TemporaryBuffSystem>();
        }
        
        tempBuff.ApplySpeedBuff(SPEED_BUFF_MULTIPLIER, SPEED_BUFF_DURATION);
    }
    
    public void ExecuteVFX(GameObject caster, Vector3 startPos, Vector3 endPos, Vector2 direction)
    {
        var networkController = caster.GetComponent<NetworkCharacterController>();
        var skillSystem = caster.GetComponent<SkillSystem>();
        
        if (networkController != null && skillSystem != null)
        {
            // Roll movement başlat - ghost trail ile
            skillSystem.StartCoroutine(ExecuteRollMovement(caster, startPos, endPos, skillSystem));
        }
        
        // Trail VFX çağrısını kaldırdık - sadece ghost trail kullanacağız
    }
    
private IEnumerator ExecuteRollMovement(GameObject caster, Vector3 startPos, Vector3 endPos, MonoBehaviour monoBehaviour)
{
    var networkController = caster.GetComponent<NetworkCharacterController>();
    var networkRigidbody = caster.GetComponent<NetworkRigidbody2D>();
    var character4D = caster.GetComponent<Character4D>();
    var casterCollider = caster.GetComponent<Collider2D>();
    
    if (networkController == null || networkRigidbody == null)
    {
        yield break;
    }
    
    // Movement'i devre dışı bırak
    networkController.SetMovementEnabled(false);
    
    // YENİ - Physics interaction'ları tamamen kapat (PiercingThrust gibi)
    bool originalIsTrigger = false;
    RigidbodyType2D originalBodyType = RigidbodyType2D.Dynamic;
    bool originalColliderEnabled = true;
    
    if (casterCollider != null)
    {
        originalIsTrigger = casterCollider.isTrigger;
        originalColliderEnabled = casterCollider.enabled;
        casterCollider.enabled = false; // YENİ - Collider'ı tamamen kapat
    }
    
    if (networkRigidbody.Rigidbody != null)
    {
        originalBodyType = networkRigidbody.Rigidbody.bodyType;
        networkRigidbody.Rigidbody.bodyType = RigidbodyType2D.Kinematic; // YENİ - Kinematic yap
    }
    
    // Roll animation başlat
    if (character4D != null)
    {
        character4D.AnimationManager.SetState(CharacterState.Run);
    }
    
    // YENİ - Roll parametreleri (PiercingThrust tarzı)
    Vector2 rollDirection = (endPos - startPos).normalized;
    float totalDistance = Vector2.Distance(startPos, endPos);
    float rollSpeed = totalDistance / ROLL_DURATION;
    
    // Ghost trail değişkenleri
    float lastGhostTime = 0f;
    float elapsedTime = 0f;
    Vector3 currentPos = startPos;
    
    // YENİ - Force position update (network sync için)
    if (networkRigidbody.Rigidbody != null)
    {
        networkRigidbody.Teleport(startPos, caster.transform.rotation);
    }
    
    while (elapsedTime < ROLL_DURATION)
    {
        float deltaTime = Time.deltaTime;
        elapsedTime += deltaTime;
        
        // YENİ - Manual position update (PiercingThrust tarzı)
        float moveDistance = rollSpeed * deltaTime;
        currentPos += (Vector3)(rollDirection * moveDistance);
        
        // YENİ - Network position'ı force update
        if (networkRigidbody.Rigidbody != null)
        {
            networkRigidbody.Teleport(currentPos, caster.transform.rotation);
        }
        else
        {
            caster.transform.position = currentPos;
        }
        
        // Ghost spawn kontrolü
        if (elapsedTime - lastGhostTime >= GHOST_SPAWN_INTERVAL)
        {
            SpawnRollGhost(caster, character4D, monoBehaviour);
            lastGhostTime = elapsedTime;
        }
        
        // YENİ - Distance check (erken çıkış için)
        float remainingDistance = Vector2.Distance(currentPos, endPos);
        if (remainingDistance <= 0.3f)
        {
            currentPos = endPos;
            break;
        }
        
        yield return null;
    }
    
    // Final position
    if (networkRigidbody.Rigidbody != null)
    {
        networkRigidbody.Teleport(endPos, caster.transform.rotation);
    }
    else
    {
        caster.transform.position = endPos;
    }
    
    // YENİ - Physics ayarlarını geri döndür (PiercingThrust gibi)
    if (casterCollider != null)
    {
        casterCollider.enabled = originalColliderEnabled;
        casterCollider.isTrigger = originalIsTrigger;
    }
    
    if (networkRigidbody.Rigidbody != null)
    {
        networkRigidbody.Rigidbody.bodyType = originalBodyType;
        networkRigidbody.Rigidbody.linearVelocity = Vector2.zero;
    }
    
    // Movement'i tekrar aç
    networkController.SetMovementEnabled(true);
    
    // Animation'ı idle'a döndür
    if (character4D != null && character4D.AnimationManager != null)
    {
        character4D.AnimationManager.SetState(CharacterState.Idle);
    }
    
    // Speed buff uygula (roll bitince)
    ApplySpeedBuff(caster);
    
    // Speed buff VFX spawn et
    SpawnSpeedBuffVFX(caster);
}
        private void SpawnRollGhost(GameObject caster, Character4D character4D, MonoBehaviour monoBehaviour)
    {
        if (character4D == null) return;

        GameObject ghostObj = new GameObject("EvasiveRollGhost");
        ghostObj.transform.position = caster.transform.position;
        ghostObj.transform.rotation = caster.transform.rotation;

        // Ana karakterin active parçasını al
        var activeCharacter = character4D.Active;
        if (activeCharacter == null) return;

        // Ghost sprite'ları oluştur
        CreateGhostSprites(ghostObj, activeCharacter);

        // Fade out başlat
        monoBehaviour.StartCoroutine(FadeOutRollGhost(ghostObj));
    }
    
    // YENİ - Ghost sprite oluşturma
    private void CreateGhostSprites(GameObject ghostObj, Character activeCharacter)
    {
        // Body sprites
        for (int i = 0; i < activeCharacter.BodyRenderers.Count; i++)
        {
            var bodyRenderer = activeCharacter.BodyRenderers[i];
            if (bodyRenderer.sprite != null && bodyRenderer.enabled)
            {
                CreateGhostSprite(ghostObj, bodyRenderer, $"RollGhostBody_{i}");
            }
        }

        // Head sprite
        if (activeCharacter.HeadRenderer.sprite != null && activeCharacter.HeadRenderer.enabled)
        {
            CreateGhostSprite(ghostObj, activeCharacter.HeadRenderer, "RollGhostHead");
        }

        // Hair sprite
        if (activeCharacter.HairRenderer.sprite != null && activeCharacter.HairRenderer.enabled)
        {
            CreateGhostSprite(ghostObj, activeCharacter.HairRenderer, "RollGhostHair");
        }
        
        // Equipment sprites
        for (int i = 0; i < activeCharacter.ArmorRenderers.Count; i++)
        {
            var armorRenderer = activeCharacter.ArmorRenderers[i];
            if (armorRenderer.sprite != null && armorRenderer.enabled)
            {
                CreateGhostSprite(ghostObj, armorRenderer, $"RollGhostArmor_{i}");
            }
        }
    }
    
    // YENİ - Tek ghost sprite oluşturma
    private void CreateGhostSprite(GameObject parent, SpriteRenderer original, string name)
    {
        GameObject spriteObj = new GameObject(name);
        spriteObj.transform.SetParent(parent.transform);
        spriteObj.transform.localPosition = original.transform.localPosition;
        spriteObj.transform.localRotation = original.transform.localRotation;
        spriteObj.transform.localScale = original.transform.localScale * GHOST_SCALE_FACTOR;

        SpriteRenderer ghostRenderer = spriteObj.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = original.sprite;
        ghostRenderer.sortingLayerName = original.sortingLayerName;
        ghostRenderer.sortingOrder = original.sortingOrder - 1; // Arkada render et
        
        // Ghost rengi - hafif mavi ton
        Color ghostColor = new Color(0.7f, 0.9f, 1f, GHOST_ALPHA);
        ghostRenderer.color = ghostColor;
    }
    
    // YENİ - Ghost fade out
    private IEnumerator FadeOutRollGhost(GameObject ghostObj)
    {
        float elapsed = 0f;
        var renderers = ghostObj.GetComponentsInChildren<SpriteRenderer>();
        var initialAlphas = new float[renderers.Length];
        
        for (int i = 0; i < renderers.Length; i++)
        {
            initialAlphas[i] = renderers[i].color.a;
        }

        while (elapsed < GHOST_FADE_DURATION)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / GHOST_FADE_DURATION;
            float alpha = Mathf.Lerp(GHOST_ALPHA, 0f, progress);

            for (int i = 0; i < renderers.Length; i++)
            {
                Color color = renderers[i].color;
                color.a = alpha;
                renderers[i].color = color;
            }

            yield return null;
        }

        Object.Destroy(ghostObj);
    }
    
    private float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
    private void SpawnSpeedBuffVFX(GameObject caster)
    {
        var prefab = Resources.Load<GameObject>("VFX/EvasiveRoll_SpeedBuff");
        if (prefab != null)
        {
            GameObject speedEffect = Object.Instantiate(prefab);
            
            // Caster'a bağla
            speedEffect.transform.SetParent(caster.transform);
            speedEffect.transform.localPosition = Vector3.zero;
            
            // Particle systems'i local space'e çevir
            ParticleSystem[] allParticles = speedEffect.GetComponentsInChildren<ParticleSystem>(true);
            foreach (var ps in allParticles)
            {
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                ps.Clear();
                ps.Play();
            }
            
            Object.Destroy(speedEffect, SPEED_BUFF_DURATION);
        }
    }
}