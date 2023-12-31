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
using System.Linq;
using RoR2.ContentManagement;
using EntityStates.DroneWeaponsChainGun;

namespace JohnHopooReturns
{
    public class SpareDroneParts : JohnHopooReturns.Behaviour<SpareDroneParts>, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Spare Drone Parts";

        public int chainGunBounces = Config.Value(SECTION, "Chain Gun Bounces", 0);
        public float chainGunDuration = Config.Value(SECTION, "Chain Gun Base Duration", 0.15f);
        public bool updatedItemDisplays = Config.Value(SECTION, "Updated Item Displays", true);

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            LanguageAPI.Add("ITEM_DRONEWEAPONS_DESC", $"Gain <style=cIsDamage>Col. Droneman</style>. \nDrones gain <style=cIsDamage>+50%</style> <style=cStack>(+50% per stack)</style> attack speed and cooldown reduction. \nDrones gain <style=cIsDamage>10%</style> chance to fire a <style=cIsDamage>missile</style> on hit, dealing <style=cIsDamage>300%</style> TOTAL damage. \nDrones gain an <style=cIsDamage>automatic chain gun</style> that deals <style=cIsDamage>6x100%</style>damage.");
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<EntityStateConfiguration>("RoR2/DLC1/DroneWeapons/EntityStates.DroneWeaponsChainGun.FireChainGun.asset", out var fireChainGunConfiguration))
            {
                yield return fireChainGunConfiguration;
            }
            fireChainGunConfiguration.Result.TryModifyFieldValue(nameof(FireChainGun.additionalBounces), chainGunBounces);
            fireChainGunConfiguration.Result.TryModifyFieldValue(nameof(FireChainGun.baseDuration), chainGunDuration);

            if (!updatedItemDisplays)
            {
                yield break;
            }

            if (!LoadAddressable<GameObject>("RoR2/DLC1/DroneWeapons/DisplayDroneWeaponMinigun.prefab", out var chainGunDisplay))
            {
                yield return chainGunDisplay;
            }
            if (!LoadAddressable<GameObject>("RoR2/DLC1/DroneWeapons/DisplayDroneWeaponRobotArm.prefab", out var robotArmDisplay))
            {
                yield return robotArmDisplay;
            }
            if (!LoadAddressable<GameObject>("RoR2/DLC1/DroneWeapons/DisplayDroneWeaponLauncher.prefab", out var launcherDisplay))
            {
                yield return launcherDisplay;
            }
            if (!LoadAddressable<ItemDef>("RoR2/DLC1/DroneWeapons/DroneWeaponsDisplay1.asset", out var DroneWeaponsDisplay1))
            {
                yield return DroneWeaponsDisplay1;
            }
            if (!LoadAddressable<ItemDef>("RoR2/DLC1/DroneWeapons/DroneWeaponsDisplay2.asset", out var DroneWeaponsDisplay2))
            {
                yield return DroneWeaponsDisplay2;
            }

            static void TryModifyItemDisplayRule(ItemDisplayRuleSet idrs, UnityEngine.Object keyAsset, GameObject followerPrefab, ModifyIdrCallback callback)
            {
                for (int i = 0; i < idrs.keyAssetRuleGroups.Length; i++)
                {
                    ref ItemDisplayRuleSet.KeyAssetRuleGroup ruleGroup = ref idrs.keyAssetRuleGroups[i];
                    if (ruleGroup.keyAsset == keyAsset && !ruleGroup.displayRuleGroup.isEmpty)
                    {
                        for (int j = 0; j < ruleGroup.displayRuleGroup.rules.Length; j++)
                        {
                            ref ItemDisplayRule rule = ref ruleGroup.displayRuleGroup.rules[j];
                            if (rule.followerPrefab == followerPrefab)
                            {
                                callback(ref rule);
                                return;
                            }
                        }
                    }
                }
            }

            if (!LoadAddressable<ItemDisplayRuleSet>("RoR2/Base/Drones/idrsMegaDrone.asset", out var idrsMegaDrone))
            {
                yield return idrsMegaDrone;
            }
            TryModifyItemDisplayRule(idrsMegaDrone.Result, DroneWeaponsDisplay1.Result, chainGunDisplay.Result, (ref ItemDisplayRule idr) =>
            {
                idr.childName = "Head";
                idr.localPos = new Vector3(-1.01503F, 0.125F, 0.00001F);
                idr.localAngles = new Vector3(357.9697F, 179.9687F, 103.5902F);
            });

            if (!LoadAddressable<ItemDisplayRuleSet>("RoR2/Base/Drones/idrsDrone1.asset", out var idrsDrone1))
            {
                yield return idrsDrone1;
            }
            TryModifyItemDisplayRule(idrsDrone1.Result, DroneWeaponsDisplay1.Result, chainGunDisplay.Result, (ref ItemDisplayRule idr) =>
            {
                idr.childName = "Head";
                idr.localPos = new Vector3(0.86973F, -0.07F, -0.05036F);
                idr.localAngles = new Vector3(273.0469F, 61.64452F, 25.27165F);
            });

            void AddNewDroneWeaponsDisplay(ItemDisplayRuleSet idrs, ItemDisplayRule chainGunRule, ItemDisplayRule robotArmRule, ItemDisplayRule launcherRule)
            {
                ArrayUtils.ArrayAppend(ref idrs.keyAssetRuleGroups, new ItemDisplayRuleSet.KeyAssetRuleGroup 
                { 
                    keyAsset = DroneWeaponsDisplay1.Result, 
                    displayRuleGroup = new DisplayRuleGroup 
                    { 
                        rules = new[] { chainGunRule, launcherRule } 
                    } 
                });
                ArrayUtils.ArrayAppend(ref idrs.keyAssetRuleGroups, new ItemDisplayRuleSet.KeyAssetRuleGroup 
                { 
                    keyAsset = DroneWeaponsDisplay2.Result, 
                    displayRuleGroup = new DisplayRuleGroup 
                    { 
                        rules = new[] { robotArmRule, launcherRule } 
                    } 
                });
            }

            if (!LoadAddressable<ItemDisplayRuleSet>("RoR2/Base/RoboBallBoss/idrsRoboBallMini.asset", out var idrsRoboBallMini))
            {
                yield return idrsRoboBallMini;
            }
            AddNewDroneWeaponsDisplay(idrsRoboBallMini.Result, new ItemDisplayRule
            {
                childName = "Muzzle",
                followerPrefab = chainGunDisplay.Result,
                localPos = new Vector3(0F, -0.90161F, -0.92476F),
                localAngles = new Vector3(0F, 267.2474F, 0F),
                localScale = new Vector3(0.65679F, 0.65679F, 0.65679F)
            },
            new ItemDisplayRule
            {
                childName = "Muzzle",
                followerPrefab = robotArmDisplay.Result,
                localPos = new Vector3(-0.98133F, 0F, -0.88825F),
                localAngles = new Vector3(37.33081F, 296.2495F, 348.2924F),
                localScale = new Vector3(1.3F, 1.3F, 1.3F)
            },
            new ItemDisplayRule
            {
                childName = "Muzzle",
                followerPrefab = launcherDisplay.Result,
                localPos = new Vector3(0F, 0.84144F, -1.48515F),
                localAngles = new Vector3(292.1092F, 179.9797F, 359.2815F),
                localScale = new Vector3(0.54199F, 0.54199F, 0.54199F)
            });

            if (!LoadAddressable<ItemDisplayRuleSet>("RoR2/Base/Toolbot/idrsToolbot.asset", out var idrsToolbot))
            {
                yield return idrsToolbot;
            }
            AddNewDroneWeaponsDisplay(idrsToolbot.Result, new ItemDisplayRule
            {
                childName = "LowerArmL",
                followerPrefab = chainGunDisplay.Result,
                localPos = new Vector3(1.18103F, 1.93993F, -0.10851F),
                localAngles = new Vector3(356.0636F, 7.73336F, 74.58245F),
                localScale = new Vector3(2.32586F, 2.32586F, 2.32586F)
            },
            new ItemDisplayRule
            {
                childName = "Chest",
                followerPrefab = robotArmDisplay.Result,
                localPos = new Vector3(4.29751F, 2.2913F, 0.1107F),
                localAngles = new Vector3(29.75314F, 56.29673F, 4.77582F),
                localScale = new Vector3(-4.78734F, 4.78734F, 4.78734F)
            }, 
            new ItemDisplayRule
            {
                childName = "Chest",
                followerPrefab = launcherDisplay.Result,
                localPos = new Vector3(0.03985F, 2.20276F, -2.28506F),
                localAngles = new Vector3(0F, 180F, 0F),
                localScale = new Vector3(1.67958F, 1.67958F, 1.67958F)
            });

            if (!LoadAddressable<ItemDisplayRuleSet>("RoR2/Base/Treebot/idrsTreebot.asset", out var idrsTreebot))
            {
                yield return idrsTreebot;
            }
            AddNewDroneWeaponsDisplay(idrsTreebot.Result, new ItemDisplayRule
            {
                childName = "PlatformBase",
                followerPrefab = chainGunDisplay.Result,
                localPos = new Vector3(0F, -0.61243F, -0.01515F),
                localAngles = new Vector3(0.29367F, 269.1211F, 359.2129F),
                localScale = new Vector3(0.6037F, 0.6037F, 0.6037F)
            },
            new ItemDisplayRule
            {
                childName = "PlatformBase",
                followerPrefab = robotArmDisplay.Result,
                localPos = new Vector3(0.70485F, 0.60179F, -0.197F),
                localAngles = new Vector3(61.1553F, 105.4397F, 34.0543F),
                localScale = new Vector3(-1.52127F, 1.52127F, 1.52127F)
            },
            new ItemDisplayRule
            {
                childName = "PlatformBase",
                followerPrefab = launcherDisplay.Result,
                localPos = new Vector3(0F, 1.31884F, -0.85263F),
                localAngles = new Vector3(0F, 180F, 0F),
                localScale = new Vector3(0.65136F, 0.65136F, 0.65136F)
            });
        }

        public delegate void ModifyIdrCallback(ref ItemDisplayRule idr);
    }
}