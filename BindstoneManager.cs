using UnityEngine;
using Fusion;

public class BindstoneManager : NetworkBehaviour
{
    private static BindstoneManager _instance;
    public static BindstoneManager Instance => _instance;
    
    // Networked dictionary - max 32 player için yeterli
    [Networked, Capacity(32)]
    private NetworkDictionary<PlayerRef, Vector2> PlayerBindstones => default;
    [Networked, Capacity(32)]
private NetworkDictionary<PlayerRef, NetworkId> PlayerSelectedBindstones => default;
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }
    
public void RegisterPlayerBindstone(PlayerRef player, Vector2 bindstonePosition, NetworkId bindstoneId)
{
    if (!Object.HasStateAuthority)
    {
        return;
    }
    
    PlayerBindstones.Set(player, bindstonePosition);
    PlayerSelectedBindstones.Set(player, bindstoneId);
}
    public bool TryGetPlayerSelectedBindstone(PlayerRef player, out NetworkId bindstoneId)
{
    return PlayerSelectedBindstones.TryGet(player, out bindstoneId);
}
    public bool TryGetPlayerBindstone(PlayerRef player, out Vector2 bindstonePosition)
    {
        bool found = PlayerBindstones.TryGet(player, out bindstonePosition);
        
        return found;
    }
    
 public void ClearPlayerBindstone(PlayerRef player)
{
    if (!Object.HasStateAuthority) return;
    
    if (PlayerBindstones.Remove(player))
    {
        PlayerSelectedBindstones.Remove(player);
    }
}
}