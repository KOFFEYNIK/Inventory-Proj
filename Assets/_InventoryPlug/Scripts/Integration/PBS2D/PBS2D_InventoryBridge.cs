using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PBS2D;

namespace InventoryPlug.Integration.PBS2D
{
    /// <summary>
    /// Мост между инвентарём (<c>_InventoryPlug</c> + UPM-пакет GridInventory) и
    /// физическим персонажем PBS_2D. Вешается на игрока (тот же GameObject, что и
    /// <see cref="Character"/> + <see cref="WeaponManager"/>).
    ///
    /// <para>Делает (Вариант A — адаптер поверх, без правок кода PBS_2D и пакета):</para>
    /// <list type="bullet">
    ///   <item>Слушает <c>WeaponHotbar.OnActiveChanged</c> → экипирует/паркует оружие
    ///         через <c>WeaponManager.EquipWeapon</c>.</item>
    ///   <item>Сохраняет состояние оружия: неактивные стволы держатся <c>SetActive(false)</c>
    ///         (ключ — <c>ItemInstance.instanceId</c>).</item>
    ///   <item>Магазинная модель (Tarkov-style): магазин — отдельный предмет инвентаря,
    ///         хранящий патроны в <c>ItemInstance.nestedContainer</c>. Россыпь патронов сама
    ///         НЕ стреляет — только набивает магазины. Без вставленного непустого магазина
    ///         оружие не стреляет (<c>Gun.CurrentLoadedAmmo = 0</c>, резерв держим в нуле).</item>
    ///   <item>R — боевая перезарядка = смена магазина (самый полный совместимый по калибру).</item>
    ///   <item>Набивка магазина (медленно, 1 патрон за <c>loadSecondsPerRound</c>): из контекстного
    ///         меню инвентаря («Снаряжать») и зажатой клавишей <see cref="loadMagKey"/> в игре.</item>
    ///   <item>Drop (G) — выбрасывает активный предмет хотбара в мир.</item>
    /// </list>
    /// См. <c>Assets/_InventoryPlug/Docs/PBS2D-Magazine-System.md</c>.
    /// </summary>
    [RequireComponent(typeof(Character))]
    [RequireComponent(typeof(WeaponManager))]
    public class PBS2D_InventoryBridge : MonoBehaviour
    {
        [Header("Magazine system")]
        [Tooltip("Каталог магазинов: магазин → калибр, ёмкость, скорость набивки.")]
        public MagazineCatalog magazineCatalog;

        [Tooltip("Клавиша боевой перезарядки (смена магазина). Совпадает с PBS Reload — " +
                 "штатный ReloadGun при резерве 0 не сработает, свап делает мост.")]
        public KeyCode reloadKey = KeyCode.R;

        [Tooltip("Зажать — набивать активный магазин из россыпи патронов в инвентаре.")]
        public KeyCode loadMagKey = KeyCode.T;

        [Tooltip("Создавать стартовый снаряжённый магазин при первом взятии оружия в руки.")]
        public bool startWithLoadedMag = true;

        [Header("Pickup")]
        [Tooltip("Глушить PBS_2D InteractionHandler (подбор по E), чтобы подбор шёл " +
                 "через инвентарный HoverPickup2D (F).")]
        public bool disablePbsInteractionHandler = true;

        [Header("Drop (Q5)")]
        [Tooltip("Клавиша «выбросить активный предмет хотбара».")]
        public KeyCode dropKey = KeyCode.G;

        [Tooltip("Горизонтальная скорость выброса (в сторону взгляда).")]
        public float dropForwardSpeed = 3f;

        [Tooltip("Вертикальная скорость выброса.")]
        public float dropUpSpeed = 1.5f;

        private const string ReservedKeyPrefix = "reserved:";
        private const string ContextLoadLabel = "Снаряжать";

        private Character _character;
        private WeaponManager _wm;

        // Ключ (instanceId предмета или "reserved:<index>") → живой объект оружия.
        private readonly Dictionary<string, GameObject> _liveWeapons = new();
        private readonly HashSet<GameObject> _managed = new();

        // Ключ оружия → вставленный в него магазин (ItemInstance с патронами в nestedContainer).
        // Магазин «в оружии» не лежит в сетке инвентаря — его держит мост.
        private readonly Dictionary<string, ItemInstance> _insertedMags = new();

        private Gun _activeGun;
        private ItemDefinition _activeAmmoDef; // калибр активного оружия
        private string _activeWeaponKey;
        private int _lastLoaded;               // для детекта выстрела (Gun сам декрементит loaded)

