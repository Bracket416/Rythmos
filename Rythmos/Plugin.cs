using Dalamud.Game;
using Dalamud.Game.ClientState.Buddy;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.File;
using Rythmos.Handlers;
using Rythmos.Windows;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Rythmos;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPartyList Party { get; private set; } = null!;

    private const string CommandName = "/rythmos";
    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("Rythmos");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    internal Task Uploading(string Name)
    {
        return Task.Run(async () =>
        {
            try
            {
                if (File.Exists(Characters.Rythmos_Path + $"\\Compressed\\{Name}.zip"))
                {
                    Networking.Progress = "Uploading";
                    await Networking.Send(File.ReadAllBytes(Characters.Rythmos_Path + $"\\Compressed\\{Name}.zip"), 1);
                    Networking.Progress = "Upload Pack";
                }
                else Networking.Progress = "Pack Missing";
            }
            catch (Exception Error)
            {
                Log.Error(Error.Message);
            }
        });
    }

    internal Task Packing(string Name, Characters.Mod_Configuration M, uint Type = 0)
    {
        return Task.Run(async () =>
        {
            MainWindow.General_Packing = true;
            try
            {
                if (Type == 0)
                {
                    MainWindow.Packing = "Packing";
                }
                else if (Type == 1)
                {
                    MainWindow.Mini_Packing = "Mini-Packing";
                }
                else if (Type == 2) MainWindow.Micro_Packing = "Micro-Packing";
                Framework.RunOnTick(async () =>
                {
                    await Characters.Pack(Name, M, Type);
                    if (Type == 0)
                    {
                        MainWindow.Packing = "Pack";
                    }
                    else if (Type == 1)
                    {
                        MainWindow.Mini_Packing = "Mini-Pack";
                    }
                    else if (Type == 2) MainWindow.Micro_Packing = "Micro-Pack";
                });
            }
            catch (Exception Error)
            {
                Log.Error(Error.Message);
            }
            MainWindow.General_Packing = false;
        });
    }

    public Plugin(IDalamudPluginInterface I)
    {
        I.Inject(this);
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        if (Configuration.Path.Length > 0)
        {
            if (!Directory.Exists(Configuration.Path + "\\Compressed")) Directory.CreateDirectory(Configuration.Path + "\\Compressed");
            if (!Directory.Exists(Configuration.Path + "\\Parts")) Directory.CreateDirectory(Configuration.Path + "\\Parts");
            if (!Directory.Exists(Configuration.Path + "\\Mods")) Directory.CreateDirectory(Configuration.Path + "\\Mods");
        }
        Networking.F = Framework;
        Networking.C = Configuration;
        Customize.Log = Log;
        Customize.Interface = I;
        Customize.Setup(I);
        Glamour.Log = Log;
        Glamour.Interface = I;
        Glamour.Setup(I);
        var Ready = Customize.Ready ? (Glamour.Ready ? "" : "Please update/install Glamourer! If Glamourer is updated/installed, ignore this message!") : (Glamour.Ready ? "Please update/install Customize+! If Customize+ is updated/installed, ignore this message!" : "Please update/install Glamourer and Customize+! If they are updated/installed, ignore this message!");
        if (Ready.Length > 0) Chat.PrintError("[Rythmos] " + Ready);
        Networking.Log = Log;
        Characters.Data_Manager = DataManager;
        Characters.Client = ClientState;
        Characters.Objects = Objects;
        Characters.Log = Log;
        Characters.Party = Party;
        Characters.Rythmos_Path = Configuration.Path;
        Characters.Setup(I, Chat);
        // You might normally want to embed resources and load them from the manifest stream
        ConfigWindow = new ConfigWindow(this);
        MainWindow.Log = Log;
        MainWindow = new MainWindow(this);
        MainWindow.ClientState = ClientState;
        //if (Objects.Length > 0) Log.Information(Objects[0].EntityId + "");
        Framework.Update += Networking.Update;
        Framework.Update += Characters.Update;

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "A useful message to display in /xlhelp"
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);
        Networking.Dispose();
        Glamour.Dispose();
        Queue.Dispose();
        Characters.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        ToggleMainUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
