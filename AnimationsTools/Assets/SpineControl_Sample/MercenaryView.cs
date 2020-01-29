using System;
using System.Collections.Generic;
using DefaultNamespace;
using Gumbug.Outpost.Audio;
using Gumbug.Outpost.Game.Camera;
using Gumbug.Outpost.Game.View.UI;
using Gumbug.Outpost.Model.Config;
using Gumbug.Outpost.UI.View;
using Gumbug.Outpost.Utils;
using Gumbug.Parzival.Input;
using Gumbug.Parzival.Pooling;
using Gumbug.Parzival.UI;
using Gumbug.Parzival.UnityObject;
using Gumbug.Senso.Math;
using Gumbug.Senso.Model;
using Gumbug.Senso.Model.Config;
using Gumbug.Senso.Physics;
using UnityEngine;
using SensoGame = Gumbug.Senso.Model.Game;

namespace Gumbug.Outpost.Game.View
{
	public class MercenaryView : EntityView
	{
		[SerializeField] private GameObject primaryReticleGameObject;
		[SerializeField] private GameObject speciaReticleGameObject;
		[SerializeField] private GameObject flareReticleGameObject;
		[SerializeField] private ReticleContainerView reticleContainerView;
		[SerializeField] private GameObject shadowGameObject;
		[SerializeField] private Renderer teamHelperRenderer;
		[SerializeField] private Texture2D team0Texture;
		[SerializeField] private Texture2D team1Texture;
		[SerializeField] private Texture2D teamHelperSoloTexture;
		[SerializeField] private Transform mercenaryAssetContainer;
		[SerializeField] private MercenaryParachuteView mercenaryParachuteView;
		[SerializeField] private Projector teamHelperProjectorRenderer;
		[SerializeField] private GameObject shadowGameProjector;
		[SerializeField] private FootstepController footstepController;

		private static GameObject mercenaryStatusPrefabGameObject;
		private MuzzleFlashContainerView muzzleFlashContainer;
		
		public bool IsUserMercenaryView { get; private set; }

		private bool ShowAsSecondaryTeam
		{
			get
			{
				var showAsSecondaryTeam = false;
				if (GameController.Instance.GameUserMode == GameUserMode.Player)
					showAsSecondaryTeam = !GameController.Instance.IsUserMercenaryTeam(Mercenary.TeamIndex);
				else
				{
					switch (GameController.Instance.GameMode)
					{
						case 1: // Supply
						case 3: // CTF
							showAsSecondaryTeam = Mercenary.TeamIndex == 1;
							break;
						case 2: // Survival
							showAsSecondaryTeam = true;
							break;
					}
				}

				return showAsSecondaryTeam;
			}
		}
		
		public bool HasDeathBet { get; private set; }
		
		public Mercenary Mercenary { get; private set; }
	    public MercenaryStatusView MercenaryStatusView => mercenaryStatusView;
		
		private MercenaryStatusView mercenaryStatusView;
		private MercenaryAnimationView mercenaryAnimationView;
		private IReticleView primaryWeaponReticleView;
		private IReticleView specialWeaponReticleView;
		private IReticleView flareWeaponReticleView;

		private int statusViewVisibilityCheckCounter;
		private bool teamHelperHidden;
		private List<WeaponEntry> primaryWeapons;
		private List<WeaponEntry> stashedWeapons;
		private AuraView aura;
		private bool entityOnUpdatedCalled;
		private Transform weaponParent;
		private ShieldView shield;
		private float fireAngle;
		private bool primaryWeaponReloaded;
		private bool inCover;
		private bool isMeleeWeapon;
		private float intervalTimer;
		private Texture2D depthMap;
		private int depthMapSize;
		private int depthMapPixelsPerTile;
	    
	    public bool IsInVehicle => currentVehicleView != null;
	    private VehicleView currentVehicleView;
	    private Transform preEnterVehicleParentTransform;
		private IReticleView activeReticleView;
		private bool firing;
		
		public bool TeamProcessed { get; set; }

