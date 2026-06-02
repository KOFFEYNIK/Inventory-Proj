# Integration: `_InventoryPlug` ↔ PBS_2D

Анализ вариантов интеграции нашего инвентаря (`Assets/_InventoryPlug/`,
plus `Packages/com.gridinventory.inventory/`) с PBS_2D. Без кода —
только концепции, плюсы/минусы и рекомендация.

---

## 1. Что есть с каждой стороны

### PBS_2D (host)

- Один слот «текущее оружие» на персонаже: `WeaponManager.Weapon` /
  `WeaponManager.Gun`.
- Подбор — через `InteractionHandler` → `Interactable.Interact(character)`.
  Единственный реализованный `Interactable` — `Weapon`.
- Dropped weapon — это **сам `Weapon`-объект на сцене**, лежащий на
  Rigidbody2D, на layer `Phasing`, `IsDropped=true`. То есть никакой
  отдельной сущности «pickup wrapper» нет.
- Резерв патронов — `Gun.CurrentReserveAmmo : int`, число живёт на
  конкретном инстансе Gun.
- HUD патронов / режима огня — singleton `WeaponUI.Instance`, **жёстко на
  `Gun`**.
- Префабов оружия много (в `Assets/PBS_2D/Prefabs/Guns/`). Каждый имеет
  `Weapon`+`Gun`+`Rigidbody2D`+`SortingGroup`+`Outline`+(`Bolt`, `Forend`,
  `MagPrefab`, точки хвата и т.д.).
- Жёсткая привязка к слоям: `"Gun"`, `"Weapon"`, `"Phasing"`, `"Outer Limb"`.

### `_InventoryPlug` (guest)

- `ItemDefinition` (SO) с `itemType` (включая `PrimaryWeapon`, `SecondaryWeapon`,
  `Sidearm`), `weaponPrefab : GameObject`, `worldPrefab2D/worldPrefab3D`,
  `canStack/maxStackSize`, `consumable : ItemEffect`.
- `ItemInstance` — рантайм-экземпляр с `instanceId`, `stackCount`,
  `nestedContainer`.
- `GridContainer` — Tetris-style grid placement.
- `EquipmentSlot` с фильтром `AcceptedItemType` (`PrimaryWeapon`/
  `SecondaryWeapon`/`Holster`/...).
- `InventoryManager` (singleton) с пресетами `Tarkov/Diablo/Minecraft`.
- `WeaponHotbar` (singleton) — `Slots[]`, `ActiveIndex`,
  `GetActiveWeaponPrefab() : GameObject`, события `OnSlotsChanged/OnActiveChanged`.
- `WorldItem2D` — мировой объект с `definition`+`stackCount`,
  `TryPickup()`, static `Spawn(...)` / `SpawnWithVelocity(...)`.
- `HoverPickup2D` (на камере) — детектор пикапов: mouse или proximity.
- `IItemEffectContext` + `PlayerModuleManager` — для consumables
  (hunger/thirst/health).

### Главные конфликты

| Точка | PBS_2D ожидает | Инвентарь даёт |
|---|---|---|
| Подбор | `Interactable.Interact` на самом оружейном объекте | `WorldItem2D.TryPickup()` на собственном wrapper'е |
| Экипировка | `WeaponManager.EquipWeapon(Weapon)` — живой объект | `WeaponHotbar.GetActiveWeaponPrefab() : GameObject` — префаб для Instantiate |
| Резерв патронов | поле `Gun.CurrentReserveAmmo` на инстансе оружия | стакающийся `ItemInstance` отдельного `ItemDefinition` для патронов |
| Drop | `WeaponManager.DropWeapon()` отрывает живой объект на пол | мы хотим, чтобы в мире появился `WorldItem2D` |
| Hover | `InteractionHandler` (OverlapCircle вокруг руки + raycast) | `HoverPickup2D` (от мыши/проксимити вокруг камеры/игрока) |
| HUD | `WeaponUI.Instance` (только Gun) | Свой Inventory/Hotbar UI |
| Лимит активного оружия | 1 | N (hotbar) |

---

## 2. Карта вариантов интеграции

### Вариант A — **Адаптер поверх (минимальная инвазия)**

Идея: оставить PBS_2D почти нетронутым, добавить тонкий адаптер-MonoBehaviour
на `Character`, который:

