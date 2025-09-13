using Dalamud.Game;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using Glamourer.Api.IpcSubscribers.Legacy;
using InteropGenerator.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Penumbra.Api.Api;
using Penumbra.Api.IpcSubscribers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
namespace Rythmos.Handlers
{
    unsafe internal class Characters
    {
        public static IObjectTable Objects;

        public static IClientState Client;

        private static Dictionary<string, Guid> Collection_Mapping = new();

        private static long T = 0;

        public class Mod_Configuration
        {
            public string Bones = "";

            public string Glamour = "";

            public Dictionary<string, Tuple<string, int, Dictionary<string, List<string>>>> Mods = new();

            public Mod_Configuration(string Bones, string Glamour, Dictionary<string, Tuple<string, int, Dictionary<string, List<string>>>> Mods)
            {
                this.Bones = Bones;
                this.Glamour = Glamour;
                this.Mods = Mods;
            }
        }

        private static Dictionary<string, Mod_Configuration> Mods = new();

        public static Dictionary<string, ushort> ID_Mapping = new();

        public static IPluginLog Log;

        private static CreateTemporaryCollection Collection_Creator;

        private static RedrawObject Redraw;

        private static RemoveTemporaryMod Temporary_Mod_Remover;

        private static AddTemporaryMod Temporary_Mod_Adder;

        private static AssignTemporaryCollection Collection_Assigner;

        private static DeleteTemporaryCollection Collection_Remover;

        private static GetCollectionForObject Collection_Getter;

        private static GetAllModSettings Settings_Getter;

        private static GetMetaManipulations Get_Meta;

        public static GetPlayerResourcePaths Get_Resources;

        public static string Penumbra_Path = "";

        public static string Rythmos_Path = "";

        public static List<string> Outdated = new();

        public static List<string> Entities = new();

        private static Dictionary<string, ushort> Pets = new();

        private static Dictionary<string, ushort> Minions = new();

        public static Dictionary<string, string> Glamours = new();

        public static void Setup(IDalamudPluginInterface I, IChatGui Chat)
        {
            try
            {
                Collection_Creator = new CreateTemporaryCollection(I);
                Collection_Assigner = new AssignTemporaryCollection(I);
                Redraw = new RedrawObject(I);
                Temporary_Mod_Adder = new AddTemporaryMod(I);
                Temporary_Mod_Remover = new RemoveTemporaryMod(I);
                Collection_Getter = new GetCollectionForObject(I);
                Settings_Getter = new GetAllModSettings(I);
                Get_Meta = new GetMetaManipulations(I);
                Collection_Remover = new DeleteTemporaryCollection(I);
                Get_Resources = new GetPlayerResourcePaths(I);
                Penumbra_Path = new GetModDirectory(I).Invoke();
            }
            catch (Exception Error)
            {
                Chat.PrintError("[Rythmos] Please update Penumbra!");
                Log.Error(Error.Message);

            }
        }

        public static string Get_Name(ushort ID)
        {
            if (ID < Objects.Length)
            {
                var O = Objects[ID];
                if (O == null) return "";
                if (O is IPlayerCharacter) return O.Name.TextValue + " " + ((IPlayerCharacter)O).HomeWorld.Value.Name.ToString();

                return O.Name.TextValue;
            }
            else return "";
        }

        public static void Set_Collection(ushort ID)
        {
            var Name = Get_Name(ID);
            if (!Collection_Mapping.ContainsKey(Name))
                if (Mods.ContainsKey(Name) ? true : File.Exists(Rythmos_Path + $"\\Mods\\{Name}\\Configuration.json"))
                {
                    Log.Information($"Setting the collection of {Name}!");
                    Collection_Creator.Invoke(Name, Name, out var Collection_ID);
                    Collection_Mapping.Add(Name, Collection_ID);
                    var Found = Collection_Getter.Invoke(ID_Mapping[Name]);
                    Log.Information(Found + " = " + Collection_Mapping[Name]);
                    Log.Information("Assignment: " + Collection_Assigner.Invoke(Collection_Mapping[Name], (int)ID_Mapping[Name]).ToString());
                    Load(Name);
                    Enable(Name);
                }
        }

        private static void Remove_Collection(string Name)
        {
            if (Collection_Mapping.ContainsKey(Name))
            {
                Collection_Remover.Invoke(Collection_Mapping[Name]);
                Collection_Mapping.Remove(Name);
            }
        }

