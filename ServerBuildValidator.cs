// Path: Assets/Game/Scripts/ServerBuildValidator.cs
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class ServerBuildValidator : MonoBehaviour
{
    // Editor-only packages that should NOT be in server builds
    private static readonly string[] PROBLEMATIC_ASSEMBLIES = new string[]
    {
        "Unity.AI.Inference",
        "Unity.AI.Assistant",
        "Unity.AI.Generators",
        "Unity.AppUI",
        "UnityEditor",
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ValidateServerBuild()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        bool hasServerArgs = args.Any(arg =>
            arg == "-server" ||
            arg == "-batchmode" ||
            arg == "-nographics" ||
            arg.StartsWith("-room"));

        if (hasServerArgs)
        {
            Debug.Log($"[SERVER VALIDATOR] ===== SERVER MODE DETECTED =====");
            Debug.Log($"[SERVER VALIDATOR] Command line args: {string.Join(", ", args)}");

            // Check for scripting defines
            #if SERVER_BUILD
            Debug.Log("[SERVER VALIDATOR] ✓ SERVER_BUILD define is active");
            #else
            Debug.LogWarning("[SERVER VALIDATOR] ⚠️ SERVER_BUILD define is missing!");
            Debug.LogWarning("[SERVER VALIDATOR] This may cause issues with editor-only code.");
            #endif

            // Validate assemblies
            ValidateAssemblies();

            // Force immediate optimizations
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            QualitySettings.SetQualityLevel(0, false);

            Debug.Log("[SERVER VALIDATOR] ✓ Server optimizations applied");
            Debug.Log("[SERVER VALIDATOR] ✓ VSync disabled, Quality set to lowest");
            Debug.Log("[SERVER VALIDATOR] ✓ Run in background enabled");
            Debug.Log("[SERVER VALIDATOR] =====================================");
        }
    }

    private static void ValidateAssemblies()
    {
        Debug.Log("[SERVER VALIDATOR] Validating loaded assemblies...");

        var loadedAssemblies = System.AppDomain.CurrentDomain.GetAssemblies();
        var problematicFound = new List<string>();

        foreach (var assembly in loadedAssemblies)
        {
            string assemblyName = assembly.GetName().Name;

            foreach (string problematic in PROBLEMATIC_ASSEMBLIES)
            {
                if (assemblyName.Contains(problematic))
                {
                    problematicFound.Add(assemblyName);
                    break;
                }
            }
        }

        if (problematicFound.Count > 0)
        {
            Debug.LogError("[SERVER VALIDATOR] ❌ PROBLEMATIC ASSEMBLIES DETECTED!");
            Debug.LogError("[SERVER VALIDATOR] These editor-only assemblies may cause AWS crashes:");
            foreach (string assembly in problematicFound)
            {
                Debug.LogError($"[SERVER VALIDATOR]    - {assembly}");
            }
            Debug.LogError("[SERVER VALIDATOR] Solution: Rebuild with ServerBuildPreprocessor enabled");
            Debug.LogError("[SERVER VALIDATOR] Or use 'Tools > Server > Build Server' menu");
        }
        else
        {
            Debug.Log("[SERVER VALIDATOR] ✓ No problematic assemblies detected");
        }
    }
}