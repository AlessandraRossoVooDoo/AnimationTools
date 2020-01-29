using System.Collections.Generic;
using Gumbug.Outpost.Graphics;
using Gumbug.Outpost.Utils;
using Gumbug.Parzival.Assets;
using Gumbug.Parzival.UnityObject;
using UnityEngine;

namespace Gumbug.Outpost.Game.View
{
    public class MercenaryAnimationView : MonoBehaviour
    {
        private MercenaryView mercenaryView; 
        private bool shoot;
        private float shootAngle;
        private Quaternion perspectiveRotationOffset;
        private Quaternion baseLocalRotation;
        private bool specialActive;
        private bool hasCancellableSpecial;
        private float glowPulseTime;
        private int stopNextFrame;
        private Quaternion spineRotation;
        private float primaryAnimLength;
        private float specialAnimLength;
        private float shootTimer;
        private static readonly string[] footstepAudioNames = {"footstep_02", "footstep_03"};
        private static AudioClip[] footstepClips;
        private AudioSource audioSource;
        private bool walking;
        private float walkingTimer;
        private bool isActiveAudioListener;
        private bool nonGrounded;
        
        private readonly Dictionary<Renderer, Material> weaponCache = new Dictionary<Renderer, Material>();
        
        private static Material coverMaterial;
        private bool flip;

        private const float AnimationLengthOffset = 0.2f;
        private static bool routinesInitialised;

//        private AnimatorLod highPolyLod;
        private AnimatorLod lowPolyLod;
        private AnimatorLod activeLod;
        private float glowFlashDuration;
        private float glowFlashTime;
        private bool glowPulseActive;
        private bool shotFlip;
        private const float FlashStrength = 5f;
        private const float GlowPulseSpeed = (Mathf.PI*2)*2f;

        public Dictionary<string, Transform> LeftHandWeaponLocators => activeLod.LeftHandWeaponLocators;
        public Dictionary<string, Transform> RightHandWeaponLocators => activeLod.RightHandWeaponLocators;

        private void Awake()
        {
            mercenaryView = transform.parent.parent.GetComponent<MercenaryView>();
            audioSource = transform.parent.GetComponent<AudioSource>();
            audioSource.loop = false;
            audioSource.playOnAwake = false;
            audioSource.priority = mercenaryView.IsUserMercenaryView ? 256 : 128;
            if (footstepClips == null)
            {
                footstepClips = new AudioClip[footstepAudioNames.Length];
                for (int i = 0; i < footstepAudioNames.Length; i++)
                {
                    var footstepAudioName = footstepAudioNames[i];
                    footstepClips[i] = AssetManager.GetObject<AudioClip>(footstepAudioName);
                }
            }
        }
        
        public void OutCoverWeapons()
        {
            foreach (var kvp in weaponCache)
                kvp.Key.sharedMaterial = kvp.Value;
        }

        public void InCover()
        {
            activeLod?.InCover();

            InCoverWeapons();
        }

        private void InCoverWeapons()
        {
            foreach (var kvp in weaponCache)
                kvp.Key.sharedMaterial = coverMaterial;
        }

        public void OutCover()
        {
            activeLod?.OutCover();

            OutCoverWeapons();
        }

        private void Start()
        {
            if (coverMaterial == null)
                coverMaterial = new Material(AssetManager.LoadFromBundleAndPath<Shader>(AssetBundleNames.Initial, "Assets/Outpost/Shaders/Other/OutlineCover.shader"));

            baseLocalRotation = Quaternion.Euler(0, 90, 0);
            transform.localRotation = baseLocalRotation;
            hasCancellableSpecial = HasCancellableSpecial();
            perspectiveRotationOffset = Quaternion.AngleAxis(GraphicProperties.PerspectiveOffset, new Vector3(1, 0, 0));

            primaryAnimLength = 0.8f;
            specialAnimLength = 0.8f;

            lowPolyLod.Start();
//            highPolyLod.Start();
        }