		public void InitMercenary(Entity entity, SensoGame game)
        {
            Init(entity, null);

	        Mercenary = (Mercenary) entity;
            Mercenary.EnergyUpdated += MercenaryOnEnergyUpdated;
            Mercenary.Respawned += MercenaryOnRespawned;
            Mercenary.EffectStateChanged += MercenaryOnEffectStateChanged;
            Mercenary.TeamChanged += MercenaryOnTeamChanged;
            Mercenary.WeaponChanged += MercenaryOnWeaponChanged;
	        Mercenary.AmmoAdded += MercenaryOnAmmoAdded;
	        Mercenary.SpecialAmmoAdded += MercenaryOnSpecialAmmoAdded;
	        
            var mercenaryWeapon = Mercenary.Weapon;
            mercenaryWeapon.Updated += WeaponOnUpdated;

	        if (GameController.Instance.GameUserMode == GameUserMode.Player)
		        IsUserMercenaryView = GameController.Instance.IsUserMercenary(Mercenary);
	        else
		        IsUserMercenaryView = false;

            mercenaryAnimationView = MercenaryViewUtils.CreateMercenaryAsset(Mercenary.CmClass.PrefabId, mercenaryAssetContainer, Mercenary.SpecialWeapon.CmWeapon.SpecialAnimatorType);
				
	        RefreshTeam();
	        RefreshMercenaryStatusView();

            primaryWeaponReticleView?.Disable();
            specialWeaponReticleView?.Disable();
	        flareWeaponReticleView?.Disable();

	        if (IsUserMercenaryView)
	        {
		        reticleContainerView.Enable();
		        var flareWeapon = new Weapon(game, -1, ConfigManager.ConfigModel.GetWeaponById("flare_launcher"), Mercenary, true);
		        bool primaryIsBallistic;
		        primaryWeaponReticleView = SelectWeaponReticle(Mercenary.Weapon, primaryReticleGameObject, out primaryIsBallistic);
		        bool specialIsBallistic;
		        specialWeaponReticleView = SelectWeaponReticle(Mercenary.SpecialWeapon, speciaReticleGameObject, out specialIsBallistic);
		        bool flareIsBallistic;
		        flareWeaponReticleView = SelectWeaponReticle(flareWeapon, flareReticleGameObject, out flareIsBallistic);
		        
		        if (primaryIsBallistic)
			        InputUtils.PrimaryXZPlane = new Plane(Vector3.up, new Vector3(0, 0, 0));
		        else
			        InputUtils.PrimaryXZPlane = new Plane(Vector3.up, new Vector3(0, Mercenary.Weapon.CmWeapon.ProjectileOffset[1], 0));
		        
		        if (specialIsBallistic)
			        InputUtils.SpecialXZPlane = new Plane(Vector3.up, new Vector3(0, 0, 0));
		        else
			        InputUtils.SpecialXZPlane = new Plane(Vector3.up, new Vector3(0, Mercenary.SpecialWeapon.CmWeapon.ProjectileOffset[1], 0));
	        }
	        
			var muzzleFlashContainerGo = new GameObject("MuzzleFlashContainer");
			muzzleFlashContainer = muzzleFlashContainerGo.AddComponent<MuzzleFlashContainerView>();
			muzzleFlashContainerGo.transform.SetParent(transform);
			muzzleFlashContainerGo.transform.localPosition = Vector3.zero;

	        primaryWeapons = new List<WeaponEntry>();
	        stashedWeapons = new List<WeaponEntry>();
	        
	        teamHelperProjectorRenderer.gameObject.SetActive(false);
	        shadowGameProjector.SetActive(false);

	        isMeleeWeapon = true;


	        string parachutePrefabId = "default";
	        if (!string.IsNullOrEmpty(Mercenary.ParachuteId))
		        parachutePrefabId = ConfigManager.ConfigModel.GetParachuteById(Mercenary.ParachuteId).PrefabId;

	        mercenaryParachuteView.AddParachuteModel(MercenaryViewUtils.CreateInGameParachuteView(parachutePrefabId));
        }

	    private void CreateMuzzleFlash(string prefabId, float[] offset, bool isSpecial)
        {
	        muzzleFlashContainer.RemoveFlash(isSpecial);
	        
            if (string.IsNullOrEmpty(prefabId))
                return;

			muzzleFlashContainer.AddFlash(prefabId, isSpecial, offset);
        }

        public void RefreshTeam()
        {
	        var isUserTeam = GameController.Instance.IsUserMercenaryTeam(Mercenary.TeamIndex);

            if (Mercenary.TeamIndex == 255)
            {
                teamHelperHidden = true;
                teamHelperRenderer.gameObject.SetActive(!teamHelperHidden);
            }
            else
            {
	            switch (GameController.Instance.GameMode)
	            {
		            case 0: // None
			            break;
                    case 1: // Supply
			            switch (GameController.Instance.GameUserMode)
			            {
				            case GameUserMode.Player:
				            case GameUserMode.Ghost:
					            teamHelperRenderer.material.mainTexture = isUserTeam ? team0Texture : team1Texture;
					            teamHelperProjectorRenderer.material.mainTexture = isUserTeam ? team0Texture : team1Texture;
					            teamHelperHidden = false;
					            teamHelperRenderer.gameObject.SetActive(!teamHelperHidden);
					            break;
				            case GameUserMode.Observer:
					            teamHelperRenderer.material.mainTexture = Mercenary.TeamIndex == 0 ? team0Texture : team1Texture;
					            teamHelperProjectorRenderer.material.mainTexture = Mercenary.TeamIndex == 0 ? team0Texture : team1Texture;
					            teamHelperHidden = false;
					            teamHelperRenderer.gameObject.SetActive(!teamHelperHidden);
					            break;
			            }
			            break;
		            case 2: // Survival
			            teamHelperHidden = true;
			            teamHelperRenderer.gameObject.SetActive(!teamHelperHidden);
			            break;
                    case 3: // Capture The Flag
                        break;
		            case 4: // Tutorial
			            teamHelperHidden = !isUserTeam;
			            teamHelperRenderer.gameObject.SetActive(!teamHelperHidden);
			            teamHelperRenderer.material.mainTexture = teamHelperSoloTexture;
			            teamHelperProjectorRenderer.material.mainTexture = teamHelperSoloTexture;
			            break;
		            default:
			            throw new ArgumentOutOfRangeException();
	            }
            }
        }

