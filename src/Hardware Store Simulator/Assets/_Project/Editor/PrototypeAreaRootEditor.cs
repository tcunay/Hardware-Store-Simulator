using HardwareStore.Gameplay.Scene;
using UnityEditor;
using UnityEngine;

namespace HardwareStore.Editor
{
    [CustomEditor(typeof(PrototypeAreaRoot))]
    public sealed class PrototypeAreaRootEditor : UnityEditor.Editor
    {
        private SerializedProperty _id;

        private void OnEnable() =>
            _id = serializedObject.FindProperty("_id");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(_id, new GUIContent("Зона"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Перемещай или поворачивай этот корень AREA — вместе с ним переедет " +
                "весь модуль. Масштаб оставляй (1, 1, 1). После изменений примени " +
                "компоновку, чтобы обновить навигацию и маршруты машин.",
                MessageType.Info);

            using (new EditorGUI.DisabledScope(
                       EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Применить компоновку карты"))
                    PrototypeSceneBuilder.ApplyManualYardLayout();
            }
        }
    }
}
