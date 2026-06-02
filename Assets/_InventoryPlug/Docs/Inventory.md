# Inventory System

Гридовый инвентарь для 2D/3D игр (исторически — в духе Tarkov):
- предметы занимают **W×H ячеек** в сетке, могут вращаться;
- контейнеры вкладываются друг в друга (рюкзак внутри рюкзака — запрещён циклический случай);
- произвольное число карманов и экипировочных слотов — задаётся через `InventoryConfig` SO;
- хотбар с конфигурируемыми слотами (`Reserved` / `EquipmentWeapon` / `UserItem`);
- эффекты предметов через абстракцию `ItemEffect` (плагин не знает про конкретные игровые системы);
- сохранение через подменяемый `ISaveProvider`.

С версии **0.1.0** ядро живёт в UPM-пакете `Packages/com.gridinventory.inventory`.
Конкретная игровая обвязка — в `Assets/_InventoryPlug/Scripts/`.

См. [Plugin-Plan.md](Plugin-Plan.md) — план развития в переиспользуемый пакет.

---

## 1. Asset-данные (`Assets/_InventoryPlug/Inventory/`)

| Файл / Папка | Тип | Описание |
|---|---|---|
| `Items/*.asset` | `ItemDefinition` | Определения предметов — оружие, броня, рюкзаки, расходники, патроны. |
| `Consumables/*.asset` | `ConsumableEffect` | Эффекты использования (Бинт, Аптечка, Еда, Вода). |
| `Configs/*.asset` | `InventoryConfig` | Пресеты инвентаря (Tarkov / Diablo / Minecraft-like). Генерируются через `Tools/Inventory/Create Config Preset/…`. |
| `DefaultInventoryLayout.asset` | `InventoryLayout` | SO-конфиг расположения панелей UI окна инвентаря. |
| `DefaultHealthConfig.asset` | `HealthConfig` | Базовый конфиг HP конечностей (используется системой здоровья). |

---

## 2. Plugin core (`Packages/com.gridinventory.inventory/Runtime/`)

### Модель данных (`Runtime/Core/`)

**`ItemDefinition`** — `ScriptableObject`, неизменяемый шаблон предмета.
- `itemId`, `displayName`, `description`, `icon`
- `itemType` (`ItemType` enum: `Generic`, `Backpack`, `Rig`, `SecureCase`, `BodyArmor`, `Helmet`, `FaceCover`, `PrimaryWeapon`, `SecondaryWeapon`, `Sidearm`, `SurgicalKit`, `Medkit`)
- Грид: `width`, `height`, `canRotate`
- Стек: `canStack` (вкл/выкл стакирование) + `maxStackSize`; computed `MaxStack` (= `maxStackSize` при `canStack`, иначе `1`)
- Вес: `weightPerUnit`
- Контейнер: `isContainer`, `containerWidth`, `containerHeight`, `containerMaxWeight` (`0` = без лимита)
- **World presentation (2D / 3D):**
  - `worldMode` — enum `WorldMode { ThreeD, TwoD }`. Управляет тем, какой префаб берётся при дропе и в окне осмотра.
  - `worldPrefab3D` — префаб для 3D-мира (старое поле `worldPrefab` мигрирует через `[FormerlySerializedAs]`).
  - `worldPrefab2D` — префаб для 2D-мира (SpriteRenderer + Collider2D).
  - `ActiveWorldPrefab` — computed property, отдаёт 2D- или 3D-префаб по `worldMode`.
- `weaponPrefab` — произвольный префаб, активируемый через хотбар (компонент-носитель определяется проектом)
- `consumable` — ссылка на `ItemEffect` (или его наследника, например `ConsumableEffect`)

**`ItemInstance`** — рантайм-экземпляр (ссылается на `ItemDefinition`).
- `instanceId` (GUID), `stackCount`, `isRotated`, `nestedContainer`
- `CurrentWidth/Height` (с учётом поворота), `TotalWeight` (рекурсивный)
- `Rotate()` — переключает `isRotated`, если `definition.canRotate`
- **Стакирование:** `MaxStack`, `FreeStackSpace`, `IsStackable` (требует `canStack` И `nestedContainer == null` —
  контейнеры не стакаются), `CanStackWith(other)`, `MergeFrom(other)` (переливает максимум единиц,
  возвращает число перенесённых, мутирует `stackCount` обоих)

**`GridContainer`** — двумерная сетка W×H для хранения предметов.
- `containerId` (GUID), `width`, `height`, `maxWeight`
- Запросы: `CanPlace`, `GetItemAt`, `TryGetPosition`, `GetAllItems`, `CanAcceptSomewhere(item)` (есть ли куда принять — стак того же типа или свободная ячейка; для подсветки вложения)
- Мутации: `TryPlace`, `Remove`, `TryStackInto(item)` (доливает `item` в существующие стаки того же типа;
  `true`, если стак поглощён полностью и его можно уничтожить)