		public void RefreshMercenaryStatusView()
		{
			var gameMode = GameController.Instance.GameMode;

			// @TODO Refactor, pooling
			if (mercenaryStatusView == null)
			{
				if (mercenaryStatusPrefabGameObject == null)
					mercenaryStatusPrefabGameObject = Resources.Load<GameObject>("Prefabs/BattleUI/Universal/MercenaryStatus");
					
				var mercenaryStatusGameObject = Instantiate(mercenaryStatusPrefabGameObject);
				mercenaryStatusGameObject.name = $"MercenaryStatus_{Entity}";
				mercenaryStatusView = mercenaryStatusGameObject.GetComponent<MercenaryStatusView>();
				mercenaryStatusGameObject.transform.SetParent(GameUIView.Instance.MercenaryStatusContainerRectTransform, false);
				mercenaryStatusView.Init(mercenaryStatusView.gameObject.name, null, this);
				mercenaryStatusView.SetUserNameText(Mercenary.Name);
			    
			    if (IsUserMercenaryView)
			        mercenaryStatusView.ShowAmmo();
			    else
			        mercenaryStatusView.HideAmmo();
			}
			
			mercenaryStatusView.UpdateVisiblity(true, IsUserMercenaryView, gameMode == 1, gameMode == 2, true, ShowAsSecondaryTeam);
		}

		private IReticleView SelectWeaponReticle(Weapon mercenaryWeapon, GameObject reticleGameObject, out bool isBallistic)
		{
			IReticleView reticleView;

			isBallistic = false;

			var projectileById = !string.IsNullOrEmpty(mercenaryWeapon.CmWeapon.ProjectileId) ? ConfigManager.ConfigModel.GetProjectileById(mercenaryWeapon.CmWeapon.ProjectileId) : null;

			if (projectileById != null)
			{
				if (projectileById.SpeedY > 0)
				{
					reticleView = reticleGameObject.GetComponent<BallisticArcReticleView>();
					isBallistic = true;
				}
				else
				{
					reticleView = reticleGameObject.GetComponent<StraightReticleView>();
					((StraightReticleView) reticleView).SetParams(projectileById.Range, mercenaryWeapon.CmWeapon.ProjectileOffset);
				}
			}
			else
			{
				reticleView = reticleGameObject.GetComponent<StraightReticleView>();
				((StraightReticleView) reticleView).SetParams(mercenaryWeapon.CmWeapon.MeleeRange, mercenaryWeapon.CmWeapon.ProjectileOffset);
			}
			
			reticleView?.Reset();
			
			return reticleView;
		}

		protected override void InitReset()
		{
			base.InitReset();
			
			Mercenary.EnergyUpdated -= MercenaryOnEnergyUpdated;
			Mercenary.Respawned -= MercenaryOnRespawned;
			Mercenary.EffectStateChanged -= MercenaryOnEffectStateChanged;
			Mercenary.TeamChanged -= MercenaryOnTeamChanged;
		    Mercenary.WeaponChanged -= MercenaryOnWeaponChanged;
			Mercenary.AmmoAdded -= MercenaryOnAmmoAdded;
			Mercenary.SpecialAmmoAdded -= MercenaryOnSpecialAmmoAdded;

			Mercenary.Weapon.Updated -= WeaponOnUpdated;

			IsUserMercenaryView = false;

			Mercenary = null;
		}

		public override void Show()
		{
			base.Show();
            mercenaryStatusView.UpdatePosition();
			mercenaryAnimationView.Show();
			ShowWeapons();
            if (!Mercenary.Grounded)
                mercenaryParachuteView.Show();
            else
                mercenaryParachuteView.Hide();
            muzzleFlashContainer?.Enable();
			
			shadowGameObject.SetActive(Mercenary.Grounded);
			shadowGameProjector.SetActive(!Mercenary.Grounded);
			
			if (aura != null)
				aura.Show();
			if (shield != null)
				shield.Show();
			if (!teamHelperHidden)
				ShowTeamHelper();
			ShowStatusView();
		}

		public override void Hide()
		{
			base.Hide();
            mercenaryAnimationView.Hide();
			mercenaryParachuteView.Hide();
			HideWeapons();
            muzzleFlashContainer?.Disable();
			shadowGameObject.SetActive(false);
			shadowGameProjector.SetActive(false);
			HideTeamHelper();
			HideStatusView();
			if (aura != null)
				aura.Hide();
			if (shield != null)
				shield.Hide();
			if (!IsUserMercenaryView) return;
			primaryWeaponReticleView?.Enable();
			specialWeaponReticleView?.Disable();
			flareWeaponReticleView?.Disable();
			HideFireHelper();
		}

		private void ShowWeapons()
		{
			for (int i = 0; i < primaryWeapons.Count; i++)
			{
				var weaponRenderers = primaryWeapons[i].Renderers;
				for (int j = 0; j < weaponRenderers.Length; j++)
					weaponRenderers[j].enabled = true;
			}
		}

		private void HideWeapons()
		{
			for (int i = 0; i < primaryWeapons.Count; i++)
			{
				var weaponRenderers = primaryWeapons[i].Renderers;
				for (int j = 0; j < weaponRenderers.Length; j++)
					weaponRenderers[j].enabled = false;
			}
		}

		public void ShowStatusView()
		{
			mercenaryStatusView.Show();
		}
		
		public void HideStatusView()
		{
			mercenaryStatusView.Hide();
		}

		public void ShowTeamHelper()
		{
			teamHelperRenderer.gameObject.SetActive(Mercenary.Grounded);
			teamHelperProjectorRenderer.gameObject.SetActive(!Mercenary.Grounded);
		}

		public void HideTeamHelper()
		{
			teamHelperRenderer.gameObject.SetActive(false);
			teamHelperProjectorRenderer.gameObject.SetActive(false);
		}
				