        public static void Set_Customize(string Name)
        {
            if (!Mods.ContainsKey(Name)) Load(Name);
            if (Mods.ContainsKey(Name)) if (Mods[Name].Bones != null) if (Mods[Name].Bones.Length > 0) Customize.Set_Bones(ID_Mapping[Name], Mods[Name].Bones.ToString());
        }
        private class Modification
        {
            public string Name;
            public string Description;
            public Dictionary<string, string> Files;
            public Dictionary<string, string> FileSwaps;
            public List<Object> Manipulations;
            public int AttributeMask = 0;
            [DefaultValue(0)]
            [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
            public int Priority = 0;
            public Dictionary<string, Tuple<string, bool>> Merge()
            {
                if (Files == null) Files = new();
                if (FileSwaps == null) FileSwaps = new();
                if (Manipulations == null) Manipulations = new List<Object>();
                if (Priority == null) Priority = 0;
                Dictionary<string, Tuple<string, bool>> Output = new Dictionary<string, Tuple<string, bool>>();
                foreach (var F in Files) Output.Add(F.Key, Tuple.Create(F.Value, true));
                foreach (var F in FileSwaps) Output.Add(F.Key, Tuple.Create(F.Value, false));
                return Output;
            }
        }
        private class Group
        {
            public string Name;
            public int Priority;
            public string Type;
            public uint DefaultSettings;
            public Object Identifier;
            public Object DefaultEntry;
            public List<Modification> Options;
            public bool AllVariants;
            public bool OnlyAttributes;
        }


        private class IMC_Entry
        {
            public int MaterialId = 0;
            public int DecalId = 0;
            public int VfxId = 0;
            public int MaterialAnimationId = 0;
            public int AttributeMask = 0;
            public int SoundId = 0;
        }
        private class IMC_Manipulation
        {
            public IMC_Entry Entry;
            public string ObjectType = "";
            public int PrimaryId = 0;
            public int SecondaryId = 0;
            public int Variant = 0;
            public string EquipSlot = "";
            public string BodySlot = "";
        }

        private class IMC
        {
            public string Type = "Imc";
            public IMC_Manipulation Manipulation;
        }

