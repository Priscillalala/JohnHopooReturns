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
using RoR2.Items;
using RoR2.Orbs;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace JohnHopooReturns
{
    public class Tesla : JohnHopooReturns.Behaviour, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Unstable Tesla Coil Adjustments";

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            LanguageAPI.Add("ITEM_SHOCKNEARBY_PICKUP", "Shock all nearby enemies every minute.");
            LanguageAPI.Add("ITEM_SHOCKNEARBY_DESC", "Every <style=cIsDamage>minute</style>, charge a <style=cIsDamage>lightning storm</style> to repeatedly hit <style=cIsDamage>3</style> <style=cStack>(+2 per stack)</style> enemies for <style=cIsDamage>300%</style> base damage. The Tesla Coil switches off after <style=cIsDamage>10 seconds</style>.");
            IL.RoR2.Items.ShockNearbyBodyBehavior.FixedUpdate += ShockNearbyBodyBehavior_FixedUpdate;
            On.RoR2.Items.ShockNearbyBodyBehavior.ctor += ShockNearbyBodyBehavior_ctor;
        }

        private void ShockNearbyBodyBehavior_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdfld<ShockNearbyBodyBehavior>(nameof(ShockNearbyBodyBehavior.teslaBuffRollTimer)),
                x => x.MatchLdcR4(out _),
                x => x.MatchBltUn(out _))
                )
            {
                c.Index += 2;
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldarg, 0);
                c.EmitDelegate<Func<ShockNearbyBodyBehavior, float>>((behaviour) => behaviour.grantingBuff ? 10f : 50f);
            }
            else Logger.LogError($"{nameof(Tesla)}.{nameof(ShockNearbyBodyBehavior_FixedUpdate)} IL hook 1 failed!");

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt<CharacterBody>("get_damage"),
                x => x.MatchLdcR4(out _),
                x => x.MatchMul(),
                x => x.MatchStfld<LightningOrb>(nameof(LightningOrb.damageValue)))
                )
            {
                c.Index++;
                c.Next.Operand = 3f;
            }
            else Logger.LogError($"{nameof(Tesla)}.{nameof(ShockNearbyBodyBehavior_FixedUpdate)} IL hook 2 failed!");
        }

        private void ShockNearbyBodyBehavior_ctor(On.RoR2.Items.ShockNearbyBodyBehavior.orig_ctor orig, ShockNearbyBodyBehavior self)
        {
            orig(self);
            self.teslaResetListInterval = 0.3f;
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<GameObject>("RoR2/Base/ShockNearby/DisplayTeslaCoil.prefab", out var DisplayTeslaCoil))
            {
                yield return DisplayTeslaCoil;
            }
            if (DisplayTeslaCoil.Result.transform.TryFind("mdlTesla/ActiveParticles/Sparks, Trail", out Transform sparks))
            {
                sparks.localPosition = new Vector3(0f, 0.5f, 0f);
                sparks.localScale = Vector3.one * 2f;
            }
        }
    }
}