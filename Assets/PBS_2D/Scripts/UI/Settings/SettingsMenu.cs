using UnityEngine;
using UnityEngine.EventSystems;

namespace PBS2D
{
    public abstract class SettingsMenu : MonoBehaviour
    {
        [SerializeField] private GameObject _firstSelected;

        public void SelectFirstButton()
        {
            EventSystem.current.SetSelectedGameObject(_firstSelected);
        }
    }
}
