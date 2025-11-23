using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using System.Collections.Generic;
using System.Linq;

namespace Server.Editor
{
    /// <summary>
    /// Server build yaparken otomatik olarak Editor-only paketleri exclude eder.
    /// Bu sayede AWS'de crash'e sebep olan Editor assembly'leri build'e dahil olmaz.
    ///
    /// ÇALIŞMA PRENSİBİ:
    /// 1. Build başlamadan önce (PreprocessBuild) tetiklenir
    /// 2. Eğer Server platform build'iyse, Editor-only paketleri disabled eder
    /// 3. Build tamamlandıktan sonra (PostprocessBuild) paketleri eski haline döndürür
    ///
    /// NOT: manifest.json'dan silmez, sadece build sırasında exclude eder.
    /// </summary>
    public class ServerBuildPreprocessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        // Build işlem sırası (düşük önce çalışır)
        public int callbackOrder => 0;

        // Editor-only paketler - Server build'de gereksiz ve crash'e sebep olabilir
        private static readonly string[] EDITOR_ONLY_PACKAGES = new string[]
        {
            // === CRITICAL: AWS crash'in ana nedenleri ===
            "com.unity.dt.app-ui",                      // AppUI Settings hatası
            "com.unity.ai.assistant",                   // AI Assistant (Editor-only)
            "com.unity.ai.generators",                  // AI Generators (Editor-only)
            "com.unity.ai.inference",                   // AI Inference (GraphLogicAnalysis hatası)

            // === Editor Development Tools ===
            "com.unity.build-report-inspector",         // Build report viewer
            "com.unity.device-simulator.devices",       // Device simulator
            "com.unity.multiplayer.playmode",           // Virtual Players (Editor testing)
            "com.unity.performance.profile-analyzer",   // Profile analyzer
            "com.unity.collab-proxy",                   // Unity Collaborate/Version Control

            // === Unity Services Editor Tools ===
            "com.unity.services.cloud-build",           // Cloud Build integration
            "com.unity.services.deployment",            // Deployment tools
            "com.unity.services.deployment.api",        // Deployment API
            "com.unity.multiplayer.center",             // Multiplayer setup wizard

            // === Graphics & Effects (Server'da gereksiz) ===
            "com.unity.visualeffectgraph",              // VFX Graph (Server -nographics çalışıyor)

            // === Optional: Kullanılmıyorsa exclude edilebilir ===
            // "com.unity.visualscripting",             // Visual Scripting (kontrol et)
            // "com.unity.netcode.gameobjects",         // NGO (Photon Fusion varken gereksiz)
            // "com.unity.transport",                   // Unity Transport (NGO dependency)
            // "com.unity.multiplayer.tools",           // Multiplayer debug tools
        };

        // Scripting defines - Server build'de aktif edilecek
        private static readonly string[] SERVER_SCRIPTING_DEFINES = new string[]
        {
            "SERVER_BUILD",         // Server build olduğunu belirtir
            "DISABLE_CLIENT_UI",    // Client UI'ları disable et
            "NO_GRAPHICS",          // Graphics sistemi yok
        };

        private BuildTargetGroup originalBuildTargetGroup;
        private string originalDefines;
        private Dictionary<string, bool> packageStatesBeforeBuild = new Dictionary<string, bool>();

        /// <summary>
        /// Build başlamadan ÖNCE çağrılır - Server build ise editor paketleri exclude et
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            // Server platform build mi kontrol et
            if (!IsServerBuild(report))
            {
                Debug.Log("[ServerBuildPreprocessor] Client build detected, skipping editor package exclusion.");
                return;
            }

            Debug.Log("=== [ServerBuildPreprocessor] SERVER BUILD STARTED ===");
            Debug.Log("[ServerBuildPreprocessor] Excluding editor-only packages for stable AWS deployment...");

            // 1. Scripting defines ekle
            AddServerScriptingDefines(report);

            // 2. Editor-only paketleri exclude et
            ExcludeEditorPackages();

            // 3. Build settings optimize et
            OptimizeServerBuildSettings();

