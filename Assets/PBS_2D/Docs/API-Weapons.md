# PBS_2D — API: Weapons

Namespace: `PBS2D`. Все пути даны от `Assets/PBS_2D/Scripts/`.

---

## `Interactable`  (`Weapons/Interactable.cs`) — abstract

**Роль.** Базовый контракт «подбираемого» объекта. Требует `Outline`.

```csharp
public abstract class Interactable : MonoBehaviour
{
    public void AddOutline(GameObject target);
    public bool TryRemoveOutline(GameObject target);
    public void ShowOutline();
    public void HideOutline();

    public abstract bool CanInteract();
    public abstract void Interact(Character interactor);
}
```

В ассете есть **ровно один** наследник — `Weapon`. То есть подбирать сейчас
можно только оружие. Расширение интерактивов на двери/предметы — extension point.

---

## `Outline`  (`Weapons/Outline.cs`)

**Роль.** Подсветка-контур (создаёт дубликаты спрайтов с outline-материалом).

**Inspector:**
- `_outlineTargets : List<GameObject>` — стартовый набор подсвечиваемых.
- `_outlineMaterial : Material` — материал с шейдер-пропсом `_ShowOutline`.

**Public methods:**
- `AddOutline(GameObject target)` — добавить дубликат с outline-материалом.
- `TryRemoveOutline(GameObject target) : bool`.
- `ShowAll()`, `HideAll()` — выставить `_ShowOutline = 1/0` всем материалам.

Сам `Interactable` оборачивает эти методы один к одному.

---

## `WeaponHoldOffset`  (`Weapons/WeaponOffset.cs`) — struct

```csharp
[System.Serializable]
public struct WeaponHoldOffset
{
    public bool UseDefaultPose;
    public Vector2 PositionOffset;
    public float Rotation;
}
```

Если `UseDefaultPose = true`, `WeaponManager.ApplyWeaponState` подменит этот
offset на defaultOffset, который сидит у `WeaponManager` в инспекторе
(`_defaultRunOffset`, `_defaultAimDownOffset`, `_defaultReloadOffset`).

---

## `Weapon`  (`Weapons/Weapon.cs`) — abstract

**Роль.** Абстрактный класс «оружия». Требует `SortingGroup` + `Rigidbody2D`.
Наследует `Interactable`. Реализаций сейчас одна — `Gun`. Mêлee, ножи и т. п.
автор планирует.

**Inspector:**
- `FrontHandPoint : Transform`, `BackHandPoint : Transform` — точки IK для рук.
- `WeaponName : string` (default `"Weapon"`).
- `IdlePosition : Vector2` — базовое смещение в держателе.
- `RunOffset : WeaponHoldOffset`.
- `FrontHandIdx : int [HandIndex]`, `BackHandIdx : int [HandIndex]` — индексы в `SkinConfig.Hands[]`.

**Runtime (`[NonSerialized]`):**
- `character : Character` — кто экипировал.
- `IsLocked : bool` — true сразу после Equip, false через 0.1s.
- `IsDropped : bool` — true когда лежит на земле.
- `IsAttacking : bool` — общий флаг атаки.

**Public properties:**
- `IsEquippedByPlayer : bool` ⇔ `!IsDropped && character?.IsPlayer == true`.

**Public methods:**
- `CanInteract() : bool` override — `!Settings.LockActions && IsDropped`.
- `Interact(Character interactor)` override — `interactor.WeaponManager.EquipWeapon(this)`.
- `Attack(InputAction.CallbackContext ctx)` — Performed → `StartAttack()`,
  Canceled → `StopAttack()`. Гейтится `Settings.LockActions`.
- `virtual StartAttack()`, `virtual StopAttack()` — пусто, override-нуть в наследнике.
- `virtual Equip(Character character)` — поднимает в air, sortingLayer=`"Gun"`,
  `Rigidbody2D.bodyType=Dynamic, mass=0.1`, ставит спрайты рук под `FrontHandIdx`/
  `BackHandIdx`, `BackHandTarget.sortingLayer="Outer Limb"`, `WeaponHolderBalance.CurrentWeight=0.25`.
  layer = `"Weapon"` (player) или `"Phasing"` (NPC). IsLocked=true, через 0.1s → false.
