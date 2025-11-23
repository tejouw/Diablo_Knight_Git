using UnityEngine;

namespace Server
{
    /// <summary>
    /// Server environment konfigürasyonu için ScriptableObject.
    /// Her environment (Production, Test, Development) için ayrı bir asset oluşturulmalı.
    /// </summary>
    [CreateAssetMenu(fileName = "ServerEnvironmentConfig", menuName = "Server/Environment Config", order = 1)]
    public class ServerEnvironmentConfig : ScriptableObject
    {
        [Header("Environment Settings")]
        [Tooltip("Environment adı (ör: Production, Test, Development)")]
        public string environmentName = "Production";

        [Header("Photon Fusion Settings")]
        [Tooltip("Session/Room adı prefix - tam session adı: prefix_MainGame şeklinde olacak")]
        public string sessionNamePrefix = "PROD";

        [Tooltip("Photon region kodu (tr, eu, us, asia, vb.)")]
        public string photonRegion = "tr";

        [Header("Game Settings")]
        [Tooltip("Maksimum oyuncu sayısı")]
        public int maxPlayers = 100;

        [Tooltip("Session görünür olsun mu? (Photon lobby listesinde)")]
        public bool isSessionVisible = true;

        [Tooltip("Session açık mı? (Yeni oyuncular katılabilir mi?)")]
        public bool isSessionOpen = true;

        [Header("Info")]
        [Tooltip("Bu config hakkında açıklama (opsiyonel)")]
        [TextArea(3, 5)]
        public string description;

        /// <summary>
        /// Tam session adını döndürür
        /// </summary>
        public string GetFullSessionName()
        {
            return $"{sessionNamePrefix}_MainGame";
        }

        /// <summary>
        /// Config'in geçerli olup olmadığını kontrol eder
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrEmpty(environmentName))
            {
                Debug.LogError($"[ServerEnvironmentConfig] Environment name boş olamaz!");
                return false;
            }

            if (string.IsNullOrEmpty(sessionNamePrefix))
            {
                Debug.LogError($"[ServerEnvironmentConfig] Session name prefix boş olamaz!");
                return false;
            }

            if (string.IsNullOrEmpty(photonRegion))
            {
                Debug.LogError($"[ServerEnvironmentConfig] Photon region boş olamaz!");
                return false;
            }

            if (maxPlayers <= 0)
            {
                Debug.LogError($"[ServerEnvironmentConfig] Max players 0'dan büyük olmalı!");
                return false;
            }

            return true;
        }

        private void OnValidate()
        {
            // Unity Editor'de değerler değiştiğinde validasyon yap
            if (!string.IsNullOrEmpty(sessionNamePrefix))
            {
                // Session prefix'i temizle (boşluk ve özel karakterler)
                sessionNamePrefix = sessionNamePrefix.Trim().Replace(" ", "_");
            }
        }
    }
}
