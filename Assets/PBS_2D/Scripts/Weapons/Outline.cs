using System.Collections.Generic;
using UnityEngine;

namespace PBS2D
{
    public class Outline : MonoBehaviour
    {
        [SerializeField] private List<GameObject> _outlineTargets;
        [SerializeField] private Material _outlineMaterial;

        private readonly Dictionary<GameObject, SpriteRenderer> outlineCopies = new();

        void Awake()
        {
            foreach (GameObject target in _outlineTargets)
            {
                AddOutline(target);
            }
        }

        public void AddOutline(GameObject target)
        {
            Sprite targetSprite = target.GetComponent<SpriteRenderer>().sprite;

            GameObject copy = new(target.name + " Outline");
            copy.transform.SetParent(target.transform);
            copy.transform.localPosition = Vector2.zero;
            copy.transform.localScale = Vector3.one;
            copy.transform.localRotation = Quaternion.identity;

            SpriteRenderer copySR = copy.AddComponent<SpriteRenderer>();
            copySR.sprite = targetSprite;
            copySR.material = Instantiate(_outlineMaterial);

            outlineCopies.Add(target, copySR);
        }

        public bool TryRemoveOutline(GameObject target)
        {
            if (outlineCopies.TryGetValue(target, out SpriteRenderer copySR))
            {
                Destroy(copySR.gameObject);
                outlineCopies.Remove(target);

                return true;
            }
            else
            {
                return false;
            }
        }

        public void ShowAll()
        {
            foreach (SpriteRenderer sr in outlineCopies.Values)
            {
                sr.material.SetFloat("_ShowOutline", 1f);
            }
        }

        public void HideAll()
        {
            foreach (SpriteRenderer sr in outlineCopies.Values)
            {
                sr.material.SetFloat("_ShowOutline", 0f);
            }
        }
    }
}
