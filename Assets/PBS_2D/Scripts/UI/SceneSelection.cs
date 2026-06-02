using UnityEngine;
using UnityEngine.SceneManagement;

namespace PBS2D
{
    public class SceneSelection : SettingsMenu
    {
        [SerializeField]
        private string _firstSceneName = "GunRange";

        [SerializeField]
        private string _secondSceneName = "TestLevel";

        public void LoadFirstScene()
        {
            SceneManager.LoadScene(_firstSceneName);
        }

        public void LoadSecondScene()
        {
            SceneManager.LoadScene(_secondSceneName);
        }
    }
}
