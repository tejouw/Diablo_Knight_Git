#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AreaWithMaxMonsters))]
public class AreaWithMaxMonstersDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        // Area referansını al
        SerializedProperty areaProp = property.FindPropertyRelative("area");
        
        // Area atanmışsa ismini göster, yoksa varsayılan ismi kullan
        string displayName = "Element " + label.text.Replace("Element ", "");
        
        if (areaProp.objectReferenceValue != null)
        {
            AreaData area = areaProp.objectReferenceValue as AreaData;
            if (area != null && !string.IsNullOrEmpty(area.areaName))
            {
                displayName = area.areaName;
            }
        }
        
        // Özelliği çiz
        EditorGUI.PropertyField(position, property, new GUIContent(displayName), true);
    }
    
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUI.GetPropertyHeight(property, true);
    }
}
#endif