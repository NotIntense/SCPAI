﻿using Exiled.API.Features;
using Exiled.API.Features.Roles;
using Exiled.Events.EventArgs.Player;
using Exiled.Events.EventArgs.Cassie;
using Exiled.Events.EventArgs.Scp096;
using Interactables.Interobjects.DoorUtils;
using MEC;
using Mirror;
using PlayerRoles;
using PlayerRoles.FirstPersonControl;
using System;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using PlayerRoles.PlayableScps.Scp173;

namespace SCPAI.Dumpster
{
    public class AIHandler : MonoBehaviour
    {
        public GameObject newPlayer;
        public Player NewPlayer;
        public CharacterController characterController;
        public float radius = 5f;
        public LayerMask layerMask;
        public ReferenceHub hubPlayer;
        public IFpcRole fpcRole;
        private readonly System.Random rnd = new();
        public Dictionary<Player, Player> scp096targets = new();
        public Dictionary<Player, Player> scp173targets = new();
        public Dictionary<Door, DoorAction> doorState = new();
        public bool scp173isbeingwatched;
        public Scp173ObserversTracker SCP173A = new Scp173ObserversTracker();


        public int DummiesAmount = Main.Instance.Dummies.Count;
        private int id;
        public void SpawnAI()
        {
            newPlayer = Instantiate(NetworkManager.singleton.playerPrefab);
            NewPlayer = new(newPlayer);
            NetworkServer.Spawn(newPlayer);
            NewPlayer.Transform.rotation = newPlayer.transform.rotation;
            NewPlayer.Transform.parent = newPlayer.transform;
            newPlayer.AddComponent<NetworkIdentity>();
            id = rnd.Next(1, 20);
            var fakeConnection = new FakeConnection(id);
            hubPlayer = newPlayer.GetComponent<ReferenceHub>();
            characterController = newPlayer.GetComponent<CharacterController>();
            Main.Instance.Dummies.Add(hubPlayer);
            NetworkServer.AddPlayerForConnection(fakeConnection, newPlayer);
            hubPlayer.characterClassManager.UserId = $"76561199221037417@steam";
            hubPlayer.enabled = true;
            hubPlayer.characterClassManager.InstanceMode = ClientInstanceMode.Host;
            hubPlayer.nicknameSync.Network_myNickSync = $"AI-{id}";
            hubPlayer.roleManager.InitializeNewRole(RoleTypeId.Spectator, RoleChangeReason.RemoteAdmin);
            hubPlayer.characterClassManager.GodMode = false;
            NewPlayer.RemoteAdminPermissions = PlayerPermissions.AFKImmunity;
            Player.Dictionary.Add(newPlayer, NewPlayer);
            if (Main.Instance.Config.NPCBadgeEnabled)
            {
                NewPlayer.RankName = Main.Instance.Config.NPCBadgeName;
                NewPlayer.RankColor = Main.Instance.Config.NPCBadgeColor;
            }
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
            if (!Main.Instance.Config.generateNavMeshOnWaiting) Log.Warn("NavMesh will only generate when needed, and this can cause performance issues!");
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
            Log.Info($"AI with ID : '{id}' has succesfully joined.");
        }