        private static bool HasCancellableSpecial()
        {
            if (GameController.Instance.HasUserMercenary)
                return GameController.Instance.UserMercenary.SpecialWeapon.CmWeapon.SpecialAnimatorType == 1;
            
            return false;
        }

//        public void DoUpdate()
//        {
//            
//
////            if (activeLod == null)
////                return;
////            var delta = GameCamera.Instance.transform.position - transform.position;
////            if (delta.sqrMagnitude > 10*10)
////            {
////                if (activeLod != lowPolyLod)
////                {
////                    ChangeToLowLod();
////                }
////            }
////            else
////            {
////                if (activeLod != highPolyLod)
////                {
////                    ChangeToHighLod();
////                }
////            }
//        }

        private void ChangeToHighLod()
        {
            if (!lowPolyLod.Initialised)
                return;
            
            lowPolyLod.DisableLod();
//            highPolyLod.EnableLod();
//            highPolyLod.SwapWeapons(lowPolyLod);
//            highPolyLod.CopyAnimatorState(lowPolyLod);
//            highPolyLod.CopyMaterialState(lowPolyLod);
            lowPolyLod.RemoveWeapons();
//            activeLod = highPolyLod;
        }

        private void ChangeToLowLod()
        {
            if (!lowPolyLod.Initialised)
                return;
            
//            highPolyLod.DisableLod();
            lowPolyLod.EnableLod();
//            lowPolyLod.SwapWeapons(highPolyLod);
//            lowPolyLod.CopyAnimatorState(highPolyLod);
//            lowPolyLod.CopyMaterialState(highPolyLod);
//            highPolyLod.RemoveWeapons();
            activeLod = lowPolyLod;
        }

        private void LateUpdate()
        {
            if (activeLod == null)
                ChangeToLowLod();

            var parentTransform = transform.parent;
            parentTransform.localRotation = Quaternion.Euler(new Vector3(0, 0, 0));
            
            transform.localRotation = baseLocalRotation;
            
            if (!hasCancellableSpecial || hasCancellableSpecial && !specialActive)
            {
                if (shoot)
                {
                    var adjustedShootAngle = shootAngle - 180;
                    var deltaAngle = Mathf.DeltaAngle(transform.eulerAngles.y, adjustedShootAngle);

                    if (Mathf.Abs(deltaAngle) > 90)
                        transform.localRotation = Quaternion.Euler(new Vector3(0, deltaAngle + 90, 0));

                    spineRotation = Quaternion.Slerp(spineRotation, Quaternion.Euler(0, adjustedShootAngle, 0), 0.8f);
                    activeLod.Spine.rotation = spineRotation;
                    
                    shootTimer -= Time.deltaTime;

                    if (shootTimer <= 0)
                        shoot = false;
                }
                else
                {
                    spineRotation = Quaternion.Slerp(spineRotation, Quaternion.Euler(0, transform.eulerAngles.y, 0), 0.4f);
                    activeLod.Spine.rotation = spineRotation;
                }
            }

            parentTransform.rotation = perspectiveRotationOffset*parentTransform.rotation;

            if (mercenaryView != null)
            {
                if (walking)
                {
                    if (walkingTimer <= 0)
                    {
                        flip = !flip;
                        audioSource.PlayOneShot(footstepClips[flip ? 0 : 1]);
                    }
                    walkingTimer += Time.deltaTime;
                    if (walkingTimer > 0.3f)
                        walkingTimer = 0;
                }
            }

            if (glowFlashDuration > 0f)
            {
                var f = Mathf.Clamp01(glowPulseTime/glowFlashDuration)*FlashStrength;
                activeLod.GlowFlash(f);
                glowFlashTime += Time.deltaTime;
                if (glowFlashTime >= glowFlashDuration)
                {
                    glowFlashDuration = 0;
                    glowFlashTime = 0;
                    DisableGlow();
                }
            }

            if (glowPulseActive)
            {
                var f = (Mathf.Cos(glowPulseTime*GlowPulseSpeed) + 1)*0.5f*FlashStrength;
                activeLod?.GlowFlash(f);
                glowPulseTime += Time.deltaTime;
            }
        }

