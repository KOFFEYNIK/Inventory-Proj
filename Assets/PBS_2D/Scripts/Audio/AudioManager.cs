using UnityEngine;
using System.Collections;

namespace PBS2D
{
    public class AudioManager : Singleton<AudioManager>
    {
        private const float SOUND_Z_POSITION = -10f;
        private const float MIN_PITCH_VARIATION = 0.9f;
        private const float MAX_PITCH_VARIATION = 1.1f;
        private const float MIN_VOLUME_VARIATION = 0.9f;
        private const float MAX_VOLUME_VARIATION = 1.1f;
        private const float CLIP_END_BUFFER = 0.1f;

        [SerializeField]
        private GameObject _soundPlayerPrefab;

        public void PlaySound(AudioClip clip, Vector2 position, float volume = 1f, float delay = 0f)
        {
            if (clip == null) return;
            StartCoroutine(PlaySoundAtLocation(clip, position, volume, delay));
        }

        private IEnumerator PlaySoundAtLocation(AudioClip clip, Vector2 position, float volume, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            GameObject playerObj = ObjectPoolManager.SpawnObject(_soundPlayerPrefab, new Vector3(position.x, position.y, SOUND_Z_POSITION), Quaternion.identity, PoolType.SoundPlayer);
            AudioSource source = playerObj.GetComponent<AudioSource>();

            float pitch = Random.Range(MIN_PITCH_VARIATION, MAX_PITCH_VARIATION);
            if (Time.timeScale > 0f)
                pitch *= Time.timeScale;

            source.pitch = pitch;
            source.PlayOneShot(clip, Random.Range(MIN_VOLUME_VARIATION, MAX_VOLUME_VARIATION) * volume);

            float actualDuration = clip.length / Mathf.Abs(pitch) + CLIP_END_BUFFER;
            yield return new WaitForSecondsRealtime(actualDuration);
            ObjectPoolManager.ReturnObjectToPool(playerObj);
        }
    }
}