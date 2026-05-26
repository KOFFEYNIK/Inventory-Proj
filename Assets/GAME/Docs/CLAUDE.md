# CLAUDE.md — гид по проекту для Claude Code

Этот файл задаёт контекст и правила работы Claude Code в этом репозитории.
Если ты — Claude, читай его перед началом любой задачи.

---

## TL;DR

Это **Unity 6 (URP 17.4)** проект, превращающий существующий Tarkov-like инвентарь
в **переиспользуемый UPM-плагин** для 2D и 3D игр. Плагин уже выделен в
`Packages/com.gridinventory.inventory/`, но миграция не закончена — часть Runtime
ещё в `Assets/GAME/Scripts/Inventory/`. Полный список незакрытых пунктов — в
[Plugin-Plan.md](Plugin-Plan.md).

Язык общения с пользователем — **русский**. Технические идентификаторы — на английском.

---

## Структура проекта

```
Inventory Proj/
├── Assets/
│   └── GAME/
│       ├── Docs/              ← документация (этот файл здесь)
│       ├── Editor/            ← editor-инструменты (SampleSceneBuilder, InventorySpawner, PickupConverter)
│       ├── Inventory/         ← ScriptableObject-ассеты (ItemDefinition, InventoryConfig, ConsumableEffect)
│       ├── Prefabs/           ← UI/контейнеры/мировые предметы
│       ├── Scenes/            ← рабочие + Sample_*.unity
│       ├── Scripts/
│       │   ├── Inventory/     ← game-side runtime (InventoryManager, InventoryUI, WeaponHotbar, ...)
│       │   ├── Health/        ← HealthSystem, body-parts
│       │   ├── Samples/       ← TopDown2D/Platformer2D/RTS controllers
│       │   └── ...            ← PlayerController, FPSMouseLook, FreeLookCamera, HoverOutline
│       └── Synty/             ← сторонние ассеты
└── Packages/
    └── com.gridinventory.inventory/   ← UPM-пакет плагина
        ├── Runtime/           ← Core / Equipment / Effects / Persistence / Config / Hotbar / World
        └── Tests/Editor/      ← NUnit-тесты GridContainer
```

### Что уже в плагине

`Packages/com.gridinventory.inventory/Runtime/`:

- **Core** — `GridContainer`, `ItemInstance`, `ItemDefinition`, `ItemType`
- **Equipment** — `EquipmentSlot`, `EquipmentSlotType`
- **Effects** — `ItemEffect` (abstract SO), `IItemEffectContext`, `ItemEffectContext`
- **Persistence** — `ISaveProvider`, `FileSaveProvider`, `InventoryPersistence`
- **Config** — `InventoryConfig` (SO с `PocketEntry`, `EquipmentSlotEntry`, `HotbarConfig`)
- **Hotbar** — `HotbarSlotKind`, `HotbarConfig`, `HotbarSlotEntry`
- **World** — `WorldItemBase`, `WorldItem3D`, `WorldItem2D`, `HoverPickup3D`, `HoverPickup2D`
- **IInventoryService** + статический `Inventory.Service` — точка инъекции

### Что ещё в `Assets/GAME/Scripts/Inventory/` (не в плагине)

- `InventoryManager` — реализует `IInventoryService`, регистрирует себя в `Awake`.
- `InventoryUI` — программно строит uGUI canvas (~800 строк, **пендинг рефактор в prefab**).
- `WeaponHotbar` / `WeaponHotbarUI` / `WeaponHotbarSlotUI`.
- `IHungerTarget`, `IThirstTarget`, `IHealthTarget` — game-specific интерфейсы. Реализуются модулями
  `HungerSystem` / `ThirstSystem` / `HealthSystem` соответственно. Каждый модуль опционален — удали
  компонент с InventoryRig, и его UI/логика пропадут (см. §6 ниже).
- `ConsumableEffect : ItemEffect` — конкретный эффект (food+drink+healing).
- `ItemUseService` — фасад, строит `ItemEffectContext` и вызывает `effect.TryApply`.
- `Layout*` — кастомизация позиций UI (`InventoryLayout`, `LayoutEditMode`, `LayoutDragHandle`).