        public void Reset()
        {       
            shoot = false;
            shootAngle = 0f;
            
            transform.localRotation = baseLocalRotation;

            lowPolyLod.Reset();
//            highPolyLod.Reset();

            Idle();
        }

        public void ShootPrimary(float angle)
        {
            shootTimer = primaryAnimLength;
            shoot = true;
            shootAngle = angle;
            shotFlip = !shotFlip;
            activeLod.SetBool(AnimatorParameters.ShotFlip, shotFlip);
            activeLod.SetTrigger(AnimatorParameters.Shoot1);
        }
        
        public void PunchPrimary(float angle)
        {
            shootTimer = primaryAnimLength;
            shoot = true;
            shootAngle = angle;
            activeLod.SetTrigger(AnimatorParameters.Punch);
        }

        public void ShootSpecial(float angle)
        {
            if (hasCancellableSpecial)
            {
                if (specialActive)
                    return;
                shoot = false;
                specialActive = true;
            }
            else
            {
                shoot = true;
                shootTimer = specialAnimLength;
            }

            shootAngle = angle;
            activeLod.ResetTrigger(AnimatorParameters.CancelSpecial);
            activeLod.SetTrigger(AnimatorParameters.Shoot2);
        }
        
        public void Stunned(bool isStunned)
        {
            activeLod.SetBool(AnimatorParameters.Stunned, isStunned);
        }
        
//        public void Victory()
//        {
//            activeLod.SetTrigger(AnimatorParameters.Victory);
//        }
//
//        public void Defeat()
//        {
//            activeLod.SetTrigger(AnimatorParameters.Defeat);
//        }
        
        public void CancelSpecial()
        {
            if (specialActive && hasCancellableSpecial)
            {
                activeLod.ResetTrigger(AnimatorParameters.Shoot2);
                activeLod.SetTrigger(AnimatorParameters.CancelSpecial);
                specialActive = false;
            }
        }

        public void EnableGlowFlash(Color color, float duration)
        {
            glowPulseTime = 0f;

            activeLod.EnableGlowFlash(color);

            glowFlashDuration = duration;
            glowFlashTime = 0f;
        }
        
        public void EnableGlowPulse(Color color)
        {
            glowPulseTime = 0f;
            activeLod.EnableGlowFlash(color);
            glowPulseActive = true;
        }
        
        public void DisableGlowPulse()
        {
            glowPulseActive = false;
            DisableGlow();
        }
        
        private void DisableGlow()
        {
            activeLod?.DisableGlow();
        }

        public void DoTheLocomotion(float speed)
        {            
            if (Mathf.Abs(speed) < 0.001f)
                Idle();
            else 
                WalkForward();     
        }
        
        private void WalkForward()
        {
            if (activeLod.GetBool(AnimatorParameters.WalkForward))
                return;
            
            if (nonGrounded)
                return;
            
            activeLod.SetBool(AnimatorParameters.WalkForward, true);
            
            walking = true;
            walkingTimer = 0;
        }

        private void Idle()
        {
            if (!activeLod.GetBool(AnimatorParameters.WalkForward))
                return;

            if (nonGrounded)
                return;

            activeLod.SetBool(AnimatorParameters.WalkForward, false);

            walking = false;
        }

        public void Hide()
        {
            activeLod?.Hide();
        }

        public void Show()
        {
            activeLod?.Show();
        }

        public void ShowGrounded()
        {
            nonGrounded = false;
            activeLod.SetTrigger(AnimatorParameters.ParachuteRelease);
        }

        public void ShowNonGrounded()
        {
            nonGrounded = true;
            walking = false;
            activeLod.SetTrigger(AnimatorParameters.ParachuteHang);
        }

        public void DisableRendering()
        {
            gameObject.SetActive(false);
        }

