using System;
using UnityEngine;
using BepInEx;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using RoR2;
using HG;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx.Configuration;
using RoR2.ContentManagement;
using HG.GeneralSerializer;

namespace JohnHopooReturns
{
    public static class Util
    {
        public static bool TryFind(this Transform transform, string n, out Transform child)
        {
            return child = transform.Find(n);
        }

        public static IEnumerable<Transform> AllChildren(this Transform transform)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                yield return transform.GetChild(i);
            }
        }

        public static T Value<T>(this ConfigFile config, string section, string key, T defaultValue, string description)
        {
            return config.Bind(section, key, defaultValue, description).Value;
        }

        public static T Value<T>(this ConfigFile config, string section, string key, T defaultValue, ConfigDescription configDescription = null)
        {
            return config.Bind(section, key, defaultValue, configDescription).Value;
        }

        public static T Value<T>(this ConfigFile config, ConfigDefinition configDefinition, T defaultValue, ConfigDescription configDescription = null)
        {
            return config.Bind(configDefinition, defaultValue, configDescription).Value;
        }

        public static void Add<TAsset>(this NamedAssetCollection<TAsset> assetCollection, TAsset newAsset)
        {
            string assetName = assetCollection.nameProvider(newAsset);
            if (assetCollection.assetToName.ContainsKey(newAsset))
            {
                throw new ArgumentException(string.Format("Asset {0} is already registered!", newAsset));
            }
            if (assetCollection.nameToAsset.ContainsKey(assetName))
            {
                throw new ArgumentException("Asset name " + assetName + " is already registered!");
            }
            NamedAssetCollection<TAsset>.AssetInfo assetInfo = new NamedAssetCollection<TAsset>.AssetInfo
            {
                asset = newAsset,
                assetName = assetName,
            };
            int index = Array.BinarySearch(assetCollection.assetInfos, assetInfo);
            ArrayUtils.ArrayInsert(ref assetCollection.assetInfos, ~index, assetInfo);
            assetCollection.nameToAsset[assetName] = newAsset;
            assetCollection.assetToName[newAsset] = assetName;
        }

        public static bool TryModifyFieldValue<T>(this EntityStateConfiguration entityStateConfiguration, string fieldName, T value)
        {
            ref SerializedField serializedField = ref entityStateConfiguration.serializedFieldsCollection.GetOrCreateField(fieldName);
            Type type = typeof(T);
            if (serializedField.fieldValue.objectValue && typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                serializedField.fieldValue.objectValue = value as UnityEngine.Object;
                return true;
            }
            else if (serializedField.fieldValue.stringValue != null && StringSerializer.CanSerializeType(type))
            {
                serializedField.fieldValue.stringValue = StringSerializer.Serialize(type, value);
                return true;
            }
            return false;
        }

        public static float StackScaling(float baseValue, float stackValue, int stack)
        {
            if (stack > 0)
            {
                return baseValue + ((stack - 1) * stackValue);
            }
            return 0f;
        }

        public static int StackScaling(int baseValue, int stackValue, int stack)
        {
            if (stack > 0)
            {
                return baseValue + ((stack - 1) * stackValue);
            }
            return 0;
        }

        public static void AddIncomingDamageReceiver(this HealthComponent healthComponent, IOnIncomingDamageServerReceiver onIncomingDamageReceiver)
        {
            if (healthComponent && Array.IndexOf(healthComponent.onIncomingDamageReceivers, onIncomingDamageReceiver) < 0)
            {
                ArrayUtils.ArrayAppend(ref healthComponent.onIncomingDamageReceivers, onIncomingDamageReceiver);
            }
        }

        public static void RemoveIncomingDamageReceiver(this HealthComponent healthComponent, IOnIncomingDamageServerReceiver onIncomingDamageReceiver)
        {
            if (healthComponent)
            {
                int index = Array.IndexOf(healthComponent.onIncomingDamageReceivers, onIncomingDamageReceiver);
                if (index >= 0)
                {
                    ArrayUtils.ArrayRemoveAtAndResize(ref healthComponent.onIncomingDamageReceivers, index);
                }
            }
        }

        public static void AddTakeDamageReceiver(this HealthComponent healthComponent, IOnTakeDamageServerReceiver onTakeDamageReceiver)
        {
            if (healthComponent && Array.IndexOf(healthComponent.onTakeDamageReceivers, onTakeDamageReceiver) < 0)
            {
                ArrayUtils.ArrayAppend(ref healthComponent.onTakeDamageReceivers, onTakeDamageReceiver);
            }
        }

        public static void RemoveTakeDamageReceiver(this HealthComponent healthComponent, IOnTakeDamageServerReceiver onTakeDamageReceiver)
        {
            if (healthComponent)
            {
                int index = Array.IndexOf(healthComponent.onTakeDamageReceivers, onTakeDamageReceiver);
                if (index >= 0)
                {
                    ArrayUtils.ArrayRemoveAtAndResize(ref healthComponent.onTakeDamageReceivers, index);
                }
            }
        }
    }
}