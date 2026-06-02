# CLAUDE.md — гид по проекту для Claude Code

Этот файл задаёт контекст и правила работы Claude Code в этом репозитории.
Если ты — Claude, читай его перед началом любой задачи.

---

## TL;DR

Это **Unity 6 (URP 17.4)** проект, превращающий существующий Tarkov-like инвентарь
в **переиспользуемый UPM-плагин** для 2D и 3D игр. Плагин уже выделен в
`Packages/com.gridinventory.inventory/`, но миграция не закончена — часть Runtime
ещё в `Assets/_InventoryPlug/Scripts/Inventory/`. Полный список незакрытых пунктов — в
[Plugin-Plan.md](Plugin-Plan.md).

### Язык общения

- **Все ответы Claude (объяснения, summary, чек-листы, вопросы)** — **на русском**.
- **Идентификаторы кода** (классы, методы, поля, пути, имена ассетов) — английские.
- **Комментарии в коде** — русские (так уже было заведено в проекте).
- Это правило также продублировано в memory (`feedback_russian_responses.md`).

### Расположение `.md`-файлов

- **Вся проектная документация живёт в `Assets/_InventoryPlug/Docs/`**. Новые `.md`-файлы
  создаём только здесь — никаких файлов в корне репозитория, в `Assets/`, в `Scripts/` и т.д.
- Единственное исключение — `Packages/com.gridinventory.inventory/README.md`: это canonical
  файл UPM-пакета, его читает Unity Package Manager. Переносить нельзя.
- Это правило продублировано в memory (`feedback_md_location.md`).

---

## Структура проекта

