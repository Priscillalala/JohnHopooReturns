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

namespace JohnHopooReturns
{
    public class Scythe : JohnHopooReturns.Behaviour
    {
        const string SECTION = "New Harvesters Scythe Visuals";

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            IL.RoR2.GlobalEventManager.OnCrit += GlobalEventManager_OnCrit;
            On.RoR2.GlobalEventManager.OnHitEnemy += GlobalEventManager_OnHitEnemy;
        }

        private void GlobalEventManager_OnCrit(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.HealOnCrit)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)))
                )
            {
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldc_I4_0);
            }
            else Logger.LogError($"{nameof(Scythe)}.{nameof(GlobalEventManager_OnCrit)} IL hook failed!");
        }

        private void GlobalEventManager_OnHitEnemy(On.RoR2.GlobalEventManager.orig_OnHitEnemy orig, GlobalEventManager self, DamageInfo damageInfo, GameObject victim)
        {
            if (NetworkServer.active 
                && !damageInfo.rejected 
                && damageInfo.attacker 
                && damageInfo.crit
                && damageInfo.procCoefficient > 0f
                && !damageInfo.procChainMask.HasProc(ProcType.HealOnCrit)
                && damageInfo.attacker.TryGetComponent(out CharacterBody attackerBody) 
                && attackerBody.inventory
                && attackerBody.healthComponent
                )
            {
                int count = attackerBody.inventory.GetItemCount(RoR2Content.Items.HealOnCrit);
                if (count > 0)
                {
                    RoR2.Util.PlaySound("Play_item_proc_crit_heal", damageInfo.attacker);
                    HealOnCritOrb healOnCritOrb = new HealOnCritOrb
                    {
                        origin = victim?.GetComponent<CharacterBody>()?.corePosition ?? attackerBody.corePosition,
                        target = attackerBody.mainHurtBox,
                        healValue = (4f + 4f * count) * damageInfo.procCoefficient,
                        procChainMask = damageInfo.procChainMask,
                    };
                    healOnCritOrb.procChainMask.AddProc(ProcType.HealOnCrit);
                    OrbManager.instance.AddOrb(healOnCritOrb);
                }
            }
            orig(self, damageInfo, victim);
        }

        /*private void GlobalEventManager_OnCrit(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.HealOnCrit)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)))
                && c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt<HealthComponent>(nameof(HealthComponent.Heal)),
                x => x.MatchPop())
                )
            {
                c.Remove();
                c.Remove();
                c.Emit(OpCodes.Ldarg, 2);
                c.EmitDelegate<Action<HealthComponent, float, ProcChainMask, bool, DamageInfo>>((healthComponent, amount, procChainMask, _, damageInfo) =>
                {
                    HealOrb healOrb = new HealOrb 
                    {
                        origin = damageInfo?.
                    };
                    healOrb.origin = base.transform.position;
                    healOrb.target = component.body.mainHurtBox;
                    healOrb.healValue = damageReport.damageDealt * this.fractionOfDamage;
                    healOrb.overrideDuration = 0.3f;
                    OrbManager.instance.AddOrb(healOrb);
                });
            }
        }*/

        public class HealOnCritOrb : Orb
        {
            public override void Begin()
            {
                if (target)
                {
                    duration = 0.3f;
                    float scale = target.healthComponent ? Mathf.Min(healValue / target.healthComponent.fullHealth, 1f) : 1f;
                    EffectData effectData = new EffectData
                    {
                        scale = scale,
                        origin = origin,
                        genericFloat = duration
                    };
                    effectData.SetHurtBoxReference(target);
                    EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/OrbEffects/HealthOrbEffect"), effectData, true);
                }
            }

            public override void OnArrival()
            {
                target?.healthComponent?.Heal(healValue, procChainMask, true);
            }

            public float healValue;
            public ProcChainMask procChainMask;
        }
    }
}