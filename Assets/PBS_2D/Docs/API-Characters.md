# PBS_2D — API: Characters

Namespace: `PBS2D`. Все пути даны от `Assets/PBS_2D/Scripts/`.

> Соглашение: «Inspector» = поля, которые показываются в инспекторе.
> «Runtime» = `[System.NonSerialized] public` поля, состояние во время игры.

---

## `Character`  (`Characters/Character.cs`)

**Роль.** Центральный координатор персонажа. Хранит ссылки на все body-parts,
суставы, IK-solvers и подсистемы. Решает, что добавить на Awake — игрока
или AI. Все остальные скрипты дёргают его через `GetComponent<Character>()`
или поле `Character` в инспекторе.

**Inspector — флаги:**
- `IsPlayer : bool` — определяет, навешивать ли `PlayerInputHandler` +
  `InteractionHandler` или `AIBrain` на Awake.
- `AIBehavior : AIBehavior` — ScriptableObject AI; используется только при
  `IsPlayer = false`.
- `Skin : SkinConfig` — спрайты для всех body-parts.
- `GroundLayer : LayerMask` — что считать землёй.
- `IKManager : IKManager2D` — Unity 2D-IK оркестратор (на Start включается).
- `IKTargets : GameObject` — родитель для всех IK-target'ов
  (`FrontHandIKTarget`, `BackHandIKTarget`, `FrontFootIKTarget`, `BackFootIKTarget`).

**Inspector — body-parts (`BodyPartRef`):**
`Head`, `UpperTorso`, `MidTorso`, `LowerTorso`, `UpperFrontArm`, `LowerFrontArm`,
`UpperBackArm`, `LowerBackArm`, `UpperFrontLeg`, `LowerFrontLeg`, `FrontFoot`,
`UpperBackLeg`, `LowerBackLeg`, `BackFoot`. Каждый — структура с
`Rigidbody2D Rb`, `Transform Transform`, `SpriteRenderer Sr`, `Balance Balance`,
`BodyPart Part`.

**Inspector — суставы:**
- `HeadHinge`, `UpperTorsoHinge`, `MidTorsoHinge` : `HingeJoint2D`
- `RestHinge`, `AimHinge` : `HingeJoint2D` — две позы корпуса
  (idle и прицел), переключаются в `WeaponManager.ApplyWeaponState`.
- `FrontHandFixedJoint`, `BackHandFixedJoint` : `FixedJoint2D` — нужны для
  «зацепиться рукой за оружие» (используется в `DropWeapon(delay)`).

**Inspector — руки и ноги:**
- `FrontHand`, `BackHand` : `GameObject` — спрайты-кисти.
- `FrontHandIKTarget`, `BackHandIKTarget` : `Rigidbody2D` — IK-цели, в Equip
  перепарентируются под оружие.
- `FrontFootIKTarget`, `BackFootIKTarget` : `Rigidbody2D`
- `FrontFootBodyPart`, `BackFootBodyPart` : `BodyPart`
- `FrontFootDetection`, `BackFootDetection`, `FrontFootTargetDetection`,
  `BackFootTargetDetection`, `FrontLegDetection`, `BackLegDetection` :
  `GroundDetection` — серия raycast-пробников.

**Runtime — состояние:**
- `IsConscious : bool` — false на смерти / ragdoll. Большинство action'ов
  гейтятся этим флагом.
- `IsDead : bool` — true когда death-fade завершён.
- `IsGrounded`, `IsFrontFootGrounded`, `IsBackFootGrounded : bool`
- `IsFacingRight : bool` — направление взгляда (используется для зеркалирования суставов).
- `IsRunning`, `IsJumping : bool`
- `IsAiming`, `IsReloading`, `IsCycling : bool`
- `AimMode : AimMode` enum `{ WorldPoint, Direction }`
- `AimWorldPoint : Vector2`, `AimDirectionInput : Vector2`, `AimDirection : float`
- `HingeNormalInit, HingeAimDownInit : Vector2` — стартовые позиции
  `WeaponHolder.localPosition` в idle/aim.

