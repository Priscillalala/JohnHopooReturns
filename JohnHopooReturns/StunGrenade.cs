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

namespace JohnHopooReturns
{
    public class StunGrenade : JohnHopooReturns.Behaviour
    {
        const string SECTION = "Stun Grenade Buff";

        public float totalDamageCoefficient = Config.Value(SECTION, "Total Damage Coefficient", 0.8f);

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true, "Stun Grenade deals bonus damage on proc."))
            {
                Destroy(this);
                return;
            }
            LanguageAPI.Add("ITEM_STUNCHANCEONHIT_DESC", $"<style=cIsDamage>5%</style> <style=cStack>(+5% per stack)</style> chance on hit to <style=cIsDamage>stun</style> enemies for <style=cIsDamage>{totalDamageCoefficient:0%}</style> TOTAL damage.");
            IL.RoR2.SetStateOnHurt.OnTakeDamageServer += SetStateOnHurt_OnTakeDamageServer;
        }

        private void SetStateOnHurt_OnTakeDamageServer(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt(typeof(EffectManager), nameof(EffectManager.SimpleImpactEffect)))
                && c.TryGotoNext(MoveType.After,
                x => x.MatchCallOrCallvirt<SetStateOnHurt>(nameof(SetStateOnHurt.SetStun)))
                )
            {
                c.Emit(OpCodes.Ldarg, 1);
                c.EmitDelegate<Action<DamageReport>>((damageReport) =>
                {
                    if (!damageReport.attackerBody)
                    {
                        return;
                    }
                    DamageInfo damageInfo = new DamageInfo
                    {
                        attacker = damageReport.attacker,
                        crit = damageReport.damageInfo.crit,
                        damage = RoR2.Util.OnHitProcDamage(damageReport.damageInfo.damage, damageReport.attackerBody.damage, totalDamageCoefficient),
                        damageColorIndex = DamageColorIndex.Item,
                        inflictor = null,
                        position = damageReport.damageInfo.position,
                        procCoefficient = 0f,
                    };
                    damageReport.victim.TakeDamage(damageInfo);
                });
            }
            else Logger.LogError($"{(nameof(StunGrenade))}.{nameof(SetStateOnHurt_OnTakeDamageServer)} IL hook failed!");
        }
    }
}