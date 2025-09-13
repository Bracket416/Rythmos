using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using static System.Text.Encoding;
using System.Threading.Tasks;

namespace Rythmos.Handlers
{
    internal class Customize
    {

        public static bool Ready = false;
        private static ICallGateSubscriber<ushort, (int, Guid?)> Get_Active_Profile;
        private static ICallGateSubscriber<Guid, (int, string?)> Get_Profile_Data;
        private static ICallGateSubscriber<ushort, string, (int, Guid?)> Set_Active_Profile;
        public static IPluginLog Log;

        public static IDalamudPluginInterface Interface;



        public static void Setup(IDalamudPluginInterface I)
        {
            Interface = I;
            Get_Active_Profile = I.GetIpcSubscriber<ushort, (int, Guid?)>("CustomizePlus.Profile.GetActiveProfileIdOnCharacter");
            Get_Profile_Data = I.GetIpcSubscriber<Guid, (int, string?)>("CustomizePlus.Profile.GetByUniqueId");
            Set_Active_Profile = I.GetIpcSubscriber<ushort, string, (int, Guid?)>("CustomizePlus.Profile.SetTemporaryProfileOnCharacter");
            try
            {
                I.GetIpcSubscriber<(int, int)>("CustomizePlus.General.GetApiVersion").InvokeFunc();
                Ready = true;
            }
            catch (Exception Error)
            {
                Log.Error("Customize+ Load: " + Error.Message);
                Ready = false;
            }
            Log.Information($"Customize+ is {(Ready ? "ready" : "not ready")}!");
        }

        public static void Set_Bones(uint Index, string Data)
        {
            if (Ready)
            {
                try
                {
                    Set_Active_Profile.InvokeFunc((ushort)Index, UTF8.GetString(System.Convert.FromBase64String(Data)));
                }
                catch (Exception Error)
                {
                    Log.Error("Set Bones: " + Error.Message);
                    Ready = false;
                }
            }
            else Setup(Interface);
        }

        public static string Pack_Bones(uint Index)
        {
            if (Ready)
            {
                try
                {
                    var Profile = Get_Active_Profile.InvokeFunc((ushort)Index).Item2.Value;
                    return System.Convert.ToBase64String(UTF8.GetBytes(Get_Profile_Data.InvokeFunc(Profile).Item2));
                }
                catch
                {
                    Ready = false;
                    return "";
                }
            }
            else
            {
                return "";
            }
        }

    }
}