**Runtime — кэшированные подсистемы (`[NonSerialized]`):**
`WeaponManager`, `Health`, `Movement`, `LegsController`, `BodyController`
(=`BodyPhysicsController`), `HeightController` (=`TorsoHeightController`),
`CharacterSkin`, `InteractionHandler`, `CharacterRotation`, `AIBrain`.
А также IK-solvers: `FrontArmSolver`, `BackArmSolver`, `FrontLegSolver`,
`BackLegSolver` (все `LimbSolver2D`).

**Lifecycle:**
- `Awake` — `CacheScripts`, `CacheBodyPartComponents`, инициализация IK,
  регистрация в `PlayerManager.Register(this)` если игрок, добавление
  `PlayerInputHandler` + `InteractionHandler` или `AIBrain`.
- `Start` — `CharacterSkin.ApplySkin`, `LegsController.PlaceLegsInitially`,
  `CharacterSkin.StartBlinking`.
- `FixedUpdate` — `CheckGrounded`.
- `OnDestroy` — `PlayerManager.Unregister(this)`.

**Публичные методы / события.** У самого `Character` публичных API почти нет
— почти всё внешнее проходит через `Movement`, `Health`, `WeaponManager`,
`InteractionHandler`. Внутри есть `Die(instant : bool)`, дёрги её только
через `CharacterHealth.TakeDamage`.

---

## `CharacterHealth`  (`Characters/CharacterHealth.cs`)

**Роль.** HP, bleed-over-time, триггер смерти.

**Inspector:**
- `MaxHealth : float [Min(0.001)]` (default 100)
- `MaxBleedRate : float [Min(0.001)]` (default 5)

**Runtime / public properties:**
- `Blood : float { get; }` — начальное 100, тикает в `BleedCoroutine`.

**Public methods:**
- `TakeDamage(damage : float, instant : bool)` — вычитает HP; если ≤0 → `character.Die(instant)`.
- `UpdateBleedRate(amountPerTick : float)` — добавляет к текущему bleed (cap = `MaxBleedRate`), стартует корутину если ещё не запущена.

**Lifecycle:**
- `Awake` — кэш Character, `Blood = 100`.
- `Start` — `_currentHealth = MaxHealth`.
- `Update` — скейлит `HeightController.Multiplier` в диапазоне 0.95–1.0 в зависимости от health-ratio (визуальный эффект «персонаж проседает»).

---

## `CharacterMovement`  (`Characters/CharacterMovement.cs`)

**Роль.** Бег/ходьба, прыжок с зарядкой, заднеходный коэффициент, кулдауны.

**Inspector:**
- `WalkSpeed : float` (2.5), `RunSpeed : float` (5), `JumpForce : float` (40)
- `_backwardSpeedMultiplier : float` (0.75)
- `_runCooldownDuration : float` (0.5)
- `_allowAirControl : bool` (false)
- `_horizontalJumpMultiplier : float` (1.5)
- `_jumpChargeDuration : float` (0.15)
- `_jumpCrouchDepth : float` (0.6)
- `_jumpCooldownDuration : float` (0.5)

**Runtime:**
- `moveInput : Vector2` — задаёт `Move(x)`. Внешний код пишет напрямую при необходимости.

**Public methods:**
- `Move(value : float)` — устанавливает `moveInput.x`. Авто-стоп бега если стик ниже 0.9.
- `Jump()` — инициирует jump-sequence (charge → impulse → cooldown).
  Гейтится: не crouching, не на cooldown, на земле, не в charge.
- `Run(context : InputActionPhase)` — Started/Canceled — старт/стоп бега;
  гейтится: stick совпадает с facing, не aiming, не crouching.