        private Coroutine _loadRoutine;
        private bool _contextEntryRegistered;

        private WeaponHotbar _subscribedHotbar;
        private InventoryManager _subscribedMgr;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _character = GetComponent<Character>();
            _wm = GetComponent<WeaponManager>();
        }

        private void Start()
        {
            if (disablePbsInteractionHandler && _character.InteractionHandler != null)
                _character.InteractionHandler.enabled = false;

            InventoryManager.OnActiveChanged += HandleInventoryActiveChanged;
            SubscribeInventory(InventoryManager.Instance);
            TrySubscribeHotbar();
            TryRegisterContextEntry();

            if (_subscribedHotbar != null)
                HandleHotbarActiveChanged(_subscribedHotbar.ActiveIndex);
        }

        private void OnDestroy()
        {
            InventoryManager.OnActiveChanged -= HandleInventoryActiveChanged;
            if (_subscribedMgr != null) _subscribedMgr.OnInventoryChanged -= HandleInventoryChanged;
            if (_subscribedHotbar != null) _subscribedHotbar.OnActiveChanged -= HandleHotbarActiveChanged;
        }

        private void Update()
        {
            if (_subscribedHotbar == null) TrySubscribeHotbar();
            if (!_contextEntryRegistered) TryRegisterContextEntry();

            if (_activeGun != null)
            {
                // Выстрел: Gun сам декрементит CurrentLoadedAmmo → списываем из вставленного магазина.
                int loaded = _activeGun.CurrentLoadedAmmo;
                if (loaded < _lastLoaded)
                {
                    RemoveFromMag(GetInsertedMag(_activeWeaponKey), _lastLoaded - loaded);
                    _lastLoaded = loaded;
                    InventoryManager.Instance?.NotifyChanged();
                    RefreshWeaponUI();
                }
                else if (loaded > _lastLoaded)
                {
                    _lastLoaded = loaded;
                }

                // Резерв всегда 0 — россыпь не стреляет, штатный ReloadGun не вмешивается.
                if (_activeGun.CurrentReserveAmmo != 0) _activeGun.CurrentReserveAmmo = 0;
            }

            if (Input.GetKeyDown(dropKey)) DropActiveHotbarItem();
            if (Input.GetKeyDown(reloadKey)) SwapMagazine();

            HandleHoldToLoad();
        }

        // ── Subscriptions ──────────────────────────────────────────────────────

        private void TrySubscribeHotbar()
        {
            if (_subscribedHotbar != null || WeaponHotbar.Instance == null) return;
            _subscribedHotbar = WeaponHotbar.Instance;
            _subscribedHotbar.OnActiveChanged += HandleHotbarActiveChanged;
        }

        private void SubscribeInventory(InventoryManager mgr)
        {
            if (_subscribedMgr == mgr) return;
            if (_subscribedMgr != null) _subscribedMgr.OnInventoryChanged -= HandleInventoryChanged;
            _subscribedMgr = mgr;
            if (_subscribedMgr != null) _subscribedMgr.OnInventoryChanged += HandleInventoryChanged;
        }

        private void HandleInventoryActiveChanged(InventoryManager mgr) => SubscribeInventory(mgr);

        private void HandleInventoryChanged() => RefreshWeaponUI();

        private void TryRegisterContextEntry()
        {
            if (_contextEntryRegistered || ContextMenuUI.Instance == null) return;
            ContextMenuUI.Instance.AddExtraEntry(ContextLoadLabel, OnContextLoadMagazine,
                                                 enabled: true, visible: IsMagazineItem);
            _contextEntryRegistered = true;
        }

        private bool IsMagazineItem(ItemInstance item) =>
            item != null && magazineCatalog != null && magazineCatalog.IsMagazine(item.definition);

        // ── Equip / Unequip ──────────────────────────────────────────────────────

        private void HandleHotbarActiveChanged(int index)
        {
            var hotbar = _subscribedHotbar;
            if (hotbar == null) return;

            GameObject prefab = hotbar.GetActiveWeaponPrefab();
            ItemInstance inst = hotbar.GetSlotItem(index); // null для reserved-слота
            string key = inst != null
                ? inst.instanceId
                : (prefab != null ? ReservedKeyPrefix + index : null);

            _activeWeaponKey = key;
            EquipFromHotbar(prefab, key);
        }

