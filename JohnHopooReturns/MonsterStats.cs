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
using System.Linq;

namespace JohnHopooReturns
{
    public class MonsterStats : JohnHopooReturns.Behaviour<MonsterStats>, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Monster Health Tweaks";

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
        }

        [SystemInitializer(typeof(CombatDirector))]
        public static void TryInit()
        {
            if (Exists)
            {
                CombatDirector.EliteTierDef tierOne = CombatDirector.eliteTiers.FirstOrDefault(x => Array.IndexOf(x.eliteTypes, RoR2Content.Elites.Fire) >= 0);
                if (tierOne != null)
                {
                    tierOne.costMultiplier = 4f;
                    foreach (EliteDef eliteDef in tierOne.eliteTypes)
                    {
                        eliteDef.healthBoostCoefficient = Mathf.Max(1f, eliteDef.healthBoostCoefficient - 1f);
                    }
                }
                CombatDirector.EliteTierDef honor = CombatDirector.eliteTiers.FirstOrDefault(x => Array.IndexOf(x.eliteTypes, RoR2Content.Elites.FireHonor) >= 0);
                if (honor != null)
                {
                    honor.costMultiplier = 2.5f;
                    foreach (EliteDef eliteDef in honor.eliteTypes)
                    {
                        eliteDef.healthBoostCoefficient = Mathf.Max(1f, eliteDef.healthBoostCoefficient - 1f);
                    }
                }
                CombatDirector.EliteTierDef tierTwo = CombatDirector.eliteTiers.FirstOrDefault(x => Array.IndexOf(x.eliteTypes, RoR2Content.Elites.Poison) >= 0);
                if (tierTwo != null)
                {
                    tierTwo.costMultiplier = 24f;
                    foreach (EliteDef eliteDef in tierTwo.eliteTypes)
                    {
                        eliteDef.healthBoostCoefficient = Mathf.Max(1f, eliteDef.healthBoostCoefficient * 0.75f);
                    }
                }
            }
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<GameObject>("RoR2/Base/Wisp/WispBody.prefab", out var wispBodyPrefab))
            {
                yield return wispBodyPrefab;
            }
            if (wispBodyPrefab.Result.TryGetComponent(out CharacterBody wispBody))
            {
                wispBody.baseMaxHealth = 60f;
                wispBody.levelMaxHealth = 18f;
            }
            if (!LoadAddressable<GameObject>("RoR2/Base/Jellyfish/JellyfishBody.prefab", out var jellyfishBodyPrefab))
            {
                yield return jellyfishBodyPrefab;
            }
            if (jellyfishBodyPrefab.Result.TryGetComponent(out CharacterBody jellyfishBody))
            {
                jellyfishBody.baseMaxHealth = 80f;
                jellyfishBody.levelMaxHealth = 24f;
            }
            if (!LoadAddressable<GameObject>("RoR2/Base/Beetle/BeetleBody.prefab", out var beetleBodyPrefab))
            {
                yield return beetleBodyPrefab;
            }
            if (beetleBodyPrefab.Result.TryGetComponent(out CharacterBody beetleBody))
            {
                beetleBody.baseMaxHealth = 144f;
                beetleBody.levelMaxHealth = 43f;
            }
            if (!LoadAddressable<GameObject>("RoR2/Base/Lemurian/LemurianBody.prefab", out var lemurianBodyPrefab))
            {
                yield return lemurianBodyPrefab;
            }
            if (lemurianBodyPrefab.Result.TryGetComponent(out CharacterBody lemurianBody))
            {
                lemurianBody.baseMaxHealth = 144f;
                lemurianBody.levelMaxHealth = 43f;
            }

            if (!LoadAddressable<CharacterSpawnCard>("RoR2/Base/GreaterWisp/cscGreaterWisp.asset", out var cscGreaterWisp))
            {
                yield return cscGreaterWisp;
            }
            cscGreaterWisp.Result.directorCreditCost = 100;
        }
    }
}