- `virtual Drop()` — `StopAllCoroutines`, `IsDropped=true`, layer=`"Phasing"`,
  `transform.parent=null`, `mass=1`.

**Замечания для интеграции.**
- `Equip()` — virtual, можно override-нуть для пользовательской логики
  (например, отмена применения хват-спрайтов если они у инвентаря свои).
- `Drop()` тоже virtual; `Gun.Drop()` дополнительно сбрасывает положение
  затвора/магазина.
- `Interact()` жёстко вызывает `WeaponManager.EquipWeapon(this)` — это
  главное место для перенаправления на инвентарь.

---

## `Gun` : `Weapon`  (`Weapons/Guns/Gun.cs`) — ~560 строк

**Inspector — references:**
- `ShootingPoint : GameObject`, `EjectionPoint : Transform`,
  `ReloadHandPoint : Transform`, `CycleHandPoint : Transform`
- `Bolt : Transform`, `Forend : Transform`
- `MagPrefab : GameObject`, `ShellPrefab : GameObject`, `CurrentMag : GameObject`

**Inspector — config (ScriptableObjects):**
- `Stats : GunStats`
- `AudioConfig : GunAudioConfig`
- `EffectConfig : GunEffectConfig`
- `ImpactConfig : GunImpactConfig`

**Inspector — позы:**
- `AimDownOffset : WeaponHoldOffset`
- `ReloadOffset : WeaponHoldOffset`
- `ReloadHandIdx : int [HandIndex]` (default 6)
- `CycleHandIdx : int [HandIndex]` (default 3)

**Inspector — cycle/reload tuning:**
- `OpenBoltWhenEmpty : bool`
- `BoltClosedSafeGuard : bool`
- `CycleHandMoveDistance : float` (-0.25)
- `BoltMoveAmount : float` (0.1875)
- `MagInsertDepth : float` (0.0625)
- `ShellInsertDepth : float` (0.1875)
- `ShellHandOffset : Vector2`, `HandToBodyOffset : Vector2`

**Runtime — ammo и состояние:**
- `CurrentLoadedAmmo : int` — патронов в магазине + патроннике.
- `CurrentReserveAmmo : int` — в резерве (карманах) — но в самом PBS_2D
  «карманов» нет, это просто число.
- `CurrentFireMode : GunTriggerMode` — `Auto | Semi | Burst` flags.
- `BoltInitialPos : Vector2` — стартовая позиция затвора (кэш).
- `IsBoltOpened`, `IsCycled : bool` — состояние механики.
- `AbortReload : bool` — флаг прервать перезарядку.
- `BoltCoroutine`, `CycleCoroutine : Coroutine` — текущие активные.
- `MagRB : Rigidbody2D` — текущий магазин (rigidbody).
- `NewShell : SpriteRenderer` — текущий загружаемый патрон (shell-reload).

**Public methods:**
- `StartAttack()` override — запускает `AttackCoroutine`.
- `StopAttack()` override — `IsAttacking = false`.
- `SwitchFireMode()` — `Stats.NextFiringMode(CurrentFireMode)`.
- `ReloadGun()` — гейтится: не reloading, не cycling, `CurrentReserveAmmo > 0`.
  Запускает `Reload.ReloadMagazine(this)` или `Reload.ReloadShell(this)`
  по `Stats.ReloadType`.
- `AbortAllActions()` — стопит все корутины (атака, болт, цикл).
- `Drop()` override — `base.Drop()` + сбрасывает позиции `Bolt`, `Forend`, `MagRB`.

**Внутренние (полезно знать):**
- `Shoot()` — выстрел; `protected`. Делает спред, рандом damage по
  `Stats.GetDamage(distance)`, raycast с пенетрацией → `BulletImpact.HandleHit`,
  PlaySound, MuzzleFlash из пула, CameraShake, recoil-impulse на `LowerTorso`,
  декремент `CurrentLoadedAmmo`, `HandleCycle()`.
