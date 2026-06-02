# План: «Inventory Plugin» — universal 2D/3D пакет

Цель — из текущей реализации в `Assets/_InventoryPlug/Scripts/Inventory` сделать пакет,
который ставится в любой Unity-проект (2D или 3D) и за минимальные усилия даёт
гридовый инвентарь с экипировкой, хотбаром и сохранением.

Текущая версия плагина: **0.1.0** (embedded UPM, `Packages/com.gridinventory.inventory`).

---

## 1. Текущая форма пакета

```
Packages/com.gridinventory.inventory/
├── package.json                              # "testables": [Runtime asmdef]
├── README.md
├── Runtime/
│   ├── GridInventory.Inventory.Runtime.asmdef
│   ├── IInventoryService.cs                  # контракт для World/Hotbar
│   ├── Inventory.cs                          # static Inventory.Service
│   ├── Core/        # GridContainer, ItemInstance, ItemDefinition, ItemType
│   ├── Equipment/   # EquipmentSlot, EquipmentSlotType
│   ├── Effects/     # ItemEffect (abstract SO), IItemEffectContext, ItemEffectContext
│   ├── Persistence/ # ISaveProvider, FileSaveProvider, InventoryPersistence
│   ├── Config/      # InventoryConfig (SO), PocketEntry, EquipmentSlotEntry
│   ├── Hotbar/      # HotbarSlotKind, HotbarConfig, HotbarSlotEntry
│   └── World/       # WorldItemBase, WorldItem3D, WorldItem2D, HoverPickup3D, HoverPickup2D,
│                    #   IWorldInteractable, WorldContainerBase, WorldContainer3D, WorldContainer2D
├── Editor/
│   └── GridInventory.Inventory.Editor.asmdef  # пока пустой
└── Tests/
    └── Editor/
        ├── GridInventory.Inventory.Tests.Editor.asmdef
        └── GridContainerTests.cs              # 15/15 ✅
```

Что осталось вне плагина в `Assets/_InventoryPlug/Scripts/` (game-side):

- `InventoryManager` — implements `IInventoryService`, регистрируется в `Inventory.Service`. Сюда не переедет, пока не появится generic-API для item database / containers registry.
- `InventoryUI`, `GridUI`, `ItemUI`, `EquipmentSlotUI`, `WeaponHotbarUI`, `WeaponHotbarSlotUI`, `ContextMenuUI`, `InspectWindowUI`, `LayoutDragHandle`, `LayoutEditMode`, `DragDropController`, `UIDragger` — UI-стек. Переедет, когда сделаем prefab-UI.
- `WeaponHotbar` (логика хотбара) — переедет после prefab-UI.
- `InventoryLayout`, `InventoryLayoutCustomization`, `InventoryLayoutPersistence` — UI-конфиг, тоже переедет.
- `ConsumableEffect : ItemEffect`, `IHungerTarget`, `IThirstTarget`, `IHealthTarget`, `ItemUseService` — game-specific эффекты и адаптеры. Остаются в `_InventoryPlug/Scripts/` как пример пользователя.
- `WorldWeaponPrefabSanitizer`, `WeaponPrefabBinder` — удалены вместе с KINEMATION-зависимостями.

---

## 2. Де-coupling — ✅ сделано (v0.1.0)

### 2.1. От FPS-специфичных скриптов
- `WorldItem3D.SanitizeWorldPrefab` теперь generic (только Rigidbody/MeshCollider).
- `WorldItem3D.OnPrefabSanitized` event — потребитель плагина подписывается и снимает свои game-specific компоненты (FPS-скрипты, recoil и т.п.).
- `WeaponHotbar.GetActiveWeaponPrefab` возвращает `GameObject`; кому он нужен — пусть подписываются на `OnActiveChanged`.

### 2.2. От `PlayerVitals` / `HealthSystem`

**Плагин (`Runtime/Effects/`):**

```csharp
public interface IItemEffectContext { T Get<T>() where T : class; }
public class ItemEffectContext : IItemEffectContext { /* словарь сервисов по типу */ }

public abstract class ItemEffect : ScriptableObject {
    public bool consumeOnUse = true;
    public string verb = "";
    public abstract bool TryApply(ItemInstance item, IItemEffectContext ctx);
}
```

