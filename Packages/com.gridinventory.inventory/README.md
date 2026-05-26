# Grid Inventory

Универсальный гридовый инвентарь для Unity, пригодный для 2D и 3D игр.

## Что внутри

- **Core** — `GridContainer`, `ItemDefinition` (SO), `ItemInstance`, `ItemType`.
- **Equipment** — `EquipmentSlot`, `EquipmentSlotType`.
- (далее): Manager, Save/Load, World pickup, Hotbar, UI.

## Установка

Пакет уже встроен в проект как `embedded UPM package` под путём
`Packages/com.gridinventory.inventory`. Для переноса в другой проект:

1. Скопировать папку `com.gridinventory.inventory` в `Packages/` целевого проекта.
2. Открыть проект в Unity — пакет подцепится автоматически.

## Версия

0.1.0 — Core-типы, без зависимостей от игровых систем.

См. план развития в `Assets/GAME/Docs/Plugin-Plan.md` (раздел v1.0).