        public void ChangeWeaponAnimator(string weaponClass, string handedness, float firingDuration)
        {
            ResetAllLayers();

            activeLod.ChangeWeaponAnimator(weaponClass, handedness);
            
            primaryAnimLength = firingDuration <= 0 ? 0.8f : firingDuration + AnimationLengthOffset;
        }

        public void RevertAnimation()
        {
            ResetAllLayers();

            primaryAnimLength = 0.8f;

            activeLod.ResetTrigger(AnimatorParameters.Shoot1);
            activeLod.ResetTrigger(AnimatorParameters.Shoot2);
            if (hasCancellableSpecial)
            {
                activeLod.SetTrigger(AnimatorParameters.CancelSpecial);
            }
        }

        private void ResetAllLayers()
        {
            activeLod.ResetAllLayers();
        }

        public Transform GetBone(HumanBodyBones bone)
        {
            return activeLod.GetBoneTransform(bone);
        }

        public void UnregisterWeapons(List<WeaponEntry> primaryWeapons)
        {
            activeLod.UnregisterWeapons(primaryWeapons);
            
            for (int j = 0; j < primaryWeapons.Count; j++)
            {
                var primaryWeapon = primaryWeapons[j];
                for (int i = 0; i < primaryWeapon.Renderers.Length; i++)
                {
                    var weaponRenderer = primaryWeapon.Renderers[i];
                    if (weaponCache.ContainsKey(weaponRenderer))
                        weaponCache.Remove(weaponRenderer);
                }
            }
        }

        public void RegisterWeapons(List<WeaponEntry> primaryWeapons, bool inCover)
        {
            activeLod.RegisterWeapons(primaryWeapons);

            for (int j = 0; j < primaryWeapons.Count; j++)
            {
                var primaryWeapon = primaryWeapons[j];
                for (int i = 0; i < primaryWeapon.Renderers.Length; i++)
                {
                    var weaponRenderer = primaryWeapon.Renderers[i];
                    weaponCache[weaponRenderer] = weaponRenderer.sharedMaterial;
                }
            }
            
            if (inCover)
                InCoverWeapons();
        }

        public void ClearWeaponCache()
        {
            weaponCache.Clear();
        }

//        public void SetHighPoly(GameObject highPoly)
//        {
//            highPolyLod = new AnimatorLod(highPoly);
//        }

        public void SetLowPoly(GameObject lowPoly)
        {
            lowPolyLod = new AnimatorLod(lowPoly);
            ChangeToLowLod();
        }

        private class AnimatorLod
        {
            public Transform Spine { get; private set; }
            public Dictionary<string, Transform> RightHandWeaponLocators { get; } = new Dictionary<string, Transform>();
            public Dictionary<string, Transform> LeftHandWeaponLocators { get; } = new Dictionary<string, Transform>();
            public bool Initialised { get; private set; }

            private bool isInCover;
            private Vector3 initialSpineEulerAngles;
            private SkinnedMeshRenderer[] skinnedMeshRenderers;
            
            private readonly List<WeaponEntry> weapons = new List<WeaponEntry>();
            private readonly GameObject source;
            private readonly Animator animator;
            private readonly List<Material> glowMaterials = new List<Material>();
            private readonly Dictionary<SkinnedMeshRenderer, Material> skinnedCache = new Dictionary<SkinnedMeshRenderer, Material>();

            public AnimatorLod(GameObject gameObject)
            {
                source = gameObject;
                animator = gameObject.GetComponent<Animator>();
            }

            public void Start()
            {
                Spine = animator.GetBoneTransform(HumanBodyBones.Spine);
                initialSpineEulerAngles = Spine.eulerAngles;
                
                for (int i = 0; i < MercenaryViewUtils.LocatorNames.Length; i++)
                {
                    var locatorName = MercenaryViewUtils.LocatorNames[i];
                    var right = source.transform.FindRecursive(MercenaryViewUtils.GetFullRightLocatorName(locatorName));
                    var left = source.transform.FindRecursive(MercenaryViewUtils.GetFullLeftLocatorName(locatorName));
                    if (right != null)
                        RightHandWeaponLocators.Add(locatorName, right);
                    if (left != null)
                        LeftHandWeaponLocators.Add(locatorName, left);
                }
                
                GatherGlowMaterials();

                Initialised = true;
            }

