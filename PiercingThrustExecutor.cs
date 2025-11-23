using UnityEngine;
using System.Collections.Generic;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Collections;
using Fusion.Addons.Physics;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using System.Linq;

public class PiercingThrustExecutor : BaseSkillExecutor
{
    public override string SkillId => "piercing_thrust";
    
    // Public static yapalım ki preview'dan erişebilsin
    public static float GetSkillRange() => 7.5f; // 7.5f olarak ayarladın
    
    private const float DAMAGE_MULTIPLIER = 1.4f;
    private const float DASH_DURATION = 0.3f;
    private const float SPEED_BUFF_DURATION = 3f;
    private const float SPEED_BUFF_MULTIPLIER = 1.2f;
    private const float GHOST_SPAWN_INTERVAL = 0.04f;
    private const float GHOST_FADE_DURATION = 0.4f;
    private const float GHOST_ALPHA = 0.5f;
    private const float GHOST_SCALE_FACTOR = 0.4f;
    
    // SKILL_RANGE'i static method'tan al
    private static float SKILL_RANGE => GetSkillRange();
public override void Execute(GameObject caster, SkillInstance skillInstance)
    {
        // Client-side sadece VFX - animation ExecuteDashMovement içinde olacak
        // StartDashAnimation çağrısını kaldır
    }
public void ExecuteOnServer(GameObject caster, SkillInstance skillInstance, SkillSystem skillSystem)
{
    
    if (!skillSystem.Object.HasStateAuthority) 
    {
        return;
    }
    
    // Server-side cooldown set
    skillInstance.lastUsedTime = Time.time;
    
    var playerStats = GetPlayerStats(caster);
    var character4D = caster.GetComponent<Character4D>();
    var networkController = caster.GetComponent<NetworkCharacterController>();
    
    if (playerStats == null || character4D == null || networkController == null)
    {
        return;
    }
    
    Vector2 dashDirection = character4D.Direction;
    Vector3 startPosition = caster.transform.position;
    
    // Calculate dash endpoint
    Vector3 dashEndpoint = CalculateDashEndpoint(startPosition, dashDirection);
    
    // Find targets along dash path
    List<GameObject> targetsInPath = FindTargetsInDashPath(caster, startPosition, dashEndpoint);
    
    // Damage calculation
    float baseDamage = playerStats.FinalDamage * DAMAGE_MULTIPLIER;
    
    // Apply damage to all targets
    foreach (var target in targetsInPath)
    {
        ApplyDamageToTarget(caster, target, baseDamage, skillSystem);
    }
    
    // Speed buff if 2+ targets hit
    if (targetsInPath.Count >= 2)
    {
        ApplySpeedBuff(caster, skillSystem);
    }
    
    // Execute dash movement and VFX
    skillSystem.ExecutePiercingThrustRPC(
        startPosition, 
        dashEndpoint, 
        dashDirection, 
        targetsInPath.Count,
        targetsInPath.Count >= 2
    );
}
    
private Vector3 CalculateDashEndpoint(Vector3 startPos, Vector2 direction)
{
    // Piercing thrust obstacles'ları ignore eder, sadece skill range'e kadar gider
    Vector3 targetEndpoint = startPos + (Vector3)(direction * SKILL_RANGE);
    
    // Sadece map boundary gibi büyük duvarları kontrol et (isteğe bağlı)
    // Küçük engelleri ignore et
    return targetEndpoint;
}
    
private List<GameObject> FindTargetsInDashPath(GameObject caster, Vector3 startPos, Vector3 endPos)
{
    return SkillTargetingUtils.FindTargetsInLine(startPos, endPos, 1.2f, caster);
}

    private void ApplySpeedBuff(GameObject caster, SkillSystem skillSystem)
    {
        // Speed buff implementation
        var tempBuff = caster.GetComponent<TemporaryBuffSystem>();
        if (tempBuff == null)
        {
            tempBuff = caster.gameObject.AddComponent<TemporaryBuffSystem>();
        }
        
        tempBuff.ApplySpeedBuff(SPEED_BUFF_MULTIPLIER, SPEED_BUFF_DURATION);
    }



