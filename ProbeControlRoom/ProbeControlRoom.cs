//ProbeControlRoom.cs
using System;
using UnityEngine;
using System.Collections.Generic;
using KSP.UI.Screens;


namespace ProbeControlRoom
{
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    /// <summary>
    /// Primary class for Probe Control room
    /// </summary>
	public class ProbeControlRoom : MonoBehaviour
    {
        //Instance reference
        public static ProbeControlRoom Instance { get; protected set; }

        //Is IVA currently active and visible
        public static bool isActive = false;
		private bool needCamReset = false;

		//Sun
		IVASun CachedSun = null;
		bool SunIsEnabled = true;

		//Stuff to mess with
		static VirindiHelpers.DynamicEmitFields.delCreateDynamicInstanceFieldGet<float, InternalCamera> field_get_internalcamera_currentPitch;
		static VirindiHelpers.DynamicEmitFields.delCreateDynamicInstanceFieldGet<float, InternalCamera> field_get_internalcamera_currentRot;
		static VirindiHelpers.DynamicEmitFields.delCreateDynamicInstanceFieldGet<float, InternalCamera> field_get_internalcamera_currentZoom;
		static VirindiHelpers.DynamicEmitFields.delCreateDynamicInstanceFieldSet<float, InternalCamera> field_set_internalcamera_currentPitch;
		static VirindiHelpers.DynamicEmitFields.delCreateDynamicInstanceFieldSet<float, InternalCamera> field_set_internalcamera_currentRot;
		static VirindiHelpers.DynamicEmitFields.delCreateDynamicInstanceFieldSet<float, InternalCamera> field_set_internalcamera_currentZoom;
		bool hassavedlookangles = false;
		float savedpitch = 0f;
		float savedrot = 0f;
		float savedzoom = 0f;
        
        //Vessel has IVA with crew onboard
        private bool canStockIVA;
		private bool maybecanstockiva;
        
        //Vessel has a probe control room available
        private bool canPCRIVA;
        
        //Current PCR module
        private ProbeControlRoomPart aModule;

        //Current PCR part with internal module
        private Part aPart;
		private MeshRenderer[] cachedrenderers = null;
        
        //Storage for original game settings
        float shipVolumeBackup = GameSettings.SHIP_VOLUME;
        float ambianceVolumeBackup = GameSettings.AMBIENCE_VOLUME;
        float cameraWobbleBackup = GameSettings.FLT_CAMERA_WOBBLE;
        float cameraFXInternalBackup = GameSettings.CAMERA_FX_INTERNAL;
        float cameraFXExternalBackup = GameSettings.CAMERA_FX_EXTERNAL;

		//Vessel labels settings
		bool HasCachedVesselLabelsSetting = false;
		bool CachedVesselLabelsSetting = false;
		bool VesselLabelKeyDisabled = false;
		KeyCode CachedLabelPrimaryKey = GameSettings.TOGGLE_LABELS.primary;
		KeyCode CachedLabelSecondaryKey = GameSettings.TOGGLE_LABELS.secondary;
		private static System.Reflection.MethodInfo method_vessellabels_enablealllabels = null;
		private static System.Reflection.MethodInfo method_vessellabels_disablealllabels = null;

		//Highlight in flight?
		bool HasCachedHighlightInFlightSetting = false;
		bool CachedHighlightInFlightSetting = true;

        //Application 
        private static ApplicationLauncherButton appLauncherButton = null;
        //App launcher in use
        private bool AppLauncher = false;

        //App launcher icons
        private Texture2D IconActivate = null;
        private Texture2D IconDeactivate = null;

