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
using RoR2.ContentManagement;
using EntityStates.EngiTurret.EngiTurretWeapon;

namespace JohnHopooReturns
{
    public class Bungus : JohnHopooReturns.Behaviour, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Bustling Fungus Returns";

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            LanguageAPI.Add("ITEM_MUSHROOM_DESC", "After standing still for <style=cIsHealing>1</style> second, create a zone that <style=cIsHealing>heals</style> for <style=cIsHealing>4.5%</style> <style=cStack>(+4.5% per stack)</style> of your <style=cIsHealing>health</style> every second to all allies within <style=cIsHealing>3m</style> <style=cStack>(+1.5m per stack)</style>.");
            IL.RoR2.Items.MushroomBodyBehavior.FixedUpdate += MushroomBodyBehavior_FixedUpdate;
        }

        private void MushroomBodyBehavior_FixedUpdate(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.GetNotMoving)))
                )
            {
                c.Emit(OpCodes.Ldarg, 0);
                c.EmitDelegate<Func<bool, MushroomBodyBehavior, bool>>((active, behaviour) => active && behaviour.body.outOfCombatStopwatch >= 1f);
            }
            else Logger.LogError($"{nameof(Bungus)}.{nameof(MushroomBodyBehavior_FixedUpdate)} IL hook 1 failed!");

            const float INTERVAL = 0.5f;

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchStfld<HealingWard>(nameof(HealingWard.interval)))
                )
            {
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldc_R4, INTERVAL);
            }
            else Logger.LogError($"{nameof(Bungus)}.{nameof(MushroomBodyBehavior_FixedUpdate)} IL hook 2 failed!");

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchStfld<HealingWard>(nameof(HealingWard.healFraction)))
                )
            {
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldarg, 0);
                c.EmitDelegate<Func<MushroomBodyBehavior, float>>((behaviour) => behaviour.stack * 0.045f * INTERVAL);
            }
            else Logger.LogError($"{nameof(Bungus)}.{nameof(MushroomBodyBehavior_FixedUpdate)} IL hook 3 failed!");
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<EntityStateConfiguration>("RoR2/Base/Engi/EntityStates.EngiTurret.EngiTurretWeapon.FireGauss.asset", out var fireGaussConfiguration))
            {
                yield return fireGaussConfiguration;
            }
            fireGaussConfiguration.Result.TryModifyFieldValue(nameof(FireGauss.damageCoefficient), 1f);
        }
    }
}