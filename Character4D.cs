using System;
using System.Collections.Generic;
using System.Linq;
using Assets.HeroEditor4D.Common.Scripts.Collections;
using Assets.HeroEditor4D.Common.Scripts.Common;
using Assets.HeroEditor4D.Common.Scripts.Data;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using Assets.HeroEditor4D.InventorySystem.Scripts;
using Assets.HeroEditor4D.InventorySystem.Scripts.Data;
using Assets.HeroEditor4D.InventorySystem.Scripts.Enums;
using UnityEngine;
using Fusion;
using System.Collections;

namespace Assets.HeroEditor4D.Common.Scripts.CharacterScripts
{
	/// <summary>
	/// Controls 4 characters (for each direction).
	/// </summary>
	public class Character4D : NetworkBehaviour
    {
[Networked] public Vector2 NetworkDirection { get; set; } = new Vector2(0, -1);
[Networked] public int CurrentState { get; set; }

        [Header("Parts")]
        public Character Front;
        public Character Back;
        public Character Left;
        public Character Right;
        public List<Character> Parts;
        public List<GameObject> Shadows;

        [System.Serializable]
        public class CharacterChunkData
        {
            public int totalChunks;
            public int chunkIndex;
            public string chunkData;
            public string sessionId;
        }

private Dictionary<PlayerRef, Dictionary<string, List<string>>> receivedChunks = new Dictionary<PlayerRef, Dictionary<string, List<string>>>();
        private const int CHUNK_SIZE = 400; // bytes

        [Header("Animation")]
        public Animator Animator;
        public AnimationManager AnimationManager;

        [Header("Other")]
        public LayerManager LayerManager;
        public Color BodyColor;
        
        public SpriteCollection SpriteCollection => Parts[0].SpriteCollection;
        private List<Character> PartsExceptBack => new List<Character> { Front, Left, Right };

        public List<Sprite> Body { set { Parts.ForEach(i => i.Body = value.ToList()); } }
        public List<Sprite> Head { set { Parts.ForEach(i => i.Head = i.HairRenderer.GetComponent<SpriteMapping>().FindSprite(value)); } }
        public List<Sprite> Hair { set { Parts.ForEach(i => i.Hair = i.HairRenderer.GetComponent<SpriteMapping>().FindSprite(value)); } }
        public List<Sprite> Beard { set { Parts.ForEach(i => { if (i.BeardRenderer) i.Beard = i.BeardRenderer.GetComponent<SpriteMapping>().FindSprite(value); }); } }
        public List<Sprite> Ears { set { Parts.ForEach(i => i.Ears = value.ToList()); } }
        public List<Sprite> Eyebrows { set { PartsExceptBack.ForEach(i => i.Expressions[0].Eyebrows = i.EyebrowsRenderer.GetComponent<SpriteMapping>().FindSprite(value)); } }
        public List<Sprite> Eyes { set { PartsExceptBack.ForEach(i => i.Expressions[0].Eyes = i.EyesRenderer.GetComponent<SpriteMapping>().FindSprite(value)); } }
        public List<Sprite> Mouth { set { PartsExceptBack.ForEach(i => i.Expressions[0].Mouth = i.MouthRenderer.GetComponent<SpriteMapping>().FindSprite(value)); } }
        public List<Sprite> Helmet { set { Parts.ForEach(i => i.Helmet = i.HelmetRenderer.GetComponent<SpriteMapping>().FindSprite(value)); } }
        public List<Sprite> Armor { set { Parts.ForEach(i => i.Armor = value.ToList()); } }
        public Sprite PrimaryWeapon { set { Parts.ForEach(i => i.PrimaryWeapon = value); } }
        public Sprite SecondaryWeapon { set { Parts.ForEach(i => i.SecondaryWeapon = value); } }
        public List<Sprite> Shield { set { Parts.ForEach(i => i.Shield = value.ToList()); } }
        public List<Sprite> CompositeWeapon { set { Parts.ForEach(i => i.CompositeWeapon = value.ToList()); } }
        public List<Sprite> Makeup { set { Parts.ForEach(i => { if (i.MakeupRenderer) i.Makeup = i.MakeupRenderer.GetComponent<SpriteMapping>().FindSprite(value); }); } }
        public List<Sprite> Mask { set { Parts.ForEach(i => { if (i.MaskRenderer) i.Mask = i.MaskRenderer.GetComponent<SpriteMapping>().FindSprite(value); }); } }
        public List<Sprite> Earrings { set { Parts.ForEach(i => i.Earrings = value.ToList()); } }
        public WeaponType WeaponType { get => Front.WeaponType; set { Parts.ForEach(i => i.WeaponType = value); } }

