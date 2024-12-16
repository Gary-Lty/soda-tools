using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

public class OpenSceneWindow : EditorWindow
{
    [MenuItem("Window/Open Scene Window")]
    public static void ShowWindow()
    {
        // 显示或获取已存在的窗口实例
        GetWindow<OpenSceneWindow>("Open Scene");
    }

    private List<string> scenePaths = new List<string>();
    private string selectedScenePath = "";

    private void OnEnable()
    {
        // 初始化场景列表
        LoadScenesFromBuildSettings();
    }

    private void OnGUI()
    {
        GUILayout.Label("Load a specific scene", EditorStyles.boldLabel);

        // 显示场景列表
        GUILayout.Label("Available Scenes:", EditorStyles.boldLabel);
        foreach (string scenePath in scenePaths)
        {
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            if (GUILayout.Button($"{sceneName}\n{scenePath}"))
            {
                selectedScenePath = scenePath;
            }
        }
        GUILayout.Space(30);
        if (selectedScenePath != "")
        {
            GUILayout.Label($"Selected Scene: {Path.GetFileNameWithoutExtension(selectedScenePath)}", EditorStyles.boldLabel);
            GUILayout.Label($"Selected Scene Path: {selectedScenePath}", EditorStyles.boldLabel);
        }

        if (GUILayout.Button("Open Selected Scene"))
        {
            // 检查当前场景是否有未保存的修改
            if (EditorApplication.SaveCurrentSceneIfUserWantsTo())
            {
                LoadSelectedScene();
            }
        }
    }

    private void LoadScenesFromBuildSettings()
    {
        scenePaths.Clear();

        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
            scenePaths.Add(scenePath);
        }
    }

    private void LoadSelectedScene()
    {
        if (string.IsNullOrEmpty(selectedScenePath))
        {
            Debug.LogWarning("No scene selected to load.");
            return;
        }

        // 使用 EditorSceneManager.OpenScene 加载场景
        Scene loadedScene = EditorSceneManager.OpenScene(selectedScenePath);
        if (!loadedScene.isLoaded)
        {
            Debug.LogWarning("Failed to open scene: " + selectedScenePath);
        }
        else
        {
            Debug.Log("Scene opened successfully: " + selectedScenePath);
        }
    }
}