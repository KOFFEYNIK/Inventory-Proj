using UnityEngine;

namespace PBS2D
{
    [RequireComponent(typeof(Outline))]
    public abstract class Interactable : MonoBehaviour
    {
        private Outline _outline;

        protected virtual void Awake()
        {
            _outline = GetComponent<Outline>();
        }

        public void AddOutline(GameObject target)
        {
            _outline.AddOutline(target);
        }

        public bool TryRemoveOutline(GameObject target)
        {
            return _outline.TryRemoveOutline(target);
        }

        public void ShowOutline()
        {
            _outline.ShowAll();
        }

        public void HideOutline()
        {
            _outline.HideAll();
        }

        public abstract bool CanInteract();

        public abstract void Interact(Character interactor);
    }
}
