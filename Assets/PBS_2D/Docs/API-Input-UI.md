# PBS_2D — API: Input & UI

Namespace: `PBS2D`. Все пути даны от `Assets/PBS_2D/Scripts/`.

---

## Input System

### `GameControls`  (`Input/GameControls.cs`, generated)
Сгенерированный C# из `GameControls.inputactions`. Использовать через
`var ctrl = new GameControls(); ctrl.Player.Enable();`.

**Action maps:**

| Map | Action | Type | Default binding |
|---|---|---|---|
| Player | `LookMouse` | Value Vector2 | Mouse position |
| Player | `LookStick` | Value Vector2 | Right stick |
| Player | `LookTouch` | PassThrough Vector2 | Touch position |
| Player | `Move` | Value Vector2 | WASD / left stick |
| Player | `Jump` | Button | Space / A |
| Player | `Crouch` | Button | Ctrl / B |
| Player | `Run` | Button | Shift / RT |
| Player | `Attack` | Button | LMB / RT |
| Player | `Aim` | Button | RMB / LT |
| Player | `Reload` | Button | R / X |
| Player | `SwitchFireMode` | Button | F / Y |
| Player | `Interact` | Button | E / LB |
| Player | `Drop` | Button | G / LB |
| Player | `Zoom` | Value Axis | Scroll / triggers |
| World  | `SlowMotion` | Button | T / Select (debug) |

> Заметка: `Drop` определён в .inputactions, но в коде `PlayerInputHandler`
> явная подписка на него **отсутствует** — то есть в текущей сборке кнопка
> «G — drop» не дёргается. Если нужен — добавляйте подписку самостоятельно.

---

### `PlayerInputHandler`  (`Input/PlayerInputHandler.cs`)

**Роль.** Мост Input System ↔ `Character`. Динамически добавляется в
`Character.Awake` если `IsPlayer = true`. Отдельных Inspector-полей нет.

**Static events** (могут пригодиться для UI):
- `OnKeyboardUsed : Action`
- `OnGamepadUsed : Action`
- `OnTouchUsed : Action`

Эти события стреляют когда определена смена устройства ввода. Используются,
например, `UIManager` для скрытия/показа `MobileControls`.

**Lifecycle:**
- `Awake` — кэш `Character` и `Camera.main`.
- `OnEnable` — `new GameControls()`, включает Player-map, подписки.
- `OnDisable` — отписки, dispose.
- `Update` — обновляет `PlayerInfo.mousePos` через `Camera.ScreenToWorldPoint`,
  сглаживает stick-input, синхронизирует `PlayerInfo` поля с `Character`.

**Маппинг колбэков (Player map → метод Character):**

| Action | Метод Character |
|---|---|
| `LookMouse` | `PlayerInfo.aimMode = WorldPoint; mouseScreenPos = value` |
| `LookStick` | `rawStickInput`, `aimMode = Direction` |
| `LookTouch` | `aimMode = WorldPoint; mouseScreenPos = value` (если не over UI) |
| `Move` | `Character.Movement.Move(x)` |
| `Jump` | `Character.Movement.Jump()` |
| `Crouch` | `Character.HeightController.Crouch()` |
| `Run` | `Character.Movement.Run(ctx)` |
| `Attack` | `Character.WeaponManager.Weapon.Attack(ctx)` (NRE если оружия нет!) |
| `Aim` | `Character.WeaponManager.Aim(ctx)` |
| `Reload` | `Character.WeaponManager.Gun.ReloadGun()` (NRE если не Gun) |
| `SwitchFireMode` | `Character.WeaponManager.SwitchGunFireMode()` |
| `Interact` | `Character.InteractionHandler.InteractWithClosest()` |

Гейты: `Settings.LockActions` блокирует всё; `Settings.HoldDownCrouch=false`
делает Crouch toggle'ом по первой кнопке.

---

### `WorldInputManager`  (`Input/WorldInputManager.cs`)

**Роль.** Глобальный input-менеджер (не для персонажа). Сейчас только Debug
slow-motion.

**Lifecycle:**
- `OnEnable` — включает World-map, подписка на `SlowMotion`.
- `OnDisable` — отписка, dispose.

**Зависимости:** `Settings.SlowMotionActive`, `Settings.SlowMotionEffect`,
`Settings.LockActions`, `Time.timeScale`.

---

### `PlayerInfo` (static)

Хранилище глобального input-состояния (используют `InteractionHandler`,
`CharacterRotation`, AI и др.):

- `mousePos : Vector2` — мировые координаты курсора/стика-цели.
- `mouseScreenPos : Vector2` — экранные.
- `currentDevice : InputDevice` — последнее активное устройство.
- `controllerInput : Vector2` — сглаженный stick.
- `controllerDirection : Vector2`
- `aimMode : enum { WorldPoint, Direction }`

Точное место объявления (если нужно поправить) — внутри `PlayerInputHandler.cs`.

---

## UI

### `WeaponUI`  (`UI/WeaponUI.cs`) — singleton