            private void GatherGlowMaterials()
            {
                Material newMat = null;
                skinnedMeshRenderers = source.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var skinnedMeshRenderer in skinnedMeshRenderers)
                {
                    if (newMat == null)
                        newMat = new Material(skinnedMeshRenderer.sharedMaterial);
                    skinnedMeshRenderer.sharedMaterial = newMat;
                    skinnedCache[skinnedMeshRenderer] = skinnedMeshRenderer.sharedMaterial;
                }
                glowMaterials.Add(newMat);
            }

            public void Reset()
            {
                Spine.eulerAngles = initialSpineEulerAngles;

                animator.ResetTrigger(AnimatorParameters.Shoot1);
                animator.ResetTrigger(AnimatorParameters.Shoot2);
//                animator.ResetTrigger(AnimatorParameters.Defeat);
//                animator.ResetTrigger(AnimatorParameters.Victory);   
                animator.ResetTrigger(AnimatorParameters.CancelSpecial);
            }

            public void SetTrigger(int parameter)
            {
                animator.SetTrigger(parameter);
            }

            public void ResetTrigger(int parameter)
            {
                animator.ResetTrigger(parameter);
            }

            public void EnableGlowFlash(Color color)
            {
                for (var i = 0; i < glowMaterials.Count; i++)
                {
                    var material = glowMaterials[i];
                    material.EnableKeyword(ShaderKeywords.OutpostMercenaryGlowOn);
                    material.SetColor(ShaderProperties.GlowColor, color);
                }
            }

            public void GlowFlash(float f)
            {
                for (int i = 0; i < glowMaterials.Count; i++)
                {
                    glowMaterials[i].SetFloat(ShaderProperties.FresnelAmount, f);
                }
            }

            public void DisableGlow()
            {
                for (var i = 0; i < glowMaterials.Count; i++)
                {
                    var material = glowMaterials[i];
                    material.DisableKeyword(ShaderKeywords.OutpostMercenaryGlowOn);
                }
            }

            public bool GetBool(int parameter)
            {
                return animator.GetBool(parameter);
            }

            public void SetBool(int parameter, bool value)
            {
                animator.SetBool(parameter, value);
            }

            public void ChangeWeaponAnimator(string weaponClass, string handedness)
            {
                var layerIndex = animator.GetLayerIndex($"{weaponClass}_{handedness}");
                animator.SetLayerWeight(layerIndex, 1);
                animator.SetInteger(AnimatorParameters.WeaponType, layerIndex - 1);
            }

            public void ResetAllLayers()
            {
                for (int i = 1; i < 10; i++)
                    animator.SetLayerWeight(i, 0);
            }

            public void UnregisterWeapons(List<WeaponEntry> primaryWeapons)
            {
                for (int i = 0; i < primaryWeapons.Count; i++)
                {
                    var primaryWeapon = primaryWeapons[i];
                    
                    for (int j = 0; j < primaryWeapon.Renderers.Length; j++)
                    {
                        var sharedMaterial = primaryWeapon.Renderers[j].sharedMaterial;
                        if (glowMaterials.Contains(sharedMaterial))
                        {
                            sharedMaterial.DisableKeyword(ShaderKeywords.OutpostMercenaryGlowOn);
                            glowMaterials.Remove(sharedMaterial);
                        }
                    }

                    if (weapons.Contains(primaryWeapon))
                        weapons.Remove(primaryWeapon);
                }
            }
            
            public void RegisterWeapons(IEnumerable<WeaponEntry> primaryWeapons)
            {
                weapons.AddRange(primaryWeapons);
                for (int i = 0; i < weapons.Count; i++)
                {
                    var weaponEntry = weapons[i];
                    RegisterWeaponMaterial(weaponEntry.Renderers);
                }
            }

