using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
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
        ******************************************************/

        int IOrderedCallback.callbackOrder => 0;

        public const string assetExcludeLable = "Exclude From Build";

        const string menu = "Assets/Exclude From Build";
        const string logCyan = "<color=Cyan>{0}</color>";
        const string backupFolder = "Assets/Editor/ExcludeFromBuildTemp/";

        static List<(string basePath, string tempPath)> listTempPath = new List<(string basePath, string tempPath)>();



        [MenuItem(menu, priority = 2000, validate = true)]
        static bool ExcludeFrommBuildValidate()
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
        static void ExcludeFrommBuild()
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
                        listLables.Remove(assetExcludeLable);
                    else
                        listLables.Add(assetExcludeLable);
                    AssetDatabase.SetLabels(obj, listLables.ToArray());
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
            //创建文件夹
            //error:Parent directory is not in asset database 这时候assetdatabase中不存在文件夹,所有要用assetdatabase.createfolder
            //if (!Directory.Exists(backupFolder))
            // Directory.CreateDirectory(backupFolder);

            CreateFolder(backupFolder);
            listTempPath.Clear();
            var allObjs = AssetDatabase.FindAssets("t:Object");
            foreach (var guid in allObjs)
            {
                var basePath = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadMainAssetAtPath(basePath);
                if (obj != null)
                {
                    if (IsExcludeFromBuildLable(obj, out List<string> _))
                    {
                        string tempPath = backupFolder + Path.GetFileName(basePath);
                        AssetDatabase.MoveAsset(basePath, tempPath);
                        listTempPath.Add((basePath, tempPath));
                    }
                }
            }
            foreach (var item in listTempPath)
            {
                Debug.Log($"Exclude From Build: [{item.basePath}]");
            }
        }
        /// <summary>
        /// Restore assets
        /// </summary>
        public static void RestoreExcludeAssetsAfterBuild()
        {
            foreach (var path in listTempPath)
            {
                var result = AssetDatabase.MoveAsset(path.tempPath, path.basePath);
                if (!string.IsNullOrEmpty(result))
                {
                    Debug.LogError($"{result}: {path.tempPath}->{path.basePath}");
                }
            }
            listTempPath.Clear();
            //delete folder
            AssetDatabase.DeleteAsset(backupFolder);
            AssetDatabase.Refresh(); //这里要刷新一下 不知为何 测试了下可以不用刷新的
            var assets = AssetDatabase.FindAssets("t:Object", new string[] { "Assets/Editor" });
            if (assets.Length <= 0)
                AssetDatabase.DeleteAsset("Assets/Editor");
            AssetDatabase.Refresh();
        }


        private static void CreateFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string[] folderNames = folder.Split("/");
            StringBuilder sb = new StringBuilder(folderNames[0]);
            for (int i = 1; i < folderNames.Length; i++)
            {
                var tempPath = sb.ToString();
                sb.Append("/" + folderNames[i]);
                if (!AssetDatabase.IsValidFolder(sb.ToString()))
                    AssetDatabase.CreateFolder(tempPath, folderNames[i]);
            }

        }
    }
}