		static void GetFields()
		{
			if (field_get_internalcamera_currentPitch == null)
				field_get_internalcamera_currentPitch = VirindiHelpers.DynamicEmitFields.CreateDynamicInstanceFieldGet<float, InternalCamera> ("currentPitch");
			if (field_get_internalcamera_currentRot == null)
				field_get_internalcamera_currentRot = VirindiHelpers.DynamicEmitFields.CreateDynamicInstanceFieldGet<float, InternalCamera> ("currentRot");
			if (field_get_internalcamera_currentZoom == null)
				field_get_internalcamera_currentZoom = VirindiHelpers.DynamicEmitFields.CreateDynamicInstanceFieldGet<float, InternalCamera> ("currentZoom");

			if (field_set_internalcamera_currentPitch == null)
				field_set_internalcamera_currentPitch = VirindiHelpers.DynamicEmitFields.CreateDynamicInstanceFieldSet<float, InternalCamera> ("currentPitch");
			if (field_set_internalcamera_currentRot == null)
				field_set_internalcamera_currentRot = VirindiHelpers.DynamicEmitFields.CreateDynamicInstanceFieldSet<float, InternalCamera> ("currentRot");
			if (field_set_internalcamera_currentZoom == null)
				field_set_internalcamera_currentZoom = VirindiHelpers.DynamicEmitFields.CreateDynamicInstanceFieldSet<float, InternalCamera> ("currentZoom");

			if (method_vessellabels_enablealllabels == null)
				method_vessellabels_enablealllabels = typeof(VesselLabels).GetMethod ("EnableAllLabels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.Instance);
			if (method_vessellabels_disablealllabels == null)
				method_vessellabels_disablealllabels = typeof(VesselLabels).GetMethod ("DisableAllLabels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.Instance);
		}

        /// <summary>
        /// Startup and initialization
        /// </summary>
        public void Start()
        {
            ProbeControlRoomUtils.Logger.debug("Start()");
            Instance = this;


			try{
				GetFields();
			}
			catch (Exception ex) {
				ProbeControlRoomUtils.Logger.debug("Exception finding fields: " + ex.ToString());
			}


            refreshVesselRooms();

            //Register game events
            GameEvents.onVesselWasModified.Fire(FlightGlobals.ActiveVessel);

            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onVesselWasModified.Add(OnVesselModified);
            GameEvents.onGUIApplicationLauncherReady.Add(onGUIApplicationLauncherReady);
			GameEvents.OnMapExited.Add(onMapExited);
			GameEvents.OnCameraChange.Add(onCameraChange);
			GameEvents.onGameSceneSwitchRequested.Add(OnGameSceneSwitchRequested);

            //If Manely mode is set true, force straight into IVA
            if (ProbeControlRoomSettings.Instance.ForcePCROnly)
            {
                ProbeControlRoomUtils.Logger.message("Start() - ForcePCROnly Enabled.");
                startIVA();
            }
        }

		void OnGameSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes> scn)
		{
			if (isActive)
				stopIVA ();
		}

        /// <summary>
        /// Setup app launcher button when the GUI is ready
        /// </summary>
        private void onGUIApplicationLauncherReady()
        {
            if (!AppLauncher)
            {
                appLauncherButton = InitializeApplicationButton();
                AppLauncher = true;
                if(!canPCRIVA)
                {
                    appLauncherButton.Disable();
                }
            }
           
        }

		private void onMapExited() {
			needCamReset = true; 
		}

		private void onCameraChange(CameraManager.CameraMode c){
			//ProbeControlRoomUtils.Logger.message("OnCameraChange: " + c.ToString());
			//needCamReset = true;
		}

        /// <summary>
        /// Sets up the App launcher button to activate and deactivate PCR
        /// </summary>
        /// <returns>Reference to created button</returns>
        ApplicationLauncherButton InitializeApplicationButton()
        {
            ApplicationLauncherButton Button = null;

            IconActivate = GameDatabase.Instance.GetTexture("ProbeControlRoom/Icons/ProbeControlRoomToolbarDisabled", false);
            IconDeactivate = GameDatabase.Instance.GetTexture("ProbeControlRoom/Icons/ProbeControlRoomToolbarEnabled", false);


            Button = ApplicationLauncher.Instance.AddModApplication(
                OnAppLauncherTrue,
                OnAppLauncherFalse,
                null,
                null,
                null,
                null,
                ApplicationLauncher.AppScenes.FLIGHT,
                IconActivate);

            if (Button == null)
            {
                ProbeControlRoomUtils.Logger.debug("InitializeApplicationButton(): Was unable to initialize button");
            }

			if (isActive)
				Button.SetTexture (IconDeactivate);

            return Button;
        }

        /// <summary>
        /// Static function for Toolbar integration
        /// </summary>
        public static bool vesselCanIVA
        {
            get {
                return Instance.canPCRIVA;
            }
        }

        /// <summary>
        /// Toggles PCR mode on
        /// </summary>
        void OnAppLauncherTrue()
        {
            toggleIVA();
        }

        /// <summary>
        /// Toggles PCR mode off
        /// </summary>
        void OnAppLauncherFalse()
        {
            toggleIVA();
        }

        /// <summary>
        /// Activates and deactivates PCR as well as changes icon in the app launcher
        /// </summary>
        private void toggleIVA()
        {
            if (isActive)
            {
                stopIVA();
            }
            else
            {
                startIVA();
            }
        }

        /// <summary>
        /// Shuts down IVA and restores all game settings
        /// </summary>
        public void OnDestroy()
        {
			SetSun (true);
			CachedSun = null;

            //in case of revert to launch while in IVA, Update() won't detect it
            //and startIVA(p) will be called without prior stopIVA
            //which will cause settings to be lost forever
            //OnDestroy() will be called though

            if (ProbeControlRoomSettings.Instance.DisableSounds)
            {
                ProbeControlRoomUtils.Logger.message("OnDestroy() - DisableSounds - RESTORE");
                //re-enable sound
                GameSettings.SHIP_VOLUME = shipVolumeBackup;
                GameSettings.AMBIENCE_VOLUME = ambianceVolumeBackup;
            }

            if (ProbeControlRoomSettings.Instance.DisableWobble)
            {
                ProbeControlRoomUtils.Logger.message("OnDestroy() - DisableWobble - RESTORE");
                //re-enable camera wobble
                GameSettings.FLT_CAMERA_WOBBLE = cameraWobbleBackup;
                GameSettings.CAMERA_FX_INTERNAL = cameraFXInternalBackup;
                GameSettings.CAMERA_FX_EXTERNAL = cameraFXExternalBackup;
            }

			ProbeControlRoomUtils.Logger.message("OnDestroy() - VesselLabels - " + HasCachedVesselLabelsSetting.ToString() + ", " + VesselLabelKeyDisabled.ToString());
			//Restore vessel labels capability.
			if (HasCachedVesselLabelsSetting)
			{
				HasCachedVesselLabelsSetting = false;
				SetVesselLabelsValue (CachedVesselLabelsSetting);
			}
			if (VesselLabelKeyDisabled)
			{
				VesselLabelKeyDisabled = false;
				GameSettings.TOGGLE_LABELS.primary = CachedLabelPrimaryKey;
				GameSettings.TOGGLE_LABELS.secondary = CachedLabelSecondaryKey;
			}

			//Restore part highlighter
			if (HasCachedHighlightInFlightSetting)
			{
				HasCachedHighlightInFlightSetting = false;
				GameSettings.INFLIGHT_HIGHLIGHT = CachedHighlightInFlightSetting;
			}

            ProbeControlRoomUtils.Logger.debug("OnDestroy()");

			GameEvents.onVesselChange.Remove(OnVesselChange);
			GameEvents.onVesselWasModified.Remove(OnVesselModified);
			GameEvents.onGUIApplicationLauncherReady.Remove(onGUIApplicationLauncherReady);
			GameEvents.OnMapExited.Remove(onMapExited);
			GameEvents.OnCameraChange.Remove(onCameraChange);
			GameEvents.onGameSceneSwitchRequested.Remove(OnGameSceneSwitchRequested);

            if (appLauncherButton != null)
            {
                ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                appLauncherButton = null;
            }
            Instance = null;
        }

		void SetSun(bool on)
		{
			ProbeControlRoomUtils.Logger.message("SetSun() - " + on.ToString());

			if (CachedSun == null)
				CachedSun = (IVASun)FindObjectOfType(typeof(IVASun));
			if (CachedSun == null)
				return;

			ProbeControlRoomUtils.Logger.message("SetSun() - SETTING");
			CachedSun.gameObject.SetActive (on);
			SunIsEnabled = on;
		}

		void SetVesselLabelsValue(bool on)
		{
			//ProbeControlRoomUtils.Logger.message("SetVesselLabelsValue() - ENTER");

			if (GameSettings.FLT_VESSEL_LABELS == on)
				return;

			VesselLabels lbls = (VesselLabels)FindObjectOfType (typeof(VesselLabels));

			ProbeControlRoomUtils.Logger.message("SetVesselLabelsValue() - lbls - " + (lbls != null).ToString());

			if (on && lbls != null && method_vessellabels_enablealllabels != null)
			{
				ProbeControlRoomUtils.Logger.message ("SetVesselLabelsValue() - INVOKE ENABLE");

				method_vessellabels_enablealllabels.Invoke (lbls, null);
				GameSettings.FLT_VESSEL_LABELS = true;
			}
			else if (!on && lbls != null && method_vessellabels_disablealllabels != null)
			{
				ProbeControlRoomUtils.Logger.message ("SetVesselLabelsValue() - INVOKE DISABLE");

				method_vessellabels_disablealllabels.Invoke (lbls, null);
				GameSettings.FLT_VESSEL_LABELS = false;
			}
			else if (!lbls)
			{
				ProbeControlRoomUtils.Logger.message ("SetVesselLabelsValue() - NO LBLS SET");

				//We are probably leaving flight, so VesselLabels has been eliminated.
				//Set this for later.
				GameSettings.FLT_VESSEL_LABELS = on;
			}
		}

        /// <summary>
        /// Sets up IVA and activates it
        /// </summary>
        /// <returns>True if successful, False on error</returns>
        public bool startIVA()
        {
			if (FlightGlobals.ActiveVessel == null)
			{
				ProbeControlRoomUtils.Logger.debug("startIVA() - return ACTIVE VESSEL NULL");
				return false;
			}
            ProbeControlRoomUtils.Logger.debug("startIVA()");
            Transform actualTransform;

			if (FlightGlobals.ActiveVessel.packed) {
				ProbeControlRoomUtils.Logger.debug("startIVA() - return vessel still packed!");
				return false;
			}

			if (canStockIVA || FlightGlobals.ActiveVessel.evaController != null) {
				ProbeControlRoomUtils.Logger.debug("startIVA() - return EVA or IVA");
				return false;
			}

            //Verify active room available
            if (!canPCRIVA)
            {
                ProbeControlRoomUtils.Logger.message("startIVA() - Refresh rooms said there were no IVAs available! Can't start.");
                return false;
            }

            //Ensure part still exists
            if (aPart == null)
            {
                ProbeControlRoomUtils.Logger.message("startIVA() Lost our part, refreshing");
                refreshVesselRooms();
            }
			if (aPart == null) {
				ProbeControlRoomUtils.Logger.message("startIVA() Can't find a part. DIE.");
				return false;
			}

            //Setup module for transforms
            if (aPart.FindModulesImplementing<ProbeControlRoomPart>().Count == 0)
            {
                ProbeControlRoomUtils.Logger.error("startIVA() a module was not found on the part now, exiting");
                return false;
            }
            aModule = aPart.FindModulesImplementing<ProbeControlRoomPart>()[0];

            //Make the PCR part the focused part
            aPart.MakeReferencePart();

            //Setup camera transform for internal seat
            actualTransform = aPart.internalModel.FindModelTransform(aModule.seatTransformName);
            if (Transform.Equals(actualTransform, null))
            {
                ProbeControlRoomUtils.Logger.error("startIVA(Part) - NULL on actualTransform-seatTransformName, using fallback...");
                actualTransform = aPart.internalModel.FindModelTransform("Seat");
            }
            else
            {
                ProbeControlRoomUtils.Logger.message("startIVA(Part) - Seat: " + aModule.seatTransformName.ToString());
            }

            ProbeControlRoomUtils.Logger.debug("startIVA() - fire up IVA");


            //disable sound
            shipVolumeBackup = GameSettings.SHIP_VOLUME;
            ambianceVolumeBackup = GameSettings.AMBIENCE_VOLUME;
            if (ProbeControlRoomSettings.Instance.DisableSounds)
            {
                ProbeControlRoomUtils.Logger.message("startIVA() - DisableSounds");
                GameSettings.SHIP_VOLUME = 0f;
                GameSettings.AMBIENCE_VOLUME = 0;
                GameSettings.MUSIC_VOLUME = 0;
                GameSettings.UI_VOLUME = 0;
                GameSettings.VOICE_VOLUME = 0;
            }

            //disable camera wobble
            cameraWobbleBackup = GameSettings.FLT_CAMERA_WOBBLE;
            cameraFXInternalBackup = GameSettings.CAMERA_FX_INTERNAL;
            cameraFXExternalBackup = GameSettings.CAMERA_FX_EXTERNAL;

            if (ProbeControlRoomSettings.Instance.DisableWobble)
            {
                ProbeControlRoomUtils.Logger.message("startIVA() - DisableWobble");
                GameSettings.FLT_CAMERA_WOBBLE = 0;
                GameSettings.CAMERA_FX_INTERNAL = 0;
                GameSettings.CAMERA_FX_EXTERNAL = 0;
            }
            // TODO: create cfg file with cached vars, on crash to be restored

			//Prevent user from turning on vessel labels
			if (!HasCachedVesselLabelsSetting)
			{
				HasCachedVesselLabelsSetting = true;
				CachedVesselLabelsSetting = GameSettings.FLT_VESSEL_LABELS;
			}
			if (!VesselLabelKeyDisabled)
			{
				VesselLabelKeyDisabled = true;
				CachedLabelPrimaryKey = GameSettings.TOGGLE_LABELS.primary;
				CachedLabelSecondaryKey = GameSettings.TOGGLE_LABELS.secondary;
				GameSettings.TOGGLE_LABELS.primary = KeyCode.None;
				GameSettings.TOGGLE_LABELS.secondary = KeyCode.None;
			}
			SetVesselLabelsValue (false);


			//Highlighters
			if (!HasCachedHighlightInFlightSetting)
			{
				HasCachedHighlightInFlightSetting = true;
				CachedHighlightInFlightSetting = GameSettings.INFLIGHT_HIGHLIGHT;
			}
			GameSettings.INFLIGHT_HIGHLIGHT = false;


            //Unsure of this purpose at the moment
            FlightCamera.fetch.EnableCamera();
            FlightCamera.fetch.DeactivateUpdate();
            FlightCamera.fetch.gameObject.SetActive(true);

            //Move internal camera to the correct position and enable
            InternalCamera.Instance.SetTransform(actualTransform, true);
            InternalCamera.Instance.EnableCamera();

            if (UIPartActionController.Instance != null)
                UIPartActionController.Instance.Deactivate();

            //Activate internal camera
            CameraManager.Instance.SetCameraInternal(aPart.internalModel, actualTransform);

            ProbeControlRoomUtils.Logger.debug("startIVA() - DONE");

			//GUI may not be started yet.
			if (appLauncherButton != null) {
				//Change app launcher button icon
				appLauncherButton.SetTexture (IconDeactivate);
			}

			if (hassavedlookangles && field_set_internalcamera_currentPitch != null && field_set_internalcamera_currentRot != null && field_set_internalcamera_currentZoom != null) {
				ProbeControlRoomUtils.Logger.debug(string.Format("startIVA() - Restoring pitch and rot. {0}, {1}", savedpitch, savedrot));

				field_set_internalcamera_currentPitch(InternalCamera.Instance, savedpitch);
				field_set_internalcamera_currentRot(InternalCamera.Instance, savedrot);
				field_set_internalcamera_currentZoom(InternalCamera.Instance, savedzoom);
				InternalCamera.Instance.Update ();
			}

			//Disable sun effects inside of IVA
			SetSun(false);

			ProbeControlRoomUtils.Logger.debug("startIVA() - REALLY DONE");

            isActive = true;

            return true;

        }

		void ResetCameraToIVA()
		{
			aModule = aPart.FindModulesImplementing<ProbeControlRoomPart>()[0];
			Transform actualTransform = aPart.internalModel.FindModelTransform(aModule.seatTransformName);
			if (Transform.Equals(actualTransform, null))
			{
				ProbeControlRoomUtils.Logger.error("ResetCameraToIVA(Part) - NULL on actualTransform-seatTransformName, using fallback...");
				actualTransform = aPart.internalModel.FindModelTransform("Seat");
			}
			else
			{
				ProbeControlRoomUtils.Logger.message("ResetCameraToIVA(Part) - Seat: " + aModule.seatTransformName.ToString());
			}
				

			CameraManager.Instance.SetCameraInternal(aPart.internalModel, actualTransform);


			//Restore PCRIVA camera
			if (hassavedlookangles && field_set_internalcamera_currentPitch != null && field_set_internalcamera_currentRot != null && field_set_internalcamera_currentZoom != null) {
				field_set_internalcamera_currentPitch(InternalCamera.Instance, savedpitch);
				field_set_internalcamera_currentRot(InternalCamera.Instance, savedrot);
				field_set_internalcamera_currentZoom(InternalCamera.Instance, savedzoom);
				InternalCamera.Instance.Update ();
			}

			//Disable sun effects inside of IVA
			SetSun(false);

			ProbeControlRoomUtils.Logger.debug("ResetCameraToIVA - DONE");
		}

        /// <summary>
        /// Shuts down current PCR IVA
        /// </summary>
        public void stopIVA()
        {

            ProbeControlRoomUtils.Logger.debug("stopIVA()");

			//Enable sun effects inside of IVA
			SetSun(true);

            isActive = false;
			needCamReset = false;
            
            //Restore settings to levels prior to entering IVA
            if (ProbeControlRoomSettings.Instance.DisableSounds)
            {
                ProbeControlRoomUtils.Logger.message("stopIVA() - DisableSounds - RESTORE");
                //re-enable sound
                GameSettings.SHIP_VOLUME = shipVolumeBackup;
                GameSettings.AMBIENCE_VOLUME = ambianceVolumeBackup;
            }

            if (ProbeControlRoomSettings.Instance.DisableWobble)
            {
                ProbeControlRoomUtils.Logger.message("stopIVA() - DisableWobble - RESTORE");
                //re-enable camera wobble
                GameSettings.FLT_CAMERA_WOBBLE = cameraWobbleBackup;
                GameSettings.CAMERA_FX_INTERNAL = cameraFXInternalBackup;
                GameSettings.CAMERA_FX_EXTERNAL = cameraFXExternalBackup;
            }

			//Restore vessel labels capability.
			if (HasCachedVesselLabelsSetting)
			{
				HasCachedVesselLabelsSetting = false;
				SetVesselLabelsValue (CachedVesselLabelsSetting);
			}
			if (VesselLabelKeyDisabled)
			{
				VesselLabelKeyDisabled = false;
				GameSettings.TOGGLE_LABELS.primary = CachedLabelPrimaryKey;
				GameSettings.TOGGLE_LABELS.secondary = CachedLabelSecondaryKey;
			}

			//Restore part highlighter
			if (HasCachedHighlightInFlightSetting)
			{
				HasCachedHighlightInFlightSetting = false;
				GameSettings.INFLIGHT_HIGHLIGHT = CachedHighlightInFlightSetting;
			}

            //Switch back to normal cameras
            CameraManager.ICameras_DeactivateAll();
            CameraManager.Instance.SetCameraFlight();

            if (UIPartActionController.Instance != null)
                UIPartActionController.Instance.Activate();

            ProbeControlRoomUtils.Logger.debug("stopIVA() - CHECKMARK");

            //Change app launcher button
            appLauncherButton.SetTexture(IconActivate);
        }

		public void Update()
		{
			if (HighLogic.LoadedScene == GameScenes.FLIGHT
			    && isActive
			    && !canStockIVA
			    && CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.IVA)
			{
				//Stuff to do early while running

				//Kill vessel labels
				SetVesselLabelsValue (false);

				//Save where the camera was looking.
				//We have to do this here because if the part is shut down,
				//we don't have time to get it before it is lost.
				if (field_get_internalcamera_currentPitch != null && field_get_internalcamera_currentRot != null && field_get_internalcamera_currentZoom != null) {
					float newpitch = field_get_internalcamera_currentPitch(InternalCamera.Instance);
					float newrot = field_get_internalcamera_currentRot(InternalCamera.Instance);
					float newzoom = field_get_internalcamera_currentZoom(InternalCamera.Instance);

					//Note that if the PCRIVA got broken, these are zero.
					if (newpitch != 0f || newrot != 0f)
					{
						hassavedlookangles = true;
						savedpitch = newpitch;
						savedrot = newrot;
						savedzoom = newzoom;
					}
				}


			}
		}

        /// <summary>
        /// Check for invalid PCR states and controls inputs for acivation/deactivation
        /// </summary>
        public void LateUpdate()
        {
            // PCR should only be active during Flight
            var scene = HighLogic.LoadedScene;
            if (scene == GameScenes.FLIGHT)
            {
				if (maybecanstockiva && HighLogic.CurrentGame.Parameters.Flight.CanIVA) {
					ProbeControlRoomUtils.Logger.message("OnUpdate() - Maybe we can IVA, rescan");
					refreshVesselRooms ();
				}

                if (isActive)
                {
					//***Start in flight and active***
					
                    //If IVA Camera is active, there is a valid and operational stock IVA and PCR needs to be shutdown
					if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA
					|| canStockIVA) {
						
						ProbeControlRoomUtils.Logger.message ("OnUpdate() - real IVA detected, ending...");
						stopIVA ();
						/*
						if (ProbeControlRoomSettings.Instance.ForcePCROnly) {
							ProbeControlRoomUtils.Logger.message ("OnUpdate() - real IVA detected, ending... KILLED - ForcePCROnly Enabled.");
							startIVA ();
						}
						*/

					} else {

						//***Start in flight, active, and should be active***

						if (aPart == null || aPart.internalModel == null)
							needCamReset = true;
						else {
							if (cachedrenderers == null)
								cachedrenderers = aPart.internalModel.GetComponentsInChildren<MeshRenderer> ();
							if (cachedrenderers.Length > 0 && !cachedrenderers [0].enabled) {
								ProbeControlRoomUtils.Logger.message ("Need cam reset because renderer off.");
								needCamReset = true;
							}
						}

						if ((needCamReset || FlightCamera.fetch.updateActive)
						   && (!MapView.MapIsEnabled && FlightGlobals.ActiveVessel != null && !FlightGlobals.ActiveVessel.packed)) {
							ResetCameraToIVA ();
							needCamReset = false;
							ProbeControlRoomUtils.Logger.message ("Done with needCamReset.");
						}

						//Check for imput to stop IVA
						if (!MapView.MapIsEnabled && GameSettings.CAMERA_MODE.GetKeyDown(false)) {
							ProbeControlRoomUtils.Logger.message ("OnUpdate() - CAMERA_MODE.key seen, stopIVA()");
							if (ProbeControlRoomSettings.Instance.ForcePCROnly) {
								ProbeControlRoomUtils.Logger.message ("OnUpdate() - CAMERA_MODE.key seen, stopIVA() KILLED - ForcePCROnly Enabled.");
							} else {
								stopIVA ();
							}
						}

						//***End in flight, active, and should be active***
					}

					//***End in flight and active***
                }
                else
                {
                    // Listen for keyboard input to start PCR unless a valid IVA exists.  PCR can still be started via AppLauncher or toolbar
					if (!maybecanstockiva && !canStockIVA && canPCRIVA && !MapView.MapIsEnabled && 
						(ProbeControlRoomSettings.Instance.ForcePCROnly || GameSettings.CAMERA_MODE.GetKeyDown(false) )
						) {
						ProbeControlRoomUtils.Logger.message ("OnUpdate() - CAMERA_MODE.key seen, startIVA()");
						startIVA ();
					}
                }
            }
            else
            {
                //Shutdown PCR if not in flight
                if (isActive)
                {
                    ProbeControlRoomUtils.Logger.error("OnUpdate() - stopping, active while not in FLIGHT");
                    stopIVA();
                }
            }
        }

