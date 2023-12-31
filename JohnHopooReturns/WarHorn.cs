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
using RoR2.ContentManagement;
using RoR2.Audio;

namespace JohnHopooReturns
{
    public class WarHorn : JohnHopooReturns.Behaviour, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "War Horn Affects Allies";

        public float baseRadius = Config.Value(SECTION, "Base Radius", 40f);
        public float stackRadius = Config.Value(SECTION, "Radius Per Stack", 4f);

        public static NetworkSoundEventDef nseWarhorn { get; private set; }
        public static GameObject WarhornEffect { get; private set; }

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            LanguageAPI.Add("ITEM_ENERGIZEDONEQUIPMENTUSE_PICKUP", "Activating your Equipment gives you and nearby allies a burst of attack speed.");
            LanguageAPI.Add("ITEM_ENERGIZEDONEQUIPMENTUSE_DESC", $"Activating your Equipment gives <style=cIsDamage>+70% attack speed</style> to you and all allies within <style=cIsDamage>{baseRadius}m</style> <style=cStack>(+{stackRadius}m per stack)</style> for <style=cIsDamage>8s</style> <style=cStack>(+4s per stack)</style>.");
            EquipmentSlot.onServerEquipmentActivated += EquipmentSlot_onServerEquipmentActivated;
        }

        private void EquipmentSlot_onServerEquipmentActivated(EquipmentSlot equipmentSlot, EquipmentIndex equipmentIndex)
        {
            if (equipmentSlot.characterBody && equipmentSlot.inventory)
            {
                int count = equipmentSlot.inventory.GetItemCount(RoR2Content.Items.EnergizedOnEquipmentUse);
                if (count > 0)
                {
                    float radius = Util.StackScaling(baseRadius, stackRadius, count);
                    float duration = Util.StackScaling(8f, 4f, count);
                    foreach (TeamComponent teamComponent in TeamComponent.GetTeamMembers(equipmentSlot.teamComponent.teamIndex))
                    {
                        if (teamComponent.body && teamComponent != equipmentSlot.teamComponent)
                        {
                            Vector3 vector = teamComponent.body.corePosition - equipmentSlot.characterBody.corePosition;
                            vector.y = 0f;
                            if (vector.sqrMagnitude <= radius * radius)
                            {
                                teamComponent.body.AddTimedBuff(RoR2Content.Buffs.Energized, duration);
                            }
                        }
                    }
                    if (!equipmentSlot.characterBody.HasBuff(RoR2Content.Buffs.Energized))
                    {
                        EntitySoundManager.EmitSoundServer(nseWarhorn.index, equipmentSlot.gameObject);
                        EffectData effectData = new EffectData
                        {
                            origin = equipmentSlot.characterBody.corePosition,
                            scale = radius,
                            rotation = Quaternion.identity
                        };
                        effectData.SetNetworkedObjectReference(equipmentSlot.gameObject);
                        EffectManager.SpawnEffect(WarhornEffect, effectData, true);
                    }
                }
            }
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            nseWarhorn = ScriptableObject.CreateInstance<NetworkSoundEventDef>();
            nseWarhorn.name = "nseWarhorn";
            nseWarhorn.eventName = "Play_item_proc_warhorn";
            Content.networkSoundEventDefs.Add(nseWarhorn);

            if (!LoadAddressable<GameObject>("RoR2/Base/IgniteOnKill/IgniteExplosionVFX.prefab", out var IgniteExplosionVFX))
            {
                yield return IgniteExplosionVFX;
            }
            WarhornEffect = PrefabAPI.InstantiateClone(IgniteExplosionVFX.Result, "WarhornEffect", false);
            WarhornEffect.transform.Find("OmniDirectionals")?.gameObject.SetActive(false);
            WarhornEffect.transform.Find("Flames")?.gameObject.SetActive(false);
            WarhornEffect.transform.Find("Point light")?.gameObject.SetActive(false);
            if (!LoadAddressable<Texture>("RoR2/Base/Common/ColorRamps/texRampTritoneSmoothed.png", out var texRampTritoneSmoothed))
            {
                yield return texRampTritoneSmoothed;
            }
            if (WarhornEffect.TryGetComponent(out ParticleSystemRenderer warhornEffectRenderer))
            {
                warhornEffectRenderer.sharedMaterial = new Material(warhornEffectRenderer.sharedMaterial);
                warhornEffectRenderer.sharedMaterial.SetColor("_TintColor", new Color32(205, 145, 35, 255));
                if (!LoadAddressable<Texture>("RoR2/DLC1/ancientloft/texAncientLoft_Cloud.tga", out var texAncientLoft_Cloud))
                {
                    yield return texAncientLoft_Cloud;
                }
                warhornEffectRenderer.sharedMaterial.SetTexture("_Cloud1Tex", texAncientLoft_Cloud.Result);
                warhornEffectRenderer.sharedMaterial.SetTexture("_RemapTex", texRampTritoneSmoothed.Result);
            }
            /*Light pointLight = WarhornEffect.GetComponentInChildren<Light>();
            if (pointLight)
            {
                pointLight.color = new Color32(255, 165, 0, 255);
                pointLight.intensity = 15f;
            }*/
            if (!LoadAddressable<GameObject>("RoR2/Base/TPHealingNova/TeleporterHealNovaPulse.prefab", out var TeleporterHealNovaPulse))
            {
                yield return TeleporterHealNovaPulse;
            }
            if (TeleporterHealNovaPulse.Result.transform.TryFind("PulseEffect/Donut", out Transform donut))
            {
                GameObject warhornPulse = Instantiate(donut.gameObject, WarhornEffect.transform);
                warhornPulse.transform.localScale = new Vector3(0.54f, 0.54f, 0.1f);
                ObjectScaleCurve pulseScaleCurve = warhornPulse.AddComponent<ObjectScaleCurve>();
                pulseScaleCurve.useOverallCurveOnly = true;
                pulseScaleCurve.overallCurve = AnimationCurve.EaseInOut(0f, 0f, 0.5f, 1f);
                pulseScaleCurve.timeMax = 1f;
                MeshRenderer pulseRenderer = warhornPulse.GetComponent<MeshRenderer>();
                Material matWarhornPulse = new Material(pulseRenderer.sharedMaterial);
                matWarhornPulse.SetColor("_TintColor", new Color32(255, 155, 0, 255));
                matWarhornPulse.SetFloat("_Boost", 1.3f);
                matWarhornPulse.SetFloat("_AlphaBoost", 1f);
                if (!LoadAddressable<Texture>("RoR2/Base/Common/VFX/texAlphaGradient2Mask.png", out var texAlphaGradient2Mask))
                {
                    yield return texAlphaGradient2Mask;
                }
                matWarhornPulse.SetTexture("_MainTex", texAlphaGradient2Mask.Result);
                //matWarhornPulse.SetTextureScale("_MainTex", new Vector2(12f, 1f));
                matWarhornPulse.SetTexture("_RemapTex", texRampTritoneSmoothed.Result);
                if (!LoadAddressable<Texture>("RoR2/Base/Common/texCloudIce.png", out var texCloudIce))
                {
                    yield return texCloudIce;
                }
                if (!LoadAddressable<Texture>("RoR2/Base/Common/texCloudWhitenoiseSubtle.png", out var texCloudWhitenoiseSubtle))
                {
                    yield return texCloudWhitenoiseSubtle;
                }
                matWarhornPulse.SetTexture("_Cloud1Tex", null);
                matWarhornPulse.SetTextureScale("_Cloud1Tex", new Vector2(6f, 1f));
                matWarhornPulse.SetTexture("_Cloud2Tex", texCloudWhitenoiseSubtle.Result);
                matWarhornPulse.SetTextureScale("_Cloud2Tex", new Vector2(1f, 1f));
                pulseRenderer.sharedMaterial = matWarhornPulse;

                ObjectScaleCurve pulse2 = Instantiate(warhornPulse.gameObject, WarhornEffect.transform).GetComponent<ObjectScaleCurve>();
                pulse2.overallCurve = AnimationCurve.EaseInOut(0.15f, 0f, 0.75f, 1f);
                ObjectScaleCurve pulse3 = Instantiate(warhornPulse.gameObject, WarhornEffect.transform).GetComponent<ObjectScaleCurve>();
                pulse3.overallCurve = AnimationCurve.EaseInOut(0.3f, 0f, 1f, 1f);
            }
            WarhornEffect.AddComponent<FollowReferencedTransform>();

            Content.effectDefs.Add(new EffectDef(WarhornEffect));

            if (!LoadAddressable<GameObject>("RoR2/Base/EnergizedOnEquipmentUse/EnergizedEffect.prefab", out var EnergizedEffect))
            {
                yield return EnergizedEffect;
            }
            if (EnergizedEffect.Result.TryGetComponent(out AkEvent akEvent))
            {
                DestroyImmediate(akEvent);
            }
        }


        [RequireComponent(typeof(EffectComponent))]
        public class FollowReferencedTransform : MonoBehaviour
        {
            private Transform referencedTransform;

            public void Start()
            {
                referencedTransform = GetComponent<EffectComponent>().GetReferencedChildTransform();
            }

            public void Update()
            {
                if (referencedTransform)
                {
                    transform.position = referencedTransform.position;
                }
            }
        }
    }
}