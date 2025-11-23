
using Fusion;
using UnityEngine;
using System.Collections;

public class PlayerSetup : NetworkBehaviour
{
[Rpc(RpcSources.All, RpcTargets.All)]
public void SetBotFlagRPC(bool isBot)
{
    
    if (isBot)
    {
        StartCoroutine(SetupBotDelayed());
    }
}

private IEnumerator SetupBotDelayed()
{
    yield return new WaitForEndOfFrame();
    
    // PlayerController'ı DESTROY ETME - sadece bot flag'i set et
    PlayerController playerController = GetComponent<PlayerController>();
    if (playerController != null)
    {
        // PlayerController'da isBot flag'i olması lazım - yoksa ekleyelim
    }
    
    // NetworkCharacterController'ı aktif et
    NetworkCharacterController networkController = GetComponent<NetworkCharacterController>();
    if (networkController != null)
    {
        networkController.enabled = true;
        networkController.SetMovementEnabled(true);
        networkController.isBot = true; // Bot flag'i set et
    }
    
    // Bot Controller'ı aktifleştir
    BotController botController = GetComponent<BotController>();
    if (botController != null)
    {
        botController.enabled = true;
    }
    else
    {
        Debug.LogError("[PlayerSetup] BotController prefab'da bulunamadı!");
    }
    
}
}