        public void ChangeAIParameters(ChangingRoleEventArgs ev)
        {
            if (ev.Player.ReferenceHub == hubPlayer)
            {
                hubPlayer.nicknameSync.UpdateNickname($"{ev.NewRole.ToString().ToUpper()} AI");
                if (ev.NewRole == RoleTypeId.Scp173)
                {
                    Log.Debug("Started 'CheckFor173Lookers' coroutine");
                    MECExtensionMethods1.RunCoroutine(Main.Instance.ainav.CheckFor173Lookers(ev.Player));
                }
                else if (ev.NewRole != RoleTypeId.Scp173)
                {
                    Log.Debug("Killed 'CheckFor173Lookers' coroutine");
                    Main.Instance.ainav.SCP173UpdateIsRunning = false;
                    CoroutineHandle coroutineHandle = Timing.RunCoroutine(Main.Instance.ainav.CheckFor173Lookers(ev.Player));
                    Timing.KillCoroutines(coroutineHandle);
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
                role.Enrage(10000);
                if (scp096targets.Count > 0)
                {
                    role.EnragedTimeLeft = 10000;
                }
                MECExtensionMethods1.RunCoroutine(WaitForEnrage(ev.Player));
            }
        }

        public void DoorListtrack(InteractingDoorEventArgs ev)
        {
            try
            {
                if (!doorState.ContainsKey(ev.Door))
                {
                    doorState.Add(ev.Door, (DoorAction)ev.Door.ExactState);
                }
                if (ev.Door.ExactState != 1 && !ev.Door.IsBroken) //Fully Open
                {
                    if (ev.Door.DoorLockType != Exiled.API.Enums.DoorLockType.None) return;
                    Log.Debug("Door Opened");
                    doorState[ev.Door] = DoorAction.Opened;
                    try
                    {
                        Destroy(ev.Door.Transform.gameObject.GetComponent<NavMeshObstacle>());
                        Log.Debug("Obstacle Deleted");
                    }
                    catch (NullReferenceException e)
                    {
                        Log.Debug($"Unable to delete NavMeshObstacle component! Error --> {e}");
                    }
                    catch (Exception ue)
                    {
                        Log.Debug($"Unknown execption thrown! Full Error --> {ue}");
                    }
                    ev.Door.Transform.gameObject.AddComponent<NavMeshLink>();
                    NavMeshLink navLink = ev.Door.Transform.gameObject.GetComponent<NavMeshLink>();
                    navLink.bidirectional = true;
                    navLink.width = ev.Door.Transform.position.y;
                }
                else if (ev.Door.ExactState != 0 && !ev.Door.IsBroken) //Fully Closed
                {
                    if (ev.Door.DoorLockType != Exiled.API.Enums.DoorLockType.None) return;
                    Log.Debug("Door Closed");
                    try
                    {
                        Destroy(ev.Door.Transform.gameObject.GetComponent<NavMeshLink>());
                        Log.Debug("NavMeshLink Deleted");
                    }
                    catch (NullReferenceException e)
                    {
                        Log.Debug($"Unable to delete NavMeshLink component! Error --> {e}");
                    }
                    catch (Exception ue)
                    {
                        Log.Debug($"Unknown execption thrown! Full Error --> {ue}");
                    }
                    doorState[ev.Door] = DoorAction.Closed;
                    ev.Door.Transform.gameObject.AddComponent<NavMeshObstacle>();
                    NavMeshObstacle navObj = ev.Door.Transform.gameObject.GetComponent<NavMeshObstacle>();
                    navObj.carving = true;
                }
            }
            catch (Exception e)
            {
                Log.Debug(e);
            }
        }

        public void AIKick(KickingEventArgs ev)
        {
            if (ev.Target.UserId == hubPlayer.characterClassManager.UserId)
            {
                Log.Warn("AI has been kicked!");
                Destroy(hubPlayer);
                Destroy(newPlayer);
                Destroy(NewPlayer.GameObject);
            }
        }

        public void AIBan(BanningEventArgs ev)
        {
            if (ev.Target.UserId == hubPlayer.characterClassManager.UserId)
            {
                ev.Player.Broadcast(5, "You cannot ban AI players!");
                Log.Warn("AI was attempted to be banned! Blocking event!");
                ev.IsAllowed = false;
            }
        }

        public void AITelsafixlmfao(TriggeringTeslaEventArgs ev)
        {
            if (ev.Player.ReferenceHub == hubPlayer && ev.IsInHurtingRange)
            {
                ev.Player.Hurt(200, Exiled.API.Enums.DamageType.Tesla);
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
        public void atahugaswgg(VoiceChattingEventArgs ev)
        {
            RaycastHit hit;
            if (Physics.Raycast(ev.Player.Position, Vector3.forward, out hit, 3f))
            {
                Log.Info(hit.transform.gameObject.name);
            }
        }
        public bool IsDummy(ReferenceHub hub)
        {
            return Main.Instance.Dummies.Contains(hub);
        }
        public void SwitchClientIM(ReferenceHub hub, ClientInstanceMode inst)
        {
            if(Main.Instance.Dummies.Contains(hub) && inst != ClientInstanceMode.Host)
            {
                hub.characterClassManager.InstanceMode = ClientInstanceMode.Host;
            }
        }
        public IEnumerator<float> WaitForEnrage(Player player)
        {
            yield return Timing.WaitForSeconds(3.5f);
            MECExtensionMethods1.RunCoroutine((Main.Instance.ainav.SCP096Update(player, characterController)));
        }
    }
}