Плагин не знает ни про hunger, ни про health — `Get<T>()` принимает любой game-side тип.

**Game-side абстракции** (`Assets/_InventoryPlug/Scripts/Inventory/`):

```csharp
public interface IHungerThirstTarget { void AddHunger(float); void AddThirst(float); }
public interface IHealthTarget {
    bool IsAlive { get; }
    bool TryRestoreFirstDestroyed(float restoreHp);
    bool TryHealAnyWoundedLimb(float amount);
    float DefaultSurgicalKitRestoreHp { get; }
}
```

`PlayerVitals` реализует `IHungerThirstTarget`. `HealthSystem` реализует `IHealthTarget`.

`ConsumableEffect : ItemEffect` — поля `restoreHunger`/`restoreThirst`/`isSurgicalKit`/`surgicalKitRestoreHp`/`healLimbAmount` и логика их применения живут здесь, в `TryApply`. Существующие `.asset` файлы загружаются без изменений.

`ItemUseService` — тонкий фасад: строит `ItemEffectContext` с `PlayerVitals.Instance` / `HealthSystem.Instance`, вызывает `effect.TryApply(item, ctx)`, при успехе уменьшает стак.

В этой же модели легко добавить новый эффект (`MagicEffect`, `ArmorRepairEffect` и т.п.) без правок плагина.

### 2.3. От `InventoryUI.Instance?.Refresh()`

`WeaponHotbar.SelectSlot` больше не вызывает UI напрямую. `InventoryUI` уже подписан на:
- `InventoryManager.OnInventoryChanged` → `Refresh()` (полное обновление)
- `PlayerVitals.OnVitalsChanged` → `RefreshVitals()`
- `HealthSystem.OnHealthChanged` → `RefreshHealth()`

Любые мутации через `ItemUseService` / эффекты автоматически обновляют UI.

### 2.4. От 3D-физики

`WorldItem` расщеплён:
- `WorldItemBase` (abstract, плагин) — общие поля + `TryPickup` + world-space подсказка.
- `WorldItem3D : WorldItemBase` (плагин, `[MovedFrom("WorldItem")]`) — `Collider`, `Rigidbody`, билборд к `Camera.main`. Содержит статические `Spawn*` фабрики.
- `WorldItem2D : WorldItemBase` (плагин) — `Collider2D`, `Rigidbody2D`, подсказка в плоскости XY.

Контракт с InventoryManager — через `IInventoryService` + статический `Inventory.Service`:
```csharp
public interface IInventoryService {
    void RegisterContainerRecursive(GridContainer c);
    bool TryEquipAnyMatchingSlot(ItemInstance item);
    IEnumerable<GridContainer> GetPickupContainers();
    void NotifyChanged();
}
```

`HoverPickup` расщеплён аналогично:
- `HoverPickup3D` (плагин, `[MovedFrom("HoverPickup")]`) — `Physics.Raycast` от мыши.
- `HoverPickup2D` (плагин) — два режима: `Mouse` (Physics2D.OverlapPoint) и `PlayerProximity` (Physics2D.OverlapCircle вокруг ссылки на игрока).

---

## 3. Конфигурация под проект — ✅ сделано (v0.1.0)

`Runtime/Config/InventoryConfig.cs`:

```csharp
[CreateAssetMenu(menuName = "Inventory/Inventory Config")]
public class InventoryConfig : ScriptableObject {
    public List<PocketEntry>         pockets;     // карманы (любого размера W×H)
    public List<EquipmentSlotEntry>  equipment;   // тип + accepts ItemType
    public HotbarConfig              hotbar;      // слоты хотбара + reservedHotkey
    public static InventoryConfig CreateDefault(); // Tarkov-like (4 кармана + 9 слотов)
}
```

`InventoryManager.config` — инспектор-поле. Если null, используется встроенный Tarkov-like дефолт через `InventoryConfig.CreateDefault()`. `Pockets[]`, Save/Load, UI работают с динамическим размером.

**Готовые пресеты** (Editor menu, кликнул — получил `.asset`):
- `Tools/Inventory/Create Config Preset/Tarkov-like` — 4 кармана 1×1, 9 equipment-слотов, хотбар 10 слотов.
- `Tools/Inventory/Create Config Preset/Diablo-like` — 0 карманов, 5 equipment-слотов, хотбар 8 user-слотов.
- `Tools/Inventory/Create Config Preset/Minecraft-like` — 1 карман 9×3, 2 armor-слота, хотбар 9 user-слотов.

