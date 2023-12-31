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
using RoR2.Items;
using RoR2.ContentManagement;
using RoR2.Projectile;
using R2API.Networking.Interfaces;
using R2API.Networking;

namespace JohnHopooReturns
{
    public class Watch : JohnHopooReturns.Behaviour<Watch>, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Delicate Watch Rework";

        public float baseRadius = Config.Value(SECTION, "Base Radius", 12f);
        public float stackRadius = Config.Value(SECTION, "Radius Per Stack", 6f);

        public static GameObject WatchSlowWard { get; private set; }

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            NetworkingAPI.RegisterMessageType<NetworkedSphereCollider.SyncRadiusMessage>();
            LanguageAPI.Add("ITEM_FRAGILEDAMAGEBONUS_NAME", "Wristwatch", "en");
            LanguageAPI.Add("ITEM_FRAGILEDAMAGEBONUS_PICKUP", "Slow nearby enemies and projectiles.");
            LanguageAPI.Add("ITEM_FRAGILEDAMAGEBONUS_DESC", $"<style=cIsUtility>Slow</style> enemies and incoming projectiles within <style=cIsUtility>{baseRadius}m</style> <style=cStack>(+{stackRadius}m per stack)</style> by <style=cIsUtility>50%</style>.");
            IL.RoR2.HealthComponent.TakeDamage += HealthComponent_TakeDamage;
            IL.RoR2.HealthComponent.ItemCounts.ctor += ItemCounts_ctor;
        }

        private void HealthComponent_TakeDamage(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.After,
                x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.FragileDamageBonus)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)))
                )
            {
                c.Emit(OpCodes.Pop);
                c.Emit(OpCodes.Ldc_I4_0);
            }
            else Logger.LogError($"{nameof(Watch)}.{nameof(HealthComponent_TakeDamage)} IL hook failed!");
        }

        private void ItemCounts_ctor(ILContext il)
        {
            ILCursor c = new ILCursor(il);
            if (c.TryGotoNext(MoveType.Before,
                x => x.MatchLdarg(0),
                x => x.MatchLdarg(1),
                x => x.MatchLdsfld(typeof(DLC1Content.Items), nameof(DLC1Content.Items.FragileDamageBonus)),
                x => x.MatchCallOrCallvirt<Inventory>(nameof(Inventory.GetItemCount)),
                x => x.MatchStfld<HealthComponent.ItemCounts>(nameof(HealthComponent.ItemCounts.fragileDamageBonus)))
                )
            {
                c.RemoveRange(5);
            }
            else Logger.LogError($"{nameof(Watch)}.{nameof(ItemCounts_ctor)} IL hook failed!");
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<ItemDef>("RoR2/DLC1/FragileDamageBonus/FragileDamageBonus.asset", out var FragileDamageBonus))
            {
                yield return FragileDamageBonus;
            }
            FragileDamageBonus.Result.tags = new[] { ItemTag.Utility };

            if (!LoadAddressable<GameObject>("RoR2/Base/NearbyDamageBonus/NearbyDamageBonusIndicator.prefab", out var NearbyDamageBonusIndicator))
            {
                yield return NearbyDamageBonusIndicator;
            }
            if (!LoadAddressable<BuffDef>("RoR2/Base/Common/bdSlow50.asset", out var bdSlow50))
            {
                yield return bdSlow50;
            }
            WatchSlowWard = PrefabAPI.InstantiateClone(NearbyDamageBonusIndicator.Result, "WatchSlowWard", true);
            WatchSlowWard.layer = LayerIndex.entityPrecise.intVal;
            TeamFilter teamFilter = WatchSlowWard.AddComponent<TeamFilter>();
            BuffWard buffWard = WatchSlowWard.AddComponent<BuffWard>();
            buffWard.shape = BuffWard.BuffWardShape.Sphere;
            buffWard.interval = 0.25f;
            buffWard.rangeIndicator = WatchSlowWard.transform.Find("Radius, Spherical");
            GameObject holder = new GameObject("Holder");
            holder.transform.SetParent(WatchSlowWard.transform);
            holder.transform.localScale = Vector3.one * 2f;
            buffWard.rangeIndicator.SetParent(holder.transform);
            if (buffWard.rangeIndicator.TryGetComponent(out MeshRenderer radiusRenderer))
            {
                radiusRenderer.sharedMaterial = new Material(radiusRenderer.sharedMaterial);
                radiusRenderer.sharedMaterial.SetColor("_TintColor", new Color32(181, 106, 44, 128));
                if (!LoadAddressable<Texture>("RoR2/Base/Common/texDottedLineMask.png", out var texDottedLineMask))
                {
                    yield return texDottedLineMask;
                }
                radiusRenderer.sharedMaterial.SetTexture("_MainTex", texDottedLineMask.Result);
                radiusRenderer.sharedMaterial.SetTextureScale("_MainTex", new Vector2(60f, 1f));
                radiusRenderer.sharedMaterial.SetFloat("_InvFade", 1f);
                radiusRenderer.sharedMaterial.SetFloat("_Boost", 0.5f);
            }
            buffWard.rangeIndicator.localScale = Vector3.zero;
            buffWard.buffDef = bdSlow50.Result;
            buffWard.buffDuration = 0.5f;
            buffWard.floorWard = false;
            buffWard.expires = false;
            buffWard.invertTeamFilter = true;
            buffWard.animateRadius = false;
            SphereCollider sphereCollider = WatchSlowWard.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            WatchSlowWard.AddComponent<NetworkedSphereCollider>();
            SlowDownProjectiles slowDownProjectiles = WatchSlowWard.AddComponent<SlowDownProjectiles>();
            slowDownProjectiles.teamFilter = teamFilter;
            slowDownProjectiles.slowDownCoefficient = 0.5f;
            Content.networkedObjectPrefabs.Add(WatchSlowWard);
        }

        public class WatchBehaviour : BaseItemBodyBehavior
        {
            [ItemDefAssociation(useOnServer = true, useOnClient = false)]
            public static ItemDef GetItemDef() => Exists ? DLC1Content.Items.FragileDamageBonus : null;

            private GameObject wardInstance;
            private BuffWard buffWard;
            private NetworkedSphereCollider collider;

            public void OnEnable()
            {
                wardInstance = Instantiate(WatchSlowWard, body.corePosition, Quaternion.identity);
                wardInstance.GetComponent<TeamFilter>().teamIndex = body.teamComponent.teamIndex;
                buffWard = wardInstance.GetComponent<BuffWard>();
                collider = wardInstance.GetComponent<NetworkedSphereCollider>();
                UpdateRadius();
                wardInstance.GetComponent<NetworkedBodyAttachment>().AttachToGameObjectAndSpawn(gameObject);
            }

            public void OnDisable()
            {
                if (wardInstance)
                {
                    Destroy(wardInstance);
                }
            }

            public void FixedUpdate()
            {
                UpdateRadius();
            }

            public void UpdateRadius()
            {
                float radius = Util.StackScaling(Instance.baseRadius, Instance.stackRadius, stack);
                if (buffWard)
                {
                    buffWard.Networkradius = radius;
                }
                if (collider)
                {
                    collider.NetworkRadius = radius;
                }
            }
        }

        [RequireComponent(typeof(SphereCollider))]
        [RequireComponent(typeof(NetworkIdentity))]
        public class NetworkedSphereCollider : MonoBehaviour
        {
            private SphereCollider sphereCollider;
            private NetworkIdentity networkIdentity;
            private float _radius;

            public float NetworkRadius
            {
                get => _radius;
                set
                {
                    if (_radius != value)
                    {
                        _radius = value;
                        new SyncRadiusMessage(value, networkIdentity.netId).Send(NetworkDestination.Clients);
                        sphereCollider.radius = value;
                    }
                }
            }

            public void Awake()
            {
                sphereCollider = GetComponent<SphereCollider>();
                networkIdentity = GetComponent<NetworkIdentity>();
            }

            public class SyncRadiusMessage : INetMessage
            {
                private float radius;
                private NetworkInstanceId networkInstanceId;

                public SyncRadiusMessage() { }

                public SyncRadiusMessage(float radius, NetworkInstanceId networkInstanceId)
                {
                    this.radius = radius;
                    this.networkInstanceId = networkInstanceId;
                }

                public void Serialize(NetworkWriter writer)
                {
                    writer.Write(radius);
                    writer.Write(networkInstanceId);
                }

                public void Deserialize(NetworkReader reader)
                {
                    radius = reader.ReadSingle();
                    networkInstanceId = reader.ReadNetworkId();
                }

                public void OnReceived()
                {
                    if (NetworkServer.active)
                    {
                        return;
                    }
                    GameObject gameObject = RoR2.Util.FindNetworkObject(networkInstanceId);
                    if (gameObject && gameObject.TryGetComponent(out SphereCollider sphereCollider))
                    {
                        sphereCollider.radius = radius;
                    }
                }
            }
        }
    }
}