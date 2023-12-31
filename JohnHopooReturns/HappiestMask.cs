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
using System.Linq;
using Mono.Cecil;

namespace JohnHopooReturns
{
    public class HappiestMask : JohnHopooReturns.Behaviour<HappiestMask>
    {
        const string SECTION = "100% Chance Happiest Mask";

        public CharacterMaster[] aiMastersByBody;

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            LanguageAPI.Add("ITEM_GHOSTONKILL_PICKUP", "Summon a ghost on killing an enemy.");
            LanguageAPI.Add("ITEM_GHOSTONKILL_DESC", "Kills <style=cIsDamage>spawn a ghost</style> of the killed enemy with <style=cIsDamage>300%</style> damage that lasts <style=cIsDamage>15s</style> <style=cStack>(+15s per stack)</style>.");
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
            IL.RoR2.Util.TryToCreateGhost += Util_TryToCreateGhost;
        }

        private void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.GhostOnKill)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)))
                && c.TryGotoNext(MoveType.Before,
                x => x.MatchLdcR4(out _),
                x => x.MatchLdloc(out _),
                x => x.MatchCallOrCallvirt(typeof(RoR2.Util), nameof(RoR2.Util.CheckRoll)),
                x => x.MatchBrfalse(out _))
                )
            {
                c.RemoveRange(4);
            }
            else Logger.LogError($"{nameof(HappiestMask)}.{nameof(GlobalEventManager_OnCharacterDeath)} IL hook 1 failed!");

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdloc(out _),
                x => x.MatchLdcI4(out _),
                x => x.MatchMul(),
                x => x.MatchCallOrCallvirt(typeof(RoR2.Util), nameof(RoR2.Util.TryToCreateGhost)))
                )
            {
                c.Index++;
                c.Next.Operand = (sbyte)15;
            }
            else Logger.LogError($"{nameof(HappiestMask)}.{nameof(GlobalEventManager_OnCharacterDeath)} IL hook 2 failed!");
        }

        private void Util_TryToCreateGhost(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            int locDisplayClassIndex = -1;
            FieldReference bodyPrefabField = null;
            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdloc(out locDisplayClassIndex),
                x => x.MatchLdarg(0),
                x => x.MatchCallOrCallvirt(typeof(BodyCatalog), nameof(BodyCatalog.FindBodyPrefab)),
                x => x.MatchStfld(out bodyPrefabField),
                x => x.MatchLdloc(locDisplayClassIndex),
                x => x.MatchLdfld(bodyPrefabField),
                x => x.MatchCallOrCallvirt<UnityEngine.Object>("op_Implicit"),
                x => x.MatchBrtrue(out _),
                x => x.MatchLdnull(),
                x => x.MatchRet(),
                x => x.MatchCallOrCallvirt(typeof(MasterCatalog), "get_allAiMasters"),
                x => x.MatchLdloc(out _),
                x => x.MatchLdftn(out _),
                x => x.MatchNewobj<Func<CharacterMaster, bool>>(),
                x => x.MatchCallOrCallvirt(typeof(Enumerable), nameof(Enumerable.FirstOrDefault))) 
                )
            {
                c.Index++;
                c.RemoveRange(9 + 5);
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldarg, 0);
                c.EmitDelegate<Func<CharacterBody, CharacterMaster>>(targetBody => ArrayUtils.GetSafe(aiMastersByBody, (int)targetBody.bodyIndex));
            }
            else Logger.LogError($"{nameof(HappiestMask)}.{nameof(Util_TryToCreateGhost)} IL hook 1 failed!");

            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchStfld<MasterSummon>(nameof(MasterSummon.preSpawnSetupCallback)))
                && c.TryGotoPrev(MoveType.Before,
                x => x.MatchCallOrCallvirt<Delegate>(nameof(Delegate.Combine)))
                )
            {
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldarg, 2);
                c.EmitDelegate<Func<int, Action<CharacterMaster>>>((duration) => new Action<CharacterMaster>(newMaster =>
                {
                    newMaster.inventory.GiveItem(RoR2Content.Items.Ghost);
                    newMaster.inventory.GiveItem(RoR2Content.Items.HealthDecay, duration);
                    newMaster.inventory.GiveItem(RoR2Content.Items.BoostDamage, 20);
                }));
            }
            else Logger.LogError($"{nameof(HappiestMask)}.{nameof(Util_TryToCreateGhost)} IL hook 2 failed!");
        }

        [SystemInitializer(typeof(BodyCatalog), typeof(MasterCatalog))]
        public static void TryInit()
        {
            if (Exists)
            {
                Instance.aiMastersByBody = new CharacterMaster[BodyCatalog.bodyCount];
                foreach (CharacterMaster aiMaster in MasterCatalog.allAiMasters)
                {
                    BodyIndex bodyIndex = BodyCatalog.FindBodyIndex(aiMaster.bodyPrefab);
                    if (bodyIndex > BodyIndex.None && bodyIndex < (BodyIndex)BodyCatalog.bodyCount && Instance.aiMastersByBody[(int)bodyIndex] == null)
                    {
                        Instance.aiMastersByBody[(int)bodyIndex] = aiMaster;
                    }
                }
            }
        }
    }
}