---

## 4. Сохранение — ✅ сделано (v0.1.0)

`Runtime/Persistence/`:

```csharp
public interface ISaveProvider {
    void Write(string key, string contents);
    string Read(string key);
    bool Exists(string key);
    void Delete(string key);
}

public class FileSaveProvider : ISaveProvider { /* persistentDataPath, .json */ }

public static class InventoryPersistence {
    public const string InventoryKey = "inventory";
    public const string LayoutKey    = "inventory_layout";
    public static ISaveProvider Provider { get; set; } // default = FileSaveProvider
}
```

`InventoryManager.Save/Load` и `InventoryLayoutPersistence` дёргают `InventoryPersistence.Provider`. Подменить — одна строка:
```csharp
InventoryPersistence.Provider = new MyCloudSaveProvider();
```

---

## 5. UI — ⏳ TODO

Сейчас `InventoryUI` строит Canvas программно через legacy `UnityEngine.UI`. Это самодостаточно, но мешает кастомизации.

Стратегия:
- **uGUI prefab-mode** — выложить `InventoryCanvas.prefab` в `Samples~`. `InventoryUI` его инстансит. Поля `[SerializeField]` — кастомизатор подменяет префаб.
- **UI Toolkit variant** (опционально, v1.1) — `Samples~/UIToolkit/` с `.uxml` + `.uss`. Общий `IInventoryView` интерфейс, две реализации.

Layout customization (`InventoryLayoutCustomization`) останется, но привяжется к `RectTransform`-якорям префаба, а не к `InventoryLayout` SO с хардкод-полями.

---

## 6. 2D / 3D — ✅ единая поверхность (v0.1.0)

| Аспект | 3D | 2D |
|---|---|---|
| Pickup-raycast | `Physics.Raycast` | `Physics2D.OverlapPoint` / `OverlapCircle` |
| Дроп с физикой | `Rigidbody.AddForce` | `Rigidbody2D.AddForce` |
| Подсказка над предметом | World-space Canvas, billboard к камере | World-space Canvas в плоскости XY |
| Иконка в UI | `ItemPreviewRenderer`: `icon` → off-screen рендер 3D-префаба | `ItemPreviewRenderer`: `icon` → спрайт из `SpriteRenderer` префаба |
| Префаб в мире | `definition.worldPrefab3D` | `definition.worldPrefab2D` |
| Переключатель | `ItemDefinition.worldMode = ThreeD` | `ItemDefinition.worldMode = TwoD` |
| Дроп-диспатчер | `WorldItemSpawner.SpawnDropped` → `WorldItem3D.SpawnWithVelocity` | `WorldItemSpawner.SpawnDropped` → `WorldItem2D.SpawnWithVelocity` |

Per-предмет переключатель `WorldMode` снимает старый баг «дроп в 2D-сцене становится 3D-кубом».
`worldPrefab` → `worldPrefab3D` мигрировано через `[FormerlySerializedAs]`, существующие `.asset`-ы
читаются без изменений.

Editor-утилиты тоже расщеплены:
- `Tools/Inventory/Spawn Inventory On Player (3D)` / `(2D)` — выбирает HoverPickup3D vs HoverPickup2D.
- `Tools/Inventory/Make Pickup From Selected — 3D` (`Ctrl+Alt+P`) / `— 2D` (`Ctrl+Alt+Shift+P`) — выбирает Collider/Rigidbody/WorldItem вариант.
- `Tools/Inventory/Make Container From Selected — 3D` / `— 2D` — статичный ящик: Collider + `WorldContainer3D/2D` (без Rigidbody).

---

## 7. Чек-лист v1.0

