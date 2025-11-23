using UnityEngine;
using Fusion;

public class TeleportPortal : NetworkBehaviour
{
    [Networked] private TickTimer Lifetime { get; set; }
    [Networked] private Vector3 SpawnPosition { get; set; } // EKLE
    
    public void Initialize(float lifetimeSeconds)
    {
        Lifetime = TickTimer.CreateFromSeconds(Runner, lifetimeSeconds);
        
        // EKLE: Spawn pozisyonunu networked property'e kaydet
        if (Object.HasStateAuthority)
        {
            SpawnPosition = transform.position;
        }
    }
    
    // EKLE: Spawned callback
    public override void Spawned()
    {
        // Server pozisyonu set etti mi kontrol et
        if (Object.HasStateAuthority)
        {
            SpawnPosition = transform.position;
        }
        else
        {
            // Client: Networked position'dan al
            transform.position = SpawnPosition;
        }
    }
    
    // EKLE: Render callback - pozisyon senkronizasyonu için
    public override void Render()
    {
        // Client'larda pozisyonu sürekli sync et
        if (!Object.HasStateAuthority && SpawnPosition != Vector3.zero)
        {
            if (Vector3.Distance(transform.position, SpawnPosition) > 0.01f)
            {
                transform.position = SpawnPosition;
            }
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        
        if (Lifetime.Expired(Runner))
        {
            Runner.Despawn(Object);
        }
    }
}