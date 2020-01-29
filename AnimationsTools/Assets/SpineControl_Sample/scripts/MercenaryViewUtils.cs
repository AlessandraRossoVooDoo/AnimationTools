using System.Collections.Generic;
using Gumbug.Outpost.Game.View;
using Gumbug.Outpost.Graphics;
using Gumbug.Parzival.Assets;
using Gumbug.Parzival.Pooling;
using Gumbug.Parzival.UnityObject;
using Gumbug.Senso.Model.Config;
using UnityEngine;

namespace Gumbug.Outpost.Utils
{
    public static class MercenaryViewUtils
    {
	    public const int UIRenderLayer = 12;
	    
        public static Animator CreateSecondaryAnimator(string prefabName, Transform parent)
        {
            var secondaryAnimatorController = AssetManager.LoadFromBundleAndPath<RuntimeAnimatorController>("mercenaries", $"Assets/Outpost/Mercenaries/{prefabName}/SecondaryAnimations/{prefabName}_secondary.overrideController");
            if (secondaryAnimatorController != null)
            {
                var secondaryAnimatorGameObject = new GameObject();
                secondaryAnimatorGameObject.transform.SetParent(parent, false);
                var secondaryAnimator = secondaryAnimatorGameObject.AddComponent<Animator>();
                secondaryAnimator.applyRootMotion = false;
                secondaryAnimator.runtimeAnimatorController = secondaryAnimatorController;
                secondaryAnimatorGameObject.AddComponent<SecondaryAnimatorListener>();
                return secondaryAnimator;
            }

            return null;
        }

	    public static MercenaryAnimationView CreateMercenaryAsset(string prefabName, Transform parentTransform, int specialType)
	    {
		    var mercenaryAnimationViewGameObject = new GameObject("MercenaryAnimationView");
		    mercenaryAnimationViewGameObject.transform.SetParent(parentTransform);
		    mercenaryAnimationViewGameObject.transform.localPosition = Vector3.zero;
		    mercenaryAnimationViewGameObject.transform.localScale = Vector3.one;
		    mercenaryAnimationViewGameObject.transform.localRotation = Quaternion.identity;
		    var mercenaryAnimationView = mercenaryAnimationViewGameObject.AddComponent<MercenaryAnimationView>();

//		    var highPoly = CreateGameAsset(prefabName, mercenaryAnimationView.transform, specialType, true);
		    var lowPoly = CreateGameAsset(prefabName, mercenaryAnimationView.transform, specialType, false);

//		    mercenaryAnimationView.SetHighPoly(highPoly);
		    mercenaryAnimationView.SetLowPoly(lowPoly);

		    return mercenaryAnimationView;
	    }

        
        public static GameObject CreateGameAsset(string prefabName, Transform parentTransform, int specialType, bool isHighPoly)
        {
            var asset = CreateGameAssetRaw(prefabName, parentTransform, specialType, isHighPoly);
            asset.transform.localRotation = Quaternion.identity;
            asset.transform.localScale = Vector3.one;
            var skinnedMeshRenderers = asset.GetComponentsInChildren<SkinnedMeshRenderer>();
            for (var index = 0; index < skinnedMeshRenderers.Length; index++)
            {
                var skinnedMeshRenderer = skinnedMeshRenderers[index];
                skinnedMeshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                skinnedMeshRenderer.skinnedMotionVectors = false;
                skinnedMeshRenderer.receiveShadows = false;
            }

            return asset;
        }
	    