- `StartRunning()`, `StopRunning()` — низкоуровневые переключатели,
  синхронизируют пору оружия (`WeaponManager.HandleWeaponRunState/Idle`)
  и ноги (`LegsController.PlaceLegsAfterRun`).
- `JumpCooldown() : IEnumerator` — внутреннее.

---

## `CharacterRotation`  (`Characters/CharacterRotation.cs`)

**Роль.** Считает целевой угол прицеливания (для головы и оружия) и
переворачивает персонажа на 180° при необходимости.

**Inspector:**
- `WeaponHolderBalance : Balance` — Balance, который крутят на цель.

**Runtime:**
- `HeadRotation : float` — целевой угол головы; читает
  `BodyPhysicsController.RotateHead`.

**Lifecycle:**
- `Update` — `UpdateFacingRight`, `FlipCharacterIfNeeded`.
- `LateUpdate` — `LookTo` (считает угол, применяет к Balance и голове).

Нет публичных методов — взаимодействие через инспекторное поле
`WeaponHolderBalance` и runtime-чтение `HeadRotation`.

---

## `CharacterSkin`  (`Characters/CharacterSkin.cs`)

**Роль.** Применяет `SkinConfig` к спрайтам body-parts, моргание, смена
спрайтов рук под тип оружия.

**Inspector:**
- `_blinkInterval : float [Min(0.001)]` (5)
- `_blinkDuration : float [Min(0.001)]` (0.1)
- `_hitBlinkChance : float [Range(0,1)]` (0.35)

**Runtime (auto-cached):**
- `FrontHandSRenderer`, `BackHandSRenderer`, `FrontHandTargetSRenderer`,
  `BackHandTargetSRenderer : SpriteRenderer`.

**Public methods:**
- `ApplySkin()` — раскладывает спрайты из `Skin` по всем body-parts.
- `StartBlinking()`
- `SetHeadSprite(sprite : Sprite)` — переключает голову (Head0 idle / Head1 aim / Head2 dead).
- `HandleGetHit()` — шанс на «непроизвольное» моргание (`_hitBlinkChance`).
- `DefaultHands()`, `DefaultFrontHand()`, `DefaultBackHand()` — вернуть руки к idle-спрайтам, скрыть IK-target hands.
- `ChangeHandSprite(frontHand : bool, handIdx : int)` — выставить спрайт-руки под weapon-specific хват (индекс в `SkinConfig.Hands`).

---

## `SkinConfig`  (`Characters/SkinConfig.cs`)  — ScriptableObject

**Roll.** Набор спрайтов на персонажа.

**Поля:** `Head0/Head1/Head2`, `UpperTorso/MidTorso/LowerTorso`,
`UpperFrontArm/LowerFrontArm/FrontHand`, `UpperBackArm/LowerBackArm/BackHand`,
`UpperFrontLeg/LowerFrontLeg/FrontFoot`, `UpperBackLeg/LowerBackLeg/BackFoot`,
`Hands : Sprite[]` — 7 вариантов рук под разные хваты оружия.

CreateMenu: `Character / Skin Config`.

---

## `TorsoHeightController`  (`Characters/TorsoHeightController.cs`)

**Роль.** Spring-damper по высоте торса. Crouch/kneel-состояния, временные
уменьшения высоты (charge прыжка, попадание).

**Inspector — основное:**
- `_targetLayers : LayerMask`
- `_defaultGroundOffset : float` (2.05)
- `_maxFootExcess : float` (0.05)
- `_kneelingCooldown : float` (0.5)
- `_crouchCooldown : float` (0.5)
- `_walkSpringStrength` / `_walkDampingCoefficient` : float
- `_runSpringStrength` / `_runDampingCoefficient` : float

**Runtime:**
- `DesiredTorsoPos`, `CurrentTorsoPos : Vector2`
- `Multiplier : float` — глобальный множитель высоты (зависит от health,
  дыхания).
- `IsKneeling`, `IsCrouching : bool`.

