using UnityEngine;
using Fusion;
using System.Collections;

using System.Collections.Generic;
public class TemporaryBuffSystem : NetworkBehaviour
{
    [Networked] public float NetworkSpeedMultiplier { get; set; } = 1f;
    [Networked] public float NetworkBuffEndTime { get; set; } = 0f;
    [Networked] public float NetworkDamageReductionMultiplier { get; set; } = 1f;
[Networked] public float NetworkDamageReductionEndTime { get; set; } = 0f;
    // Class'ın başına event'leri ekle
    public event System.Action<string, Sprite, float> OnBuffStarted;
    public event System.Action<string> OnBuffEnded;
    [Header("Accuracy Debuff Settings")]
    [SerializeField] private Sprite accuracyDebuffIcon;

[Header("Slow Debuff Settings")]
[SerializeField] private Sprite slowDebuffIcon;
[Header("Buff Icons")]
[SerializeField] private Sprite speedBuffIcon; // Speed buff için (mevcut)
[SerializeField] private Sprite damageReductionBuffIcon; // Damage reduction için (mevcut)
[SerializeField] private Sprite attackSpeedBuffIcon; // YENİ - Attack speed buff için
[SerializeField] private Sprite damageDebuffIcon; // YENİ - Damage debuff için
    [SerializeField] private Sprite defaultBuffIcon; // Fallback icon (mevcut)
    [Header("Invulnerability Settings")]
[SerializeField] private Sprite invulnerabilityIcon; // YENİ - Invul icon
[SerializeField] private Color invulnerabilityColor = new Color(0.6f, 0.6f, 0.7f, 1f); // YENİ - Metalik gri

// Invulnerability visual effect için değişkenler
private bool isInvulnerabilityEffectActive = false;
private Dictionary<SpriteRenderer, Color> invulOriginalColors = new Dictionary<SpriteRenderer, Color>();
private Coroutine invulnerabilityVisualCoroutine;
private Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character lastActiveCharacter;

[Networked] public float NetworkSlowMultiplier { get; set; } = 1f;
[Networked] public float NetworkSlowEndTime { get; set; } = 0f;
    [Networked] public float NetworkAttackSpeedMultiplier { get; set; } = 1f;
[Networked] public float NetworkAttackSpeedEndTime { get; set; } = 0f;
[Networked] public float NetworkDamageDebuffMultiplier { get; set; } = 1f;
    [Networked] public float NetworkDamageDebuffEndTime { get; set; } = 0f;
[Networked] public float NetworkAccuracyDebuffMultiplier { get; set; } = 1f;
    [Networked] public float NetworkAccuracyDebuffEndTime { get; set; } = 0f;
[Networked] public bool NetworkIsInvulnerable { get; set; } = false;
[Networked] public float NetworkInvulnerabilityEndTime { get; set; } = 0f;