- [x] Перенести Core (`GridContainer`, `ItemInstance`, `ItemDefinition`, `ItemType`) в asmdef без зависимостей от Vitals/Health. (`Runtime/Core` + `Runtime/Equipment`)
- [x] `IItemEffect` + ScriptableObject-based эффекты, отвязать от `PlayerVitals`/`HealthSystem`. (`Runtime/Effects/`)
- [x] События вместо `InventoryUI.Instance.Refresh()`.
- [x] `ISaveProvider` + дефолт File-based. (`Runtime/Persistence/`)
- [x] `InventoryConfig` SO — убрать хардкод 4 карманов / 9 слотов. (`Runtime/Config/`)
- [x] `WorldItem` → `WorldItemBase` + `WorldItem3D` + `WorldItem2D`. (`Runtime/World/`, `[MovedFrom]`)
- [x] `HoverPickup3D` + `HoverPickup2D`.
- [x] `Hotbar` — generic, через `InventoryConfig`. (`Runtime/Hotbar/`)
- [x] Editor 2D/3D-режимы для InventorySpawner и PickupConverter.
- [x] Пресеты `InventoryConfig` (Tarkov / Diablo / Minecraft-like) — Editor menu.
- [x] Edit-mode тесты для `GridContainer` — 15/15 passing.
- [x] Sample-сцены через editor-builder: `Tools/Inventory/Samples/Build {3D, 2D} Sample`. Создаёт чистую `.unity` в `Assets/_InventoryPlug/Scenes/`, расставляет камеру/игрока/3 пикапа, вешает `InventoryRig` через `InventorySpawner` в нужном режиме, привязывает `InventoryConfig_{TarkovLike, DiabloLike}`. Запустить Play → подобрать пикапы (F), Tab — открыть инвентарь.
- [x] **Per-предмет `WorldMode` (2D / 3D)** — `ItemDefinition.worldMode` + `worldPrefab3D` / `worldPrefab2D` / `ActiveWorldPrefab`. Дроп идёт через `WorldItemSpawner.SpawnDropped` (диспатчер).
- [x] **Мульти-инвентарь (BG-style тактика).** `InventoryManager` теперь мульти-инстанс с `OnActiveChanged` + `SetActive`. `RTSUnitInventory` создаёт data-only менеджер на каждом юните. `RTSCommander.Select` свапает активный. UI и хотбар ребилдятся на `OnActiveChanged`.
- [x] **Тогл и редактируемый размер хотбара.** `HotbarConfig.enabled` + `HotbarConfig.slotCount` + сценовые `WeaponHotbar.hotbarEnabledOverride` / `slotCountOverride`. Live-edit в инспекторе во время игры — поддерживается.
- [x] **Редактируемое RMB-меню в инвентаре.** `ContextMenuUI.entries` (Use / Inspect / Open / Split / Drop) + `extraEntries`; кнопки пересобираются на каждый `Show()`.
- [x] **Система стаков предметов.** `ItemDefinition.canStack` + `maxStackSize` → computed `MaxStack`. `ItemInstance` получил `IsStackable` / `MaxStack` / `FreeStackSpace` / `CanStackWith` / `MergeFrom`; `GridContainer.TryStackInto`. Слияние **везде**: авто-долив при подборе из мира (`WorldItemBase.TryPickup`), слияние при drag-drop на стак того же типа. Split — контекстное меню «Разделить» + `Shift`+drag (отделяет половину). Бейдж `×N` в углу ячейки при `stackCount > 1`. Контейнеры не стакаются (`nestedContainer == null`).
- [x] **Generic ПКМ-меню в мире.** `WorldContextMenu` (универсальное) + `WorldPickupContextMenu` (привязан к `HoverPickup`-событиям пакета, редактируемые `Осмотр`/`Подобрать`). HoverPickup имеет `CurrentItem` / `OnRightClickedItem` / `SuppressHoverUpdates`.
- [x] **Мировые контейнеры (ящики/сундуки).** Статичные `WorldContainer3D`/`WorldContainer2D` (база `WorldContainerBase`) — не подбираются, хранят свой `GridContainer` с настраиваемым числом ячеек. Камерный хват обобщён на `IWorldInteractable` (реализуют и `WorldItemBase`, и `WorldContainerBase`); `HoverPickup3D/2D` получили `OnRightClickedContainer`. Открытие — `IInventoryService.OpenContainerWindow` (панель игрока + окно ящика, Tarkov-style); ПКМ «Открыть» через `WorldPickupContextMenu`. Утилита `Tools/Inventory/Make Container From Selected — 3D|2D`. Содержимое — только runtime/сессия; персист (save/load мировых ящиков) — будущая задача.
- [x] **Подключаемые модули.** Hunger/Thirst/Health расщеплены на отдельные `MonoBehaviour`-системы + HUD. `PlayerModuleManager` собирает их через `IPlayerModule`. Удалил компонент с `InventoryRig` — отрубилась логика и UI-бар.
- [x] **Превью предметов из world-префаба.** `ItemPreviewRenderer` (static) рендерит/кеширует спрайт по `ItemDefinition`: 3D — off-screen снимок меша, 2D — спрайт из `SpriteRenderer` префаба. Используется в сетке, экипировке, хотбаре и ghost'е перетаскивания вместо статичной `icon`.
- [x] **Ресайз окна инвентаря в edit-режиме.** `UIResizer` — угловая ручка, тянет окно при `LayoutEditMode.IsActive`, сохраняет размер/позицию в JSON.
- [ ] **Префаб-based UI (uGUI)** — главный оставшийся крупный пункт.
- [ ] (v1.1) Перенести sample-сцены в UPM `Samples~/` папку пакета, чтобы импортировались через Package Manager.

