using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text;

public class ChangeScriptEncodingFormat
{
    // 添加一个右键菜单。
    // % 按下ctrl时显示菜单。（Windows: control, macOS: command）
    // & 按下alt时显示菜单。(Windows/Linux: alt, macOS: option)
    // _ 按下shift时显示菜单。(Windows/Linux/macOS: shift)
    [MenuItem("Assets/脚本改格式：GB2312->UTF8无BOM %g", false, 100)]
    private static void CustomMenu()
    {
        // 例如: 获取Project视图中选定的对象
        Object selectedObject = Selection.activeObject;

        if (selectedObject != null)
        {
            // 获取选定对象的相对路径
            string relativeAssetPath = AssetDatabase.GetAssetPath(selectedObject);
            // 获取项目根目录路径
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            // 获取选定对象的绝对路径
            string absoluteAssetPath = Path.Combine(projectPath, relativeAssetPath);
            // 获取选定对象的文件名（包括后缀）
            string fileName = Path.GetFileName(relativeAssetPath);

            Debug.Log("执行自定义操作: " + selectedObject.name +
                      ", 相对路径: " + relativeAssetPath +
                      ", 绝对路径: " + absoluteAssetPath +
                      ", 文件名: " + fileName);

            //判断是否是CSharp文件
            if (IsCSharpFile(fileName))
            {
                Debug.Log("这是一个csharp文件");
                ChangeFormat(absoluteAssetPath);
            }
            else
            {
                Debug.Log("兄弟，这不是一个csharp文件啊~~~~~~~~~~~");
            }
        }
        else
        {
            Debug.LogWarning("没有选中任何对象.");
        }
    }

    // 如果项目视图中有选中的对象，则启用右键菜单项
    [MenuItem("Assets/脚本改格式：GB2312->UTF8无BOM %g", true)]
    private static bool ValidateCustomMenu()
    {
        return Selection.activeObject != null;
    }

    /// <summary>
    /// 判断该文件是否是CSharp文件
    /// </summary>
    /// <param name="fileName"></param>
    /// <returns></returns>
    private static bool IsCSharpFile(string fileName)
    {
        // 获取文件扩展名（包括点）
        string fileExtension = Path.GetExtension(fileName);

        // 将扩展名转换为小写并与 ".cs" 进行比较
        if (fileExtension.ToLower() == ".cs")
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 文件格式转码：GB2312转成UTF8
    /// 读取指定的文件，转换成UTF8（无BOM标记）格式后，回写覆盖原文件
    /// </summary>
    /// <param name="sourceFilePath">文件路径</param>
    public static void ChangeFormat(string sourceFilePath)
    {
        string fileContent = File.ReadAllText(sourceFilePath, Encoding.GetEncoding("GB2312"));
        File.WriteAllText(sourceFilePath, fileContent,new UTF8Encoding(false));
        Debug.Log("处理结束！");
    }
}