**Public methods:**
- `Crouch()` — toggle. Стопает бег если входит в crouch.
- `TemporaryChangeHeight(targetReduction : float, duration : float)` —
  добавляет временный multiplier (например, 0.8 на 0.4s).

---

## `LegsController`  (`Characters/LegsController.cs`) — ~700 строк

**Роль.** Процедурная анимация ног и стоп. Перемещает IK-target'ы по дугам,
обходит препятствия, считает баланс.

**Inspector:**
- `_groundLayers : LayerMask`
- `_stepClip : AudioClip`
- `_stepOverObstacles : bool`
- `WalkStepSettings`, `RunStepSettings`, `CrouchStepSettings : StepSettings` —
  скорость, длина шага, высота, длительность для каждой походки.

**Runtime:**
- `FrontLegState`, `BackLegState : LegState` — структура с position, target,
  balance, флагом movement.

**Public methods:**
- `StopLeg(leg : LegState)`
- `MoveTargetToFeet()` — мгновенно поставить IK-target в текущую позицию
  стопы (emergency landing).
- `PlaceLegs(randomizeStance : bool)`
- `InverseLegsOrientation()` — поменять front/back местами при flip'е.
- `PlaceLegsAfterRun()`

**Lifecycle:**
- `FixedUpdate` — каждый кадр считает balance; если стойка перекошена —
  двигает «свободную» ногу.

---

## `InteractionHandler`  (`Characters/InteractionHandler.cs`)

**Роль.** Полишинг ближайшего `Interactable` вокруг `BackHand` + raycast
вперёд, подсветка через `Outline`, исполнение `Interact()` по кнопке.

**Inspector:**
- `_detectionRadius : float` (0.5)
- `_detectionRayDistance : float` (2.5)
- `_detectionInterval : float` (0.2)

**Public methods:**
- `HasInteractable() : bool`
- `InteractWithClosest()` — гейтится `IsConscious`. Зовёт `_closestInteractable.Interact(_character)`.
- `InteractWithPos(pos : Vector2)` — точечный raycast в `pos`.

**Lifecycle:**
- `OnEnable` — запускает `CheckClosestInteractableRoutine` (раз в `_detectionInterval`).
- `OnDisable` — корутина стопится, текущий highlight снимается.

**Логика поиска.**
1. `FindClosestCircle` — `Physics2D.OverlapCircleAll(handPosition, _detectionRadius)`, фильтр по `CanInteract()`, ближайшая `ClosestPoint` от руки.
2. Если ничего — `FindClosestRaycast` — raycast от руки по `PlayerInfo.mousePos` (или `PlayerInfo.controllerInput` если геймпад).

`InteractionHandler` пишется на персонажа динамически в `Character.Awake`
если `IsPlayer = true`. То есть «на NPC его нет» — это важно.

---

## `WeaponManager`  (`Characters/WeaponManager.cs`)

**Роль.** Удерживает текущее оружие. Управляет позой (idle/run/aim/reload),
суставом `WeaponHolder`, синхронизацией IK-targets к точкам захвата.

**Inspector:**
- `WeaponHolder : GameObject` — дочерний объект персонажа, имеет
  `RelativeJoint2D`.
- `_defaultRunOffset`, `_defaultAimDownOffset`, `_defaultReloadOffset : WeaponHoldOffset`
  — fallback-позы, если оружие говорит `UseDefaultPose = true`.

**Runtime:**
- `Weapon : Weapon` — текущее оружие или `null`.
- `Gun : Gun` — то же, но cast к Gun (null если не Gun).

**Public properties:**
- `IsHoldingWeapon : bool` ⇔ `Weapon != null`
- `IsHoldingGun : bool` ⇔ `Gun != null`

**Public methods:**

