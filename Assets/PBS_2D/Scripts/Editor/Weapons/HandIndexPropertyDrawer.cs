using UnityEngine;
using UnityEditor;
using PBS2D;

namespace PBS2D.Editor
{
    [CustomPropertyDrawer(typeof(HandIndexAttribute))]
    public class HandIndexPropertyDrawer : PropertyDrawer
    {
        private const float PREVIEW_SIZE = 50f;
        private const float LABEL_WIDTH = 100f;

        // Cache skin once
        private static SkinConfig cachedSkin;

        private static SkinConfig GetSkin()
        {
            if (cachedSkin != null) return cachedSkin;

            var guids = AssetDatabase.FindAssets("t:SkinConfig");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                cachedSkin = AssetDatabase.LoadAssetAtPath<SkinConfig>(path);
            }
            return cachedSkin;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.isExpanded)
                return EditorGUIUtility.singleLineHeight + PREVIEW_SIZE + 4f;
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            // Foldout with clickable label
            var foldoutRect = new Rect(position.x, position.y, LABEL_WIDTH, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

            // field (only write back if changed)
            var fieldRect = new Rect(position.x + LABEL_WIDTH, position.y, 60, EditorGUIUtility.singleLineHeight);

            var skin = GetSkin();

            EditorGUI.BeginChangeCheck();
            int v = EditorGUI.IntField(fieldRect, property.intValue);
            int maxIdx = (skin != null && skin.Hands != null) ? skin.Hands.Length - 1 : 0;
            v = Mathf.Clamp(v, 0, maxIdx);
            if (EditorGUI.EndChangeCheck())
                property.intValue = v;

            if (property.isExpanded)
            {
                if (skin && skin.Hands != null && v < skin.Hands.Length)
                {
                    var sprite = skin.Hands[v];
                    if (sprite)
                    {
                        var previewRect = new Rect(position.x + LABEL_WIDTH + 65, position.y, PREVIEW_SIZE, PREVIEW_SIZE);
                        DrawSpritePreview(previewRect, sprite);

                        var idxRect = new Rect(previewRect.xMax + 5, position.y, 100, EditorGUIUtility.singleLineHeight);
                        EditorGUI.LabelField(idxRect, $"Hand {v}", EditorStyles.miniLabel);
                    }
                }
            }

            EditorGUI.EndProperty();
        }


        private void DrawSpritePreview(Rect rect, Sprite sprite)
        {
            if (sprite != null && sprite.texture != null)
            {
                // Create a white background
                EditorGUI.DrawRect(rect, Color.white);

                // Use AssetPreview to get a proper preview texture
                Texture2D preview = AssetPreview.GetAssetPreview(sprite);

                if (preview != null)
                {
                    // Calculate sprite aspect ratio
                    float spriteWidth = sprite.rect.width;
                    float spriteHeight = sprite.rect.height;
                    float spriteAspect = spriteWidth / spriteHeight;

                    // Calculate preview rect maintaining aspect ratio
                    Rect spriteRect = rect;
                    spriteRect.x += 2;
                    spriteRect.y += 2;
                    spriteRect.width -= 4;
                    spriteRect.height -= 4;

                    float previewAspect = spriteRect.width / spriteRect.height;

                    if (spriteAspect > previewAspect)
                    {
                        // Sprite is wider - fit to width
                        float newHeight = spriteRect.width / spriteAspect;
                        spriteRect.y += (spriteRect.height - newHeight) * 0.5f;
                        spriteRect.height = newHeight;
                    }
                    else
                    {
                        // Sprite is taller - fit to height
                        float newWidth = spriteRect.height * spriteAspect;
                        spriteRect.x += (spriteRect.width - newWidth) * 0.5f;
                        spriteRect.width = newWidth;
                    }

                    // Draw the preview texture
                    GUI.DrawTexture(spriteRect, preview, ScaleMode.ScaleToFit, true);
                }
                else
                {
                    // Fallback: try to draw the sprite texture directly if it's readable
                    try
                    {
                        var tex = sprite.texture;
                        if (tex != null && tex.isReadable)
                        {
                            GUI.DrawTextureWithTexCoords(rect, tex, GetSpriteUVs(sprite));
                        }
                        else
                        {
                            // Show a placeholder text if texture is not readable
                            EditorGUI.LabelField(rect, "Hand " + sprite.name, EditorStyles.centeredGreyMiniLabel);
                        }
                    }
                    catch
                    {
                        // If all fails, show placeholder
                        EditorGUI.LabelField(rect, "Preview", EditorStyles.centeredGreyMiniLabel);
                    }
                }

                // Draw border
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), Color.gray);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y + rect.height - 1, rect.width, 1), Color.gray);
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), Color.gray);
                EditorGUI.DrawRect(new Rect(rect.x + rect.width - 1, rect.y, 1, rect.height), Color.gray);
            }
        }

        private Rect GetSpriteUVs(Sprite sprite)
        {
            Texture2D texture = sprite.texture;
            Rect spriteRect = sprite.rect;

            return new Rect(
                spriteRect.x / texture.width,
                spriteRect.y / texture.height,
                spriteRect.width / texture.width,
                spriteRect.height / texture.height
            );
        }

    }
}
