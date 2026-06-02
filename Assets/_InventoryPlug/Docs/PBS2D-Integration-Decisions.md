# PBS_2D ↔ `_InventoryPlug` — зафиксированные решения по интеграции

> Этот файл — **decision record**. Здесь зафиксирован выбранный путь интеграции
> и принятые архитектурные решения. Анализ вариантов (A/B/C, плюсы/минусы) —
> в [`Assets/PBS_2D/Docs/Integration-Inventory.md`](../../PBS_2D/Docs/Integration-Inventory.md).
> Документация PBS_2D — в `Assets/PBS_2D/Docs/`. Документация инвентаря —
> [`Inventory.md`](Inventory.md), [`CLAUDE.md`](CLAUDE.md).
>
> Зафиксировано: 2026-06-02.

---

## 0. TL;DR

Берём **Вариант A** — инвентарь (`_InventoryPlug`) как хост инвентарной логики,
PBS_2D как «руки/тело» (физический персонаж + IK + ragdoll + физическое оружие).
Связь — один тонкий адаптер на игроке. **Код PBS_2D и UPM-пакета не трогаем.**

---

## 1. Принятые решения

| # | Вопрос | Решение |
|---|---|---|
| Охват | Что унифицируем | **Полная унификация (Вариант A).** Оружие, патроны, расходники, ящики — всё через инвентарь. Оружие подбирается/выдаётся через hotbar, PBS_2D работает как «руки». |
| Тело игрока | Чей персонаж основной | **PBS_2D `Character`** (ragdoll + IK + физическое оружие). `InventoryRig` вешается слоем сверху. Sample-`PlayerController` инвентаря не используется. |
| Q1 | Состояние оружия при переключении слота хотбара | **Сохранять.** Патроны в магазине + режим огня переживают переключение. |
| Q2 | Модель патронов | **По калибрам.** Отдельные `ItemDefinition` на калибр (`Ammo_9x19`, `Ammo_762x39`…), патроны подходят только к своему стволу. |
| Q4 | Кто рисует HUD патронов/режима огня | **Инвентарный hotbar UI.** Штатный `WeaponUI` PBS_2D глушим (no-op-приёмник, чтобы `EquipWeapon` не падал на NRE). |
| Q5 | Кнопка Drop (`G`) | **Да** — привязываем к «выбросить активный hotbar-предмет». В PBS_2D `Drop` объявлен в `.inputactions`, но не подключён — подписку добавляет адаптер. |
| Q6 | Клавиша подбора | **Унифицировать на `F`** (как в инвентаре). PBS_2D `InteractionHandler` (`E`) глушим. |

---

## 2. Архитектура

### 2.1. Новые типы (всё в `Assets/_InventoryPlug/Scripts/Integration/PBS2D/`)

| Тип | Роль | Статус |
|---|---|---|
| `PBS2D_InventoryBridge` (MonoBehaviour, на игроке) | Главный мост. Подписки на `WeaponHotbar.OnActiveChanged` и `InventoryManager.OnInventoryChanged`; владеет живыми Gun-объектами и их состоянием; синхронизирует резерв патронов; обрабатывает drop по `G`; глушит `InteractionHandler`. | ✅ написан |
| `InventoryWeaponBinding` (MonoBehaviour, на оружейном префабе) | Связь «ствол → калибр»: поле `ItemDefinition ammoDefinition`. Мост читает после спавна. Без правок UPM-пакета. | ✅ написан |
| ~~`InventoryWeaponUIAdapter : WeaponUI`~~ | **Отменён.** При чтении исходника выяснилось, что `WeaponUI.UpdateAmmoUI()/UpdateFireModeIcon()` **не `virtual`**, а `EquipWeapon` зовёт их через статический тип `WeaponUI` — подкласс перехватить вызовы не может. См. 2.4. | ❌ не нужен |

Файлы: `Assets/_InventoryPlug/Scripts/Integration/PBS2D/` (без отдельного asmdef —
оба нужных asmdef'а PBS_2D и пакета `autoReferenced`, а game-side `InventoryManager`/
`WeaponHotbar` лежат в `Assembly-CSharp`; отдельный asmdef не смог бы на них сослаться).

### 2.2. Сохранение состояния оружия

Неактивные стволы **не уничтожаются** — держатся `SetActive(false)` под игроком,
ключ — `ItemInstance.instanceId`. Словарь `Dictionary<string instanceId, GameObject>`
в мосту. Повторный equip берёт тот же объект — `CurrentLoadedAmmo` и
`CurrentFireMode` живут на самом `Gun`, поэтому переживают переключение
автоматически. Объект уничтожается, только когда предмет реально покинул
инвентарь (drop / удаление из слота).

> Save/Load этого рантайм-состояния (магазин + режим) — **будущая задача**:
> отдельный DTO, сериализуемый рядом с инвентарём. Для v1 состояние живёт
> только в сессии.

### 2.3. Патроны по калибрам

