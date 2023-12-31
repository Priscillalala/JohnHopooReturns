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
using EntityStates;
using RoR2.Skills;
using RoR2.CharacterAI;
using System.Linq;
using EntityStates.BeetleMonster;
using RoR2.Navigation;
using UnityEngine.Networking;

namespace JohnHopooReturns
{
    public class Beetles : JohnHopooReturns.Behaviour<Beetles>, JohnHopooReturns.IOnLoadStaticContentAsyncProvider
    {
        const string SECTION = "Beetles";

        public static SkillDef BeetleBodyBurrow { get; private set; }
        public static GameObject BeetleBurrowEffect { get; private set; }

        public void Awake()
        {
            if (!Config.Value(SECTION, string.Format(BEHAVIOUR_ENABLED, SECTION), true))
            {
                Destroy(this);
                return;
            }
            Content.entityStateTypes.Add(typeof(EnterBurrow));
            Content.entityStateTypes.Add(typeof(BeetleBurrow));
            Content.entityStateTypes.Add(typeof(ExitBurrow));
        }

        public IEnumerator LoadStaticContentAsync(LoadStaticContentAsyncArgs args)
        {
            BeetleBodyBurrow = ScriptableObject.CreateInstance<SkillDef>();
            BeetleBodyBurrow.skillName = "GS_BeetleBodyBurrow";
            BeetleBodyBurrow.activationStateMachineName = "Body";
            BeetleBodyBurrow.activationState = new SerializableEntityStateType(typeof(EnterBurrow));
            BeetleBodyBurrow.baseRechargeInterval = 8f;
            BeetleBodyBurrow.cancelSprintingOnActivation = false;
            BeetleBodyBurrow.isCombatSkill = false;
            Content.skillDefs.Add(BeetleBodyBurrow);
            if (!LoadAddressable<SkillFamily>("RoR2/Base/Beetle/BeetleBodySecondaryFamily.asset", out var BeetleBodySecondaryFamily)) 
            {
                yield return BeetleBodySecondaryFamily;
            }
            BeetleBodySecondaryFamily.Result.variants[0].skillDef = BeetleBodyBurrow;
            if (!LoadAddressable<GameObject>("RoR2/Base/Beetle/BeetleMaster.prefab", out var BeetleMaster))
            {
                yield return BeetleMaster;
            }
            AISkillDriver[] skillDrivers = BeetleMaster.Result.GetComponents<AISkillDriver>();
            AISkillDriver followNodeGraphToTarget = skillDrivers.FirstOrDefault(x => x.customName == "FollowNodeGraphToTarget");
            if (followNodeGraphToTarget)
            {
                DestroyImmediate(followNodeGraphToTarget);
            }
            AISkillDriver jumpAtTarget = BeetleMaster.Result.AddComponent<AISkillDriver>();
            jumpAtTarget.customName = "BurrowTowardsTarget";
            jumpAtTarget.skillSlot = SkillSlot.Secondary;
            jumpAtTarget.requireSkillReady = true;
            jumpAtTarget.minDistance = 20f;
            jumpAtTarget.maxDistance = 60f;
            jumpAtTarget.selectionRequiresTargetLoS = true;
            jumpAtTarget.selectionRequiresOnGround = true;
            jumpAtTarget.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            jumpAtTarget.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
            jumpAtTarget.aimType = AISkillDriver.AimType.AtCurrentEnemy;

            AISkillDriver newFollowNodeGraphToTarget = BeetleMaster.Result.AddComponent<AISkillDriver>();
            newFollowNodeGraphToTarget.customName = "FollowNodeGraphToTarget";
            newFollowNodeGraphToTarget.skillSlot = SkillSlot.None;
            newFollowNodeGraphToTarget.moveTargetType = AISkillDriver.TargetType.CurrentEnemy;
            newFollowNodeGraphToTarget.movementType = AISkillDriver.MovementType.ChaseMoveTarget;
            newFollowNodeGraphToTarget.aimType = AISkillDriver.AimType.MoveDirection;

            if (!LoadAddressable<EntityStateConfiguration>("RoR2/Base/Beetle/EntityStates.BeetleMonster.SpawnState.asset", out var spawnStateConfiguration))
            {
                yield return spawnStateConfiguration;
            }
            spawnStateConfiguration.Result.TryModifyFieldValue(nameof(SpawnState.duration), 3.5f);

            if (!LoadAddressable<GameObject>("RoR2/Base/Beetle/BeetleBody.prefab", out var beetleBodyPrefab))
            {
                yield return beetleBodyPrefab;
            }
            beetleBodyPrefab.Result.GetComponent<CharacterBody>().baseMoveSpeed = 7f;

            if (!LoadAddressable<GameObject>("RoR2/Base/Beetle/BeetleGuardSunderPop.prefab", out var BeetleGuardSunderPop))
            {
                yield return BeetleGuardSunderPop;
            }
            BeetleBurrowEffect = PrefabAPI.InstantiateClone(BeetleGuardSunderPop.Result, "BeetleBurrowEffect", false);
            BeetleBurrowEffect.GetComponent<VFXAttributes>().vfxPriority = VFXAttributes.VFXPriority.Always;
            if (BeetleBurrowEffect.transform.TryFind("Particles/ParticleInitial/Dust", out Transform dust) && dust.TryGetComponent(out ParticleSystemRenderer dustRenderer))
            {
                dustRenderer.sharedMaterial = new Material(dustRenderer.sharedMaterial);
                dustRenderer.sharedMaterial.SetColor("_TintColor", new Color32(201, 126, 44, 255));
            }
            if (!LoadAddressable<GameObject>("RoR2/Base/Beetle/BeetleGuardGroundSlam.prefab", out var BeetleGuardGroundSlam))
            {
                yield return BeetleGuardGroundSlam;
            }
            if (BeetleGuardGroundSlam.Result.transform.TryFind("ParticleInitial/Decal", out var decal))
            {
                GameObject burrowDecal = Instantiate(decal.gameObject, BeetleBurrowEffect.transform);
                burrowDecal.transform.localScale = Vector3.one * 5f;
            }
            Content.effectDefs.Add(new EffectDef(BeetleBurrowEffect));
        }

