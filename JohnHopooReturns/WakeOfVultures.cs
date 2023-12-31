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
using RoR2.Orbs;
using Mono.Cecil.Cil;
using MonoMod.Cil;

namespace JohnHopooReturns
{
    public class WakeOfVultures : JohnHopooReturns.Behaviour, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "New Wake of Vultures Visuals";

        public static GameObject BuffTransferOrbEffect { get; private set; }

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            IL.RoR2.GlobalEventManager.OnCharacterDeath += GlobalEventManager_OnCharacterDeath;
        }

        private void GlobalEventManager_OnCharacterDeath(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.HeadHunter)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)))
                && c.TryGotoNext(MoveType.Before,
                x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.AddTimedBuff)))
                )
            {
                c.Remove();
                c.Emit(OpCodes.Ldarg, 1);
                c.EmitDelegate<Action<CharacterBody, BuffIndex, float, DamageReport>>((attackerBody, buffIndex, buffDuration, damageReport) => 
                {
                    OrbManager.instance.AddOrb(new BuffTransferOrb
                    {
                        target = attackerBody.mainHurtBox,
                        buffIndex = buffIndex,
                        buffDuration = buffDuration,
                        origin = damageReport.victimBody.corePosition,
                    });
                });
            }
            else Logger.LogError($"{nameof(WakeOfVultures)}.{nameof(GlobalEventManager_OnCharacterDeath)} IL hook failed!");
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<GameObject>("RoR2/Base/Common/VFX/ItemTransferOrbEffect.prefab", out var ItemTransferOrbEffect))
            {
                yield return ItemTransferOrbEffect;
            }
            BuffTransferOrbEffect = PrefabAPI.InstantiateClone(ItemTransferOrbEffect.Result, "BuffTransferOrbEffect", false);
            BuffTransferOrbEffect.transform.Find("BillboardBase/DropShadow")?.gameObject.SetActive(false);
            BuffTransferOrbEffect.transform.Find("BillboardBase/Corners")?.gameObject.SetActive(false);
            if (!LoadAddressable<Material>("RoR2/Base/PassiveHealing/matWoodSpriteCloud.mat", out var matWoodSpriteCloud))
            {
                yield return matWoodSpriteCloud;
            }
            if (!LoadAddressable<Texture>("RoR2/Base/Common/ColorRamps/texRampTritone.png", out var texRampTritone))
            {
                yield return texRampTritone;
            }
            if (BuffTransferOrbEffect.transform.TryFind("BillboardBase/PickupSprite", out Transform pickupSprite) && pickupSprite.TryGetComponent(out SpriteRenderer pickupSpriteRenderer))
            {
                pickupSprite.localScale = Vector3.one * 0.5f;
                pickupSpriteRenderer.sharedMaterial = new Material(matWoodSpriteCloud.Result);
                pickupSpriteRenderer.sharedMaterial.SetTexture("_MainTex", null);
                pickupSpriteRenderer.sharedMaterial.SetTexture("_RemapTex", texRampTritone.Result);
                pickupSpriteRenderer.sharedMaterial.EnableKeyword("CLOUDOFFSET");
                pickupSpriteRenderer.sharedMaterial.EnableKeyword("VERTEXCOLOR");
                pickupSpriteRenderer.sharedMaterial.SetVector("_CutoffScroll", new Vector4(0f, 1f));
                pickupSpriteRenderer.sharedMaterial.SetFloat("_Boost", 8f);
                pickupSpriteRenderer.sharedMaterial.SetFloat("_AlphaBoost", 1f);
            }
            if (BuffTransferOrbEffect.transform.TryFind("Trail Parent/Trail", out Transform trail) && trail.TryGetComponent(out TrailRenderer trailRenderer))
            {
                trailRenderer.startWidth = 0.2f;
                trailRenderer.endWidth = 0f;
                trailRenderer.sharedMaterial = new Material(trailRenderer.sharedMaterial);
                if (!LoadAddressable<Texture>("RoR2/Base/Common/VFX/texBehemothTileMask.png", out var texBehemothTileMask))
                {
                    yield return texBehemothTileMask;
                }
                trailRenderer.sharedMaterial.SetTexture("_MainTex", texBehemothTileMask.Result);
            }
            if (BuffTransferOrbEffect.TryGetComponent(out OrbEffect orbEffect))
            {
                const float MULTIPLIER = 0.25f;
                orbEffect.startVelocity1 *= MULTIPLIER;
                orbEffect.startVelocity2 *= MULTIPLIER;
                orbEffect.endVelocity1 *= MULTIPLIER;
                orbEffect.endVelocity2 *= MULTIPLIER;
            }
            ItemTakenOrbEffect itemTakenOrbEffect = BuffTransferOrbEffect.GetComponent<ItemTakenOrbEffect>();
            BuffTakenOrbEffect buffTakenOrbEffect = BuffTransferOrbEffect.AddComponent<BuffTakenOrbEffect>();
            buffTakenOrbEffect.trailToColor = itemTakenOrbEffect.trailToColor;
            buffTakenOrbEffect.particlesToColor = itemTakenOrbEffect.particlesToColor;
            buffTakenOrbEffect.iconSpriteRenderer = itemTakenOrbEffect.iconSpriteRenderer;
            DestroyImmediate(itemTakenOrbEffect);
            Content.effectDefs.Add(new EffectDef(BuffTransferOrbEffect));
        }

        [RequireComponent(typeof(EffectComponent))]
        public class BuffTakenOrbEffect : MonoBehaviour
        {
            private void Start()
            {
                BuffDef buffDef = BuffCatalog.GetBuffDef((BuffIndex)RoR2.Util.UintToIntMinusOne(GetComponent<EffectComponent>().effectData.genericUInt));
                Color color = buffDef?.eliteDef?.color ?? default;
                Sprite sprite = buffDef?.iconSprite;
                trailToColor.startColor *= color;
                trailToColor.endColor *= color;
                for (int i = 0; i < particlesToColor.Length; i++)
                {
                    ParticleSystem particleSystem = particlesToColor[i];
                    ParticleSystem.MainModule main = particleSystem.main;
                    main.startColor = color;
                    particleSystem.Play();
                }
                iconSpriteRenderer.sprite = sprite;
                //iconSpriteRenderer.material.SetColor("_TintColor", color);
                iconSpriteRenderer.color = color;
            }

            public TrailRenderer trailToColor;
            public ParticleSystem[] particlesToColor;
            public SpriteRenderer iconSpriteRenderer;
        }

        public class BuffTransferOrb : Orb
        {
            public override void Begin()
            {
                duration = travelDuration;
                if (target == null)
                {
                    return;
                }
                EffectData effectData = new EffectData
                {
                    origin = origin,
                    genericFloat = duration,
                    genericUInt = RoR2.Util.IntToUintPlusOne((int)buffIndex)
                };
                effectData.SetHurtBoxReference(target);
                EffectManager.SpawnEffect(BuffTransferOrbEffect, effectData, true);
            }

            public override void OnArrival()
            {
                if (target && target.healthComponent && target.healthComponent.body)
                {
                    target.healthComponent.body.AddTimedBuff(buffIndex, buffDuration);
                }
            }

            public BuffIndex buffIndex;
            public float buffDuration;
            public float travelDuration = 1f;
        }
    }
}