        private void EquipFromHotbar(GameObject prefab, string key)
        {
            if (prefab == null)
            {
                UnequipCurrent();
                return;
            }

            GameObject targetGO = GetOrCreateLiveWeapon(key, prefab);
            if (targetGO == null) return;

            Weapon targetWeapon = targetGO.GetComponent<Weapon>();
            if (targetWeapon == null)
            {
                Debug.LogWarning($"[Bridge] Префаб оружия '{prefab.name}' не содержит компонент Weapon.");
                return;
            }

            Weapon previous = _wm.Weapon;
            if (previous == targetWeapon) return; // уже в руках

            targetGO.SetActive(true);
            _wm.EquipWeapon(targetWeapon); // внутри сам роняет previous

            if (previous != null && previous.gameObject != targetGO)
                ParkWeapon(previous);

            SetupActiveGun(targetGO);
        }

        private void UnequipCurrent()
        {
            Weapon previous = _wm.Weapon;
            if (previous == null) return;

            _wm.DropWeapon();
            ParkWeapon(previous);

            _activeGun = null;
            _activeAmmoDef = null;
        }

        private GameObject GetOrCreateLiveWeapon(string key, GameObject prefab)
        {
            if (key != null && _liveWeapons.TryGetValue(key, out var existing) && existing != null)
            {
                existing.SetActive(true);
                return existing;
            }

            GameObject go = Instantiate(prefab);
            if (key != null) _liveWeapons[key] = go;
            _managed.Add(go);
            return go;
        }

        private void ParkWeapon(Weapon weapon)
        {
            GameObject go = weapon.gameObject;

            if (!_managed.Contains(go))
            {
                Destroy(go);
                return;
            }

            go.transform.SetParent(transform, false);
            go.SetActive(false);
        }

        private void SetupActiveGun(GameObject targetGO)
        {
            _activeGun = targetGO.GetComponent<Gun>();
            _activeAmmoDef = null;

            if (_activeGun == null) return;

            var binding = targetGO.GetComponent<InventoryWeaponBinding>();
            if (binding != null) _activeAmmoDef = binding.ammoDefinition;

            // Магазинная модель: россыпь — НЕ резерв.
            _activeGun.CurrentReserveAmmo = 0;

            // Обеспечить вставленный магазин (стартовый — снаряжённый).
            ItemInstance mag = GetInsertedMag(_activeWeaponKey);
            if (mag == null && startWithLoadedMag && _activeAmmoDef != null)
            {
                mag = CreateFullMag(_activeAmmoDef);
                if (mag != null) SetInsertedMag(_activeWeaponKey, mag);
            }

            SyncGunFromMag(mag);
            RefreshWeaponUI();
        }

        // ── Magazine helpers ─────────────────────────────────────────────────────

        private ItemInstance GetInsertedMag(string key) =>
            key != null && _insertedMags.TryGetValue(key, out var m) ? m : null;

        private void SetInsertedMag(string key, ItemInstance mag)
        {
            if (key != null) _insertedMags[key] = mag;
        }

        /// <summary>Первый (единственный) стак патронов внутри магазина, или null.</summary>
        private static ItemInstance GetMagAmmoItem(ItemInstance mag)
        {
            var c = mag?.nestedContainer;
            if (c == null) return null;
            foreach (var (item, _) in c.GetAllItems()) return item;
            return null;
        }

        private static int GetMagLoaded(ItemInstance mag)
        {
            var ammo = GetMagAmmoItem(mag);
            return ammo != null ? ammo.stackCount : 0;
        }

        /// <summary>Синхронизировать ствол с зарядом вставленного магазина.</summary>
        private void SyncGunFromMag(ItemInstance mag)
        {
            int loaded = GetMagLoaded(mag);
            if (_activeGun != null)
            {
                _activeGun.CurrentLoadedAmmo = loaded;
                _activeGun.CurrentReserveAmmo = 0;
            }
            _lastLoaded = loaded;
        }

        private void RemoveFromMag(ItemInstance mag, int amount)
        {
            if (mag == null || amount <= 0) return;
            var ammo = GetMagAmmoItem(mag);
            if (ammo == null) return;
            ammo.stackCount -= amount;
            if (ammo.stackCount <= 0) mag.nestedContainer.Remove(ammo);
        }

        /// <summary>Создать снаряжённый «под завязку» магазин под калибр (для старта).</summary>
        private ItemInstance CreateFullMag(ItemDefinition caliber)
        {
            var entry = magazineCatalog != null ? magazineCatalog.GetDefaultForCaliber(caliber) : null;
            if (entry == null || entry.magazineDefinition == null) return null;

            var mag = new ItemInstance(entry.magazineDefinition, 1); // ctor создаёт nestedContainer (isContainer)
            EnsureMagContainer(mag, entry);

            var ammo = new ItemInstance
            {
                instanceId = Guid.NewGuid().ToString(),
                definition = caliber,
                stackCount = Mathf.Max(1, entry.capacity)
            };
            TryPlaceAnywhere(mag.nestedContainer, ammo);
            return mag;
        }

