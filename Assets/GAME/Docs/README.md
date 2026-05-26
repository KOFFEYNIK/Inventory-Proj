# GAME — Документация

Эта папка содержит проектную документацию по содержимому `Assets/GAME`.

## Структура папки `GAME/`

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

- [CLAUDE.md](CLAUDE.md) — гид по проекту для Claude Code: структура, архитектурные правила, команды.
- [Inventory.md](Inventory.md) — архитектура и API системы инвентаря.
- [Plugin-Plan.md](Plugin-Plan.md) — план превращения текущего инвентаря в переиспользуемый плагин для 2D/3D игр.

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

Game-side абстракции (живут в `Assets/GAME/Scripts/Inventory/`):

- `IHungerTarget`, `IThirstTarget`, `IHealthTarget` — игровые интерфейсы для модулей голода, жажды и здоровья. Реализуются `HungerSystem` / `ThirstSystem` / `HealthSystem` соответственно — каждый опциональный (удалил компонент → нет ни логики, ни UI).
- `ConsumableEffect : ItemEffect` — конкретная реализация: hunger/thirst + body-parts healing.
- `ItemUseService` — фасад, строит контекст и вызывает `effect.TryApply`.

Остальные модули (`InventoryManager`, `InventoryUI`, `WeaponHotbar`, `WorldItem`, Save/Load, Layout) пока остаются в `Assets/GAME/Scripts/Inventory` — будут перенесены в плагин в следующих итерациях ([Plugin-Plan.md](Plugin-Plan.md)).

## Ключевые сцены и точки входа

- `InventoryManager` — корневой singleton (DefaultExecutionOrder = -100).
- `InventoryUI` — UI-окно, открывается по `Tab`.
- `WeaponHotbar` — 10-слотовый хотбар (1..9, 0; `H` — нож).
- `WorldItem` — компонент на мировых предметах, подбирается через `HoverPickup` на камере (`F`).
