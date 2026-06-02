# _InventoryPlug — Документация

Эта папка содержит проектную документацию по содержимому `Assets/_InventoryPlug`.

## Структура папки `_InventoryPlug/`

| Папка | Что внутри |
|---|---|
| `Anim Assets/` | Анимации персонажа |
| `Character/` | Префабы / меши персонажа |
| `Editor/` | Editor-скрипты (спавнеры, генераторы префабов, конвертеры пикапов) |
| `FlatIcons/` | Иконки предметов |
| `Inventory/` | Asset-данные: `ItemDefinition`-SO, `ConsumableEffect`-SO, `InventoryLayout`-SO |
| `Prefabs/` | Префабы сцены (UI, контейнеры, мировые предметы) |
| `Scenes/` | Игровые сцены |
| `Scripts/` | Runtime-логика (см. `Scripts/Inventory/`, `Scripts/Health/`, `Scripts/UI/`) |
| `Settings/` | URP / Input настройки |
| `Shaders/` | Кастомные шейдеры |
| `Synty/` | Сторонние ассеты Synty |
| `Docs/` | **Эта папка** — `.md`-документация |

## Документы

- [AGENTS.md](AGENTS.md) — компактный гид для AI-агентов (Codex / Cursor / Claude Code).
- [CLAUDE.md](CLAUDE.md) — расширенный гид по проекту для Claude Code: структура, архитектурные правила, команды.
- [Inventory.md](Inventory.md) — архитектура и API системы инвентаря.
- [Plugin-Plan.md](Plugin-Plan.md) — план превращения текущего инвентаря в переиспользуемый плагин для 2D/3D игр.

> Все проектные `.md`-файлы хранятся здесь, в `Assets/_InventoryPlug/Docs/`.
> Единственное исключение — `Packages/com.gridinventory.inventory/README.md` (UPM-canonical).

## Плагин

С версии `0.1.0` Core-типы (`GridContainer`, `ItemInstance`, `ItemDefinition`, `ItemType`, `EquipmentSlot`/`EquipmentSlotType`) живут в UPM-пакете:

```
Packages/com.gridinventory.inventory/
├── package.json
├── README.md
├── Runtime/
│   ├── GridInventory.Inventory.Runtime.asmdef
│   ├── Core/        # GridContainer, ItemInstance, ItemDefinition, ItemType
│   ├── Equipment/   # EquipmentSlot, EquipmentSlotType
│   ├── Effects/     # ItemEffect (abstract SO), IItemEffectContext, ItemEffectContext
│   ├── Persistence/ # ISaveProvider, FileSaveProvider, InventoryPersistence
│   ├── Config/      # InventoryConfig (SO), PocketEntry, EquipmentSlotEntry
│   ├── Hotbar/      # HotbarSlotKind, HotbarConfig, HotbarSlotEntry
│   ├── World/       # WorldItemBase, WorldItem3D, WorldItem2D, HoverPickup3D, HoverPickup2D
│   ├── IInventoryService.cs
│   └── Inventory.cs # static accessor для IInventoryService
└── Editor/
    └── GridInventory.Inventory.Editor.asmdef
```

Game-side абстракции (живут в `Assets/_InventoryPlug/Scripts/Inventory/` и `Scripts/`):

- `IHungerTarget`, `IThirstTarget`, `IHealthTarget` — игровые интерфейсы для модулей голода, жажды и здоровья. Реализуются `HungerSystem` / `ThirstSystem` / `HealthSystem` соответственно — каждый опциональный (удалил компонент → нет ни логики, ни UI).
- `IPlayerModule` + `PlayerModuleManager` — сборщик опциональных модулей на `InventoryRig` (DefaultExecutionOrder=-95). `ItemUseService.BuildContext()` ходит через него.
- `ConsumableEffect : ItemEffect` — конкретная реализация: hunger/thirst + body-parts healing.
- `ItemUseService` — фасад, строит контекст и вызывает `effect.TryApply`.
- `WorldContextMenu` + `WorldPickupContextMenu` — generic ПКМ-меню в мире (RTS/2D/3D) и его прокладка к `HoverPickup` из пакета.
- `RTSUnitInventory` — per-unit инвентарь для тактических RPG / RTS (стиль Baldur's Gate).

Остальные модули (`InventoryManager`, `InventoryUI`, `WeaponHotbar`, Save/Load, Layout) пока остаются в `Assets/_InventoryPlug/Scripts/Inventory` — будут перенесены в плагин в следующих итерациях ([Plugin-Plan.md](Plugin-Plan.md)).

## Ключевые сцены и точки входа

- `InventoryManager` — мульти-инстанс, активным считается один (`Instance` / `Active`); смена активного — `SetActive` + `OnActiveChanged`. DefaultExecutionOrder = -100.
- `InventoryUI` — UI-окно, открывается по `Tab`. Ребилдится на `OnActiveChanged`.
- `WeaponHotbar` — хотбар (1..9, 0; `H` — нож). Тогл и число слотов настраиваются (`HotbarConfig.enabled` / `slotCount` или сценовые override-поля).
- `WorldItem3D` / `WorldItem2D` — мировые предметы. Подбирается через `HoverPickup3D` / `HoverPickup2D` (`F`). Дроп — через `WorldItemSpawner.SpawnDropped` (диспатчер по `ItemDefinition.worldMode`).
