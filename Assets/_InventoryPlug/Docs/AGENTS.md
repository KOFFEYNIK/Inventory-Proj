# AGENTS.md

Гид для AI-агентов (Claude Code, Codex, Cursor и т.п.), работающих в этом репозитории.
Этот файл — короткая выжимка; полная версия с примерами — в [CLAUDE.md](CLAUDE.md).

---

## TL;DR

**Unity 6 (URP 17.4)** проект, превращающий Tarkov-like гридовый инвентарь в
**переиспользуемый UPM-плагин** для 2D и 3D игр. Ядро уже выделено в
`Packages/com.gridinventory.inventory/`, миграция продолжается.

- Язык общения с пользователем — **русский**.
- Идентификаторы кода — **английские**, комментарии в коде — **русские**.
- **Все `.md`-файлы проекта живут в этой папке (`Assets/_InventoryPlug/Docs/`).**
  Единственное исключение — `Packages/com.gridinventory.inventory/README.md`: это canonical
  файл UPM-пакета, его читает Package Manager — переносить нельзя.
- Никаких новых `.md`-файлов без прямой просьбы пользователя.

---

## Структура проекта

```
Inventory Proj/
├── Assets/_InventoryPlug/
│   ├── Docs/         ← ВСЯ проектная документация (.md). Этот файл здесь.
│   ├── Editor/       ← editor-инструменты (SampleSceneBuilder, InventorySpawner, PickupConverter, ...)
│   ├── Inventory/    ← ScriptableObject-ассеты (ItemDefinition, InventoryConfig, ConsumableEffect)
│   ├── Prefabs/      ← UI / контейнеры / мировые предметы
│   ├── Scenes/       ← рабочие + Sample_*.unity
│   ├── Scripts/      ← game-side runtime (InventoryManager, InventoryUI, WeaponHotbar, Health, Samples, ...)
│   ├── Settings/     ← URP / Input
│   └── Synty/        ← сторонние ассеты
└── Packages/com.gridinventory.inventory/   ← UPM-плагин (asmdef GridInventory.Inventory.Runtime)
    ├── Runtime/  Core, Equipment, Effects, Persistence, Config, Hotbar, World, IInventoryService
    └── Tests/Editor/  GridContainerTests.cs (15/15 ✅)
```

> Историческая справка: раньше папка называлась `Assets/GAME/`. Если встретишь ссылки
> на старый путь в коде/документации — поправь на `Assets/_InventoryPlug/`.

**Граница плагина:** код пакета **не может** ссылаться на `Assembly-CSharp` (нет общего asmdef).
Связь между плагином и игрой — только через интерфейсы:
- `IInventoryService` ↔ статический `Inventory.Service`.
- `IItemEffectContext` (`ctx.Get<T>()`) — слой для game-specific эффектов.
- `ISaveProvider` ↔ `InventoryPersistence.Provider`.

---

## Где что искать

| Хочу понять… | Файл |
|---|---|
| Архитектуру плагина + game-side API | [Inventory.md](Inventory.md) |
| Полные правила работы агента + примеры | [CLAUDE.md](CLAUDE.md) |
| Что ещё нужно перенести в UPM-пакет | [Plugin-Plan.md](Plugin-Plan.md) |
| Обзор папки `Assets/_InventoryPlug/` | [README.md](README.md) |
| Описание пакета (UPM root) | [../../../Packages/com.gridinventory.inventory/README.md](../../../Packages/com.gridinventory.inventory/README.md) |

---

## Команды и инструменты (Unity Editor)

- `Tools/Inventory/Samples/Build {3D, 2D, RTS, …} Sample` — 5 готовых демо-сцен.
- `Tools/Inventory/Spawn Inventory Rig (3D|2D)` — добавить рут инвентаря в текущую сцену.
- `Tools/Inventory/Modules/Add|Remove (Hunger|Thirst|Health) Module` — подключить/отключить модуль.
- `Tools/Inventory/Make Pickup` (`Ctrl+Alt+P` для 3D / `Ctrl+Alt+Shift+P` для 2D) — конвертация в `WorldItem3D/2D`.
- `Tools/Inventory/Make Container From Selected — 3D|2D` — назначить статичный мировой ящик/сундук (`WorldContainer3D/2D`).
- `Tools/Inventory/Create Config Preset/{Tarkov|Diablo|Minecraft}-like` — создать `InventoryConfig`-ассет.
- Тесты: `Window → General → Test Runner → EditMode → Run All`
  (или MCP-команда `mcp__ai-game-developer__tests-run`).
