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

namespace JohnHopooReturns
{
    public class LeechingSeed : JohnHopooReturns.Behaviour<LeechingSeed>
    {
        const string SECTION = "Leeching Seed Ignores Proc Coefficient";

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true, "Leeching Seed heals 1 from all damage, including damage over times."))
            {
                Destroy(this);
                return;
            }
            IL.RoR2.GlobalEventManager.OnHitEnemy += GlobalEventManager_OnHitEnemy;
        }

        private void GlobalEventManager_OnHitEnemy(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Seed)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)))
                )
            {
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldc_I4_0);
            }
            else Logger.LogError($"{nameof(LeechingSeed)}.{nameof(GlobalEventManager_OnHitEnemy)} IL hook failed!");
        }

        public class HealOnHitBehaviour : BaseItemBodyBehavior, IOnDamageDealtServerReceiver
        {
            [ItemDefAssociation(useOnServer = true, useOnClient = false)]
            public static ItemDef GetItemDef() => Exists ? RoR2Content.Items.Seed : null;

            public void OnDamageDealtServer(DamageReport damageReport)
            {
                if (body.healthComponent && !damageReport.damageInfo.procChainMask.HasProc(ProcType.HealOnHit))
                {
                    ProcChainMask procChainMask = damageReport.damageInfo.procChainMask;
                    procChainMask.AddProc(ProcType.HealOnHit);
                    body.healthComponent.Heal(stack, procChainMask, true);
                }
            }
        }
    }
}