# Grid Inventory

Универсальный гридовый инвентарь для Unity, пригодный для 2D и 3D игр.

## Что внутри

- **Core** — `GridContainer`, `ItemDefinition` (SO с переключателем `WorldMode` + `worldPrefab3D` / `worldPrefab2D`), `ItemInstance`, `ItemType`.
- **Equipment** — `EquipmentSlot`, `EquipmentSlotType`.
- **Config** — `InventoryConfig`, `PocketEntry`, `EquipmentSlotEntry`.
- **Hotbar** — `HotbarConfig` (с тоглом `enabled` + `slotCount`), `HotbarSlotEntry`, `HotbarSlotKind`.
- **Effects** — `ItemEffect` (abstract SO), `IItemEffectContext`, `ItemEffectContext`.
- **Persistence** — `ISaveProvider`, `FileSaveProvider`, `InventoryPersistence`.
- **World** — `WorldItemBase`, `WorldItem3D`, `WorldItem2D`, `WorldItemSpawner` (диспатчер по `WorldMode`), `HoverPickup3D`, `HoverPickup2D`.
- **IInventoryService** + `Inventory.Service` — статическая точка инъекции.
- (за пределами пакета): `InventoryManager` (мульти-инстанс с `OnActiveChanged`), UI-стек, `WeaponHotbar`, эффекты Hunger/Thirst/Health — пока в `Assets/_InventoryPlug/Scripts/`.

## Установка

Пакет уже встроен в проект как `embedded UPM package` под путём
`Packages/com.gridinventory.inventory`. Для переноса в другой проект:

1. Скопировать папку `com.gridinventory.inventory` в `Packages/` целевого проекта.
2. Открыть проект в Unity — пакет подцепится автоматически.

## Версия

0.1.0 — Core-типы, без зависимостей от игровых систем.

См. план развития в `Assets/GAME/Docs/Plugin-Plan.md` (раздел v1.0).
