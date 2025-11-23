using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;

namespace Server.Editor
{
    /// <summary>
    /// Unity Editor'de Server build'leri kolayca yapmak için menü sistemi.
    /// Menu: Tools > Server > Build Server
    ///
    /// Bu script:
    /// 1. SERVER_BUILD scripting define'ını otomatik ekler
    /// 2. ServerBuildPreprocessor'ı tetikler (editor paketleri exclude eder)
    /// 3. Windows veya Linux için server build yapar
    /// 4. Build başarılı olursa AWS'ye yüklemek için hazır olduğunu bildirir
    /// </summary>
    public class ServerBuildMenu
    {
        private const string BUILD_OUTPUT_FOLDER = "Builds/Server";
        private const string SERVER_SCENE_PATH = "Assets/Scenes/MainGame.unity"; // Kendi scene path'inizi ekleyin

        [MenuItem("Tools/Server/Build Server (Windows)", priority = 100)]
        public static void BuildServerWindows()
        {
            BuildServer(BuildTarget.StandaloneWindows64, "Windows");
        }

        [MenuItem("Tools/Server/Build Server (Linux)", priority = 101)]
        public static void BuildServerLinux()
        {
            BuildServer(BuildTarget.StandaloneLinux64, "Linux");
        }

        [MenuItem("Tools/Server/Build Both (Windows + Linux)", priority = 102)]
        public static void BuildServerBoth()
        {
            Debug.Log("=== Building BOTH Windows and Linux Server Builds ===");

            bool windowsSuccess = BuildServer(BuildTarget.StandaloneWindows64, "Windows");
            if (!windowsSuccess)
            {
                Debug.LogError("Windows build failed! Aborting Linux build.");
                return;
            }

            bool linuxSuccess = BuildServer(BuildTarget.StandaloneLinux64, "Linux");
            if (!linuxSuccess)
            {
                Debug.LogError("Linux build failed!");
                return;
            }

            Debug.Log("=== ✓ BOTH SERVER BUILDS COMPLETED SUCCESSFULLY ===");
            EditorUtility.DisplayDialog("Build Complete",
                "Both Windows and Linux server builds completed!\n\n" +
                $"Output: {Path.GetFullPath(BUILD_OUTPUT_FOLDER)}",
                "OK");
        }

        private static bool BuildServer(BuildTarget target, string platformName)
        {
            Debug.Log($"=== [SERVER BUILD] Starting {platformName} Server Build ===");

            // 1. Build output path hazırla
            string buildFolder = $"{BUILD_OUTPUT_FOLDER}/{platformName}";
            if (!Directory.Exists(buildFolder))
            {
                Directory.CreateDirectory(buildFolder);
            }

            string extension = (target == BuildTarget.StandaloneWindows64) ? ".exe" : "";
            string buildPath = $"{buildFolder}/Diablo Knight{extension}";

            Debug.Log($"[SERVER BUILD] Output: {Path.GetFullPath(buildPath)}");

            // 2. Scene'leri kontrol et
            string[] scenes = GetServerScenes();
            if (scenes.Length == 0)
            {
                Debug.LogError("[SERVER BUILD] No scenes found! Please add scenes to Build Settings.");
                EditorUtility.DisplayDialog("Build Error",
                    "No scenes in Build Settings!\n\nPlease add your game scene to File > Build Settings > Scenes in Build.",
                    "OK");
                return false;
            }

            Debug.Log($"[SERVER BUILD] Building {scenes.Length} scene(s): {string.Join(", ", scenes)}");

            // 3. Build options ayarla
            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath,
                target = target,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None // Development build YAPMA (production için)
            };

            // 4. Server subtarget ayarla (Unity 6000'de var)
            #if UNITY_6000_0_OR_NEWER
            buildOptions.subtarget = (int)StandaloneBuildSubtarget.Server;
            Debug.Log("[SERVER BUILD] Using Dedicated Server subtarget");
            #endif

            // 5. Build yap
            Debug.Log("[SERVER BUILD] Starting build process...");
            Debug.Log("[SERVER BUILD] ServerBuildPreprocessor will exclude editor packages automatically.");

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            // 6. Sonucu kontrol et
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"=== [SERVER BUILD] ✓ BUILD SUCCESSFUL ===");
                Debug.Log($"[SERVER BUILD] Platform: {platformName}");
                Debug.Log($"[SERVER BUILD] Size: {FormatBytes(summary.totalSize)}");
                Debug.Log($"[SERVER BUILD] Time: {summary.totalTime.TotalSeconds:F1}s");
                Debug.Log($"[SERVER BUILD] Output: {Path.GetFullPath(buildPath)}");
                Debug.Log($"[SERVER BUILD] =============================");

