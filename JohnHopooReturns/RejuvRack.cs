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
using RoR2.Audio;
using RoR2.ContentManagement;
using RoR2.Projectile;
using R2API.Networking.Interfaces;
using R2API.Networking;

namespace JohnHopooReturns
{
    public class RejuvRack : JohnHopooReturns.Behaviour<RejuvRack>, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Rejuvenation Rack Rework";

        public float spiritDuration = 10f;
        public float baseSpiritRegen = 1f;

        public static GameObject SpiritPrefab { get; private set; }
        public static GameObject SummonSpiritsEffect { get; private set; }

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            NetworkingAPI.RegisterMessageType<SummonSpiritsBehaviour.SummonSpiritMessage>();
            LanguageAPI.Add("ITEM_INCREASEHEALING_PICKUP", "Invite friendly spirits when healed.");
            LanguageAPI.Add("ITEM_INCREASEHEALING_DESC", $"Store <style=cIsHealing>100%</style> <style=cStack>(+100% per stack)</style> of healing as <style=cIsHealing>Soul Energy</style>. After your <style=cIsHealing>Soul Energy</style> reaches 10% of your maximum health, <style=cIsHealing>invite a spirit</style> to increase <style=cIsHealing>base health regeneration</style> by <style=cIsHealing>+{baseSpiritRegen} hp/s</style> over <style=cIsHealing>{spiritDuration}s</style>.");
            IL.RoR2.HealthComponent.ItemCounts.ctor += ItemCounts_ctor;
            IL.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
        }

        private void ItemCounts_ctor(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(1),
                x => x.MatchLdsfld(typeof(RoR2Content.Items), nameof(RoR2Content.Items.IncreaseHealing)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)),
                x => x.MatchStfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.increaseHealing)))
                )
            {
                c.RemoveRange(5);
            }
            else Logger.LogError($"{nameof(RejuvRack)}.{nameof(ItemCounts_ctor)} IL hook failed!");
        }

        private void CharacterBody_RecalculateStats(ILContext il)
        {
            int locLevelMultiplierIndex = -1;
            int locMeatRegenBoostIndex = -1;
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(JunkContent.Buffs).GetField(nameof(JunkContent.Buffs.MeatRegenBoost))),
                x => x.MatchCallOrCallvirt<CharacterBody>(nameof(CharacterBody.HasBuff)),
                x => x.MatchBrtrue(out _),
                x => x.MatchLdcR4(out _),
                x => x.MatchBr(out _),
                x => x.MatchLdcR4(out _),
                x => x.MatchLdloc(out locLevelMultiplierIndex),
                x => x.MatchMul(),
                x => x.MatchStloc(out locMeatRegenBoostIndex)
                ) && c.TryGotoNext(MoveType.After,
                x => x.MatchLdloc(locMeatRegenBoostIndex),
                x => x.MatchAdd()
                ))
            {
                c.Emit(OpCodes.Ldarg, 0);
                c.Emit(OpCodes.Ldloc, locLevelMultiplierIndex);
                c.EmitDelegate<Func<CharacterBody, float, float>>((body, levelMultiplier) => 
                {
                    if (body.inventory && body.inventory.GetItemCount(RoR2Content.Items.IncreaseHealing) > 0 && body.TryGetComponent(out SummonSpiritsBehaviour summonSpiritsBehaviour))
                    {
                        return baseSpiritRegen * levelMultiplier * summonSpiritsBehaviour.activeSpirits.Count;
                    }
                    return 0f;
                });
                c.Emit(OpCodes.Add);
            }
            else Logger.LogError($"{nameof(RejuvRack)}.{nameof(CharacterBody_RecalculateStats)} IL hook failed!");
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<GameObject>("RoR2/DLC1/LunarSun/LunarSunProjectileGhost.prefab", out var LunarSunProjectileGhost))
            {
                yield return LunarSunProjectileGhost;
            }
            SpiritPrefab = PrefabAPI.InstantiateClone(LunarSunProjectileGhost.Result, "HealingSpirit", false);
            if (SpiritPrefab.TryGetComponent(out ProjectileGhostController projectileGhostController))
            {
                DestroyImmediate(projectileGhostController);
            }
            if (SpiritPrefab.TryGetComponent(out VFXAttributes vFXAttributes))
            {
                DestroyImmediate(vFXAttributes);
            }
            SummonSpiritsBehaviour.SpiritController spiritController = SpiritPrefab.AddComponent<SummonSpiritsBehaviour.SpiritController>();
            if (!LoadAddressable<Texture>("RoR2/Base/Common/ColorRamps/texRampAntler.png", out var texRampAntler))
            {
                yield return texRampAntler;
            }
            if (!LoadAddressable<Texture>("RoR2/Base/Common/texCloudSkulls.png", out var texCloudSkulls))
            {
                yield return texCloudSkulls;
            }
            if (!LoadAddressable<Material>("RoR2/Base/ExplodeOnDeath/matWilloWispCore.mat", out var matWilloWispCore))
            {
                yield return matWilloWispCore;
            }
            if (!LoadAddressable<Texture>("RoR2/Base/Common/ColorRamps/texRampHealing.png", out var texRampHealing))
            {
                yield return texRampHealing;
            }
            if (SpiritPrefab.transform.TryFind("mdlSunProjectile/Backdrop", out Transform backdrop) && backdrop.TryGetComponent(out ParticleSystemRenderer backdropRenderer))
            {
                backdropRenderer.sharedMaterial = new Material(backdropRenderer.sharedMaterial);
                backdropRenderer.sharedMaterial.SetTexture("_RemapTex", texRampAntler.Result);
                backdropRenderer.sharedMaterial.SetFloat("_AlphaBoost", 1.3f);
                backdropRenderer.sharedMaterial.SetFloat("_AlphaBias", 0.075f);
                backdropRenderer.sharedMaterial.SetTexture("_Cloud1Tex", texCloudSkulls.Result);
                backdrop.localScale = Vector3.one * 2f;
            }
            if (SpiritPrefab.transform.TryFind("mdlSunProjectile/Quad", out Transform billboard) && billboard.TryGetComponent(out MeshRenderer billboardRenderer))
            {
                billboardRenderer.sharedMaterial = new Material(matWilloWispCore.Result);
                billboardRenderer.sharedMaterial.SetTexture("_RemapTex", texRampHealing.Result);
                billboardRenderer.sharedMaterial.SetFloat("_Boost", 3f);
                if (billboard.TryGetComponent(out SetRandomScale setRandomScale))
                {
                    setRandomScale.minimumScale = 0.5f;
                    setRandomScale.maximumScale = 1f;
                }
            }
            if (SpiritPrefab.transform.TryFind("Particles/CloseParticles", out Transform closeParticles) && closeParticles.TryGetComponent(out ParticleSystem closeParticlesSystem))
            {
                ParticleSystem.MainModule main = closeParticlesSystem.main;
                main.startColor = new ParticleSystem.MinMaxGradient(Color.yellow, Color.green);
                spiritController.particles = closeParticlesSystem;
                //SpiritPrefab.AddComponent<DetachParticleOnDestroyAndEndEmission>().particleSystem = closeParticlesSystem;
                /*if (closeParticles.TryGetComponent(out ParticleSystemRenderer closeParticlesRenderer))
                {
                    closeParticlesRenderer.sharedMaterial = new Material(closeParticlesRenderer.sharedMaterial);
                    closeParticlesRenderer.sharedMaterial
                }*/
            }
            if (SpiritPrefab.transform.TryFind("Particles/OnSpawnPopParticle", out Transform onSpawnPopParticle) && onSpawnPopParticle.TryGetComponent(out ParticleSystem onSpawnPopParticleSystem))
            {
                ParticleSystem.MainModule main = onSpawnPopParticleSystem.main;
                main.startColor = new ParticleSystem.MinMaxGradient(Color.yellow, Color.green);
            }
            if (SpiritPrefab.transform.TryFind("Particles/Trail", out Transform trail) && trail.TryGetComponent(out TrailRenderer trailRenderer))
            {
                trailRenderer.sharedMaterial = new Material(trailRenderer.sharedMaterial);
                trailRenderer.sharedMaterial.SetTexture("_RemapTex", texRampAntler.Result);
                trailRenderer.time = 0.3f;
                spiritController.trail = trailRenderer;
            }

            ObjectScaleCurve objectScaleCurve = SpiritPrefab.AddComponent<ObjectScaleCurve>();
            objectScaleCurve.useOverallCurveOnly = true;
            objectScaleCurve.overallCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            objectScaleCurve.timeMax = 0.2f;
            objectScaleCurve.enabled = false;
            //objectScaleCurve.baseScale = Vector3.one;
            DestroyOnTimer destroyOnTimer = SpiritPrefab.AddComponent<DestroyOnTimer>();
            destroyOnTimer.duration = 0.5f;
            destroyOnTimer.enabled = false;
            SpiritPrefab.AddComponent<DitherModelFast>().renderers = SpiritPrefab.GetComponentsInChildren<Renderer>(false);

            if (!LoadAddressable<GameObject>("RoR2/Base/ShockNearby/TeslaFieldBuffEffect.prefab", out var TeslaFieldBuffEffect))
            {
                yield return TeslaFieldBuffEffect;
            }
            SummonSpiritsEffect = PrefabAPI.InstantiateClone(TeslaFieldBuffEffect.Result, "SummonSpiritsEffect", false);
            SummonSpiritsEffect.transform.Find("VisualTransform/Rings")?.gameObject.SetActive(false);
            if (SummonSpiritsEffect.transform.TryFind("VisualTransform/Random Particles", out Transform randomParticles) && randomParticles.TryGetComponent(out ParticleSystem randomParticlesSystem))
            {
                ParticleSystem.MainModule main = randomParticlesSystem.main;
                main.startColor = new ParticleSystem.MinMaxGradient(Color.yellow, Color.green);
            }
            if (SummonSpiritsEffect.transform.TryFind("VisualTransform/SoftGlow", out Transform softGlow) && softGlow.TryGetComponent(out ParticleSystem softGlowSystem))
            {
                ParticleSystem.MainModule main = softGlowSystem.main;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color32(110, 255, 0, 64), new Color32(0, 255, 0, 60));
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
                if (softGlow.TryGetComponent(out ParticleSystemRenderer softGlowRenderer))
                {
                    softGlowRenderer.maxParticleSize = 1f;
                }
            }
            Light light = SummonSpiritsEffect.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color32(216, 255, 0, 255);
            light.intensity = 1f;
            light.shadows = LightShadows.None;
            if (!LoadAddressable<GameObject>("RoR2/DLC1/MushroomVoid/MushroomVoidEffect.prefab", out var MushroomVoidEffect))
            {
                yield return MushroomVoidEffect;
            }
            if (MushroomVoidEffect.Result.transform.TryFind("Visual/Crosses", out Transform crosses))
            {
                GameObject crossesInstance = Instantiate(crosses.gameObject, SummonSpiritsEffect.transform.Find("VisualTransform"));
                SummonSpiritsEffect.AddComponent<DetachParticleOnDestroyAndEndEmission>().particleSystem = crossesInstance.GetComponent<ParticleSystem>();
            }
        }

        public float offsetSmoothTime = 1f;
        public float offsetMaxSpeed = 5f;
        public float smoothTime = 0.2f;
        public float maxSpeed = 200f;
        public float offsetRadius = 2f;
        public float maxVfxRadiusCoefficient = 2f;
        public float lightRadiusMultiplier = 5f;
        public string spawnSoundEffect = "Play_item_proc_regenOnKill";

        public class SummonSpiritsBehaviour : BaseItemBodyBehavior
        {
            [ItemDefAssociation(useOnServer = true, useOnClient = true)]
            public static ItemDef GetItemDef() => Exists ? RoR2Content.Items.IncreaseHealing : null;

            [SerializeField]
            private Transform headTransform;
            private NetworkIdentity networkIdentity;
            [SerializeField]
            private float healingPoolServer;
            [SerializeField]
            private float summonSpiritTimerServer;
            public Queue<SpiritController> activeSpirits = new Queue<SpiritController>();
            private TemporaryVisualEffect vfxInstance;
            //private float _activeSpiritCount;

            /*public float activeSpiritCount
            {
                get => _activeSpiritCount;
                set
                {
                    if (_activeSpiritCount != value)
                    {
                        _activeSpiritCount = value;
                        body?.MarkAllStatsDirty();
                    }
                }
            }*/

            public float vfxRadius => Mathf.Min(activeSpirits.Count * 0.1f, 1f) * Instance.maxVfxRadiusCoefficient * body.bestFitRadius;

            public void OnEnable()
            {
                ChildLocator childLocator = body?.modelLocator?.modelTransform?.GetComponent<ChildLocator>();
                if (childLocator)
                {
                    headTransform = childLocator.FindChild("Head");
                }
                networkIdentity = GetComponent<NetworkIdentity>();
                HealthComponent.onCharacterHealServer += HealthComponent_onCharacterHealServer;
            }

            public void OnDisable()
            {
                HealthComponent.onCharacterHealServer -= HealthComponent_onCharacterHealServer;
            }

            private void HealthComponent_onCharacterHealServer(HealthComponent healthComponent, float amount, ProcChainMask procChainMask)
            {
                if (healthComponent && healthComponent == body.healthComponent)
                {
                    healingPoolServer = Mathf.Min(healingPoolServer + amount * stack, body.healthComponent.fullCombinedHealth);
                }   
            }

            public void FixedUpdate()
            {
                if (NetworkServer.active)
                {
                    FixedUpdateServer();
                }
                if (activeSpirits.Count > 0)
                {
                    SpiritController spirit = activeSpirits.Peek();
                    if (spirit == null || spirit.lifespan <= 0f)
                    {
                        RemoveNextSpirit();
                    }
                }
            }

            public void FixedUpdateServer()
            {
                if (healingPoolServer <= 0f)
                {
                    return;
                }
                summonSpiritTimerServer -= Time.fixedDeltaTime;
                if (summonSpiritTimerServer <= 0f)
                {
                    summonSpiritTimerServer += 0.1f;
                    healingPoolServer -= body.healthComponent.fullCombinedHealth / 10f;
                    SummonSpiritServer();
                }
            }

            public void OnDestroy()
            {
                while (activeSpirits.Count > 0)
                {
                    RemoveNextSpirit();
                }
            }

            public void SummonSpiritServer()
            {
                Vector3 offset = UnityEngine.Random.onUnitSphere * 20f;
                offset.y = Mathf.Abs(offset.y);
                InstantiateSpirit(offset);
                if (networkIdentity)
                {
                    new SummonSpiritMessage(offset, networkIdentity.netId).Send(NetworkDestination.Clients);
                }
            }

            public void InstantiateSpirit(Vector3 offset)
            {
                Transform targetTransform = headTransform ?? transform;
                SpiritController spiritInstance = Instantiate(SpiritPrefab, targetTransform.position + offset, Quaternion.identity).GetComponent<SpiritController>();
                spiritInstance.lifespan = Instance.spiritDuration;
                spiritInstance.targetTransform = targetTransform;
                spiritInstance.currentOffset = offset;
                activeSpirits.Enqueue(spiritInstance);
                OnSpiritsUpdated();
            }

            public void RemoveNextSpirit()
            {
                SpiritController spirit = activeSpirits.Dequeue();
                if (spirit)
                {
                    spirit.BeginDestroy();
                }
                OnSpiritsUpdated();
            }

            public void OnSpiritsUpdated()
            {
                body.MarkAllStatsDirty();
                if (activeSpirits.Count > 0)
                {
                    if (vfxInstance == null)
                    {
                        vfxInstance = Instantiate(SummonSpiritsEffect, body.corePosition, Quaternion.identity).GetComponent<TemporaryVisualEffect>();
                        vfxInstance.parentTransform = body.coreTransform;
                        vfxInstance.visualState = TemporaryVisualEffect.VisualState.Enter;
                        vfxInstance.healthComponent = body.healthComponent;
                    }
                    vfxInstance.radius = vfxRadius;
                    if (vfxInstance.TryGetComponent(out Light light))
                    {
                        light.range = vfxInstance.radius * Instance.lightRadiusMultiplier;
                    }
                }
                else if (vfxInstance)
                {
                    vfxInstance.visualState = TemporaryVisualEffect.VisualState.Exit;
                }
            }

            public class SummonSpiritMessage : INetMessage
            {
                private Vector3 offset;
                private NetworkInstanceId networkInstanceId;

                public SummonSpiritMessage() { }

                public SummonSpiritMessage(Vector3 offset, NetworkInstanceId networkInstanceId)
                {
                    this.offset = offset;
                    this.networkInstanceId = networkInstanceId;
                }

                public void Serialize(NetworkWriter writer)
                {
                    writer.Write(offset);
                    writer.Write(networkInstanceId);
                }

                public void Deserialize(NetworkReader reader)
                {
                    offset = reader.ReadVector3();
                    networkInstanceId = reader.ReadNetworkId();
                }

                public void OnReceived()
                {
                    if (NetworkServer.active)
                    {
                        return;
                    }
                    GameObject gameObject = RoR2.Util.FindNetworkObject(networkInstanceId);
                    if (gameObject && gameObject.TryGetComponent(out SummonSpiritsBehaviour summonSpiritsBehaviour))
                    {
                        summonSpiritsBehaviour.InstantiateSpirit(offset);
                    }
                }
            }

            public class SpiritController : MonoBehaviour
            {
                public ParticleSystem particles;
                public TrailRenderer trail;
                public float lifespan;
                public Transform targetTransform;
                public float updateOffsetTimer;
                public Vector3 currentOffset;
                public Vector3 targetOffset;
                private Vector3 _currentOffsetVelocity;
                private Vector3 _currentVelocity;

                public void Start()
                {
                    RoR2.Util.PlaySound(Instance.spawnSoundEffect, gameObject);
                }

                public void FixedUpdate()
                {
                    lifespan -= Time.fixedDeltaTime;
                }

                public void Update()
                {
                    if (!targetTransform)
                    {
                        return;
                    }
                    updateOffsetTimer -= Time.deltaTime;
                    if (updateOffsetTimer <= 0f)
                    {
                        Vector3 a = UnityEngine.Random.insideUnitSphere;
                        Vector3 b = UnityEngine.Random.insideUnitSphere;
                        Vector3 normalizedOffset = a.sqrMagnitude < b.sqrMagnitude ? a : b;
                        targetOffset = Instance.offsetRadius * normalizedOffset;
                        updateOffsetTimer += Mathf.Lerp(4f, 1f, normalizedOffset.sqrMagnitude);
                    }
                    currentOffset = Vector3.SmoothDamp(currentOffset, targetOffset, ref _currentOffsetVelocity, Instance.offsetSmoothTime, Instance.offsetMaxSpeed, Time.deltaTime);
                    Vector3 currentPosition = transform.position - currentOffset;
                    currentPosition = Vector3.SmoothDamp(currentPosition, targetTransform.position, ref _currentVelocity, Instance.smoothTime, Instance.maxSpeed, Time.deltaTime);
                    transform.position = currentPosition + currentOffset;
                }

                public void BeginDestroy()
                {
                    GetComponent<ObjectScaleCurve>().enabled = true;
                    GetComponent<DestroyOnTimer>().enabled = true;
                    if (particles)
                    {
                        ParticleSystem.EmissionModule emission = particles.emission;
                        emission.enabled = false;
                        particles.Stop();
                    }
                    if (trail)
                    {
                        trail.emitting = false;
                    }
                }
            }
        }

        public class DitherModelFast : MonoBehaviour
        {
            public void OnEnable()
            {
                if (instancesList == null)
                {
                    instancesList = new List<DitherModelFast>();
                    SceneCamera.onSceneCameraPreRender += OnSceneCameraPreRender;
                }
                instancesList.Add(this);
            }

            public void OnDisable()
            {
                if (instancesList.Remove(this) && instancesList.Count <= 0)
                {
                    instancesList = null;
                    SceneCamera.onSceneCameraPreRender -= OnSceneCameraPreRender;
                }
            }

            private static void OnSceneCameraPreRender(SceneCamera sceneCamera)
            {
                CameraRigController cameraRigController = sceneCamera.cameraRigController;
                if (!cameraRigController)
                {
                    return;
                }
                Vector3 position = cameraRigController.transform.position;
                for (int i = 0; i < instancesList.Count; i++)
                {
                    DitherModelFast instance = instancesList[i];
                    if (cameraRigController.enableFading)
                    {
                        instance.fade = Mathf.Clamp01(RoR2.Util.Remap(Vector3.Distance(instance.transform.position, position), cameraRigController.fadeStartDistance, cameraRigController.fadeEndDistance, 0f, 1f));
                    }
                    else
                    {
                        instance.fade = 1f;
                    }
                    instance.UpdateDither();
                }
            }

            private void UpdateDither()
            {
                for (int i = renderers.Length - 1; i >= 0; i--)
                {
                    Renderer renderer = renderers[i];
                    renderer.GetPropertyBlock(propertyStorage);
                    propertyStorage.SetFloat("_Fade", fade);
                    renderer.SetPropertyBlock(propertyStorage);
                }
            }

            public float fade;
            public Renderer[] renderers;
            private MaterialPropertyBlock propertyStorage = new MaterialPropertyBlock();
            private static List<DitherModelFast> instancesList;
        }
    }
}