- Перекомпиляция: `UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation()`.

---

## Архитектурные правила (короткая выжимка)

1. **Плагин не знает про игру.** Никаких `using` на game-side класс внутри пакета. Только через
   интерфейсы выше.
2. **`[MovedFrom]`** обязателен при переименовании / переносе типа между assembly, чтобы префабы
   и сцены не теряли ссылки. Для полей — `[FormerlySerializedAs]`.
3. **URP-материалы.** В коде клонируем `GraphicsSettings.currentRenderPipeline.defaultMaterial`,
   а не `new Material(Shader.Find("Universal Render Pipeline/Lit"))` (magenta).
4. **2D vs 3D разделены** на уровне типов:
   `WorldItem3D` / `WorldItem2D`, `HoverPickup3D` / `HoverPickup2D`.
   Единая точка дропа — `WorldItemSpawner.SpawnDropped(...)` — сама выбирает 2D/3D по
   `ItemDefinition.worldMode`. Конкретные префабы: `worldPrefab3D` / `worldPrefab2D` /
   `ActiveWorldPrefab` (computed).
5. **Мульти-инвентарь** (BG-style). `InventoryManager` — мульти-инстанс; активным считается
   один (`static Instance` / `Active`). Смена — `SetActive(mgr)` + событие `OnActiveChanged`.
   Per-unit (RTS): `RTSUnitInventory` создаёт data-only менеджер на юните
   (`isPlayerRig=false`, `registerAsActiveOnAwake=false`).
6. **Подключаемые модули.** Голод / жажда / здоровье — три независимых `MonoBehaviour`-системы +
   HUD. Каждая реализует `IPlayerModule`; собираются `PlayerModuleManager`-ом
   (`DefaultExecutionOrder=-95`). Удалил компонент с `InventoryRig` — отрубилась логика и UI-бар.
7. **Хотбар тогл и размер** — два уровня:
   `HotbarConfig.enabled` / `slotCount` (проектный) + `WeaponHotbar.hotbarEnabledOverride` /
   `slotCountOverride` (сценовый). Live-edit в инспекторе во время игры поддерживается.
8. **Контекстные меню редактируемые.** `ContextMenuUI` — список `entries` (Use/Inspect/Open/Drop)
   с editable label/enabled + `extraEntries` через `AddExtraEntry`. В мире — `WorldContextMenu`
   (generic) + `WorldPickupContextMenu` (привязан к `HoverPickup.OnRightClickedItem`).
9. **`DefaultExecutionOrder`:** `InventoryManager` = -100, `PlayerModuleManager` = -95,
   `WeaponHotbar` = -85.
10. **Курсор.** `InventoryUI` курсором **не управляет по умолчанию**. В FPS-сцене им рулит
    `FPSMouseLook`. В 2D/RTS/TopDown — никто.
11. **Превью предметов в UI.** Иконки в сетке/экипировке/хотбаре/ghost'е рендерит
    `ItemPreviewRenderer` из world-префаба (приоритет: `icon` → 2D-спрайт префаба → off-screen
    рендер 3D), кеш по `ItemDefinition` (`ClearCache()` при смене префаба). Не хардкодь
    `definition.icon` напрямую в UI — иди через `ItemPreviewRenderer.GetSprite(def)`.
12. **Стаки.** Тумблер `ItemDefinition.canStack` + `maxStackSize`; читай предел через computed
    `MaxStack` (не `maxStackSize` напрямую). `ItemInstance.IsStackable` требует `canStack` И
    `nestedContainer == null` — контейнеры не стакаются. Слияние везде (подбор из мира + drag-drop),
    split через меню «Разделить» + `Shift`-drag, бейдж `×N` при `stackCount > 1`.