        private static void EnsureMagContainer(ItemInstance mag, MagazineCatalog.Entry entry)
        {
            if (mag.nestedContainer != null) return;
            var def = entry.magazineDefinition;
            mag.nestedContainer = new GridContainer(
                Mathf.Max(1, def.containerWidth),
                Mathf.Max(1, def.containerHeight),
                def.containerMaxWeight);
        }

        private static bool TryPlaceAnywhere(GridContainer container, ItemInstance item)
        {
            if (container == null) return false;
            for (int y = 0; y < container.height; y++)
                for (int x = 0; x < container.width; x++)
                    if (container.TryPlace(item, new Vector2Int(x, y)))
                        return true;
            return false;
        }

        // ── Reload R (смена магазина) ─────────────────────────────────────────────

        private void SwapMagazine()
        {
            if (_activeGun == null || _activeAmmoDef == null) return;
            var mgr = InventoryManager.Instance;
            if (mgr == null) return;

            // Найти самый полный совместимый по калибру магазин в инвентаре.
            GridContainer bestC = null;
            ItemInstance best = null;
            int bestLoaded = -1;

            foreach (var c in mgr.GetPickupContainers())
            {
                if (c == null) continue;
                foreach (var (item, _) in c.GetAllItems())
                {
                    var e = magazineCatalog != null ? magazineCatalog.GetByMagazine(item.definition) : null;
                    if (e == null || e.caliber != _activeAmmoDef) continue;
                    int l = GetMagLoaded(item);
                    if (l > bestLoaded) { bestLoaded = l; best = item; bestC = c; }
                }
            }

            if (best == null) return; // нет запасного магазина — перезарядка невозможна

            bestC.Remove(best);

            // Текущий магазин — обратно в инвентарь (или, если места нет, в мир).
            var current = GetInsertedMag(_activeWeaponKey);
            if (current != null && !TryReturnMagToInventory(current))
                DropMagToWorld(current);

            SetInsertedMag(_activeWeaponKey, best);
            SyncGunFromMag(best);

            PlaySound(_activeGun.AudioConfig != null ? _activeGun.AudioConfig.ReleaseMagClip : null);
            PlaySound(_activeGun.AudioConfig != null ? _activeGun.AudioConfig.SnapMagClip : null);

            mgr.NotifyChanged();
            RefreshWeaponUI();
        }

        private bool TryReturnMagToInventory(ItemInstance mag)
        {
            var mgr = InventoryManager.Instance;
            if (mgr == null) return false;
            foreach (var c in mgr.GetPickupContainers())
                if (c != null && TryPlaceAnywhere(c, mag)) return true;
            return false;
        }

        private void DropMagToWorld(ItemInstance mag)
        {
            float dir = _character.IsFacingRight ? 1f : -1f;
            Vector3 pos = transform.position + new Vector3(dir * 0.5f, 0.5f, 0f);
            Vector3 vel = new Vector3(dir * dropForwardSpeed, dropUpSpeed, 0f);
            WorldItemSpawner.SpawnDropped(mag, pos, vel);
        }

        // ── Набивка магазина (медленно) ───────────────────────────────────────────

        private void HandleHoldToLoad()
        {
            if (!Input.GetKey(loadMagKey)) return;
            if (_loadRoutine != null) return;
            var mag = GetInsertedMag(_activeWeaponKey);
            if (mag == null) return;
            _loadRoutine = StartCoroutine(LoadMagazineRoutine(mag, isHold: true));
        }

        private void OnContextLoadMagazine(ItemInstance item)
        {
            if (!IsMagazineItem(item)) return;
            if (_loadRoutine != null) StopCoroutine(_loadRoutine);
            _loadRoutine = StartCoroutine(LoadMagazineRoutine(item, isHold: false));
        }