```
Inventory Proj/
├── Assets/
│   └── _InventoryPlug/
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
- **World** — `WorldItemBase`, `WorldItem3D`, `WorldItem2D`, `HoverPickup3D`, `HoverPickup2D`,
  `IWorldInteractable`, `WorldContainerBase`, `WorldContainer3D`, `WorldContainer2D`
- **IInventoryService** + статический `Inventory.Service` — точка инъекции

### Что ещё в `Assets/_InventoryPlug/Scripts/Inventory/` (не в плагине)

- `InventoryManager` — реализует `IInventoryService`. **Мульти-инстанс**: активный держится в
  `static Instance`, смена — `SetActive(mgr)` + событие `OnActiveChanged`. См. §4.6.
- `InventoryUI` — программно строит uGUI canvas (~800 строк, **пендинг рефактор в prefab**).
  Подписан на `OnActiveChanged` и ребилдится на смене активного менеджера.
- `WeaponHotbar` / `WeaponHotbarUI` / `WeaponHotbarSlotUI` — см. §4.7 (тогл + редактируемый размер).
- `ItemPreviewRenderer` (static) — рендерит world-префаб предмета (3D-меш off-screen или 2D-спрайт
  из `SpriteRenderer`'а) в кешированный спрайт-превью для UI; заменяет статичную `icon` в сетке,
  экипировке, хотбаре и ghost'е перетаскивания. См. §4.9.
- `UIResizer` — угловая ручка ресайза окна инвентаря (только в `LayoutEditMode`).
- `IHungerTarget`, `IThirstTarget`, `IHealthTarget` — game-specific интерфейсы. Реализуются модулями
  `HungerSystem` / `ThirstSystem` / `HealthSystem` соответственно. Каждый модуль опционален — удали
  компонент с InventoryRig, и его UI/логика пропадут (см. §6 ниже).
- `PlayerModuleManager` — собирает `IPlayerModule`-компоненты в `Awake` (DefaultExecutionOrder=-95);
  `ItemUseService.BuildContext()` ходит через него.
- `ConsumableEffect : ItemEffect` — конкретный эффект (food+drink+healing).
- `ItemUseService` — фасад, строит `ItemEffectContext` и вызывает `effect.TryApply`.
- `Layout*` — кастомизация позиций UI (`InventoryLayout`, `LayoutEditMode`, `LayoutDragHandle`).
- `Samples/RTSUnitInventory.cs` — per-unit инвентарь для тактических RPG / RTS (см. §4.6).
- `UI/WorldContextMenu.cs` + `UI/WorldPickupContextMenu.cs` — generic ПКМ-меню в мире (см. §4.5).

---

## Архитектурные правила

### 1. Плагин не знает про игру

Всё, что специфично для проекта (`HungerSystem`/`ThirstSystem`/`HealthSystem`, `WeaponHotbar` пока, UI Layout)
— **остаётся в `Assets/_InventoryPlug/`**. Связь с плагином — только через интерфейсы:

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

### 4.5. WorldContextMenu (универсальное ПКМ-меню в мире)

[WorldContextMenu.cs](../Scripts/UI/WorldContextMenu.cs) — generic экранное меню для
мира (RTS/TopDown/любая 2D-3D сцена). По стилю — клон inventory `ContextMenuUI`, но не
привязан к `ItemInstance`: принимает произвольный `(label, action)`-список через
`Show(screenPos, params (string, Action)[])`. Авто-закрытие на повторную ПКМ или Escape.
Создаётся лениво через `WorldContextMenu.Ensure()`.

**Используется в двух местах:**

1. **`RTSCommander`** — флаг `useContextMenu` (default `true`). ПКМ по земле открывает
   меню `Идти сюда / Стоять / Снять выделение` вместо немедленного `MoveTo`. Кроме того
   RMB пропускается, если курсор наведён на `WorldItemBase` (`HoverPickup3D/2D.CurrentItem`
   != null или raycast попал в пикап) — тогда меню открывает `WorldPickupContextMenu`.

2. **`WorldPickupContextMenu`** ([Scripts/UI/WorldPickupContextMenu.cs](../Scripts/UI/WorldPickupContextMenu.cs))
   — игровая прокладка между `HoverPickup3D`/`HoverPickup2D` (живут в UPM-пакете) и
   `WorldContextMenu`. Вешается на ту же камеру; подписывается на `OnRightClickedItem`,
   открывает меню с двумя редактируемыми пунктами:

   | Поле | Default | Назначение |
   |---|---|---|
   | `useContextMenu` | `true` | Глобальный тогл для меню над пикапами |
   | `showInspect` / `labelInspect` | `true` / `"Осмотр"` | Открыть `InspectWindowUI` для предмета |
   | `showPickup`  / `labelPickup`  | `true` / `"Подобрать"` | Вызвать `WorldItemBase.TryPickup()` |

   Дополнительные пункты можно добавить из кода через `AddExtraEntry(label, handler)` —
   handler получает наведённый `WorldItemBase`. Пока меню открыто, у HoverPickup
   выставляется `SuppressHoverUpdates = true`, чтобы цель не «съезжала» под курсором.

   Компонент **автоматически добавляется** через `InventorySpawner.Spawn()`. Для
   старых сцен — переспауни инвентарь.

**Inventory `ContextMenuUI`** — теперь полностью редактируемый:

- В инспекторе компонента есть список `entries` (built-in кнопки: Use / Inspect / Open /
  Split / Drop) с полями `label` и `enabled` для каждой. `Split` («Разделить») показывается
  только для стака >1 в сетке; `Awake` досоздаёт пункт в старых сериализованных списках.
- Список `extraEntries` — для проектных пунктов. Колбэк назначается из кода через
  `ContextMenuUI.Instance.AddExtraEntry(label, item => …)`.
- Кнопки пересобираются на каждый `Show()` (поэтому `Open` корректно скрывается для
  неконтейнеров).

### 4.6. Мульти-инвентарь (тактические RPG / RTS, стиль Baldur's Gate)

В одной сцене может жить **несколько `InventoryManager`-ов одновременно**. UI/хотбар/подбор
работают с *активным* — он один в каждый момент времени.

**Ключевое в `InventoryManager`:**

| Поле / API | Смысл |
|---|---|
| `static Instance` / `static Active` | Текущий активный менеджер. |
| `static event OnActiveChanged` | Срабатывает при свапе (UI, хотбар на нём держатся). |
| `static SetActive(mgr)` | Свап. Обновляет `Instance` + `Inventory.Service`. |
| `registerAsActiveOnAwake` (default `true`) | Первый Awake-нувшийся менеджер становится активным. Для per-unit data-only — выключай. |
| `isPlayerRig` (default `true`) | true → `transform.SetParent(null)` + `DontDestroyOnLoad`. Per-unit менеджеры — `false`. |
| `inventoryName` | Лейбл для дебага. |

**`RTSUnitInventory`** ([Scripts/Samples/RTSUnitInventory.cs](../Scripts/Samples/RTSUnitInventory.cs))
— компонент на юните. В `Awake` создаёт **дочерний** GO `UnitInventoryData` с
`InventoryManager` (`isPlayerRig=false`, `registerAsActiveOnAwake=false`). Конфиг (`InventoryConfig`)
можно переопределить per-unit — Tank/Scout/Engineer могут иметь разные слоты.

**`RTSCommander.Select(unit)`** теперь вызывает `unit.GetComponent<RTSUnitInventory>().Activate()`.
Это меняет активный менеджер → `WorldItemBase.TryPickup` (через `Inventory.Service`)
складывает предмет в инвентарь выбранного. У `RTSCommander` есть поле `autoSelectUnit`
для пред-выбора при старте сцены.

**Сэмпл `Build 3D Sample (RTS — mouse-select)`** теперь создаёт 3 юнита (`Hero`, `Scout`,
`Engineer`), каждый со своим `RTSUnitInventory`. Hero — основной (на нём `InventoryRig`
с UI/модулями), выбран автоматически.

**На что подписываться при расширении:**
- UI/хотбар: `InventoryManager.OnActiveChanged` + ребилд (см. `InventoryUI.HandleActiveChanged`).
- Per-unit модули (hunger/health на каждого) — пока не реализованы, vitals глобальные.

### 4.7. Хотбар (быстрый доступ 1-0)

`WeaponHotbar` ([Scripts/Inventory/WeaponHotbar.cs](../Scripts/Inventory/WeaponHotbar.cs)) +
`WeaponHotbarUI` — панель внизу экрана и цифровые клавиши 1..9, 0.

**Тогл + размер настраиваются с двух мест:**

| Уровень | Поле | Где | Описание |
|---|---|---|---|
| Проект | `HotbarConfig.enabled` | `InventoryConfig.hotbar` | Дефолтный тогл (true) |
| Проект | `HotbarConfig.slotCount` | `InventoryConfig.hotbar` | 0 = размер из `slots.Count`; >0 = принудительно столько |
| Сцена  | `WeaponHotbar.hotbarEnabledOverride` | компонент на InventoryRig | Сценовый override (galка) |
| Сцена  | `WeaponHotbar.slotCountOverride` | компонент на InventoryRig | 0 = из конфига; >0 = forced |

**Логика:** `IsHotbarEnabled = hotbarEnabledOverride && HotbarConfig.enabled`. Если выключен —
цифровые клавиши не активны и панель внизу экрана спрятана.

**Live-edit:** правка `hotbarEnabledOverride` / `slotCountOverride` прямо в инспекторе во
время игры — `WeaponHotbar.LateUpdate` (`#if UNITY_EDITOR`) ловит изменения и
перестраивает хотбар + UI (`OnEnabledChanged`, `OnSlotsChanged`).