---

## Архитектурные правила

### 1. Плагин не знает про игру

Всё, что специфично для проекта (`HungerSystem`/`ThirstSystem`/`HealthSystem`, `WeaponHotbar` пока, UI Layout)
— **остаётся в `Assets/GAME/`**. Связь с плагином — только через интерфейсы:

- `IInventoryService` — `Inventory.Service` (статический accessor).
- `IItemEffectContext` — типобезопасный контейнер для эффектов (`ctx.Get<IHungerTarget>()` и т.д.).
- `ISaveProvider` — `InventoryPersistence.Provider` (default — `FileSaveProvider`).

**Никогда** не добавляй `using` на game-side класс из кода плагина.

### 2. `[MovedFrom]` для переименований

Когда переносишь класс в плагин и меняешь namespace/assembly — обязательно вешай
`[MovedFrom(true, sourceNamespace:null, sourceAssembly:"Assembly-CSharp", sourceClassName:"…")]`,
чтобы существующие префабы и сцены не теряли ссылки.

### 3. URP-материалы

Проект — на URP 17.4. При создании материалов в коде (`SampleSceneBuilder` и т.п.):

```csharp
Material baseMat = GraphicsSettings.currentRenderPipeline?.defaultMaterial;
if (baseMat == null) baseMat = renderer.sharedMaterial;
Material mat = baseMat != null ? new Material(baseMat) : new Material(Shader.Find("Standard"));
if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
if (mat.HasProperty("_Color"))     mat.SetColor("_Color", color);
```

`new Material(Shader.Find("Universal Render Pipeline/Lit"))` **не работает** —
получаешь magenta, потому что нет нужных keywords. Всегда клонируй `defaultMaterial`.

### 4. 2D vs 3D

Все мировые компоненты раздвоены: `WorldItem3D`/`WorldItem2D`, `HoverPickup3D`/`HoverPickup2D`.
Editor-инструменты (`InventorySpawner`, `PickupConverter`) имеют `Mode { TwoD, ThreeD }`.
`SampleSceneBuilder` строит 5 сэмплов:

| Меню | Что собирает |
|---|---|
| `Tools/Inventory/Samples/Build 3D Sample (Tarkov-like)`        | FPS: PlayerController + FPSMouseLook |
| `Tools/Inventory/Samples/Build 2D Sample (Diablo-like — top-down)` | Ortho-камера + TopDown2DController |
| `Tools/Inventory/Samples/Build 2D Sample (Metroid-like — platformer)` | Гравитация + Platformer2DController |
| `Tools/Inventory/Samples/Build 3D Sample (RTS — mouse-select)` | RTSCommander + 3 капсулы-юнита |
| `Tools/Inventory/Samples/Build 3D Sample (TopDown — FreeLookCamera Orbit)` | FreeLookCamera orbit |

### 5. Управление курсором

`InventoryUI` курсором **не управляет по умолчанию**. Поля:

- `manageCursor` (default `false`) — должен ли UI трогать `Cursor.lockState` / `Cursor.visible`.
- `lockCursorOnClose` (default `true`) — лочить ли курсор при `Close()` (только если `manageCursor=true`).

В FPS-сцене курсором рулит `FPSMouseLook` (по своему `manageCursor`).
В 2D/RTS/TopDown сэмплах курсор всегда видим — никто его не трогает.

### 6. Подключаемые модули (Hunger / Thirst / Health)

Голод, жажда и здоровье — **три независимых модуля**. Каждый = `MonoBehaviour`-система
+ опциональный HUD-компонент. Все живут на `InventoryRig`. Удаление компонента отключает
и логику, и UI-полоску внутри окна инвентаря.

| Модуль | Логика | HUD | Интерфейс |
|---|---|---|---|
| Голод   | `HungerSystem` | `HungerHUD` | `IHungerTarget` |
| Жажда   | `ThirstSystem` | `ThirstHUD` | `IThirstTarget` |
| Здоровье | `HealthSystem` | `HealthHUD` | `IHealthTarget` |