- Патроны — обычные стакающиеся `ItemDefinition` (`canStack=true`).
- Каждый оружейный префаб несёт `InventoryWeaponBinding.ammoDefinition` (его калибр).
- Синхронизация резерва: `Gun.CurrentReserveAmmo = Σ stackCount` всех `ItemInstance`,
  у которых `definition == ammoDefinition`, по всем контейнерам инвентаря.
- После reload/выстрела `CurrentReserveAmmo` уменьшается — мост ловит дельту
  (поллинг по кадру у активного `Gun`, т.к. событий у `Gun` нет) и списывает
  столько же единиц патронов из инвентаря.

### 2.4. HUD

**Реальность:** `WeaponUI.UpdateAmmoUI()` / `UpdateFireModeIcon()` не `virtual`,
а `WeaponManager.EquipWeapon` вызывает их через `WeaponUI.Instance` (статический
тип `WeaponUI`). Перехватить подклассом нельзя. Кроме того, `WeaponUI.Instance`
**обязан** существовать в сцене — иначе `EquipWeapon` падает на NRE.

**Решение (v1):** в сцене лежит штатный `WeaponUI` (с заполненными TMP-полями),
размещённый в зоне инвентарного/hotbar-HUD — он и есть единственный показ патронов
(отдельного боевого HUD PBS_2D в сцене нет). Мост дополнительно толкает в него
актуальные данные (`Gun`, резерв из инвентаря, режим огня) через
`RefreshWeaponUI()` после экипировки и на изменения инвентаря. Это «один источник
истины в зоне инвентаря» без правок кода PBS_2D.

> Если в будущем нужно встроить ammo-readout **прямо в `WeaponHotbarUI`**
> (а не отдельным `WeaponUI`) — это follow-up на стороне инвентарного UI.

### 2.5. Подбор и ящики

- `HoverPickup2D` в режиме `PlayerProximity` (anchor — PBS_2D игрок) обслуживает
  и `WorldItem2D` (пикапы), и `WorldContainerBase` (ящики/сундуки) через общий
  контракт `IWorldInteractable`.
- PBS_2D `InteractionHandler` глушим (`enabled=false` на префабе `Character`).
- Ящики/сундуки **уже готовы** в плагине: `WorldContainer2D` +
  `Tools/Inventory/Make Container From Selected — 2D`. Делать заново не нужно.

### 2.6. Drop

- Из инвентаря (контекст-меню / кнопка `G`) → `WorldItemSpawner.SpawnDropped`.
- Drop «по смерти» PBS_2D (ragdoll роняет `Weapon`) — мост ловит выпавший объект
  и конвертит в `WorldItem2D`, синхронит инвентарь.
  - Способ перехвата уточняется при реализации: подписка на смерть персонажа
    (`CharacterHealth`) **или** подкласс `Weapon`/`Gun`. Предпочтительно — без
    правок PBS_2D-кода.

---

## 3. Что трогаем / что не трогаем

**Не трогаем (black-box):**
- Код PBS_2D (`WeaponManager`, `Gun`, `Reload`, `Cycle`, `Character`, …).
- Код UPM-пакета `Packages/com.gridinventory.inventory/`.

**Новое (только в `Assets/_InventoryPlug/Scripts/Integration/PBS2D/`):**
- `PBS2D_InventoryBridge`, `InventoryWeaponBinding`, `InventoryWeaponUIAdapter`.

**Настройка в Editor (один раз, руками):**
- На каждый ствол: `ItemDefinition` с `weaponPrefab = <PBS gun prefab>`,
  `worldPrefab2D = <pickup-вариант>` (через `Make Pickup — 2D`), компонент
  `InventoryWeaponBinding`.
- `ItemDefinition` на каждый калибр патронов (`canStack=true`).
- Слои PBS_2D в Project Settings → Tags & Layers: `Gun`, `Weapon`, `Phasing`,
  `Outer Limb`.

---

## 4. План работ

1. **Код-модуль** (`Assets/_InventoryPlug/Scripts/Integration/PBS2D/`):
   `PBS2D_InventoryBridge` + `InventoryWeaponBinding` + `InventoryWeaponUIAdapter`
   (+ при необходимости `.asmdef` со ссылками на пакет и PBS_2D-сборку).
2. **Пилот**: настроить один ствол как `ItemDefinition` (weaponPrefab +
   worldPrefab2D + binding) + один калибр патронов.
3. **Demo-сцена**: PBS_2D игрок + `InventoryRig` + одна `WorldItem2D`-винтовка
   на полу + один `WorldContainer2D`-ящик. Сценарий: подбор (F) → hotbar →
   стрельба → reload (списание патронов из инвентаря) → переключение слота
   (состояние сохранилось) → drop (G) → ящик.

---

## 4a. Статус пилота (GunRangeTest, AK / 7.62×39) — собран и проверен

Зафиксировано 2026-06-02 на сцене `Assets/PBS_2D/Scenes/GunRangeTest.unity`.

**Создано:**
- `ItemDefinition` `Items2D/Weapon_AK47.asset` — `itemType=PrimaryWeapon`, 4×2,
  `weaponPrefab=AK-47.prefab`, `worldMode=TwoD`, `icon=ak-47`.