        public float baseBurrowDuration = 1f;
        public float baseBurrowExitDuration = 1.3f;
        public float exitJumpMarker = 0.4f;
        public float exitVelocityBonus = 0f;
        public float crossfadeDelay = 1.1f;
        public float crossfadeDuration = 0.2f;
        public float animSpeed = 1.3f;
        public string burrowSoundString = "Play_treeBot_sprint_end";
        public string startSoundString = "Play_beetle_worker_idle";
        public string endSoundString = "Play_hermitCrab_unburrow";
        public float burrowAccuracyCoefficient = 0.3f;

        public class EnterBurrow : BaseState
        {
            private Animator modelAnimator;
            private float duration;
            private bool didCrossfade;

            public override void OnEnter()
            {
                base.OnEnter();
                duration = Instance.crossfadeDelay + Instance.crossfadeDuration; //Instance.baseBurrowEntryDuration;
                PlayCrossfade("Body", "EmoteSurprise", 0.1f);
                modelAnimator = GetModelAnimator();
                if (modelAnimator)
                {
                    modelAnimator.speed = Instance.animSpeed;
                    //PlayAnimationOnAnimator(modelAnimator, "Body", "Spawn1", "Spawn1.playbackRate", -duration);
                    /*modelAnimator.speed = 1f;
                    modelAnimator.Update(0f);
                    int layerIndex = modelAnimator.GetLayerIndex("Body");
                    modelAnimator.SetFloat("Spawn1.playbackRate", 1f);
                    modelAnimator.CrossFadeInFixedTime("Spawn1", Instance.crossfadeDuration, layerIndex);
                    modelAnimator.Update(0f);
                    float length = modelAnimator.GetNextAnimatorStateInfo(layerIndex).length;
                    modelAnimator.SetFloat("Spawn1.playbackRate", length / -duration);*/

                }
                RoR2.Util.PlaySound(Instance.startSoundString, gameObject);
            }

            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (fixedAge >= Instance.crossfadeDelay)
                {
                    TryCrossfade();
                }
                if (isAuthority && fixedAge >= duration)
                {
                    outer.SetNextState(new BeetleBurrow());
                    return;
                }
            }

