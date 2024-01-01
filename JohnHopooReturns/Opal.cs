using System;
using UnityEngine;
using BepInEx;
using RoR2;
using HG;
using System.Collections.Generic;
using R2API;
using System.Collections;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using UnityEngine.Networking;
using RoR2.ContentManagement;

namespace JohnHopooReturns
{
    public class Opal : JohnHopooReturns.Behaviour, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Uncommon Oddly-shaped Opal";

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
            if (!LoadAddressable<ItemDef>("RoR2/DLC1/OutOfCombatArmor/OutOfCombatArmor.asset", out var OutOfCombatArmor))
            {
                yield return OutOfCombatArmor;
            }
#pragma warning disable CS0618 // Type or member is obsolete
            OutOfCombatArmor.Result.deprecatedTier = ItemTier.Tier2;
#pragma warning restore CS0618 // Type or member is obsolete
            var texOddlyShapedOpalIcon = Assets.LoadAssetAsync<Sprite>("texOddlyShapedOpalIcon");
            yield return texOddlyShapedOpalIcon;
            OutOfCombatArmor.Result.pickupIconSprite = (Sprite)texOddlyShapedOpalIcon.asset;
        }
    }
}