using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp096;
using MEC;
using Mirror;
using PlayerRoles;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SCPAI.Dumpster
{
    public class AIHandler : MonoBehaviour
    {
        public GameObject newPlayer;
        public Player AIPlayer;
        public CharacterController characterController;
        public float radius = 5f;
        public LayerMask layerMask;
        public string UserID;
        public ReferenceHub hubPlayer;

        public Dictionary<Player, Player> scp096targets = new Dictionary<Player, Player>();

        public int DummiesAmount;

        public void SpawnAI()
        {
            newPlayer = Instantiate(NetworkManager.singleton.playerPrefab);
            Player NewPlayer = new Player(newPlayer);
            int id = DummiesAmount;
            var fakeConnection = new FakeConnection(id++);
            hubPlayer = newPlayer.GetComponent<ReferenceHub>();
            characterController = newPlayer.GetComponent<CharacterController>();
            Main.Instance.Dummies.Add(hubPlayer);
            NetworkServer.AddPlayerForConnection(fakeConnection, newPlayer);
            hubPlayer.characterClassManager.UserId = $"AI-{id}";
            UserID = $"AI-{id}";
            hubPlayer.enabled = true;
            hubPlayer.characterClassManager.syncMode = (SyncMode)ClientInstanceMode.Unverified;
            hubPlayer.nicknameSync.Network_myNickSync = "SCP-AI";
            hubPlayer.roleManager.InitializeNewRole(RoleTypeId.Spectator, reason: RoleChangeReason.RemoteAdmin);
            hubPlayer.characterClassManager.GodMode = false;
            Player.Dictionary.Add(newPlayer, NewPlayer);
            Log.Info($"AI with ID : '{id}' has succesfully joined");
            GenNavStart();
        }

        public void GenNavStart()
        {
            if (Main.Instance.Config.LogStartup)  Log.Info("Adding Agent to AI...");
            try
            {
                Main.Instance.ainav.AddAgent();
            }
            catch (NullReferenceException er)
            {
                Log.Error($"Couldnt add a agent to the AI player due to the AI object not existing. AI Navagation wont work. Full error : {er}");
            }
            catch (Exception e)
            {
                Log.Error($"Unknown error occured, send error message to NotIntense#1613 on discord : {e}");
            }
            if (Main.Instance.Config.LogStartup) Log.Info("Succesfully added Agent component! Creating NavMeshLinks for doors..");
            try
            {
                Main.Instance.ainav.GenerateNavMesh();
            }
            catch (NullReferenceException er)
            {
                Log.Error($"Couldnt add a agent to the AI player due to the AI object not existing. AI Navagation wont work. Full error : {er}");
            }
            catch (Exception e)
            {
                Log.Error($"Unknown error occured, send error message to NotIntense#1613 on discord : {e}");
            }
            
            if (Main.Instance.Config.LogStartup) Log.Info("NavMeshLink Generation Succesful!");     
        }

        public void ChangeAIParameters(ChangingRoleEventArgs ev)
        {
            if (ev.Player.UserId == UserID)
            {
                ev.NewRole = RoleTypeId.Scp096;
                hubPlayer.nicknameSync.UpdateNickname($"{ev.NewRole} AI");
                hubPlayer.nicknameSync.Network_displayName = $"{ev.NewRole} AI";
                if(ev.Player == null)
                {
                    Log.Info("idot");
                }
                if (characterController == null)
                {
                    Log.Info("idot2");
                }
                if (ev.NewRole.GetTeam() == Team.SCPs)
                {
                    //MECExtensionMethods1.RunCoroutine(Main.Instance.ainav.SCPWander(ev.Player, characterController));
                }
            }
        }

        public void AIRage(AddingTargetEventArgs ev)
        {
            if (ev.Player.UserId == UserID)
            {
                scp096targets.Add(ev.Target, ev.Target);
                if (ev.Player.Role.Is(out Scp096Role scp096))
                {
                    Log.Info("True");
                    scp096.Enrage(100000);
                    scp096.EnragedTimeLeft = 10000f;
                    Log.Info("True");
                    MECExtensionMethods1.RunCoroutine(WaitForEnrage(ev.Player));
                    Log.Info("True");
                }
            }
        }

        public void ReloadPlugin()
        {
            if (Main.Instance.Dummies.Count > 1)
            {
                foreach (ReferenceHub hub in Main.Instance.Dummies)
                {
                    Destroy(hub);
                }
            }
        }
        public bool IsDummy(ReferenceHub hub)
        {
            return Main.Instance.Dummies.Contains(hub);
        }

        public IEnumerator<float> WaitForEnrage(Player player)
        {
            scp096targets.Add(player, player);
            yield return Timing.WaitForSeconds(3f);
            MECExtensionMethods1.RunCoroutine((Main.Instance.ainav.SCP096Update(player, characterController)));
        }
    }
}