            public override void OnExit()
            {
                TryCrossfade();
                if (NetworkServer.active)
                {
                    RoR2.Util.CleanseBody(characterBody, true, false, false, true, false, false);
                }
                base.OnExit();
            }

            public void TryCrossfade()
            {
                if (!didCrossfade)
                {
                    if (modelAnimator)
                    {
                        modelAnimator.speed = 1f;
                        modelAnimator.Update(0f);
                        modelAnimator.SetFloat("Spawn1.playbackRate", 0f);
                        modelAnimator.CrossFadeInFixedTime("Spawn1", Instance.crossfadeDuration, modelAnimator.GetLayerIndex("Body"));

                        EffectManager.SimpleEffect(BeetleBurrowEffect, characterBody.footPosition, Quaternion.identity, false);
                        RoR2.Util.PlaySound(Instance.burrowSoundString, gameObject);
                        didCrossfade = true;
                    }
                }
            }
        }

        public class BeetleBurrow : BaseState
        {
            const float ARBITRARY_RADIUS = 10f;
            private HurtBox target;
            private Vector3 predictedDestination;
            public float duration;
            private CharacterModel characterModel;
            private HurtBoxGroup hurtboxGroup;

            public override void OnEnter()
            {
                base.OnEnter();
                duration = Instance.baseBurrowDuration;
                Transform modelTransform = GetModelTransform();
                if (modelTransform)
                {
                    characterModel = modelTransform.GetComponent<CharacterModel>();
                    hurtboxGroup = modelTransform.GetComponent<HurtBoxGroup>();
                }
                if (characterModel)
                {
                    characterModel.invisibilityCount++;
                }
                if (hurtboxGroup)
                {
                    hurtboxGroup.hurtBoxesDeactivatorCounter++;
                }
                if (characterMotor)
                {
                    characterMotor.enabled = false;
                }
                gameObject.layer = LayerIndex.fakeActor.intVal;
                characterMotor.Motor.RebuildCollidableLayers();
                CalculatePredictedDestination();
                RoR2.Util.PlaySound("Play_magmaWorm_burrowed_loop", gameObject);
            }

            private void CalculatePredictedDestination()
            {
                Vector3 difference = Vector3.zero;
                Ray aimRay = GetAimRay();
                BullseyeSearch bullseyeSearch = new BullseyeSearch
                {
                    searchOrigin = aimRay.origin,
                    searchDirection = aimRay.direction,
                    maxDistanceFilter = 100f,
                    teamMaskFilter = TeamMask.allButNeutral,
                    filterByLoS = false,
                    sortMode = BullseyeSearch.SortMode.Angle,
                };
                bullseyeSearch.teamMaskFilter.RemoveTeam(TeamComponent.GetObjectTeam(gameObject));
                bullseyeSearch.RefreshCandidates();
                target = bullseyeSearch.GetResults().FirstOrDefault();
                if (target)
                {
                    difference = target.transform.position - transform.position;
                    CharacterMotor characterMotor = target.healthComponent?.body?.characterMotor;
                    if (characterMotor)
                    {
                        Vector3 moveDirection = characterMotor.moveDirection.normalized;
                        float estimatedTravelDistance = target.healthComponent.body.moveSpeed * duration;
                        Vector3 differenceFromMotor = difference + ((estimatedTravelDistance + ARBITRARY_RADIUS) * moveDirection);
                        if (differenceFromMotor.sqrMagnitude <= ARBITRARY_RADIUS * ARBITRARY_RADIUS)
                        {
                            differenceFromMotor = difference - ((estimatedTravelDistance - ARBITRARY_RADIUS) * moveDirection);
                        }
                        difference = differenceFromMotor;
                    }
                }
                predictedDestination = transform.position + difference;
                characterDirection.forward = difference;
            }

