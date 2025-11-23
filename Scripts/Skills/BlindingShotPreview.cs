using UnityEngine;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using System.Collections.Generic;

public class BlindingShotPreview : BaseSkillPreview
{
    public override string SkillId => "blinding_shot";
    
    private const float SKILL_RANGE = 10f; // BlindingShotExecutor ile aynı
    
    private GameObject activeTargetAura;
    private GameObject targetAuraPrefab;
    private GameObject currentTarget;
    
    protected override void CreatePreviewObject()
    {
        previewObject = new GameObject("BlindingShot_Preview");
        previewObject.transform.SetParent(transform);
        
        // Target_Aura prefab'ını yükle
        LoadTargetAuraPrefab();
    }
    
    private void LoadTargetAuraPrefab()
    {
        targetAuraPrefab = Resources.Load<GameObject>("VFX/Target_Aura");
        if (targetAuraPrefab == null)
        {
            Debug.LogError("[BlindingShotPreview] Target_Aura prefab not found in Resources/VFX/!");
        }
    }
    
    public override void UpdatePreview(GameObject caster, Vector2 direction)
    {
        if (previewObject == null || targetAuraPrefab == null) return;

        Vector3 casterPos = caster.transform.position;
        
        // En yakın target'ı bul (BlindingShotExecutor mantığıyla)
        GameObject nearestTarget = FindNearestTarget(caster, casterPos);
        
        // Target değiştiyse aura'yı güncelle
        if (nearestTarget != currentTarget)
        {
            ClearTargetAura();
            currentTarget = nearestTarget;
            
            if (currentTarget != null)
            {
                SpawnTargetAura(currentTarget);
            }
        }
    }
    
private GameObject FindNearestTarget(GameObject caster, Vector3 casterPosition)
{
    var character4D = caster.GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
    Vector2 direction = character4D?.Direction ?? Vector2.down;

    // BlindingShotExecutor ile aynı mantığı kullan
    return SkillTargetingUtils.FindClosestTarget(casterPosition, SKILL_RANGE, caster, direction, 0.7f);
}

    
    private void SpawnTargetAura(GameObject target)
    {
        if (targetAuraPrefab == null || target == null) return;
        
        Vector3 auraPosition = target.transform.position;
        activeTargetAura = Instantiate(targetAuraPrefab, auraPosition, Quaternion.identity);
        
        // Aura controller ekle
        TargetAuraController controller = activeTargetAura.GetComponent<TargetAuraController>();
        if (controller == null)
        {
            controller = activeTargetAura.AddComponent<TargetAuraController>();
        }
        controller.SetTarget(target);
    }
    
    private void ClearTargetAura()
    {
        if (activeTargetAura != null)
        {
            Destroy(activeTargetAura);
            activeTargetAura = null;
        }
    }
    
    public override void HidePreview()
    {
        base.HidePreview();
        
        ClearTargetAura();
        currentTarget = null;
    }
    
    protected override void OnDestroy()
    {
        base.OnDestroy();
        
        ClearTargetAura();
    }
}