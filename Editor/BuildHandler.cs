using System;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace ZeroUltra.ExcludeFormBuild
{
    [InitializeOnLoad]
    public class BuildHandler
    {
        //https://gamedev.stackexchange.com/questions/181611/custom-build-failure-callback
        //https://docs.unity3d.com/ScriptReference/BuildPlayerWindow.RegisterBuildPlayerHandler.html
        public static event System.Action OnBuildFaild;
        static BuildHandler()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(Build);
        }

        private static void Build(BuildPlayerOptions buildOptions)
        {
            try
            {
                //now start Unity's default building procedure
                BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(buildOptions);
            }
            catch (BuildPlayerWindow.BuildMethodException ex)
            {
                Debug.LogError(ex.ToString());
                OnBuildFaild?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.ToString());
                OnBuildFaild?.Invoke();
            }
        }

    }
}