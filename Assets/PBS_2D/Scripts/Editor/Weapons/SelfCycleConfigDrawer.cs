using UnityEngine;
using UnityEditor;
using PBS2D;

namespace PBS2D.Editor
{
    [CustomPropertyDrawer(typeof(SelfCycleConfig))]
    public class SelfCycleConfigDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            // 1. Draw the main Foldout
            Rect foldoutRect = new Rect(position.x, position.y, position.width, lineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                SerializedProperty firingModes = property.FindPropertyRelative("FiringModes");
                SerializedProperty isBurstSemi = property.FindPropertyRelative("IsBurstSemiAuto");
                SerializedProperty burstLength = property.FindPropertyRelative("BurstLength");
                SerializedProperty burstInterval = property.FindPropertyRelative("BurstInterval");

                float currentY = position.y + lineHeight + spacing;

                // 2. Always draw Firing Modes
                Rect modesRect = new Rect(position.x, currentY, position.width, lineHeight);
                EditorGUI.PropertyField(modesRect, firingModes);
                currentY += lineHeight + spacing;

                // 3. Logic: Check if 'Burst' flag (value 4) is toggled
                // We use bitwise AND to check the flag
                bool hasBurst = (firingModes.intValue & (int)GunTriggerMode.Burst) != 0;

                if (hasBurst)
                {
                    // Draw Burst specific fields
                    EditorGUI.PropertyField(new Rect(position.x, currentY, position.width, lineHeight), isBurstSemi);
                    currentY += lineHeight + spacing;

                    EditorGUI.PropertyField(new Rect(position.x, currentY, position.width, lineHeight), burstLength);
                    currentY += lineHeight + spacing;

                    EditorGUI.PropertyField(new Rect(position.x, currentY, position.width, lineHeight), burstInterval);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded) return EditorGUIUtility.singleLineHeight;

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            // Base: Foldout line + FiringModes line
            int lines = 2;

            SerializedProperty firingModes = property.FindPropertyRelative("FiringModes");
            bool hasBurst = (firingModes.intValue & (int)GunTriggerMode.Burst) != 0;

            if (hasBurst)
            {
                lines += 3; // IsBurstSemiAuto, BurstLength, BurstInterval
            }

            return (lines * lineHeight) + ((lines - 1) * spacing);
        }
    }
}