		public void ShowFireHelper(Vector3 firePosition, FireHelperType fireHelperType)
		{
			if (!Mercenary.Grounded)
			{
				primaryWeaponReticleView?.Disable();
				specialWeaponReticleView?.Disable();
				flareWeaponReticleView?.Disable();
				return;
			}

			CMWeapon cmWeapon;

			switch (fireHelperType)
			{
				case FireHelperType.Default:
					cmWeapon = Mercenary.Weapon.CmWeapon;
					primaryWeaponReticleView?.Enable();
					specialWeaponReticleView?.Disable();
					flareWeaponReticleView?.Disable();
					activeReticleView = primaryWeaponReticleView;
					break;
				case FireHelperType.Special:
					cmWeapon = Mercenary.SpecialWeapon.CmWeapon;
					primaryWeaponReticleView?.Disable();
					specialWeaponReticleView?.Enable();
					flareWeaponReticleView?.Disable();
					activeReticleView = specialWeaponReticleView;
					break;
				case FireHelperType.Flare:
					cmWeapon = ConfigManager.ConfigModel.GetWeaponById("flare_launcher");
					primaryWeaponReticleView?.Disable();
					specialWeaponReticleView?.Disable();
					flareWeaponReticleView?.Enable();
					activeReticleView = flareWeaponReticleView;
					break;
				default:
					return;
			}
			
			ReticleUtils.SetFireState(firing ? ReticleState.Firing : ReticleState.Aiming);

			var fireVector = firePosition - Mercenary.Collider.Position.ToVector3();
			fireAngle = Mathf.Atan2(fireVector.x, fireVector.z) * Mathf.Rad2Deg;

			reticleContainerView.Enable();
			reticleContainerView.SetAngle(fireAngle);
			
			if (shield != null)
				shield.UpdateShield(Quaternion.Euler(0, fireAngle - 90f, 0));

			var ballisticArcReticleView = activeReticleView as BallisticArcReticleView;
			if (ballisticArcReticleView == null) return;

		    if (cmWeapon != null)
		    {
		        var cmProjectile = cmWeapon.Projectile;

		        var explosionRadius = FindExplosionRadiusInWeapon(cmWeapon);

		        var range = FloatUtils.Min(fireVector.magnitude, cmProjectile.Range);
		        var speed = Ballistics.GetSpeedForRange(range, cmProjectile.Gravity, cmWeapon.ProjectileOffset[1], new Point(cmProjectile.SpeedXZ, cmProjectile.SpeedY, 0));
		        ballisticArcReticleView.SetParams(cmProjectile.Gravity, explosionRadius, cmWeapon.ProjectileOffset[1], new Vector2(speed.X, speed.Y));
		    }
		    else
		        Debug.LogWarning($"[MercenaryView.ShowFireHelper] null cmWeapon with fireHelperType {fireHelperType}");
		}

		private float FindExplosionRadiusInWeapon(CMWeapon weapon)
        {
            if (weapon.HasExpireEffect)
                return FindExplosionRadiusInEffect(weapon.ExpireEffect);
            
            if (weapon.HasProjectile)
                return FindExplosionRadiusInProjectile(weapon.Projectile);
            
	        return 0.25f;
        }

		private float FindExplosionRadiusInEffect(CMEffect effect)
        {
            if (effect.HasDeployWeapon)
                return FindExplosionRadiusInWeapon(effect.DeployWeapon);
            
            if (effect.HasExplosion)
                return effect.Explosion.EffectRadius;
            
	        return 0.25f;
        }

		private float FindExplosionRadiusInProjectile(CMProjectile projectile)
        {
            if (projectile.HasExpireEffect)
                return FindExplosionRadiusInEffect(projectile.ExpireEffect);
            
            return 0;
        }

        public void HideFireHelper()
        {
	        activeReticleView = null;
	        firing = false;
	        reticleContainerView.Disable();
        }

		public void ShowEnergyUpdateView(int damage, bool armor, bool nearDeath, bool critical)
		{
			DamageTextCount++;

			if (DamageTextCount > MaxDamageTextCount)
				DamageTextCount = 0;
			
			GameView.Instance.ShowEnergyUpdateView(this, damage, nearDeath, critical, armor);
		}
				
		public void ShowDeathBetMarker(string userName, uint deathTime)
		{
			HasDeathBet = true;
			mercenaryStatusView.ShowDeathBetMarker(userName, deathTime);
		}
		
		public void HideDeathBetMarker()
		{
			HasDeathBet = false;
			mercenaryStatusView.HideDeathBetMarker();
		}

		public void PlayFireAnimation(float angle, bool specialWeapon)
		{	
			angle = GameController.Instance.ProcessMirrorAngle(angle);

			var shootAngle = 270 - angle;
			if (!specialWeapon)
			{
				if (primaryWeapons.Count > 0)
				{
					mercenaryAnimationView.ShootPrimary(shootAngle);
					muzzleFlashContainer?.SetAngle(shootAngle);
					muzzleFlashContainer?.Fire(false);
				}
				else
				{
					mercenaryAnimationView.PunchPrimary(shootAngle);
				}

				var cmWeaponTriggerAudioIds = Mercenary.Weapon.CmWeapon.TriggerAudioIds;
				if (cmWeaponTriggerAudioIds != null && !Hidden && intervalTimer <= 0f)
					OutpostAudioManager.PlayGameEffect(cmWeaponTriggerAudioIds, transform.position, null, IsUserMercenaryView ? 0x100 : -1);
				
				if (IsUserMercenaryView && intervalTimer <= 0f)
					GameCamera.Instance.Shake(Mercenary.Weapon.CmWeapon.ShakeAmount);
			}
			else
			{
				mercenaryAnimationView.ShootSpecial(shootAngle);
				muzzleFlashContainer?.SetAngle(shootAngle);
				muzzleFlashContainer?.Fire(true);
				
				if (IsUserMercenaryView && intervalTimer <= 0f)
					GameCamera.Instance.Shake(Mercenary.SpecialWeapon.CmWeapon.ShakeAmount);
			}
			
			if (isMeleeWeapon)
			{
				if (intervalTimer <= 0f)
					intervalTimer = 0.2f;
			}
		}
		
