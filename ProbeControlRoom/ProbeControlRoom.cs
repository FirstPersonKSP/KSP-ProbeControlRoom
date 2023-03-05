//ProbeControlRoom.cs
using System;
using UnityEngine;
using System.Collections.Generic;
using KSP.UI.Screens;
using ToolbarControl_NS;
using System.Collections;
using KSP.UI.Screens.Flight;
using System.Reflection;

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

        //Current PCR module
        private ProbeControlRoomPart aModule;
        private Part aPart => aModule?.part;

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
        KeyCodeExtended CachedLabelPrimaryKey = GameSettings.TOGGLE_LABELS.primary;
        KeyCodeExtended CachedLabelSecondaryKey = GameSettings.TOGGLE_LABELS.secondary;
        private static System.Reflection.MethodInfo method_vessellabels_enablealllabels = null;
        private static System.Reflection.MethodInfo method_vessellabels_disablealllabels = null;

        //Highlight in flight?
        bool HasCachedHighlightInFlightSetting = false;
        bool CachedHighlightInFlightSetting = true;

        //Application 
        //private static ApplicationLauncherButton appLauncherButton = null;
        ToolbarControl toolbarControl;
        //App launcher in use
        private bool AppLauncher = false;

        //App launcher icons
        string IconActivate = "ProbeControlRoom/Icons/ProbeControlRoomToolbarDisabled";
        string IconDeactivate = "ProbeControlRoom/Icons/ProbeControlRoomToolbarEnabled";

        string enabledTexture = "ProbeControlRoom/Icons/ProbeControlRoomToolbarEnabled";
        string disabledTexture = "ProbeControlRoom/Icons/ProbeControlRoomToolbarDisabled";

        static void GetFields()
        {
            if (field_get_internalcamera_currentPitch == null)
                field_get_internalcamera_currentPitch = VirindiHelpers.DynamicEmitFields.CreateDynamicInstanceFieldGet<float, InternalCamera>("currentPitch");
            if (field_get_internalcamera_currentRot == null)
                field_get_internalcamera_currentRot = VirindiHelpers.DynamicEmitFields.CreateDynamicInstanceFieldGet<float, InternalCamera>("currentRot");
            if (field_get_internalcamera_currentZoom == null)
                field_get_internalcamera_currentZoom = VirindiHelpers.DynamicEmitFields.CreateDynamicInstanceFieldGet<float, InternalCamera>("currentZoom");

            if (field_set_internalcamera_currentPitch == null)
                field_set_internalcamera_currentPitch = VirindiHelpers.DynamicEmitFields.CreateDynamicInstanceFieldSet<float, InternalCamera>("currentPitch");
            if (field_set_internalcamera_currentRot == null)
                field_set_internalcamera_currentRot = VirindiHelpers.DynamicEmitFields.CreateDynamicInstanceFieldSet<float, InternalCamera>("currentRot");
            if (field_set_internalcamera_currentZoom == null)
                field_set_internalcamera_currentZoom = VirindiHelpers.DynamicEmitFields.CreateDynamicInstanceFieldSet<float, InternalCamera>("currentZoom");

            if (method_vessellabels_enablealllabels == null)
                method_vessellabels_enablealllabels = typeof(VesselLabels).GetMethod("EnableAllLabels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.Instance);
            if (method_vessellabels_disablealllabels == null)
                method_vessellabels_disablealllabels = typeof(VesselLabels).GetMethod("DisableAllLabels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.InvokeMethod | System.Reflection.BindingFlags.Instance);
        }

        /// <summary>
        /// Startup and initialization
        /// </summary>
        public void Start()
        {
            ProbeControlRoomUtils.Logger.debug("Start()");
            Instance = this;


            try
            {
                GetFields();
            }
            catch (Exception ex)
            {
                ProbeControlRoomUtils.Logger.debug("Exception finding fields: " + ex.ToString());
            }


            refreshVesselRooms();

            //Register game events
            GameEvents.onVesselWasModified.Fire(FlightGlobals.ActiveVessel);

            GameEvents.onVesselChange.Add(OnVesselChange);
            GameEvents.onVesselWasModified.Add(OnVesselModified);
            GameEvents.onGUIApplicationLauncherReady.Add(onGUIApplicationLauncherReady);
            GameEvents.onGUIApplicationLauncherDestroyed.Add(OnGUIAppLauncherDestroyed);
            GameEvents.onGameSceneSwitchRequested.Add(OnGameSceneSwitchRequested);

            MapView.OnExitMapView += OnExitMapView;

            //If Manely mode is set true, force straight into IVA
            if (ProbeControlRoomSettings.Instance.ForcePCROnly)
            {
                ProbeControlRoomUtils.Logger.message("Start() - ForcePCROnly Enabled.");
                startIVA();
            }
        }

        // when the map view exits, it tries to return to the previous camera mode (IVA).  But that fails, because the vessel has no crew.  So we need to manually restart the PCR mode.
		private void OnExitMapView()
		{
			if (isActive)
			{
                startIVA();
			}
		}

		void OnGameSceneSwitchRequested(GameEvents.FromToAction<GameScenes, GameScenes> scn)
        {
            if (isActive)
                stopIVA();

        }

        void OnGUIAppLauncherDestroyed()
        {
            if (toolbarControl != null)
            {
                //ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                toolbarControl.OnDestroy();
                Destroy(toolbarControl);
            }
        }
        /// <summary>
        /// Setup app launcher button when the GUI is ready
        /// </summary>
        private void onGUIApplicationLauncherReady()
        {
            if (!AppLauncher)
            {
                InitializeApplicationButton();
                AppLauncher = true;
            }

        }

        internal const string MODID = "PCR_NS";
        internal const string MODNAME = "Probe Control Room";
        /// <summary>
        /// Sets up the App launcher button to activate and deactivate PCR
        /// </summary>
        /// <returns>Reference to created button</returns>
        void InitializeApplicationButton()
        {
            toolbarControl = gameObject.AddComponent<ToolbarControl>();
            toolbarControl.AddToAllToolbars(OnAppLauncherTrue,
                OnAppLauncherFalse,
                ApplicationLauncher.AppScenes.FLIGHT,
                MODID,
                "probeControlRoomButton",
                IconActivate, IconDeactivate,
                enabledTexture, disabledTexture,
                MODNAME
            );

            if (toolbarControl == null)
            {
                ProbeControlRoomUtils.Logger.debug("InitializeApplicationButton(): Was unable to initialize button");
            }

            if (isActive)
                toolbarControl.SetTexture(IconDeactivate, disabledTexture);

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
            if (isActive)
            {
                stopIVA();
            }

            ProbeControlRoomUtils.Logger.debug("OnDestroy()");

            GameEvents.onVesselChange.Remove(OnVesselChange);
            GameEvents.onVesselWasModified.Remove(OnVesselModified);
            //GameEvents.onGUIApplicationLauncherReady.Remove(onGUIApplicationLauncherReady);
            onGUIApplicationLauncherReady();
            GameEvents.onGUIApplicationLauncherDestroyed.Remove(OnGUIAppLauncherDestroyed);
            GameEvents.onGameSceneSwitchRequested.Remove(OnGameSceneSwitchRequested);
            
            MapView.OnExitMapView -= OnExitMapView;

            if (toolbarControl != null)
            {
#if false
                ApplicationLauncher.Instance.RemoveModApplication(appLauncherButton);
                appLauncherButton = null;
#endif
                toolbarControl.OnDestroy();
                Destroy(toolbarControl);
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
            CachedSun.gameObject.SetActive(on);
            SunIsEnabled = on;
        }

        void SetVesselLabelsValue(bool on)
        {
            //ProbeControlRoomUtils.Logger.message("SetVesselLabelsValue() - ENTER");

            if (GameSettings.FLT_VESSEL_LABELS == on)
                return;

            VesselLabels lbls = (VesselLabels)FindObjectOfType(typeof(VesselLabels));

            ProbeControlRoomUtils.Logger.message("SetVesselLabelsValue() - lbls - " + (lbls != null).ToString());

            if (on && lbls != null && method_vessellabels_enablealllabels != null)
            {
                ProbeControlRoomUtils.Logger.message("SetVesselLabelsValue() - INVOKE ENABLE");

                method_vessellabels_enablealllabels.Invoke(lbls, null);
                GameSettings.FLT_VESSEL_LABELS = true;
            }
            else if (!on && lbls != null && method_vessellabels_disablealllabels != null)
            {
                ProbeControlRoomUtils.Logger.message("SetVesselLabelsValue() - INVOKE DISABLE");

                method_vessellabels_disablealllabels.Invoke(lbls, null);
                GameSettings.FLT_VESSEL_LABELS = false;
            }
            else if (!lbls)
            {
                ProbeControlRoomUtils.Logger.message("SetVesselLabelsValue() - NO LBLS SET");

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

            if (FlightGlobals.ActiveVessel.packed)
            {
                ProbeControlRoomUtils.Logger.debug("startIVA() - return vessel still packed!");
                return false;
            }

            //Ensure part still exists
            if (aModule == null)
            {
                ProbeControlRoomUtils.Logger.message("startIVA() Lost our part, refreshing");
                refreshVesselRooms();
            }
            if (aModule == null)
            {
                ProbeControlRoomUtils.Logger.message("startIVA() Can't find a part. DIE.");
                return false;
            }

            // this prevents the portrait gallery from responding to VesselWasModified callbacks
            KerbalPortraitGallery.Instance.enabled = false;

            // spawn the internal model
            if (aPart.internalModel == null)
            {
                if (aPart.internalModel == null)
                {
                    aPart.CreateInternalModel();
                    if (aPart.internalModel == null)
                    {
                        ProbeControlRoomUtils.Logger.message("startIVA() failed to spawn the internal model. DIE.");
                        return false;
                    }
                }
                
                aPart.internalModel.Initialize(aPart);
                aPart.internalModel.SpawnCrew();
            }
            else
            {
                aPart.internalModel.gameObject.SetActive(true);
                aPart.internalModel.SetVisible(true);
            }

            // remove any PCMs that were added
            aPart.protoModuleCrew.Clear();

            //Make the PCR part the focused part
            aPart.MakeReferencePart();

            ProbeControlRoomUtils.Logger.debug("startIVA() - fire up IVA");

            // store settings so they can be restored later
            if (!isActive)
            {
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
                    GameSettings.TOGGLE_LABELS.primary = new KeyCodeExtended(KeyCode.None);
                    GameSettings.TOGGLE_LABELS.secondary = new KeyCodeExtended(KeyCode.None);
                }
                SetVesselLabelsValue(false);

                //Highlighters
                if (!HasCachedHighlightInFlightSetting)
                {
                    HasCachedHighlightInFlightSetting = true;
                    CachedHighlightInFlightSetting = GameSettings.INFLIGHT_HIGHLIGHT;
                }
                GameSettings.INFLIGHT_HIGHLIGHT = false;

                // lock out camera modes (V and C buttons)
                InputLockManager.SetControlLock(ControlTypes.CAMERAMODES, "ProbeControlRoom");
            }

            if (UIPartActionController.Instance != null)
                UIPartActionController.Instance.Deactivate();

            //Activate internal camera
            //CameraManager.Instance.SetCameraInternal(aPart.internalModel, actualTransform);
            StartCoroutine(DelayedIVACameraSwitch());

            ProbeControlRoomUtils.Logger.debug("startIVA() - DONE");

            //GUI may not be started yet.
            if (toolbarControl != null)
            {
                //Change app launcher button icon
                toolbarControl.SetTexture(IconActivate, enabledTexture);
            }

            if (hassavedlookangles && field_set_internalcamera_currentPitch != null && field_set_internalcamera_currentRot != null && field_set_internalcamera_currentZoom != null)
            {
                ProbeControlRoomUtils.Logger.debug(string.Format("startIVA() - Restoring pitch and rot. {0}, {1}", savedpitch, savedrot));

                field_set_internalcamera_currentPitch(InternalCamera.Instance, savedpitch);
                field_set_internalcamera_currentRot(InternalCamera.Instance, savedrot);
                field_set_internalcamera_currentZoom(InternalCamera.Instance, savedzoom);
                InternalCamera.Instance.Update();
            }

            //Disable sun effects inside of IVA
            SetSun(false);

            ProbeControlRoomUtils.Logger.debug("startIVA() - REALLY DONE");

            return true;

        }

        static FieldInfo x_Kerbal_running_FieldInfo;

        static ProbeControlRoom()
        {
            x_Kerbal_running_FieldInfo = typeof(Kerbal).GetField("running", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        IEnumerator DelayedIVACameraSwitch()
        {
            // we have to wait a frame after spawning the kerbals to switch to IVA camera, because they need to have their Start() functions called for everything to work properly
            yield return null;

            // remove the kerbals from the portrait gallery
            foreach (var seat in aPart.internalModel.seats)
            {
                KerbalPortraitGallery.Instance.UnregisterActiveCrew(seat.kerbalRef);
                x_Kerbal_running_FieldInfo.SetValue(seat.kerbalRef, false);
            }

            CameraManager.Instance.SetCameraIVA(aPart.internalModel.seats[0].kerbalRef, true);

            // stock bug: SetCameraIVA turns off the head renderers and then calls InternalModel.SetVisible, which enables all renderers in the IVA and turns the heads back on
            CameraManager.Instance.IVACameraActiveKerbal.IVAEnable(true);

            isActive = true;
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

            if (aPart != null && aPart.internalModel != null)
            {
                aPart.internalModel.gameObject.SetActive(false);
            }

            // re-enable the portrait gallery
            KerbalPortraitGallery.Instance.enabled = true;
            KerbalPortraitGallery.Instance.StartRefresh(aPart?.vessel);

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
                SetVesselLabelsValue(CachedVesselLabelsSetting);
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

            // unlock camera modes (V and C buttons)
            InputLockManager.RemoveControlLock("ProbeControlRoom");

            //Switch back to normal cameras
            if (CameraManager.Instance != null && CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)
            {
                CameraManager.Instance.SetCameraFlight();
            }

            if (UIPartActionController.Instance != null)
                UIPartActionController.Instance.Activate();

            ProbeControlRoomUtils.Logger.debug("stopIVA() - CHECKMARK");

            //Change app launcher button
            toolbarControl.SetTexture(IconDeactivate, disabledTexture);
        }

        bool KerbalIsUnbuckled(Kerbal kerbal)
        {
            if (kerbal == null) return false;

            if (kerbal.transform.parent == kerbal.protoCrewMember.seat.seatTransform) return false;

            return true;
        }

        // FreeIva changes its buckled states inside Update, so we need to use the state from the end of the previous frame
        bool kerbalWasUnbuckled;

        /// <summary>
        /// Check for invalid PCR states and controls inputs for acivation/deactivation
        /// </summary>
        public void LateUpdate()
        {
            if (isActive)
            {
                bool canChangeCameras = !MapView.MapIsEnabled && !kerbalWasUnbuckled;

                if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Flight)
                {
                    stopIVA();
                }
                //Check for imput to stop IVA
                else if (canChangeCameras && GameSettings.CAMERA_MODE.GetKeyDown(false))
                {
                    ProbeControlRoomUtils.Logger.message("LateUpdate() - CAMERA_MODE.key seen, stopIVA()");
                    if (ProbeControlRoomSettings.Instance.ForcePCROnly)
                    {
                        ProbeControlRoomUtils.Logger.message("LateUpdate() - CAMERA_MODE.key seen, stopIVA() KILLED - ForcePCROnly Enabled.");
                    }
                    else
                    {

                        stopIVA();
                    }
                }
                // Cycle to the next kerbal
                else if (canChangeCameras && GameSettings.CAMERA_NEXT.GetKeyDown(false))
                {
                    // the normal IVA kerbal cycling code uses the crew on the vessel, but the kerbals in the PCR are not in the vessel!
                    int currentIndex = aPart.internalModel.seats.FindIndex(seat => seat.kerbalRef == CameraManager.Instance.IVACameraActiveKerbal);
                    int nextIndex = (currentIndex + 1) % aPart.internalModel.seats.Count;
                    CameraManager.Instance.SetCameraIVA(aPart.internalModel.seats[nextIndex].kerbalRef, true);

                    // stock bug: SetCameraIVA turns off the head renderers and then calls InternalModel.SetVisible, which enables all renderers in the IVA and turns the heads back on
                    CameraManager.Instance.IVACameraActiveKerbal.IVAEnable(true);
                }
            }
            else
            {
                // if pressing the camera mode (C) button and we either failed to enter IVA mode or just left it, then try to start PCR mode
                if (!GameSettings.MODIFIER_KEY.GetKey() && GameSettings.CAMERA_MODE.GetKeyDown() && CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Flight)
                {
                    // If we were previously in IVA mode and pressing C switched to Flight, the portrait gallery will have spun up a coroutine to refresh itself
                    // which interferes with which internal spaces are shown or hidden.  Stop it from doing that.
                    if (startIVA())
                    {
                        KerbalPortraitGallery.Instance.StopAllCoroutines();
                    }
                }
            }

            kerbalWasUnbuckled = KerbalIsUnbuckled(CameraManager.Instance.IVACameraActiveKerbal);
        }

        /// <summary>
        /// Vessel has changed to a new one, check IVA status (docking, vessel switching, etc.)
        /// </summary>
        /// <param name="v">New Vessel</param>
        private void OnVesselChange(Vessel v)
        {
            ProbeControlRoomUtils.Logger.message("OnVesselChange(Vessel)");
            aModule = null;
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
            if (FlightGlobals.ActiveVessel == null)
            {
                ProbeControlRoomUtils.Logger.message("vesselModified() - no active vessel, returning");
                stopIVA();
                return;
            }
            Part oldPart = aPart;
            refreshVesselRooms();
            //Only stop the IVA if the part is missing, restart it otherwise
            if (isActive)
            {
                if (aPart == null)
                {
                    ProbeControlRoomUtils.Logger.message("vesselModified() - Can no longer use PCR on this vessel");
                    stopIVA();
                }

                if (aPart != oldPart)
                {
                    ProbeControlRoomUtils.Logger.message("vesselModified() - Have to change part.");
                    //Can still PCR IVA but the part has changed, restart

                    stopIVA();
                    startIVA();
                }
            }
            else if (aPart != null && !MapView.MapIsEnabled && ProbeControlRoomSettings.Instance.ForcePCROnly)
            {
                startIVA();
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
                aModule = null;
                ProbeControlRoomUtils.Logger.error("refreshVesselRooms() - ERROR: FlightGlobals.activeVessel is NULL");
                return;
            }

            // part is still on the vessel
            if (aModule != null && aModule.vessel == vessel)
            {
                return;
            }

            aModule = vessel.FindPartModuleImplementing<ProbeControlRoomPart>();
        }
    }
}