    public void ExecuteVFX(GameObject caster, Vector3 startPos, Vector3 endPos, Vector2 direction, int targetCount, bool hasSpeedBuff)
    {
        // Character dash movement
        var networkController = caster.GetComponent<NetworkCharacterController>();
        if (networkController != null)
        {
            var skillSystem = caster.GetComponent<SkillSystem>();
            if (skillSystem != null)
            {
                // *** MonoBehaviour referansı geç ***
                skillSystem.StartCoroutine(ExecuteDashMovement(caster, startPos, endPos, skillSystem));
            }
        }

        // VFX spawning
        SpawnDashTrailVFX(startPos, endPos, direction);

        if (targetCount > 0)
        {
            SpawnHitImpactVFX(endPos);
        }

        if (hasSpeedBuff)
        {
            SpawnSpeedBuffVFX(caster);
        }
    }
private IEnumerator ExecuteDashMovement(GameObject caster, Vector3 startPos, Vector3 endPos, MonoBehaviour monoBehaviour)
{
    var networkController = caster.GetComponent<NetworkCharacterController>();
    var networkRigidbody = caster.GetComponent<NetworkRigidbody2D>();
    var character4D = caster.GetComponent<Character4D>();
    var casterCollider = caster.GetComponent<Collider2D>();

    if (networkController == null || networkRigidbody == null)
    {
        yield break;
    }

    // Disable movement input during dash
    networkController.SetMovementEnabled(false);
    
    // Physics interaction'ları kapat
    bool originalIsTrigger = false;
    RigidbodyType2D originalBodyType = RigidbodyType2D.Dynamic;
    bool originalColliderEnabled = true;
    
    if (casterCollider != null)
    {
        originalIsTrigger = casterCollider.isTrigger;
        originalColliderEnabled = casterCollider.enabled;
        casterCollider.enabled = false; // Collider'ı tamamen kapat
    }
    
    if (networkRigidbody.Rigidbody != null)
    {
        originalBodyType = networkRigidbody.Rigidbody.bodyType;
        networkRigidbody.Rigidbody.bodyType = RigidbodyType2D.Kinematic;
    }

    // Cast animation başlat
    if (character4D != null)
    {
        character4D.AnimationManager.Cast();
    }

    // Dash parametreleri
    Vector2 dashDirection = (endPos - startPos).normalized;
    float totalDistance = Vector2.Distance(startPos, endPos);
    float dashSpeed = totalDistance / DASH_DURATION;

    // Ghost trail variables
    float lastGhostTime = 0f;
    float elapsedTime = 0f;
    Vector3 currentPos = startPos;

    // Force position update - network sync için
    if (networkRigidbody.Rigidbody != null)
    {
        networkRigidbody.Teleport(startPos, caster.transform.rotation);
    }

    while (elapsedTime < DASH_DURATION)
    {
        float deltaTime = Time.deltaTime;
        elapsedTime += deltaTime;

        // Manual position update
        float moveDistance = dashSpeed * deltaTime;
        currentPos += (Vector3)(dashDirection * moveDistance);
        
        // Network position'ı force update
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
            SpawnGhost(caster, character4D, monoBehaviour);
            lastGhostTime = elapsedTime;
        }

        // Distance check
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

    // Physics ayarlarını geri döndür
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

    // Movement'i enable et
    networkController.SetMovementEnabled(true);

    // Animasyonu idle'a döndür
    if (character4D != null && character4D.AnimationManager != null)
    {
        character4D.AnimationManager.SetState(CharacterState.Idle);
    }
}
private void SpawnGhost(GameObject caster, Character4D character4D, MonoBehaviour monoBehaviour)
{
    if (character4D == null) return;

    GameObject ghostObj = new GameObject("PiercingGhost");
    
    // Ghost pozisyonunu character yönüne göre ayarla
    Vector3 ghostPosition = caster.transform.position;
    
    // Sağ/sol hareketlerde Y offset ekle
    if (character4D.Direction == Vector2.left || character4D.Direction == Vector2.right)
    {
        ghostPosition.y += 0.5f; // Y ekseninde yukarı çıkar
    }
    
    ghostObj.transform.position = ghostPosition;
    ghostObj.transform.rotation = caster.transform.rotation;

    // Ana karakterin active parçasını al
    var activeCharacter = character4D.Active;
    if (activeCharacter == null) return;

    // Ghost sprite'ları oluştur
    CreateGhostSprites(ghostObj, activeCharacter);

    // Fade out başlat
    monoBehaviour.StartCoroutine(FadeOutGhost(ghostObj));
}

private void CreateGhostSprites(GameObject ghostObj, Character activeCharacter)
{
    // Body sprites
    for (int i = 0; i < activeCharacter.BodyRenderers.Count; i++)
    {
        var bodyRenderer = activeCharacter.BodyRenderers[i];
        if (bodyRenderer.sprite != null && bodyRenderer.enabled)
        {
            CreateGhostSprite(ghostObj, bodyRenderer, $"GhostBody_{i}");
        }
    }

    // Head sprite
    if (activeCharacter.HeadRenderer.sprite != null && activeCharacter.HeadRenderer.enabled)
    {
        CreateGhostSprite(ghostObj, activeCharacter.HeadRenderer, "GhostHead");
    }

    // Hair sprite (opsiyonel)
    if (activeCharacter.HairRenderer.sprite != null && activeCharacter.HairRenderer.enabled)
    {
        CreateGhostSprite(ghostObj, activeCharacter.HairRenderer, "GhostHair");
    }
}

private void CreateGhostSprite(GameObject parent, SpriteRenderer original, string name)
{
    GameObject spriteObj = new GameObject(name);
    spriteObj.transform.SetParent(parent.transform);
    spriteObj.transform.localPosition = original.transform.localPosition;
    spriteObj.transform.localRotation = original.transform.localRotation;
    spriteObj.transform.localScale = original.transform.localScale * GHOST_SCALE_FACTOR; // Scale faktörü uygula

    SpriteRenderer ghostRenderer = spriteObj.AddComponent<SpriteRenderer>();
    ghostRenderer.sprite = original.sprite;
    ghostRenderer.sortingLayerName = original.sortingLayerName;
    ghostRenderer.sortingOrder = original.sortingOrder - 1; // Arkada render et
    
    // Ghost rengi ve şeffaflık
    Color ghostColor = Color.white;
    ghostColor.a = GHOST_ALPHA;
    ghostRenderer.color = ghostColor;
}

private IEnumerator FadeOutGhost(GameObject ghostObj)
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
private IEnumerator SmoothVelocityTransition(NetworkRigidbody2D networkRigidbody, Vector2 startVelocity)
{
    if (networkRigidbody?.Rigidbody == null) yield break;
    
    float transitionTime = 0.05f; // 0.1f'den 0.05f'e düşür - daha hızlı geçiş
    float elapsed = 0f;
    
    while (elapsed < transitionTime)
    {
        elapsed += Time.deltaTime;
        float progress = elapsed / transitionTime;
        
        Vector2 currentVelocity = Vector2.Lerp(startVelocity, Vector2.zero, progress);
        networkRigidbody.Rigidbody.linearVelocity = currentVelocity;
        
        yield return null;
    }
    
    // Son olarak tamamen sıfırla
    networkRigidbody.Rigidbody.linearVelocity = Vector2.zero;
}

private void SpawnHitImpactVFX(Vector3 position)
{
    var prefab = Resources.Load<GameObject>("VFX/PiercingThrust_Impact");
    if (prefab != null)
    {
        GameObject impact = Object.Instantiate(prefab, position, Quaternion.identity);
        Object.Destroy(impact, 0.8f);
    }
}

    
    private void SpawnDashTrailVFX(Vector3 startPos, Vector3 endPos, Vector2 direction)
    {
        var prefab = Resources.Load<GameObject>("VFX/PiercingThrust_DashTrail");
        if (prefab != null)
        {
            Vector3 trailPos = Vector3.Lerp(startPos, endPos, 0.5f);
            GameObject trail = Object.Instantiate(prefab, trailPos, Quaternion.identity);
            
            // Direction alignment
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            trail.transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
            
            // Scale to dash length
            float dashLength = Vector3.Distance(startPos, endPos);
            trail.transform.localScale = new Vector3(dashLength / 4f, 1f, 1f);
            
            Object.Destroy(trail, 1f);
        }
    }
private void SpawnSpeedBuffVFX(GameObject caster)
{
    var prefab = Resources.Load<GameObject>("VFX/SpeedBuff_Aura");
    if (prefab != null)
    {
        GameObject aura = Object.Instantiate(prefab);
        
        // Karaktere bağla
        aura.transform.SetParent(caster.transform);
        aura.transform.localPosition = Vector3.zero;
        
        // Particle system'leri local space'e çevir
        ParticleSystem[] allParticles = aura.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var ps in allParticles)
        {
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            ps.Clear();
            ps.Play();
        }
        
        Object.Destroy(aura, SPEED_BUFF_DURATION);
    }
}
}