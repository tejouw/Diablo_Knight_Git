using System;
using System.Collections.Generic;
using System.Linq;
using Assets.HeroEditor4D.Common.Scripts.Collections;
using Assets.HeroEditor4D.Common.Scripts.Data;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using UnityEngine;

namespace Assets.HeroEditor4D.Common.Scripts.CharacterScripts
{
    /// <summary>
    /// Character presentation in editor. Contains sprites, renderers, animation and so on.
    /// </summary>
    public partial class Character : MonoBehaviour
    {
        public SpriteCollection SpriteCollection;

        [Header("Body")]
        public List<Sprite> Ears;
        public Sprite Hair;
        public Sprite HairCropped;
        public Sprite Beard;
        public List<Sprite> Body;
        public Sprite Head;

        [Header("Expressions")]
        public string Expression = "Default";
        public List<Expression> Expressions;

        [Header("Equipment")]
        public Sprite Helmet;
        public Sprite PrimaryWeapon;
        public Sprite SecondaryWeapon;
        public Sprite Back;
        public Sprite Quiver;
        public List<Sprite> Shield;
        public List<Sprite> Armor;
        public List<Sprite> CompositeWeapon;

        [Header("Accessories")]
        public Sprite Makeup;
        public Sprite Mask;
        public List<Sprite> Earrings;
        public Sprite Wings;

        [Header("Body renderers")]
        public List<SpriteRenderer> BodyRenderers;
        public SpriteRenderer HeadRenderer;
        public List<SpriteRenderer> EarsRenderers;
        public SpriteRenderer HairRenderer;
        public SpriteRenderer BeardRenderer;
        public SpriteRenderer EyebrowsRenderer;
        public SpriteRenderer EyesRenderer;
        public SpriteRenderer MouthRenderer;

        [Header("Equipment renderers")]
        public SpriteRenderer HelmetRenderer;
        public SpriteRenderer PrimaryWeaponRenderer;
        public SpriteRenderer SecondaryWeaponRenderer;
        public List<SpriteRenderer> ArmorRenderers;
        public List<SpriteRenderer> VestRenderers;
        public List<SpriteRenderer> BracersRenderers;
        public List<SpriteRenderer> LeggingsRenderers;
        public SpriteRenderer BackRenderer;
        public SpriteRenderer QuiverRenderer;
        public List<SpriteRenderer> ShieldRenderers;
        public List<SpriteRenderer> BowRenderers;
        public List<SpriteRenderer> CrossbowRenderers;

        [Header("Accessories renderers")]
        public SpriteRenderer MakeupRenderer;
        public SpriteRenderer MaskRenderer;
        public List<SpriteRenderer> EarringsRenderers;
        public SpriteRenderer WingsRenderer;

        [Header("Materials")]
        public Material DefaultMaterial;
        public Material EyesPaintMaterial;
        public Material EquipmentPaintMaterial;

        [Header("Animation")]
        public WeaponType WeaponType;

        [Header("Anchors")]
	    public Transform AnchorBody;
	    public Transform AnchorSword;
	    public Transform AnchorBow;
        public Transform AnchorFireMuzzle;

        [Header("Service")]
		public LayerManager LayerManager;

        [Header("Custom")]
        public List<Sprite> Underwear;
        public Color UnderwearColor;
        public List<EquipmentTag> EquipmentTags;
        public bool ShowHelmet = true;

        /// <summary>
        /// Initializes character renderers with selected sprites.
        /// </summary>
// DEĞİŞTİR
public void Initialize()
{
    try
    {
        TryInitialize();
        
        // LOG EKLE
        int enabledCount = 0;
        int disabledCount = 0;
        var allRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var r in allRenderers)
        {
            if (r.enabled) enabledCount++;
            else disabledCount++;
        }
    }
    catch (Exception e)
    {
        Debug.LogWarningFormat("Unable to initialize character {0}: {1}", name, e.Message);
    }
}

