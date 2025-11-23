using UnityEngine;
using UnityEditor;
using System.IO;

namespace Server.Editor
{
    /// <summary>
    /// Unity Editor'de ServerEnvironmentConfig asset'lerini hızlıca oluşturmak için yardımcı araç.
    /// Menu: Tools > Server > Create Environment Configs
    /// </summary>
    public class ServerConfigCreator : EditorWindow
    {
        private const string CONFIG_PATH = "Assets/Resources/ServerConfigs";

        [MenuItem("Tools/Server/Create Environment Configs")]
        public static void CreateDefaultConfigs()
        {
            // Resources klasörünü oluştur (yoksa)
            if (!Directory.Exists(CONFIG_PATH))
            {
                Directory.CreateDirectory(CONFIG_PATH);
                AssetDatabase.Refresh();
                Debug.Log($"[ServerConfigCreator] Created directory: {CONFIG_PATH}");
            }

            // Production Config
            CreateProductionConfig();

            // Test Config
            CreateTestConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[ServerConfigCreator] Config dosyaları oluşturuldu! Klasör: {CONFIG_PATH}");

            // Klasörü göster
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(CONFIG_PATH);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }

        private static void CreateProductionConfig()
        {
            string path = $"{CONFIG_PATH}/ProductionServerConfig.asset";

            // Zaten varsa üzerine yazma
            if (File.Exists(path))
            {
                Debug.LogWarning($"[ServerConfigCreator] {path} zaten mevcut, atlanıyor.");
                return;
            }

            ServerEnvironmentConfig config = ScriptableObject.CreateInstance<ServerEnvironmentConfig>();

            // Production ayarları
            config.environmentName = "Production";
            config.sessionNamePrefix = "PROD";
            config.photonRegion = "tr";
            config.maxPlayers = 100;
            config.isSessionVisible = true;
            config.isSessionOpen = true;
            config.description = "PRODUCTION SERVER - Oyuncuların bağlandığı CANLI sunucu.\n\n" +
                                "Bu config'i Production server build'inde kullanın.\n" +
                                "Session Name: PROD_MainGame";

            AssetDatabase.CreateAsset(config, path);
            Debug.Log($"[ServerConfigCreator] ✓ Production config oluşturuldu: {path}");
        }

        private static void CreateTestConfig()
        {
            string path = $"{CONFIG_PATH}/TestServerConfig.asset";

            // Zaten varsa üzerine yazma
            if (File.Exists(path))
            {
                Debug.LogWarning($"[ServerConfigCreator] {path} zaten mevcut, atlanıyor.");
                return;
            }

            ServerEnvironmentConfig config = ScriptableObject.CreateInstance<ServerEnvironmentConfig>();

            // Test ayarları
            config.environmentName = "Test";
            config.sessionNamePrefix = "TEST";
            config.photonRegion = "tr";
            config.maxPlayers = 50;
            config.isSessionVisible = true;
            config.isSessionOpen = true;
            config.description = "TEST SERVER - Geliştirme ve test için kullanılan sunucu.\n\n" +
                                "Bu config'i Test server build'inde kullanın.\n" +
                                "Session Name: TEST_MainGame\n" +
                                "Dikkat: Production oyuncuları bu sunucuyu görmez!";

            AssetDatabase.CreateAsset(config, path);
            Debug.Log($"[ServerConfigCreator] ✓ Test config oluşturuldu: {path}");
        }

        [MenuItem("Tools/Server/Open Configs Folder")]
        public static void OpenConfigsFolder()
        {
            if (!Directory.Exists(CONFIG_PATH))
            {
                Debug.LogWarning($"[ServerConfigCreator] Config klasörü bulunamadı: {CONFIG_PATH}");
                return;
            }

            Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>(CONFIG_PATH);
            EditorGUIUtility.PingObject(Selection.activeObject);
        }
    }
}
