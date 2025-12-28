using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace Rythmos;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public string Player = "";

    public bool Sync_Glamourer = false;

    public bool Experimental = false;

    public List<string> Friends = new List<string>();

    public string Path = "";
    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
