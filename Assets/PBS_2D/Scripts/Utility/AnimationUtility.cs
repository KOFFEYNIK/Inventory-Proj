using System.Collections;
using UnityEngine;

namespace PBS2D
{
    public static class AnimationUtility
    {
        public static IEnumerator MoveTo(Transform obj, Vector2 targetPos, float duration)
        {
            Vector2 startPos = obj.localPosition;

            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                obj.localPosition = Vector2.Lerp(startPos, targetPos, elapsed / duration);
                yield return null;
            }

            obj.localPosition = targetPos;
        }

        public static IEnumerator RotateTo(Transform transform, float targetRotation, float duration)
        {
            float startRotation = transform.localEulerAngles.z;

            for (float elapsed = 0; elapsed < duration; elapsed += Time.deltaTime)
            {
                float zRotation = Mathf.LerpAngle(startRotation, targetRotation, elapsed / duration);
                transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);

                yield return null;
            }

            transform.localRotation = Quaternion.Euler(0f, 0f, targetRotation);
        }
    }
}
