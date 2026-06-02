using System.Collections;
using UnityEngine;

namespace PBS2D
{
    public static class Cycle
    {
        private const float BOLT_MOVE_DURATION = .035f;
        private const float HAND_TO_BOLT_DURATION = 0.2f;
        private const float GRAB_DELAY = 0.1f;
        private const float COCK_PULL_DURATION = 0.15f;
        private const float BOLT_PAUSE_DURATION = 0.25f;
        private const float BOLT_CYCLE_MOVE_DURATION = 0.1f;
        private const float HAND_TO_GUN_DURATION = 0.15f;
        private const float AUTO_CYCLE_DELAY = 0.3f;
        private const float FOREND_MOVE_DURATION = 0.08f;
        private const float FOREND_PAUSE_DURATION = 0.15f;

        public static IEnumerator CockGun(Gun g)
        {
            g.character.IsCycling = true;

            g.character.CharacterSkin.ChangeHandSprite(true, g.CycleHandIdx);

            // Hand to bolt
            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.character.FrontHandIKTarget.transform, g.CycleHandPoint.localPosition, HAND_TO_BOLT_DURATION));

            g.character.FrontHandIKTarget.transform.SetParent(g.Bolt);
            yield return new WaitForSeconds(GRAB_DELAY);

            AudioManager.Instance.PlaySound(g.AudioConfig.PullBoltClip, g.transform.position);