1. Слушает `WeaponHotbar.OnActiveChanged`.
2. На смену слота — берёт `GetActiveWeaponPrefab()`, спавнит его рядом с
   персонажем (или из пула) и вручную вызывает
   `WeaponManager.EquipWeapon(spawned.GetComponent<Weapon>())`.
3. На предыдущее оружие — `WeaponManager.DropWeapon()` (PBS_2D отлично это
   умеет), а отвалившийся `Weapon`-объект **перехватываем сразу**: либо
   уничтожаем и пишем `ItemInstance.stackCount` обратно в инвентарь, либо
   парк-аём (deactivate + сохраняем рантайм-состояние Gun: ammo, fire mode).
4. На `Interactable.Interact` — переопределяем (наследник `Weapon`):
   вместо `EquipWeapon` зовём `InventoryManager.Active.TryEquipAnyMatchingSlot`,
   уничтожаем `Weapon`-объект из мира.
5. Резерв патронов — каждое экипирование/реquip синхронизирует
   `Gun.CurrentReserveAmmo = sum(stackCount всех совместимых ItemInstance)`.
   После выстрела/перезарядки — обратная синхронизация.
6. Drop из инвентаря в мир — `WorldItem2D.Spawn(definition,...)` через
   обычный API инвентаря, никаких PBS_2D-методов.
7. `WeaponUI` оставить как есть — он сам обновится из существующих хуков
   в `WeaponManager.EquipWeapon` / `Gun.Shoot`. Инвентарный UI работает
   рядом.

