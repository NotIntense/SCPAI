using Exiled.API.Enums;
using Exiled.API.Features;
using System;
using System.Collections.Generic;
using Player = Exiled.Events.Handlers.Player;
using SCP096 = Exiled.Events.Handlers.Scp096;
using Server = Exiled.Events.Handlers.Server;

namespace SCPAI.Dumpster
{
    public class Main : Plugin<Config>
    {
        public static Main Instance;
        public AIHandler aihand;
        public AINav ainav;
        public List<ReferenceHub> Dummies = new();
        public override string Name => "SCP-AI";
        public override string Prefix => "SCPAI";
        public override string Author => "NotIntense";
        public override PluginPriority Priority => PluginPriority.Medium;
        public override Version Version => new Version(1, 0, 0);
        public override Version RequiredExiledVersion => new Version(6, 0, 0);

        public override void OnEnabled()
        {
            Instance = this;
            aihand = new AIHandler();
            ainav = new AINav();
            RegisterEvents();
            base.OnEnabled();
        }

        public override void OnDisabled()
        {
            Instance = null;
            UnRegisterEvents();
            base.OnDisabled();
        }

        public Player player;        

        public void RegisterEvents()
        {
            player = new Player();;
            Player.ChangingRole +=  aihand.ChangeAIParameters;
            SCP096.AddingTarget += aihand.AIRage;
            Server.WaitingForPlayers += aihand.SpawnAI;
            Server.RestartingRound += aihand.ReloadPlugin;
        }

        public void UnRegisterEvents()
        {
            player = null;
        }
    }
}