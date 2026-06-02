using System.Linq;
using UnityEditor;
using UnityEngine;
using PBS2D;

namespace PBS2D.Editor
{
    [CustomPropertyDrawer(typeof(WeaponHoldOffset))]
    [CustomPropertyDrawer(typeof(NoDefaultPoseAttribute))]
    public class WeaponPoseOffsetDrawer : PropertyDrawer
    {
        private bool HasNoDefaultAttribute()
        {
            return fieldInfo.GetCustomAttributes(typeof(NoDefaultPoseAttribute), true).Any();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            bool hideDefault = HasNoDefaultAttribute();

            SerializedProperty useDefaultPose = property.FindPropertyRelative("UseDefaultPose");
            SerializedProperty positionOffset = property.FindPropertyRelative("PositionOffset");
            SerializedProperty rotation = property.FindPropertyRelative("Rotation");

            Rect currentRect = new (position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);

            property.isExpanded = EditorGUI.Foldout(currentRect, property.isExpanded, label, true);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                if (!hideDefault)
                {
                    currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    EditorGUI.PropertyField(currentRect, useDefaultPose);
                }

                if (hideDefault || !useDefaultPose.boolValue)
                {
                    currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    EditorGUI.PropertyField(currentRect, positionOffset);

                    currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    EditorGUI.PropertyField(currentRect, rotation);
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (!property.isExpanded)
            {
                return EditorGUIUtility.singleLineHeight;
            }

            bool hideDefault = HasNoDefaultAttribute();
            SerializedProperty useDefaultPose = property.FindPropertyRelative("UseDefaultPose");

            int lineCount = 1; // Foldout

            if (!hideDefault)
            {
                lineCount += 1; // UseDefaultPose
            }

            if (hideDefault || !useDefaultPose.boolValue)
            {
                lineCount += 2; // PositionOffset + Rotation
            }

            return lineCount * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
        }
    }
}