- Защита от циклов: `IsContainedWithin` — нельзя положить контейнер в самого себя или в свой nested-tree

### Экипировка (`Runtime/Equipment/`)

**`EquipmentSlot` / `EquipmentSlotType`** — типы слотов: `Backpack`, `Rig`, `SecureCase`, `BodyArmor`, `Helmet`, `FaceCover`, `PrimaryWeapon`, `SecondaryWeapon`, `Holster`. `Accepts(item)` фильтрует по `ItemType`. Особый случай: `SecondaryWeapon` принимает `PrimaryWeapon` (две винтовки), Holster — только `Sidearm`.

### Конфигурация (`Runtime/Config/`)

**`InventoryConfig`** — `ScriptableObject` с тремя секциями:
- `List<PocketEntry> pockets` — карманы (id + W×H + maxWeight).
- `List<EquipmentSlotEntry> equipment` — слоты экипировки (тип + accepts ItemType).
- `HotbarConfig hotbar` — конфигурация хотбара.

`InventoryConfig.CreateDefault()` — встроенный Tarkov-like дефолт (4 кармана 1×1 + 9 equipment-слотов + 10-слотовый хотбар).

### Хотбар (`Runtime/Hotbar/`)

**`HotbarSlotKind`** — `Empty` / `Reserved` / `EquipmentWeapon` / `UserItem`.

**`HotbarConfig`** — `List<HotbarSlotEntry> slots` + `reservedHotkey` + `reservedHotkeyTarget` плюс **тогл и размер**:
- `enabled` (default `true`) — глобальный switch. Если `false`, цифровые клавиши не активны, UI-панель не строится.
- `slotCount` (default `0`) — желаемое количество слотов. `0` = длина `slots`; `>0` = форсированный размер, лишние срезаются, недостающие добавляются как `UserItem`. Применяется через `ApplySlotCountOverride()`.

**`HotbarSlotEntry`** — `kind` + `equipmentSource` (для EquipmentWeapon) + `reservedPrefab` (для Reserved).

`HotbarConfig.CreateDefault()` — Tarkov-like: Reserved (slot 0) + 3 EquipmentWeapon (Primary/Secondary/Holster) + 6 UserItem.

### Эффекты (`Runtime/Effects/`)

**`ItemEffect`** — abstract `ScriptableObject` с `consumeOnUse`, `verb` и абстрактным `TryApply(ItemInstance, IItemEffectContext)`.

**`IItemEffectContext`** — контейнер game-side сервисов: `T Get<T>() where T : class`. Дефолтная реализация `ItemEffectContext` — словарь по типу.

Плагин не знает про конкретные таргеты (hunger, health и т.п.); они вводятся в коде проекта.

### Сохранение (`Runtime/Persistence/`)

**`ISaveProvider`** — `Write` / `Read` / `Exists` / `Delete` по string key.
**`FileSaveProvider`** — дефолт, пишет в `Application.persistentDataPath/<key>.json`.
**`InventoryPersistence`** — глобальная точка инжекции `Provider` + константы ключей (`InventoryKey`, `LayoutKey`).

### Контракт с менеджером (`Runtime/IInventoryService.cs`)

```csharp
public interface IInventoryService {
    void RegisterContainerRecursive(GridContainer c);
    bool TryEquipAnyMatchingSlot(ItemInstance item);
    IEnumerable<GridContainer> GetPickupContainers();
    void NotifyChanged();
    void OpenContainerWindow(GridContainer container, string title); // мировой ящик/сундук → панель игрока + окно ящика
}
```

**`Inventory.Service`** (static) — глобальный доступ. `InventoryManager` реализует `IInventoryService` и регистрирует себя в `Awake`.

### Мир (`Runtime/World/`)

**`WorldItemBase`** — abstract база для пикапов. Поля `definition`, `stackCount`, `preservedInstance`, `pickupKey`. Логика `TryPickup` идёт через `Inventory.Service`: сначала (для стакаемых) доливает в существующие стаки того же типа через `GridContainer.TryStackInto`, затем экипировка, затем свободное место в контейнерах. World-space подсказка показывает `×N` при `stackCount > 1`.

**`WorldItem3D`** (`[MovedFrom("WorldItem")]`) — требует `Collider`. Подсказка билбордится к `Camera.main`. Статические фабрики `Spawn` / `SpawnWithVelocity` — читают `definition.worldPrefab3D`, создают `Rigidbody`, fallback на куб. `SanitizeWorldPrefab` чистит вложенные `Rigidbody`, переводит `MeshCollider` в convex; event `OnPrefabSanitized` — хук для пользовательской чистки (например, FPS-скриптов).