        /// <summary>
        /// Vessel has changed to a new one, check IVA status (docking, vessel switching, etc.)
        /// </summary>
        /// <param name="v">New Vessel</param>
        private void OnVesselChange(Vessel v)
        {
            ProbeControlRoomUtils.Logger.message("OnVesselChange(Vessel)");
			aPart = null;
            vesselModified();
        }

        /// <summary>
        /// Vessel has been modified, check IVA status (Staging, RUD, etc.)
        /// </summary>
        /// <param name="v">New Vessel</param>
        private void OnVesselModified(Vessel v)
        {
            ProbeControlRoomUtils.Logger.message("onVesselWasModified(Vessel)");
            vesselModified();
        }

        /// <summary>
        /// Rebuild room and IVA information and reset PCR as needed
        /// </summary>
        private void vesselModified()
        {
            ProbeControlRoomUtils.Logger.message("vesselModified()");
			if (FlightGlobals.ActiveVessel == null) {
				ProbeControlRoomUtils.Logger.message("vesselModified() - no active vessel, returning");
				stopIVA ();
				return;
			}
            Part oldPart = aPart;
            refreshVesselRooms();
            //Only stop the IVA if the part is missing, restart it otherwise
			if (isActive) {
				if (!canPCRIVA) {
					ProbeControlRoomUtils.Logger.message ("vesselModified() - Can no longer use PCR on this vessel");
					stopIVA ();
				}

				if (aPart != oldPart) {
					ProbeControlRoomUtils.Logger.message ("vesselModified() - Have to change part.");
					//Can still PCR IVA but the part has changed, restart

					stopIVA ();
					startIVA ();
				}
			} else if (canPCRIVA && !canStockIVA && !MapView.MapIsEnabled && ProbeControlRoomSettings.Instance.ForcePCROnly) {
				startIVA ();
			}
        }