        /// <summary>Набивать магазин по 1 патрону каждые <c>loadSecondsPerRound</c> секунд из россыпи
        /// в инвентаре, пока магазин не полон / не кончилась россыпь / не прервано.</summary>
        private IEnumerator LoadMagazineRoutine(ItemInstance mag, bool isHold)
        {
            var entry = magazineCatalog != null ? magazineCatalog.GetByMagazine(mag.definition) : null;
            if (entry == null) { _loadRoutine = null; yield break; }

            float perRound = Mathf.Max(0.01f, entry.loadSecondsPerRound);
            int capacity = Mathf.Max(1, entry.capacity);

            while (true)
            {
                if (isHold && !Input.GetKey(loadMagKey)) break;
                if (GetMagLoaded(mag) >= capacity) break;
                if (!PullOneRound(entry.caliber, mag, capacity)) break; // нет россыпи / другой калибр

                PlaySound(_activeGun != null && _activeGun.AudioConfig != null
                    ? _activeGun.AudioConfig.InsertShellClip : null);

                if (mag == GetInsertedMag(_activeWeaponKey)) SyncGunFromMag(mag);
                InventoryManager.Instance?.NotifyChanged();
                RefreshWeaponUI();

                yield return new WaitForSeconds(perRound);
            }

            _loadRoutine = null;
        }

        /// <summary>Перенести 1 патрон калибра из россыпи (pickup-контейнеры) в магазин.</summary>
        private bool PullOneRound(ItemDefinition caliber, ItemInstance mag, int capacity)
        {
            var mgr = InventoryManager.Instance;
            if (mgr == null || caliber == null) return false;

            // Источник: россыпь нужного калибра.
            GridContainer srcC = null;
            ItemInstance srcItem = null;
            foreach (var c in mgr.GetPickupContainers())
            {
                if (c == null) continue;
                foreach (var (item, _) in c.GetAllItems())
                    if (item.definition == caliber && item.stackCount > 0) { srcC = c; srcItem = item; break; }
                if (srcItem != null) break;
            }
            if (srcItem == null) return false;

            var magC = mag.nestedContainer;
            if (magC == null) return false;

            ItemInstance magAmmo = GetMagAmmoItem(mag);
            if (magAmmo != null && magAmmo.definition != caliber) return false; // в магазине другой калибр
            if (magAmmo == null)
            {
                magAmmo = new ItemInstance
                {
                    instanceId = Guid.NewGuid().ToString(),
                    definition = caliber,
                    stackCount = 0
                };
                if (!TryPlaceAnywhere(magC, magAmmo)) return false;
            }
            if (magAmmo.stackCount >= capacity) return false;

            magAmmo.stackCount += 1;
            srcItem.stackCount -= 1;
            if (srcItem.stackCount <= 0) srcC.Remove(srcItem);
            return true;
        }

        // ── Drop (G) ─────────────────────────────────────────────────────────────

        private void DropActiveHotbarItem()
        {
            var hotbar = _subscribedHotbar;
            if (hotbar == null) return;

            ItemInstance item = hotbar.GetSlotItem(hotbar.ActiveIndex);
            if (item == null) return; // reserved/empty — ронять нечего

            string key = item.instanceId;
            bool isCurrentWeapon =
                _liveWeapons.TryGetValue(key, out var liveGO) &&
                liveGO != null &&
                _wm.Weapon != null &&
                _wm.Weapon.gameObject == liveGO;

            if (isCurrentWeapon)
            {
                _wm.DropWeapon();
                _activeGun = null;
                _activeAmmoDef = null;
            }
            if (liveGO != null)
            {
                _managed.Remove(liveGO);
                Destroy(liveGO);
            }
            _liveWeapons.Remove(key);
            _insertedMags.Remove(key); // вставленный магазин уходит вместе с оружием

            RemoveItemFromInventory(item);

            float dir = _character.IsFacingRight ? 1f : -1f;
            Vector3 pos = transform.position + new Vector3(dir * 0.5f, 0.5f, 0f);
            Vector3 vel = new Vector3(dir * dropForwardSpeed, dropUpSpeed, 0f);
            WorldItemSpawner.SpawnDropped(item, pos, vel);
        }

        private bool RemoveItemFromInventory(ItemInstance item)
        {
            var mgr = InventoryManager.Instance;
            if (mgr == null || item == null) return false;

            var slot = mgr.FindSlotByEquippedItem(item);
            if (slot.HasValue) { mgr.Unequip(slot.Value); return true; }

            if (mgr.TryLocateItem(item, out var container, out _))
            {
                mgr.RemoveItem(item, container);
                return true;
            }
            return false;
        }

        // ── Misc ───────────────────────────────────────────────────────────────

        private void PlaySound(AudioClip clip)
        {
            if (clip == null || AudioManager.Instance == null) return;
            AudioManager.Instance.PlaySound(clip, transform.position);
        }

        private void RefreshWeaponUI()
        {
            if (!_character.IsPlayer || WeaponUI.Instance == null) return;
            WeaponUI.Instance.Gun = _activeGun;
            WeaponUI.Instance.UpdateAmmoUI();
            WeaponUI.Instance.UpdateFireModeIcon();
        }
    }
}