        private static Tuple<string, Dictionary<string, string>> Parse_Mod(string Name, Tuple<string, int, Dictionary<string, List<string>>> Settings)
        {
            var Manipulations = new List<Tuple<int, string>>();
            var Path = Rythmos_Path + $"\\Mods\\{Name}\\" + Settings.Item1;
            var Priority = Settings.Item2;
            var Default = JsonConvert.DeserializeObject<Modification>(File.ReadAllText(Path + "\\default_mod.json"));
            Default.Priority += Priority;
            var Mods = new List<Modification> { Default };
            foreach (var F in Directory.GetFiles(Path).ToList().FindAll(X => X.StartsWith($"{Path}\\group_")))
            {
                var Data = JsonConvert.DeserializeObject<Group>(File.ReadAllText(F));
                if (Data.Type == "Imc")
                {
                    IMC I = new IMC();
                    I.Manipulation = new();
                    if (Data.Identifier != null) I.Manipulation = ((JObject)Data.Identifier).ToObject<IMC_Manipulation>();
                    I.Manipulation.Entry = new();
                    if (Data.DefaultEntry != null) I.Manipulation.Entry = ((JObject)Data.DefaultEntry).ToObject<IMC_Entry>();
                    foreach (var D in Data.Options) if (Settings.Item3[Data.Name].Contains(D.Name)) I.Manipulation.Entry.AttributeMask |= D.AttributeMask;
                    Manipulations.Add(Tuple.Create(Priority + Data.Priority, JsonConvert.SerializeObject(I)));
                    if (Data.AllVariants)
                    {
                        I.Manipulation.Variant = 1 - I.Manipulation.Variant;
                        Manipulations.Add(Tuple.Create(Priority + Data.Priority, JsonConvert.SerializeObject(I, Formatting.None)));
                    }
                }
                else
                    foreach (var D in Data.Options) if (Settings.Item3[Data.Name].Contains(D.Name))
                        {
                            D.Priority += Priority + Data.Priority;
                            Mods.Add(D);
                            if (D.Manipulations != null) foreach (var M in D.Manipulations)
                                {
                                    var Converted = JsonConvert.SerializeObject(M, Formatting.None);//Regex.Replace(JsonConvert.SerializeObject(M, Formatting.None), @"""(-?\d+(\.\d+)?([eE][+-]?\d+)?)""", "$1");
                                    Manipulations.Add(Tuple.Create(D.Priority, Converted));
                                }
                        }
            }
            var Output = new Dictionary<string, string>();
            var Setter = new Dictionary<string, int>();
            foreach (var Mod in Mods) foreach (var Swap in Mod.Merge())
                {
                    if (!Setter.ContainsKey(Swap.Key)) Setter.Add(Swap.Key, Mod.Priority);
                    if (Setter[Swap.Key] >= Mod.Priority)
                    {
                        if (!Output.ContainsKey(Swap.Key)) Output.Add(Swap.Key, Swap.Value.Item1);

                        if (Swap.Value.Item2 && File.Exists(Path + "\\" + Swap.Value.Item1)) Output[Swap.Key] = Path + "\\" + Swap.Value.Item1;
                        Setter[Swap.Key] = Mod.Priority;
                    }
                }
            if (Default.Manipulations != null) foreach (var M in Default.Manipulations)
                {
                    var Converted = JsonConvert.SerializeObject(M, Formatting.None);//Regex.Replace(JsonConvert.SerializeObject(M, Formatting.None), @"""(-?\d+(\.\d+)?([eE][+-]?\d+)?)""", "$1");
                    Manipulations.Add(Tuple.Create(Priority, Converted));
                }

            var Final_Manipulations = new List<Object>();

            string O = null;

            var P = int.MinValue;

            var Found = new List<string>();

            foreach (var M in Manipulations)
            {
                P = M.Item1;
                O = M.Item2;
                if (Found.Contains(O)) continue;
                foreach (var N in Manipulations) if (O == N.Item2 && P < N.Item1)
                    {
                        P = N.Item1;
                        O = N.Item2;
                    }
                Found.Add(O);
                Final_Manipulations.Add(JsonConvert.DeserializeObject(O));
            }
            //Log.Information(string.Join("\n", Final_Manipulations));
            //foreach (var Item in Output) Log.Information(Item.Key + " " + Item.Value);
            return Tuple.Create(Make(Final_Manipulations, 0), Output);
        }

        public static string Make(Object Data, byte Version)
        {
            try
            {
                var Raw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(Data, Formatting.None));
                using var S = new MemoryStream();
                using (var Zipped = new GZipStream(S, CompressionMode.Compress))
                {
                    Zipped.Write(new ReadOnlySpan<byte>(&Version, 1));
                    Zipped.Write(Raw, 0, Raw.Length);
                }
                var Output = Convert.ToBase64String(S.ToArray());
                return Output;
            }
            catch
            {
                return string.Empty;
            }
        }
        public static Mod_Configuration Gather_Mods(string Name)
        {
            // Later, I can provide a filtering argument of sorts, like a list of changed mods.
            var Settings = new Dictionary<string, Tuple<string, int, Dictionary<string, List<string>>>>(); // Mod -> (File Path, Priority, Group Settings)
            if (Client.LocalPlayer != null)
            {
                foreach (var O in Objects) if (O.ObjectKind is Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player) if (Get_Name(O.ObjectIndex) == Name)
                        {
                            Log.Information($"Now gathering the mods of {O.Name.TextValue}.");
                            var Output = Collection_Getter.Invoke(O.ObjectIndex);
                            if (Output.ObjectValid)
                            {
                                var C = Collection_Getter.Invoke(O.ObjectIndex).EffectiveCollection.Id;
                                Log.Information($"The collection is {C}.");
                                var S = Settings_Getter.Invoke(C);
                                foreach (var Mod in S.Item2.Keys) if (S.Item2[Mod].Item1) Settings.Add(Mod, Tuple.Create(Mod, S.Item2[Mod].Item2, S.Item2[Mod].Item3));
                                return new Mod_Configuration(Customize.Pack_Bones(O.ObjectIndex), Glamour.Pack(O.ObjectIndex), Settings);
                            }
                        }
                return new Mod_Configuration("", "", Settings);
            }
            return new Mod_Configuration("", "", Settings);
            //foreach (var Entry in Parse_Mod(Settings["Background Screen"])) Log.Information(Entry.ToString());
        }