    public void ApplyAttackSpeedBuff(float multiplier, float duration)
    {
        if (!Object.HasStateAuthority) return;

        NetworkAttackSpeedMultiplier = multiplier;
        NetworkAttackSpeedEndTime = Time.time + duration;

        // Doğru icon ile event'i tetikle
        OnBuffStarted?.Invoke("attack_speed_buff", attackSpeedBuffIcon ?? defaultBuffIcon, duration);

        SyncAttackSpeedBuffRPC(multiplier, duration);
    }
    private void Update()
    {
        // Invulnerability effect aktifken direction değişimi kontrol et
        if (isInvulnerabilityEffectActive)
        {
            CheckForDirectionChange();
        }
    }
private void CheckForDirectionChange()
{
    var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
    if (character4D != null && character4D.Active != null)
    {
        // Active character değişti mi?
        if (lastActiveCharacter != character4D.Active)
        {
            
            lastActiveCharacter = character4D.Active;
            
            // Yeni active character'a invulnerability effect'ini uygula
            ApplyInvulnerabilityToNewActiveCharacter();
        }
    }
}

// YENİ - Yeni aktif character'a invul effect uygulama
private void ApplyInvulnerabilityToNewActiveCharacter()
{
    var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
    if (character4D?.Active == null) return;
    
    // Yeni character'ın renderer'larını bul ve effect uygula
    SpriteRenderer[] newRenderers = character4D.Active.GetComponentsInChildren<SpriteRenderer>(true);
    
    foreach (var renderer in newRenderers)
    {
        if (renderer != null && renderer.enabled && renderer.sprite != null)
        {
            // Bu renderer'ı daha önce sakladık mı kontrol et
            if (!invulOriginalColors.ContainsKey(renderer))
            {
                // Yeni renderer - original rengini sakla
                invulOriginalColors[renderer] = renderer.color;
            }
            
            // Metalik gri uygula
            Color originalColor = invulOriginalColors[renderer];
            Color metalicGray = new Color(0.5f, 0.5f, 0.6f, originalColor.a);
            renderer.color = metalicGray;
        }
    }
    
}
// TemporaryBuffSystem.cs - ApplySlowDebuff METHOD'UNU DEĞİŞTİR
public void ApplyInvulnerability(float duration)
{
    if (!Object.HasStateAuthority) return;
    
    NetworkIsInvulnerable = true;
    NetworkInvulnerabilityEndTime = Time.time + duration;
    
    // YENİ - Doğru icon ile event'i tetikle
    OnBuffStarted?.Invoke("invulnerability", invulnerabilityIcon ?? defaultBuffIcon, duration);
    
    SyncInvulnerabilityRPC(duration);
}

// SyncInvulnerabilityRPC metodunu güncelle - timing düzelt
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncInvulnerabilityRPC(float duration)
{
    
    // YENİ - Condition'ı kaldır, direkt başlat
    OnBuffStarted?.Invoke("invulnerability", invulnerabilityIcon ?? defaultBuffIcon, duration);
    StartCoroutine(InvulnerabilityCoroutine(duration));
    
    // Visual effect başlat (Network objesi varsa)
    if (Object != null && Object.IsValid)
    {
        StartInvulnerabilityVisualEffect();
    }
    else
    {
        Debug.LogWarning("[InvulRPC] Network object geçerli değil, visual effect başlatılamadı");
    }
}
private IEnumerator InvulnerabilityCoroutine(float duration)
{
    yield return new WaitForSeconds(duration);
    
    if (Object.HasStateAuthority)
    {
        NetworkIsInvulnerable = false;
        NetworkInvulnerabilityEndTime = 0f;
    }
    
    // YENİ - Visual effect'i durdur
    StopInvulnerabilityVisualEffect();
    
    OnBuffEnded?.Invoke("invulnerability");
}

private void StartInvulnerabilityVisualEffect()
{
    if (isInvulnerabilityEffectActive) 
    {
        return;
    }
    
    isInvulnerabilityEffectActive = true;
    
    // YENİ - Initial active character'ı kaydet
    var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
    if (character4D != null)
    {
        lastActiveCharacter = character4D.Active;
    }
    
    // Önceki effect varsa durdur
    if (invulnerabilityVisualCoroutine != null)
    {
        StopCoroutine(invulnerabilityVisualCoroutine);
    }
    
    invulnerabilityVisualCoroutine = StartCoroutine(InvulnerabilityVisualEffect());
}

// YENİ - Invulnerability visual effect durdurma
private void StopInvulnerabilityVisualEffect()
{
    isInvulnerabilityEffectActive = false;
    
    if (invulnerabilityVisualCoroutine != null)
    {
        StopCoroutine(invulnerabilityVisualCoroutine);
        invulnerabilityVisualCoroutine = null;
    }
    
    // YENİ - Active character reference'ını temizle
    lastActiveCharacter = null;
    
    // Renkleri normale döndür
    RestoreInvulOriginalColors();
}

// YENİ - Invulnerability visual effect coroutine
private IEnumerator InvulnerabilityVisualEffect()
{
    
    // Original renkleri sakla
    StoreInvulOriginalColors();
    
    
    // Metalik gri rengi uygula
    ApplyInvulnerabilityColor();
    
    // Effect aktif olduğu sürece bekle
    while (isInvulnerabilityEffectActive)
    {
        yield return null;
    }
    
    
    // Renkleri normale döndür
    RestoreInvulOriginalColors();
}

// YENİ - Original renkleri saklama
private void StoreInvulOriginalColors()
{
    if (invulOriginalColors.Count > 0) 
    {
        return;
    }
    
    invulOriginalColors.Clear();
    
    var character4D = GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
    if (character4D == null)
    {
        Debug.LogError("[InvulVisual] Character4D bulunamadı!");
        return;
    }
    
    // YENİ - TÜM direction'ları için renkleri sakla (sadece aktif olanı değil)
    var allCharacters = new[] { character4D.Front, character4D.Back, character4D.Left, character4D.Right };
    
    foreach (var character in allCharacters)
    {
        if (character != null)
        {
            SpriteRenderer[] childRenderers = character.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var renderer in childRenderers)
            {
                if (renderer != null && renderer.sprite != null && !invulOriginalColors.ContainsKey(renderer))
                {
                    invulOriginalColors[renderer] = renderer.color;
                }
            }
        }
    }
    
}

