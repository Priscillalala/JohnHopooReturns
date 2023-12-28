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
using UnityEngine.Rendering.PostProcessing;
using RoR2.UI;

namespace JohnHopooReturns
{
    public class HideOSPBar : JohnHopooReturns.Behaviour, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Hide One Shot Protection Indicator";

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }   
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            if (!LoadAddressable<HealthBarStyle>("RoR2/Base/Common/HUDHealthBar.asset", out var HUDHealthBar))
            {
                yield return HUDHealthBar;
            }
            HUDHealthBar.Result.ospStyle.enabled = false;
        }
    }
}