| Метод | Что делает |
|---|---|
| `Aim(InputActionPhase ctx)` | Started=включить прицел (стопит бег, ставит aim-pose), Canceled=выйти. |
| `EquipWeapon(Weapon weapon)` | Сначала `DropWeapon()` если уже что-то есть. Парентит weapon к `WeaponHolder`, цепляет `RelativeJoint2D.connectedBody`, ставит IK-targets рукам в `FrontHandPoint/BackHandPoint` оружия, вызывает `weapon.Equip(character)`, применяет idle/run/aim/reload позу. **Для игрока пишет `WeaponUI.Instance.Gun` и обновляет UI.** |
| `DropWeapon()` | Включает ragdoll рук, отвязывает все суставы, отпускает IK-targets, вызывает `Weapon.Drop()`, обнуляет `Weapon`/`Gun`. |
| `DropWeapon(float delay)` | «Падающее оружие при смерти» — ragdoll одной руки, фиксированный шарнир, через `delay` секунд → `DropWeapon()`. |
| `SwitchGunFireMode()` | `Gun.SwitchFireMode()` + UI. |
| `InvertWeaponPositionAndRotation()` | Зеркало позы (для flip персонажа). |
| `ChangeWeaponPosition(Vector2)` / `ChangeWeaponRotation(float)` | Прямое перепозиционирование шарнира; учитывает `IsFacingRight`. |
| `HandleWeaponIdleState(float correction)` | Применить idle-позу. |
| `HandleWeaponRunState()` | Применить running-позу. |
| `HandleGunAimState(float correction)` | Применить aim-позу + переключить голову на `Head1`. |
| `HandleGunReloadState()` | Применить reload-позу. |

**Lifecycle:**
- `Awake` — кэш `Character`, `RelativeJoint2D`, `InitWeaponFromChild` (если
  при старте сцены ребёнок `WeaponHolder` — `Weapon`, цепляем его).
- `Start` — если `IsHoldingWeapon` → `EquipWeapon(Weapon)` (по-настоящему
  применить позу/IK к уже стоявшему в иерархии оружию).

**Ключевые ограничения для интеграции:**
- `EquipWeapon` принимает ТОЛЬКО уже существующий объект в сцене —
  ассет не инстанцирует оружие из префаба.
- `WeaponUI.Instance.Gun = ...` идёт жёстко на singleton.
- При наличии текущего оружия `EquipWeapon` всегда сначала дропает старое
  на землю (вместо «положить в инвентарь»).

---

## `PlayerManager`  (`Characters/PlayerManager.cs`) — static class

**Роль.** Глобальный реестр живого игрока.

**Public static API:**
- `Player : Character { get; }`
- event `OnPlayerSpawned : Action<Character>`
- event `OnPlayerDespawned : Action<Character>`
- `Register(Character)`
- `Unregister(Character)`
- `ResetStatics()` — `[RuntimeInitializeOnLoadMethod]` для defaulta при
  выключенном Domain Reload.

Используется: `CameraManager` (следить), `AIBrain` (цель), `EnemySpawner`
(дистанция), HUD'ом.

---

## Тело (`Characters/Body/*`)

### `Balance` — каждая body-part имеет свой
- `TargetRotation : float`, `CurrentWeight : float`, `Active : bool`
- `_force : float` — torque-множитель.
- `SetBalanceWeight(w)`, `FadeOut(duration1, duration2, lastBalance)`,
  `FadeOut(duration1)`.
- FixedUpdate лерпает rotation rigidbody к `TargetRotation` × weight.

### `BodyPart` (база, тут общий damage-handling)
- `Character : Character`, `_damageMultiplier : float`, `_maxBloodCascades : int`, `_bloodCascadeLifetime : DynamicFloat`.
- `TakeDamage(RaycastHit2D hit, float damage, float bulletForce)` — virtual; применяет `damage × multiplier`. Спавнит blood-cascade и пишет в `Character.Health`.
- `FixLocalPosition()`, `CanSpawnBloodCascade() : bool`, `AddBloodCascade(BloodCascade)`.

