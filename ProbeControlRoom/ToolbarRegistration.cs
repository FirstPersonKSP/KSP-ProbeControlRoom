using UnityEngine;
using ToolbarControl_NS;

namespace ProbeControlRoom
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        void Start()
        {
            ToolbarControl.RegisterMod(ProbeControlRoom.MODID, ProbeControlRoom.MODNAME);
        }
    }
}