	    public static GameObject CreateGameAssetRaw(string prefabName, Transform parentTransform, int specialType, bool isHighPoly)
	    {
		    var polySubPath = isHighPoly ? "High" : "Low";
		    var assetPath = $"Assets/Outpost/Mercenaries/{prefabName}/{polySubPath}/{prefabName}@asset.fbx";
		    var assetPrefab = AssetManager.LoadFromBundleAndPath<GameObject>(AssetBundleNames.Mercenaries, assetPath);
		    var instance = GameObject.Instantiate(assetPrefab);
		    instance.transform.SetParent(parentTransform);
		    instance.transform.localPosition = Vector3.zero;

		    var controllerName = prefabName;

		    var animatorController = AssetManager.LoadFromBundleAndPath<RuntimeAnimatorController>(AssetBundleNames.Mercenaries, $"Assets/Outpost/Mercenaries/{prefabName}/{controllerName}.overrideController");
		    var animator = instance.GetComponent<Animator>();
		    animatorController.name = controllerName;
		    animator.runtimeAnimatorController = animatorController;
		    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

			animator.SetInteger(AnimatorParameters.SpecialType, specialType);
			
		    return instance;
	    }
        
        public static GameObject CreateMenuAsset(string prefabName, Transform parentTransform, int specialType)
        {
            var assetPrefab = AssetManager.LoadFromBundleAndPath<GameObject>(AssetBundleNames.Mercenaries, $"Assets/Outpost/Mercenaries/{prefabName}/High/{prefabName}@asset.fbx");
            var instance = GameObject.Instantiate(assetPrefab);
            instance.transform.SetParent(parentTransform);
            instance.transform.localPosition = Vector3.zero;

            var controllerName = $"{prefabName}_menu";

            var animatorController = AssetManager.LoadFromBundleAndPath<RuntimeAnimatorController>(AssetBundleNames.Mercenaries, $"Assets/Outpost/Mercenaries/{prefabName}/{controllerName}.overrideController");
            var animator = instance.GetComponent<Animator>();
            animatorController.name = controllerName;
            animator.runtimeAnimatorController = animatorController;
			
            return instance;
        }
        
        public static List<GameObject> CreateWeapons(CMWeapon cmWeapon, GameObject owner)
		{
			var cmWeaponPrefabId = cmWeapon.PrefabId;
			var weapons = new List<GameObject>();
			
			if (string.IsNullOrEmpty(cmWeaponPrefabId))
				return weapons;

			var prefab = AssetManager.LoadFromBundleAndPath(AssetBundleNames.Weapons, WeaponsPathUtils.WeaponPrefabPath(cmWeaponPrefabId));
			if (prefab == null)
			{
				Debug.LogError($"Cannot find weapon: {cmWeaponPrefabId}");
				return weapons;
			}

			var locator = cmWeapon.WeaponLocator;
			
			if (cmWeapon.Handedness == "right" || cmWeapon.Handedness == "both")
				weapons.Add(CreateWeaponForHand(owner, $"char:Rhandle_{locator}_LOC", prefab));
			if (cmWeapon.Handedness == "left" || cmWeapon.Handedness == "both")
				weapons.Add(CreateWeaponForHand(owner, $"char:Lhandle_{locator}_LOC", prefab));
			
			return weapons;
		}

	    public static List<WeaponEntry> CreateWeaponsFromPool(CMWeapon cmWeapon, Dictionary<string, Transform> leftHandLocators, Dictionary<string, Transform> rightHandLocators)
		{
			var cmWeaponPrefabId = cmWeapon.PrefabId;
			var weapons = new List<WeaponEntry>();
			
			if (string.IsNullOrEmpty(cmWeaponPrefabId))
				return weapons;
			
			var locator = cmWeapon.WeaponLocator;

			var manager = PoolingManager.GetPoolManager(cmWeaponPrefabId);
			if (cmWeapon.Handedness == "right" || cmWeapon.Handedness == "both")
			{
				var rightHandWeapon = CreateWeaponForHandFromPool(locator, rightHandLocators[locator], manager, true);
				if (rightHandWeapon != null)
					weapons.Add(rightHandWeapon);
			}
			if (cmWeapon.Handedness == "left" || cmWeapon.Handedness == "both")
			{
				var leftHandWeapon = CreateWeaponForHandFromPool(locator, leftHandLocators[locator], manager, false);
				if (leftHandWeapon != null)
					weapons.Add(leftHandWeapon);
			}
			
			return weapons;
		}