### `Head : BodyPart`
- `TakeDamage` override: knockback + `damage × multiplier`, `instant = true` (мгновенная смерть на хедшот), `TemporaryChangeHeight(0.7, 0.4)`.

### `Leg : BodyPart`
- override: impulse в `LowerTorso` + `TemporaryChangeHeight(0.8, 0.2)`.

### `Torso : BodyPart`
- override: `damage`, knockback ×1.25 в точке попадания.

### `BodyPhysicsController` — большой оркестратор тела (~500 строк)
- Inspector: куча параметров — `_maxHeadAngle` (50), `_maxTorsoAngle` (20),
  углы idle/crouch торса, breathing (`_breathingSpeed`, `_breathingAmplitude`,
  `_upperTorsoBreathingScale`, `_aimBreathingScale`),
  `_velocityLeanMultiplier`, `_headAimOffset`, `_upperTorsoHeadFollow`,
  `_midTorsoHeadFollow`, пороги падения (`_torsoTiltThreshold` 40°,
  `_footDistanceThreshold` 0.6), позы падения, веса при fade-out.
- Public:
  - `ActivateBalance()`
  - `FadeOutBalance(float duration) : IEnumerator` — death fade
  - `DeactivateBalances()` — мгновенная смерть
  - `RagdollArms(bool ragdoll)`, `RagdollFrontArm(bool)`, `RagdollBackArm(bool)`
  - `RagdollLegs(bool, bool falling)`, `RagdollFrontLeg(bool)`, `RagdollBackLeg(bool)`
  - `FlipCharacter()` — зеркалит limits всех суставов, ноги, IK-targets.

### `FootAlignment`
- `_character`, `_rayLength` (0.25), `_groundLayer`, `_rotationSpeed` (20),
  `frontRayOffset`, `backRayOffset`.
- `Enabled : bool { get; set; }` — Character переключает по факту касания.
- `Update` — raycast'ит вниз, поворачивает стопу под нормаль земли.

### `GroundDetection`
- `_rayDistance` (0.4), `_targetLayers : LayerMask`.
- `IsGrounded() : bool` — единичный `Physics2D.Raycast` вниз.

---

## AI

### `AIBehavior` — ScriptableObject  (`Characters/AI/AIBehavior.cs`)
- `TickInterval [Min(0.05)]` (0.3)
- `ChaseTarget : bool`, `KeepDistance : float`, `WalkDistance : float`
- `AttackTarget : bool`, `MaxShootDistance : float`
- `RayCount [Min(1)]` (5), `RaySpreadAngle` (20°)
- `ReactionTime` (0.75), `BurstDuration` (0.5), `BurstPause` (0.5)
- `HitChance [Range(0,1)]` (0.5)

CreateMenu: `Character / AI Behavior`.

### `AIBrain : MonoBehaviour`  (`Characters/AI/AIBrain.cs`)
- Inspector: `Behavior : AIBehavior`.
- Динамически добавляется к Character если `IsPlayer = false`.
- Подписывается на `PlayerManager.OnPlayerSpawned/OnPlayerDespawned`.
- Read-only внешне; внутри — машина состояний: aim + trigger.

`Character.RollHit()` — используется AI-пулей для вероятностного попадания
(на основе `HitChance`).

---

## Спавнеры

### `EnemySpawner`  (`Characters/Spawning/EnemySpawner.cs`)
- `_enemyPrefab`, `_respawnDelay` (7.5)
- `_onlySpawnOneAtATime : bool` (true) — sequential vs interval
- `_spawnInterval` (5), `_minSpawnDistance` (3)
- `_gunDespawnTime` (30) — время жизни выпавшего ствола если никто не подобрал.

### `PlayerSpawner`  (`Characters/Spawning/PlayerSpawner.cs`)
- `_playerPrefab`, `_respawnDelay` (3).
- На Start запускает SpawnLoop, который ждёт смерти и спавнит заново.
