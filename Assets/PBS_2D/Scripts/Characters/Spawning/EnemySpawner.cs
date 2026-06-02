using UnityEngine;
using System.Collections;

namespace PBS2D
{
    public class EnemySpawner : MonoBehaviour
    {
        [SerializeField, Tooltip("Enemy prefab to spawn")]
        private GameObject _enemyPrefab;

        [SerializeField, Tooltip("Time after enemy death before the corpse is destroyed (seconds). Doubles as the respawn gate in one-at-a-time mode")]
        private float _respawnDelay = 7.5f;
        
        [SerializeField, Tooltip("Wait for the previous enemy to die before spawning the next. Off = spawns on a fixed interval")]
        private bool _onlySpawnOneAtATime = true;

        [SerializeField, Tooltip("Time between enemy spawns (seconds)")]
        private float _spawnInterval = 5f;

        [SerializeField, Tooltip("Don't spawn while the player is closer than this. Stops enemies spawning on top of the player")]
        private float _minSpawnDistance = 3;

        [SerializeField, Tooltip("How long a dropped enemy gun lives before despawning (seconds). Picking it up cancels the timer")]
        private float _gunDespawnTime = 30f;

        private Character _player;
        private static readonly WaitForSeconds _waitForSeconds_1 = new(.1f);

        void OnEnable()
        {
            _player = PlayerManager.Player;
            PlayerManager.OnPlayerSpawned += HandlePlayerSpawned;
            PlayerManager.OnPlayerDespawned += HandlePlayerDespawned;
        }

        void OnDisable()
        {
            PlayerManager.OnPlayerSpawned -= HandlePlayerSpawned;
            PlayerManager.OnPlayerDespawned -= HandlePlayerDespawned;
        }

        private void HandlePlayerSpawned(Character player) => _player = player;
        private void HandlePlayerDespawned(Character player) => _player = null;

        void Start()
        {
            if (_onlySpawnOneAtATime)
                StartCoroutine(SingleSpawnLoop());
            else
                StartCoroutine(SpawnCoroutine());
        }

        private float DistanceToPlayer()
        {
            if(_player != null)
                return Vector2.Distance(transform.position, _player.LowerTorso.Transform.position);
            else
                return Mathf.Infinity;
        }

        private IEnumerator SingleSpawnLoop()
        {
            while (true)
            {
                yield return StartCoroutine(Spawn());
            }
        }

        private IEnumerator Spawn()
        {
            GameObject enemy = Instantiate(_enemyPrefab, transform.position, transform.rotation);
            Character c = enemy.GetComponent<Character>();

            Gun gun = c.WeaponManager.Weapon.GetComponent<Gun>();

            StartCoroutine(GunDespawner(gun, c));

            yield return new WaitWhile(() => c.IsConscious);
            yield return new WaitForSeconds(_respawnDelay);

            while (DistanceToPlayer() < _minSpawnDistance)
            {
                yield return null;
            }

            Destroy(c.gameObject);
        }

        private IEnumerator SpawnCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(_spawnInterval);

                if (DistanceToPlayer() < _minSpawnDistance)
                    continue;

                StartCoroutine(Spawn());
            }
        }

        private IEnumerator GunDespawner(Gun gun, Character owner)
        {
            yield return _waitForSeconds_1;
            yield return new WaitUntil(() => gun == null || gun.IsDropped || owner.IsDead);

            if (gun == null) yield break;

            float elapsed = 0f;

            while (elapsed < _gunDespawnTime)
            {
                if (gun == null || gun.IsEquippedByPlayer)
                    yield break;

                elapsed += Time.deltaTime;
                yield return null;
            }

            if (gun != null)
                Destroy(gun.gameObject);
        }
    }
}
