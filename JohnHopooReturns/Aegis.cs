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

namespace JohnHopooReturns
{
    public class Aegis : JohnHopooReturns.Behaviour<Aegis>
    {
        const string SECTION = "Aegis Buff";

        public float conversionRate = Config.Value(SECTION, "Healing To Barrier Ratio", 1f);

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            LanguageAPI.Add("ITEM_BARRIERONOVERHEAL_DESC", $"Healing past full grants you a <style=cIsHealing>temporary barrier</style> for <style=cIsHealing>{conversionRate:0%} <style=cStack>(+{conversionRate:0%} per stack)</style></style> of the amount you <style=cIsHealing>healed</style>.");
            IL.RoR2.HealthComponent.Heal += HealthComponent_Heal;
        }

        private void HealthComponent_Heal(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.barrierOnOverHeal)),
                x => x.MatchConvR4(),
                x => x.MatchLdcR4(out _),
                x => x.MatchMul())
                )
            {
                c.Index += 2;
                c.Next.Operand = conversionRate;
            }
            else Logger.LogError($"{nameof(Aegis)}.{nameof(HealthComponent_Heal)} IL hook failed!");
        }
    }
}