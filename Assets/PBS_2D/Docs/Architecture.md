# PBS_2D — архитектура

## 1. Главные «оси» ассета

PBS_2D — это **side-view 2D шутер на физике и 2D-IK**. Главные оси:

1. **Character** — персонаж как цепочка из 14 `Rigidbody2D` body-parts,
   соединённых `HingeJoint2D`. Поза и движение строятся не через
   `Animator`, а через
   - `Balance` на каждой части (spring-torque к целевому углу),
   - `LimbSolver2D` для рук/ног,
   - `LegsController` — процедурная анимация ходьбы (свинг ноги в дугу),
   - `TorsoHeightController` — spring-damper по высоте торса,
   - `BodyPhysicsController` — оркестратор: дыхание, лин, ragdoll-переходы.
2. **Weapon** — отдельный `Rigidbody2D`-объект на сцене, который физически
   «крепится» к WeaponHolder персонажа через `RelativeJoint2D`. Руки
   персонажа подтягиваются к `FrontHandPoint`/`BackHandPoint` на оружии
   через 2D IK targets, а не через бонды.
3. **Pickup-цикл** — `Interactable` (abstract) → `Weapon` (override).
   `InteractionHandler` на персонаже периодически сканирует
   `OverlapCircleAll` вокруг `BackHand` + raycast вперёд, подсвечивает
   ближайший `Interactable` через `Outline`, а нажатие кнопки Interact
   вызывает `_closestInteractable.Interact(_character)`.
4. **Input** — `PlayerInputHandler` сидит на самом `Character`, добавляется
   динамически в `Character.Awake` если `IsPlayer = true`. Никакого
   отдельного «PlayerController» MonoBehaviour для камеры/инпута нет —
   камера в `CameraManager` (singleton) сама находит игрока через
   `PlayerManager.Player`.
5. **AI** — `AIBrain` динамически добавляется к `Character` если
   `IsPlayer = false`, читает `AIBehavior` (ScriptableObject), смотрит на
   `PlayerManager.Player`, дёргает те же `Movement.Move/Run`,
   `WeaponManager.Aim`, `Gun.StartAttack/StopAttack` — то есть AI и Player
   ходят через один и тот же API персонажа.

## 2. Карта зависимостей (минимальный набор связей)

```
                  ┌─────────────────────────────────────────────┐
                  │  PlayerInputHandler  (только если IsPlayer)  │
                  │     ↑ GameControls (Input System)            │
                  └────────────┬────────────────────────────────┘
                               │ Move/Jump/Crouch/Run/Attack/
                               │ Aim/Reload/SwitchFireMode/Interact
                               ▼
        ┌─────────────────────────────────────────────────┐
        │                  Character                       │
        │  IsPlayer, IsConscious, IsDead, IsFacingRight,   │
        │  IsAiming, IsReloading, IsCycling, IsRunning,    │
        │  AimMode (WorldPoint|Direction), AimWorldPoint   │
        └─┬──────┬───────┬───────┬────────┬────────┬──────┘
          │      │       │       │        │        │
          ▼      ▼       ▼       ▼        ▼        ▼
       Movement Health  Skin   Rotation Height   Weapon
                                        Controller Manager
                                                  │
                                                  ▼
                            ┌──────────────────────────────┐
                            │     Weapon (=Interactable)    │
                            │   FrontHandPoint/BackHandPoint │
                            │   IdlePosition / RunOffset     │
                            └─────────────┬────────────────┘
                                          │ (cast)
                                          ▼
                              ┌──────────────────────┐
                              │         Gun           │
                              │  Stats (GunStats SO)  │
                              │  AudioConfig/EffectCfg│
                              │  ImpactConfig         │
                              │  ShootingPoint, Bolt  │
                              │  CurrentLoadedAmmo    │
                              │  CurrentReserveAmmo   │
                              └──────────────────────┘
```

Дополнительно вне Character:

- `InteractionHandler` (только на player) — добавляется в `Character.Awake`,
  ищет `Interactable` рядом → вызывает `Interactable.Interact(character)`.
