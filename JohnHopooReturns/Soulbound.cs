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
using BepInEx.Bootstrap;
using RoR2.UI;
using System.Linq;
using UnityEngine.UI;
using RoR2.ContentManagement;

namespace JohnHopooReturns
{
    public class Soulbound : JohnHopooReturns.Behaviour<Soulbound>, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Soulbound Catalyst Rework";

        public float baseConversionRate = Config.Value(SECTION, "Base Conversion Rate", 1f);
        public float stackConversionRate = Config.Value(SECTION, "Conversion Rate Per Stack", 0.2f);
        public string sound = Config.Value(SECTION, "Proc Sound", "Play_item_proc_ghostOnKill");
        public Color flashColor = Config.Value<Color>(SECTION, "Equipment Icon Flash Color", new Color32(170, 255, 235, 255));//new Color32(44, 225, 179, 255));

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            LanguageAPI.Add("ITEM_TALISMAN_PICKUP", "Losing health reduces equipment cooldown.");
            LanguageAPI.Add("ITEM_TALISMAN_DESC", $"<style=cIsUtility>Reduce equipment cooldown</style> on taking damage by <style=cIsUtility>{baseConversionRate:0%}</style> <style=cStack>(+{stackConversionRate:0%} per stack)</style> of the <style=cIsHealth>maximum health percentage you lost</style>.");
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
        }

        private void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.Talisman)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)))
                )
            {
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldc_I4_0);
            }
            else Logger.LogError($"{nameof(Soulbound)}.{nameof(GlobalEventManager_OnCharacterDeath)} IL hook failed!");
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<ItemDef>("RoR2/Base/Talisman/Talisman.asset", out var Talisman))
            {
                yield return Talisman;
            }
            Talisman.Result.tags = new[] { ItemTag.Utility, ItemTag.EquipmentRelated };

            if (!LoadAddressable<EquipmentDef>("RoR2/Base/DeathProjectile/DeathProjectile.asset", out var DeathProjectile))
            {
                yield return DeathProjectile;
            }
            DeathProjectile.Result.cooldown = 30f;
        }

        public class CatalystBehaviour : BaseItemBodyBehavior, IOnIncomingDamageServerReceiver, IOnTakeDamageServerReceiver
        {
            [ItemDefAssociation(useOnServer = true, useOnClient = true)]
            public static ItemDef GetItemDef() => Exists ? RoR2Content.Items.Talisman : null;

            private float _health;
            private float _cooldownTimer;
            //private Soulbound soulbound;

            public void Start()
            {
                _cooldownTimer = body.equipmentSlot.cooldownTimer;
            }

            public void OnEnable()
            {
                //soulbound = Chainloader.ManagerObject.GetComponent<Soulbound>();
                body.healthComponent.AddIncomingDamageReceiver(this);
                body.healthComponent.AddTakeDamageReceiver(this);
            }

            public void OnDisable()
            {
                body.healthComponent.RemoveIncomingDamageReceiver(this);
                body.healthComponent.RemoveTakeDamageReceiver(this);
            }

            public void FixedUpdate()
            {
                if (body.equipmentSlot)
                {
                    if (_cooldownTimer - body.equipmentSlot.cooldownTimer >= 0.5f)
                    {
                        RoR2.Util.PlaySound(Instance.sound, gameObject);
                        if (body.equipmentSlot.stock <= 0)
                        {
                            HUD hud = HUD.readOnlyInstanceList.FirstOrDefault(x => x.targetBodyObject == gameObject);
                            foreach (EquipmentIcon equipmentIcon in hud.equipmentIcons)
                            {
                                if (!equipmentIcon.displayAlternateEquipment && equipmentIcon.reminderFlashPanelObject && !equipmentIcon.currentDisplayData.isReady)
                                {
                                    GameObject flashInstance = Instantiate(equipmentIcon.reminderFlashPanelObject, equipmentIcon.reminderFlashPanelObject.transform.parent);
                                    if (flashInstance.TryGetComponent(out Image image))
                                    {
                                        image.color = Instance.flashColor;
                                    }
                                    if (flashInstance.TryGetComponent(out AnimateUIAlpha animateUIAlpha))
                                    {
                                        animateUIAlpha.time = 0;
                                        animateUIAlpha.destroyOnEnd = true;
                                    }
                                    flashInstance.SetActive(true);
                                }
                            }
                        }
                    }
                    _cooldownTimer = body.equipmentSlot.cooldownTimer;
                }
            }

            public void OnIncomingDamageServer(DamageInfo damageInfo)
            {
                _health = body.healthComponent.health;
            }

            public void OnTakeDamageServer(DamageReport damageReport)
            {
                float healthLost = _health - body.healthComponent.health;
                if (healthLost > 0f && body.inventory && body.inventory.currentEquipmentState.equipmentDef)
                {
                    float cooldown = body.inventory.currentEquipmentState.equipmentDef.cooldown * body.inventory.CalculateEquipmentCooldownScale();
                    float conversionRate = Util.StackScaling(Instance.baseConversionRate, Instance.stackConversionRate, stack);
                    body.inventory.DeductActiveEquipmentCooldown(conversionRate * cooldown * healthLost / body.healthComponent.fullHealth);
                }
            }
        }
    }
}