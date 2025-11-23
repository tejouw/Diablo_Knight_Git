using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.IO;

namespace Server.Editor
{
    /// <summary>
    /// Production ve Test build'leri için yardımcı tool.
    /// Build öncesi otomatik olarak doğru ServerEnvironmentConfig'i NetworkManager'a atar.
    /// Menu: Tools > Server > Build Production / Build Test
    /// </summary>
    public class EnvironmentBuildHelper : EditorWindow
    {
        private const string PRODUCTION_CONFIG_PATH = "Assets/Resources/ServerConfigs/ProductionServerConfig.asset";
        private const string TEST_CONFIG_PATH = "Assets/Resources/ServerConfigs/TestServerConfig.asset";

        [MenuItem("Tools/Server/Build Production APK")]
        public static void BuildProduction()
        {
            if (!SetEnvironmentConfig(PRODUCTION_CONFIG_PATH))
            {
                Debug.LogError("[EnvironmentBuildHelper] Production config bulunamadı! Önce 'Tools > Server > Create Environment Configs' ile config'leri oluşturun.");
                return;
            }

            Debug.Log("[EnvironmentBuildHelper] ===== PRODUCTION BUILD BAŞLADI =====");
            Debug.Log($"[EnvironmentBuildHelper] Config: {PRODUCTION_CONFIG_PATH}");
            Debug.Log($"[EnvironmentBuildHelper] Session: PROD_MainGame");

            // Build ayarları - isterseniz custom yapabilirsiniz
            // BuildProduction() metodunu kendi build scriptinizle değiştirebilirsiniz
            Debug.Log("[EnvironmentBuildHelper] Production build tamamlanmak üzere. Build Settings'ten manuel build yapın veya bu script'i genişletin.");

            EditorUtility.DisplayDialog(
                "Production Build Hazır",
                "NetworkManager'a ProductionServerConfig atandı.\n\n" +
                "Şimdi File > Build Settings > Build ile Production APK'yı oluşturabilirsiniz.\n\n" +
                "Session: PROD_MainGame",
                "Tamam"
            );
        }

        [MenuItem("Tools/Server/Build Test APK")]
        public static void BuildTest()
        {
            if (!SetEnvironmentConfig(TEST_CONFIG_PATH))
            {
                Debug.LogError("[EnvironmentBuildHelper] Test config bulunamadı! Önce 'Tools > Server > Create Environment Configs' ile config'leri oluşturun.");
                return;
            }

            Debug.Log("[EnvironmentBuildHelper] ===== TEST BUILD BAŞLADI =====");
            Debug.Log($"[EnvironmentBuildHelper] Config: {TEST_CONFIG_PATH}");
            Debug.Log($"[EnvironmentBuildHelper] Session: TEST_MainGame");

            // Build ayarları
            Debug.Log("[EnvironmentBuildHelper] Test build tamamlanmak üzere. Build Settings'ten manuel build yapın veya bu script'i genişletin.");

            EditorUtility.DisplayDialog(
                "Test Build Hazır",
                "NetworkManager'a TestServerConfig atandı.\n\n" +
                "Şimdi File > Build Settings > Build ile Test APK'yı oluşturabilirsiniz.\n\n" +
                "Session: TEST_MainGame",
                "Tamam"
            );
        }

        /// <summary>
        /// Active scene'deki NetworkManager'a belirtilen config'i atar
        /// </summary>
        private static bool SetEnvironmentConfig(string configPath)
        {
            // Config'i yükle
            ServerEnvironmentConfig config = AssetDatabase.LoadAssetAtPath<ServerEnvironmentConfig>(configPath);
            if (config == null)
            {
                Debug.LogError($"[EnvironmentBuildHelper] Config bulunamadı: {configPath}");
                return false;
            }

            // Active scene'i al
            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid())
            {
                Debug.LogError("[EnvironmentBuildHelper] Active scene geçersiz!");
                return false;
            }

            // NetworkManager'ı bul
            NetworkManager networkManager = GameObject.FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("[EnvironmentBuildHelper] NetworkManager bulunamadı! Scene'de NetworkManager var mı?");
                return false;
            }

            // Config'i ata (Reflection kullanarak private field'a erişim)
            SerializedObject serializedObject = new SerializedObject(networkManager);
            SerializedProperty configProperty = serializedObject.FindProperty("currentEnvironmentConfig");

            if (configProperty == null)
            {
                Debug.LogError("[EnvironmentBuildHelper] 'currentEnvironmentConfig' field bulunamadı!");
                return false;
            }

            configProperty.objectReferenceValue = config;
            serializedObject.ApplyModifiedProperties();

            // Scene'i kaydet
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);

            Debug.Log($"[EnvironmentBuildHelper] ✓ NetworkManager'a config atandı: {config.environmentName}");
            Debug.Log($"[EnvironmentBuildHelper] ✓ Session Name: {config.GetFullSessionName()}");
            Debug.Log($"[EnvironmentBuildHelper] ✓ Scene kaydedildi: {activeScene.name}");

            return true;
        }

        [MenuItem("Tools/Server/Validate Current Config")]
        public static void ValidateCurrentConfig()
        {
            NetworkManager networkManager = GameObject.FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                EditorUtility.DisplayDialog(
                    "Hata",
                    "NetworkManager bulunamadı!\n\nScene'de NetworkManager var mı kontrol edin.",
                    "Tamam"
                );
                return;
            }

            SerializedObject serializedObject = new SerializedObject(networkManager);
            SerializedProperty configProperty = serializedObject.FindProperty("currentEnvironmentConfig");

            if (configProperty == null || configProperty.objectReferenceValue == null)
            {
                EditorUtility.DisplayDialog(
                    "Config Atanmamış",
                    "NetworkManager'a henüz bir ServerEnvironmentConfig atanmamış!\n\n" +
                    "Şunlardan birini yapın:\n" +
                    "1. Tools > Server > Build Production/Test APK kullanın\n" +
                    "2. Inspector'dan manuel olarak config atayın",
                    "Tamam"
                );
                return;
            }

            ServerEnvironmentConfig config = configProperty.objectReferenceValue as ServerEnvironmentConfig;

            string message = $"Mevcut Config:\n\n" +
                            $"Environment: {config.environmentName}\n" +
                            $"Session Name: {config.GetFullSessionName()}\n" +
                            $"Region: {config.photonRegion}\n" +
                            $"Max Players: {config.maxPlayers}\n" +
                            $"Visible: {config.isSessionVisible}\n" +
                            $"Open: {config.isSessionOpen}\n\n" +
                            $"Config Path: {AssetDatabase.GetAssetPath(config)}";

            EditorUtility.DisplayDialog(
                "Current Environment Config",
                message,
                "Tamam"
            );

            Debug.Log($"[EnvironmentBuildHelper] Current Config:\n{message}");
        }
    }
}
