using System;
using UnityEngine;
using BepInEx;
using RoR2;
using HG;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using R2API;
using System.Collections;
using RoR2.ContentManagement;
using RoR2.Skills;

namespace JohnHopooReturns
{
    public class SurvivorSpeed : JohnHopooReturns.Behaviour<SurvivorSpeed>, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Survivor Speed Changes";

        public float baseSpeed = Config.Value(SECTION, "Base Movement Speed", 5.8f);
        public float sprintSpeedMultiplier = Config.Value(SECTION, "Sprint Speed Multiplier", 1.75f);

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true, "Survivors walk slower but sprint at the same speed. Also fixes Commando's roll not scaling with sprint speed."))
            {
                Destroy(this);
                return;
            }
        }

        [SystemInitializer(typeof(SurvivorCatalog))]
        public static void TryInit()
        {
            if (Exists) 
            {
                float baseSpeedScalar = Instance.baseSpeed / 7f;
                float sprintSpeedScalar = Instance.sprintSpeedMultiplier / 1.45f;
                foreach (SurvivorDef survivorDef in SurvivorCatalog.allSurvivorDefs)
                {
                    if (survivorDef.bodyPrefab && survivorDef.bodyPrefab.TryGetComponent(out CharacterBody characterBody))
                    {
                        characterBody.baseMoveSpeed *= baseSpeedScalar;
                        characterBody.sprintingSpeedMultiplier *= sprintSpeedScalar;
                    }
                }
            }
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<SkillDef>("RoR2/Base/Commando/CommandoBodyRoll.asset", out var CommandoBodyRoll))
            {
                yield return CommandoBodyRoll;
            }
            CommandoBodyRoll.Result.cancelSprintingOnActivation = false;
        }
    }
}