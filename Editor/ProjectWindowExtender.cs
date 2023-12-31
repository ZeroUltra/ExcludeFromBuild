using System;
using System.Collections;
using UnityEditor;
using UnityEngine;

namespace ZeroUltra.ExcludeFormBuild
{
    [InitializeOnLoad]
    public class ProjectWindowExtender
    {
        static Texture icon;
        static ProjectWindowExtender()
        {
            icon = EditorGUIUtility.IconContent("winbtn_win_close").image;
            EditorApplication.projectWindowItemOnGUI += OnGUI;
        }

        private static void OnGUI(string guid, Rect selectionRect)
        {
            if (Array.Exists(AssetDatabase.GetLabels(new GUID(guid)), lable => lable == ExcludeFormBuilder.assetExcludeLable))
            {
                Rect rect = selectionRect;
                rect.x += 10;
                rect.y += 6;
                rect.height = 13;
                rect.width = 13;
                GUI.color = Color.red;
                GUI.DrawTexture(rect, icon);
                GUI.color = Color.white;
            }
        }
    }
}