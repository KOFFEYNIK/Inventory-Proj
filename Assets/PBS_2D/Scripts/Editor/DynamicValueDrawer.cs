using UnityEngine;
using UnityEditor;
using PBS2D;

namespace PBS2D.Editor
{
    [CustomPropertyDrawer(typeof(DynamicFloat), true)]
    [CustomPropertyDrawer(typeof(DynamicInt), true)]
    public class DynamicValueDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            Rect foldoutRect = new(position.x, position.y, position.width, lineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                SerializedProperty mode = property.FindPropertyRelative("Mode");
                SerializedProperty value = property.FindPropertyRelative("Value");
                SerializedProperty minValue = property.FindPropertyRelative("MinValue");
                SerializedProperty maxValue = property.FindPropertyRelative("MaxValue");

                float currentY = position.y + lineHeight + spacing;

                void DrawField(ref float y, SerializedProperty prop)
                {
                    Rect rect = new(position.x, y, position.width, lineHeight);
                    EditorGUI.PropertyField(EditorGUI.IndentedRect(rect), prop);
                    y += lineHeight + spacing;
                }

                EditorGUI.indentLevel++;

                DrawField(ref currentY, mode);

                if (mode.enumValueIndex == 0)
                {
                    DrawField(ref currentY, value);
                }
                else
                {
                    DrawField(ref currentY, minValue);
                    DrawField(ref currentY, maxValue);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            if (!property.isExpanded)
            {
                return lineHeight;
            }

            SerializedProperty mode = property.FindPropertyRelative("Mode");

            int lineCount = 2;

            if (mode != null)
            {
                lineCount += (mode.enumValueIndex == 0) ? 1 : 2;
            }

            return (lineHeight * lineCount) + (spacing * (lineCount - 1));
        }
    }
}
