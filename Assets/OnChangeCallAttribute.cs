using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// https://stackoverflow.com/questions/63778384/unity-how-to-update-an-object-when-a-serialized-field-is-changed

public class OnChangeCallAttribute : PropertyAttribute {
    public string MethodName;

    public OnChangeCallAttribute(string methodName) {
        MethodName = methodName;
    }
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(OnChangeCallAttribute))]
public class OnChangeCallAttributeDrawer : PropertyDrawer {
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
        EditorGUI.BeginChangeCheck();
        EditorGUI.PropertyField(position, property, label);
        if (EditorGUI.EndChangeCheck()) {
            var at = attribute as OnChangeCallAttribute;
            var method = property.serializedObject.targetObject
                .GetType().GetMethods().First(m => m.Name == at.MethodName);
            
            if (!method.GetParameters().Any())
                method.Invoke(property.serializedObject.targetObject, null);
        }
    }
}

#endif