**Плюсы.**
- 0 правок в коде PBS_2D и `_InventoryPlug`. Всё в одном новом классе
  (например, `PBS2D_InventoryBridge` на player'е).
- При обновлении PBS_2D (autor выпустил v1.1) — мерж без боли.
- `InteractionHandler` (с его OverlapCircle вокруг руки) даже не нужен,
  если перевести пикап на `HoverPickup2D` / `WorldItem2D.TryPickup()`.
  Или можно оставить оба — PBS_2D-хайлайт работает, но `Interact` уходит
  в инвентарь.

**Минусы.**
- Двойной HUD: `WeaponUI` PBS_2D + наш hotbar UI. Если автор инвентаря/PBS
  оба хотят показывать патроны — рискуем дублированием. Решается тем, что
  отключаем `WeaponUI`-канвас в сцене.
- Префабы PBS_2D-оружия нужно положить в `ItemDefinition.weaponPrefab` —
  это надо сделать руками для каждого ствола. (Один раз.)
- Резерв патронов как stackable `ItemInstance` нужно поддерживать
  отдельным `ItemDefinition` на каждый калибр. Сейчас в PBS_2D «калибра»
  нет — `CurrentReserveAmmo` это абстрактное число. Придётся ввести
  поле «caliber» на ItemDefinition или на Gun (через адаптер-таблицу).
- Каждый цикл equip/drop переинстанцирует оружие — состояние Gun
  (`CurrentLoadedAmmo`, `CurrentFireMode`) нужно либо сохранять снаружи
  (на `ItemInstance` через дополнительное поле metadata), либо принимать,
  что при переключении hotbar патроны в магазине теряются.

### Вариант B — **Полная замена pickup-цепочки**

Идея: убрать PBS_2D-пикапы совсем. В мире оружие — только наши
`WorldItem2D` со ссылкой на ItemDefinition. На сцене и в префабах PBS_2D
никаких лежащих `Weapon`-объектов с `Outline` нет. Equip — через hotbar.

**Плюсы.**
- Один UX-канал для пикапа (без двух систем хайлайта).
- В инвентаре всё унифицировано — оружие, патроны, расходники.

**Минусы.**
- Демо-сцены PBS_2D перестанут работать «как есть»: в них оружие именно
  как `Weapon`-объекты на полу. Нужно переделать сцены.
- Тестирование становится дороже — нельзя «просто закинуть Glock-префаб
  на сцену».
- Все авторские туториалы по PBS_2D работают по-другому, чем у нас.

### Вариант C — **Глубокое переписывание `WeaponManager`**

Заменить `WeaponManager` собственной версией, которая:
- хранит словарь «слот → Gun instance»,
- принимает `EquipFromInventory(ItemInstance)`,
- сама инстанцирует оружие из префаба и кладёт в активный hotbar-слот,
- сама знает про калибры/резерв,
- сама обновляет наш HUD (отказ от `WeaponUI`).

**Плюсы.**
- Чистая архитектура без двойного состояния.
- Можно дать персонажу 2 оружия одновременно (primary+secondary) с
  переключением.

**Минусы.**
- `WeaponManager` — ~300 строк, переплетён со `Character`, IK, ragdoll,
  суставами. Перепишешь — придётся отлавливать пограничные кейсы
  (smerть с оружием, flip, reload-pose, aim-pose).
- Любой апдейт ассета от автора будет вызывать конфликты merge.
- При наличии Варианта A с теми же возможностями — это over-engineering.

---

## 3. Рекомендация

**Вариант A (адаптер) + 3 точечные правки.**

Рекомендация: пойти по Варианту A. Это режим «инвентарь как хост, PBS_2D
как black-box компонент». Но чтобы оно не было «костылём поверх костыля»,
понадобятся 3 точечные изменения уже в существующем коде:

1. **В новом `Weapon`-наследнике переопределить `Interact()`** (PBS_2D
   разрешает — `Interact` virtual через `Interactable`). Это уже описанный
   override-target.
2. **Подменить или отключить `WeaponUI.Instance` HUD.** Самый чистый
   способ — не класть `WeaponUI` префаб в сцену вовсе. Тогда
   `WeaponUI.Instance` залезет в `FindFirstObjectByType<WeaponUI>()` и
   вернёт null → `WeaponManager.EquipWeapon` упадёт на `NullReference`.
   Поэтому правильнее — оставить пустой `WeaponUI` без визуала (поля
   текста/иконки = null), он будет работать как no-op-приёмник. Или
   сделать subclass `InventoryWeaponUIAdapter : WeaponUI`, который
   перенаправляет `UpdateAmmoUI` в наш HUD.
3. **Расширить `ItemDefinition`** (или добавить отдельный SO «AmmoCaliber»)
   полем calibre/ammoType, чтобы синхронизация резерва между Gun и
   стакающимися патронами была однозначной. Делается в нашем коде,
   PBS_2D не трогаем.

Дальше — один адаптер-компонент на player'е (`PBS2D_InventoryBridge`):
- слушает `WeaponHotbar.OnActiveChanged` → `Equip/Unequip Gun`,
- слушает наши же пикапы (`HoverPickup2D` / `WorldItem2D.TryPickup`),
- синхронизирует `Gun.CurrentReserveAmmo` с инвентарём по событиям
  `InventoryManager.OnInventoryChanged`,
- на `WeaponManager.DropWeapon` (вызванном PBS_2D-кодом — например, на смерти)
  — перехватывает выпавший `Weapon`-объект и кладёт в инвентарь или спавнит
  как `WorldItem2D`.

### Что НЕ нужно делать на старте

- **Не нужно** убирать `InteractionHandler`. Его можно либо оставить
  параллельно работающим (он будет подсвечивать `Weapon`-объекты, если
  они в мире есть; если в мире только наши `WorldItem2D` — он просто
  ничего не найдёт), либо отключить `enabled=false` на `Character`-префабе.
- **Не нужно** переписывать `WeaponManager` — он отлично делает свою
  работу (IK, suspension, поза, ragdoll arm на drop). Адаптер ему
  даёт уже готовый `Weapon`-объект.
- **Не нужно** трогать `Gun.cs`, `Reload.cs`, `Cycle.cs`. Они не знают
  ни про инвентарь, ни про hotbar — только про `CurrentLoadedAmmo` /
  `CurrentReserveAmmo` / `Stats`. Это идеально для проксирования.

---

## 4. Открытые вопросы (решить до начала кода)

Эти решения сильно меняют дизайн адаптера. Их лучше зафиксировать заранее.

### Q1. Сохранение состояния `Gun` при переключении hotbar
- Если игрок переключился с AK (12/30 в магазине) на пистолет, потом
  обратно — должно ли в AK остаться 12 патронов?
  - **a)** Да, тогда состояние нужно куда-то сериализовать (вариант:
    добавить `Dictionary<instanceId, AmmoState>` в адаптере; или вообще
    не уничтожать неактивные `Gun`-объекты, держать deactivated).
  - **b)** Нет, перезарядка с нуля каждое equip — проще, но «по-аркадному».

### Q2. Что значит «calibre» в инвентаре
- В PBS_2D нет калибров — `CurrentReserveAmmo` это абстрактное число.
- **a)** Один универсальный «AmmoItem» (без типа). Тогда любые патроны
  кормят любое оружие. (Просто, но без оттенков.)