        public static Task Pack(string Name, Mod_Configuration M, bool All = true)
        {
            return Task.Run(() =>
            {
                Log.Information("Packing: " + Rythmos_Path + $"\\Compressed\\{Name}.zip");
                using (FileStream B = new FileStream(Rythmos_Path + $"\\Compressed\\{Name}.zip", FileMode.Create))
                {
                    using (ZipArchive A = new(B, ZipArchiveMode.Create))
                    {
                        try
                        {
                            var Current_Files = Get_Resources.Invoke()[0].Keys.ToList().Select(X => X.ToLower());
                            var Paths = new List<string>();
                            M.Mods.ToList().ForEach(X => Paths.Add(Penumbra_Path + "\\" + X.Value.Item1));
                            foreach (var File in Directory.EnumerateFiles(Penumbra_Path, "*", SearchOption.AllDirectories)) if (Paths.Any(X => File.StartsWith(X + "\\")) && (All || Current_Files.Contains(File.ToString().ToLower()) || File.EndsWith(".json") || File.EndsWith(".pap") || File.EndsWith("sklb") || File.EndsWith("kbd") || File.EndsWith("avfx"))) A.CreateEntryFromFile(File, File.Substring(Penumbra_Path.Length + 1));
                            using (StreamWriter W = new StreamWriter(A.CreateEntry("Configuration.json").Open())) W.Write(JsonConvert.SerializeObject(M, Formatting.Indented));
                        }
                        catch (Exception Error)
                        {
                            Log.Information($"Pack: {Error.Message}");
                        }
                    }
                }
            });
        }
        public static void Unpack(string Name)
        {
            var Zip_Path = Rythmos_Path + $"\\Compressed\\{Name}.zip";
            if (File.Exists(Zip_Path)) ZipFile.ExtractToDirectory(Zip_Path, Rythmos_Path + $"\\Mods\\{Name}\\", true);
        }

        public static void Load(string Name)
        {
            if (File.Exists(Rythmos_Path + $"\\Mods\\{Name}\\Configuration.json"))
            {
                Mods.Remove(Name);
                Mods.Add(Name, JsonConvert.DeserializeObject<Mod_Configuration>(File.ReadAllText(Rythmos_Path + $"\\Mods\\{Name}\\Configuration.json")));
                if (!Glamours.Keys.Contains(Name)) Glamours[Name] = Mods[Name].Glamour;
            }
        }

        public static void Enable(string Name)
        {
            if (Collection_Mapping.ContainsKey(Name) && Mods.ContainsKey(Name))
            {
                try
                {
                    Set_Customize(Name);
                    Dictionary<string, (Tuple<string, Dictionary<string, string>>, int)> Mod_Data = new();
                    foreach (var Mod in Mods[Name].Mods) Mod_Data[Mod.Key] = (Parse_Mod(Name, Mod.Value), Mod.Value.Item2);
                    foreach (var Mod in Mod_Data) Temporary_Mod_Adder.Invoke(Mod.Key, Collection_Mapping[Name], Mod.Value.Item1.Item2, Mod.Value.Item1.Item1, Mod.Value.Item2).ToString();
                    Set_Glamour(Name, Glamours[Name] ?? string.Empty);
                    Redraw_Character(Name);
                    if (Pets.ContainsKey(Name)) Redraw.Invoke(Pets[Name]);
                    if (Minions.ContainsKey(Name)) Redraw.Invoke(Minions[Name]);
                }
                catch (Exception Error)
                {
                    Log.Error("Enable: " + Error.Message);
                }
            }
        }

        public static void Update_Glamour(nint Address)
        {
            if (Client.LocalPlayer != null ? Client.LocalPlayer.Address == Address && Networking.C.Sync_Glamourer : false) Queue.Send(Encoding.UTF8.GetBytes(string.Join(", ", Entities) + "|" + Glamour.Pack(Client.LocalPlayer.ObjectIndex)), 4);
        }

        public static void Set_Glamour(string Name, string Data)
        {
            if (Data != null) if (Data.Length > 2)
                {
                    Glamours[Name] = Data;
                    if (ID_Mapping.ContainsKey(Name)) Glamour.Set(ID_Mapping[Name], Data);
                }
        }

