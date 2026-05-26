# Inventory System

Гридовый инвентарь для 2D/3D игр (исторически — в духе Tarkov):
- предметы занимают **W×H ячеек** в сетке, могут вращаться;
- контейнеры вкладываются друг в друга (рюкзак внутри рюкзака — запрещён циклический случай);
- произвольное число карманов и экипировочных слотов — задаётся через `InventoryConfig` SO;
- хотбар с конфигурируемыми слотами (`Reserved` / `EquipmentWeapon` / `UserItem`);
- эффекты предметов через абстракцию `ItemEffect` (плагин не знает про конкретные игровые системы);
- сохранение через подменяемый `ISaveProvider`.

С версии **0.1.0** ядро живёт в UPM-пакете `Packages/com.gridinventory.inventory`.
Конкретная игровая обвязка — в `Assets/GAME/Scripts/`.

См. [Plugin-Plan.md](Plugin-Plan.md) — план развития в переиспользуемый пакет.

---

## 1. Asset-данные (`Assets/GAME/Inventory/`)

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
- Стек: `maxStackSize`
- Вес: `weightPerUnit`
- Контейнер: `isContainer`, `containerWidth`, `containerHeight`, `containerMaxWeight` (`0` = без лимита)
- `worldPrefab` — префаб в мире (для дропа и осмотра)
- `weaponPrefab` — произвольный префаб, активируемый через хотбар (компонент-носитель определяется проектом)
- `consumable` — ссылка на `ItemEffect` (или его наследника, например `ConsumableEffect`)

**`ItemInstance`** — рантайм-экземпляр (ссылается на `ItemDefinition`).
- `instanceId` (GUID), `stackCount`, `isRotated`, `nestedContainer`
- `CurrentWidth/Height` (с учётом поворота), `TotalWeight` (рекурсивный)
- `Rotate()` — переключает `isRotated`, если `definition.canRotate`

**`GridContainer`** — двумерная сетка W×H для хранения предметов.
- `containerId` (GUID), `width`, `height`, `maxWeight`
- Запросы: `CanPlace`, `GetItemAt`, `TryGetPosition`, `GetAllItems`
- Мутации: `TryPlace`, `Remove`
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