// YENİ - Invulnerability rengini uygulama
private void ApplyInvulnerabilityColor()
{
    int appliedCount = 0;
    
    foreach (var kvp in invulOriginalColors)
    {
        if (kvp.Key != null && kvp.Key.enabled)
        {
            // YENİ - Daha belirgin metalik gri
            Color originalColor = kvp.Value;
            
            // Metalik gri - sabit renk kullan
            Color metalicGray = new Color(0.5f, 0.5f, 0.6f, originalColor.a);
            
            kvp.Key.color = metalicGray;
            appliedCount++;
        }
    }
    
}

// YENİ - Original renkleri geri döndürme
private void RestoreInvulOriginalColors()
{
    foreach (var kvp in invulOriginalColors)
    {
        if (kvp.Key != null && kvp.Key.enabled)
        {
            kvp.Key.color = kvp.Value;
        }
    }
    
    invulOriginalColors.Clear();
}

// YENİ - Component destroy olduğunda cleanup
private void OnDestroy()
{
    if (invulnerabilityVisualCoroutine != null)
    {
        StopCoroutine(invulnerabilityVisualCoroutine);
    }
    
    invulOriginalColors.Clear();
}

public bool IsInvulnerable()
{
    // Network ready kontrolü ekle
if (Object == null || !Object.IsValid) { return false; }
    
    if (Time.time < NetworkInvulnerabilityEndTime)
    {
        return NetworkIsInvulnerable;
    }
    return false;
}