		/// <summary>
		/// Initializes character renderers with selected sprites.
		/// </summary>
		private void TryInitialize()
        {
            var expressionNames = Expressions.Select(i => i.Name).ToList();

            if (!expressionNames.Contains("Default") || !expressionNames.Contains("Angry") || !expressionNames.Contains("Dead"))
            {
                throw new Exception("Character must have at least 3 basic expressions: Default, Angry and Dead.");
            }

            if (ShowHelmet)
            {
                var ears = true;

                if (Helmet == null || EquipmentTags.Contains(EquipmentTag.FullHair)) ears = !EquipmentTags.Contains(EquipmentTag.HideEars);
                else ears = EquipmentTags.Contains(EquipmentTag.ShowEars);

                MapSprites(EarsRenderers, ears ? Ears : null);
                SetSprite(HairRenderer, Hair == null ? null : Helmet != null && !EquipmentTags.Contains(EquipmentTag.FullHair) ? HairCropped : Hair);
                SetSprite(HelmetRenderer, Helmet);
            }
            else
            {
                MapSprites(EarsRenderers, Ears);
                SetSprite(HairRenderer, Hair);
                SetSprite(HelmetRenderer, null);
			}

            SetExpression(Expression);
			SetSprite(BeardRenderer, Beard);
			MapSprites(BodyRenderers, Body);
            SetSprite(HeadRenderer, Head);
            MapSprites(ArmorRenderers, Armor);
            SetSprite(BackRenderer, Back);
            SetSprite(QuiverRenderer, Quiver);
            MapSprites(ShieldRenderers, Shield);
			SetSprite(MakeupRenderer, Makeup);
            SetSprite(MaskRenderer, Mask);
            MapSprites(EarringsRenderers, Earrings);
            SetSprite(WingsRenderer, Wings);

    SetSprite(PrimaryWeaponRenderer, PrimaryWeapon);
    SetSprite(SecondaryWeaponRenderer, SecondaryWeapon);
    MapSprites(BowRenderers, CompositeWeapon);
    MapSprites(CrossbowRenderers, CompositeWeapon);
    
    // Weapon type'a göre enabled/disabled yap, ama hepsini render et
    PrimaryWeaponRenderer.enabled = PrimaryWeapon != null;
    SecondaryWeaponRenderer.enabled = SecondaryWeapon != null && WeaponType == WeaponType.Paired;
BowRenderers.ForEach(i => i.enabled = CompositeWeapon != null);
CrossbowRenderers.ForEach(i => i.enabled = CompositeWeapon != null);

    ApplyMaterials();
		}

        private static void SetSprite(SpriteRenderer spriteRenderer, Sprite sprite)
        {
            if (spriteRenderer == null) return;

            spriteRenderer.sprite = sprite;
            spriteRenderer.enabled = sprite != null;
        }

		private void MapSprites(List<SpriteRenderer> spriteRenderers, List<Sprite> sprites)
        {
            spriteRenderers.ForEach(i => MapSprite(i, sprites));
        }

        private void MapSprite(SpriteRenderer spriteRenderer, List<Sprite> sprites)
        {
            SetSprite(spriteRenderer, sprites == null ? null : spriteRenderer.GetComponent<SpriteMapping>().FindSprite(sprites));
        }

        private void ApplyMaterials()
        {
            var renderers = ArmorRenderers.ToList();

            renderers.Add(HairRenderer);
            renderers.Add(PrimaryWeaponRenderer);
            renderers.Add(SecondaryWeaponRenderer);
            renderers.ForEach(i => i.sharedMaterial = i.color == Color.white ? DefaultMaterial : EquipmentPaintMaterial);
        }

        public void CopyFrom(Character character)
        {
            if (character == null) throw new ArgumentNullException(nameof(character), "Can't copy from empty character!");

            Body = character.Body.ToList();
            Ears = character.Ears.ToList();
            Hair = character.Hair;
            Expression = character.Expression;
            Expressions = character.Expressions.ToList();
            Beard = character.Beard;

            Helmet = character.Helmet;
            Armor = character.Armor.ToList();
            PrimaryWeapon = character.PrimaryWeapon;
            SecondaryWeapon = character.SecondaryWeapon;
            Back = character.Back;
            Quiver = character.Quiver;
            Shield = character.Shield.ToList();
            CompositeWeapon = character.CompositeWeapon.ToList();

            Makeup = character.Makeup;
            Mask = character.Mask;
            Earrings = character.Earrings.ToList();

            foreach (var target in GetComponentsInChildren<SpriteRenderer>(true).Where(i => i.sprite != null))
            {
                foreach (var source in character.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (target.name == source.name && target.transform.parent.name == source.transform.parent.name)
                    {
                        target.color = source.color;
                        break;
                    }
                }
            }

            WeaponType = character.WeaponType;
            EquipmentTags = character.EquipmentTags.ToList();
            Initialize();
            SetExpression("Default");
        }
    }
}