## v1.1 (опционально)

- [ ] Тесты `InventoryManager` (Save/Load round-trip, `MoveItem` rollback).
- [ ] Play-mode smoke-тест: spawn `WorldItem3D` → `TryPickup` → виден в инвентаре.
- [ ] UI Toolkit вариант UI.
- [ ] Дополнительные `ISaveProvider`: `PlayerPrefsSaveProvider`, `MemorySaveProvider` (тесты), Cloud-save sample.
- [ ] Inspector-окно для редактирования `InventoryConfig` визуально.
- [ ] Drag-on-mobile (touch) — проверить, что `PointerEventData`-стек работает.
- [ ] Локализация (через `LocalizedString` или адаптер).
- [ ] Тесты `WorldItem2D` / `HoverPickup2D` (Physics2D-сцены, требуют PlayMode).

---

## 8. Тесты

✅ Edit-mode unit-тесты `GridContainer` (15/15 passing) — `Packages/com.gridinventory.inventory/Tests/Editor/GridContainerTests.cs`:
- `TryPlace` — границы, занятые ячейки, замещение в той же позиции.
- `Rotate` — swap W/H, фильтр canRotate, помещение после поворота.
- `IsContainedWithin` — самовложенность контейнера, цикл outer→inner→outer.
- WeightLimit — `0 = unlimited`, отказ при overweight, accumulation across items.
- `Remove` — освобождение клеток, `GetItemAt` после remove.

⏳ Осталось (v1.1):
- Edit-mode тесты `InventoryManager`: Save → Load round-trip (с nested-контейнерами), `MoveItem` rollback при неудачном `TryPlace`.
- Play-mode smoke-тест: spawn `WorldItem3D` → `TryPickup` → виден в инвентаре.

---

## 9. Открытые вопросы

1. **Inspector workflow.** Сейчас предмет = SO в проекте. Для крупного проекта это сотни ассетов. Опция — рантайм-каталог из JSON/CSV. За рамками v1.
2. **Networking.** Полностью out-of-scope для v1; но в API уже минимизирован static state — `Inventory.Service` можно подменить, `InventoryPersistence.Provider` тоже. Singleton остаётся только в `InventoryManager`/`InventoryUI`/`WeaponHotbar` — у них Instance-поле, но плагин-код через них не работает.
3. **Совместимость с UI Toolkit прицельно** — может быть отдельным пакетом-аддоном, чтобы Core оставался лёгким.
4. **Имя пакета и vendor scope.** `com.gridinventory.inventory` — рабочее. Нужно решить до публикации в OpenUPM/Asset Store.

---

## 10. Что НЕ переносим в пакет

- `PlayerVitals`, `HealthSystem`, `HealthConfig`, `HealthHUD`, `VitalsHUD`, `BodyPartType` — игровые механики, не инвентарь.
- `PlayerController`, `FreeLookCamera`, `HoverOutline`, `CustomCursor*` — общий код проекта.
- `ConsumableEffect`, `IHungerThirstTarget`, `IHealthTarget`, `ItemUseService` — game-specific эффекты и адаптеры. Остаются как пример пользователя плагина.
- FPS-специфичные компоненты (анимация, recoil) — Sample может их использовать, но Core — нет.
