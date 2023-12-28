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
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace JohnHopooReturns
{
    public class LaserScope : JohnHopooReturns.Behaviour, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Laser Scope Rework";

        public float damageBonus = Config.Value(SECTION, "Damage Bonus Per Stack", 10f);
        public float procCoefficient = Config.Value(SECTION, "Proc Coefficient", 2f);

        public static DamageAPI.ModdedDamageType DidSuperCrit { get; private set; }
        public static GameObject CritSparkSuperHeavy { get; private set; }

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            DidSuperCrit = DamageAPI.ReserveDamageType();
            LanguageAPI.Add("ITEM_CRITDAMAGE_PICKUP", "10% chance for Critical Strikes to 'Critically Strike' again, dealing unreal damage.");
            LanguageAPI.Add("ITEM_CRITDAMAGE_DESC", $"Gain <style=cIsDamage>5% critical chance</style>. Your <style=cIsDamage>Critical Strikes</style> have a <style=cIsDamage>10%</style> chance to deal an additional <style=cIsDamage>+{damageBonus:0%} <style=cStack>(+{damageBonus:0%} per stack)</style></style> damage.");
            IL.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
            IL.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;
            IL.RoR2.GlobalEventManager.OnCrit += GlobalEventManager_OnCrit;
            RecalculateStatsAPI.GetStatCoefficients += RecalculateStatsAPI_GetStatCoefficients;
        }

        private void CharacterBody_RecalculateStats(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.CritDamage)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)))
                )
            {
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldc_I4_0);
            }
            else Logger.LogError($"{nameof(LaserScope)}.{nameof(CharacterBody_RecalculateStats)} IL hook failed!");
        }

        private void HealthComponent_TakeDamage(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            int locAttackerBodyIndex = -1;
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdloc(out locAttackerBodyIndex),
                x => x.MatchCallOrCallvirt<CharacterBody>("get_critMultiplier"))
                )
            {
                c.Emit(OpCodes.Ldarg, 0);
                c.Emit(OpCodes.Ldarg, 1);
                c.Emit(OpCodes.Ldloc, locAttackerBodyIndex);
                c.EmitDelegate<Func<float, HealthComponent, DamageInfo, CharacterBody, float>>((critMultiplier, healthComponent, damageInfo, attackerBody) =>
                {
                    if (attackerBody && attackerBody.inventory)
                    {
                        int count = attackerBody.inventory.GetItemCount(DLC1Content.Items.CritDamage);
                        if (count > 0 && RoR2.Util.CheckRoll(10f, attackerBody.master)) 
                        {
                            critMultiplier += 10f * count;
                            damageInfo.procCoefficient *= procCoefficient;
                            damageInfo.AddModdedDamageType(DidSuperCrit);
                        }
                    }
                    return critMultiplier;
                });
            }
            else Logger.LogError($"{nameof(LaserScope)}.{nameof(HealthComponent_TakeDamage)} IL hook failed!");
        }

        private void GlobalEventManager_OnCrit(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            int locCritEffectIndex = -1;
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdstr("Prefabs/Effects/ImpactEffects/CritsparkHeavy"),
                x => x.MatchCallOrCallvirt(typeof(LegacyResourcesAPI), nameof(LegacyResourcesAPI.Load)),
                x => x.MatchStloc(out locCritEffectIndex))
                )
            {
                c.Index--;
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldloc, locCritEffectIndex);
            }
            else Logger.LogError($"{nameof(LaserScope)}.{nameof(GlobalEventManager_OnCrit)} IL hook 1 failed!");

            if (c.TryGotoNext(MoveType.AfterLabel,
                x => x.MatchLdloc(locCritEffectIndex),
                x => x.MatchCallOrCallvirt<UnityEngine.Object>("op_Implicit"),
                x => x.MatchBrfalse(out _))
                )
            {
                c.Emit(OpCodes.Ldloc, locCritEffectIndex);
                c.Emit(OpCodes.Ldarg, 2);
                c.EmitDelegate<Func<GameObject, DamageInfo, GameObject>>((critEffect, damageInfo) => damageInfo.HasModdedDamageType(DidSuperCrit) ? CritSparkSuperHeavy : critEffect);
                c.Emit(OpCodes.Stloc, locCritEffectIndex);
            }
            else Logger.LogError($"{nameof(LaserScope)}.{nameof(GlobalEventManager_OnCrit)} IL hook 2 failed!");
        }

        private void RecalculateStatsAPI_GetStatCoefficients(CharacterBody sender, RecalculateStatsAPI.StatHookEventArgs args)
        {
            if (sender.inventory && sender.inventory.GetItemCount(DLC1Content.Items.CritDamage) > 0)
            {
                args.critAdd += 5f;
            }
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<GameObject>("RoR2/Base/Common/VFX/CritsparkHeavy.prefab", out var CritsparkHeavy))
            {
                yield return CritsparkHeavy;
            }
            CritSparkSuperHeavy = PrefabAPI.InstantiateClone(CritsparkHeavy.Result, "CritSparkSuperHeavy", false);
            CritSparkSuperHeavy.GetComponent<EffectComponent>().soundName = "Play_railgunner_m2_headshot";
            CritSparkSuperHeavy.GetComponent<VFXAttributes>().vfxPriority = VFXAttributes.VFXPriority.Medium;
            Content.effectDefs.Add(new EffectDef(CritSparkSuperHeavy));
        }
    }
}