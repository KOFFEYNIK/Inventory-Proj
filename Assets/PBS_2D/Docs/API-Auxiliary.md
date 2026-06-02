# PBS_2D — API: Auxiliary

Namespace: `PBS2D`. Все пути даны от `Assets/PBS_2D/Scripts/`.
Здесь — вспомогательные системы (звук, камера, эффекты, окружение,
пуллинг, утилиты).

---

## Audio

### `AudioManager`  (`Audio/AudioManager.cs`) — singleton
- `PlaySound(AudioClip clip, Vector2 position, float volume, float delay)` —
  одношотный звук с pitch/volume-вариацией.
- Под капотом — `ObjectPoolManager` для аудио-источников.

### `ImpactSound`  (`Audio/ImpactSound.cs`)
Базовый MonoBehaviour, играет звук удара на коллизии в зависимости от
скорости (clamp между `_minImpactSpeed`/`_maxImpactSpeed`) + cooldown.
Inspector: `_impactClips`, `_targetLayers`, `_minImpactSpeed`,
`_maxImpactSpeed`, `_cooldown`, `_volumeMultiplier`.

### `CharacterImpactSound : ImpactSound`  (`Audio/CharacterImpactSound.cs`)
Inspector: `_character`, `_bodyPartGroup : enum { Head, Torso, FrontArm, BackArm, FrontLeg, BackLeg }`.

### `WeaponImpactSound : ImpactSound`  (`Audio/WeaponImpactSound.cs`)
Inspector: `_weapon`. Играет только если `weapon.IsDropped`.

---

## Camera

### `CameraManager`  (`Camera/CameraManager.cs`) — singleton
- `ChangeCameraSize(float size)` — менять ortho-size.
- `Shake(float magnitude, float duration)` — шейк.
- Сама находит `PlayerManager.Player` и сглаженно следит, со сдвигом
  в сторону `aimWorldPoint` и `facing`.

---

## Effects

### `BloodCascade`  (`Effects/BloodCascade.cs`)
- `Initialize(Character character)` — биндит к персонажу, ставит bleed-rate.
- `LifeTimeCoroutine(float lifetime)`.

### `BloodSplat`  (`Effects/BloodSplat.cs`)
Спавнит splat'ы при коллизии партиклов через `SplatManager`. Никаких
character-ссылок.

### `Casing`  (`Effects/Casing.cs`)
Гильза. `Play()` — активировать партиклы и поставить в очередь возврата в пул.

### `SplatManager`  (`Effects/SplatManager.cs`) — singleton
- `PlaceBloodSplat(Vector2 position, bool horizontal) : bool` — кладёт сплат
  по grid'у, растит ближайшие если есть место, отказывается ставить если
  слишком много рядом.

### `Wound`  (`Effects/Wound.cs`)
- `RandomizeSprite()` — рандомный спрайт+цвет из массивов на компоненте.

---

## Environment

### `Killbox`  (`Environment/Killbox.cs`)
Триггер-зона, мгновенно убивает Character на контакте. Inspector:
`_targetLayers`.

### `NightLight`  (`Environment/NightLight.cs`)
`Light2D`, переключается on/off по `Settings.DayTime` каждый кадр.

### `WorldTimeManager`  (`Environment/WorldTimeManager.cs`) — singleton
- `SetDayTime()`, `SetNightTime()` — глобальный свет + цвет камеры.

---

## Pooling

### `ObjectPoolManager`  (`Pooling/ObjectPoolManager.cs`)
Статический API (сам класс — `MonoBehaviour`, но всё через static).

- `SpawnObject(GameObject prefab, Vector3 pos, Quaternion rot, PoolType poolType) : GameObject`
- `ReturnObjectToPool(GameObject obj)`
- `GetPoolParent(PoolType poolType) : Transform`
- `ObjectPools : Dictionary<string, Queue<GameObject>>` (public)
- enum `PoolType { GameObject, ParticleSystem, SoundFX, ... }` (точные значения смотри в файле)

Используется массово: эффекты пуль, гильзы, кровь, звуки, муззл-флеши.

### `ReturnToPool`  (`Pooling/ReturnToPool.cs`)
Компонент-таймер: `_disableDelay` — через сколько вернуть себя в пул.

---

## Utility

### `Singleton<T>`  (`Utility/Singleton.cs`) — abstract generic
`T.Instance` — `FindFirstObjectByType<T>()` на первый доступ.
Используется `AudioManager`, `CameraManager`, `WeaponUI`, `PauseMenu`,
`MobileControls`, `SplatManager`, `WorldTimeManager`.

### `Settings`  (`Utility/Settings.cs`) — static class
См. [API-Input-UI.md](API-Input-UI.md#settings--utilitysettingscs--static-class).

### `AnimationUtility`  (`Utility/AnimationUtility.cs`) — static class
- `MoveTo(Transform obj, Vector2 targetPos, float duration) : IEnumerator`
- `RotateTo(Transform tr, float targetRotation, float duration) : IEnumerator`

### `AverageRotation`  (`Utility/AverageRotation.cs`)
Усреднение угла Z двух transform'ов каждый кадр.
Inspector: `_targetA`, `_targetB`.

### `DynamicValue`  (`Utility/DynamicValue.cs`)
- `enum DynamicValueMode { Constant, BetweenTwoConstants /* +возможно ещё */ }`
- `DynamicFloat`, `DynamicInt` — `[System.Serializable]` структуры с
  `Mode`, `Value`, `MinValue`, `MaxValue`.
- `GetValue()` — возвращает константу или random между min/max.

Кастомный PropertyDrawer — `Scripts/Editor/DynamicValueDrawer.cs`.

---

## Editor (`Scripts/Editor/...`)

Чисто Editor-only компоненты (живут в Editor-assembly, не идут в билд).
В рантайме их не дёргаешь, но полезно знать что они есть:

| Файл | Что рисует |
|---|---|
| `Characters/CharacterEditor.cs` | Кастомный Inspector для `Character`. |
| `Characters/AIBehaviorEditor.cs` | Inspector AI ScriptableObject. |
| `Characters/EnemySpawnerEditor.cs` | Inspector спавнера. |
| `Characters/BodyPartRefDrawer.cs` | Drawer для структуры `BodyPartRef`. |
| `Weapons/GunEditor.cs` | Inspector `Gun`. |
| `Weapons/GunStatsEditor.cs` | Inspector `GunStats` SO. |
| `Weapons/HandIndexPropertyDrawer.cs` | Popup для `[HandIndex]`. |
| `Weapons/SelfCycleConfigDrawer.cs` | Drawer структуры. |
| `Weapons/WeaponPoseOffsetDrawer.cs` | Drawer `WeaponHoldOffset`. |
| `DynamicValueDrawer.cs` | Drawer `DynamicFloat`/`DynamicInt`. |