        public void OnValidate()
        {
            Parts = new List<Character> { Front, Back, Left, Right };
            Parts.ForEach(i => i.BodyRenderers.ForEach(j => j.color = BodyColor));
            Parts.ForEach(i => i.EarsRenderers.ForEach(j => j.color = BodyColor));
        }
public void Start()
{
    // Server modunda animasyon sistemlerini başlatma
    if (IsServerMode())
    {
        return;
    }

    var stateHandler = Animator.GetBehaviours<StateHandler>().SingleOrDefault(i => i.Name == "Death");

    if (stateHandler != null)
    {
        stateHandler.StateExit.AddListener(() => SetExpression("Default"));
    }

    Animator.keepAnimatorStateOnDisable = true;

    if (AnimationManager != null)
        AnimationManager.SetState(CharacterState.Idle);
}

public void Initialize()
{
    // Server modunda görsel initialization atla
    if (IsServerMode())
    {
        return;
    }

    Parts.ForEach(i => i.Initialize());

    // Material'ları uygula - renk kayıtlarından sonra
    StartCoroutine(ApplyMaterialsDelayed());
}

// Yeni metod ekle
private IEnumerator ApplyMaterialsDelayed()
{
    yield return new WaitForEndOfFrame();
    
    // Tüm character'ların material'larını uygula
    foreach (var character in Parts)
    {
        if (character != null)
        {
            try
            {
                var renderers = character.ArmorRenderers.ToList();
                renderers.Add(character.HairRenderer);
                renderers.Add(character.PrimaryWeaponRenderer);
                renderers.Add(character.SecondaryWeaponRenderer);
                
                foreach (var renderer in renderers)
                {
                    if (renderer != null)
                    {
                        renderer.sharedMaterial = renderer.color == Color.white ? 
                            character.DefaultMaterial : character.EquipmentPaintMaterial;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Material apply warning: {e.Message}");
            }
        }
    }
}

        public void SetBody(ItemSprite item, BodyPart part)
        {
            Parts.ForEach(i => i.SetBody(item, part));
        }

        public void SetBody(ItemSprite item, BodyPart part, Color? color)
        {
            Parts.ForEach(i => i.SetBody(item, part, color));
        }

        public void SetExpression(string expression)
        {
            Parts.ForEach(i => i.SetExpression(expression));
        }

        public void Equip(ItemSprite item, EquipmentPart part)
        {
            Parts.ForEach(i => i.Equip(item, part));
            UpdateWeaponType(part);
        }

        public void Equip(ItemSprite item, EquipmentPart part, Color? color)
        {
            Parts.ForEach(i => i.Equip(item, part, color));
            UpdateWeaponType(part);
        }
        public void OptimizeForMobile()
        {
            if (Application.isMobilePlatform)
            {
                var renderers = GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var renderer in renderers)
                {
                    if (renderer.material.mainTexture != null)
                    {
                        renderer.material.mainTexture.filterMode = FilterMode.Bilinear;
                    }

                    if (renderer.gameObject.name.Contains("Shadow"))
                    {
                        renderer.enabled = QualitySettings.GetQualityLevel() > 1;
                    }
                }
            }
        }
public override void Spawned()
{
    // Server modunda görsel işlemleri atla
    if (!IsServerMode())
    {
        SetDirection(Vector2.down);

        if (AnimationManager != null)
        {
            AnimationManager.SetState(CharacterState.Idle);
        }
    }

    if (Object.HasInputAuthority)
    {
        // Local player - network direction set et
        NetworkDirection = Vector2.down;
    }
    else
    {
        // Remote player - character sync request gönder
        // Not: CharacterLoader zaten RequestCharacterSyncRPC çağıracak
        // Burada sadece fallback olarak check ediyoruz
    }

    if (receivedChunks == null)
    {
        receivedChunks = new Dictionary<PlayerRef, Dictionary<string, List<string>>>();
    }
}

        private void OnDestroy()
        {
            if (receivedChunks != null)
            {
                receivedChunks.Clear();
            }
        }

        public override void FixedUpdateNetwork()
        {
            // Mevcut kod...

            // Chunk cleanup (her 30 saniyede bir)
            if (Time.fixedTime % 30f < 0.02f)
            {
                CleanupOldChunks();
            }
            
        }
[Rpc(RpcSources.All, RpcTargets.All)]
public void SyncCharacterRPC(string json)
{
    try
    {
        FromJson(json, silent: true);
        Initialize();
        SetDirection(Vector2.down);
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Character JSON senkronizasyon hatası: {e.Message}");
    }
}
        public void SyncCharacterAppearance(NetworkObject networkObject)
        {
            if (networkObject == null || !networkObject.HasInputAuthority) return;

            string json = this.ToJson();
            if (!string.IsNullOrEmpty(json))
            {
                SyncCharacterRPC(json);
            }
        }
// DEĞİŞTİRİLECEK METOD - Character4D.cs
[Rpc(RpcSources.InputAuthority, RpcTargets.All)]
public void RPC_SendCharacterChunk(int totalChunks, int chunkIndex, string chunkData, string sessionId)
{
    // Kendi karakterini sync etme (sonsuz döngü önleme)
    if (Object.HasInputAuthority)
    {
        return;
    }

    // DÜZELTME: Fusion'da RPC sender bilgisi
    PlayerRef sender = Object.InputAuthority; // RPC.FromPlayer yerine

    // Sender için dictionary oluştur
    if (!receivedChunks.ContainsKey(sender))
    {
        receivedChunks[sender] = new Dictionary<string, List<string>>();
    }

    // Session için chunk listesi oluştur
    if (!receivedChunks[sender].ContainsKey(sessionId))
    {
        receivedChunks[sender][sessionId] = new List<string>(new string[totalChunks]);
    }

    // Chunk'ı kaydet
    receivedChunks[sender][sessionId][chunkIndex] = chunkData;

    // Tüm chunk'lar geldi mi kontrol et
    bool allChunksReceived = true;
    for (int i = 0; i < totalChunks; i++)
    {
        if (string.IsNullOrEmpty(receivedChunks[sender][sessionId][i]))
        {
            allChunksReceived = false;
            break;
        }
    }

    if (allChunksReceived)
    {
        // JSON'ı reconstruct et
        string fullJson = string.Join("", receivedChunks[sender][sessionId]);
        receivedChunks[sender].Remove(sessionId); // Cleanup

        ApplyReceivedCharacterData(fullJson);
    }
}

// DEĞİŞTİR
private void ApplyReceivedCharacterData(string characterJson)
{
    try
    {
        // ÖNEMLI: FromJson zaten Initialize() çağırıyor
        // Bu yüzden burada Initialize() gereksiz
        FromJson(characterJson, silent: true);
        
        // SetDirection'ı NetworkDirection ile çağır
        if (NetworkDirection != Vector2.zero)
        {
            SetDirection(NetworkDirection);
        }
        else
        {
            SetDirection(Vector2.down);
        }
        
        // LOG EKLE
        LogRendererStates("After ApplyReceivedCharacterData");
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[Character4D] Character sync error: {e.Message}");
    }
}

// YENİ METOD EKLE - Renderer state'lerini logla
private void LogRendererStates(string context)
{
    for (int i = 0; i < Parts.Count; i++)
    {
        var part = Parts[i];
        if (part == null) continue;
        
        var renderers = part.GetComponentsInChildren<SpriteRenderer>(true); // includeInactive = true
        int enabledCount = 0;
        int disabledCount = 0;
        
        foreach (var r in renderers)
        {
            if (r.enabled) enabledCount++;
            else disabledCount++;
        }
        
    }
}
public void SendCharacterDataInChunks()
{
    try
    {
        string characterJson = ToJson();
        if (string.IsNullOrEmpty(characterJson))
        {
            Debug.LogError("[Character4D] Character JSON is empty");
            return;
        }

        // Chunk'lara böl
        List<string> chunks = new List<string>();
        for (int i = 0; i < characterJson.Length; i += CHUNK_SIZE)
        {
            int length = Mathf.Min(CHUNK_SIZE, characterJson.Length - i);
            chunks.Add(characterJson.Substring(i, length));
        }

        string sessionId = System.Guid.NewGuid().ToString("N").Substring(0, 8);

        // TÜM client'lara broadcast et
        for (int i = 0; i < chunks.Count; i++)
        {
            RPC_SendCharacterChunk(chunks.Count, i, chunks[i], sessionId);
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[Character4D] Character chunking error: {e.Message}");
    }
}

private void CleanupOldChunks()
{
    if (Runner == null || !Runner.IsRunning)
    {
        return;
    }

    List<PlayerRef> playersToRemove = new List<PlayerRef>();
    
    foreach (var playerChunks in receivedChunks)
    {
        // DÜZELTME: Fusion'da player varlık kontrolü
        bool playerExists = false;
        
        // Tüm aktif oyuncuları kontrol et
        foreach (var activePlayer in Runner.ActivePlayers)
        {
            if (activePlayer == playerChunks.Key)
            {
                playerExists = true;
                break;
            }
        }
        
        if (!playerExists)
        {
            playersToRemove.Add(playerChunks.Key);
        }
    }
    
    foreach (var player in playersToRemove)
    {
        receivedChunks.Remove(player);
    }
}

[Rpc(RpcSources.All, RpcTargets.InputAuthority)]
public void RequestCharacterSyncRPC(PlayerRef requestingPlayer, RpcInfo info = default)
{
    // Sadece bu character'ın sahibi (InputAuthority) bu RPC'yi alır
    if (!Object.HasInputAuthority) return;
    
    // Requester'a özel olarak character data gönder
    StartCoroutine(SendCharacterDataToSpecificPlayer(requestingPlayer));
}

// YENİ METOD
private IEnumerator SendCharacterDataToSpecificPlayer(PlayerRef targetPlayer)
{
    yield return new WaitForSeconds(0.1f);
    
    string characterJson = ToJson();
    if (string.IsNullOrEmpty(characterJson))
    {
        yield break;
    }

    List<string> chunks = new List<string>();
    for (int i = 0; i < characterJson.Length; i += CHUNK_SIZE)
    {
        int length = Mathf.Min(CHUNK_SIZE, characterJson.Length - i);
        chunks.Add(characterJson.Substring(i, length));
    }

    string sessionId = System.Guid.NewGuid().ToString("N").Substring(0, 8);

    for (int i = 0; i < chunks.Count; i++)
    {
        RPC_SendCharacterChunkToPlayer(targetPlayer, chunks.Count, i, chunks[i], sessionId);
        yield return new WaitForSeconds(0.05f);
    }
}
[Rpc(RpcSources.InputAuthority, RpcTargets.All)]
public void RPC_SendCharacterChunkToPlayer(PlayerRef targetPlayer, int totalChunks, int chunkIndex, string chunkData, string sessionId)
{
    // Sadece target player işlesin
    if (Runner.LocalPlayer != targetPlayer)
    {
        return;
    }
    
    // Kendi karakterini sync etme (sonsuz döngü önleme)
    if (Object.HasInputAuthority)
    {
        return;
    }

    PlayerRef sender = Object.InputAuthority;

    // Sender için dictionary oluştur
    if (!receivedChunks.ContainsKey(sender))
    {
        receivedChunks[sender] = new Dictionary<string, List<string>>();
    }

    // Session için chunk listesi oluştur
    if (!receivedChunks[sender].ContainsKey(sessionId))
    {
        receivedChunks[sender][sessionId] = new List<string>(new string[totalChunks]);
    }

    // Chunk'ı kaydet
    receivedChunks[sender][sessionId][chunkIndex] = chunkData;

    // Tüm chunk'lar geldi mi kontrol et
    bool allChunksReceived = true;
    for (int i = 0; i < totalChunks; i++)
    {
        if (string.IsNullOrEmpty(receivedChunks[sender][sessionId][i]))
        {
            allChunksReceived = false;
            break;
        }
    }

    if (allChunksReceived)
    {
        // JSON'ı reconstruct et
        string fullJson = string.Join("", receivedChunks[sender][sessionId]);
        receivedChunks[sender].Remove(sessionId);

        ApplyReceivedCharacterData(fullJson);
    }
}


        private void UpdateWeaponType(EquipmentPart part)
        {
            switch (part)
            {
                case EquipmentPart.MeleeWeapon1H: Animator.SetInteger("WeaponType", (int) WeaponType.Melee1H); break;
                case EquipmentPart.MeleeWeapon2H: Animator.SetInteger("WeaponType", (int) WeaponType.Melee2H); break;
                case EquipmentPart.Bow: Animator.SetInteger("WeaponType", (int) WeaponType.Bow); break;
                case EquipmentPart.Crossbow: Animator.SetInteger("WeaponType", (int) WeaponType.Crossbow); break;
                case EquipmentPart.Firearm1H: Animator.SetInteger("WeaponType", (int) WeaponType.Firearm1H); break;
                case EquipmentPart.Firearm2H: Animator.SetInteger("WeaponType", (int) WeaponType.Firearm2H); break;
                case EquipmentPart.SecondaryFirearm1H: Animator.SetInteger("WeaponType", (int) WeaponType.Paired); break;
            }
        }

        public void UnEquip(EquipmentPart part)
        {
            Parts.ForEach(i => i.UnEquip(part));
        }

        public void ResetEquipment()
        {
            Parts.ForEach(i => i.ResetEquipment());
            Animator.SetInteger("WeaponType", (int)WeaponType.Melee1H);
        }

        public Vector2 Direction { get; private set; }
        public Character Active { get; private set; }

        public void SetDirection(Vector2 direction)
        {
            // Sadece geçerli direction'lar kabul et
            if (direction == Vector2.zero) return;

            // Normalize et - sadece 4 yön
            Vector2 normalizedDirection = NormalizeToFourDirections(direction);

            if (Direction == normalizedDirection) return;

            Direction = normalizedDirection;

            // Network property'yi güncelle - InputAuthority VEYA StateAuthority sahibi
            if (Object != null && Object.IsValid && (Object.HasInputAuthority || Object.HasStateAuthority))
            {
                NetworkDirection = normalizedDirection;
            }

            // Direction visual'larını set et
            SetDirectionVisualsOnly(normalizedDirection);

}

// YENİ METOD EKLE - Sadece visual değişim, animator'a dokunmadan
public void SetDirectionVisualsOnly(Vector2 direction)
{
    // Tüm parçaları pozisyonla
    Parts.ForEach(i => i.transform.localPosition = Vector3.zero);
    Shadows.ForEach(i => i.transform.localPosition = Vector3.zero);

    int index;

    if (direction == Vector2.left)
    {
        index = 2;
    }
    else if (direction == Vector2.right)
    {
        index = 3;
    }
    else if (direction == Vector2.up)
    {
        index = 1;
    }
    else if (direction == Vector2.down)
    {
        index = 0;
    }
    else
    {
        index = 0;
        Direction = Vector2.down;
    }

    // Sadece doğru yönü aktif et
    for (var i = 0; i < Parts.Count; i++)
    {
        Parts[i].SetActive(i == index);
        Shadows[i].SetActive(i == index);
    }

    Active = Parts[index];
}

// YENİ METOD EKLE:
private Vector2 NormalizeToFourDirections(Vector2 direction)
{
    if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
    {
        return direction.x > 0 ? Vector2.right : Vector2.left;
    }
    else
    {
        return direction.y > 0 ? Vector2.up : Vector2.down;
    }
}

public override void Render()
{
    // Sadece direction sync - animasyon PlayerController'da handle ediliyor
    if (Object != null && Object.IsValid && !Object.HasInputAuthority)
    {
        WanderingNPC wanderingNPC = GetComponent<WanderingNPC>();
        if (wanderingNPC == null)
        {
            if (NetworkDirection != Vector2.zero && NetworkDirection != Direction)
            {
                SetDirection(NetworkDirection);
            }
        }
    }
}

        public void CopyFrom(Character4D character)
        {
            for (var i = 0; i < Parts.Count; i++)
            {
                Parts[i].CopyFrom(character.Parts[i]);
                Parts[i].WeaponType = character.Parts[i].WeaponType;
                Parts[i].EquipmentTags = character.Parts[i].EquipmentTags.ToList();
                Parts[i].AnchorFireMuzzle.localPosition = character.Parts[i].AnchorFireMuzzle.localPosition;
            }

            Animator.SetInteger("WeaponType", (int) character.WeaponType);
        }

        public string ToJson()
        {
            return Front.ToJson();
        }

        public void FromJson(string json, bool silent)
        {
            Parts.ForEach(i => i.LoadFromJson(json, silent));
            Animator.SetInteger("WeaponType", (int) Parts[0].WeaponType);
        }

        #region Setup Examples

        public void Equip(Item item)
        {
            var itemParams = ItemCollection.Active.GetItemParams(item);

            switch (itemParams.Type)
            {
                case ItemType.Helmet: EquipHelmet(item); break;
                case ItemType.Armor: EquipArmor(item); break;
                case ItemType.Vest: EquipVest(item); break;
                case ItemType.Bracers: EquipBracers(item); break;
                case ItemType.Leggings: EquipLeggings(item); break;
                case ItemType.Shield: EquipShield(item); break;
                case ItemType.Weapon:
                {
                    switch (itemParams.Class)
                    {
                            case ItemClass.Bow: EquipBow(item); break;
                            case ItemClass.Firearm: EquipSecondaryFirearm(item); break;
                            default:
                                if (itemParams.Tags.Contains(ItemTag.TwoHanded))
                                {
                                    EquipMeleeWeapon2H(item);
                                }
                                else
                                {
                                    EquipMeleeWeapon1H(item);
                                }
                                break;
                    }
                    break;
                }
            }
        }

        public void EquipSecondaryMelee1H(Item item)
        {
            Equip(SpriteCollection.MeleeWeapon1H.SingleOrDefault(i => i.Id == item.Params.SpriteId), EquipmentPart.SecondaryMelee1H);
        }

        public void EquipArmor(Item item)
        {
            if (item == null) UnEquip(EquipmentPart.Armor);
            else Equip(SpriteCollection.Armor.Single(i => i.Id == item.Params.SpriteId), EquipmentPart.Armor);
        }

        public void EquipHelmet(Item item)
        {
            if (item == null) UnEquip(EquipmentPart.Helmet);
            else Equip(SpriteCollection.Armor.Single(i => i.Id == item.Params.SpriteId), EquipmentPart.Helmet);
        }

        public void EquipVest(Item item)
        {
            if (item == null) UnEquip(EquipmentPart.Vest);
            else Equip(SpriteCollection.Armor.Single(i => i.Id == item.Params.SpriteId), EquipmentPart.Vest);
        }

        public void EquipBracers(Item item)
        {
            if (item == null) UnEquip(EquipmentPart.Bracers);
            else Equip(SpriteCollection.Armor.Single(i => i.Id == item.Params.SpriteId), EquipmentPart.Bracers);
        }

        public void EquipLeggings(Item item)
        {
            if (item == null) UnEquip(EquipmentPart.Leggings);
            else Equip(SpriteCollection.Armor.Single(i => i.Id == item.Params.SpriteId), EquipmentPart.Leggings);
        }

        public void EquipShield(Item item)
        {
            Equip(SpriteCollection.Shield.SingleOrDefault(i => i.Id == item.Params.SpriteId), EquipmentPart.Shield);
        }
        
        public void EquipMeleeWeapon1H(Item item)
        {
            Equip(SpriteCollection.MeleeWeapon1H.SingleOrDefault(i => i.Id == item.Params.SpriteId), EquipmentPart.MeleeWeapon1H);
        }

        public void EquipMeleeWeapon2H(Item item)
        {
            Equip(SpriteCollection.MeleeWeapon2H.SingleOrDefault(i => i.Id == item.Params.SpriteId), EquipmentPart.MeleeWeapon2H);
        }

        public void EquipBow(Item item)
        {
            Equip(SpriteCollection.Bow.SingleOrDefault(i => i.Id == item.Params.SpriteId), EquipmentPart.Bow);
        }

        public void EquipCrossbow(Item item)
        {
            Equip(SpriteCollection.Crossbow.SingleOrDefault(i => i.Id == item.Params.SpriteId), EquipmentPart.Crossbow);
        }

        public void EquipSecondaryFirearm(Item item)
        {
            Equip(SpriteCollection.Firearm1H.SingleOrDefault(i => i.Id == item.Params.SpriteId), EquipmentPart.SecondaryFirearm1H);
        }

        #endregion

        /// <summary>
        /// Server modunda mı çalıştığını kontrol eder
        /// </summary>
        private bool IsServerMode()
        {
            if (Application.isEditor) return false;

            string[] args = System.Environment.GetCommandLineArgs();
            return System.Array.Exists(args, arg => arg == "-server" || arg == "-batchmode");
        }
    }
}