        /// <summary>
        /// Scans vessel for usable IVA rooms and PCR rooms and initializes them as neccessary
        /// </summary>
		private void refreshVesselRooms()
        {
            ProbeControlRoomUtils.Logger.debug("refreshVesselRooms()");

            Vessel vessel = FlightGlobals.ActiveVessel;

            //If the vessel is null, there is something wrong and no reason to continue scan
            if (vessel == null)
            {
                canStockIVA = false;
				maybecanstockiva = false;
                aPart = null;
				cachedrenderers = null;
                aModule = null;
                ProbeControlRoomUtils.Logger.error("refreshVesselRooms() - ERROR: FlightGlobals.activeVessel is NULL");
                return;
            }

			canStockIVA = false;
			maybecanstockiva = false;
			for (int i = 0; i < vessel.parts.Count; i++) {
				Part p = vessel.parts [i];
				ProbeControlRoomPart room = p.GetComponent<ProbeControlRoomPart> ();

				//Are we not loaded yet?
				if ((!HighLogic.CurrentGame.Parameters.Flight.CanIVA || vessel.packed) && p.protoModuleCrew.Count > 0) {
					ProbeControlRoomUtils.Logger.message ("refreshVesselRooms() - Maybe we can IVA!");
					maybecanstockiva = true;
					canStockIVA = true;
				} else if (HighLogic.CurrentGame.Parameters.Flight.CanIVA && p.protoModuleCrew.Count > 0 && p.internalModel != null) {
					ProbeControlRoomUtils.Logger.message ("refreshVesselRooms() - Stock IVA possible. Part: " + p.ToString ());
					canStockIVA = true;
				}
			}

            // If our current vessel still has the old PCR part, keep it active
			if (aPart != null && vessel.parts.Contains (aPart)) {

				// If stock IVA is available then we can't have our model around, it might
				// interfere with stock IVA clicks.
				if (canStockIVA) {
					ProbeControlRoomUtils.Logger.debug ("refreshVesselRooms() - Destroying existing PCR part due to stock IVA.");
					canPCRIVA = false;
					if (aPart.internalModel != null) {
						aPart.internalModel.gameObject.DestroyGameObject ();
						aPart.internalModel = null;
					}
					aPart = null;
				} else {
					canPCRIVA = true;
					ProbeControlRoomUtils.Logger.debug ("refreshVesselRooms() - Old part still there, cleaning up extra rooms and returning");
					//Our old part is still there and active. Clean up extras as needed and return
					for (int i = 0; i < vessel.parts.Count; i++) {
						Part p = vessel.parts [i];
						if (p.GetComponent<ProbeControlRoomPart> () != null && aPart != p && p.protoModuleCrew.Count == 0 && p.internalModel != null) {
							ProbeControlRoomUtils.Logger.debug ("refreshRooms() Found and destroying old PCR in " + p.ToString ());
							p.internalModel.gameObject.DestroyGameObject ();
							p.internalModel = null;
						}
					}
				}
				return;
			} else {
				aPart = null;
				ProbeControlRoomUtils.Logger.debug ("refreshVesselRooms() - Old part no longer in vessel.");
			}

			//Do not create PCR when stock IVA available.
			if (canStockIVA) {
				canPCRIVA = false;
				return;
			}

            //No current active PCR found, time to create a new one
            canPCRIVA = false;
            List<Part> rooms = new List<Part>();
            List<Part> pcrNoModel = new List<Part>();

            ProbeControlRoomUtils.Logger.message("refreshVesselRooms() - scanning vessel: " + vessel.ToString());


            //Look throught the list of parts and save those that have probe control room modules on them based on available internal models
            for (int i = 0; i < vessel.parts.Count; i++)
            {
                Part p = vessel.parts[i];
                ProbeControlRoomPart room = p.GetComponent<ProbeControlRoomPart>();

                if (room != null)
                {
                    if (p.internalModel != null)
                    {
                        //Check for stock IVA
                        if (p.protoModuleCrew.Count > 0)
                        {
                            ProbeControlRoomUtils.Logger.message("refreshVesselRooms() - Found Stock IVA with crew: " + p.ToString());
                            canStockIVA = true;
                        }
                        else
                        {
                            //No stock IVA possible, PCR model found
                            ProbeControlRoomUtils.Logger.message("refreshVesselRooms() - Found part with PCR IVA model: " + p.ToString());
                            rooms.Add(p);
                        }
                    }
                    else
                    {
                        //PCR Module noted but no active internal model found
                        ProbeControlRoomUtils.Logger.message("refreshVesselrooms() - Found PCR part but it has no model: " + p.ToString());
                        pcrNoModel.Add(p);
                    }


                }
            }

            //Clean up and specifiy active rooms
            if (rooms.Count > 0)
            {
                ProbeControlRoomUtils.Logger.message("refreshVesselRooms() - Cleaning up pcrNoModel List");
                pcrNoModel.Clear();
                pcrNoModel = null;


                //Select primary part for use and verify it's initialized
				ProbeControlRoomUtils.Logger.message("refreshVesselRooms() - Initializing room in " + rooms[0].ToString());
                aPart = rooms[0];
				cachedrenderers = null;
                aPart.internalModel.Initialize(aPart);
                aPart.internalModel.SetVisible(false);

				ProbeControlRoomUtils.Logger.message ("refreshVesselRooms() - C");

                //Remove Excess internal models
                if (rooms.Count > 1)
                {
                    ProbeControlRoomUtils.Logger.debug("refreshVesselRooms() - Removing " + (rooms.Count - 1) + " Rooms");
                    for (int i = 1; i < rooms.Count; i++)
                    {
                        rooms[i].internalModel.gameObject.DestroyGameObject();
                        rooms[i].internalModel = null;
                    }
                }
                canPCRIVA = true;
                rooms.Clear();
                rooms = null;
            }
            else if(pcrNoModel.Count > 0)
            {
                // No parts with an available internal model were found, attempting to create one
                aPart = pcrNoModel[0];
				cachedrenderers = null;
                aPart.CreateInternalModel();
                ProbeControlRoomUtils.Logger.debug("refreshVesselRooms() - No active room with a model found, creating now in " + aPart.ToString());
                if (aPart.internalModel == null)
                {
                    //Something went wrong creating the model
                    ProbeControlRoomUtils.Logger.message("refreshVesselRooms() - ERROR creating internal model");
                    return;
                }
                aPart.internalModel.Initialize(aPart);
                aPart.internalModel.SetVisible(false);
                canPCRIVA = true;
            }

			if (pcrNoModel != null)
				pcrNoModel.Clear ();
            pcrNoModel = null;

            // Set app launcher availability based on current layout.
            if(AppLauncher)
            {
                if (canPCRIVA)
                {
                    appLauncherButton.Enable();
                }
                else
                {
                    appLauncherButton.Disable();
                }
            }
            return;
        }
    }
}

