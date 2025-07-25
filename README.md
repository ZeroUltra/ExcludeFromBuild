

# ExcludeFromBuild

作用：在Build Unity工程的时候排除不参与打包的资源

## 打包过程中经常遇到的问题

* 在打包过程中，是不允许运行时脚本有Editor的代码的（除非使用#if UNITY_EDITOR包裹），但是在开发中经常会写一些测试脚本，经常在Build的时候发生错误，例如下面代码

```c#
using System.Collections;
using UnityEditor;
using UnityEngine;

public class NewMonoBehaviour : MonoBehaviour
{
    private void Start()
    {
        AssetDatabase.GetAssetPath(this.gameObject);
    }
}
```

​		（这段代码没有单独创建Assembly Definition）,那么打包的时候就会出现下面错误，错误也很明显，引用了Editor代码

![image-20231231190825083](https://raw.githubusercontent.com/ZeroUltra/MediaLibrary/main/Imgs/202312311908857.png)

当然Unity提供了一种特殊文件方式，用于忽略项目文件

[Unity - Manual: Special folder names (unity3d.com)](https://docs.unity3d.com/Manual/SpecialFolders.html)

- Hidden folders.
- Files and folders which start with ‘**.**’.
- Files and folders which end with ‘**~**’.
- Files and folders named **cvs**.
- Files with the extension **.tmp**.

利用这种方式可以规避某些不要的文件进入打包。

但是这种方式有两个麻烦点

1. 要手动修改文件（文件夹）格式（这个容易）
2. 第二就是如果把`文件`修改，如将`Test.cs`->`Test.cs~`,那么它的meta文件也会消失，就很蛋疼

## 解决方案

步骤：

1. 标记需要排除的资源
2. 在Build之前将资源和.meta移到`项目Assets之外`，并记录
3. 打包Build完成或者Build失败之后要还原

## 使用方法

1. 使用UnityPackageManager,添加git项目地址
2. 导入插件后，标记需要排除的资源（文件或文件夹）

![111](https://raw.githubusercontent.com/ZeroUltra/MediaLibrary/main/Imgs/202312311932079.gif)

![222](https://raw.githubusercontent.com/ZeroUltra/MediaLibrary/main/Imgs/202312311938476.gif)

**鼠标右键->Exclude From Build**，会将资源标记，如果已经标记了再次选择则会排除

*tips:如果你的projects视图也是`two column layout`,不要选左边目录栏，unity将不认*

OK已经完成了所有操作，接下来就可以打包了

## 注意事项

1. 只适用于内置Build，没有测试SBP

2. 打包时只能使用Unity自带得Build（也就是Build Setting窗口得`Build`和`Build And Run`）,因为使用了`BuildPlayerWindow.RegisterBuildPlayerHandler`来获取打包失败回调，具体查看：[Unity - Scripting API: BuildPlayerWindow.RegisterBuildPlayerHandler (unity3d.com)](https://docs.unity3d.com/ScriptReference/BuildPlayerWindow.RegisterBuildPlayerHandler.html)

3. 如果使用自定义代码打包,先注释`ExcludeFormBuilder.cs`代码中`BackupExcludeAssetsBeforeBuild`，然后可以使用类似如下代码，需用`try finally` 防止过程出错

     ![image-20250627142323856](https://raw.githubusercontent.com/ZeroUltra/MediaLibrary/main/Imgs/202507231140597.png)

```c#
//先备份
ZeroUltra.ExcludeFormBuild.ExcludeFormBuilder.BackupExcludeAssetsBeforeBuild();
try
{
    //模拟打app 
   var buildReports = BuildPipeline.BuildPlayer(buildPlayerOptions);
}
catch (Exception)
{
      throw;
}
finally 
{ 
    //最后还原
     ZeroUltra.ExcludeFormBuild.ExcludeFormBuilder.RestoreExcludeAssetsAfterBuild();
}
```



4. 由于使用了`IPreprocessBuildWithReport, IPostprocessBuildWithReport`,这两个接口，但是它们在打包AssetBundle不起作用，参考：

   https://docs.unity3d.com/ScriptReference/Build.IPreprocessBuildWithReport.OnPreprocessBuild.html，

   https://docs.unity3d.com/ScriptReference/Build.IPostprocessBuildWithReport.OnPostprocessBuild.html

   所有在打包AssetBundle的时候，也做如下类似修改

```c#
ZeroUltra.ExcludeFormBuild.ExcludeFormBuilder.BackupExcludeAssetsBeforeBuild();
try
{
    BuildPipeline.BuildAssetBundles(outPath, BuildAssetBundleOptions.None, BuildTarget.Android);
}
catch (Exception)
{
      throw;
}
finally 
{ 
     ZeroUltra.ExcludeFormBuild.ExcludeFormBuilder.RestoreExcludeAssetsAfterBuild();
}
```



**有问题欢迎提issues**
