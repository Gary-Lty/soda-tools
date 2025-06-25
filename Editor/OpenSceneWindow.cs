using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using System.IO;

public class OpenSceneWindow : EditorWindow
{
    [MenuItem("Window/Open Scene Window %F1")]
    public static void ShowWindow()
    {
        // 显示或获取已存在的窗口实例
        GetWindow<OpenSceneWindow>("Open Scene");
    }

    private List<string> scenePaths = new List<string>();
    private string selectedScenePath = "";
    private List<string> otherScene = new List<string>();

    private void OnEnable()
    {
        // 初始化场景列表
        LoadScenesFromBuildSettings();
        LoadOtherScenes();
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

        // 显示 Other Scenes 列表
        GUILayout.Label("Other Scenes:", EditorStyles.boldLabel);
        foreach (var scene in otherScene)
        {
            string sceneName = Path.GetFileNameWithoutExtension(scene);
            if (GUILayout.Button($"{sceneName}\n{scene}"))
            {
                selectedScenePath = scene;
            }
        }

        GUILayout.Space(30);

        if (!string.IsNullOrEmpty(selectedScenePath))
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

        GUILayout.Space(10);
        if (GUILayout.Button("Edit Other Scenes"))
        {
            OtherSceneEditorWindow.ShowWindow(this, otherScene);
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

    private void LoadOtherScenes()
    {
        string path = Application.dataPath +  "/Editor/otherScenes.txt";
        if (File.Exists(path))
        {
            otherScene = new List<string>(File.ReadAllLines(path));
        }
    }

    public void SaveOtherScenes()
    {
        string path = Application.dataPath + "/Editor/otherScenes.txt";
        File.WriteAllLines(path, otherScene.ToArray());
    }

    public void RefreshOtherScenes(List<string> updatedScenes)
    {
        otherScene = updatedScenes;
        Repaint(); // 强制重绘窗口
    }
}

public class OtherSceneEditorWindow : EditorWindow
{
    private List<string> scenes;
    private OpenSceneWindow parentWindow;

    public static void ShowWindow(OpenSceneWindow parent, List<string> currentScenes)
    {
        var window = GetWindow<OtherSceneEditorWindow>("Edit Other Scenes");
        window.parentWindow = parent;
        window.scenes = new List<string>(currentScenes);
    }

    private void OnGUI()
    {
        Event evt = Event.current;
        Rect dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));

        GUI.Box(dropArea, "Drag & Drop Scenes Here To Add Scene");

        switch (evt.type)
        {
            case EventType.DragUpdated:
            case EventType.DragPerform:
                if (!dropArea.Contains(evt.mousePosition))
                    break;

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();

                    foreach (string draggedObject in DragAndDrop.paths)
                    {
                        if (draggedObject.EndsWith(".unity") || draggedObject.EndsWith(".scene"))
                        {
                            scenes.Add(draggedObject);
                        }
                    }

                    Repaint();
                }
                break;
        }

        GUILayout.Label("Edit Other Scenes", EditorStyles.boldLabel);

        for (int i = 0; i < scenes.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            scenes[i] = EditorGUILayout.TextField(scenes[i]);
            if (GUILayout.Button("-", GUILayout.Width(25)))
            {
                scenes.RemoveAt(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("+ Add New Scene"))
        {
            scenes.Add("");
        }

        if (GUILayout.Button("Save Changes"))
        {
            SaveChanges();
        }
    }

    private void SaveChanges()
    {
        if (parentWindow != null)
        {
            parentWindow.RefreshOtherScenes(new List<string>(scenes));
            parentWindow.SaveOtherScenes();
        }
        Close();
    }
}