		private static WeaponEntry CreateWeaponForHandFromPool(string locatorName, Transform weaponLocator, PoolManager manager, bool isRightHand)
		{
			if (weaponLocator == null)
				return null;
			
			var weaponInstance = (WeaponView) manager.Get();
			weaponInstance.transform.SetParent(weaponLocator);
			weaponInstance.transform.localPosition = Vector3.zero;
			weaponInstance.transform.localScale = Vector3.one;
			weaponInstance.transform.localRotation = Quaternion.identity;

			var renderers = weaponInstance.GetComponentsInChildren<Renderer>();
			for (int i = 0; i < renderers.Length; i++)
			{
				renderers[i].enabled = true;
				renderers[i].sharedMaterial.DisableKeyword(ShaderKeywords.OutpostItemGlowOn);
			}

			return new WeaponEntry(weaponInstance, manager, weaponLocator, isRightHand, locatorName);
		}

		private static GameObject CreateWeaponForHand(GameObject owner, string locatorName, GameObject prefab)
		{
			var weaponInstance = GameObject.Instantiate(prefab);
			var weaponLocator = owner.transform.FindRecursive(locatorName);

			weaponInstance.transform.SetParent(weaponLocator);
			weaponInstance.transform.localPosition = Vector3.zero;
			weaponInstance.transform.localScale = Vector3.one;
			weaponInstance.transform.localRotation = Quaternion.identity;

			return weaponInstance;
		}
	    
	    public static string GetFullLeftLocatorName(string locatorName)
	    {
		    return $"char:Lhandle_{locatorName}_LOC";
	    }

	    public static string GetFullRightLocatorName(string locatorName)
	    {
		    return $"char:Rhandle_{locatorName}_LOC";
	    }
	    
	    public static string[] LocatorNames = {
		    "pistol",
		    "rifle",
		    "grenade",
		    "bazooka",
		    "laser",
		    "melee",
		    "bow"
	    };

	    public static GameObject CreateInGameParachuteView(string parachuteId)
	    {
		    return CreateParachute(parachuteId, CreateInGameParachuteMaterial(parachuteId));
	    }

	    public static GameObject CreateMenuParachute(string parachuteId)
	    {
		    var menuParachute = CreateParachute(parachuteId, CreateMenuParachuteMaterial(parachuteId));
		    menuParachute.layer = UIRenderLayer;

		    return menuParachute;
	    }

	    private static GameObject CreateParachute(string parachuteId, Material parachuteMaterial)
	    {
		    var parachuteGameObject = CreateParachute(parachuteId);
		    var meshRenderer = parachuteGameObject.GetComponentInChildren<MeshRenderer>();
		    meshRenderer.sharedMaterial = parachuteMaterial;
		    return parachuteGameObject;
	    }

	    private static GameObject CreateParachute(string parachuteId)
	    {
		    return GameObject.Instantiate(AssetManager.LoadFromBundleAndPath<GameObject>(AssetBundleNames.Parachutes, $"Assets/Outpost/Parachutes/{parachuteId}/{parachuteId}.prefab"));
	    }

	    private static Material CreateInGameParachuteMaterial(string parachuteId)
	    {
		    return AssetManager.LoadFromBundleAndPath<Material>(AssetBundleNames.Parachutes, $"Assets/Outpost/Parachutes/{parachuteId}/Materials/{parachuteId}_LOD1.mat");
	    }

	    private static Material CreateMenuParachuteMaterial(string parachuteId)
	    {
		    return AssetManager.LoadFromBundleAndPath<Material>(AssetBundleNames.Parachutes, $"Assets/Outpost/Parachutes/{parachuteId}/Materials/{parachuteId}.mat");
	    }
    }
}