            private Vector3 GetFinalPosition()
            {
                Vector3 finalDestination = target ? Vector3.Lerp(predictedDestination, target.transform.position, Instance.burrowAccuracyCoefficient) : predictedDestination;
                NodeGraph groundNodes = SceneInfo.instance.groundNodes;
                NodeGraph.NodeIndex nodeIndex = groundNodes.FindClosestNode(finalDestination, characterBody.hullClassification);
                groundNodes.GetNodePosition(nodeIndex, out finalDestination);
                finalDestination += transform.position - characterBody.footPosition;
                return finalDestination;
            }

            private void SetPosition(Vector3 newPosition)
            {
                characterMotor?.Motor.SetPositionAndRotation(newPosition, Quaternion.identity, true);
            }

            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (characterMotor)
                {
                    characterMotor.velocity = Vector3.zero;
                }
                if (isAuthority && fixedAge >= duration)
                {
                    outer.SetNextState(new ExitBurrow());
                }
            }

            public override void OnExit()
            {
                Vector3 finalPosition = GetFinalPosition();
                characterDirection.forward = finalPosition - transform.position;
                SetPosition(finalPosition);
                gameObject.layer = LayerIndex.defaultLayer.intVal;
                characterMotor?.Motor.RebuildCollidableLayers();
                if (characterModel)
                {
                    characterModel.invisibilityCount--;
                }
                if (hurtboxGroup)
                {
                    hurtboxGroup.hurtBoxesDeactivatorCounter--;
                }
                if (characterMotor)
                {
                    characterMotor.enabled = true;
                }
                RoR2.Util.PlaySound("Stop_magmaWorm_burrowed_loop", gameObject);
                base.OnExit();
            }
        }

        public class ExitBurrow : BaseState
        {
            const float ANIM_DURATION_COEF = 0.7f;
            const float ANIM_JUMP_MARKER = 0.6f;

            private float duration;
            private bool didJump;
            private bool didCancelAnimation;
            private bool movementHitAuthority;

            public override void OnEnter()
            {
                base.OnEnter();
                duration = Instance.baseBurrowExitDuration / attackSpeedStat;
                PlayAnimation("Body", "Spawn1", "Spawn1.playbackRate", duration / ANIM_DURATION_COEF);
                if (isAuthority)
                {
                    characterMotor.onMovementHit += OnMovementHit;
                }
                /*if (characterMotor)
                {
                    characterMotor.Motor.ForceUnground();
                    //characterMotor.velocity = new Vector3(characterMotor.velocity.x, Mathf.Max(characterMotor.velocity.y, 5f), characterMotor.velocity.z);
                    characterMotor.velocity += Vector3.up * Instance.exitVelocityBonus;
                }*/
            }

            public override void FixedUpdate()
            {
                base.FixedUpdate();
                if (fixedAge >= duration * Instance.exitJumpMarker / ANIM_DURATION_COEF)
                {
                    TryJump();
                }
                if (fixedAge < duration)
                {
                    return;
                }
                TryCancelAnimation();
                if (isAuthority && (movementHitAuthority || (characterMotor.Motor.GroundingStatus.IsStableOnGround && !characterMotor.Motor.LastGroundingStatus.IsStableOnGround)))
                {
                    outer.SetNextStateToMain();
                }
            }

            public override void OnExit()
            {
                TryJump();
                TryCancelAnimation();
                if (isAuthority)
                {
                    characterMotor.onMovementHit -= OnMovementHit;
                }
                base.OnExit();
            }

            public void TryJump()
            {
                if (!didJump && characterMotor)
                {
                    EffectManager.SimpleEffect(BeetleBurrowEffect, characterBody.footPosition, Quaternion.identity, false);
                    RoR2.Util.PlaySound(Instance.burrowSoundString, gameObject);
                    RoR2.Util.PlaySound(Instance.endSoundString, gameObject);
                    characterMotor.Motor.ForceUnground();
                    characterMotor.velocity += Vector3.up * Instance.exitVelocityBonus;
                    didJump = true;
                }
            }

            public void TryCancelAnimation()
            {
                if (!didCancelAnimation)
                {
                    PlayCrossfade("Body", isGrounded ? "Idle" : "Jump", 1f);
                    didCancelAnimation = true;
                }
            }

            private void OnMovementHit(ref CharacterMotor.MovementHitInfo movementHitInfo)
            {
                movementHitAuthority = true;
            }
        }
    }
}