- `AIBrain` (только на NPC) — добавляется в `Character.Awake`, читает
  `AIBehavior` SO.

## 3. Глобальные singleton'ы (статическое состояние)

Эти объекты ассет ожидает увидеть в сцене единственными:

| Singleton | Где | Что хранит |
|---|---|---|
| `PlayerManager` (static class) | — | `Player : Character`, `OnPlayerSpawned/OnPlayerDespawned` |
| `CameraManager.Instance` | префаб камеры | следование за игроком, ShakeCamera |
| `WeaponUI.Instance` | UI canvas | патроны/режим огня — **жёстко завязано на `Gun`**, не на абстрактном оружии |
| `AudioManager.Instance` | в сцене | `PlaySound(clip, pos, vol, delay)` |
| `ObjectPoolManager` (static) | в сцене (MonoBehaviour, но API статический) | `SpawnObject/ReturnObjectToPool` для всех эффектов, патронов, гильз |
| `SplatManager.Instance` | в сцене | укладывает blood-splats без overcrowding |
| `WorldTimeManager.Instance` | в сцене | `SetDayTime()/SetNightTime()` |
| `PauseMenu.Instance` | UI canvas | `OnMenuOpened`, `OnResetValues`, `OnApplyValues` |
| `MobileControls.Instance` | мобильный UI | джойстики (если есть touchscreen) |
| `Settings` (static class) | — | `LockActions`, `HoldDownAim`, `HoldDownCrouch`, `AutoCycle`, `DayTime`, `TracerRounds`, `BulletForceMultiplier`, `SlowMotionEffect`, `MasterVolume` |
| `PlayerInfo` (static) | — | `mousePos`, `mouseScreenPos`, `currentDevice`, `controllerInput`, `aimMode` |

Из этих синглтонов на интеграцию с инвентарём напрямую влияют только
`WeaponUI.Instance` (он жёстко знает про `Gun`) и `Settings.LockActions`
(блокирует ввод в т.ч. интеракции).

## 4. Поток данных: пикап оружия

```
[Tick каждые 0.2s]
  InteractionHandler.CheckClosestInteractableRoutine()
    └─ FindClosestCircle (OverlapCircleAll вокруг BackHand)
       или FindClosestRaycast (по AimDirection)
    └─ _closestInteractable.ShowOutline()   // подсветка через Outline

[Игрок жмёт Interact]
  PlayerInputHandler.Interact(ctx)
    └─ Character.InteractionHandler.InteractWithClosest()
       └─ _closestInteractable.Interact(_character)
          └─ Weapon.Interact(character) =
             character.WeaponManager.EquipWeapon(this)
             └─ если уже было оружие → DropWeapon()
             └─ если IsPlayer:
                  WeaponUI.Instance.Gun = weapon.GetComponent<Gun>()
                  WeaponUI.Instance.UpdateAmmoUI() / UpdateFireModeIcon()
             └─ Weapon = weapon; Gun = weapon.GetComponent<Gun>()
             └─ weapon.transform.SetParent(WeaponHolder)
             └─ RelativeJoint2D.connectedBody = weapon.Rigidbody2D
             └─ BodyPhysicsController.RagdollArms(false)
             └─ FrontHandIKTarget/BackHandIKTarget → child of weapon
                 (localPosition = Gun.FrontHandPoint/BackHandPoint)
             └─ weapon.Equip(character):
                  sortingLayer = "Gun"
                  rb.bodyType = Dynamic, mass = 0.1
                  ChangeHandSprite(true/false, FrontHandIdx/BackHandIdx)
                  layer = "Weapon" (player) / "Phasing" (AI)
                  Coroutine WaitToUnlock → IsLocked = false через 0.1с
             └─ ApplyWeaponState(idle/run/aim/reload)
```

**Важный факт:** `EquipWeapon` принимает **уже существующий `Weapon`-объект
в сцене**. WeaponManager НЕ умеет инстанцировать оружие из префаба. Это
ключевой пункт для интеграции с инвентарём, где оружие хранится как
`weaponPrefab : GameObject` внутри `ItemDefinition`.

## 5. Поток данных: выстрел

