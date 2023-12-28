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
using UnityEngine.SceneManagement;

namespace JohnHopooReturns
{
    public class NoMeteors : JohnHopooReturns.Behaviour
    {
        const string SECTION = "Hide Skybox Meteors";

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            SceneManager.activeSceneChanged += SceneManager_activeSceneChanged;
        }

        private void SceneManager_activeSceneChanged(Scene oldScene, Scene newScene)
        {
            GameObject.Find("HOLDER: Meteors")?.SetActive(false);
        }
    }
}