`InventoryUI` при `Start()` спрашивает `*.Instance != null` для каждого модуля и строит
бары только для подключённых.

#### PlayerModuleManager

Все системы реализуют `IPlayerModule` и регистрируются в `PlayerModuleManager`
(компонент на том же `InventoryRig`, DefaultExecutionOrder=-95). Менеджер:

- В `Awake` собирает все `IPlayerModule`-компоненты с GameObject через `GetComponents<>`.
- Экспонирует список `Modules` и lookup `Get<T>()` / `Has<T>()`.
- `ItemUseService.BuildContext()` идёт через `PlayerModuleManager.Instance.Get<T>()` для
  каждого target-интерфейса. Если менеджера нет (старая сцена) — fallback на
  `HungerSystem.Instance` и т.д.

Чтобы добавить **новый** player-модуль (например, выносливость):
1. `public class StaminaSystem : MonoBehaviour, IStaminaTarget, IPlayerModule { public string ModuleName => "Stamina"; }`
2. Положи на `InventoryRig` — менеджер сам подцепит.
3. Зарегистрируй интерфейс в `ItemUseService.BuildContext()`, если эффекты должны его дёргать.

Add/Remove через меню: `Tools/Inventory/Modules/Add|Remove (Hunger|Thirst|Health) Module`.
`InventorySpawner.Spawn(mode, withHunger, withThirst)` — программный API.

---

## Команды и инструменты

### Меню в Unity Editor

- `Tools/Inventory/Samples/Build … Sample (…)` — собрать любую из 5 сэмпл-сцен.
- `Tools/Inventory/Spawn Inventory Rig (3D|2D)` — добавить рут инвентаря в текущую сцену.
- `Tools/Inventory/Modules/Add|Remove (Hunger|Thirst|Health) Module` — подключить/отключить модуль на существующем `InventoryRig`.
- `Tools/Health/Spawn Health On Player` — добавить модуль здоровья на игрока.
- `Tools/Inventory/Make Pickup (Ctrl+Alt+P 3D / Ctrl+Alt+Shift+P 2D)` — конвертнуть выделенный объект в `WorldItem3D/2D` с коллайдером.
- `Tools/Inventory/Configs/Create Preset (TarkovLike|DiabloLike|MinecraftLike)` — создать `InventoryConfig`-ассет.

### Тесты

Плагин содержит NUnit-тесты в `Packages/com.gridinventory.inventory/Tests/Editor/`.
Запуск: `Window → General → Test Runner → EditMode → Run All` либо
`mcp__ai-game-developer__tests-run`.

### Перекомпиляция

Если Unity не подхватил изменения скриптов:
```csharp
UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
```

---

## Управление

| Клавиша | Действие |
|---|---|
| `Tab`     | Открыть/закрыть инвентарь |
| `F`       | Подобрать предмет под курсором / прицелом |
| `1`..`9`,`0` | Выбрать слот хотбара |
| `H`       | Нож (reserved hotkey, настраивается в `HotbarConfig`) |
| WASD / AD+Space / ЛКМ+ПКМ | По контроллеру (FPS / 2D-platformer / RTS) |

---

## Стиль кода и поведения

- **Язык общения:** русский. Технические термины и идентификаторы — английские.
- **Комментарии:** только если объясняют *почему*, а не *что*. Уже сделанная очистка
  XML-docs на русском в плагине — стандарт.
- **Не плодить файлы:** редактируем существующие, новые `.md`-файлы — только по явной
  просьбе пользователя.
- **Editor-only код** — только в папках `Editor/` или под `#if UNITY_EDITOR`.
- **Никаких новых зависимостей** без согласования (URP уже стоит, KINEMATION удалена).
- **Серилизация:** для рефакторов с переименованием — обязателен `[MovedFrom]`.

---

## Где смотреть дальше

- [Inventory.md](Inventory.md) — детальное описание системы инвентаря и её API.
- [Plugin-Plan.md](Plugin-Plan.md) — чеклист v1.0 и план миграции в UPM.
- [README.md](README.md) — короткий обзор папки `GAME/`.
