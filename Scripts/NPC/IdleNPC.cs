using UnityEngine;
using Fusion;
using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class IdleNPC : NetworkBehaviour
{
[Header("Character Settings")]
[SerializeField] private Character4D character4D;
[SerializeField] private Vector2 facingDirection = Vector2.down;

// Chunk sistemi için gerekli alanlar
private Dictionary<PlayerRef, Dictionary<string, List<string>>> receivedChunks = new Dictionary<PlayerRef, Dictionary<string, List<string>>>();
private const int CHUNK_SIZE = 400;
    
    private void Awake()
    {
        if (character4D == null)
        {
            character4D = GetComponent<Character4D>();
        }
    }

    private void Start()
    {
        // Network bağlantısını bekle ve başlat
        StartCoroutine(WaitForNetworkAndInitialize());
    }
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void RPC_SendIdleNPCChunk(int totalChunks, int chunkIndex, string chunkData, string sessionId)
{
    PlayerRef sender = Object.StateAuthority;

    if (!receivedChunks.ContainsKey(sender))
    {
        receivedChunks[sender] = new Dictionary<string, List<string>>();
    }

    if (!receivedChunks[sender].ContainsKey(sessionId))
    {
        receivedChunks[sender][sessionId] = new List<string>(new string[totalChunks]);
    }

    receivedChunks[sender][sessionId][chunkIndex] = chunkData;

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
        string fullJson = string.Join("", receivedChunks[sender][sessionId]);
        receivedChunks[sender].Remove(sessionId);
        ApplyReceivedCharacterData(fullJson);
    }
}

private void ApplyReceivedCharacterData(string characterJson)
{
    if (character4D == null) return;

    // Server modunda görsel işlemleri atla
    if (IsServerMode())
    {
        return;
    }

    try
    {
        character4D.FromJson(characterJson, silent: true);
        character4D.Initialize();
        character4D.SetDirection(facingDirection);
        if (character4D.AnimationManager != null)
            character4D.AnimationManager.SetState(CharacterState.Idle);
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[IdleNPC] Karakter senkronizasyon hatası: {e.Message}");
    }
}
private IEnumerator WaitForNetworkAndInitialize()
{
    // Network bağlantısını bekle
    while (Runner == null || !Runner.IsRunning)
    {
        yield return new WaitForSeconds(0.5f);
    }

    // Karakteri başlat
    InitializeCharacter();
    
    // Karakter görünümünü senkronize et
    if (Object != null && Object.HasStateAuthority)
    {
        Invoke("SyncCharacterAppearance", 1f);
    }
}

private void InitializeCharacter()
{
    if (character4D == null) return;

    // Server modunda görsel işlemleri atla
    if (IsServerMode())
    {
        return;
    }

    // Karakteri initialize et
    character4D.Initialize();

    // Yönü ayarla - network kontrolü yapma
    character4D.SetDirection(facingDirection);

    // Idle animasyonunu başlat
    if (character4D.AnimationManager != null)
        character4D.AnimationManager.SetState(CharacterState.Idle);
}

private void SyncCharacterAppearance()
{
    if (!Object.HasStateAuthority || character4D == null) return;
    
    try
    {
        string characterJson = character4D.ToJson();
        if (string.IsNullOrEmpty(characterJson)) return;

        List<string> chunks = new List<string>();
        for (int i = 0; i < characterJson.Length; i += CHUNK_SIZE)
        {
            int length = Mathf.Min(CHUNK_SIZE, characterJson.Length - i);
            chunks.Add(characterJson.Substring(i, length));
        }

        string sessionId = System.Guid.NewGuid().ToString("N").Substring(0, 8);

        for (int i = 0; i < chunks.Count; i++)
        {
            RPC_SendIdleNPCChunk(chunks.Count, i, chunks[i], sessionId);
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[IdleNPC] Character chunking error: {e.Message}");
    }
}

// DEĞİŞEN METODLAR:
[Rpc(RpcSources.StateAuthority, RpcTargets.All)]
private void SyncCharacterAppearanceRPC(string characterJson)
{
    if (character4D == null) return;
    
    try
    {
        character4D.FromJson(characterJson, silent: true);
        character4D.Initialize();
        character4D.SetDirection(facingDirection);
        character4D.AnimationManager.SetState(CharacterState.Idle);
    }
    catch (System.Exception e)
    {
        Debug.LogError($"[IdleNPC] Karakter senkronizasyon hatası: {e.Message}");
    }
}
    // Inspector'dan yön değiştirilebilsin
    [ContextMenu("Set Facing Direction - Down")]
    private void SetFacingDown() { facingDirection = Vector2.down; UpdateDirection(); }
    
    [ContextMenu("Set Facing Direction - Up")]
    private void SetFacingUp() { facingDirection = Vector2.up; UpdateDirection(); }
    
    [ContextMenu("Set Facing Direction - Left")]
    private void SetFacingLeft() { facingDirection = Vector2.left; UpdateDirection(); }
    
    [ContextMenu("Set Facing Direction - Right")]
    private void SetFacingRight() { facingDirection = Vector2.right; UpdateDirection(); }

    private void UpdateDirection()
    {
        if (character4D != null && !IsServerMode())
        {
            character4D.SetDirection(facingDirection);
        }
    }

    private bool IsServerMode()
    {
        if (Application.isEditor) return false;

        string[] args = System.Environment.GetCommandLineArgs();
        return System.Array.Exists(args, arg => arg == "-server" || arg == "-batchmode");
    }
}