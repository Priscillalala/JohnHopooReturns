using System;
using UnityEngine;
using BepInEx;
using RoR2;
using HG;
using System.Collections.Generic;
using R2API;
using System.Collections;
using RoR2.ContentManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using EntityStates.Squid.SquidWeapon;

namespace JohnHopooReturns
{
    public class Polyp : JohnHopooReturns.Behaviour, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Reduced Squid Polyp Knockback";

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }   
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<EntityStateConfiguration>("RoR2/Base/Squid/EntityStates.Squid.SquidWeapon.FireSpine.asset", out var fireSpineConfiguration))
            {
                yield return fireSpineConfiguration;
            }
            fireSpineConfiguration.Result.TryModifyFieldValue(nameof(FireSpine.forceScalar), 400f);
            //fireSpineConfiguration.Result.TryModifyFieldValue(nameof(FireSpine.damageCoefficient), 7.5f);
            fireSpineConfiguration.Result.TryModifyFieldValue(nameof(FireSpine.baseDuration), 0.5f);
        }
    }
}