using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace ProbeControlRoom
{
	public class CivilianKerbal : InternalModule
	{
		[KSPField]
		public string prefabName;

		[KSPField]
		public string childObjectName = string.Empty;

		public ProtoCrewMember CrewMember { get; private set; }

		void Start()
		{
			var prefab = AssetBase.GetPrefab(prefabName) ?? GameDatabase.Instance.GetModel(prefabName);
			var bodyPrefab = childObjectName == string.Empty ? prefab : prefab.transform.Find(childObjectName).gameObject;

			var seat = internalProp.GetComponent<InternalSeat>();

			CrewMember = new ProtoCrewMember(ProtoCrewMember.KerbalType.Unowned, "kerbalname");
			var oldSpawnDelegate = ProtoCrewMember.Spawn;
			ProtoCrewMember.Spawn = (pcm) =>
			{
				var kerbalObj = GameObject.Instantiate(bodyPrefab);
				var kerbal = kerbalObj.GetComponentInChildren<Kerbal>();
				if (kerbal == null)
				{
					kerbal = kerbalObj.AddComponent<Kerbal>();
				}

				kerbalObj.SetLayerRecursive(16);

				return kerbal;
			};

			seat.crew = CrewMember;
			seat.SpawnCrew();
			ProtoCrewMember.Spawn = oldSpawnDelegate;
		}
	}
}