- **b)** N разных `ItemDefinition` для калибров (`Ammo_9x19`, `Ammo_762x39`),
  плюс поле `ammoDefinition` в новой обёртке вокруг `Gun`. (Более
  «реалистично», нужна доп. конфигурация.)

### Q3. Пикап dropped weapon (выпало на смерти)
- PBS_2D при смерти/дропе оставляет `Weapon`-объект на полу с `IsDropped=true`.
  Что с ним делать?
  - **a)** Адаптер ловит drop-событие (нужно либо переопределить `Weapon.Drop()`,
    либо подписаться на `WeaponManager.DropWeapon` через decorator) и
    сразу преобразует в `WorldItem2D.Spawn(definition, ...)`, уничтожая
    PBS_2D-обёртку.
  - **b)** Оставить как есть; `InteractionHandler` подберёт как раньше,
    но в этот момент адаптер должен перехватить `Interact()` и положить
    в инвентарь.
  - Вариант **a** даёт визуальную унификацию (везде наши `WorldItem2D`),
    но теряется красивая ragdoll-выкидываемая физика PBS_2D.

### Q4. UI HUD патронов — кто рисует
- **a)** Наш hotbar UI рисует, `WeaponUI` отключить.
- **b)** `WeaponUI` рисует, наш UI скрыт когда есть Gun.
- **c)** Двойной: каждый своё (рисково — два разных числа на одного игрока).

### Q5. Поведение `DropAction` (G)
- В .inputactions есть кнопка `Drop`, но PBS_2D её не использует.
  Хотим ли мы привязать её к нашему «выбросить активный hotbar-айтем»?
  Если да — добавить подписку в адаптер.

### Q6. Mobile controls + touchscreen
- `MobileControls` и `HoverPickup2D` (mouse-based) одновременно — будут
  конфликтовать. Если поддержка тача нужна — наш пикап должен иметь
  proximity-mode и кнопку Interact на UI.

---

## 5. План работы (если выбираем Вариант A)

Минимальный milestone-список, если запустим:

1. Положить префабы PBS_2D-оружия в новые `ItemDefinition` (вручную в Editor).
   Заполнить `weaponPrefab = <pbs gun prefab>`, `worldPrefab2D = <тот же или
   облегчённый dropped-вариант>`.
2. Решить Q1–Q6 (выше). Зафиксировать в `Assets/_InventoryPlug/Docs/PBS2D-Integration.md`.
3. Создать наследника `Weapon` (например, `InventoryAwareWeapon`) с
   override-ом `Interact()`.
4. Реализовать `PBS2D_InventoryBridge` MonoBehaviour:
   - подписки на hotbar/inventory,
   - метод `EquipFromInventory(ItemInstance)` → `Instantiate(prefab)` →
     `WeaponManager.EquipWeapon(...)`,
   - метод `OnDropFromInventory(ItemInstance, Vector2 velocity)` →
     `WorldItem2D.SpawnWithVelocity(...)`,
   - синхронизация резерва патронов.
5. Решить вопрос с `WeaponUI`:
   - либо `InventoryWeaponUIAdapter : WeaponUI` override,
   - либо отключённый канвас с пустыми ссылками + null-safe правки в нашем коде.
6. Проверить слои в проекте: `Gun`, `Weapon`, `Phasing`, `Outer Limb` —
   должны быть в Project Settings → Tags & Layers.
7. Сделать минимальную demo-сцену: персонаж + одна `WorldItem2D`-винтовка
   на полу + InventoryRig. Подобрать → hotbar → переключение → стрельба.

---

## 6. Альтернатива: использовать PBS_2D без инвентаря

Если выяснится, что для целевого UX (например, чистый аркадный шутер
типа Liero/Soldat) полнобанковый инвентарь — оверкилл, есть промежуточный
сценарий: оставить PBS_2D-пикап как есть, использовать инвентарь только
для не-оружейных предметов (medkit, food, бонусы). Это даже проще
Варианта A — у `_InventoryPlug` есть готовые `ConsumableEffect`,
`HungerSystem`, `HealthSystem`. Это можно подвесить рядом со «штатным»
PBS_2D-пикапом оружия и получить геймплей-петлю «здоровье/еда» при
неизменных стволах.

Этот режим стоит обсудить с заказчиком прежде чем кидаться писать
адаптер.
