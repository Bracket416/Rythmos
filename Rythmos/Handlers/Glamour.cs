using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Glamourer.Api.IpcSubscribers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rythmos.Handlers
{
    internal class Glamour
    {
        public static bool Ready = false;
        private static GetState Get;
        private static ApplyState Apply;
        private static UnlockState Unlock_State;
        private static RevertState Revert_State;
        private static Glamourer.Api.Helpers.EventSubscriber<nint> E;
        public static IPluginLog Log;
        public static IDalamudPluginInterface Interface;

        public static void Setup(IDalamudPluginInterface I)
        {
            Interface = I;
            Get = new GetState(I);
            Apply = new ApplyState(I);
            Unlock_State = new UnlockState(I);
            Revert_State = new RevertState(I);

            E = StateChanged.Subscriber(I, Characters.Update_Glamour);
            try
            {
                new ApiVersion(I).Invoke();
                Ready = true;
            }
            catch (Exception Error)
            {
                Log.Error("Glamourer Load: " + Error.Message);
                Ready = false;
            }
            Log.Information($"Glamourer is {(Ready ? "ready" : "not ready")}!");
        }

        public static string Pack(ushort Index)
        {
            if (Ready)
            {
                try
                {
                    var Data = Get.Invoke(Index, 416);
                    if (Data.Item1 == Glamourer.Api.Enums.GlamourerApiEc.Success)
                    {
                        return JsonConvert.SerializeObject(Data.Item2, Formatting.None);
                    }
                    else
                    {
                        Ready = false;
                        Log.Error("Glamourer Serialization: " + Data.Item1.ToString());
                        return "{}";
                    }
                }
                catch (Exception Error)
                {
                    Ready = false;
                    Log.Error("Glamourer Pack: " + Error.Message);
                    return "{}";
                }
            } else
            {
                Setup(Interface);
                return "{}";
            }
        }

        public static void Set(ushort Index, string Data)
        {
            if (Ready)
            {
                try
                {
                    var A = JsonConvert.DeserializeObject<JObject>(Data);
                    Apply!.Invoke(A, Index, 416);
                }
                catch (Exception Error)
                {
                    Log.Error("Set: " + Error.Message);
                    Ready = false;
                }
            }
            else Setup(Interface);
        }
        public static void Unlock(ushort Index)
        {
            if (Ready)
            {
                try
                {
                    Unlock_State.Invoke(Index, 416);
                }
                catch (Exception Error)
                {
                    Log.Error("Unlock: " + Error.Message);
                    Ready = false;
                }
            } else Setup(Interface);
        }

        public static void Revert(ushort Index)
        {
            if (Ready)
            {
                try
                {
                    Revert_State.Invoke(Index, 416);
                }
                catch (Exception Error)
                {
                    Log.Error("Revert: " + Error.Message);
                    Ready = false;
                }
            }
            else Setup(Interface);
        }
        public static void Dispose()
        {
            E.Dispose();
        }
    }
}
