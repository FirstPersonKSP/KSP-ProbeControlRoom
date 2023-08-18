ProbeControlRoom
=========

Features
--------
	Adds IVA to UNMANNED Vessels/CommandModules that have no Crew/Internal.
		AutoUpdates all "ModuleCommand"-Parts that have no "INTERNAL"
		RasterPropMonitor gets enabled automatically if the .dll is loaded

	
Requires
----------
	- ModuleManager
	- ToolbarController
 	- RasterPropMonitor

How to use
----------
	1. Remove any previous versions of "ProbeControlRoom" in your KerbalSpaceProgram/GameData directory
	2. Merge the downloaded "GameData" into your KerbalSpaceProgram directory
	3. Start the game and build a vessel with a ProbeCore
	4. Start the ControlRoom by:
	 a. MannedVessel: Bottom right, above the KerbalAvatars is a IVA button
	 b. UnmannedVessel: Use the CameraMode button (C) to access/leave IVA
	 c. Righclick the ProbeCore and click "IVA from here"
	 d. click the IVA button in the toolbar

	 
Extended
----------
	On the first start a config file is created at GameData/ProbeControlRoom/Settings.cfg
	ForcePCROnly (default: False)
		forces the use of PCR
		- UI/Toolbar buttons to overwrite
		- Traps mapView and "Camera"-Key
	DisableWobble (default: True)
		disables the cameraShake when inside a PCR
	DisableSounds (default: False) [ALPHA - CurrentMethod (GameSettings-Volume) not stable]
		disables the Ship_Audio when inside a PCR
		- Seems to ignore Engines, Staging, Parachutes and similar, while explosions get muted.
		- With ForcePCROnly=True muting works, but always for every rocket with a PCR...


Thanks
-------
	- G'th for the idea&kick to actually do it
	- all the KSP modders putting tons of sourcecode out there to read & learn
	- the folks in the #kspmodders irc that linked me there ;-)
	- "Albert VDS" for the excelent work hes done on the 3D Model/Textures/Pages
	- "Dexter9313" and "Z-Key Aerospace" for the pull-requests regarding Wobble&Audio removal
	- "MeCripp", "jlcarneiro" and again "Z-Key Aerospace" & "Dexter9313" for providing updates while i could not
	
	
Authors
-------
	Contributions by: Virindi, Tabakhase, Icedown, Albert_VDS, MeCripp, Nils277, JPLRepo, Dexter9313, Z-Key Aerospace, JlCarneiro, LinuxGuruGamer, JonnyOThan
	
	
License
-------
	This project is licensed under the Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
	To view a copy of this license, visit http://creativecommons.org/licenses/by-nc-sa/3.0/