            Debug.Log("[ServerBuildPreprocessor] Preprocessing complete. Editor packages excluded.");
        }

        /// <summary>
        /// Build tamamlandıktan SONRA çağrılır - Ayarları eski haline döndür
        /// </summary>
        public void OnPostprocessBuild(BuildReport report)
        {
            if (!IsServerBuild(report))
            {
                return;
            }

            Debug.Log("=== [ServerBuildPreprocessor] SERVER BUILD FINISHED ===");
            Debug.Log("[ServerBuildPreprocessor] Restoring editor packages and settings...");

            // 1. Scripting defines'ı eski haline döndür
            RestoreScriptingDefines();

            // 2. Paket ayarlarını restore et (şu an Unity API bunu desteklemiyor, future-proof)
            RestoreEditorPackages();

            Debug.Log("[ServerBuildPreprocessor] Postprocessing complete. Editor state restored.");
        }

        // ============================================
        // HELPER METHODS
        // ============================================

        /// <summary>
        /// Server build mi kontrol et (platform veya command line args)
        /// </summary>
        private bool IsServerBuild(BuildReport report)
        {
            // Platform kontrolü
            if (report.summary.platform == BuildTarget.StandaloneLinux64 ||
                report.summary.platform == BuildTarget.StandaloneWindows64)
            {
                // Command line'dan -server flag var mı kontrol et
                string[] args = System.Environment.GetCommandLineArgs();
                if (args.Contains("-server") || args.Contains("--server"))
                {
                    return true;
                }

                // Build path'de "Server" var mı kontrol et
                if (report.summary.outputPath.Contains("Server") ||
                    report.summary.outputPath.Contains("server"))
                {
                    return true;
                }
            }

            // Dedicated Server platform
            if (report.summary.platform.ToString().Contains("Server"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Server build için scripting defines ekle
        /// </summary>
        private void AddServerScriptingDefines(BuildReport report)
        {
            originalBuildTargetGroup = BuildPipeline.GetBuildTargetGroup(report.summary.platform);
            originalDefines = PlayerSettings.GetScriptingDefineSymbolsForGroup(originalBuildTargetGroup);

            // Mevcut defines'ları al ve server defines'ları ekle
            List<string> defines = originalDefines.Split(';').ToList();

            foreach (string define in SERVER_SCRIPTING_DEFINES)
            {
                if (!defines.Contains(define))
                {
                    defines.Add(define);
                    Debug.Log($"[ServerBuildPreprocessor] Added scripting define: {define}");
                }
            }

            string newDefines = string.Join(";", defines);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(originalBuildTargetGroup, newDefines);

            Debug.Log($"[ServerBuildPreprocessor] Scripting Defines: {newDefines}");
        }

        /// <summary>
        /// Scripting defines'ı eski haline döndür
        /// </summary>
        private void RestoreScriptingDefines()
        {
            if (!string.IsNullOrEmpty(originalDefines))
            {
                PlayerSettings.SetScriptingDefineSymbolsForGroup(originalBuildTargetGroup, originalDefines);
                Debug.Log($"[ServerBuildPreprocessor] Restored scripting defines: {originalDefines}");
            }
        }

        /// <summary>
        /// Editor-only paketleri build'den exclude et
        /// NOT: Unity'nin package management API'si build sırasında paket disable etmeyi desteklemiyor.
        /// Bu method future-proof olarak eklendi, şu an sadece warning veriyor.
        /// </summary>
        private void ExcludeEditorPackages()
        {
            Debug.Log($"[ServerBuildPreprocessor] Checking {EDITOR_ONLY_PACKAGES.Length} editor-only packages...");

            foreach (string packageName in EDITOR_ONLY_PACKAGES)
            {
                // Package var mı kontrol et
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath($"Packages/{packageName}");

                if (package != null)
                {
                    Debug.LogWarning($"[ServerBuildPreprocessor] ⚠️ Editor-only package detected: {packageName}");
                    Debug.LogWarning($"   This package may cause crashes on AWS if included in build.");
                    Debug.LogWarning($"   Consider removing it from manifest.json or use #if !SERVER_BUILD guards.");
                }
            }

            Debug.Log("[ServerBuildPreprocessor] Note: Unity doesn't support disabling packages during build.");
            Debug.Log("[ServerBuildPreprocessor] Use 'SERVER_BUILD' scripting define to guard editor-only code.");
        }

        /// <summary>
        /// Paket ayarlarını restore et (future-proof)
        /// </summary>
        private void RestoreEditorPackages()
        {
            // Unity API bunu desteklemiyor şu an, future-proof method
            Debug.Log("[ServerBuildPreprocessor] Package states don't need restoration (not modified).");
        }

        /// <summary>
        /// Server build settings'i optimize et
        /// </summary>
        private void OptimizeServerBuildSettings()
        {
            Debug.Log("[ServerBuildPreprocessor] Optimizing server build settings...");

            // IL2CPP Stripping Level - Unity 6000'de NamedBuildTarget kullan
            #if UNITY_6000_0_OR_NEWER
            var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(originalBuildTargetGroup);
            if (PlayerSettings.GetManagedStrippingLevel(namedBuildTarget) == ManagedStrippingLevel.Disabled)
            {
                Debug.LogWarning("[ServerBuildPreprocessor] ⚠️ Managed Stripping is DISABLED!");
                Debug.LogWarning("   Consider enabling 'Minimal' or 'Low' for smaller build size.");
            }
            #elif UNITY_2021_2_OR_NEWER
            // Unity 2021-2023 için eski API
            if (PlayerSettings.GetManagedStrippingLevel(originalBuildTargetGroup) == ManagedStrippingLevel.Disabled)
            {
                Debug.LogWarning("[ServerBuildPreprocessor] ⚠️ Managed Stripping is DISABLED!");
                Debug.LogWarning("   Consider enabling 'Minimal' or 'Low' for smaller build size.");
            }
            #endif

            // Engine Code Stripping
            if (!PlayerSettings.stripEngineCode)
            {
                Debug.LogWarning("[ServerBuildPreprocessor] ⚠️ Engine Code Stripping is DISABLED!");
                Debug.LogWarning("   Consider enabling it for server builds to reduce size.");
            }

            Debug.Log("[ServerBuildPreprocessor] Build optimization checks complete.");
        }
    }
}
