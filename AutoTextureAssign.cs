using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable]
public class AssignData
{
    public string propertyName;
    public string textureName;
}

public class AssignDataVisualElement : VisualElement 
{
    public AssignDataVisualElement() 
    {
        var root = new VisualElement();

        var propertyName = new TextField { name = "propertyName", label = "Property Name", tooltip = "Property name that define in the shader" };
        var textureName = new TextField { name = "textureName", label = "Texture Name", tooltip = "Search filer will be material name + input string" };

        root.Add(propertyName);
        root.Add(textureName);
        Add(root);
    }
}

public class AutoTextureAssign : EditorWindow
{
    List<AssignData> assignData = new List<AssignData>();

    string findingDirectory = "Assets/";

    [MenuItem("Tools/Auto Texture Assign")]
    public static void ShowWindow()
    {
        AutoTextureAssign wnd = GetWindow<AutoTextureAssign>();
        wnd.titleContent = new GUIContent("AutoTextureAssign");
    }

    private void CreateGUI()
    {
        VisualElement root = rootVisualElement;

        Func<VisualElement> makeItem = () =>
        {
            var item = new AssignDataVisualElement();

            var propertyName = item.Q<TextField>("propertyName");
            var textureName = item.Q<TextField>("textureName");

            propertyName.RegisterValueChangedCallback(evt =>
            {
                var i = (int)propertyName.userData;
                var data = assignData[i];
                data.propertyName = evt.newValue.ToString();
            });

            textureName.RegisterValueChangedCallback(evt =>
            {
                var i = (int)textureName.userData;
                var data = assignData[i];
                data.textureName = evt.newValue.ToString();
            });

            return item;
        };

        Action<VisualElement, int> bindItem = (e, i) =>
        {
            var dataItem = e as AssignDataVisualElement;
            var propertyName = dataItem.Q<TextField>("propertyName");
            var textureName = dataItem.Q<TextField>("textureName");
            var data = assignData[i];

            propertyName.userData = i;
            textureName.userData = i;
        };

        var pathText = new TextField {name = "pathText", label = "Search Path", value = "Assets/" };

        var listView = new ListView(assignData, 40, makeItem, bindItem);
        listView.showBorder = true;
        listView.headerTitle = "Assign Data";
        listView.showFoldoutHeader = true;
        listView.showAddRemoveFooter = true;

        var assignButton = new Button { name = "assignButton", text = "Assign", tooltip = "Auto assign selected materials" };
        assignButton.clicked += Click;

        root.Add(pathText);
        root.Add(listView);
        root.Add(assignButton);

        pathText.RegisterValueChangedCallback(evt => 
        {
            findingDirectory = evt.newValue;
        });

        listView.Q<Button>("unity-list-view__add-button").clickable = new Clickable(() =>
        {
            listView.itemsSource.Add(new AssignData());
            listView.RefreshItems();
        });

        listView.Q<Button>("unity-list-view__remove-button").clickable = new Clickable(() =>
        {
            if (listView.selectedIndex < 0)
            {
                listView.itemsSource.RemoveAt(assignData.Count - 1);
            }
            else
            {
                listView.itemsSource.RemoveAt(listView.selectedIndex);
            }
            listView.RefreshItems();
        });
    }

    void Click()
    {
        var selection = Selection.objects;
        UpgradeSelection(selection, assignData);
    }

    public void UpgradeSelection(UnityEngine.Object[] selection, List<AssignData> assignData)
    {
        if (selection != null)
        {
            if ((!Application.isBatchMode) && (!EditorUtility.DisplayDialog("Material Upgrader", "The upgrade will overwrite materials in your project. " + "Make sure to have a project backup before proceeding", "Proceed", "Cancel")))
                return;

            int totalMaterialCount = 0;
            foreach (var obj in selection)
            {
                if (obj.GetType() == typeof(Material))
                    totalMaterialCount++;
            }

            int materialIndex = 0;
            foreach (var obj in selection)
            {
                if (obj.GetType() == typeof(Material))
                {
                    materialIndex++;
                    if (UnityEditor.EditorUtility.DisplayCancelableProgressBar("Updating", string.Format("({0} of {1}) {2}", materialIndex, totalMaterialCount, obj.name), (float)materialIndex / (float)totalMaterialCount))
                        break;

                    Material m = obj as Material;

                    Assign(m, assignData);
                }
            }
            UnityEditor.EditorUtility.ClearProgressBar();
            SaveAssetsAndFreeMemory();
        }
    }

    void Assign(Material m, List<AssignData> assignData) 
    {
        foreach (var data in assignData)
        {
            if (!m.HasTexture(data.propertyName))
                continue;

            var texture = SearchTexture(m.name, data.textureName);

            if (texture == null)
                continue;

            m.SetTexture(data.propertyName, texture);
        }
    }

    private Texture SearchTexture(string materialName, string name)
    {
        string[] pathArray = new string[1];
        pathArray.SetValue(findingDirectory, 0);

        var GUIDs = AssetDatabase.FindAssets(materialName + " " + name + " " + " t=Texture", pathArray);

        if (GUIDs.Length <= 0)
            return null;

        var assetsPaths = new List<string>();
        foreach (var Guid in GUIDs)
        {
            assetsPaths.Add(AssetDatabase.GUIDToAssetPath(Guid));
        }

        var textures = new List<Texture>();
        foreach (var path in assetsPaths) 
        {
            textures.Add(AssetDatabase.LoadAssetAtPath<Texture>(path));
        }

        var texture = AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(GUIDs[0])); ;
        
        return texture;
    }

    static void SaveAssetsAndFreeMemory()
    {
        AssetDatabase.SaveAssets();
        GC.Collect();
        EditorUtility.UnloadUnusedAssetsImmediate();
        AssetDatabase.Refresh();
    }
}