**Runtime API:**
- `WeaponHotbar.Instance.SetHotbarEnabled(bool)`
- `WeaponHotbar.Instance.SetSlotCount(int)`

Перебрасывает `OnSlotsChanged` → `WeaponHotbarUI.HandleSlotsChanged` пересобирает UI
с нуля, если число слотов изменилось.

### 4.8. 2D / 3D представление предмета в мире (`WorldMode`)

Каждый `ItemDefinition` имеет переключатель — **2D или 3D**. Это влияет и на дроп из
инвентаря, и на префаб, который инстанциируется в мире / в окне осмотра.

**Поля в `ItemDefinition`:**

| Поле | Назначение |
|---|---|
| `worldMode` (enum `WorldMode { ThreeD, TwoD }`, default `ThreeD`) | Режим существования предмета |
| `worldPrefab3D` | Префаб для 3D-мира. Мешевый + Renderer; Rigidbody добавится при дропе. (Старое имя `worldPrefab` мигрирует через `[FormerlySerializedAs]`.) |
| `worldPrefab2D` | Префаб для 2D-мира. SpriteRenderer + Collider2D; Rigidbody2D добавится при дропе. |
| `ActiveWorldPrefab` | Computed: возвращает 2D- или 3D-префаб в зависимости от `worldMode`. |

**Спавн в мире:**

- Внутренний `WorldItem3D.Spawn` читает `worldPrefab3D`.
- Внутренний `WorldItem2D.Spawn` читает `worldPrefab2D`.
- Универсальная точка для дропа — `WorldItemSpawner.SpawnDropped(instance, position, velocity)`
  — сама смотрит на `worldMode` и зовёт нужный конкретный спавнер. В 2D-режиме отбрасывает Z.

**Дроп из инвентаря (`ContextMenuUI.DropItemFromPlayer`)** теперь идёт через диспатчер:
для 3D-предмета — origin от капсулы игрока + forward камеры; для 2D — позиция игрока +
направление по `transform.localScale.x` (зеркало спрайта).

