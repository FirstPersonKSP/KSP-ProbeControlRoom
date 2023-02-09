using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

        [KSPField]
        public string headTransformPath = string.Empty;

        [KSPField]
        public string kerbalName = string.Empty;

        public ProtoCrewMember CrewMember { get; private set; }

        public override void OnAwake()
        {
            if (HighLogic.LoadedSceneIsFlight)
            {
                CreateKerbal();
            }
        }

        public void CreateKerbal()
        {
            if (CrewMember != null) return;

            var prefab = AssetBase.GetPrefab(prefabName) ?? GameDatabase.Instance.GetModel(prefabName);
            var bodyPrefab = childObjectName == string.Empty ? prefab : prefab.transform.Find(childObjectName).gameObject;

            var seat = internalProp.GetComponent<InternalSeat>();

            CrewMember = new ProtoCrewMember(ProtoCrewMember.KerbalType.Unowned, kerbalName);
            KerbalRoster.SetExperienceTrait(CrewMember, KerbalRoster.touristTrait); // TODO: make a custom experience trait for mission control?

            // what a horrible, elegant hack - add this PCM to the part's crew, so that it gets properly seated in InternalModel.Initialize
            part.protoModuleCrew.Add(CrewMember);
            CrewMember.seatIdx = internalModel.seats.IndexOf(seat);

            var kerbalObj = GameObject.Instantiate(bodyPrefab);
            kerbalObj.SetLayerRecursive(16);

            var kerbal = kerbalObj.GetComponentInChildren<Kerbal>();
            if (kerbal == null)
            {
                try
                {
                    // NOTE: this will always throw an exception because it access a pre-bound camera reference inside Awake, but it can be silently swallowed
                    // Maybe in the future we can instantiate one of the "real" IVA kerbals, and then just swap out the model
                    kerbal = kerbalObj.AddComponent<Kerbal>();
                }
                catch (Exception) { }
                kerbal.enabled = true;
                kerbal.eyeTransform = new GameObject().transform;
                kerbal.eyeTransform.SetParent(kerbalObj.transform);
                kerbal.eyeTransform.localPosition = new Vector3(0, 0.602098f, 0.1537878f);
                kerbal.headTransform = kerbalObj.transform.Find(headTransformPath);
                kerbal.textureTargets = new Renderer[0];
                kerbal.InPart = part;
            }

            if (kerbal.kerbalCam == null)
            {
                kerbal.kerbalCam = kerbalObj.AddComponent<Camera>();
                kerbal.kerbalCam.enabled = false;
            }

            kerbal.name = CrewMember.name;
            kerbal.crewMemberName = CrewMember.name;
            kerbal.stupidity = CrewMember.stupidity;
            kerbal.courage = CrewMember.courage;
            kerbal.isBadass = CrewMember.isBadass;
            kerbal.veteran = CrewMember.veteran;
            kerbal.protoCrewMember = CrewMember;
            kerbal.rosterStatus = CrewMember.rosterStatus;
            kerbal.showHelmet = CrewMember.hasHelmetOn;
            CrewMember.KerbalRef = kerbal;
        }
    }
}