**`WorldItem2D`** — требует `Collider2D`. Подсказка в плоскости XY. Фабрики читают `definition.worldPrefab2D`, создают `Rigidbody2D`, fallback на `SpriteRenderer`-объект.

**`WorldItemSpawner`** (`static`) — единый диспатчер для дропа из инвентаря. `SpawnDropped(instance, position, velocity, rotation)` смотрит на `instance.definition.worldMode` и зовёт нужный `WorldItem3D.SpawnWithVelocity` или `WorldItem2D.SpawnWithVelocity`. В 2D-режиме Z-координата отбрасывается, используется (x, y) скорость.

**`IWorldInteractable`** — общий контракт камерного хвата: `Interact()` (F / основное действие) + `SetPromptVisible(bool)`. Реализуют `WorldItemBase` (Interact = подбор) и `WorldContainerBase` (Interact = открыть). Позволяет `HoverPickup` работать с пикапами и ящиками единообразно.

**`WorldContainerBase`** — abstract база статичного мирового **контейнера** (ящик/сундук). НЕ подбирается. Поля `displayTitle`, `gridWidth`, `gridHeight`, `maxWeight`, `openKey`. Лениво создаёт рантайм-`GridContainer` (id `WorldContainer_{InstanceID}`), регистрирует его в `Inventory.Service`, строит world-space подсказку «[F] Открыть». `Open()` → `Inventory.Service.OpenContainerWindow(Container, Title)`. Содержимое живёт только в рантайме (сессия) — персиста пока нет.

**`WorldContainer3D`** — требует `Collider` (не-триггер), подсказка билбордится к `Camera.main`. **`WorldContainer2D`** — требует `Collider2D`, подсказка в плоскости (без билборда).

**`HoverPickup3D`** (`[MovedFrom("HoverPickup")]`) — на камеру, `Physics.Raycast` от позиции мыши. Рейкаст ищет `WorldItem3D`, затем `WorldContainerBase`; F → `Interact()` наведённой цели. Экспортит `CurrentItem` (текущий **пикап** под курсором; null для ящика), `OnRightClickedItem` (ПКМ по пикапу), `OnRightClickedContainer` (ПКМ по ящику), флаг `SuppressHoverUpdates`. `pickupOnHover` авто-подбирает только предметы.

**`HoverPickup2D`** — два режима: `Mouse` (Physics2D.OverlapPoint под курсором) или `PlayerProximity` (Physics2D.OverlapCircle вокруг ссылки на игрока с радиусом). Так же ищет `WorldItem2D` → `WorldContainerBase`. Те же `CurrentItem` / `OnRightClickedItem` / `OnRightClickedContainer` / `SuppressHoverUpdates`.

---

## 3. Game-side (`Assets/_InventoryPlug/Scripts/Inventory/`)

### Центральная логика

**`InventoryManager`** — *мульти-инстанс* (но активным в любой момент времени считается **один**),
`DefaultExecutionOrder(-100)`, implements `IInventoryService`.
- `static Instance` / `static Active` — текущий активный менеджер.
- `static event OnActiveChanged` — срабатывает при смене активного (UI и хотбар на нём держатся).
- `static SetActive(mgr)` — свап активного. Обновляет `Instance` + `Inventory.Service` и шлёт `OnActiveChanged`.
- `registerAsActiveOnAwake` (default `true`) — первый Awake-нувшийся менеджер становится активным.
  Для per-unit (data-only) менеджеров — выставляется в `false`.
- `isPlayerRig` (default `true`) — `true` ⇒ `transform.SetParent(null)` + `DontDestroyOnLoad`.
  Per-unit менеджеры (на юнитах RTS) — `false`.
- `inventoryName` — лейбл для дебага и идентификации.
- `config: InventoryConfig` — инспектор-поле (если null → `InventoryConfig.CreateDefault()`).
- `Pockets[]` — динамический размер из config.
- `EquipmentSlots` — словарь по `EquipmentSlotType`.
- `BackpackContainer / RigContainer / SecureCaseContainer` — short-cuts к nested-контейнерам.
- Реестр: `RegisterContainer` / `RegisterContainerRecursive` / `GetContainer(id)`.
- `TryLocateItem(item, out container, out pos)` / `TryEquip` / `TryEquipAnyMatchingSlot` / `Unequip` / `MoveItem` / `GetPickupContainers`.
- `Save()` / `Load()` — через `InventoryPersistence.Provider`.
- Событие `OnInventoryChanged`.

