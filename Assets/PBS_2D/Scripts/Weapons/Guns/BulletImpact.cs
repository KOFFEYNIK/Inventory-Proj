using UnityEngine;

namespace PBS2D
{
    public static class BulletImpact
    {
        public static void HandleHit(Gun g, RaycastHit2D hit, float distance, float remainingPenetration)
        {
            if (!hit.collider) return;

            float woundAngle = Mathf.Atan2(hit.normal.y, hit.normal.x) * Mathf.Rad2Deg;
            Quaternion woundRot = Quaternion.Euler(0f, 0f, woundAngle);

            Wound wound = null;

            if (g.ImpactConfig.Wound != null && Random.value < g.ImpactConfig.WoundSpawnChance)
            {
                wound = ObjectPoolManager.SpawnObject(g.ImpactConfig.Wound, hit.point, woundRot, PoolType.Effect).GetComponent<Wound>();

                if (wound != null)
                {
                    Vector2 localPoint = hit.transform.InverseTransformPoint(hit.point);
                    float halfWidth = hit.collider.bounds.extents.x;

                    // More offset when hit from the front, less from behind
                    float offsetX = localPoint.x > 0f
                        ? Random.Range(.05f, halfWidth)
                        : Random.Range(0f, halfWidth / 7.5f);

                    wound.transform.position += wound.transform.right * -offsetX;
                    wound.transform.SetParent(hit.transform);
                    wound.RandomizeSprite();
                }
            }

            // Use wound position if available, else fall back to hit point
            Vector2 effectPos = wound != null ? (Vector2)wound.transform.position : hit.point;
            Quaternion effectRot = wound != null ? wound.transform.rotation : woundRot;

            if (g.ImpactConfig.BloodDrops != null && Random.value < g.ImpactConfig.BloodDropsSpawnChance)
            {
                ObjectPoolManager.SpawnObject(g.ImpactConfig.BloodDrops, effectPos, effectRot, PoolType.Unparented).transform.SetParent(hit.transform);
            }

            if (g.ImpactConfig.BloodMush != null && Random.value < g.ImpactConfig.BloodMushSpawnChance)
            {
                ObjectPoolManager.SpawnObject(g.ImpactConfig.BloodMush, effectPos, effectRot, PoolType.Effect);
            }

            if (hit.collider.TryGetComponent<BodyPart>(out var bodyPart))
            {
                if (g.ImpactConfig.BloodCascade != null && bodyPart.CanSpawnBloodCascade() && Random.value < g.ImpactConfig.BloodCascadeSpawnChance)
                {
                    GameObject bloodCascadeObj = ObjectPoolManager.SpawnObject(g.ImpactConfig.BloodCascade, effectPos, effectRot, PoolType.Effect);
                    bloodCascadeObj.transform.SetParent(bodyPart.transform);
                    bodyPart.AddBloodCascade(bloodCascadeObj.GetComponent<BloodCascade>());
                }

                if (bodyPart.Character.IsDead) return;

                float penetrationsUsed = g.Stats.MaxPenetration - remainingPenetration;
                float dmgMultiplier = Mathf.Pow(g.Stats.PenetrationDamageMultiplier, penetrationsUsed);
                bodyPart.TakeDamage(hit, g.Stats.GetDamage(distance) * dmgMultiplier, g.Stats.BulletForce);

                if (bodyPart.Character.IsDead && bodyPart is Head)
                {
                    if (g.ImpactConfig.BloodMushFinisher != null && Random.value < g.ImpactConfig.BloodMushFinisherSpawnChance)
                        ObjectPoolManager.SpawnObject(g.ImpactConfig.BloodMushFinisher, effectPos, effectRot, PoolType.Effect);

                    if (g.ImpactConfig.BloodWallSplash != null)
                        ObjectPoolManager.SpawnObject(g.ImpactConfig.BloodWallSplash, effectPos, effectRot, PoolType.Effect);
                }
            }
        }
    }
}
