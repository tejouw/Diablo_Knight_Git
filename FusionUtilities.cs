using Fusion;
using UnityEngine;

public static class FusionUtilities
{
    /// <summary>
    /// Local player'ın bu NetworkObject'e input authority'si var mı?
    /// </summary>
    public static bool IsLocalPlayerInputAuthority(this NetworkObject networkObject)
    {
        if (networkObject == null || networkObject.Runner == null) return false;
        return networkObject.HasInputAuthority;
    }
    
    /// <summary>
    /// Bu PlayerRef local player mı?
    /// </summary>
    public static bool IsLocalPlayer(this NetworkRunner runner, PlayerRef playerRef)
    {
        if (runner == null) return false;
        return runner.LocalPlayer == playerRef;
    }
    
    /// <summary>
    /// Local player'ın PlayerRef'ini döndürür
    /// </summary>
    public static PlayerRef GetLocalPlayerRef(this NetworkRunner runner)
    {
        return runner?.LocalPlayer ?? PlayerRef.None;
    }
    
    /// <summary>
    /// Bu NetworkObject'in sahibi local player mı?
    /// </summary>
    public static bool IsOwnedByLocalPlayer(this NetworkObject networkObject)
    {
        if (networkObject == null || networkObject.Runner == null) return false;
        return networkObject.InputAuthority == networkObject.Runner.LocalPlayer;
    }
    
    /// <summary>
    /// Bu obje belirtilen PlayerRef'e ait mi?
    /// </summary>
    public static bool IsOwnedBy(this NetworkObject networkObject, PlayerRef playerRef)
    {
        if (networkObject == null) return false;
        return networkObject.InputAuthority == playerRef;
    }
    
    /// <summary>
    /// Local player GameObject'ini bulur
    /// </summary>
    public static GameObject FindLocalPlayerGameObject()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.HasInputAuthority)
            {
                return player;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Belirtilen PlayerRef'e ait GameObject'i bulur
    /// </summary>
    public static GameObject FindPlayerGameObject(PlayerRef playerRef)
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        
        foreach (GameObject player in players)
        {
            NetworkObject netObj = player.GetComponent<NetworkObject>();
            if (netObj != null && netObj.InputAuthority == playerRef)
            {
                return player;
            }
        }
        
        return null;
    }
}