using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ProbeControlRoom
{
	internal static class ThroughTheEyes
	{
		static TypeInfo x_FirstPersonEVA_TypeInfo;
		static FieldInfo x_instance_FieldInfo;
		static FieldInfo x_fpCameraManagerFieldInfo;

		static TypeInfo x_FirstPersonCameraManager_TypeInfo;
		static FieldInfo x_isFirstPerson_FieldInfo;
		static MethodInfo x_resetCamera_MethodInfo;

		static ThroughTheEyes()
		{
			var throughTheEyesAssembly = AssemblyLoader.loadedAssemblies.FirstOrDefault(a => a.assembly.GetName().Name == "ThroughTheEyes");
			if (throughTheEyesAssembly == null) return;

			x_FirstPersonEVA_TypeInfo = throughTheEyesAssembly.assembly.GetType("FirstPerson.FirstPersonEVA").GetTypeInfo();
			x_instance_FieldInfo = x_FirstPersonEVA_TypeInfo.GetField("instance", BindingFlags.Static | BindingFlags.Public);
			x_fpCameraManagerFieldInfo = x_FirstPersonEVA_TypeInfo.GetField("fpCameraManager", BindingFlags.Instance | BindingFlags.Public);

			x_FirstPersonCameraManager_TypeInfo = throughTheEyesAssembly.assembly.GetType("FirstPerson.FirstPersonCameraManager").GetTypeInfo();
			x_isFirstPerson_FieldInfo = x_FirstPersonCameraManager_TypeInfo.GetField("isFirstPerson", BindingFlags.Instance | BindingFlags.Public);
			x_resetCamera_MethodInfo = x_FirstPersonCameraManager_TypeInfo.GetMethod("resetCamera", BindingFlags.Instance | BindingFlags.Public);
		}

		public static bool IsFirstPerson
		{
			get
			{
				if (x_isFirstPerson_FieldInfo == null)
				{
					return false;
				}

				object firstPersonEVAInstance = x_instance_FieldInfo.GetValue(null);
				object firstPersonCameraManager = x_fpCameraManagerFieldInfo.GetValue(firstPersonEVAInstance);
				return (bool)x_isFirstPerson_FieldInfo.GetValue(firstPersonCameraManager);
			}
		}

		public static void ExitFirstPerson()
		{
			if (x_isFirstPerson_FieldInfo == null) return;

			object firstPersonEVAInstance = x_instance_FieldInfo.GetValue(null);
			object firstPersonCameraManager = x_fpCameraManagerFieldInfo.GetValue(firstPersonEVAInstance);
			x_resetCamera_MethodInfo.Invoke(firstPersonCameraManager, new object[] { FlightGlobals.ActiveVessel });
		}
	}
}
