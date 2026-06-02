using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using PBS2D;

namespace PBS2D.Editor
{
    [CustomEditor(typeof(GunStats))]
    public class GunStatsEditor : UnityEditor.Editor
    {
        private SerializedProperty _cycleType;

        void OnEnable()
        {
            _cycleType = serializedObject.FindProperty("CycleType");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((GunStats)target), typeof(MonoScript), false);
            }

            var exclusions = new List<string> { "m_Script" };

            if (_cycleType.enumValueIndex != (int)GunCycleType.SelfCycle)
            {
                exclusions.AddRange(new[] { "SelfCycleConfig" });
            }

            // Draw everything except the exclusions
            DrawPropertiesExcluding(serializedObject, exclusions.ToArray());

            serializedObject.ApplyModifiedProperties();
        }
    }
}
