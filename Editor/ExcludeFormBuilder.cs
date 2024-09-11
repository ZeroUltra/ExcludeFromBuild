using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using static UnityEditor.Progress;
using static ZeroUltra.ExcludeFormBuild.ExcludeAssetsPersistentPath;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Path = System.IO.Path;

namespace ZeroUltra.ExcludeFormBuild
{
    public class ExcludeFormBuilder : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        /******************************************************
         * https://docs.unity3d.com/ScriptReference/Build.IPreprocessBuildWithReport.OnPreprocessBuild.html
         * https://docs.unity3d.com/ScriptReference/Build.IPostprocessBuildWithReport.OnPostprocessBuild.html
         * This callback is invoked during Player builds, but not during AssetBundle builds.
         * If the build stops early, due to a failure or cancellation, then the callback is not invoked.
         * 只会响应打包app
        ******************************************************/
        public const string assetExcludeLable = "Excludefrombuild";
        private const string menu = "Assets/Exclude From Build";
        private const string logCyan = "<color=Cyan>{0}</color>";
        private const string logYellow = "<color=#ffa500>{0}</color>";

        int IOrderedCallback.callbackOrder => 0;
        private static string backupFolder => Application.dataPath.Replace("Assets", string.Empty) + "ExcludeFromBuildTemp/";



        [MenuItem(menu, priority = 2000, validate = true)]
        private static bool ExcludeFrommBuildValidate()
        {
            var objs = Selection.objects;
            if (objs != null && objs.Length > 0)
            {
                return Array.TrueForAll(objs, obj => AssetDatabase.IsMainAsset(obj));
            }
            else
                return false;
        }

        [MenuItem(menu, priority = 2000, validate = false)]
        private static void ExcludeFrommBuild()
        {
            var objs = Selection.objects;
            if (objs != null && objs.Length > 0)
            {
                foreach (var obj in objs)
                {
                    //if (AssetDatabase.IsSubAsset(obj))
                    //{
                    //    Debug.LogError($"不支持子资源:{obj.name},请选择主资源,(not support sub asset:{obj.name},Please select the main Assets)");
                    //    continue;
                    //}
                    if (IsExcludeFromBuildLable(obj, out List<string> listLables))
                    {
                        listLables.Remove(assetExcludeLable);
                    }
                    else
                    {
                        listLables.Add(assetExcludeLable);
                    }
                    AssetDatabase.SetLabels(obj, listLables.ToArray());
                    AssetDatabase.Refresh();
                }


            }
        }

        /// <summary>
        /// 是否是Exclude From Build Lable
        /// </summary>
        /// <param name="listLables">返回当前object的lables</param>
        /// <returns>如果>=0 则是 Exclude From Build Lable,  ==-1,则不是</returns>
        private static bool IsExcludeFromBuildLable(Object obj, out List<string> listLables)
        {
            listLables = new List<string>(AssetDatabase.GetLabels(obj));
            return listLables.Exists(lable => lable == assetExcludeLable);
        }

        /// <summary>
        /// Begin Build
        /// </summary>
        void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
        {
            Debug.LogFormat(logCyan, "Begin Build");
            BuildHandler.OnBuildFaild -= BuildHandler_OnBuildFaild;
            BuildHandler.OnBuildFaild += BuildHandler_OnBuildFaild;
            BackupExcludeAssetsBeforeBuild();
        }

        /// <summary>
        /// Build Faild
        /// </summary>
        private void BuildHandler_OnBuildFaild()
        {
            Debug.LogError("Build Failed");
            RestoreExcludeAssetsAfterBuild();
        }

        /// <summary>
        /// Success Build
        /// </summary>
        void IPostprocessBuildWithReport.OnPostprocessBuild(BuildReport report)
        {
            Debug.LogFormat(logCyan, "Success Build");
            RestoreExcludeAssetsAfterBuild();
        }

