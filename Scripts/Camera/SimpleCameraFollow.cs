// Path: Assets/Game/Scripts/SimpleCameraFollow.cs

using UnityEngine;
using Fusion; // Photon.Pun yerine

public class SimpleCameraFollow : MonoBehaviour
{
    private Transform target;
    public float smoothSpeed = 5f;
    public Vector3 offset = new Vector3(0, 0, -10); // z ekseni için kamera offset'i

    private void Start()
    {
        // Kamerayı Main Camera yap
        Camera.main.orthographic = true;
        Camera.main.orthographicSize = 10f; // Görüş alanı - ihtiyaca göre ayarlanabilir
    }

private void LateUpdate()
{
    if (target == null)
    {
        // Sahnedeki tüm oyuncuları kontrol et
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject player in players)
        {
            NetworkObject networkObject = player.GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.HasInputAuthority)
            {
                target = player.transform;
                break;
            }
        }
        return;
    }

    // Hedef pozisyonu hesapla
    Vector3 desiredPosition = target.position + offset;
    
    // Yumuşak geçişli hareket
    Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    transform.position = smoothedPosition;
}
}