- `GetRandomBulletDir()` — спред зависит от `Stats.Accuracy`.
- `ProcessBullet(Vector2 dir)` — корутина пули; raycast'ит, обрабатывает
  пенетрацию (`Stats.MaxPenetration`).

---

## `GunStats`  (`Weapons/Guns/GunStats.cs`) — ScriptableObject

**CreateMenu:** `Guns/Stats`.

**Enums:**
- `GunCycleType { SelfCycle, BoltAction, PumpAction }`
- `GunReloadType { Magazine, Shell }`
- `GunTriggerMode` — `[Flags]` enum: `Auto=1, Semi=2, Burst=4`
  (определён в `SelfCycleConfig.cs`).

**Поля:**
- `CycleType : GunCycleType` (default `SelfCycle`)
- `SelfCycleConfig : SelfCycleConfig` (см. ниже)
- `RoundsPerMinute : int [Min(1)]` (600)
- `ProjectilesPerShot : int [Min(1)]` (1) — дробь = N
- `ReloadType : GunReloadType` (Magazine)
- `MaxLoadedAmmo : int [Min(1)]` (30)
- `MaxReserveAmmo : int [Min(1)]` (120)
- `MaxPenetration : int [Min(1)]` (2)
- `PenetrationDamageMultiplier : float [Range(0,1)]` (0.7)
- `PenetrationRangeReduction : float [Range(0,1)]` (0.7)
- `Range : DynamicFloat` (min 45, max 50)
- `ProjectileSpeed : DynamicFloat` (175–200)
- `_hitDamage : ParticleSystem.MinMaxCurve [SerializeField]` — damage от
  нормализованной дистанции (default `EaseInOut(0, 30, 1, 15)`).
- `HitMask : LayerMask`
- `Accuracy : float [Range(0,100)]` (95)
- `RecoilForce : float [Min(0)]` (1)
- `BulletForce : float [Min(0)]` (2)

**Methods:**
- `GetDamage(float distance = 0) : int`
  — `Mathf.Clamp01(distance / Range.GetValue())`, `_hitDamage.Evaluate(t, Random.value)`,
  `Mathf.CeilToInt(...)`.
- `NextFiringMode(GunTriggerMode current) : GunTriggerMode` — циклит
  `Auto → Semi → Burst → Auto`, пропуская те, что не выставлены в
  `SelfCycleConfig.FiringModes`.
- `HasFireMode(GunTriggerMode mode) : bool`.

---

## `SelfCycleConfig`  (`Weapons/Guns/SelfCycleConfig.cs`) — struct

```csharp
[System.Flags] public enum GunTriggerMode { Auto = 1, Semi = 2, Burst = 4 }

[System.Serializable]
public struct SelfCycleConfig {
    public GunTriggerMode FiringModes;
    public bool IsBurstSemiAuto;
    public int BurstLength;
    public float BurstInterval;
}
```

---

## `GunAudioConfig`  (`Weapons/Guns/GunAudioConfig.cs`) — ScriptableObject
Поля: `ShootClip`, `PullTriggerClip`, `GunMechClip`, `PullBoltClip`,
`PushBoltClip`, `SnapMagClip`, `ReleaseMagClip`, `PullForendClip`,
`PushForendClip`, `InsertShellClip`.

---

## `GunEffectConfig`  (`Weapons/Guns/GunEffectConfig.cs`) — ScriptableObject
Поля: `EjectEffect`, `MuzzleFlash`, `MuzzleSmoke`, `BulletPrefab` (опц.,
для трассеров), `CameraShakeAmount [Min(0)]`, `KickBackDistance [Min(0)]`.

---

## `GunImpactConfig`  (`Weapons/Guns/GunImpactConfig.cs`) — ScriptableObject
Поля: `GroundImpact`, `BloodWallSplash`, `Wound`, `BloodDrops`, `BloodMush`,
`BloodMushFinisher`, `BloodCascade`, + `WoundSpawnChance`,
`BloodDropsSpawnChance`, `BloodMushSpawnChance`, `BloodMushFinisherSpawnChance`,
`BloodCascadeSpawnChance` — все `[Range(0,1)]`.

---

## `Reload`  (`Weapons/Guns/Reload.cs`) — static class