            private void RegisterWeaponMaterial(Renderer[] renderers)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    var weaponRenderer = renderers[i];
                    weaponRenderer.sharedMaterial = new Material(weaponRenderer.sharedMaterial);
                    glowMaterials.Add(weaponRenderer.sharedMaterial);
                }
            }

            public void InCover()
            {
                isInCover = true;
                foreach (var kvp in skinnedCache)
                    kvp.Key.sharedMaterial = coverMaterial;
            }

            public void OutCover()
            {
                isInCover = false;
                foreach (var kvp in skinnedCache)
                    kvp.Key.sharedMaterial = kvp.Value;
            }

            public Transform GetBoneTransform(HumanBodyBones bone)
            {
                return animator.GetBoneTransform(bone);
            }

            public void DisableLod()
            {
                for (int i = 0; i < skinnedMeshRenderers.Length; i++)
                    skinnedMeshRenderers[i].enabled = false;
            }
            
            public void EnableLod()
            {
                for (int i = 0; i < skinnedMeshRenderers.Length; i++)
                    skinnedMeshRenderers[i].enabled = true;
            }

            public void RemoveWeapons()
            {
                UnregisterWeapons(weapons);
                weapons.Clear();
            }

            public void SwapWeapons(AnimatorLod sourceLod)
            {
                var sourceWeapons = sourceLod.weapons;
                for (int i = 0; i < sourceWeapons.Count; i++)
                {
                    var sourceWeapon = sourceWeapons[i];
                    var weaponTransform = sourceWeapon.Weapon.transform;
                    var targetLocator = sourceWeapon.IsRightHand ? RightHandWeaponLocators[sourceWeapon.LocatorName] : LeftHandWeaponLocators[sourceWeapon.LocatorName];

                    weaponTransform.SetParent(targetLocator);
                    weaponTransform.localPosition = Vector3.zero;
                    weaponTransform.localRotation = Quaternion.identity;
                    weaponTransform.localScale = Vector3.one;
                }
                
                RegisterWeapons(sourceWeapons);
            }

            public void Hide()
            {
                var renderers = source.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                    renderer.enabled = false;
            }

            public void Show()
            {
                var renderers = source.GetComponentsInChildren<Renderer>();
                foreach (var renderer in renderers)
                    renderer.enabled = true;
            }

            public void CopyAnimatorState(AnimatorLod sourceLod)
            {
                var sourceAnimator = sourceLod.animator;
                var targetAnimator = animator;

                for (int i = 0; i < sourceAnimator.layerCount; i++)
                {
                    var layerWeight = sourceAnimator.GetLayerWeight(i);
                    targetAnimator.SetLayerWeight(i, layerWeight);
                }

                for (int i = 0; i < sourceAnimator.parameterCount; i++)
                {
                    var sourceParameter = sourceAnimator.GetParameter(i);
                    switch (sourceParameter.type)
                    {
                        case AnimatorControllerParameterType.Bool:
                            targetAnimator.SetBool(sourceParameter.nameHash, sourceAnimator.GetBool(sourceParameter.nameHash));
                            break;
                        case AnimatorControllerParameterType.Float:
                            targetAnimator.SetFloat(sourceParameter.nameHash, sourceAnimator.GetFloat(sourceParameter.nameHash));
                            break;
                        case AnimatorControllerParameterType.Int:
                            targetAnimator.SetInteger(sourceParameter.nameHash, sourceAnimator.GetInteger(sourceParameter.nameHash));
                            break;
                        case AnimatorControllerParameterType.Trigger:
                            if (sourceAnimator.GetBool(sourceParameter.nameHash))
                                targetAnimator.SetTrigger(sourceParameter.nameHash);
                            else
                                targetAnimator.ResetTrigger(sourceParameter.nameHash);
                            break;
                    }
                }
            }

            public void CopyMaterialState(AnimatorLod sourceLod)
            {
                if (sourceLod.isInCover)
                    InCover();
                else
                    OutCover();
            }
        }

        
    }
}
