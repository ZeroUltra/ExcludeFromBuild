using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

#if ADDRESSABLES_EXISTS
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
#endif

namespace ZeroUltra.ExcludeFormBuild
{
    public class ExcludeFromBuildAddressablesPrebuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string logCyan = "<color=Cyan>{0}</color>";
        private const string logYellow = "<color=#ffa500>{0}</color>";
        private const string persistFile = "ProjectSettings/ExcludeFormBuilder_AddressablesRestore.json";
        private const string tempGroupName = "ExcludedFromPlayer_Temp";

        [Serializable]
        private class RestoreEntry
        {
            public string entryGuid;
            public string originalGroupGuid;
            public string originalGroupName;
            public string address;
            public List<string> labels;
        }

        [Serializable]
        private class RestorePayload
        {
            public List<RestoreEntry> entries = new List<RestoreEntry>();
        }

        // 尽量早于 Addressables
        int IOrderedCallback.callbackOrder => -10000;

        void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
        {
#if ADDRESSABLES_EXISTS
            TryPrepareAddressables();
#endif
            BuildHandler.OnBuildFaild -= OnBuildFailed;
            BuildHandler.OnBuildFaild += OnBuildFailed;
        }

        void IPostprocessBuildWithReport.OnPostprocessBuild(BuildReport report)
        {
#if ADDRESSABLES_EXISTS
            TryRestoreAddressables();
#endif
            BuildHandler.OnBuildFaild -= OnBuildFailed;
        }

        private void OnBuildFailed()
        {
#if ADDRESSABLES_EXISTS
            TryRestoreAddressables();
#endif
            BuildHandler.OnBuildFaild -= OnBuildFailed;
        }

#if ADDRESSABLES_EXISTS
        /// <summary>
        /// 针对被标记为排除的资源，尝试将其移入一个临时的非打包组中
        /// </summary>
        private void TryPrepareAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null) return;

            var guids = AssetDatabase.FindAssets("l:" + ExcludeFormBuilder.assetExcludeLable);
            if (guids == null || guids.Length == 0) return;

            var payload = new RestorePayload();

            var tempGroup = settings.groups.FirstOrDefault(g => g != null && g.Name == tempGroupName);
            if (tempGroup == null)
            {
                tempGroup = settings.CreateGroup(tempGroupName, false, false, false, null,
                    typeof(BundledAssetGroupSchema));
            }
            var schema = tempGroup.GetSchema<BundledAssetGroupSchema>() ?? tempGroup.AddSchema<BundledAssetGroupSchema>();
            schema.IncludeInBuild = false;

            bool anyMoved = false;

            foreach (var guid in guids)
            {
                var entry = settings.FindAssetEntry(guid);
                if (entry == null) continue;

                if (entry.parentGroup != null && entry.parentGroup != tempGroup)
                {
                    payload.entries.Add(new RestoreEntry
                    {
                        entryGuid = guid,
                        originalGroupGuid = entry.parentGroup.Guid,
                        originalGroupName = entry.parentGroup.Name,
                        address = entry.address,
                        labels = entry.labels != null ? entry.labels.ToList() : new List<string>()
                    });

                    settings.MoveEntry(entry, tempGroup);
                    anyMoved = true;

                    Debug.LogFormat(logYellow, $"Addressables: Move to temp non-build group: [{entry.address}]");
                }
            }

            if (anyMoved)
            {
                File.WriteAllText(persistFile, JsonUtility.ToJson(payload, true));
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
        }

        /// <summary>
        /// 恢复Addressables的原始状态
        /// </summary>
        private void TryRestoreAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null) return;
            if (!File.Exists(persistFile)) return;

            var json = File.ReadAllText(persistFile);
            var payload = new RestorePayload();
            JsonUtility.FromJsonOverwrite(json, payload);

            var tempGroup = settings.groups.FirstOrDefault(g => g != null && g.Name == tempGroupName);

            foreach (var item in payload.entries)
            {
                // 找回原组（不存在则创建）
                var originalGroup =
                    settings.groups.FirstOrDefault(g => g != null && g.Guid == item.originalGroupGuid)
                    ?? settings.groups.FirstOrDefault(g => g != null && g.Name == item.originalGroupName)
                    ?? settings.CreateGroup(item.originalGroupName, false, false, false, null, typeof(BundledAssetGroupSchema));

                // 找回条目；若被删除，则重建
                var entry = settings.FindAssetEntry(item.entryGuid)
                           ?? settings.CreateOrMoveEntry(item.entryGuid, originalGroup, false, false);

                if (entry.parentGroup != originalGroup)
                {
                    settings.MoveEntry(entry, originalGroup);
                }

                // 还原 address 与 labels
                if (!string.IsNullOrEmpty(item.address) && entry.address != item.address)
                {
                    entry.SetAddress(item.address);
                }
                if (item.labels != null && item.labels.Count > 0)
                {
                    var current = entry.labels != null ? entry.labels.ToList() : new List<string>();
                    foreach (var l in current)
                        entry.SetLabel(l, false);
                    foreach (var l in item.labels)
                        entry.SetLabel(l, true);
                }

                Debug.LogFormat(logCyan, $"Addressables: Restore entry [{entry.address}] -> Group [{originalGroup.Name}]");
            }

            // 清理临时组（若已空）
            if (tempGroup != null && tempGroup.entries.Count == 0)
            {
                settings.RemoveGroup(tempGroup);
            }

            File.Delete(persistFile);

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
#endif
    }
}