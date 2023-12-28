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
using RoR2.ContentManagement;
using RoR2.Audio;

namespace JohnHopooReturns
{
    public class RoseBuckler : JohnHopooReturns.Behaviour, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Rose Buckler Re-tier";

        public sbyte armorBonus = Config.Value<sbyte>(SECTION, "Armor Bonus Per Stack", 10);

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            LanguageAPI.Add("ITEM_SPRINTARMOR_DESC", $"<style=cIsHealing>Increase armor</style> by <style=cIsHealing>{armorBonus}</style> <style=cStack>(+{armorBonus} per stack)</style> <style=cIsUtility>while sprinting</style>.");
            IL.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
        }

        private void CharacterBody_RecalculateStats(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            int locCountIndex = -1;
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.SprintArmor)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)),
                x => x.MatchStloc(out locCountIndex))
                && c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt<CharacterBody>("get_armor"),
                x => x.MatchLdloc(locCountIndex),
                x => x.MatchLdcI4(out _),
                x => x.MatchMul(),
                x => x.MatchConvR4(),
                x => x.MatchAdd(),
                x => x.MatchCallOrCallvirt<CharacterBody>("set_armor"))
                )
            {
                c.Index += 2;
                c.Next.Operand = armorBonus;
            }
            else Logger.LogError($"{nameof(RoseBuckler)}.{nameof(CharacterBody_RecalculateStats)} IL hook failed!");
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<ItemDef>("RoR2/Base/SprintArmor/SprintArmor.asset", out var SprintArmor))
            {
                yield return SprintArmor;
            }
#pragma warning disable CS0618 // Type or member is obsolete
            SprintArmor.Result.deprecatedTier = ItemTier.Tier1;
#pragma warning restore CS0618 // Type or member is obsolete
            var texBucklerIcon = Assets.LoadAssetAsync<Sprite>("texBucklerIcon");
            yield return texBucklerIcon;
            SprintArmor.Result.pickupIconSprite = (Sprite)texBucklerIcon.asset;
        }
    }
}