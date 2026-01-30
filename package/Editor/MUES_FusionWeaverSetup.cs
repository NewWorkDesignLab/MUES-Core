#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Reflection;

[InitializeOnLoad]
public static class MUES_FusionWeaverSetup
{
    private const string ASSEMBLY_NAME = "MUES-Core.Runtime";
    private const string SETUP_DONE_KEY = "MUES_FusionWeaver_SetupDone_v1";

    static MUES_FusionWeaverSetup()
    {
        if (SessionState.GetBool(SETUP_DONE_KEY, false))
            return;

        EditorApplication.delayCall += TryRegisterAssembly;
    }

    private static void TryRegisterAssembly()
    {
        SessionState.SetBool(SETUP_DONE_KEY, true);

        if (!TryGetNetworkProjectConfig(out var config))
        {
            Debug.LogWarning("[MUES] NetworkProjectConfig not found. Please manually add 'MUES-Core.Runtime' to Fusion Weaver.");
            return;
        }

        if (IsAssemblyAlreadyRegistered(config))
        {
            Debug.Log("[MUES] Assembly already registered for Fusion Weaving.");
            return;
        }

        if (RegisterAssembly(config))
        {
            Debug.Log($"[MUES] '{ASSEMBLY_NAME}' was automatically registered for Fusion Weaving. Please run 'Fusion > Run Weaver'.");
            EditorUtility.DisplayDialog(
                "MUES Setup",
                $"The assembly '{ASSEMBLY_NAME}' has been registered for Fusion IL Weaving.\n\n" +
                "Please run now:\n" +
                "Menu: Fusion > Run Weaver\n\n" +
                "Or restart Unity.",
                "OK");
        }
    }

    private static bool TryGetNetworkProjectConfig(out ScriptableObject config)
    {
        config = null;

        var configType = System.Type.GetType("Fusion.NetworkProjectConfig, Fusion.Runtime");
        if (configType == null)
        {
            configType = System.Type.GetType("Fusion.NetworkProjectConfig, Fusion.Unity");
        }

        if (configType == null)
        {
            Debug.LogWarning("[MUES] Fusion NetworkProjectConfig type not found.");
            return false;
        }

        var globalProp = configType.GetProperty("Global", BindingFlags.Public | BindingFlags.Static);
        if (globalProp != null)
        {
            config = globalProp.GetValue(null) as ScriptableObject;
            return config != null;
        }

        var guids = AssetDatabase.FindAssets("t:NetworkProjectConfig");
        if (guids.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            config = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            return config != null;
        }

        return false;
    }

    private static bool IsAssemblyAlreadyRegistered(ScriptableObject config)
    {
        var configType = config.GetType();
        
        var assemblyNamesField = configType.GetField("AssembliesToWeave", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? configType.GetField("_assembliesToWeave", BindingFlags.NonPublic | BindingFlags.Instance);

        if (assemblyNamesField != null)
        {
            var value = assemblyNamesField.GetValue(config);
            if (value is string[] names)
                return names.Contains(ASSEMBLY_NAME);
            if (value is System.Collections.IList list)
                return list.Cast<object>().Any(x => x?.ToString() == ASSEMBLY_NAME);
        }

        return false;
    }

    private static bool RegisterAssembly(ScriptableObject config)
    {
        var configType = config.GetType();
        var assemblyNamesField = configType.GetField("AssembliesToWeave", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            ?? configType.GetField("_assembliesToWeave", BindingFlags.NonPublic | BindingFlags.Instance);

        if (assemblyNamesField == null)
        {
            Debug.LogWarning("[MUES] Could not find AssembliesToWeave field. Please add manually.");
            return false;
        }

        try
        {
            var currentValue = assemblyNamesField.GetValue(config);

            if (currentValue is string[] currentArray)
            {
                var newArray = currentArray.Append(ASSEMBLY_NAME).ToArray();
                assemblyNamesField.SetValue(config, newArray);
            }
            else if (currentValue is System.Collections.IList list)
            {
                list.Add(ASSEMBLY_NAME);
            }
            else
            {
                Debug.LogWarning($"[MUES] Unknown field type: {currentValue?.GetType()}");
                return false;
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[MUES] Error registering assembly: {ex.Message}");
            return false;
        }
    }

    [MenuItem("MUES/Setup Fusion Weaving")]
    public static void ManualSetup()
    {
        SessionState.SetBool(SETUP_DONE_KEY, false);
        TryRegisterAssembly();
    }
}
#endif