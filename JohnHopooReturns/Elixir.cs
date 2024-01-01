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
using RoR2.Items;
using System.Linq;
using RoR2.ContentManagement;

namespace JohnHopooReturns
{
    public class Elixir : JohnHopooReturns.Behaviour<Elixir>, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Power Elixir Rework";

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true, "Power Elixir becomes Elixir and saves you from lethal damage once."))
            {
                Destroy(this);
                return;
            }
            LanguageAPI.Add("ITEM_HEALINGPOTION_NAME", "Elixir", "en");
            LanguageAPI.Add("ITEM_HEALINGPOTION_PICKUP", "Survive one lethal attack. Consumed on use.");
            LanguageAPI.Add("ITEM_HEALINGPOTION_DESC", "If you would recieve <style=cIsHealth>lethal damage</style>, <style=cIsUtility>consume</style> this item to <style=cIsHealing>survive</style> with <style=cIsHealing>1 hp</style>.");
            IL.RoR2.HealthComponent.ItemCounts.ctor += ItemCounts_ctor;
        }

        private void ItemCounts_ctor(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(1),
                x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.HealingPotion)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)),
                x => x.MatchStfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.healingPotion)))
                )
            {
                c.RemoveRange(5);
            }
            else Logger.LogError($"{nameof(Elixir)}.{nameof(ItemCounts_ctor)} IL hook failed!");
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<ItemDef>("RoR2/DLC1/HealingPotion/HealingPotion.asset", out var HealingPotion))
            {
                yield return HealingPotion;
            }
            HealingPotion.Result.tags = new[] { ItemTag.Healing };
        }

        public class ElixirBehaviour : BaseItemBodyBehavior, IOnTakeDamageServerReceiver
        {
            [ItemDefAssociation(useOnServer = true, useOnClient = false)]
            public static ItemDef GetItemDef() => Exists ? DLC1Content.Items.HealingPotion : null;

            public void OnEnable()
            {
                body.healthComponent.AddTakeDamageReceiver(this);
            }

            public void OnDisable()
            {
                body.healthComponent.RemoveTakeDamageReceiver(this);
            }

            public void OnTakeDamageServer(DamageReport damageReport)
            {
                if (stack > 0 && !body.healthComponent.alive)
                {
                    body.healthComponent.Networkhealth = 1f;
                    RoR2.Util.CleanseBody(body, true, false, false, true, false, true);
                    body.inventory.RemoveItem(DLC1Content.Items.HealingPotion);
                    body.inventory.GiveItem(DLC1Content.Items.HealingPotionConsumed);
                    CharacterMasterNotificationQueue.SendTransformNotification(body.master, DLC1Content.Items.HealingPotion.itemIndex, DLC1Content.Items.HealingPotionConsumed.itemIndex, CharacterMasterNotificationQueue.TransformationType.Default);
                    EffectData effectData = new EffectData
                    {
                        origin = body.corePosition
                    };
                    effectData.SetNetworkedObjectReference(gameObject);
                    EffectManager.SpawnEffect(LegacyResourcesAPI.Load<GameObject>("Prefabs/Effects/HealingPotionEffect"), effectData, true);
                }
            }
        }
    }
}