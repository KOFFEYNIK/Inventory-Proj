using System.Collections.Generic;
using UnityEditor;

namespace PBS2D.Editor
{
    [CustomEditor(typeof(EnemySpawner))]
    public class EnemySpawnerEditor : UnityEditor.Editor
    {
        private SerializedProperty _onlySpawnOneAtATime;

        void OnEnable()
        {
            _onlySpawnOneAtATime = serializedObject.FindProperty("_onlySpawnOneAtATime");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((EnemySpawner)target), typeof(MonoScript), false);
            }

            var exclusions = new List<string> { "m_Script" };

            if (_onlySpawnOneAtATime != null && _onlySpawnOneAtATime.boolValue)
            {
                exclusions.Add("_spawnInterval");
            }

            DrawPropertiesExcluding(serializedObject, exclusions.ToArray());

            serializedObject.ApplyModifiedProperties();
        }
    }
}
