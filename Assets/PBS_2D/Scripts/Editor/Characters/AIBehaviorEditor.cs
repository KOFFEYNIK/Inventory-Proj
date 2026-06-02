using System.Collections.Generic;
using UnityEditor;
using PBS2D;

namespace PBS2D.Editor
{
    [CustomEditor(typeof(AIBehavior))]
    public class AIBehaviorEditor : UnityEditor.Editor
    {
        private SerializedProperty _chaseTarget;
        private SerializedProperty _attackTarget;

        void OnEnable()
        {
            _chaseTarget = serializedObject.FindProperty("ChaseTarget");
            _attackTarget = serializedObject.FindProperty("AttackTarget");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromScriptableObject((AIBehavior)target), typeof(MonoScript), false);
            }

            var exclusions = new List<string> { "m_Script" };

            if (_chaseTarget != null && !_chaseTarget.boolValue)
            {
                exclusions.AddRange(new[] { "KeepDistance", "WalkDistance" });
            }

            if (_attackTarget != null && !_attackTarget.boolValue)
            {
                exclusions.AddRange(new[] { "MaxShootDistance", "RayCount", "RaySpreadAngle", "ReactionTime", "BurstDuration", "BurstPause", "HitChance" });
            }

            DrawPropertiesExcluding(serializedObject, exclusions.ToArray());

            serializedObject.ApplyModifiedProperties();
        }
    }
}
