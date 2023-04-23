using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Scp096;
using MEC;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using System;
using System.Collections.Generic;
using UnityEngine;
using Interactables.Interobjects.DoorUtils;
using UnityEngine.AI;

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
        public IFpcRole fpcRole;

        public Dictionary<Player, Player> scp096targets = new Dictionary<Player, Player>();
        public Dictionary<Door, DoorAction> doorState = new();

        public int DummiesAmount = Main.Instance.Dummies.Count;
        private int id;
        private NavMeshSurface navSurface;

        public void SpawnAI()
        {
            newPlayer = Instantiate(NetworkManager.singleton.playerPrefab);
            Player NewPlayer = new(newPlayer);
            NewPlayer.Transform.rotation = newPlayer.transform.rotation;
            NewPlayer.Transform.parent = newPlayer.transform;
            id = DummiesAmount;
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
            fpcRole = Main.Instance.aihand.newPlayer.GetComponent<IFpcRole>();
            GenNavStart();
        }

        public void GenNavStart()
        {
            if (Main.Instance.Config.LogStartup) Log.Info("Adding Agent to AI...");
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
            if (Main.Instance.Config.LogStartup) Log.Info("Succesfully added Agent component!");
            if (!Main.Instance.Config.generateNavMeshOnWaiting) Log.Warn("NavMesh will only generate when needed, and this can cause serious performance issues!");
            if (Main.Instance.Config.generateNavMeshOnWaiting) Log.Info("Generating NavMesh...");
            try
            {
                if (Main.Instance.Config.generateNavMeshOnWaiting) Main.Instance.ainav.GenerateNavMesh();
            }
            catch (NullReferenceException er)
            {
                Log.Error($"Unable to add NavMeshLinks to doors. Maybe none exist? Full Error : {er}");
            }
            catch (Exception e)
            {
                Log.Error($"Unknown error occured, send error message to NotIntense#1613 on discord : {e}");
            }
            if (Main.Instance.Config.generateNavMeshOnWaiting) Log.Info("NavMesh Generation succesful!");
            Log.Info($"AI with ID : '{id}' has succesfully joined");
        }

        public void ChangeAIParameters(ChangingRoleEventArgs ev)
        {
            if (ev.Player.UserId == UserID)
            {
                hubPlayer.nicknameSync.UpdateNickname($"{ev.NewRole} AI");
                if (ev.Player == null)
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
            if (ev.Player.ReferenceHub == hubPlayer)
            {
                if (!scp096targets.ContainsKey(ev.Target))
                {
                    scp096targets.Add(ev.Target, ev.Target);
                }
                
                ev.Player.Role.Is(out Scp096Role role);
                role.Enrage(100000000);
                if(scp096targets.Count > 0)
                {
                    role.EnragedTimeLeft = 10000000;
                }
                MECExtensionMethods1.RunCoroutine(WaitForEnrage(ev.Player));
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
        public void DoorListtrack(InteractingDoorEventArgs ev)
        {
            try
            {
                if (!doorState.ContainsKey(ev.Door))
                {
                    doorState.Add(ev.Door, (DoorAction)ev.Door.ExactState);
                }
                if (ev.Door.ExactState != 1) //Fully Open
                {
                    if (ev.Door.DoorLockType != Exiled.API.Enums.DoorLockType.None) return;
                    Log.Debug("Door Opened");
                    doorState[ev.Door] = DoorAction.Opened;
                    if (ev.Door.Transform.gameObject.GetComponent<NavMeshObstacle>())
                    {
                        Destroy(ev.Door.Transform.gameObject.GetComponent<NavMeshObstacle>());
                    }
                    ev.Door.Transform.gameObject.AddComponent<NavMeshSurface>();
                    ev.Door.Transform.gameObject.AddComponent<NavMeshLink>();
                    navSurface = ev.Door.Transform.gameObject.GetComponent<NavMeshSurface>();
                    navSurface.collectObjects = CollectObjects.Children;
                    Main.Instance.ainav.currentNavSurface.AddData();
                }
                else if (ev.Door.ExactState != 0) //Fully Closed
                {
                    if (ev.Door.DoorLockType != Exiled.API.Enums.DoorLockType.None) return;
                    Log.Debug("Door Closed");
                    if (navSurface == null)
                    {
                        Log.Warn("NavSurface was null but was attempted to be deleted");
                        return;
                    }
                    else
                    {
                        Destroy(ev.Door.Transform.gameObject.GetComponent<NavMeshSurface>());
                        Destroy(ev.Door.Transform.gameObject.AddComponent<NavMeshLink>());
                    }
                    doorState[ev.Door] = DoorAction.Closed;
                    ev.Door.Transform.gameObject.AddComponent<NavMeshObstacle>();
                    NavMeshObstacle navObj = ev.Door.Transform.gameObject.GetComponent<NavMeshObstacle>();
                    navObj.carving = true;
                    Main.Instance.ainav.currentNavSurface.AddData();
                }
            }
            catch(Exception e)
            {
                Log.Debug($"Unknown error, if needed contact NotIntense#1613 on discord {e}");
            }
           
        }

        public IEnumerator<float> WaitForEnrage(Player player)
        {
            yield return Timing.WaitForSeconds(3.5f);
            MECExtensionMethods1.RunCoroutine((Main.Instance.ainav.SCP096Update(player, characterController)));
        }
    }
}