### Per-unit инвентари (тактические RPG / RTS, стиль Baldur's Gate)

**`RTSUnitInventory`** (`Scripts/Samples/RTSUnitInventory.cs`) — компонент на юните. В `Awake`
создаёт **дочерний** GO `UnitInventoryData` с `InventoryManager` (`isPlayerRig=false`,
`registerAsActiveOnAwake=false`). Конфиг (`InventoryConfig`) можно переопределить per-unit —
Tank/Scout/Engineer могут иметь разные слоты. Метод `Activate()` зовёт
`InventoryManager.SetActive(Manager)` — после этого подбор/UI/хотбар работают с этим юнитом.

`RTSCommander.Select(unit)` дёргает `unit.GetComponent<RTSUnitInventory>().Activate()`.

### Эффекты (game-specific)

Голод, жажда, здоровье — **три независимых модуля**. Каждый = `MonoBehaviour`-система
+ опциональный HUD-компонент. Все живут на `InventoryRig`. Удаление компонента отключает
и логику, и UI-полоску внутри окна инвентаря.

| Модуль | Логика | HUD | Интерфейс |
|---|---|---|---|
| Голод   | `HungerSystem` | `HungerHUD` | `IHungerTarget` |
| Жажда   | `ThirstSystem` | `ThirstHUD` | `IThirstTarget` |
| Здоровье | `HealthSystem` | `HealthHUD` | `IHealthTarget` |

`IHealthTarget` экспонирует `IsAlive`, `TryRestoreFirstDestroyed`, `TryHealAnyWoundedLimb`,
`DefaultSurgicalKitRestoreHp` (HealthSystem на body-parts).

**`PlayerModuleManager`** (`DefaultExecutionOrder=-95`, на `InventoryRig`) — все системы
реализуют `IPlayerModule` и собираются в `Awake` через `GetComponents<>`. Менеджер экспонирует
`Modules`, `Get<T>()`, `Has<T>()`. `ItemUseService.BuildContext()` ходит через него — если
менеджера нет (старая сцена), fallback на `HungerSystem.Instance` / `ThirstSystem.Instance` /
`HealthSystem.Instance`.

Добавить новый модуль — реализовать `IPlayerModule` + соответствующий target-интерфейс,
положить компонент на `InventoryRig`, при необходимости расширить `ItemUseService.BuildContext()`.

**`ConsumableEffect : ItemEffect`** — поля `restoreHunger`/`restoreThirst`/`isSurgicalKit`/`surgicalKitRestoreHp`/`healLimbAmount`. `TryApply` дёргает `ctx.Get<IHungerTarget>()`, `ctx.Get<IThirstTarget>()`, `ctx.Get<IHealthTarget>()`.

**`ItemUseService`** (static) — фасад:
1. Строит `ItemEffectContext` с активными модулями (через `PlayerModuleManager`).
2. Вызывает `effect.TryApply(item, ctx)`.
3. Если успех и `consumeOnUse=true` — уменьшает стак / удаляет / снимает с экипа.

### Хотбар

**`WeaponHotbar`** — `DefaultExecutionOrder(-85)`. Singleton, ребилдится на `OnActiveChanged`.
- Читает `InventoryManager.config.hotbar` или fallback на `HotbarConfig.CreateDefault()`.
- `Slots[]` — динамический размер, типы из `HotbarConfig.slots`.
- Клавиши `1..9, 0` (если есть столько слотов) + `reservedHotkey` (по умолчанию `H` → слот 0).
- Активация: Reserved → берёт `reservedPrefab`. EquipmentWeapon → берёт оружие из equipment-слота. UserItem с `weaponPrefab` → в руки; с `consumable` → `ItemUseService.TryUse`.
- Подписан на `OnInventoryChanged`, сбрасывает orphaned user-предметы.

**Тогл и размер настраиваются с двух мест:**

| Уровень | Поле | Где | Описание |
|---|---|---|---|
| Проект | `HotbarConfig.enabled` | `InventoryConfig.hotbar` | Дефолтный тогл (true) |
| Проект | `HotbarConfig.slotCount` | `InventoryConfig.hotbar` | 0 = размер из `slots.Count`; >0 = принудительно столько |
| Сцена  | `WeaponHotbar.hotbarEnabledOverride` | компонент на InventoryRig | Сценовый override (галка) |
| Сцена  | `WeaponHotbar.slotCountOverride` | компонент на InventoryRig | 0 = из конфига; >0 = forced |

`IsHotbarEnabled = hotbarEnabledOverride && HotbarConfig.enabled`. Если выключен — клавиши не работают, UI-панель не строится.

