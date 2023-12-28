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

namespace JohnHopooReturns
{
    public class PostProcessing : JohnHopooReturns.Behaviour
    {
        const string SECTION = "Updated Post Processing";

        public float temperature = Config.Value(SECTION, "Temperature", 2f);
        //public float saturation = Config.Value(SECTION, "Saturation", 1f);
        public bool enableToneMapping = Config.Value(SECTION, "Enable Custom Tone Mapping", true);

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            PostProcessProfile ppProfile = ScriptableObject.CreateInstance<PostProcessProfile>();
            ppProfile.name = "ppJohnHopooGames";
            ColorGrading colorGrading = ppProfile.AddSettings<ColorGrading>();
            colorGrading.temperature.Override(temperature);
            if (enableToneMapping)
            {
                colorGrading.tonemapper.Override(Tonemapper.Custom);
                colorGrading.toneCurveToeStrength.Override(0.35f);
                colorGrading.toneCurveToeLength.Override(0.6f);
                colorGrading.toneCurveShoulderStrength.Override(1f);
                colorGrading.toneCurveShoulderLength.Override(2f);
            }

            GameObject ppController = new GameObject("JohnHopooGamesPostProcessing");
            ppController.layer = LayerIndex.postProcess.intVal;
            DontDestroyOnLoad(ppController);
            PostProcessVolume ppVolume = ppController.AddComponent<PostProcessVolume>();
            ppVolume.sharedProfile = ppProfile;
            ppVolume.isGlobal = true;
            ppVolume.weight = 1f;
            ppVolume.priority = 1f;
            //RoR2Application.onStart = (Action)Delegate.Combine(RoR2Application.onStart, new Action(delegate
            //{
                /*PostProcessVolume ppVolume = ArrayUtils.GetSafe(RoR2Application.instance.GetComponents<PostProcessVolume>(), 1);
                if (ppVolume && ppVolume.profile && ppVolume.profile.TryGetSettings(out ColorGrading colorGrading))
                {
                    colorGrading.temperature.Override(temperature);
                    //colorGrading.saturation.Override(saturation);
                    colorGrading.tonemapper.Override(Tonemapper.Custom);
                    colorGrading.toneCurveToeStrength.Override(0.35f);
                    colorGrading.toneCurveToeLength.Override(0.6f);
                    colorGrading.toneCurveShoulderStrength.Override(1f);
                    colorGrading.toneCurveShoulderLength.Override(2f);
                }*/
                
            //}));
        }
    }
}