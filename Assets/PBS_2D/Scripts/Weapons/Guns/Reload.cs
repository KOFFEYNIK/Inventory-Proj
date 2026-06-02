using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace PBS2D
{
    public static class Reload
    {
        private const float DELAY_DURATION = .15f;
        private const float WAIT_AT_BODY_DURATION = .3f;
        private const float HAND_TO_MAG_DURATION = .15f;
        private const float HAND_TO_BODY_DURATION = .3f;
        private const float MAG_TO_GUN_DURATION = .4f;
        private const float HAND_SNAP_DURATION = .1f;
        private const float MAG_EJECT_IMPULSE = .015f;
        private const float MAG_EJECT_SPEED = .48f;

        private static void EjectMag(Gun g)
        {
            AudioManager.Instance.PlaySound(g.AudioConfig.ReleaseMagClip, g.transform.position);

            g.StartCoroutine(MoveMagToPosition(g, g.MagRB, MAG_EJECT_SPEED * g.MagInsertDepth));
        }

        private static IEnumerator MoveMagToPosition(Gun gun, Rigidbody2D magRb, float duration)
        {
            float elapsed = 0f;
            Vector2 initialPos = magRb.transform.localPosition;
            Vector2 targetPos = new(magRb.transform.localPosition.x, magRb.transform.localPosition.y - gun.MagInsertDepth);

            while (elapsed < duration)
            {
                magRb.transform.localPosition = Vector2.Lerp(initialPos, targetPos, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }

            magRb.transform.localPosition = targetPos;

            magRb.transform.parent = null;
            magRb.bodyType = RigidbodyType2D.Dynamic;

            magRb.linearVelocity = gun.GetComponent<Rigidbody2D>().linearVelocity;
            magRb.AddForce(-magRb.transform.up * MAG_EJECT_IMPULSE, ForceMode2D.Impulse);
        }

        #region MAGAZINE
        public static IEnumerator ReloadMagazine(Gun g)
        {
            g.character.IsReloading = true;
            g.character.BackHandIKTarget.GetComponent<SortingGroup>().enabled = true;
            g.character.CharacterRotation.WeaponHolderBalance.CurrentWeight = .1f;

            if (!g.character.IsAiming && !g.character.IsRunning) g.character.WeaponManager.HandleGunReloadState();

            yield return ReloadMagazineCoroutine(g);

            yield return new WaitForSeconds(.3f);
            g.character.CharacterRotation.WeaponHolderBalance.CurrentWeight = .25f;
        }

        public static IEnumerator ReloadShell(Gun g)
        {
            g.character.IsReloading = true;
            g.character.CharacterRotation.WeaponHolderBalance.CurrentWeight = .1f;

            if (!g.character.IsAiming && !g.character.IsRunning) g.character.WeaponManager.HandleGunReloadState();

            yield return ReloadShellCoroutine(g);

            yield return new WaitForSeconds(.3f);
            g.character.CharacterRotation.WeaponHolderBalance.CurrentWeight = .25f;
        }

        public static IEnumerator ReloadMagazineCoroutine(Gun g)
        {
            bool empty = g.CurrentLoadedAmmo == 0;
            bool hasOutline = g.TryRemoveOutline(g.CurrentMag);

            // NEW MAG
            EjectMag(g);
            GameObject newMag = Object.Instantiate(g.MagPrefab, Vector2.zero, g.transform.rotation, g.character.BackHandIKTarget.transform);
            newMag.transform.localPosition = g.MagPrefab.transform.localPosition - g.ReloadHandPoint.transform.localPosition;
            newMag.GetComponent<SpriteRenderer>().enabled = false;
            newMag.transform.localScale = Vector2.one;
            g.CurrentMag = newMag;
            g.MagRB = newMag.GetComponent<Rigidbody2D>();
            g.MagRB.bodyType = RigidbodyType2D.Kinematic;

            if (hasOutline) g.AddOutline(g.CurrentMag);

            // HAND TO BODY
            yield return MoveHandToBody(g, WAIT_AT_BODY_DURATION);

            // HAND TO GUN
            g.character.CharacterSkin.ChangeHandSprite(false, g.ReloadHandIdx);
            g.CurrentMag.GetComponent<SpriteRenderer>().enabled = true;
            g.CurrentMag.GetComponent<SpriteRenderer>().sortingLayerName = "Default";
            yield return MoveHandToGun(g, true, true);

            // SNAP MAG
            g.character.CharacterSkin.BackHandTargetSRenderer.sortingLayerName = "Outer Limb";
            newMag.GetComponent<SpriteRenderer>().sortingLayerName = "Gun";
            g.character.BackHandIKTarget.GetComponent<SortingGroup>().enabled = false;
            yield return HandSnap(g, g.Stats.MaxLoadedAmmo);

            // RETURN TO NORMAL
            g.CurrentMag.transform.SetParent(g.transform);
            g.character.CharacterSkin.ChangeHandSprite(false, g.BackHandIdx);
            g.character.BackHandIKTarget.transform.SetParent(g.BackHandPoint);

            if (!empty || (g.Stats.CycleType == GunCycleType.BoltAction && !Settings.AutoCycle)) // NOT EMPTY
            {
                if (!g.character.IsRunning && !g.character.IsAiming)
                    g.character.WeaponManager.HandleWeaponIdleState(.1f);

                g.character.IsReloading = false;

                yield return g.StartCoroutine(AnimationUtility.MoveTo(g.character.BackHandIKTarget.transform, Vector2.zero, HAND_TO_MAG_DURATION));
            }
            else // EMPTY
            {
                yield return g.StartCoroutine(AnimationUtility.MoveTo(g.character.BackHandIKTarget.transform, Vector2.zero, HAND_TO_MAG_DURATION));

                yield return new WaitForSeconds(DELAY_DURATION);

                if (!g.OpenBoltWhenEmpty)
                    yield return g.StartCoroutine(Cycle.CockGun(g));
                else
                    yield return g.StartCoroutine(Cycle.PushBolt(g, true));

                if (!g.character.IsRunning && !g.character.IsAiming)
                    g.character.WeaponManager.HandleWeaponIdleState(.1f);

                g.character.IsReloading = false;
            }
        }

        #endregion
        #region SHELL

        public static IEnumerator ReloadShellCoroutine(Gun g)
        {
            // Hand to body
            g.character.BackHandIKTarget.GetComponent<SortingGroup>().enabled = true;
            yield return MoveHandToBody(g, 0f);

            // Hand to gun
            g.character.CharacterSkin.ChangeHandSprite(false, g.ReloadHandIdx);
            g.NewShell = Object.Instantiate(g.ShellPrefab, Vector2.zero, g.character.BackHandIKTarget.transform.rotation).GetComponent<SpriteRenderer>();
            if (g.transform.lossyScale.x < 0) g.NewShell.transform.localScale = new(-g.NewShell.transform.localScale.x, g.NewShell.transform.localScale.y);
            g.NewShell.sortingLayerName = "Default";
            g.NewShell.transform.SetParent(g.character.BackHandIKTarget.transform);
            g.NewShell.transform.localPosition = g.ShellHandOffset;
            yield return MoveHandToGun(g, false, false);

            // SNAP SHELL
            AudioManager.Instance.PlaySound(g.AudioConfig.InsertShellClip, g.transform.position);
            g.character.BackHandIKTarget.GetComponent<SortingGroup>().enabled = false;
            yield return HandSnap(g, g.CurrentLoadedAmmo + 1);
            Object.Destroy(g.NewShell);

            if (g.CurrentLoadedAmmo == 1 && Settings.AutoCycle)
            {
                g.character.CharacterSkin.ChangeHandSprite(false, g.BackHandIdx);
                g.character.CharacterSkin.BackHandTargetSRenderer.sortingLayerName = "Outer Limb";
                g.character.BackHandIKTarget.transform.SetParent(g.BackHandPoint);
                yield return g.StartCoroutine(AnimationUtility.MoveTo(g.character.BackHandIKTarget.transform, Vector2.zero, HAND_TO_MAG_DURATION));

                yield return Cycle.CycleForend(g, false);

                yield return new WaitForSeconds(DELAY_DURATION);
            }

            if (g.CurrentLoadedAmmo == g.Stats.MaxLoadedAmmo || g.AbortReload || g.CurrentReserveAmmo == 0)
            {
                // RETURN TO NORMAL
                g.character.CharacterSkin.ChangeHandSprite(false, g.BackHandIdx);
                g.character.CharacterSkin.BackHandTargetSRenderer.sortingLayerName = "Outer Limb";
                g.character.BackHandIKTarget.transform.SetParent(g.BackHandPoint);

                if (!g.character.IsRunning)
                    g.character.WeaponManager.ChangeWeaponRotation(0);

                yield return g.StartCoroutine(AnimationUtility.MoveTo(g.character.BackHandIKTarget.transform, Vector2.zero, HAND_TO_MAG_DURATION));

                g.character.IsReloading = false;
                g.AbortReload = false;
            }
            else
            {
                // LOAD NEW SHELL
                yield return g.StartCoroutine(ReloadShellCoroutine(g));
            }
        }
        #endregion

        #region AUXILIAR
        private static IEnumerator MoveHandToBody(Gun g, float waitDuration)
        {
            g.character.CharacterSkin.DefaultBackHand();
            g.character.CharacterSkin.BackHandTargetSRenderer.sortingLayerName = "Default";

            g.character.BackHandIKTarget.transform.SetParent(g.character.LowerTorso.Transform);

            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.character.BackHandIKTarget.transform, g.HandToBodyOffset, HAND_TO_BODY_DURATION));

            yield return new WaitForSeconds(waitDuration);
        }

        private static IEnumerator MoveHandToGun(Gun g, bool playSnapSound, bool mag)
        {
            g.character.BackHandIKTarget.transform.SetParent(g.transform);
            g.character.BackHandIKTarget.transform.localRotation = Quaternion.identity;

            Vector2 startPos = g.character.BackHandIKTarget.transform.localPosition;
            Vector2 midPosGun = g.transform.InverseTransformPoint(g.character.LowerTorso.Transform.position);
            Vector2 endPos = g.ReloadHandPoint.transform.localPosition;
            if (mag)
            {
                endPos = new(endPos.x, endPos.y - g.MagInsertDepth);
            }
            else
            {
                endPos = new(endPos.x - g.ShellInsertDepth, endPos.y);
            }
            Vector2 controlPoint = (midPosGun + endPos) / 2f + new Vector2(0f, -0.5f);

            for (float elapsed = 0; elapsed < MAG_TO_GUN_DURATION; elapsed += Time.deltaTime)
            {
                float t = elapsed / MAG_TO_GUN_DURATION;
                g.character.BackHandIKTarget.transform.localPosition = Mathf.Pow(1 - t, 2) * startPos +
                                         2 * (1 - t) * t * controlPoint +
                                         t * t * endPos;
                yield return null;
            }

            g.character.BackHandIKTarget.transform.localPosition = endPos;

            if (playSnapSound)
            {
                AudioManager.Instance.PlaySound(g.AudioConfig.SnapMagClip, g.transform.position);
            }

            yield return new WaitForSeconds(DELAY_DURATION);
        }

        private static IEnumerator HandSnap(Gun g, int ammo)
        {
            yield return g.StartCoroutine(AnimationUtility.MoveTo(g.character.BackHandIKTarget.transform, g.ReloadHandPoint.transform.localPosition, HAND_SNAP_DURATION));

            int ammoNeeded = ammo - g.CurrentLoadedAmmo;

            if (ammoNeeded > 0) // we're adding ammo
            {
                int ammoToLoad = Mathf.Min(ammoNeeded, g.CurrentReserveAmmo);
                g.CurrentLoadedAmmo += ammoToLoad;
                g.CurrentReserveAmmo -= ammoToLoad;
            }

            if (g.IsEquippedByPlayer) WeaponUI.Instance.UpdateAmmoUI();

            yield return new WaitForSeconds(DELAY_DURATION);
        }

        #endregion
    }
}
