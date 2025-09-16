using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Rythmos.Handlers;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;
namespace Rythmos.Windows;
public class MainWindow : Window, IDisposable
{

    public IClientState ClientState;
    private Plugin P;
    private static readonly Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Action> Reference = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();

    public static IPluginLog Log = null!;

    public string Path = "";

    public string Packing = "Pack";

    public string Mini_Packing = "Mini-Pack";

    public string Micro_Packing = "Micro-Pack";

    public MainWindow(Plugin P)
        : base("Rythmos###Rythmos Main", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        this.P = P;
        Path = P.Configuration.Path;

        if (Directory.Exists(Path)) // This exists to clean early versions of files.
        {
            if (Directory.GetFiles(Path + "\\Compressed").Any(X => X.Split(" ").Length == 2))
            {
                Directory.Delete(Path + "\\Compressed", true);
                Directory.CreateDirectory(Path + "\\Compressed");
            }
            if (Directory.GetDirectories(Path + "\\Mods").Any(X => X.Split(" ").Length == 2))
            {
                Directory.Delete(Path + "\\Mods", true);
                Directory.CreateDirectory(Path + "\\Mods");
            }
        }
    }

    public void Dispose() { }

    public override void Draw()
    {
        using (var child = ImRaii.Child("Scroll##Rythmos Scroll", Vector2.Zero, true))
        {
            if (child.Success)
            {
                if (ClientState.LocalPlayer != null)
                {
                    var Add = false;

                    ImGui.Spacing();
                    ImGui.Text(Networking.Client.Connected ? "Connected" : "Connecting");
                    ImGui.Spacing();
                    ImGui.Checkbox("Save##Rythmos Save Path", ref Add);
                    ImGui.SameLine();
                    ImGui.InputText($"Path##Rythmos File", ref Path);
                    if (Add)
                    {
                        if (!Directory.Exists(Path)) Directory.CreateDirectory(Path);
                        if (!Directory.Exists(Path + "\\Mods")) Directory.CreateDirectory(Path + "\\Mods");
                        if (!Directory.Exists(Path + "\\Compressed")) Directory.CreateDirectory(Path + "\\Compressed");
                        Characters.Rythmos_Path = Path;
                        P.Configuration.Path = Path;
                        P.Configuration.Save();
                    }
                    if (Characters.Rythmos_Path.Length > 0)
                    {
                        ImGui.Spacing();
                        Add = false;
                        ImGui.Checkbox($"{Packing}##Rythmos Button", ref Add);
                        if (Add) P.Packing(Networking.Name, Characters.Gather_Mods(Networking.Name));
                        Add = false;
                        ImGui.SameLine();
                        ImGui.Checkbox($"{Mini_Packing}##Rythmos Button", ref Add);
                        if (Add) P.Packing(Networking.Name, Characters.Gather_Mods(Networking.Name), 1);
                        Add = false;
                        ImGui.SameLine();
                        ImGui.Checkbox($"{Micro_Packing}##Rythmos Button", ref Add);
                        if (Add) P.Packing(Networking.Name, Characters.Gather_Mods(Networking.Name), 2);
                        Add = false;
                        ImGui.Spacing();
                        ImGui.Checkbox($"{(Networking.Progress.Length == 0 ? "Upload Pack" : Networking.Progress)}##Rythmos Button", ref Add);
                        if (Add) P.Uploading(Networking.Name);
                        Add = false;
                        var Previous = P.Configuration.Sync_Glamourer;
                        ImGui.Spacing();
                        ImGui.Checkbox($"Sync Glamourer##Rythmos Syncing", ref P.Configuration.Sync_Glamourer);
                        if (Previous != P.Configuration.Sync_Glamourer) P.Configuration.Save();

                    }
                }
            }
        }
    }
}