**`HotbarConfig`** — `List<HotbarSlotEntry> slots` + `reservedHotkey` + `reservedHotkeyTarget`.

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
}
```

**`Inventory.Service`** (static) — глобальный доступ. `InventoryManager` реализует `IInventoryService` и регистрирует себя в `Awake`.

### Мир (`Runtime/World/`)

**`WorldItemBase`** — abstract база для пикапов. Поля `definition`, `stackCount`, `preservedInstance`, `pickupKey`. Логика `TryPickup` идёт через `Inventory.Service`. World-space подсказка.

**`WorldItem3D`** (`[MovedFrom("WorldItem")]`) — требует `Collider`. Подсказка билбордится к `Camera.main`. Статические фабрики `Spawn` / `SpawnWithVelocity` — создают `Rigidbody`, fallback на куб. `SanitizeWorldPrefab` чистит вложенные `Rigidbody`, переводит `MeshCollider` в convex; event `OnPrefabSanitized` — хук для пользовательской чистки (например, FPS-скриптов).

**`WorldItem2D`** — требует `Collider2D`. Подсказка в плоскости XY. Фабрики создают `Rigidbody2D`, fallback на `SpriteRenderer`-объект.

**`HoverPickup3D`** (`[MovedFrom("HoverPickup")]`) — на камеру, `Physics.Raycast` от позиции мыши, F-подбор.

**`HoverPickup2D`** — два режима: `Mouse` (Physics2D.OverlapPoint под курсором) или `PlayerProximity` (Physics2D.OverlapCircle вокруг ссылки на игрока с радиусом).

---

## 3. Game-side (`Assets/GAME/Scripts/Inventory/`)

### Центральная логика

**`InventoryManager`** — singleton, `DefaultExecutionOrder(-100)`, implements `IInventoryService`.
- `config: InventoryConfig` — инспектор-поле (если null → `InventoryConfig.CreateDefault()`).
- `Pockets[]` — динамический размер из config.
- `EquipmentSlots` — словарь по `EquipmentSlotType`.
- `BackpackContainer / RigContainer / SecureCaseContainer` — short-cuts к nested-контейнерам.
- Реестр: `RegisterContainer` / `RegisterContainerRecursive` / `GetContainer(id)`.
- `TryLocateItem(item, out container, out pos)` / `TryEquip` / `TryEquipAnyMatchingSlot` / `Unequip` / `MoveItem` / `GetPickupContainers`.
- `Save()` / `Load()` — через `InventoryPersistence.Provider`.
- Событие `OnInventoryChanged`.

### Эффекты (game-specific)

**`IHungerThirstTarget`** — `AddHunger(float)`, `AddThirst(float)`. Реализуется на `PlayerVitals`.

**`IHealthTarget`** — `IsAlive`, `TryRestoreFirstDestroyed`, `TryHealAnyWoundedLimb`, `DefaultSurgicalKitRestoreHp`. Реализуется на `HealthSystem`.

**`ConsumableEffect : ItemEffect`** — поля `restoreHunger`/`restoreThirst`/`isSurgicalKit`/`surgicalKitRestoreHp`/`healLimbAmount`. `TryApply` дёргает `ctx.Get<IHungerThirstTarget>()` и `ctx.Get<IHealthTarget>()`.

**`ItemUseService`** (static) — фасад:
1. Строит `ItemEffectContext` с `PlayerVitals.Instance` / `HealthSystem.Instance`.
2. Вызывает `effect.TryApply(item, ctx)`.
3. Если успех и `consumeOnUse=true` — уменьшает стак / удаляет / снимает с экипа.

### Хотбар

**`WeaponHotbar`** — `DefaultExecutionOrder(-85)`. Singleton.
- Читает `InventoryManager.config.hotbar` или fallback на `HotbarConfig.CreateDefault()`.
- `Slots[]` — динамический размер, типы из `HotbarConfig.slots`.
- Клавиши `1..9, 0` (если есть столько слотов) + `reservedHotkey` (по умолчанию `H` → слот 0).
- Активация: Reserved → берёт `reservedPrefab`. EquipmentWeapon → берёт оружие из equipment-слота. UserItem с `weaponPrefab` → в руки; с `consumable` → `ItemUseService.TryUse`.
- Подписан на `OnInventoryChanged`, сбрасывает orphaned user-предметы.

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

### UI (`Assets/GAME/Scripts/Inventory/UI/`)

- **`InventoryUI`** — корневой UI. Singleton. `Tab` для toggle. `Awake` создаёт runtime-копию `InventoryLayout`, применяет `customization`, строит Canvas программно. Подписан на `OnInventoryChanged` / `OnVitalsChanged` / `OnHealthChanged`.
- **`GridUI`** — отрисовка одного `GridContainer`.
- **`EquipmentSlotUI`** — слот экипировки, принимает drop.
- **`ItemUI`** — один предмет на гриде (drag handlers).
- **`DragDropController`** — singleton; ghost-картинка, `R` поворачивает предмет. Дроп в сетку / equipment-слот / хотбар.
- **`ContextMenuUI`** — RMB-меню: «Использовать», «Снять», «Осмотреть», «Выбросить», «Назначить на хотбар».
- **`InspectWindowUI`** — рендерит `worldPrefab` на RenderTexture.
- **`LayoutDragHandle`** + **`LayoutEditMode`** — рантайм-редактор layout.
- **`UIDragger`** — общий движок drag-on-screen для окон.
- **`WeaponHotbarUI`** / **`WeaponHotbarSlotUI`** — нижняя полоса хотбара (динамическое число слотов).

---

## 4. Связанные подсистемы

### `Scripts/Health/`
- `HealthSystem` (singleton, `IHealthTarget`) — HP по `BodyPartType`, разрушенные / раненые части.
- `HealthConfig` SO — максимум HP, дефолтное восстановление хирургическим набором.
- `BodyPartType` enum — 7 частей.

### `Scripts/PlayerVitals.cs`
- Голод / жажда, `IHungerThirstTarget`. Singleton.

### `Scripts/UI/VitalsHUD.cs`
- Полоски голода/жажды/HP вне инвентаря.

### `Editor/`
- **`InventorySpawner`** — `Tools/Inventory/Spawn Inventory On Player (3D)` / `(2D)`. 3D вешает `HoverOutline` + `HoverPickup3D`. 2D — только `HoverPickup2D`.
- **`PickupConverter`** — `Tools/Inventory/Make Pickup From Selected — 3D` (`Ctrl+Alt+P`) / `— 2D` (`Ctrl+Alt+Shift+P`). 3D добавляет `BoxCollider`+`Rigidbody`+`WorldItem3D`. 2D — `BoxCollider2D`+`Rigidbody2D`+`WorldItem2D`.
- **`InventoryConfigPresets`** — `Tools/Inventory/Create Config Preset/{Tarkov, Diablo, Minecraft}-like`.
- **`EquipmentPrefabGenerator`** — генерирует ItemDefinition и автокуб-prefab для базовой экипировки.

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
User: right-click          → ContextMenuUI → ItemUseService.TryUse / Unequip / Drop (WorldItem3D.SpawnWithVelocity)
User: 1..9, 0 / hotkey     → WeaponHotbar.SelectSlot → активное оружие / ItemUseService.TryUse
User: Save/Load (buttons)  → InventoryManager.Save() / Load() → InventoryPersistence.Provider
```

