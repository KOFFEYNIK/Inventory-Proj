# PBS_2D — справочная документация

Эта папка — независимая документация к ассету **PBS 2D (Physics-Based Shooter 2D)**.
Оригинальный `Assets/PBS_2D/README.md` — короткий маркетинговый обзор от автора.
Здесь — подробный технический разбор по подсистемам и план интеграции с
проектным инвентарём (`Assets/_InventoryPlug/`).

Документация написана по результатам прямого чтения исходников ассета
(85 .cs-файлов в `Assets/PBS_2D/Scripts/`). Все ссылки указывают на конкретные
файлы и (где это полезно) строки.

## Состав

| Файл | Что внутри |
|---|---|
| [Architecture.md](Architecture.md) | Общая карта систем, кто кого вызывает, поток данных при пикапе/стрельбе/смерти. |
| [API-Characters.md](API-Characters.md) | `Character`, `CharacterHealth`, `CharacterMovement`, `CharacterRotation`, `CharacterSkin`, `TorsoHeightController`, `LegsController`, `InteractionHandler`, `PlayerManager`, тело (`BodyPart`, `Head`, `Torso`, `Leg`, `Balance`, `BodyPhysicsController`, `FootAlignment`), `GroundDetection`, AI (`AIBrain`, `AIBehavior`), спавнеры. |
| [API-Weapons.md](API-Weapons.md) | `Interactable`, `Weapon`, `Gun`, `GunStats`, `GunAudioConfig`, `GunEffectConfig`, `GunImpactConfig`, `Reload`, `Cycle`, `SelfCycleConfig`, `BulletImpact`, `WeaponOffset`, `Outline`, атрибуты. |
| [API-Input-UI.md](API-Input-UI.md) | `PlayerInputHandler`, `WorldInputManager`, `GameControls`, `WeaponUI`, `PauseMenu`, `MobileControls`, `Settings`. |
| [API-Auxiliary.md](API-Auxiliary.md) | `AudioManager`, `CameraManager`, эффекты (`BloodCascade`, `BloodSplat`, `Casing`, `SplatManager`, `Wound`), окружение (`Killbox`, `NightLight`, `WorldTimeManager`), пуллинг (`ObjectPoolManager`, `ReturnToPool`), утилиты (`Singleton<T>`, `DynamicValue`, `AnimationUtility`, `AverageRotation`). |
| [Integration-Inventory.md](Integration-Inventory.md) | Анализ вариантов интеграции `_InventoryPlug` ↔ PBS_2D с плюсами/минусами и рекомендацией. |

## Соглашения

- **«Player» vs «AI»** — один и тот же префаб `Character` ведёт себя по-разному
  по флагу `IsPlayer`. На Awake добавляется либо `PlayerInputHandler` +
  `InteractionHandler`, либо `AIBrain`.
- **Namespace** — все скрипты ассета сидят в `namespace PBS2D` (включая
  `Settings`, `PlayerInfo` и др. — это не глобальные имена).
- **Стили полей** — у автора стабильно: `[SerializeField] private _camelCase`
  для приватных Inspector-полей, `PublicPascalCase` для публичных. Поля,
  помеченные `[System.NonSerialized] public`, — рантайм-состояние, в инспекторе
  не показываются.

## Версия ассета и Unity

- Unity 6000.4.0f1+
- URP (2D-renderer)
- Unity Input System (assembly `UnityEngine.InputSystem`)
- Unity 2D IK (`UnityEngine.U2D.IK` — `IKManager2D`, `LimbSolver2D`)
