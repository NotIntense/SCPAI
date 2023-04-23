using Exiled.API.Interfaces;
using System.ComponentModel;

namespace SCPAI
{
    public class Config : IConfig
    {
        [Description("Sets the plugin to be enabled or not")]
        public bool IsEnabled { get; set; } = true;

        [Description("Spams trash in console")]
        public bool Debug { get; set; } = false;

        [Description("Shows messages in the console about what the plugin is doing during startup sequence, not really needed")]
        public bool LogStartup { get; set; } = false;

        [Description("Genertates most of the required NavMeshes while waiting for players, instead of generating when it needs it. (RECOMMENED TO LEAVE TRUE)")]
        public bool generateNavMeshOnWaiting { get; set; } = true;
    }
}