                // Başarı dialogu
                bool openFolder = EditorUtility.DisplayDialog(
                    $"{platformName} Server Build Complete",
                    $"Server build başarılı!\n\n" +
                    $"Size: {FormatBytes(summary.totalSize)}\n" +
                    $"Time: {summary.totalTime.TotalSeconds:F1}s\n\n" +
                    $"AWS'ye yüklemek için hazır!\n\n" +
                    $"Path: {Path.GetFullPath(buildPath)}",
                    "Open Folder",
                    "Close");

                if (openFolder)
                {
                    EditorUtility.RevealInFinder(buildPath);
                }

                return true;
            }
            else
            {
                Debug.LogError($"=== [SERVER BUILD] ✗ BUILD FAILED ===");
                Debug.LogError($"[SERVER BUILD] Result: {summary.result}");
                Debug.LogError($"[SERVER BUILD] Errors: {summary.totalErrors}");
                Debug.LogError($"[SERVER BUILD] Warnings: {summary.totalWarnings}");

                // Hata loglarını göster
                foreach (BuildStep step in report.steps)
                {
                    foreach (BuildStepMessage message in step.messages)
                    {
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                        {
                            Debug.LogError($"[SERVER BUILD ERROR] {message.content}");
                        }
                    }
                }

                EditorUtility.DisplayDialog(
                    "Build Failed",
                    $"Server build başarısız!\n\n" +
                    $"Errors: {summary.totalErrors}\n" +
                    $"Check the Console for details.",
                    "OK");

                return false;
            }
        }

        /// <summary>
        /// Build Settings'deki aktif scene'leri al
        /// </summary>
        private static string[] GetServerScenes()
        {
            var scenes = new System.Collections.Generic.List<string>();

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled)
                {
                    scenes.Add(scene.path);
                }
            }

            return scenes.ToArray();
        }

        /// <summary>
        /// Byte'ları human-readable formata çevir
        /// </summary>
        private static string FormatBytes(ulong bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        // ============================================
        // QUICK ACCESS MENU ITEMS
        // ============================================

        [MenuItem("Tools/Server/Open Build Folder", priority = 200)]
        public static void OpenBuildFolder()
        {
            string fullPath = Path.GetFullPath(BUILD_OUTPUT_FOLDER);

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                Debug.Log($"[SERVER BUILD] Created build folder: {fullPath}");
            }

            EditorUtility.RevealInFinder(fullPath);
        }

        [MenuItem("Tools/Server/Clean Build Folder", priority = 201)]
        public static void CleanBuildFolder()
        {
            string fullPath = Path.GetFullPath(BUILD_OUTPUT_FOLDER);

            if (Directory.Exists(fullPath))
            {
                bool confirm = EditorUtility.DisplayDialog(
                    "Clean Build Folder",
                    $"Delete all files in:\n{fullPath}\n\nAre you sure?",
                    "Yes, Delete",
                    "Cancel");

                if (confirm)
                {
                    Directory.Delete(fullPath, true);
                    Directory.CreateDirectory(fullPath);
                    AssetDatabase.Refresh();
                    Debug.Log($"[SERVER BUILD] Build folder cleaned: {fullPath}");
                }
            }
            else
            {
                Debug.LogWarning($"[SERVER BUILD] Build folder doesn't exist: {fullPath}");
            }
        }

        [MenuItem("Tools/Server/Show Build Settings", priority = 202)]
        public static void ShowBuildSettings()
        {
            BuildTargetGroup group = EditorUserBuildSettings.selectedBuildTargetGroup;
            var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);

            string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group);
            var strippingLevel = PlayerSettings.GetManagedStrippingLevel(namedBuildTarget);

            Debug.Log("=== SERVER BUILD SETTINGS ===");
            Debug.Log($"Build Target: {EditorUserBuildSettings.activeBuildTarget}");
            Debug.Log($"Scripting Backend: {PlayerSettings.GetScriptingBackend(namedBuildTarget)}");
            Debug.Log($"Scripting Defines: {defines}");
            Debug.Log($"Managed Stripping: {strippingLevel}");
            Debug.Log($"Strip Engine Code: {PlayerSettings.stripEngineCode}");

            // IL2CPP Code Generation - Unity 6000'de sadece NamedBuildTarget ile çalışır
            #if UNITY_6000_0_OR_NEWER
            Debug.Log($"IL2CPP Code Generation: {PlayerSettings.GetIl2CppCodeGeneration(namedBuildTarget)}");
            #endif

            Debug.Log("============================");

            EditorUtility.DisplayDialog("Build Settings",
                $"Build Target: {EditorUserBuildSettings.activeBuildTarget}\n" +
                $"Scripting Backend: {PlayerSettings.GetScriptingBackend(namedBuildTarget)}\n" +
                $"Managed Stripping: {strippingLevel}\n" +
                $"Strip Engine Code: {PlayerSettings.stripEngineCode}\n\n" +
                $"Check Console for full details.",
                "OK");
        }
    }
}
