using UnityEngine;

namespace InventoryPlug.Integration.PBS2D
{
    /// <summary>
    /// Проектная связка «оружие → калибр». Вешается на оружейный префаб PBS_2D
    /// (тот, что назначен в <c>ItemDefinition.weaponPrefab</c>). Мост
    /// <see cref="PBS2D_InventoryBridge"/> читает <see cref="ammoDefinition"/>
    /// после спавна и синхронизирует <c>Gun.CurrentReserveAmmo</c> с количеством
    /// патронов этого калибра в инвентаре.
    ///
    /// <para>Намеренно не трогает код PBS_2D и UPM-пакета — только хранит ссылку
    /// на <see cref="ItemDefinition"/> патрона.</para>
    /// </summary>
    public class InventoryWeaponBinding : MonoBehaviour
    {
        [Tooltip("ItemDefinition патрона (калибра) для этого ствола. " +
                 "Резерв оружия = сумма stackCount всех предметов с этим определением " +
                 "в карманах/разгрузке/рюкзаке. Если не задано — резерв остаётся " +
                 "внутренним числом Gun (как в чистом PBS_2D).")]
        public ItemDefinition ammoDefinition;
    }
}
