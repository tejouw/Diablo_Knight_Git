using UnityEngine;
using System.Collections.Generic;

public class SkillPreviewManager : MonoBehaviour
{
    private static SkillPreviewManager instance;
    public static SkillPreviewManager Instance => instance;
    
    private Dictionary<string, ISkillPreview> skillPreviews = new Dictionary<string, ISkillPreview>();
    private ISkillPreview currentActivePreview;
    private GameObject currentCaster;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        RegisterSkillPreviews();
    }
    
private void RegisterSkillPreviews()
{
    // CleaveStrike preview'ı ekle
    var cleavePreview = gameObject.AddComponent<CleaveStrikePreview>();
    skillPreviews[cleavePreview.SkillId] = cleavePreview;
    
    // PiercingThrust preview'ı ekle
    var piercingPreview = gameObject.AddComponent<PiercingThrustPreview>();
    skillPreviews[piercingPreview.SkillId] = piercingPreview;
    
    // SeismicRupture preview'ı ekle
    var seismicPreview = gameObject.AddComponent<SeismicRupturePreview>();
    skillPreviews[seismicPreview.SkillId] = seismicPreview;
    
    // BlindingShot preview'ı ekle
    var blindingShotPreview = gameObject.AddComponent<BlindingShotPreview>();
    skillPreviews[blindingShotPreview.SkillId] = blindingShotPreview;
    
    // RainOfArrows preview'ı ekle
    var rainOfArrowsPreview = gameObject.AddComponent<RainOfArrowsPreview>();
    skillPreviews[rainOfArrowsPreview.SkillId] = rainOfArrowsPreview;
    
    // PiercingArrow preview'ı ekle - YENİ SATIR
    var piercingArrowPreview = gameObject.AddComponent<PiercingArrowPreview>();
    skillPreviews[piercingArrowPreview.SkillId] = piercingArrowPreview;
    
        // ColossusStance preview'ı ekle - YENİ SATIR
    var colossusStancePreview = gameObject.AddComponent<ColossusStancePreview>();
    skillPreviews[colossusStancePreview.SkillId] = colossusStancePreview;
    
    // Gelecekte diğer skill preview'ları buraya eklenecek
}
    
public void ShowSkillPreview(string skillId, GameObject caster)
{
    
    HideCurrentPreview();

    if (skillPreviews.TryGetValue(skillId, out var preview))
    {
        
        currentActivePreview = preview;
        currentCaster = caster;

        var character4D = caster.GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
        Vector2 direction = character4D?.Direction ?? Vector2.down;


        preview.ShowPreview(caster, direction);
        
    }

}
    
public void UpdateCurrentPreview()
{
    if (currentActivePreview != null && currentCaster != null)
    {
        var character4D = currentCaster.GetComponent<Assets.HeroEditor4D.Common.Scripts.CharacterScripts.Character4D>();
        Vector2 direction = character4D?.Direction ?? Vector2.down;
        
        currentActivePreview.UpdatePreview(currentCaster, direction);
    }
}
    
    public void HideCurrentPreview()
    {
        if (currentActivePreview != null)
        {
            currentActivePreview.HidePreview();
            currentActivePreview = null;
            currentCaster = null;
        }
    }
    
    public bool IsPreviewActive => currentActivePreview != null && currentActivePreview.IsPreviewActive;
    
    private void Update()
    {
        // Preview aktifse sürekli güncelle
        if (IsPreviewActive)
        {
            UpdateCurrentPreview();
        }
    }
}