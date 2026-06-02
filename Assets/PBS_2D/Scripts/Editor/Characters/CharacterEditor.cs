using System.Collections.Generic;
using UnityEditor;
using PBS2D;

namespace PBS2D.Editor
{
    [CustomEditor(typeof(Character))]
    public class CharacterEditor : UnityEditor.Editor
    {
        private SerializedProperty _isPlayer;

        void OnEnable()
        {
            _isPlayer = serializedObject.FindProperty("IsPlayer");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((Character)target), typeof(MonoScript), false);
            }

            var exclusions = new List<string> { "m_Script" };

            if (_isPlayer != null && _isPlayer.boolValue)
                exclusions.Add("AIBehavior");

            DrawPropertiesExcluding(serializedObject, exclusions.ToArray());

            serializedObject.ApplyModifiedProperties();
        }
    }
}