		protected override void EntityOnUpdated(Entity entity)
		{
			base.EntityOnUpdated(entity);
			
			if (entityOnUpdatedCalled)
				return;

			entityOnUpdatedCalled = true;

			if (mercenaryStatusView == null || mercenaryStatusView.gameObject.Equals(null)) return;

			var mercenary = (Mercenary) entity;

		    if (IsUserMercenaryView)
		    {
			    mercenaryStatusView.UpdateMercenaryHealth(mercenary.Energy, mercenary.MaxEnergy);
			    mercenaryStatusView.UpdateMercenaryArmor(mercenary.Armor, mercenary.MaxArmor);
			    mercenaryStatusView.UpdateSupplyItemCounter(mercenary.Supplies);
			    mercenaryStatusView.UpdateBatteryItemCounter(mercenary.BatteryNumericalValue);
			    
		        switch (mercenary.Weapon.CmWeapon.Calibre)
		        {
		            case 0:
			            if (mercenary.ReserveWeapon == null)
		                	mercenaryStatusView.HideAmmo();
			            else
			            {
				            var reserveWeaponCalibre = mercenary.ReserveWeapon.CmWeapon.Calibre;
				            mercenaryStatusView.UpdateCurrentWeaponAmmo(mercenary.GetAmmo(reserveWeaponCalibre), 
					            reserveWeaponCalibre);
			            }
		                break;
		            case 1:
		                MercenaryStatusView.UpdateCurrentWeaponAmmo(mercenary.GetAmmo(1), mercenary.Weapon.CmWeapon.Calibre);
		                break;
		            case 2:
		                MercenaryStatusView.UpdateCurrentWeaponAmmo(mercenary.GetAmmo(2), mercenary.Weapon.CmWeapon.Calibre);
		                break;
		            case 3:
		                MercenaryStatusView.UpdateCurrentWeaponAmmo(mercenary.GetAmmo(3), mercenary.Weapon.CmWeapon.Calibre);
		                break;
		        }
		    }
		    else
		    {
                // if (mercenaryStatusView.gameObject.activeSelf)
                //    mercenaryStatusView.Hide();
		    }

			if (Hidden || !Mercenary.Grounded) return;

			mercenaryAnimationView.DoTheLocomotion(Entity.TargetLinearSpeed);
			var isInCover = GameView.Instance.GetTile(Mercenary.TileX, Mercenary.TileZ);
			if (isInCover != inCover)
			{
				if (isInCover)
					mercenaryAnimationView.InCover();
				else
					mercenaryAnimationView.OutCover();
			}
			inCover = isInCover;
		}

		private void LateUpdate()
		{
			entityOnUpdatedCalled = false;
		}

		private void MercenaryOnEnergyUpdated(Mercenary mercenary, float energy)
		{			
		}

        private void MercenaryOnTeamChanged(Mercenary mercenary, byte oldTeam, byte newTeam)
        {
            RefreshTeam();
            RefreshMercenaryStatusView();
        }
	    
	    private void MercenaryOnWeaponChanged(Mercenary mercenary, Weapon oldWeapon, Weapon weapon, bool special, bool fromReserve, bool toReserve, byte weaponTeamIndex)
	    {
	        ChangeWeapon(weapon.CmWeapon, fromReserve, toReserve, weaponTeamIndex);
	    }

		private void MercenaryOnAmmoAdded(Mercenary mercenary, int ammoAmount, int calibre)
		{
			if (IsUserMercenaryView)
				mercenaryStatusView.OnAmmoAdded(ammoAmount, calibre);
		}

		private void MercenaryOnSpecialAmmoAdded(Mercenary mercenary, float ammoAmount)
		{
			if (IsUserMercenaryView)
				mercenaryStatusView.OnSpecialAmmo(ammoAmount);
		}

        public void ProcessDead()
        {
            Hide();

			if (GameCamera.IsWorldPositionVisible(transform.position))
	        {
		        var mercenaryDeath = (MercenaryDeathView) PoolingManager.Get("mercenary_death");
		        mercenaryDeath.Init(transform.position);
	        }

			mercenaryAnimationView.CancelSpecial();

			HideInteraction();

			muzzleFlashContainer.Disable();

			// Remove weapons
			RemoveAllWeapons();
	        mercenaryAnimationView.UnregisterWeapons(stashedWeapons);
	        mercenaryAnimationView.UnregisterWeapons(primaryWeapons);
	        primaryWeapons.Clear();
			//

			// @TODO Hack, this method needs to be refactored for respawning to work
			if (GameController.Instance.GameMode == 3)
				return;
	        
	        mercenaryAnimationView.DisableRendering();
	        
	        if (aura != null)
		        aura.ReturnToPool();
	        aura = null;
	        
	        RemoveShield();
	        
	        RemoveAllWeapons();
	        mercenaryAnimationView.UnregisterWeapons(stashedWeapons);
	        mercenaryAnimationView.UnregisterWeapons(primaryWeapons);
	        primaryWeapons.Clear();
        }

		public void ProcessGameEnd(GameResult gameResult)
		{
			mercenaryAnimationView.DoTheLocomotion(0f);
		}

