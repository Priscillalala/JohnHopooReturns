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
using RoR2.Orbs;
using UnityEngine.Networking;
using RoR2.Items;
using BepInEx.Bootstrap;
using RoR2.UI;
using System.Linq;
using UnityEngine.UI;
using RoR2.Audio;
using RoR2.ContentManagement;

namespace JohnHopooReturns
{
    public class BisonSteak : JohnHopooReturns.Behaviour<BisonSteak>, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Bison Steak Rework";

        public float health = Config.Value(SECTION, "Health Bonus", 50f);
        public float maxHealthBonus = Config.Value(SECTION, "Max Health Bonus Per Stack", 0.8f);

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            LanguageAPI.Add("ITEM_FLATHEALTH_NAME", "Bison Steak");
            LanguageAPI.Add("ITEM_FLATHEALTH_PICKUP", "Increase max health while fighting bosses.");
            LanguageAPI.Add("ITEM_FLATHEALTH_DESC", $"Increases <style=cIsHealing>maximum health</style> by <style=cIsHealing>{health}</style> <style=cStack>(+{health} per stack)</style> while a boss is alive.");
            IL.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
        }

        private void CharacterBody_RecalculateStats(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.FlatHealth)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)))
                )
            {
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldc_I4_0);
            }
            else Logger.LogError($"{nameof(BisonSteak)}.{nameof(CharacterBody_RecalculateStats)} IL hook failed!");
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<ItemDef>("RoR2/Base/FlatHealth/FlatHealth.asset", out var FlatHealth))
            {
                yield return FlatHealth;
            }
            FlatHealth.Result.tags = new[] { ItemTag.Healing, ItemTag.AIBlacklist };
        }

        public class ElixirBehaviour : BaseItemBodyBehavior, IOnDamageDealtServerReceiver
        {
            [ItemDefAssociation(useOnServer = true, useOnClient = true)]
            public static ItemDef GetItemDef() => Exists ? RoR2Content.Items.FlatHealth : null;

            public void OnEnable()
            {
                RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
            }

            public void OnDisable()
            {
                RecalculateStatsAPI.GetStatCoefficients -= RecalculateStatsAPI_GetStatCoefficients;
            }

            private void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
            {

            }

            public void OnDamageDealtServer(DamageReport damageReport)
            {
                
            }
        }
    }
}