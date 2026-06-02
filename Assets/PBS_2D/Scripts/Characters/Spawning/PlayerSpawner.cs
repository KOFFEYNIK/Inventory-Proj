using UnityEngine;
using System.Collections;

namespace PBS2D
{
    public class PlayerSpawner : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField, Tooltip("Player prefab to spawn. Must have IsPlayer = true on its Character component.")]
        private GameObject _playerPrefab;

        [SerializeField, Min(0), Tooltip("Delay between the player going down and the corpse being cleared for respawn.")]
        private float _respawnDelay = 3f;

        void Start()
        {
            StartCoroutine(SpawnLoop());
        }

        private IEnumerator SpawnLoop()
        {
            while (true)
            {
                GameObject playerObj = Instantiate(_playerPrefab, transform.position, transform.rotation);
                Character player = playerObj.GetComponent<Character>();

                yield return new WaitWhile(() => player != null && player.IsConscious);
                yield return new WaitForSeconds(_respawnDelay);

                if (player != null)
                    Destroy(player.gameObject);
            }
        }
    }
}
