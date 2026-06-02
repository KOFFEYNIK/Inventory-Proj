using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PBS2D
{
    public class WeaponUI : Singleton<WeaponUI>
    {
        [System.NonSerialized] public Gun Gun;

        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI _loadedAmmoText;
        [SerializeField] private TextMeshProUGUI _reserveAmmoText;
        [SerializeField] private Image _fireModeImage;

        [Header("Fire Mode Icons")]
        [SerializeField] private Sprite _fullAutoIcon;
        [SerializeField] private Sprite _semiAutoIcon;
        [SerializeField] private Sprite _burstIcon;

        public void UpdateAmmoUI()
        {
            if (Gun == null) return;

            _loadedAmmoText.text = Gun.CurrentLoadedAmmo.ToString();
            _reserveAmmoText.text = Gun.CurrentReserveAmmo.ToString();
        }

        public void UpdateFireModeIcon()
        {
            if (Gun == null) return;

            GunTriggerMode mode = (Gun.Stats.CycleType == GunCycleType.SelfCycle) ? Gun.CurrentFireMode : GunTriggerMode.Semi;

            _fireModeImage.sprite = mode switch
            {
                GunTriggerMode.Auto => _fullAutoIcon,
                GunTriggerMode.Burst => _burstIcon,
                _ => _semiAutoIcon
            };
        }
    }
}