Оркестрирует анимацию перезарядки. Запускается из `Gun.ReloadGun()`.

**Public methods:**
- `ReloadMagazine(Gun g) : IEnumerator`
- `ReloadShell(Gun g) : IEnumerator`
- `ReloadMagazineCoroutine(Gun g) : IEnumerator` — внутренний шаг
  с трансфером боеприпасов.
- `ReloadShellCoroutine(Gun g) : IEnumerator` — цикл по 1 патрону.

**Ключевое для интеграции.** Трансфер патронов происходит внутри `HandSnap()`:

```
ammoToLoad = min(g.Stats.MaxLoadedAmmo - g.CurrentLoadedAmmo,
                 g.CurrentReserveAmmo)
g.CurrentLoadedAmmo += ammoToLoad
g.CurrentReserveAmmo -= ammoToLoad
WeaponUI.Instance.UpdateAmmoUI()
```

Если кладёшь свой инвентарь — резерв нужно подать сюда (либо переопределить
этот пайплайн целиком).

---

## `Cycle`  (`Weapons/Guns/Cycle.cs`) — static class

Оркестрирует анимации затвора (bolt) и помпы (forend).

**Public methods:**
- `CockGun(Gun g) : IEnumerator` — полный взвод (рука к затвору, назад, вперёд).
- `MoveBolt(Gun g) : IEnumerator` — простой pull+push без анимации руки.
- `PullBolt(Gun g, bool playSound) : IEnumerator`
- `PushBolt(Gun g, bool playSound) : IEnumerator`
- `CycleBolt(Gun g, bool ejectCasing) : IEnumerator`
- `CycleForend(Gun g, bool ejectShell) : IEnumerator`

`Push*/Cycle*` ставят `IsCycled = true` и гейтят `Gun.CanShoot()`.

---

## `BulletImpact`  (`Weapons/Guns/BulletImpact.cs`) — static class

**Public method:**
- `HandleHit(Gun g, RaycastHit2D hit, float distance, float remainingPenetration)`
  — спавнит wound/blood-эффекты из `g.ImpactConfig` (с шансами), вызывает
  `BodyPart.TakeDamage(hit, g.Stats.GetDamage(distance) * mult, g.Stats.BulletForce)`.

---

## `BulletVisual`  (`Weapons/Projectiles/BulletVisual.cs`)

MonoBehaviour требует `TrailRenderer`. `OnEnable` — очищает trail, эмиссия on;
`OnDisable` — выкл. Спавнится `Gun.Shoot()` если `EffectConfig.BulletPrefab`
выставлен и `Settings.TracerRounds = true`.

---

## Атрибуты

- `HandIndexAttribute` (`Weapons/Attributes/HandIndexAttribute.cs`) —
  `PropertyAttribute`, рисует индекс в `SkinConfig.Hands[]` как popup.
- `NoDefaultPoseAttribute` (`Weapons/Attributes/NoDefaultPoseAttribute.cs`) —
  `PropertyAttribute`, помечает поля `WeaponHoldOffset` в `WeaponManager`,
  у которых нет смысла переключатель `UseDefaultPose`.

Оба используются только в Editor-drawers (`Scripts/Editor/Weapons/...`).

---

## Краткая «карта override-точек» для интеграции

| Что | Где | Override-target |
|---|---|---|
| Перехватить пикап | `Weapon.Interact(Character)` | переопределить в наследнике, не звать `EquipWeapon` напрямую — вместо этого положить в `InventoryManager` |
| Перехватить equip | `Weapon.Equip(Character)` | virtual; можно дописать запись «активного оружия» в hotbar |
| Перехватить drop | `Weapon.Drop()` | virtual; можно спавнить наш `WorldItem2D` вместо физического dropped weapon |
| Заменить UI патронов | `WeaponUI.Instance` зашит в `WeaponManager.EquipWeapon` и `Reload.HandSnap` | через адаптер: оставить `WeaponUI` пустым, подписаться на свои события |
| Подменить резерв патронов | `Gun.CurrentReserveAmmo` | синхронизировать с `ItemInstance.stackCount` соответствующего ItemDefinition |
