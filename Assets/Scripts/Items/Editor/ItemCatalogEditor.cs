using How2Shop.Items;
using UnityEditor;
using UnityEngine;

namespace How2Shop.Items.Editor
{
    [CustomEditor(typeof(ItemCatalog))]
    public sealed class ItemCatalogEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("items"), true);
            serializedObject.ApplyModifiedProperties();

            var catalog = (ItemCatalog)target;
            var errors = catalog.GetValidationErrors();
            foreach (var error in errors)
                EditorGUILayout.HelpBox(error, MessageType.Error);

            if (errors.Count == 0)
                EditorGUILayout.HelpBox($"{catalog.Items.Count} item(s) ready.", MessageType.Info);
        }
    }
}