public float GetRemainingInvulnerabilityTime()
{
    // Network ready kontrolü ekle
if (Object == null || !Object.IsValid) {
    return 0f;
}
    
    if (IsInvulnerable())
    {
        return NetworkInvulnerabilityEndTime - Time.time;
    }
    return 0f;
}
public void ApplySlowDebuff(float slowPercent, float duration)
{
    string playerName = GetComponent<PlayerStats>()?.GetPlayerDisplayName() ?? "Unknown";
    
    if (Object == null)
    {
        Debug.LogError($"[SLOW-DEBUG-{playerName}] Object is NULL!");
        return;
    }
    
    if (!Object.HasStateAuthority) 
    {
        Debug.LogWarning($"[SLOW-DEBUG-{playerName}] No StateAuthority!");
        return;
    }
    
    float multiplier = 1f - (slowPercent / 100f);
    NetworkSlowMultiplier = multiplier;
    NetworkSlowEndTime = Time.time + duration;
    
    
    // Event null check
    if (OnBuffStarted != null)
    {
        
        Sprite iconToUse = slowDebuffIcon;
        if (iconToUse == null)
        {
            Debug.LogWarning($"[SLOW-DEBUG-{playerName}] slowDebuffIcon is null, using defaultBuffIcon");
            iconToUse = defaultBuffIcon;
        }
        
        if (iconToUse == null)
        {
            Debug.LogError($"[SLOW-DEBUG-{playerName}] Both slowDebuffIcon and defaultBuffIcon are null!");
            // Event'i icon olmadan tetikle
            OnBuffStarted?.Invoke("slow_debuff", null, duration);
        }
        else
        {
            OnBuffStarted?.Invoke("slow_debuff", iconToUse, duration);
        }
    }
    else
    {
        Debug.LogWarning($"[SLOW-DEBUG-{playerName}] OnBuffStarted event is NULL!");
    }
    
    SyncSlowDebuffRPC(multiplier, duration);
    
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncSlowDebuffRPC(float multiplier, float duration)
{
    // Condition kaldırıldı - direkt başlat
    OnBuffStarted?.Invoke("slow_debuff", slowDebuffIcon ?? defaultBuffIcon, duration);
    StartCoroutine(SlowDebuffCoroutine(duration));
}
public void ApplyAccuracyDebuff(float reductionPercent, float duration)
{
    if (!Object.HasStateAuthority) return;
    
    float multiplier = 1f - (reductionPercent / 100f);
    NetworkAccuracyDebuffMultiplier = multiplier;
    NetworkAccuracyDebuffEndTime = Time.time + duration;
    
    OnBuffStarted?.Invoke("accuracy_debuff", accuracyDebuffIcon ?? defaultBuffIcon, duration);
    
    SyncAccuracyDebuffRPC(multiplier, duration);
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncAccuracyDebuffRPC(float multiplier, float duration)
{
    // Condition kaldırıldı - direkt başlat
    OnBuffStarted?.Invoke("accuracy_debuff", accuracyDebuffIcon ?? defaultBuffIcon, duration);
    StartCoroutine(AccuracyDebuffCoroutine(duration));
}

private IEnumerator AccuracyDebuffCoroutine(float duration)
{
    yield return new WaitForSeconds(duration);
    
    if (Object.HasStateAuthority)
    {
        NetworkAccuracyDebuffMultiplier = 1f;
        NetworkAccuracyDebuffEndTime = 0f;
    }
    
    OnBuffEnded?.Invoke("accuracy_debuff");
}

public float GetCurrentAccuracyMultiplier()
{
    // Network ready kontrolü ekle
if (Object == null || !Object.IsValid) {
    return 1f;
}
    
    if (Time.time < NetworkAccuracyDebuffEndTime)
    {
        return NetworkAccuracyDebuffMultiplier;
    }
    return 1f;
}
private IEnumerator SlowDebuffCoroutine(float duration)
{
    yield return new WaitForSeconds(duration);
    
    if (Object.HasStateAuthority)
    {
        NetworkSlowMultiplier = 1f;
        NetworkSlowEndTime = 0f;
    }
    
    OnBuffEnded?.Invoke("slow_debuff");
}

public float GetCurrentSlowMultiplier()
{
    // Network ready kontrolü ekle
if (Object == null || !Object.IsValid) {
    return 1f;
}
    
    if (Time.time < NetworkSlowEndTime)
    {
        return NetworkSlowMultiplier;
    }
    return 1f;
}
public void ApplyDamageDebuff(float reductionPercent, float duration)
{
    if (!Object.HasStateAuthority) return;
    
    float multiplier = 1f - (reductionPercent / 100f);
    NetworkDamageDebuffMultiplier = multiplier;
    NetworkDamageDebuffEndTime = Time.time + duration;
    
    // Doğru icon ile event'i tetikle
    OnBuffStarted?.Invoke("damage_debuff", damageDebuffIcon ?? defaultBuffIcon, duration);
    
    SyncDamageDebuffRPC(multiplier, duration);
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncAttackSpeedBuffRPC(float multiplier, float duration)
{
    // Condition kaldırıldı - direkt başlat
    OnBuffStarted?.Invoke("attack_speed_buff", attackSpeedBuffIcon ?? defaultBuffIcon, duration);
    StartCoroutine(AttackSpeedBuffCoroutine(duration));
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncDamageDebuffRPC(float multiplier, float duration)
{
    // Condition kaldırıldı - direkt başlat
    OnBuffStarted?.Invoke("damage_debuff", damageDebuffIcon ?? defaultBuffIcon, duration);
    StartCoroutine(DamageDebuffCoroutine(duration));
}

private IEnumerator AttackSpeedBuffCoroutine(float duration)
{
    yield return new WaitForSeconds(duration);
    
    if (Object.HasStateAuthority)
    {
        NetworkAttackSpeedMultiplier = 1f;
        NetworkAttackSpeedEndTime = 0f;
    }
    
    OnBuffEnded?.Invoke("attack_speed_buff");
}

private IEnumerator DamageDebuffCoroutine(float duration)
{
    yield return new WaitForSeconds(duration);
    
    if (Object.HasStateAuthority)
    {
        NetworkDamageDebuffMultiplier = 1f;
        NetworkDamageDebuffEndTime = 0f;
    }
    
    OnBuffEnded?.Invoke("damage_debuff");
}

public float GetCurrentAttackSpeedMultiplier()
{
    // Network ready kontrolü ekle
if (Object == null || !Object.IsValid) {
    return 1f;
}
    
    if (Time.time < NetworkAttackSpeedEndTime)
    {
        return NetworkAttackSpeedMultiplier;
    }
    return 1f;
}

public float GetCurrentDamageDebuffMultiplier()
{
    // Network ready kontrolü ekle
if (Object == null || !Object.IsValid) {
    return 1f;
}
    
    if (Time.time < NetworkDamageDebuffEndTime)
    {
        return NetworkDamageDebuffMultiplier;
    }
    return 1f;
}
    private PlayerStats playerStats;

    
    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
    }
    
public void ApplySpeedBuff(float multiplier, float duration)
{
    if (!Object.HasStateAuthority) return;
    
    NetworkSpeedMultiplier = multiplier;
    NetworkBuffEndTime = Time.time + duration;
    
    // Doğru icon ile event'i tetikle
    OnBuffStarted?.Invoke("speed_buff", speedBuffIcon ?? defaultBuffIcon, duration);
    
    SyncSpeedBuffRPC(multiplier, duration);
}
public void ApplyDamageReductionBuff(float reductionPercent, float duration)
{
    if (!Object.HasStateAuthority) return;
    
    float multiplier = 1f - (reductionPercent / 100f);
    NetworkDamageReductionMultiplier = multiplier;
    NetworkDamageReductionEndTime = Time.time + duration;
    
    // Doğru icon ile event'i tetikle
    OnBuffStarted?.Invoke("damage_reduction", damageReductionBuffIcon ?? defaultBuffIcon, duration);
    
    SyncDamageReductionBuffRPC(multiplier, duration);
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncDamageReductionBuffRPC(float multiplier, float duration)
{
    // Condition kaldırıldı - direkt başlat
    OnBuffStarted?.Invoke("damage_reduction", damageReductionBuffIcon ?? defaultBuffIcon, duration);
    StartCoroutine(DamageReductionBuffCoroutine(duration));
}

private IEnumerator DamageReductionBuffCoroutine(float duration)
{
    yield return new WaitForSeconds(duration);
    
    if (Object.HasStateAuthority)
    {
        NetworkDamageReductionMultiplier = 1f;
        NetworkDamageReductionEndTime = 0f;
    }
    
    OnBuffEnded?.Invoke("damage_reduction"); // Buff tipini belirt
}

public float GetCurrentDamageReductionMultiplier()
{
    // Network ready kontrolü ekle
if (Object == null || !Object.IsValid) {
    return 1f;
}
    
    if (Time.time < NetworkDamageReductionEndTime)
    {
        return NetworkDamageReductionMultiplier;
    }
    return 1f;
}

[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncSpeedBuffRPC(float multiplier, float duration)
{
    // Condition kaldırıldı - direkt başlat
    OnBuffStarted?.Invoke("speed_buff", speedBuffIcon ?? defaultBuffIcon, duration);
    StartCoroutine(SpeedBuffCoroutine(duration));
}
private IEnumerator SpeedBuffCoroutine(float duration)
{
    yield return new WaitForSeconds(duration);
    
    if (Object.HasStateAuthority)
    {
        NetworkSpeedMultiplier = 1f;
        NetworkBuffEndTime = 0f;
    }
    
    OnBuffEnded?.Invoke("speed_buff"); // Buff tipini belirt
}
    
public float GetCurrentSpeedMultiplier()
{
    // Network ready kontrolü ekle
if (Object == null || !Object.IsValid) {
    return 1f;
}
    
    if (Time.time < NetworkBuffEndTime)
    {
        return NetworkSpeedMultiplier;
    }
    return 1f;
}
}