        private void MercenaryOnRespawned(Mercenary mercenary)
		{
			muzzleFlashContainer.Disable();
			mercenaryAnimationView.Reset();
			mercenaryStatusView.UpdatePosition();
		}
		
		private void MercenaryOnEffectStateChanged(Mercenary mercenary, string effect, bool state, float duration)
		{
		    duration = duration / GameController.Instance.ServerUpdateFrequency;
		    
			if (GameController.Instance.IsUserMercenary(mercenary.Id))
			{
				switch (effect)
				{
					case "confusion":
						if (state)
						    UIManager.ShowVignetteEffect(VignetteEffect.Confusion, duration);
						else
							UIManager.HideVignette();
						
						break;
					case "stun":
						if (state)
						{
							mercenaryAnimationView.Stunned(true);
							UIManager.ShowVignetteEffect(VignetteEffect.Stun, duration);
						}
						else
						{
							mercenaryAnimationView.Stunned(false);
							UIManager.HideVignette();
						}
						break;
					case "invulnerability":
						if (state)
							mercenaryAnimationView.EnableGlowPulse(Color.white);
						else
							mercenaryAnimationView.DisableGlowPulse();

						break;
					default:
						if (!state)
							UIManager.HideVignette();
						
						break;
				}
			}
			else
			{
				switch (effect) {
					case "invulnerability":
					{
						if (state)
							mercenaryAnimationView.EnableGlowPulse(Color.white);
						else
							mercenaryAnimationView.DisableGlowPulse();
						break;
					}
					case "stun":
						mercenaryAnimationView.Stunned(state);
						break;
				}
			}
		}

		public void ShowDamage()
		{
			mercenaryAnimationView.EnableGlowFlash(Color.red, 0.2f);
		}
		
		public void ShowHeal()
		{
			mercenaryAnimationView.EnableGlowFlash(Color.yellow, 0.2f);
		}
		
		private void WeaponOnUpdated(Entity entity)
		{
			if (mercenaryStatusView == null || mercenaryStatusView.gameObject.Equals(null)) return;

			if (IsUserMercenaryView)
			{
				if (Mercenary.Weapon.Energy >= 1)
				{
					if (!primaryWeaponReloaded)
					{
						primaryWeaponReloaded = true;
						OutpostAudioManager.PlayGameEffect("reload_full", transform.position);
					}
				}
				else
				{
					primaryWeaponReloaded = false;
				}
			}
		}
		
		protected override void OnDestroy()
        {
	        base.OnDestroy();
	        
            if (mercenaryStatusView != null)
	            mercenaryStatusView.SafeDestroy();
	        
	        mercenaryAnimationView.SafeDestroy();
	        ((MonoBehaviour) primaryWeaponReticleView).SafeDestroy();
	        ((MonoBehaviour) specialWeaponReticleView).SafeDestroy();

	        Mercenary.EnergyUpdated -= MercenaryOnEnergyUpdated;
            Mercenary.Respawned -= MercenaryOnRespawned;
            Mercenary.EffectStateChanged -= MercenaryOnEffectStateChanged;
            Mercenary.TeamChanged -= MercenaryOnTeamChanged;
            Mercenary.WeaponChanged -= MercenaryOnWeaponChanged;
            Mercenary.Weapon.Updated -= WeaponOnUpdated;
	        Mercenary.AmmoAdded -= MercenaryOnAmmoAdded;
	        Mercenary.SpecialAmmoAdded -= MercenaryOnSpecialAmmoAdded;
        }
		
		private void RemoveAllWeapons()
		{
			mercenaryAnimationView.OutCoverWeapons();
			
			mercenaryAnimationView.UnregisterWeapons(stashedWeapons);
			mercenaryAnimationView.UnregisterWeapons(primaryWeapons);
			
			for (int i = 0; i < primaryWeapons.Count; i++)
				primaryWeapons[i].Return();

			for (int i = 0; i < stashedWeapons.Count; i++)
				stashedWeapons[i].Return();
		}
		
		private void RemovePrimaryWeapons()
		{
			mercenaryAnimationView.OutCoverWeapons();
			
			mercenaryAnimationView.UnregisterWeapons(primaryWeapons);
			
			for (int i = 0; i < primaryWeapons.Count; i++)
				primaryWeapons[i].Return();
		}