- `ItemDefinition` `Items2D/Ammo_762x39.asset` — `Generic`, 1×1, `canStack`,
  `maxStackSize=60`, `worldMode=TwoD`.
- `AK-47.prefab`: компонент `InventoryWeaponBinding.ammoDefinition = Ammo_762x39`.
- `Player.prefab` (на него ссылается `PlayerSpawner`): компонент `PBS2D_InventoryBridge`.
- Сцена: root `InventoryRig` (`InventoryManager`+config `InventoryConfig_TarkovLike`+
  `InventoryUI`+`PlayerModuleManager`+`WeaponHotbar`+`WeaponHotbarUI`, `itemDatabase`
  заполнен), на `Main Camera` — `HoverPickup2D` (Mouse, F) + `WorldPickupContextMenu`,
  пикапы `Pickup_AK47` + 2×`Pickup_Ammo_762x39` у точки спавна.

**Проверено в play-mode (скриптами):**
- Подбор через `HoverPickup2D`/F работает; PBS_2D `InteractionHandler` заглушён мостом (`enabled=false`).
- AK уходит в equipment-слот PrimaryWeapon; `SelectSlot(1)` → AK в руках PBS-персонажа (`AK-47(Clone)`).
- Резерв синхронизирован по калибру: `Gun.CurrentReserveAmmo == Σ stackCount(7.62×39)` (60 = 60).
- Списание из инвентаря: `ConsumeAmmo(20)` → инвентарь 110→90 (ровно −20); в живой игре резерв и инвентарь падают синхронно при стрельбе+перезарядке.

**Второй ствол (Glock-17 / 9×19) + пустые руки — добавлено и проверено (2026-06-02):**
- `Items2D/Weapon_Glock17.asset` — `itemType=Sidearm`, `weaponPrefab=Glock.prefab`,
  `icon=glock`; идёт в equipment-слот **Holster** → hotbar-слот «4».
- `Items2D/Ammo_9x19.asset` — `Generic`, `canStack`, отдельный калибр.
- `Glock.prefab` ← `InventoryWeaponBinding.ammoDefinition = Ammo_9x19`.
- Пикапы `Pickup_Glock17` + `Pickup_Ammo_9x19` слева от точки спавна.

**Пустые руки на «1» / H:** правка `WeaponHotbar.SelectSlot` — `Reserved`-слот с
`reservedPrefab == null` теперь **становится активным** (раньше был no-op), мост
получает null-префаб и снимает оружие. `reservedHotkey` (H) и цифра «1» (слот 0)
возвращают бойца к безоружному состоянию, как при спавне.

**Проверено в play-mode (один прогон, скриптом):**
- AK(«2», 7.62, резерв 60) ⇄ Glock(«4», 9×19, резерв 34) — калибры раздельные ✅
- Магазин AK выставлен в 7 → переключение на Glock и обратно → магазин AK = **7** ✅
  (парковка `SetActive(false)` по `instanceId` сохраняет состояние ствола).
- «1»/slot 0 → руки пустые (`Weapon == null`, ActiveIndex=0); обратно на «2» → магазин AK снова 7 ✅.

**Управление в сцене:** `F` — подобрать (навести мышь), `1`/`H` — пустые руки,
`2` — AK (PrimaryWeapon), `4` — Glock (Holster), ЛКМ — огонь, `R` — перезарядка
(тянет патроны соответствующего калибра из инвентаря), `G` — выбросить активный
предмет, `Tab` — инвентарь.

---

## 4b. Фикс: искажение модели при дропе (worldPrefab2D)

**Симптом:** выброшенное оружие сильно растягивалось.

**Причина:** у `ItemDefinition` оружия не был задан `worldPrefab2D`. `WorldItem2D.Spawn`
в этом случае строит fallback-спрайт и **масштабирует его трансформ по `width×height`**
(AK = 4×2) → спрайт вытягивается (подтверждено: `localScale=(4,2,1)`).

**Решение:** созданы pickup-префабы `Items2D/Pickups/WorldItem_AK47.prefab` и
`WorldItem_Glock17.prefab` (спрайт в нативном масштабе + `BoxCollider2D` +
`WorldItem2D`), прописаны в `ItemDefinition.worldPrefab2D`. Теперь и подбор, и дроп
инстанцируют один и тот же префаб → масштаб `(1,1,1)`, без искажений (проверено в play-mode).
Пикапы в сцене пересобраны из этих же префабов, поэтому «подобрал» == «выбросил».

> ⚠️ Правило для **любого нового оружия/предмета**: всегда задавай `worldPrefab2D`
> (для 2D) — иначе дроп исказит спрайт по grid-размеру. Для предметов 1×1 (патроны)
> искажения нет, но префаб всё равно желателен для единообразного вида.

---

## 5. Открытые/будущие задачи

- Save/Load рантайм-состояния оружия (магазин + режим огня) — отдельный DTO.
- Save/Load содержимого мировых контейнеров (в плагине пока только сессия).
- Поведение на мобильных (touch): `MobileControls` PBS_2D vs инвентарный UI —
  отдельная проработка, если нужна тач-сборка.
