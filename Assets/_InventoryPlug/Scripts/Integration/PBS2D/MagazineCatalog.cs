using System;
using System.Collections.Generic;
using UnityEngine;

namespace InventoryPlug.Integration.PBS2D
{
    /// <summary>
    /// Каталог магазинов: привязывает мета-данные (калибр, ёмкость, скорость набивки) к
    /// предмету-магазину, НЕ трогая <see cref="ItemDefinition"/> пакета. Тот же приём, что
    /// <see cref="InventoryWeaponBinding"/> для оружия. Один ассет на проект, ссылка — на мосте
    /// (<see cref="PBS2D_InventoryBridge.magazineCatalog"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "MagazineCatalog", menuName = "Inventory/PBS2D/Magazine Catalog")]
    public class MagazineCatalog : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            [Tooltip("Предмет-магазин (ItemDefinition, isContainer = true).")]
            public ItemDefinition magazineDefinition;

            [Tooltip("Калибр патрона, который принимает магазин.")]
            public ItemDefinition caliber;

            [Tooltip("Ёмкость магазина (макс. патронов).")]
            public int capacity = 30;

            [Tooltip("Скорость набивки: секунд на один патрон.")]
            public float loadSecondsPerRound = 0.5f;
        }

        public List<Entry> entries = new();

        /// <summary>Запись по предмету-магазину (или null, если это не магазин из каталога).</summary>
        public Entry GetByMagazine(ItemDefinition magDef)
        {
            if (magDef == null) return null;
            foreach (var e in entries)
                if (e != null && e.magazineDefinition == magDef) return e;
            return null;
        }

        /// <summary>Первый магазин под калибр — для стартового снаряжённого магазина.</summary>
        public Entry GetDefaultForCaliber(ItemDefinition caliber)
        {
            if (caliber == null) return null;
            foreach (var e in entries)
                if (e != null && e.caliber == caliber) return e;
            return null;
        }

        public bool IsMagazine(ItemDefinition def) => GetByMagazine(def) != null;
    }
}