Runtime API: `WeaponHotbar.Instance.SetHotbarEnabled(bool)` и `SetSlotCount(int)`.
Событие `OnEnabledChanged` + `OnSlotsChanged` — UI пересобирается с нуля при разнице в числе слотов.
Live-edit в инспекторе во время игры (`#if UNITY_EDITOR` ветка в `LateUpdate`) — поддерживается.

### Сохранение / Загрузка

**`InventorySaveData`** + `ContainerSaveData` / `ItemSaveData` / `EquipmentSlotSaveData` — DTO для `JsonUtility`.

`InventoryManager.Save` → `InventoryPersistence.Provider.Write(InventoryKey, json)`.
`InventoryManager.Load` → `InventoryPersistence.Provider.Read(InventoryKey)`.

Дефолтный `FileSaveProvider` пишет в `Application.persistentDataPath/inventory.json` (на Windows — `%AppData%\..\LocalLow\<Company>\<Product>\inventory.json`).

Карманы при загрузке восстанавливаются по `containerId` из `config.pockets` (не хардкод `Pocket1..4`).

### UI-конфиг

**`InventoryLayout`** — SO, описывает позиции UI-элементов (используется только `InventoryUI`, хардкодит 4 pocket-позиции — TODO: разруливается через будущий prefab-UI).
**`InventoryLayoutCustomization`** — пользовательские overrides поверх SO.
**`InventoryLayoutPersistence`** — JSON в `inventory_layout.json` через `InventoryPersistence.Provider`.

### UI (`Assets/_InventoryPlug/Scripts/Inventory/UI/`)