```
PlayerInputHandler.Attack(ctx)
  └─ Character.WeaponManager.Weapon.Attack(ctx)
     └─ Weapon.Attack(ctx) переключает StartAttack/StopAttack
        └─ Gun.StartAttack() → AttackCoroutine
           └─ Gun.Shoot():
              ├─ Spread = f(Stats.Accuracy)
              ├─ Stats.GetDamage(distance) — MinMaxCurve
              ├─ ProcessBullet(dir): raycast с пенетрацией,
              │     BulletImpact.HandleHit → BodyPart.TakeDamage
              ├─ AudioManager.PlaySound(AudioConfig.ShootClip)
              ├─ ObjectPoolManager.SpawnObject(EffectConfig.MuzzleFlash)
              ├─ CameraManager.Instance.Shake(EffectConfig.CameraShakeAmount)
              ├─ Recoil → Character.LowerTorso.Rb.AddForce(...)
              ├─ CurrentLoadedAmmo--
              └─ HandleCycle() (cock bolt / pump / next round)
              └─ if IsEquippedByPlayer: WeaponUI.UpdateAmmoUI()
```

`GunStats._hitDamage` — `ParticleSystem.MinMaxCurve`, читается через
`Stats.GetDamage(normalizedDistance)`. Damage ≈ `int(curve.Evaluate(dist))`.

## 6. Поток данных: dropped weapon на сцене

Когда оружие лежит на земле, оно — `Weapon`-компонент на `Rigidbody2D`-объекте
с `Outline`, `IsDropped = true`, на layer `"Phasing"` (без коллизий с
персонажами). `InteractionHandler` любого живого персонажа в радиусе
автоматически найдёт его, подсветит, и при нажатии Interact подберёт.

То есть **dropped weapon в PBS_2D и есть мировой объект** — нет промежуточной
сущности «pickup, который превращается в оружие при подборе». Это вторая
ключевая особенность для интеграции — наш `WorldItem2D` инвентаря логически
ближе, но не идентичен.

## 7. Жизненный цикл `Character` (укрупнённо)

| Состояние | `IsConscious` | `IsDead` | Что разрешено |
|---|---|---|---|
| Живой | true | false | всё |
| Получает урон | true | false | мерцание, отдача |
| `Die(false)` → fade-out | false | false | ragdoll начинается, ввод заблокирован |
| `Die(true)` мгновенно | false | false → true | мгновенный ragdoll |
| Полностью мёртв | false | true | оружие выпало, ввод заблокирован |

Все геймплейные методы проверяют `IsConscious` перед действием
(`InteractWithClosest`, `Move`, `Aim`, `Shoot`, `Reload` и т. д.).

## 8. Слои и теги, на которые ассет завязан

В коде хардкод-имена слоёв (через `LayerMask.NameToLayer`):
- `"Gun"` — sortingLayer для оружия в руках,
- `"Weapon"` — physics layer оружия игрока,
- `"Phasing"` — physics layer оружия NPC и dropped weapons (без коллизий с персонажами),
- `"Outer Limb"` — sortingLayer для backhand при равиппе.

Если в проекте этих слоёв нет, пикап/equip сломается тихо — `NameToLayer`
вернёт `-1`. **Перед интеграцией проверь, что эти слои существуют.**

## 9. Что НЕ присутствует в ассете

Полезный негативный список, чтобы не искать зря:

- Нет своего инвентаря — у Character ровно один слот «текущее оружие» (`WeaponManager.Weapon`). Нельзя «переключить на пистолет» — только подобрать новый.
- Нет save/load — `Settings` хранит часть значений в `PlayerPrefs`, но gameplay-состояние не сериализуется.
- Нет ammo как сущности — `CurrentLoadedAmmo`/`CurrentReserveAmmo` живут на инстансе `Gun`. При дропе они сохраняются на этом же объекте; при подборе обратно — резерв всё ещё там.
- Нет UI для нескольких оружий, hotbar'а, контейнеров, иконок.
- Нет нелетального оружия, гранат, ближнего боя — `Weapon` абстрактен, но единственная реализация — `Gun`.
- Нет networking-кода.