            // Pull back
            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.Bolt, new Vector2(g.BoltInitialPos.x + g.CycleHandMoveDistance, g.BoltInitialPos.y), COCK_PULL_DURATION));

            yield return new WaitForSeconds(BOLT_PAUSE_DURATION);

            g.character.FrontHandIKTarget.transform.SetParent(g.transform);

            // Push bolt
            if (g.BoltCoroutine == null)
                g.BoltCoroutine = g.StartCoroutine(PushBolt(g, true));

            // Hand to gun
            g.character.CharacterSkin.ChangeHandSprite(true, g.FrontHandIdx);
            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.character.FrontHandIKTarget.transform, g.FrontHandPoint.transform.localPosition, HAND_TO_GUN_DURATION));

            g.character.IsCycling = false;
        }

        public static IEnumerator MoveBolt(Gun g)
        {
            yield return g.StartCoroutine(PullBolt(g, false));

            yield return g.StartCoroutine(PushBolt(g, false));
        }

        // Move bolt backwards
        public static IEnumerator PullBolt(Gun g, bool playSound)
        {
            Casing casing = ObjectPoolManager.SpawnObject(g.EffectConfig.EjectEffect, g.EjectionPoint.position, Quaternion.Euler(0, 0, g.transform.eulerAngles.z), PoolType.Unparented).GetComponent<Casing>();
            casing.transform.SetParent(g.transform);
            if (g.transform.lossyScale.x < 0f)
                casing.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * casing.transform.localRotation;

            if (playSound)
            {
                AudioManager.Instance.PlaySound(g.AudioConfig.PullBoltClip, g.transform.position);
            }

            Vector2 finalPos = g.BoltInitialPos + new Vector2(-g.BoltMoveAmount, 0f);
            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.Bolt, finalPos, BOLT_MOVE_DURATION));

            casing.Play();

            g.BoltCoroutine = null;
        }

        // Move bolt forward
        public static IEnumerator PushBolt(Gun g, bool playSound)
        {
            if (playSound)
            {
                AudioManager.Instance.PlaySound(g.AudioConfig.PushBoltClip, g.transform.position);
            }

            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.Bolt, g.BoltInitialPos, BOLT_MOVE_DURATION));
            g.IsCycled = true;
            g.BoltCoroutine = null;
        }

        public static IEnumerator CycleBolt(Gun g, bool ejectCasing)
        {
            g.character.IsCycling = true;

            g.IsBoltOpened = true;

            g.character.CharacterSkin.ChangeHandSprite(true, g.CycleHandIdx);

            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.character.FrontHandIKTarget.transform, g.CycleHandPoint.localPosition, HAND_TO_BOLT_DURATION));

            g.character.FrontHandIKTarget.transform.SetParent(g.Bolt);
            yield return new WaitForSeconds(GRAB_DELAY);

            Casing casing = null;
            if (ejectCasing)
            {
                casing = ObjectPoolManager.SpawnObject(g.EffectConfig.EjectEffect, g.EjectionPoint.position, Quaternion.Euler(0, 0, g.transform.eulerAngles.z), PoolType.Unparented).GetComponent<Casing>();
                casing.transform.SetParent(g.transform);
                if (g.transform.lossyScale.x < 0f)
                    casing.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * casing.transform.localRotation;
            }

            AudioManager.Instance.PlaySound(g.AudioConfig.PullBoltClip, g.transform.position);

            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.Bolt, new Vector2(g.BoltInitialPos.x + g.CycleHandMoveDistance, g.BoltInitialPos.y), BOLT_CYCLE_MOVE_DURATION));

            if (casing != null)
                casing.Play();

            yield return new WaitForSeconds(BOLT_PAUSE_DURATION);
            AudioManager.Instance.PlaySound(g.AudioConfig.PushBoltClip, g.transform.position);

            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.Bolt, g.BoltInitialPos, BOLT_CYCLE_MOVE_DURATION));
            g.character.FrontHandIKTarget.transform.SetParent(g.transform);

            yield return new WaitForSeconds(BOLT_PAUSE_DURATION);

            g.character.CharacterSkin.ChangeHandSprite(true, g.FrontHandIdx);
            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.character.FrontHandIKTarget.transform, g.FrontHandPoint.transform.localPosition, HAND_TO_GUN_DURATION));

            g.IsBoltOpened = false;
            if (g.CurrentLoadedAmmo > 0)
                g.IsCycled = true;
            g.CycleCoroutine = null;
            g.character.IsCycling = false;
        }

        public static IEnumerator CycleForend(Gun g, bool ejectShell)
        {
            g.character.IsCycling = true;

            g.IsBoltOpened = true;
            if (Settings.AutoCycle)
                yield return new WaitForSeconds(AUTO_CYCLE_DELAY);

            g.Bolt.SetParent(g.Forend);
            g.character.BackHandIKTarget.transform.SetParent(g.Forend);

            Casing shell = null;
            if (ejectShell)
            {
                shell = ObjectPoolManager.SpawnObject(g.EffectConfig.EjectEffect, g.EjectionPoint.position, Quaternion.Euler(0, 0, g.transform.eulerAngles.z), PoolType.Unparented).GetComponent<Casing>();
                shell.transform.SetParent(g.transform);
                if (g.transform.lossyScale.x < 0f)
                    shell.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * shell.transform.localRotation;
            }

            // PULL FOREND
            AudioManager.Instance.PlaySound(g.AudioConfig.PullForendClip, g.transform.position);
            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.Forend, new Vector2(g.Forend.transform.localPosition.x - g.BoltMoveAmount, g.Forend.transform.localPosition.y), FOREND_MOVE_DURATION));

            if (shell != null)
                shell.Play();

            yield return new WaitForSeconds(FOREND_PAUSE_DURATION);

            // PUSH FOREND
            AudioManager.Instance.PlaySound(g.AudioConfig.PushForendClip, g.transform.position);
            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.Forend, new Vector2(g.Forend.transform.localPosition.x + g.BoltMoveAmount, g.Forend.transform.localPosition.y), FOREND_MOVE_DURATION));
            g.character.BackHandIKTarget.transform.SetParent(g.transform);

            g.IsBoltOpened = false;
            if (g.CurrentLoadedAmmo > 0)
                g.IsCycled = true;
            g.CycleCoroutine = null;
            g.character.IsCycling = false;
        }

    }
}