- **`InventoryUI`** — корневой UI. Singleton. `Tab` для toggle. `Awake` создаёт runtime-копию `InventoryLayout`, применяет `customization`, строит Canvas программно. Подписан на `OnInventoryChanged` / `OnVitalsChanged` / `OnHealthChanged`.
- **`GridUI`** — отрисовка одного `GridContainer`. `CellSize` / `CellGap` — **`static`** (а не `const`), чтобы `LayoutEditMode` мог менять размер ячейки в рантайме (после смены требуется пересоздать все `GridUI` через `InventoryUI.RebuildInventoryPanel()`). Спрайт предмета — отдельный дочерний `Icon`, который и поворачивается на `-90°` при `isRotated` (корень-footprint остаётся ровным, подпись/фон не крутятся). При `stackCount > 1` в правом-нижнем углу футпринта рисуется бейдж `×N` (на корне, поэтому не вращается с иконкой).
- **`EquipmentSlotUI`** — слот экипировки, принимает drop. Визуал экипированного предмета — превью из `ItemPreviewRenderer`.
- **`ItemUI`** — один предмет на гриде (drag handlers).
- **`ItemPreviewRenderer`** (static) — **рендерит world-префаб предмета в спрайт-превью** для UI. Заменяет статичные иконки: в сетке, экипировке, хотбаре и ghost'е перетаскивания показываются настоящие префаб-объекты. Приоритет: назначенная `ItemDefinition.icon` → для 2D берётся спрайт прямо из `SpriteRenderer`'а префаба → для 3D рендерится off-screen (изолированный camera-rig на `y=-1000` + 3 Point-light'а, снимок в `RenderTexture` → `Texture2D` → `Sprite`). Рендер **один раз на `ItemDefinition`** и кешируется (`ClearCache()` сбрасывает).
- **`DragDropController`** — singleton; ghost-картинка (спрайт из `ItemPreviewRenderer`), `R` поворачивает предмет (ghost ресайзится и подсветка целевых ячеек пересчитывается сразу, не дожидаясь движения мыши). Целевая ячейка — прямо под курсором (детект меняется ровно на границах сетки). Дроп в сетку / equipment-слот / хотбар.
  - **Стакирование:** дроп стакаемого предмета на стак того же типа под курсором **сливает** их (`ItemInstance.MergeFrom`); остаток (если стак переполнился) возвращается на origin.
  - **Split (`Shift` + drag):** начать перетаскивание с зажатым `Shift` со стака >1 — отделяется половина (округление вверх) в новый под-стак; источник остаётся на месте. Если дроп не удался — под-стак вливается обратно в источник (`splitSourceItem`). Под-стак нельзя класть в хотбар/экипировку (там — только цельный предмет).
  - **Вложение в контейнер (Tarkov-style):** дроп предмета поверх предмета-контейнера (`nestedContainer != null`), лежащего в сетке, кладёт его **внутрь** этого контейнера (`TryDropIntoContainer` — сначала долить в стаки того же типа, затем первая свободная ячейка). При наведении подсвечивается весь футпринт контейнера (зелёный/красный по `GridContainer.CanAcceptSomewhere`). Над контейнером раскладка по внешней сетке не делается: не поместилось — предмет вернётся на origin. Самовложение заблокировано (`CanPlace` → `IsContainedWithin`).
- **`ContextMenuUI`** — RMB-меню инвентаря, **полностью редактируемое**:
  - В инспекторе компонента есть список `entries` из built-in кнопок (`Use` / `Inspect` /
    `Open` / `Split` / `Drop`); у каждой — поля `label` и `enabled`. `Split` показывается только
    для стака >1 в сетке и кладёт отделённую половину в свободную ячейку того же контейнера;
    `Awake` досоздаёт пункт `Split` в старых сериализованных списках (миграция).
  - Список `extraEntries` — для проектных пунктов. Колбэк назначается из кода через
    `ContextMenuUI.Instance.AddExtraEntry(label, item => …)`.
  - Кнопки **пересобираются на каждый `Show()`** — `Open` корректно скрывается для
    неконтейнеров и т.п.
  - Дроп выкидывает предмет через `WorldItemSpawner.SpawnDropped(...)` — диспатчер сам
    выбирает 2D/3D вариант по `ItemDefinition.worldMode`.
- **`InspectWindowUI`** — окно осмотра: рендерит `ItemDefinition.ActiveWorldPrefab` на `RenderTexture` (отдельная изолированная камера + Point-light'ы). ЛКМ-drag по вьюпорту вращает 3D-модель (yaw/pitch), колесо — зум. Для 2D-предмета вращение отключено (плоский спрайт лицом к камере). Описание / тип / вес / размер / заполненность контейнера.
- **`LayoutDragHandle`** + **`LayoutEditMode`** — рантайм-редактор layout.
- **`UIResizer`** — «ручка» в углу окна для растягивания (`Target`) перетаскиванием. Работает только в `LayoutEditMode.IsActive`; pivot окна (0.5, 0.5) — противоположный угол остаётся на месте. По отпускании зовёт `OnResized(newSize, newPos)`, `InventoryUI` сохраняет в JSON.
- **`UIDragger`** — общий движок drag-on-screen для окон.
- **`WeaponHotbarUI`** / **`WeaponHotbarSlotUI`** — нижняя полоса хотбара (динамическое число слотов, иконки через `ItemPreviewRenderer`).

---

## 4. Связанные подсистемы

### `Scripts/Health/`
- `HealthSystem` (`MonoBehaviour`, реализует `IHealthTarget` + `IPlayerModule`) — HP по `BodyPartType`, разрушенные / раненые части.
- `HealthConfig` SO — максимум HP, дефолтное восстановление хирургическим набором.
- `BodyPartType` enum — 7 частей.

### `Scripts/HungerSystem.cs` / `Scripts/ThirstSystem.cs`
- Отдельные модули голода и жажды, каждый — `IHungerTarget` / `IThirstTarget` + `IPlayerModule`. Удаление компонента отключает соответствующую логику и бар в UI инвентаря.

### `Scripts/PlayerModuleManager.cs`
- `DefaultExecutionOrder=-95`. Собирает все `IPlayerModule`-компоненты с GameObject через `GetComponents<>` в `Awake`. Экспонирует `Modules`, `Get<T>()`, `Has<T>()`. Используется `ItemUseService.BuildContext()`.

### `Scripts/UI/HungerHUD.cs` / `ThirstHUD.cs` / `HealthHUD.cs` / `VitalsHUD.cs`
- HUD-полоски для соответствующих модулей вне окна инвентаря. Не строятся, если у `InventoryRig` нет нужной системы.

### `Scripts/UI/WorldContextMenu.cs`
- Generic экранное ПКМ-меню в мире (RTS/TopDown/2D-3D сцена). Не привязан к `ItemInstance`,
  принимает произвольный `(label, action)`-список через
  `Show(screenPos, params (string, Action)[])`. Создаётся лениво через `WorldContextMenu.Ensure()`.
  Авто-закрытие на повторную ПКМ или Escape.

### `Scripts/UI/WorldPickupContextMenu.cs`
- Игровая прокладка между UPM-`HoverPickup3D`/`HoverPickup2D` и `WorldContextMenu`.
  Вешается на ту же камеру (автоматически через `InventorySpawner.Spawn()`); подписывается на
  `OnRightClickedItem`, открывает меню с двумя редактируемыми пунктами `showInspect`/`labelInspect`
  и `showPickup`/`labelPickup` (default "Осмотр" / "Подобрать"). Дополнительные пункты — через
  `AddExtraEntry(label, handler)`; handler получает наведённый `WorldItemBase`. Пока меню
  открыто — у HoverPickup взводится `SuppressHoverUpdates = true`.
- Также подписан на `OnRightClickedContainer` (ПКМ по мировому ящику/сундуку) и открывает меню
  с пунктом «Открыть» (`showOpen`/`labelOpen`, default true / "Открыть") → `WorldContainerBase.Open()`.

### `Editor/`
- **`InventorySpawner`** — `Tools/Inventory/Spawn Inventory On Player (3D)` / `(2D)`. 3D вешает `HoverOutline` + `HoverPickup3D`. 2D — только `HoverPickup2D`. Дополнительно добавляет `WorldPickupContextMenu` на камеру.
- **`PickupConverter`** — `Tools/Inventory/Make Pickup From Selected — 3D` (`Ctrl+Alt+P`) / `— 2D` (`Ctrl+Alt+Shift+P`). 3D добавляет `BoxCollider`+`Rigidbody`+`WorldItem3D` и пишет префаб в `def.worldPrefab3D`. 2D — `BoxCollider2D`+`Rigidbody2D`+`WorldItem2D`.
- **`WorldContainerConverter`** — `Tools/Inventory/Make Container From Selected — 3D` / `— 2D`. Добавляет коллайдер (не-триггер, auto-fit) + `WorldContainer3D/2D`. **Rigidbody НЕ добавляет** (ящик статичен). Размер сетки/заголовок/лимит веса — на компоненте.
- **`InventoryConfigPresets`** — `Tools/Inventory/Create Config Preset/{Tarkov, Diablo, Minecraft}-like`.
- **`EquipmentPrefabGenerator`** — генерирует ItemDefinition и автокуб-prefab для базовой экипировки (пишет в `def.worldPrefab3D`).
- **`SampleSceneBuilder`** — `Tools/Inventory/Samples/Build … Sample` — 5 готовых сцен (FPS / 2D Top-Down / 2D Platformer / RTS / 3D TopDown-Orbit). RTS-сэмпл собирает 3 юнита с `RTSUnitInventory`.

---

## 5. Поток данных (cheat-sheet)

```
WorldItem3D|2D.TryPickup()
        │
        ▼
Inventory.Service.TryEquipAnyMatchingSlot / GetPickupContainers → GridContainer.TryPlace
        │
        ▼
NotifyChanged → InventoryManager.OnInventoryChanged
        │
        ├─► InventoryUI.Refresh
        └─► WeaponHotbar.OnInventoryChanged → OnSlotsChanged
                                    │
                                    └─► WeaponHotbarUI.Refresh
```

```
User: Tab                  → InventoryUI.Toggle
User: drag/drop            → DragDropController → InventoryManager.MoveItem
User: R during drag        → ItemInstance.Rotate → ghost resize
User: right-click (item)   → ContextMenuUI → ItemUseService.TryUse / Unequip / Drop (WorldItemSpawner.SpawnDropped)
User: right-click (world)  → WorldPickupContextMenu / WorldContextMenu (через HoverPickup OnRightClickedItem)
User: 1..9, 0 / hotkey     → WeaponHotbar.SelectSlot → активное оружие / ItemUseService.TryUse
User: Save/Load (buttons)  → InventoryManager.Save() / Load() → InventoryPersistence.Provider
User: RTS select unit      → RTSCommander.Select → RTSUnitInventory.Activate → InventoryManager.SetActive
```

```
ItemUseService.TryUse(item, container, slotType)
        │
        ▼
ItemEffectContext { IHungerTarget, IThirstTarget, IHealthTarget } (через PlayerModuleManager)
        │
        ▼
item.definition.consumable.TryApply(item, ctx)
        │
        ▼
HungerSystem.AddHunger / ThirstSystem.AddThirst / HealthSystem.RestoreLimb / ...
        │
        ▼
OnHungerChanged / OnThirstChanged / OnHealthChanged → UI обновляется
```

---

## 6. Особенности и подводные камни

- **`InventoryManager.Awake` снимает себя с родителя** (`SetParent(null)`) **только если `isPlayerRig=true`**. Это нужно, потому что `InventoryRig` — child префаба игрока, но `DontDestroyOnLoad` обязан стоять на root. У per-unit (RTS) менеджеров `isPlayerRig=false` — они никуда не переподнимаются.
- **DefaultExecutionOrder**: `InventoryManager` = `-100`, `PlayerModuleManager` = `-95`, `WeaponHotbar` = `-85`. UI и модули инициализируются позже, поэтому в их `Awake` `InventoryManager.Instance` уже не null.
- **`Inventory.Service` обновляется через `InventoryManager.SetActive`** — `WorldItemBase.TryPickup` опирается на эту регистрацию. При выборе другого юнита в RTS — Service перепривязывается автоматически.
- **`OnActiveChanged`-подписки** держат UI/хотбар: `InventoryUI.HandleActiveChanged` и `WeaponHotbar.HandleActiveChanged` ребилдятся с нуля на смену активного.
- **Drag-and-drop равнодушен к `Unequip`**: `DragDropController.BeginDrag` снимает экипировку через прямое присвоение `slot.EquippedItem = null` (а не через `InventoryManager.Unequip`) — иначе `OnInventoryChanged` уничтожит сам `ItemUI`, который перетаскивается.
- **Drop из мира** сохраняет `ItemInstance` (с вложенным контейнером!) через `WorldItem*.preservedInstance`. Поднял → `TryPickup` использует именно этот instance. `WorldItemSpawner.SpawnDropped` пробрасывает preservedInstance в нужный 2D/3D-спавнер.
- **MaxWeight у контейнера**: `0f` = без лимита (см. `GridContainer.CanPlace`).
- **Самовложенность контейнера** заблокирована: `IsContainedWithin` рекурсивно идёт по `nestedContainer.placed`.
- **`[MovedFrom]` на `WorldItem3D` / `HoverPickup3D`** — обеспечивает совместимость с префабами и сценами, где раньше были `WorldItem` / `HoverPickup`. После первого Save сцены ссылка обновится навсегда.
- **Layout customization vs SO**: `InventoryUI` всегда работает с `Instantiate(Layout)`. SO-ассет не загрязняется. JSON `inventory_layout.json` (через `InventoryPersistence.Provider`) — единственный носитель override-позиций.
- **Превью кешируется по `ItemDefinition`**: `ItemPreviewRenderer.GetSprite` рендерит/достаёт спрайт один раз на тип предмета. Если поменял префаб/иконку в рантайме — зови `ItemPreviewRenderer.ClearCache()`, иначе будет старая картинка.
- **`GridUI.CellSize/CellGap` — `static`, не `const`**: смена размера ячейки на лету требует пересоздать все `GridUI` (`InventoryUI.RebuildInventoryPanel()`); просто поменять значение поля недостаточно — уже построенные сетки не пересчитаются сами.
- **Стакирование требует `canStack`**: `ItemInstance.IsStackable` = `definition.canStack && nestedContainer == null`. Контейнеры (рюкзаки/кейсы) **никогда** не стакаются — каждый уникален. `MaxStack` = `1` при выключенном `canStack`, поэтому `maxStackSize` без `canStack` игнорируется. Merge'ы (мир/drag/split) **не проверяют** `maxWeight` контейнера — для v1 это сознательное упрощение (контейнеры обычно `maxWeight = 0`).

---

## 7. Тесты

`Packages/com.gridinventory.inventory/Tests/Editor/GridContainerTests.cs` — 15 EditMode-тестов, **15/15 passing**:
- Placement (4): границы, занятые ячейки, замещение в той же позиции, fail outside bounds.
- Rotation (3): swap W/H, canRotate filter, помещение после поворота.
- Nested containers (3): самовложенность, цикл outer→inner→outer.
- WeightLimit (3): `0 = unlimited`, отказ при overweight, accumulation.
- Removal (2): освобождение клеток, `GetItemAt`.

Запуск из Test Runner: `Window → General → Test Runner → EditMode → GridInventory.Inventory.Tests.Editor`.

---

## 8. Что осталось переехать в плагин

Из `Assets/_InventoryPlug/Scripts/Inventory/`:
- `InventoryManager` — implements `IInventoryService`. Переедет, когда появится generic-API для item database и UI-конфига.
- `InventoryUI`, `GridUI`, `ItemUI`, `EquipmentSlotUI`, `WeaponHotbarUI`, `WeaponHotbarSlotUI`, `ContextMenuUI`, `InspectWindowUI`, `LayoutDragHandle`, `LayoutEditMode`, `DragDropController`, `UIDragger` — UI-стек. Переедет с **prefab-based UI** рефактором.
- `WeaponHotbar` (логика хотбара) — переедет с prefab-UI.
- `InventoryLayout`, `InventoryLayoutCustomization`, `InventoryLayoutPersistence` — UI-конфиг, переедет с prefab-UI.

Game-specific и **в плагин не переедут**:
- `ConsumableEffect`, `IHungerThirstTarget`, `IHealthTarget`, `ItemUseService` — пример пользовательских эффектов.
- `PlayerVitals`, `HealthSystem`, `HealthConfig`, `VitalsHUD`, `HealthHUD`, `BodyPartType` — игровые механики.
- `PlayerController`, `FreeLookCamera`, `HoverOutline`, `CustomCursor*` — общий код проекта.