        private static void Disable(string Name)
        {
            if (Collection_Mapping.ContainsKey(Name) && Mods.ContainsKey(Name))
            {
                foreach (var Mod in Mods[Name].Mods) Temporary_Mod_Remover.Invoke(Mod.Key, Collection_Mapping[Name], Mod.Value.Item2);
                Redraw_Character(Name);
            }
        }

        public static void Redraw_Character(string Name) => Redraw.Invoke((int)ID_Mapping[Name]);

        private static bool Update_Characters()
        {
            var Changed = false;
            try
            {
                var New_Friend = false;
                Entities.Clear();
                Pets.Clear();
                Minions.Clear();
                foreach (var O in Objects) if (O.ObjectKind is Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player)
                    {
                        var Name = Get_Name(O.ObjectIndex);
                        if (!(((BattleChara*)O.Address)->IsFriend) && !Networking.C.Friends.Contains(Name)) continue;
                        if (O.ObjectIndex == Client.LocalPlayer?.ObjectIndex) continue;
                        if (Glamour.Ready) if (ID_Mapping.ContainsKey(Name)) if (ID_Mapping[Name] != O.ObjectIndex)
                                {
                                    Glamour.Unlock(ID_Mapping[Name]);
                                    Glamour.Revert(ID_Mapping[Name]);
                                    if (Collection_Mapping.ContainsKey(Name))
                                    {
                                        Collection_Remover.Invoke(Collection_Mapping[Name]);
                                        Collection_Mapping.Remove(Name);
                                    }
                                    ID_Mapping[Name] = O.ObjectIndex;
                                }
                        ID_Mapping[Name] = O.ObjectIndex;
                        foreach (var I in Objects) if (I.ObjectKind is Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc || I.ObjectKind is Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Companion) if (I.OwnerId == O.GameObjectId)
                                {
                                    if (I.ObjectKind is Dalamud.Game.ClientState.Objects.Enums.ObjectKind.BattleNpc)
                                    {
                                        Pets[Name] = I.ObjectIndex;
                                    }
                                    else Minions[Name] = I.ObjectIndex;
                                    break;
                                }
                        if (Glamour.Ready)
                        {
                            Set_Collection(O.ObjectIndex);
                        }
                        else Set_Customize(Name);
                        Entities.Add(Name);
                        Changed = true;
                        if (!Networking.C.Friends.Contains(Name))
                        {
                            Networking.C.Friends.Add(Name);
                            New_Friend = true;
                        }
                    }
                if (New_Friend) Networking.C.Save();
            }
            catch (Exception Error)
            {
                Log.Error("Update Characters: " + Error.Message);
            }
            return Changed;
        }

        public static void Update(IFramework F)
        {
            if (Rythmos_Path.Length > 0 && Customize.Ready)
            {
                if (!Client.IsLoggedIn && Client.LocalPlayer is null)
                {
                    foreach (var Key in Collection_Mapping.Keys.AsEnumerable<string>()) Remove_Collection(Key);
                    T = 0;
                }
                else if (Client.LocalPlayer is not null)
                {
                    if (Glamour.Ready) foreach (var O in Objects)
                        {
                            var Keys = ID_Mapping.Keys.ToList();
                            var Name = Get_Name(O.ObjectIndex);
                            foreach (var Key in Keys) if (ID_Mapping[Key] == O.ObjectIndex && Key != Name)
                                {
                                    Log.Information($"The object index of {Key} has become that of {Name}.");
                                    Glamour.Unlock(O.ObjectIndex);
                                    Glamour.Revert(O.ObjectIndex);
                                    if (Collection_Mapping.ContainsKey(Key))
                                    {
                                        Collection_Remover.Invoke(Collection_Mapping[Key]);
                                        Collection_Mapping.Remove(Key);
                                    }
                                    ID_Mapping.Remove(Key);
                                }
                        }
                    var New_T = TimeProvider.System.GetTimestamp();
                    if (New_T - T > 30000000)
                    {
                        T = New_T;
                        if (!Update_Characters()) T -= 30000000;
                        if (!((BattleChara*)Client.LocalPlayer.Address)->InCombat) if (Outdated.Count > 0 && !Networking.Downloading) foreach (var Old in Outdated) if (Entities.Contains(Old))
                                    {
                                        Queue.Send(Encoding.UTF8.GetBytes(Old), 2);
                                        break;
                                    }
                    }
                }
            }
        }
    }
}