**Роль.** HUD с патронами и иконкой режима огня. **Жёстко завязан на `Gun`**.

**Public API:**
- `Gun : Gun` (public field) — текущее оружие игрока.
- `UpdateAmmoUI()` — перерисовать `loaded / reserve`.
- `UpdateFireModeIcon()` — перерисовать иконку режима.

**Singleton access:** `WeaponUI.Instance` (через `Singleton<T>`).

Дёргается из: `WeaponManager.EquipWeapon` (set Gun + Update*),
`WeaponManager.SwitchGunFireMode`, `Gun.Shoot`, `Reload.HandSnap`,
`Cycle.PushBolt` (если ammo меняется).

**Для интеграции** — это самый «токсичный» singleton: его нужно либо
подменить на адаптер (свой компонент, который реализует тот же интерфейс
доступа), либо принять как есть и обновлять рядом со своим инвентарным UI.

---

### `UIManager`  (`UI/UIManager.cs`)

**Роль.** Связывает Input-колбэки с `PauseMenu`, переключает видимость
mobile-controls по типу устройства.

**Public methods:**
- `EnableTouchControlCanvas()`
- `DisableTouchControlCanvas()`

---

### `PauseMenu`  (`UI/PauseMenu.cs`) — singleton

**Public:**
- `ToggleMenu()`, `OpenMenu()`, `CloseMenu()`
- `HandleReset()`, `HandleApply()`
- events: `OnMenuOpened : Action`, `OnResetValues : Action`, `OnApplyValues : Action`

`SettingElement<T>` слушает `OnResetValues`/`OnApplyValues` чтобы откатывать
или сохранять значения настроек.

Открытие меню выставляет `Settings.LockActions = true`.

---

### `MobileControls`  (`UI/MobileControls.cs`) — singleton

**Public:**
- `CustomSticks : CustomStickManager[]`
- static `IsTouchClaimedByStick(int id) : bool` — для `PlayerInputHandler.LookTouch`,
  чтобы не путать тач-прицел с тач-стиком.

### `CustomStickManager`  (`UI/CustomStickManager.cs`)
Обёртка над `OnScreenStick` — отслеживает finger-id, экспортирует
`OnPointerDown/Drag/Up`.

---

### `MenuSelector`  (`UI/MenuSelector.cs`)
Кнопка-переключатель меню. `SelectMenu()` показывает свой меню и
рассылает `OnUnselectMenus`.

### `SceneSelection`  (`UI/SceneSelection.cs`)
Наследник `SettingsMenu`, грузит сцены по имени (`GunRange`, `TestLevel`).

---

### Settings UI (`UI/Settings/*`)

| Класс | Что делает |
|---|---|
| `SettingsMenu` (abstract) | Базовый компонент для панели настроек, фокус на первой кнопке. |
| `SettingElement<T>` (abstract generic) | База одной настройки; событие `OnValueApplied`, метод `DiscardSetting()`. Синхронизируется с `PauseMenu.OnReset/OnApply`. |
| `SliderSetting` | Float-настройка через `Slider`. Inspector: `_minValue`, `_maxValue`, `_step`, `_percentage`. Методы `IncreaseValue()`/`DecreaseValue()`. |
| `ToggleSetting` | Bool. Метод `ToggleValue()`. |
| `AudioSettings : SettingsMenu` | Привязывает мастер-громкость к `Settings` и `AudioListener.volume`. |
| `GameplaySettings : SettingsMenu` | Привязывает 6 геймплейных тогглов/слайдеров (`AutoCycle`, `DayTime`, `TracerRounds`, `BulletForceMultiplier`, размер камеры, `SlowMotionEffect`). |
| `VideoSettings : SettingsMenu` | `_renderScale` слайдер на URP-asset. |

---

## `Settings`  (`Utility/Settings.cs`) — static class

Глобальный «бэк-офис» настроек. Часть значений хранится в `PlayerPrefs`.

| Поле | Тип | Назначение |
|---|---|---|
| `LockActions` | bool | runtime-флаг: блокирует ввод (Pause, диалоги). |
| `SlowMotionActive` | bool | runtime: текущий slow-mo. |
| `HoldDownAim` | bool | user pref: hold vs toggle для Aim. |
| `HoldDownCrouch` | bool | user pref: hold vs toggle для Crouch. |
| `AutoCycle` | bool | user pref: авто-передёрг затвора. |
| `DayTime` | bool | user pref: день/ночь (читает `NightLight`, `WorldTimeManager`). |
| `TracerRounds` | bool | user pref: показывать трассеры. |
| `BulletForceMultiplier` | float | user pref: масштаб физического импульса попаданий. |
| `SlowMotionEffect` | float | user pref: post-эффект во время slow-mo. |
| `MasterVolume` | float | user pref: громкость. |

Доступ — статический (`Settings.LockActions = true`). Никаких событий нет;
если нужно реагировать на смену — придётся слушать `PauseMenu.OnApplyValues`.