**Раньше был баг:** дроп всегда спавнил `WorldItem3D`, поэтому в 2D-сцене брошенный
предмет внезапно становился 3D-моделью. Теперь — корректно по `worldMode`.

### 4.9. Превью предметов в UI (`ItemPreviewRenderer`)

[ItemPreviewRenderer.cs](../Scripts/Inventory/UI/ItemPreviewRenderer.cs) — статический рендерер,
который превращает world-префаб предмета в спрайт для UI. Раньше в слотах рисовалась статичная
`ItemDefinition.icon`; теперь по умолчанию показывается **сам префаб**.

**Приоритет источника спрайта** (`GetSprite(def)`):
1. Назначенная `ItemDefinition.icon` (если есть).
2. Для `WorldMode.TwoD` — спрайт **напрямую** из `SpriteRenderer`'а префаба
   (`ActiveWorldPrefab` → `worldPrefab2D` → `worldPrefab3D`) — без off-screen рендера.
3. Иначе (3D или у 2D не нашлось спрайта) — off-screen рендер: изолированный camera-rig на
   `y=-1000`, 3 Point-light'а, инстанс префаба без физики/коллайдеров, кадрирование по bounds,
   один `Render()` в `RenderTexture` → `ReadPixels` → `Sprite`.

**Кеш:** один спрайт на `ItemDefinition`, считается лениво при первом запросе. Поменял
префаб/иконку в рантайме — зови `ItemPreviewRenderer.ClearCache()`.

**Где используется:** `GridUI` (предмет в сетке), `EquipmentSlotUI`, `WeaponHotbarSlotUI`,
`DragDropController` (ghost). Окно осмотра (`InspectWindowUI`) рендерит префаб **отдельно**
живой камерой (вращение/зум), кеш превью не использует.

> Тот же принцип изоляции (camera-rig на `y=-1000`, Point-light'ы, без Directional) применён,
> чтобы рендер не засветил основную сцену и URP не выбрал inspect-свет как main light.

### 4.10. Система стаков предметов

Стакирование настраивается на `ItemDefinition` и выполняется на `ItemInstance` / `GridContainer`.

**Настройка (`ItemDefinition`):**
- `canStack` (bool, default `false`) — главный тумблер. Выключен → каждый предмет занимает
  отдельную ячейку, `maxStackSize` игнорируется.
- `maxStackSize` (int) — предел стака; работает только при `canStack = true`.
- `MaxStack` (computed) — `maxStackSize` при `canStack`, иначе `1`. **Всегда** читай через
  `MaxStack`, а не напрямую `maxStackSize`.

**Рантайм (`ItemInstance`):**
- `IsStackable` = `definition.canStack && nestedContainer == null` — **контейнеры
  (рюкзаки/кейсы) никогда не стакаются**, каждый уникален.
- `FreeStackSpace`, `CanStackWith(other)` (оба стакаемы + один `definition`),
  `MergeFrom(other)` — переливает максимум единиц, мутирует `stackCount` обоих, возвращает
  число перенесённых.
- `GridContainer.TryStackInto(item)` — доливает `item` в уже лежащие стаки того же типа;
  `true`, если стак поглощён полностью.

**Слияние — везде:**
- **Подбор из мира** (`WorldItemBase.TryPickup`): для стакаемого предмета сначала пытается
  долить в существующие стаки (`TryStackInto`) — и только потом экипировка / свободное место.
- **Drag-drop** (`DragDropController`): дроп на стак того же типа под курсором → `MergeFrom`;
  остаток (переполнение) возвращается на origin.
- **Вложение в контейнер (Tarkov-style):** дроп предмета поверх предмета-контейнера
  (`nestedContainer != null`) в сетке кладёт его **внутрь** (`TryDropIntoContainer`: стак того же
  типа → первая свободная ячейка). Футпринт контейнера подсвечивается по
  `GridContainer.CanAcceptSomewhere`. Над контейнером раскладка по внешней сетке не выполняется —
  не влезло, предмет вернётся на origin. Самовложение блокирует `CanPlace` (`IsContainedWithin`).

**Split (разделение):**
- **Контекстное меню «Разделить»** — пункт `Split`, виден для стака >1 в сетке; отделяет
  половину (округление вверх) в свободную ячейку того же контейнера.
- **`Shift` + drag** — начать перетаскивание с зажатым `Shift` со стака >1 → тащится новый
  под-стак (половина); источник остаётся на месте. Неудачный дроп вливает под-стак обратно.

**Отображение:** бейдж `×N` в правом-нижнем углу ячейки при `stackCount > 1` (на корне-футпринте,
не вращается с иконкой). Окно осмотра показывает `Стак: N/MaxStack`. Подсказка пикапа в мире —
`×N`.

> ⚠️ Merge'ы **не проверяют** `maxWeight` контейнера (v1-упрощение; контейнеры обычно
> `maxWeight = 0` = без лимита). `stackCount` уже сериализуется в Save/Load — отдельных правок
> персистентности не потребовалось.