13. **Мировые контейнеры (ящики/сундуки).** Статичные `WorldContainer3D`/`WorldContainer2D`
    (база `WorldContainerBase`, плагин `Runtime/World`) — НЕ подбираются, хранят свой
    `GridContainer` с настраиваемым числом ячеек (`gridWidth`×`gridHeight`). Камерный хват
    обобщён на интерфейс `IWorldInteractable` (его же реализует `WorldItemBase`): `HoverPickup3D/2D`
    наводится, по `F` зовёт `Interact()` (открыть), по ПКМ шлёт `OnRightClickedContainer` →
    `WorldPickupContextMenu` показывает «Открыть». Открытие идёт через
    `IInventoryService.OpenContainerWindow` → панель игрока + окно ящика рядом (Tarkov-style).
    Содержимое — **только runtime/сессия** (персиста пока нет). Назначение — утилитой
    `Tools/Inventory/Make Container From Selected — 3D|2D`.

---

## Стиль кода и поведения

- **Комментарии** — только если объясняют *почему*, а не *что*. XML-doc'и на русском —
  стандарт в плагине.
- **Не плодить файлы.** Редактируем существующие. Новые `.md` — только по явной просьбе,
  и **класть их строго в `Assets/_InventoryPlug/Docs/`**.
- **Editor-only код** — в папках `Editor/` или под `#if UNITY_EDITOR`.
- **Никаких новых внешних зависимостей** без согласования (URP уже стоит, KINEMATION удалена).
- **Серилизация:** `[FormerlySerializedAs]` для переименованных полей, `[MovedFrom]` для классов.
- **Деструктивные действия** (`git reset --hard`, `git push --force`, удаление сцен/префабов,
  массовое переименование) — только с явного согласия пользователя.

---

## Управление в сэмплах

| Клавиша | Действие |
|---|---|
| `Tab`     | Открыть/закрыть инвентарь |
| `F`       | Подобрать предмет / открыть ящик-сундук под курсором |
| `R` (during drag) | Повернуть предмет (ghost-картинка resize'ится) |
| `Shift` + drag | Разделить стак: отделить половину в новый под-стак |
| `1`..`9`,`0` | Выбрать слот хотбара |
| `H`       | Reserved hotkey (по умолчанию — нож; настраивается в `HotbarConfig`) |
| `WASD` / `AD+Space` / `ЛКМ+ПКМ` | По активному контроллеру (FPS / 2D-platformer / RTS) |
| `ПКМ` (на предмете в инвентаре) | Контекстное меню инвентаря |
| `ПКМ` (на пикапе в мире) | `WorldPickupContextMenu` («Осмотр» / «Подобрать») |
| `ПКМ` (на ящике/сундуке) | `WorldPickupContextMenu` («Открыть») |
| `ПКМ` (по земле в RTS) | `WorldContextMenu` («Идти сюда» / «Стоять» / «Снять выделение») |

---

## Тесты

`Packages/com.gridinventory.inventory/Tests/Editor/GridContainerTests.cs` — 15 EditMode-тестов,
**15/15 passing**. Покрывают placement / rotation / nested-containers / weight limit / removal.

В планах (v1.1): тесты `InventoryManager` (Save/Load round-trip), play-mode smoke-тест
`WorldItem3D → TryPickup`. См. [Plugin-Plan.md §8](Plugin-Plan.md).

---

## Чего избегать

- Прямых обращений `InventoryUI.Instance.Refresh()` из плагинного кода — всё через события
  (`OnInventoryChanged` / `OnActiveChanged` / `OnHungerChanged` / `OnThirstChanged` /
  `OnHealthChanged`).
- Хардкода числа карманов / слотов экипировки / слотов хотбара — всё лежит в `InventoryConfig`.
- `new Material(Shader.Find(...))` для URP — см. правило №3.
- `InventoryManager` как «классического» синглтона — теперь мульти-инстанс с активным
  через `SetActive`.
- Спавна `WorldItem3D` напрямую при дропе — иди через `WorldItemSpawner.SpawnDropped`,
  иначе ломаются 2D-сцены.
- Создания `.md`-файлов вне `Assets/_InventoryPlug/Docs/` (кроме UPM-`README.md` пакета).