        private void ChangeWeapon(CMWeapon cmWeapon, bool fromReserve, bool toReserve, byte weaponTeamIndex)
        {
            if (toReserve)
            {
                stashedWeapons.Clear();
                stashedWeapons.AddRange(primaryWeapons);

                for (var i = 0; i < stashedWeapons.Count; i++)
                {
                    var stashedWeapon = stashedWeapons[i];
                    var bone = mercenaryAnimationView.GetBone(HumanBodyBones.UpperChest);
                    var weaponTransform = stashedWeapon.Weapon.transform;
                    weaponTransform.SetParent(bone);

                    if (i == 0)
                    {
                        var boundsCenter = weaponTransform.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.center;
                        weaponTransform.localScale = Vector3.one;
                        weaponTransform.localPosition = new Vector3(-boundsCenter.x * 0.7f, -boundsCenter.y, 0.145f);
                        weaponTransform.localRotation = Quaternion.identity;
                        weaponTransform.RotateAround(bone.position, weaponTransform.forward, 45);
                    }
                    else
                    {
                        stashedWeapon.Weapon.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                RemovePrimaryWeapons();
            }
            
            if (cmWeapon.Id == "punch")
            {
                mercenaryAnimationView.UnregisterWeapons(primaryWeapons);
                primaryWeapons.Clear();
                muzzleFlashContainer.RemoveFlash(false);
                mercenaryAnimationView.RevertAnimation();
                isMeleeWeapon = true;
            }
            else
            {
                if (fromReserve)
                {
                    primaryWeapons.Clear();
                    primaryWeapons.AddRange(stashedWeapons);
                    stashedWeapons.Clear();

                    for (var i = 0; i < primaryWeapons.Count; i++)
                    {
                        var primaryWeapon = primaryWeapons[i];
                        var weaponTransform = primaryWeapon.Weapon.transform;
                        weaponTransform.SetParent(primaryWeapon.WeaponLocator);
                        weaponTransform.localPosition = Vector3.zero;
                        weaponTransform.localScale = Vector3.one;
                        weaponTransform.localRotation = Quaternion.identity;
                        primaryWeapon.Weapon.gameObject.SetActive(true);
                        primaryWeapon.Weapon.SetTeam(weaponTeamIndex);
                    }
                }
                else
                {
                    primaryWeapons = MercenaryViewUtils.CreateWeaponsFromPool(cmWeapon, mercenaryAnimationView.LeftHandWeaponLocators, mercenaryAnimationView.RightHandWeaponLocators);
                    
                    //This needs to occur before registering weapons
                    for (var index = 0; index < primaryWeapons.Count; index++)
	                    primaryWeapons[index].Weapon.SetTeam(weaponTeamIndex);

                    mercenaryAnimationView.RegisterWeapons(primaryWeapons, inCover);
                }
                CreateMuzzleFlash(cmWeapon.MuzzleFlashPrefabId, cmWeapon.ProjectileOffset, false);
                isMeleeWeapon = cmWeapon.MeleeRange > 0;
                mercenaryAnimationView.ChangeWeaponAnimator(cmWeapon.WeaponClass, cmWeapon.Handedness, cmWeapon.FiringDuration);
                if (IsUserMercenaryView)
                    mercenaryStatusView.OnWeaponChange(cmWeapon);
            }

            if (Hidden)
                HideWeapons();

            if (IsUserMercenaryView)
            {
                primaryWeaponReticleView?.Disable();

                bool primaryIsBallistic;
                primaryWeaponReticleView = SelectWeaponReticle(Mercenary.Weapon, primaryReticleGameObject, out primaryIsBallistic);

                InputUtils.PrimaryXZPlane = primaryIsBallistic ? new Plane(Vector3.up, new Vector3(0, 0, 0)) : new Plane(Vector3.up, new Vector3(0, Mercenary.Weapon.CmWeapon.ProjectileOffset[1], 0));
            }
        }

        

		protected override void SensoUpdate(bool hard = false)
		{
			base.SensoUpdate(hard);
			
			if (IsUserMercenaryView)
				reticleContainerView.UpdateAngle();
		}

		protected override void Update()
		{			
			base.Update();

			if (IsUserMercenaryView)
				reticleContainerView.UpdateAngle();
			
			if (!reticleContainerView.gameObject.activeSelf)
				if (shield != null)
					shield.UpdateShield(transform.rotation);
		    
			var mercenaryAnimationViewTransform = mercenaryAnimationView.transform;
			
			if (!mercenaryStatusView.Hidden)
			{
				statusViewVisibilityCheckCounter++;
				if (statusViewVisibilityCheckCounter >= 5)
				{
					var visible = GameCamera.IsWorldPositionVisible(transform.position);
					var mercenaryStatusViewGameObject = mercenaryStatusView.gameObject;
					
					if (visible && !mercenaryStatusViewGameObject.activeSelf)
						mercenaryStatusViewGameObject.SetActive(true);
					else if (!visible && mercenaryStatusViewGameObject.activeSelf)
						mercenaryStatusViewGameObject.SetActive(false);
				    
				    /*if (visible && mercenaryStatusView.Hidden)
				        mercenaryStatusView.Show();
				    else if (!visible && !mercenaryStatusView.Hidden)
				        mercenaryStatusView.Hide();*/
					
					statusViewVisibilityCheckCounter = 0;
				}
			}
		    
			if (Mercenary.Grounded)
			{
				var position = mercenaryAnimationViewTransform.localPosition;
				var isInWater = Mercenary.IsInWater();

				if (isInWater)
				{
					if (depthMap == null && GameView.Instance != null)
					{
						depthMap = GameView.Instance.HeightMapTexture;

					    if (depthMap != null)
					    {
					        depthMapSize = depthMap.width;
					        depthMapPixelsPerTile = depthMapSize / 256;
					    }
					}

					if (depthMap != null) 
					{
						var worldPosition = mercenaryAnimationViewTransform.position;
						var offset = 8 * depthMapPixelsPerTile;
						var ux = (offset + worldPosition.x * depthMapPixelsPerTile) / depthMapSize;
						var uy = (offset + worldPosition.z * depthMapPixelsPerTile) / depthMapSize;
						var pixel = depthMap.GetPixelBilinear(ux, uy);
						position.y = Mathf.Max(-0.3f, -pixel.a);
						mercenaryAnimationViewTransform.localPosition = position;
					}
				}
				else
				{
					position.y = 0f;
				}

				mercenaryAnimationViewTransform.localPosition = position;

				if (!Hidden)
				{
					footstepController.UpdateSteps(isInWater);
				}
            }

			if (isMeleeWeapon)
			{
				intervalTimer -= Time.deltaTime;
			}
		}

		public void CreateVisualEffect(Weapon weapon)
		{
			if (!string.IsNullOrEmpty(weapon.CmWeapon.TeamEffect?.PrefabId))
				CreateAura(weapon);
			if (weapon.CmWeapon.Shield)
				CreateShield(weapon);
		}

		private void CreateAura(Weapon weapon)
		{
			var projectilePool = PoolingManager.GetPoolManager(weapon.CmWeapon.TeamEffect.PrefabId);
			if (projectilePool == null)
				Debug.LogErrorFormat($"[MercenaryView.CreateAura] missing prefab {weapon.CmWeapon.TeamEffect.PrefabId}");
			else
			{
				if (aura == null)
				{
					aura = (AuraView) projectilePool.Get();
					aura.transform.SetParent(transform);
					aura.Init(weapon, projectilePool);
					aura.name = Mercenary.Name;
				}
				if (Hidden)
					aura.Hide();
			}
		}

		public void RemoveVisualEffect(Weapon weapon)
		{
			if (!string.IsNullOrEmpty(weapon.CmWeapon.TeamEffect?.PrefabId))
			{
				RemoveAura();
			}
			
			if (weapon.CmWeapon.Shield)
				RemoveShield ();
			
			if (weapon.IsSpecial && weapon.CmWeapon.Duration > 0)
				mercenaryAnimationView.CancelSpecial();
		}

		private void CreateShield(Weapon weapon)
		{
			var projectilePool = PoolingManager.GetPoolManager(weapon.CmWeapon.PrefabId);
			if (projectilePool == null)
				Debug.LogError("[MercenaryView.CreateShield] missing prefab shield");
			else
			{
				if (shield == null)
				{
					shield = null;
					shield = (ShieldView) projectilePool.Get();
					shield.transform.SetParent(transform);
					shield.Init(weapon, projectilePool, Quaternion.Euler(0, fireAngle - 90f, 0));
					shield.name = Mercenary.Name;
				}
				if (Hidden)
					shield.Hide();
			}
		}

		private void RemoveShield()
		{
			if (shield != null)
				shield.DestroyShield();
			shield = null;
		}
		
		private void RemoveAura()
		{
			if (aura != null)
				aura.ReturnToPool();
			aura = null;
		}

		public void ImpactShield()
		{
			if (shield != null)
				shield.ImpactShield();
		}

		public void ShowGrounded()
		{
            if (!Hidden)
            {
                for (int i = 0; i < primaryWeapons.Count; i++)
                    primaryWeapons[i].Weapon.gameObject.SetActive(true);
                mercenaryParachuteView.Release();
	            if (!teamHelperHidden)
	            {
		            teamHelperRenderer.gameObject.SetActive(true);
		            teamHelperProjectorRenderer.gameObject.SetActive(false);
	            }
	            shadowGameObject.SetActive(true);
	            shadowGameProjector.SetActive(false);
            }
            mercenaryAnimationView.ShowGrounded();
			GameTutorialPopupManager.ShowActive();
		}

        public void ShowNonGrounded()
		{
            if (!Hidden)
            {
	            primaryWeaponReticleView?.Disable();
	            specialWeaponReticleView?.Disable();
	            flareWeaponReticleView?.Disable();
	            
                for (int i = 0; i < primaryWeapons.Count; i++)
					primaryWeapons[i].Weapon.gameObject.SetActive(false);
                mercenaryParachuteView.Deploy();
	            if (!teamHelperHidden)
	            {
		            teamHelperRenderer.gameObject.SetActive(false);
		            teamHelperProjectorRenderer.gameObject.SetActive(true);
	            }
	            shadowGameObject.SetActive(false);
	            shadowGameProjector.SetActive(true);
            }

			inCover = false;
            mercenaryAnimationView.ShowNonGrounded();
			mercenaryAnimationView.OutCover();
		}

		public void ShowInteraction(float duration)
		{
			mercenaryStatusView.ShowInteraction(duration);
			GameTutorialPopupManager.HideActive();
		}

		public void HideInteraction()
		{
			mercenaryStatusView.HideInteraction();
			GameTutorialPopupManager.ShowActive();
		}

	    public void EnterVehicle(VehicleView vehicleView, bool isDrive, Point position)
	    {
	        currentVehicleView = vehicleView;
	        preEnterVehicleParentTransform = transform.parent;
	        transform.SetParent(vehicleView.transform);
	        transform.localPosition = position.ToVector3();
	    }

	    public void ExitVehicle()
	    {
	        currentVehicleView = null;
	        transform.SetParent(preEnterVehicleParentTransform);
	        preEnterVehicleParentTransform = null;
	    }

		public void FiringStarted()
		{
			if (activeReticleView == null)
				return;
			
			firing = true;
		}

		public void CancelSpecial()
		{
			mercenaryAnimationView.CancelSpecial();
		}
	}

	public class WeaponEntry
	{
		public WeaponView Weapon { get; }
		public Transform WeaponLocator { get; }
		public bool IsRightHand { get; }
		public string LocatorName { get; }
		public Renderer[] Renderers { get; }
		
		public readonly PoolManager Pool;

		public WeaponEntry(WeaponView weapon, PoolManager pool, Transform weaponLocator, bool isRightHand, string locatorName)
		{
			Weapon = weapon;
			this.Pool = pool;
			WeaponLocator = weaponLocator;
			IsRightHand = isRightHand;
			LocatorName = locatorName;
			Renderers = Weapon.GetComponentsInChildren<Renderer>();
		}

		public void Return()
		{
			Pool.Return(Weapon);
		}
	}
}