### 4.11. Мировые контейнеры (ящики / сундуки)

Статичный мировой контейнер, в который можно класть и забирать предметы (как тарковский ящик).
В отличие от пикапа — **не подбирается**, стоит в мире, хранит свой `GridContainer`.

**Плагин (`Runtime/World`):**
- `IWorldInteractable` — общий контракт камерного хвата: `Interact()` + `SetPromptVisible(bool)`.
  Реализуют **и** `WorldItemBase` (Interact = подбор), **и** `WorldContainerBase` (Interact = открыть).
- `WorldContainerBase` (abstract) — поля `displayTitle`, `gridWidth`, `gridHeight`, `maxWeight`,
  `openKey`. Лениво создаёт рантайм-`GridContainer` (id `WorldContainer_{InstanceID}`), строит
  world-space подсказку «[F] Открыть\n{Title}», регистрирует контейнер в `Inventory.Service`.
  `Open()` → `Inventory.Service.OpenContainerWindow(Container, Title)`.
- `WorldContainer3D` (требует `Collider`, билборд к `Camera.main`) / `WorldContainer2D`
  (требует `Collider2D`, без билборда).

**Камерный хват:** `HoverPickup3D/2D` обобщены на `IWorldInteractable`. Рейкаст/оверлап сначала
ищет `WorldItem*`, затем `WorldContainerBase`. `CurrentItem` (тип `WorldItem*`) и
`OnRightClickedItem` сохранены 1-в-1 — подбор и `WorldPickupContextMenu` для пикапов не затронуты.
Добавлено событие `OnRightClickedContainer`. Авто-подбор `pickupOnHover` работает только для
предметов (ящик так не открываем — был бы спам).

**Открытие (Tarkov-style):** `IInventoryService.OpenContainerWindow(container, title)` →
`InventoryManager` раскрывает панель игрока (`InventoryUI.Open()` если закрыта) и поверх показывает
окно ящика (`InventoryUI.OpenNestedContainer`) — чтобы таскать предметы между ними. ПКМ по ящику →
`WorldPickupContextMenu.OpenContainerMenu` (пункт «Открыть», поля `showOpen`/`labelOpen`).

**Позиция окна ящика** настраивается на `InventoryUI` (Header «Окно ящика/контейнера»):
`nestedWindowAnchor` (нормализованная точка холста для центра окна, дефолт ≈ `(0.7, 0.62)` —
правее-выше центра), `nestedWindowOffset` (px-смещение), `nestedWindowRandomize` +
`nestedWindowRandomSpread` (опц. случайный разброс, чтобы окна не ложились стопкой). Окно всё равно
перетаскивается мышью (`UIDragger`) — это лишь стартовая позиция.

**Назначение в дизайн-тайме:** `WorldContainerConverter`
([Editor/WorldContainerConverter.cs](../Editor/WorldContainerConverter.cs)) — меню
`Tools/Inventory/Make Container From Selected — 3D|2D`: добавляет коллайдер (не-триггер, auto-fit) +
`WorldContainer3D/2D`. **Rigidbody НЕ добавляет** — ящик статичен. Размер сетки/заголовок/лимит
веса правятся на компоненте в инспекторе.

> ⚠️ Содержимое — **только runtime/сессия**: при перезагрузке сцены ящик пересоздаётся пустым.
> Персист (save/load мировых контейнеров через `ISaveProvider`) — отдельная будущая задача.

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
- `Tools/Inventory/Make Container From Selected — 3D|2D` — назначить выделенный объект статичным ящиком (`WorldContainer3D/2D`, без Rigidbody).
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
| `F`       | Подобрать предмет / открыть ящик-сундук под курсором |
| `ПКМ` (на ящике в мире) | `WorldPickupContextMenu` — пункт «Открыть» |
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
- [README.md](README.md) — короткий обзор папки `_InventoryPlug/`.
