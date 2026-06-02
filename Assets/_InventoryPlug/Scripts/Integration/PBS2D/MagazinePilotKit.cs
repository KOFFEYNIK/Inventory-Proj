using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventoryPlug.Integration.PBS2D
{
    /// <summary>
    /// Пилотный стартовый «кит» для проверки магазинной системы: при старте надевает рюкзак
    /// (чтобы появилась реальная сетка под магазины) и сеет в него запасные магазины
    /// (с предзарядкой патронами) и россыпь. Вешается на InventoryRig в тестовой сцене.
    /// Чисто сценарное удобство — не часть рантайма плагина.
    /// </summary>
    public class MagazinePilotKit : MonoBehaviour
    {
        [Serializable]
        public class MagSeed
        {
            [Tooltip("Предмет-магазин (ItemDefinition, isContainer = true).")]
            public ItemDefinition magazineDefinition;
            [Tooltip("Калибр, которым предзарядить магазин.")]
            public ItemDefinition caliber;
            [Tooltip("Сколько патронов уже в магазине (0 = пустой).")]
            public int preload = 30;
        }

        [Serializable]
        public class AmmoSeed
        {
            public ItemDefinition ammoDefinition;
            public int count = 40;
        }

        [Header("Backpack")]
        [Tooltip("Рюкзак, который надеть в слот Backpack, если он пуст.")]
        public ItemDefinition backpackDefinition;
        public bool equipBackpackIfEmpty = true;

        [Header("Seed into inventory")]
        public List<MagSeed> spareMags = new();
        public List<AmmoSeed> looseAmmo = new();

        private bool _done;

        private void Update()
        {
            if (_done) return;
            var mgr = InventoryManager.Instance;
            if (mgr == null) return; // ждём инициализации
            Seed(mgr);
            _done = true;
        }

        private void Seed(InventoryManager mgr)
        {
            // 1) Рюкзак.
            if (equipBackpackIfEmpty && backpackDefinition != null)
            {
                var slot = mgr.GetSlot(EquipmentSlotType.Backpack);
                if (slot != null && slot.EquippedItem == null)
                    slot.EquippedItem = new ItemInstance(backpackDefinition, 1);
            }

            // 2) Запасные магазины (с предзарядкой).
            foreach (var s in spareMags)
            {
                if (s == null || s.magazineDefinition == null) continue;
                var mag = new ItemInstance(s.magazineDefinition, 1);
                if (mag.nestedContainer == null)
                    mag.nestedContainer = new GridContainer(
                        Mathf.Max(1, s.magazineDefinition.containerWidth),
                        Mathf.Max(1, s.magazineDefinition.containerHeight),
                        s.magazineDefinition.containerMaxWeight);

                if (s.caliber != null && s.preload > 0)
                {
                    var ammo = new ItemInstance
                    {
                        instanceId = Guid.NewGuid().ToString(),
                        definition = s.caliber,
                        stackCount = s.preload
                    };
                    PlaceAnywhere(mag.nestedContainer, ammo);
                }
                PlaceIntoInventory(mgr, mag);
            }

            // 3) Россыпь.
            foreach (var a in looseAmmo)
            {
                if (a == null || a.ammoDefinition == null || a.count <= 0) continue;
                var loose = new ItemInstance(a.ammoDefinition, a.count);
                PlaceIntoInventory(mgr, loose);
            }

            mgr.NotifyChanged();
        }

        private static void PlaceIntoInventory(InventoryManager mgr, ItemInstance item)
        {
            foreach (var c in mgr.GetPickupContainers())
                if (c != null && PlaceAnywhere(c, item)) return;
        }

        private static bool PlaceAnywhere(GridContainer c, ItemInstance item)
        {
            if (c == null) return false;
            for (int y = 0; y < c.height; y++)
                for (int x = 0; x < c.width; x++)
                    if (c.TryPlace(item, new Vector2Int(x, y))) return true;
            return false;
        }
    }
}