```
ItemUseService.TryUse(item, container, slotType)
        │
        ▼
ItemEffectContext { IHungerThirstTarget, IHealthTarget }
        │
        ▼
item.definition.consumable.TryApply(item, ctx)
        │
        ▼
PlayerVitals.AddHunger / HealthSystem.RestoreLimb / ...
        │
        ▼
OnVitalsChanged / OnHealthChanged → UI обновляется
```

---

## 6. Особенности и подводные камни

- **`InventoryManager.Awake` снимает себя с родителя** (`SetParent(null)`), потому что `InventoryRig` — child префаба игрока, но нужен `DontDestroyOnLoad` на root.
- **DefaultExecutionOrder**: `InventoryManager` = `-100`, `WeaponHotbar` = `-85`. UI и `PlayerVitals` инициализируются позже, поэтому в их `Awake` `InventoryManager.Instance` уже не null.
- **`Inventory.Service` устанавливается в `InventoryManager.Awake`** — `WorldItemBase.TryPickup` опирается на эту регистрацию.
- **Drag-and-drop равнодушен к `Unequip`**: `DragDropController.BeginDrag` снимает экипировку через прямое присвоение `slot.EquippedItem = null` (а не через `InventoryManager.Unequip`) — иначе `OnInventoryChanged` уничтожит сам `ItemUI`, который перетаскивается.
- **Drop из мира** сохраняет `ItemInstance` (с вложенным контейнером!) через `WorldItem.preservedInstance`. Поднял → `TryPickup` использует именно этот instance.
- **MaxWeight у контейнера**: `0f` = без лимита (см. `GridContainer.CanPlace`).
- **Самовложенность контейнера** заблокирована: `IsContainedWithin` рекурсивно идёт по `nestedContainer.placed`.
- **`[MovedFrom]` на `WorldItem3D` / `HoverPickup3D`** — обеспечивает совместимость с префабами и сценами, где раньше были `WorldItem` / `HoverPickup`. После первого Save сцены ссылка обновится навсегда.
- **Layout customization vs SO**: `InventoryUI` всегда работает с `Instantiate(Layout)`. SO-ассет не загрязняется. JSON `inventory_layout.json` (через `InventoryPersistence.Provider`) — единственный носитель override-позиций.

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

Из `Assets/GAME/Scripts/Inventory/`:
- `InventoryManager` — implements `IInventoryService`. Переедет, когда появится generic-API для item database и UI-конфига.
- `InventoryUI`, `GridUI`, `ItemUI`, `EquipmentSlotUI`, `WeaponHotbarUI`, `WeaponHotbarSlotUI`, `ContextMenuUI`, `InspectWindowUI`, `LayoutDragHandle`, `LayoutEditMode`, `DragDropController`, `UIDragger` — UI-стек. Переедет с **prefab-based UI** рефактором.
- `WeaponHotbar` (логика хотбара) — переедет с prefab-UI.
- `InventoryLayout`, `InventoryLayoutCustomization`, `InventoryLayoutPersistence` — UI-конфиг, переедет с prefab-UI.

Game-specific и **в плагин не переедут**:
- `ConsumableEffect`, `IHungerThirstTarget`, `IHealthTarget`, `ItemUseService` — пример пользовательских эффектов.
- `PlayerVitals`, `HealthSystem`, `HealthConfig`, `VitalsHUD`, `HealthHUD`, `BodyPartType` — игровые механики.
- `PlayerController`, `FreeLookCamera`, `HoverOutline`, `CustomCursor*` — общий код проекта.