        /// <summary>
        /// Backup assets
        /// </summary>
        public static void BackupExcludeAssetsBeforeBuild()
        {
            //寻找所有被标记的资源
            var excludeObjs = AssetDatabase.FindAssets("l:" + assetExcludeLable);
            if (excludeObjs != null && excludeObjs.Length > 0)
            {
                if (!Directory.Exists(backupFolder))
                    Directory.CreateDirectory(backupFolder);

                var excludePersistent = new ExcludeAssetsPersistentPath();
                foreach (var guid in excludeObjs)
                {
                    var basePath = AssetDatabase.GUIDToAssetPath(guid);
                    var obj = AssetDatabase.LoadMainAssetAtPath(basePath);
                    if (obj != null)
                    {
                        string backupPath = backupFolder + Path.GetFileName(basePath);
                        excludePersistent.listPath.Add(new ExcludeAssetsPersistentPath.AssetPath(backupPath, basePath));
                        string fullBasePath = Path.GetFullPath(basePath);
                        var att = File.GetAttributes(fullBasePath);
                        try
                        {
                            if (att == FileAttributes.Directory)
                            {
                                Directory.Move(fullBasePath, backupPath);
                            }
                            else
                            {
                                File.Move(fullBasePath, backupPath);
                            }
                            if (File.Exists(fullBasePath+".meta"))
                                File.Move(fullBasePath + ".meta", backupPath + ".meta");
                            Debug.LogFormat(logYellow, $"Strip Exclude Asset: [{basePath}]");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"backup error,备份资源错误 [{basePath}] : {e.ToString()}");
                        }
                    }
                }
                excludePersistent.Save();
            }
        }

        /// <summary>
        /// Restore assets
        /// </summary>
        public static void RestoreExcludeAssetsAfterBuild()
        {
            var excludePersistent = new ExcludeAssetsPersistentPath();
            excludePersistent.Load();
            if (excludePersistent != null && excludePersistent.listPath.Count > 0)
            {
                foreach (var assetPath in excludePersistent.listPath)
                {
                    var att = File.GetAttributes(assetPath.BackupPath);
                    if (att == FileAttributes.Directory)
                    {
                        if (Directory.Exists(assetPath.BackupPath))
                        {
                            Directory.Move(assetPath.BackupPath, assetPath.BasePath);
                            Debug.LogFormat(logCyan, $"Restore From Build: [{assetPath.BasePath}]");
                        }
                    }
                    else
                    {
                        if (File.Exists(assetPath.BackupPath))
                        {
                            File.Move(assetPath.BackupPath, assetPath.BasePath);
                            Debug.LogFormat(logCyan, $"Restore Exclude Asset: [{assetPath.BasePath}]");
                        }
                    }
                    if (File.Exists(assetPath.BackupPath + ".meta"))
                    {
                        File.Move(assetPath.BackupPath + ".meta", assetPath.BasePath + ".meta");
                    }
                }
                AssetDatabase.Refresh();
            }
        }
    }

    /// <summary>
    /// 将资源路径可持久化保存 防止打包过程出现错误导致资源被删除
    /// </summary>
    public class ExcludeAssetsPersistentPath
    {
        [System.Serializable]
        public class AssetPath
        {
            public string BackupPath;
            public string BasePath;
            public AssetPath(string backupPath, string basePath)
            {
                BackupPath = backupPath;
                BasePath = basePath;
            }
        }

        private string persistentExcludePath;
        public List<AssetPath> listPath = new List<AssetPath>();
        public ExcludeAssetsPersistentPath()
        {
            persistentExcludePath = Application.dataPath + "/../ProjectSettings/ExcludeFormBuilder.json";
        }
        public void Save()
        {
            File.WriteAllText(persistentExcludePath, JsonUtility.ToJson(this, true));
        }
        public void Load()
        {
            if (File.Exists(persistentExcludePath))
                JsonUtility.FromJsonOverwrite(File.ReadAllText(persistentExcludePath), this);
        }
    }
    public class ScriptsProcessor : AssetModificationProcessor
    {
        static AssetDeleteResult OnWillDeleteAsset(string assetPath, RemoveAssetOptions option)
        {
            if (option == RemoveAssetOptions.MoveAssetToTrash)
            {

            }
            return AssetDeleteResult.DidNotDelete;
        }
    }
}