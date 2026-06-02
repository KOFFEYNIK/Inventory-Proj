using System.Collections.Generic;
using UnityEditor;
using PBS2D;

namespace PBS2D.Editor
{
    [CustomEditor(typeof(Gun))]
    public class GunEditor : UnityEditor.Editor
    {
        private SerializedProperty _stats;

        void OnEnable()
        {
            _stats = serializedObject.FindProperty("Stats");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour((Gun)target), typeof(MonoScript), false);
            }

            var exclusions = new List<string> { "m_Script" };

            if (_stats != null)
            {
                GunStats stats = (GunStats)_stats.objectReferenceValue;

                if (stats != null)
                {
                    GunCycleType cycleType = stats.CycleType;
                    GunReloadType reloadType = stats.ReloadType;

                    // CycleType
                    if (cycleType == GunCycleType.SelfCycle)
                    {
                        exclusions.AddRange(new[] { "Forend" });
                    }
                    else if (cycleType == GunCycleType.BoltAction)
                    {
                        exclusions.AddRange(new[] { "FiringModes", "Forend" });
                    }
                    else if (cycleType == GunCycleType.PumpAction)
                    {
                        exclusions.AddRange(new[] { "FiringModes" });
                    }


                    // ReloadType
                    if (reloadType == GunReloadType.Magazine)
                    {
                        exclusions.AddRange(new[] { "ShellPrefab", "ShellInsertDepth", "ShellHandOffset" });
                    }
                    else if (reloadType == GunReloadType.Shell)
                    {
                        exclusions.AddRange(new[] { "MagPrefab", "MagInsertDepth", "CurrentMag" });
                    }
                }
            }

            DrawPropertiesExcluding(serializedObject, exclusions.ToArray());

            serializedObject.ApplyModifiedProperties();
        }
    }
}
