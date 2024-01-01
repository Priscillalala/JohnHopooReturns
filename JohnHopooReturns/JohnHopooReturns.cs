using System;
using UnityEngine;
using BepInEx;
using System.Security;
using System.Security.Permissions;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using RoR2;
using HG;
using RoR2.ContentManagement;
using System.Collections;
using RoR2.ExpansionManagement;
using UnityEngine.Networking;
using System.Threading.Tasks;
using R2API;
using System.Linq;
using UnityEngine.ResourceManagement;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using R2API.Networking;

[module: UnverifiableCode]
#pragma warning disable
[assembly: SecurityPermission(System.Security.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore
[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace JohnHopooReturns
{
    [BepInPlugin("com.groovesalad.JohnHopooReturns", "JohnHopooReturns", "0.0.0")]
    [BepInDependency(LanguageAPI.PluginGUID)]
    [BepInDependency(PrefabAPI.PluginGUID)]
    [BepInDependency(NetworkingAPI.PluginGUID)]
    [BepInDependency(RecalculateStatsAPI.PluginGUID)]
    public class JohnHopooReturns : BaseUnityPlugin, IContentPackProvider
    {
        public ContentPack contentPack;
        public AssetBundleCreateRequest johnhopoogamesassets;

        public static JohnHopooReturns instance;
        public string identifier => "groovesalad.JohnHopooReturns";

        public void Awake()
        {
            instance = this;

            contentPack = new ContentPack
            {
                identifier = identifier,
            };

            johnhopoogamesassets = AssetBundle.LoadFromFileAsync(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location), "johnhopoogamesassets"));

            gameObject.AddComponent<SurvivorSpeed>();
            gameObject.AddComponent<PostProcessing>();
            gameObject.AddComponent<Beetles>();
            gameObject.AddComponent<MonsterStats>();
            gameObject.AddComponent<Scythe>();
            gameObject.AddComponent<LeechingSeed>();
            gameObject.AddComponent<WarHorn>();
            gameObject.AddComponent<Bungus>();
            gameObject.AddComponent<HappiestMask>();
            gameObject.AddComponent<Opal>();
            gameObject.AddComponent<RoseBuckler>();
            gameObject.AddComponent<Soulbound>();
            gameObject.AddComponent<Elixir>();
            gameObject.AddComponent<HideOSPBar>();
            gameObject.AddComponent<Polyp>();
            gameObject.AddComponent<RejuvRack>();
            gameObject.AddComponent<NoMeteors>();
            //gameObject.AddComponent<LaserScope>();
            gameObject.AddComponent<Tesla>();
            //gameObject.AddComponent<WakeOfVultures>();
            gameObject.AddComponent<StunGrenade>();
            gameObject.AddComponent<Aegis>();
            gameObject.AddComponent<SpareDroneParts>();
            gameObject.AddComponent<Watch>();

            ContentManager.collectContentPackProviders += add => add(this);
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!johnhopoogamesassets.isDone)
            {
                yield return johnhopoogamesassets;
            }
            IOnLoadStaticContentAsyncProvider[] providers = GetComponents<IOnLoadStaticContentAsyncProvider>();
            for (int i = 0; i < providers.Length; i++)
            {
                yield return providers[i].LoadStaticContentAsync(args);
                args.ReportProgress((float)i / providers.Length);
            }
            yield break;
        }

        public IEnumerator GenerateContentPackAsync(GetContentPackAsyncArgs args)
        {
            ContentPack.Copy(contentPack, args.output);
            yield break;
        }

        public IEnumerator FinalizeAsync(FinalizeAsyncArgs args)
        {
            johnhopoogamesassets.assetBundle?.Unload(false);
            yield break;
        }

        public interface IOnLoadStaticContentAsyncProvider
        {
            public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args);
        }

        public class Behaviour : MonoBehaviour
        {
            public static ContentPack Content => instance.contentPack;
            public static ConfigFile Config => instance.Config;
            public static ManualLogSource Logger => instance.Logger;
            public static AssetBundle Assets => instance.johnhopoogamesassets.assetBundle;

            public const string BEHAVIOUR_ENABLED = "Enabled";

            public static bool LoadAddressable<TObject>(object key, out AsyncOperationHandle<TObject> op)
            {
                op = Addressables.LoadAssetAsync<TObject>(key);
                return op.IsDone;
            }
        }

        public class Behaviour<TBehaviour> : Behaviour where TBehaviour : Behaviour<TBehaviour>
        {
            public static TBehaviour Instance { get; private set; }
            public static bool Exists => Instance != null;

            public Behaviour()
            {
                Instance = this as TBehaviour;
            }
        }
    }
}
