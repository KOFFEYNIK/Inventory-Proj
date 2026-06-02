using System.Collections;
using UnityEngine;

namespace PBS2D
{
    public class ReturnToPool : MonoBehaviour
    {
        [SerializeField] private float _disableDelay = 1;

        void OnEnable()
        {
            StartCoroutine(DisableAfterTime());
        }

        private IEnumerator DisableAfterTime()
        {
            yield return new WaitForSeconds(_disableDelay);

            ObjectPoolManager.ReturnObjectToPool(gameObject);
            StopAllCoroutines();
        }
    }
}
