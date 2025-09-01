using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;


namespace ZeroUltra.ExcludeFormBuild
{
    /// <summary>
    /// 用于同步 Addressables 包的存在性到脚本定义符 ADDRESSABLES_EXISTS
    /// </summary>

    [InitializeOnLoad]
    public static class AddressablesDefineSync
    {
        private const string Define = "ADDRESSABLES_EXISTS";

        static AddressablesDefineSync()
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    SyncDefineIfNeeded();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AddressablesDefineSync] Sync failed: {e}");
                }
            };
        }

        [MenuItem("Tools/Addressables/Sync ADDRESSABLES_EXISTS Define")]
        private static void ManualSync()
        {
            SyncDefineIfNeeded(verbose: true);
        }

        private static void SyncDefineIfNeeded(bool verbose = false)
        {
            bool present = IsAddressablesPresent();

            var changedGroups = new List<BuildTargetGroup>();
            foreach (var group in GetValidBuildTargetGroups())
            {
                string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(group) ?? string.Empty;
                var set = new HashSet<string>(defines.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries));

                bool hasDefine = set.Contains(Define);
                bool needDefine = present;

                if (needDefine && !hasDefine)
                {
                    set.Add(Define);
                }
                else if (!needDefine && hasDefine)
                {
                    set.Remove(Define);
                }
                else
                {
                    continue;
                }

                string newDefines = string.Join(";", set);
                PlayerSettings.SetScriptingDefineSymbolsForGroup(group, newDefines);
                changedGroups.Add(group);
            }

            if (verbose)
            {
                if (changedGroups.Count > 0)
                {
                    Debug.Log($"[AddressablesDefineSync] {(present ? "Enabled" : "Disabled")} {Define} for: {string.Join(", ", changedGroups)}");
                }
                else
                {
                    Debug.Log($"[AddressablesDefineSync] No changes. Addressables present = {present}");
                }
            }
        }

        // 判断 Addressables 是否存在：优先通过类型反射，其次检查包目录
        private static bool IsAddressablesPresent()
        {
            // 1) 通过反射类型判断（更稳妥）
            if (Type.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor", false) != null)
                return true;
            if (Type.GetType("UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables", false) != null)
                return true;

            try
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string pkgPath = Path.Combine(projectRoot, "Packages/com.unity.addressables");
                if (Directory.Exists(pkgPath))
                    return true;
            }
            catch { /* ignore */ }

            return false;
        }

        private static IEnumerable<BuildTargetGroup> GetValidBuildTargetGroups()
        {
            foreach (BuildTargetGroup g in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                if (g == BuildTargetGroup.Unknown) continue;

                var fi = typeof(BuildTargetGroup).GetField(g.ToString());
                if (fi != null && fi.GetCustomAttributes(typeof(ObsoleteAttribute), true).Length > 0)
                    continue;

                yield return g;
            }
        }
    }
}