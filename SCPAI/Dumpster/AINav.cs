using Exiled.API.Features;
using Exiled.API.Features.Roles;

using PlayerRoles.FirstPersonControl;
using System;
using System.Linq;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using MEC;

namespace SCPAI.Dumpster
{
    public class AINav : MonoBehaviour
    {
        public Vector3 doorToMoveTo;
        public Door doorLook;
        public AINav Instance;
        public Player currentTarget;
        public int buttonIndex;
        public NavMeshAgent scp096navMeshAgent;
        public NavMeshSurface currentNavSurface;
        public int numoftargets;
        public float distanceThreshold = 0.5f;
        public float attackRange = 3.0f;
        public float radius = 2.0f;
        public float lookingAtThreshold = 19.9f;
        public float lookingAtDistanceThreshold = 20f;
        public LayerMask wallMask = 13;
        public bool SCP173UpdateIsRunning;

        public List<GameObject> generateNav = new List<GameObject>();
        private int randomIndex;

        public void AddAgent()
        {
            scp096navMeshAgent = Main.Instance.aihand.newPlayer.gameObject.AddComponent<NavMeshAgent>();
            scp096navMeshAgent.radius = Main.Instance.aihand.characterController.radius;
            scp096navMeshAgent.acceleration = 40f;
            scp096navMeshAgent.speed = 8.5f;
            scp096navMeshAgent.angularSpeed = 120f;
            scp096navMeshAgent.stoppingDistance = 0.3f;
            scp096navMeshAgent.baseOffset = 1;
            scp096navMeshAgent.autoRepath = true;
            scp096navMeshAgent.autoTraverseOffMeshLink = false;
            scp096navMeshAgent.height = Main.Instance.aihand.characterController.height;
            scp096navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }

        public void GenerateNavMesh()
        {
            foreach (Room room in Room.List)
            {
                room.GameObject.AddComponent<NavMeshSurface>();
                var navSurface = room.gameObject.GetComponent<NavMeshSurface>();
                navSurface.collectObjects = CollectObjects.Children;
                navSurface.BuildNavMesh();
            }
            foreach (Door door in Door.List)
            {
                Main.Instance.aihand.doorState.Add(door, Interactables.Interobjects.DoorUtils.DoorAction.Closed);
                door.GameObject.AddComponent<NavMeshObstacle>();
            }
            foreach (Lift lift in Lift.List)
            {
                lift.GameObject.AddComponent<NavMeshLink>();
                lift.GameObject.AddComponent<NavMeshSurface>();
                var liftSur = lift.GameObject.GetComponent<NavMeshSurface>();
                liftSur.collectObjects = CollectObjects.Children;
                liftSur.BuildNavMesh();
            }
        }

        public IEnumerator<float> SCP096Update(Player player, CharacterController controller)
        {
            for (; ;)
            {
                try
                {
                    numoftargets = Main.Instance.aihand.scp096targets.Values.Count;
                    if (currentTarget == null)
                    {
                        System.Random rnd = new();
                        randomIndex = rnd.Next(0, Main.Instance.aihand.scp096targets.Count);
                        if (!Main.Instance.aihand.scp096targets.Contains(Main.Instance.aihand.scp096targets.ElementAt(randomIndex)))
                        {
                            randomIndex = rnd.Next(0, Main.Instance.aihand.scp096targets.Count);
                        }
                        currentTarget = Main.Instance.aihand.scp096targets.ElementAt(randomIndex).Value;
                    }
                    if (currentTarget != null && currentTarget.IsDead && Main.Instance.aihand.scp096targets.Count > 0)
                    {
                        Main.Instance.aihand.scp096targets.Remove(currentTarget);

                        System.Random rnd = new();
                        int randomIndex = rnd.Next(0, Main.Instance.aihand.scp096targets.Count);
                        currentTarget = Main.Instance.aihand.scp096targets.ElementAt(randomIndex).Value;
                    }
                    else if (currentTarget != null && currentTarget.IsDead && Main.Instance.aihand.scp096targets.Count == 0)
                    {
                        Main.Instance.aihand.scp096targets.Clear();
                        currentTarget = null;
                        player.Role.As<Scp096Role>().Calm(true);
                        player.Role.As<Scp096Role>().ClearTargets();
                        yield break;
                    }
                    if (scp096navMeshAgent.isOnOffMeshLink)
                    {
                        OffMeshLinkData data = scp096navMeshAgent.currentOffMeshLinkData;
                        Vector3 endPos = data.endPos + Vector3.up * scp096navMeshAgent.baseOffset;
                        float distance = Vector3.Distance(scp096navMeshAgent.transform.position, endPos);
                        float offMeshLinkSpeed = 4f * scp096navMeshAgent.speed;
                        scp096navMeshAgent.transform.position = Vector3.MoveTowards(scp096navMeshAgent.transform.position, endPos, offMeshLinkSpeed * Time.deltaTime);
                        if (scp096navMeshAgent.transform.position == endPos)
                        {
                            scp096navMeshAgent.CompleteOffMeshLink();
                        }
                    }
                    if(player.CurrentRoom.Type != Exiled.API.Enums.RoomType.HczServers)
                    {
                        scp096navMeshAgent.baseOffset = 1.0f;
                    }
                    else
                    {
                        scp096navMeshAgent.baseOffset = 1.4f;
                    }
                    if (scp096navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid || scp096navMeshAgent.pathStatus == NavMeshPathStatus.PathPartial)
                    {
                        Log.Debug("Path Invalid");
                        Door closestDoor = null;
                        float closestDoorWeightedDistance = float.MaxValue;
                        foreach (Door door in player.CurrentRoom.Doors)
                        {
                            float aiToDoorDistance = Vector3.Distance(scp096navMeshAgent.transform.position, door.Transform.position);
                            float playerToDoorDistance = Vector3.Distance(currentTarget.Position, door.Transform.position);
                            float weightedDistance = aiToDoorDistance + playerToDoorDistance;
                            if (weightedDistance < closestDoorWeightedDistance && !door.Name.StartsWith("Elevator"))
                            {
                                closestDoorWeightedDistance = weightedDistance;
                                closestDoor = door;
                                doorLook = door;
                            }
                        }

                        if (closestDoor != null)
                        {
                            scp096navMeshAgent.SetDestination(closestDoor.Position);
                            if (Vector3.Distance(closestDoor.Position, player.Position) <= attackRange || closestDoor.IsBroken)
                            {
                                Log.Debug($"{closestDoor.Name} in attack range");
                                closestDoor.DamageDoor(1000, Interactables.Interobjects.DoorUtils.DoorDamageType.Scp096);
                                try
                                {
                                    Destroy(closestDoor.Transform.gameObject.GetComponent<NavMeshObstacle>());
                                    closestDoor.Transform.gameObject.AddComponent<NavMeshLink>();
                                    NavMeshLink navLink = closestDoor.Transform.gameObject.GetComponent<NavMeshLink>();
                                    navLink.bidirectional = true;
                                    navLink.width = closestDoor.Transform.position.y;
                                    doorLook = null;
                                    closestDoor = null;
                                    scp096navMeshAgent.ResetPath();
                                    scp096navMeshAgent.SetDestination(currentTarget.Position);
                                }
                                catch (Exception e)
                                {
                                    Log.Error("Error in : if(closestDoor != null)" + e);
                                }
                            }
                        }
                        else
                        {
                            scp096navMeshAgent.SetDestination(currentTarget.Position);
                        }
                    }
                    else
                    {
                        scp096navMeshAgent.SetDestination(currentTarget.Position);
                    }
                    if (player.CurrentRoom == currentTarget.CurrentRoom || scp096navMeshAgent.pathStatus == NavMeshPathStatus.PathComplete)
                    {
                        var mouseLookInsameroom = ((IFpcRole)Main.Instance.aihand.hubPlayer.roleManager.CurrentRole).FpcModule.MouseLook;
                        var eulerAnglesinsameroom = Quaternion.LookRotation(currentTarget.Position - player.Position, Vector3.up).eulerAngles;
                        mouseLookInsameroom.CurrentHorizontal = eulerAnglesinsameroom.y;
                        mouseLookInsameroom.CurrentVertical = 0;
                        Vector3 rotation = new(mouseLookInsameroom.CurrentVertical, mouseLookInsameroom.CurrentHorizontal, 0f);
                        player.Rotation = rotation;
                    }
                    else
                    {
                        var mouseLook = ((IFpcRole)Main.Instance.aihand.hubPlayer.roleManager.CurrentRole).FpcModule.MouseLook;
                        var eulerAngles = Quaternion.LookRotation(doorLook.Position - player.Position, Vector3.up).eulerAngles;
                        mouseLook.CurrentHorizontal = eulerAngles.y;
                        mouseLook.CurrentVertical = 0;
                        Vector3 rotation = new(mouseLook.CurrentHorizontal, mouseLook.CurrentHorizontal, 0f);
                        player.Rotation = rotation;
                    }
                    int layerToIgnore = LayerMask.NameToLayer("Player");
                    int layerMask = 8 << layerToIgnore;
                    layerMask = ~layerMask;
                    if (Physics.Raycast(player.Position, Vector3.down, out RaycastHit hit, 3f))
                    {
                        GameObject hitObject = hit.collider.gameObject;
                        NavMeshSurface navSurface = hitObject.GetComponent<NavMeshSurface>();
                        currentNavSurface = navSurface;
                        if (navSurface == null && !hitObject.name.StartsWith("LCZ") && hitObject.layer != layerToIgnore && !hitObject.name.StartsWith("Collider") && !hitObject.name.StartsWith("workbench") && !hitObject.name.StartsWith("mixamorig") && hitObject.name != "Frame")
                        {
                            Log.Debug($"Adding NavMeshSurface for {hitObject.name}");
                            navSurface = hitObject.AddComponent<NavMeshSurface>();
                            navSurface.size = hitObject.transform.localScale;
                            navSurface.collectObjects = CollectObjects.Children;
                            navSurface.BuildNavMesh();
                        }
                    }
                }
                catch (ArgumentOutOfRangeException)
                {
                    currentTarget = null;
                    Main.Instance.aihand.scp096targets.Clear();
                    player.Role.As<Scp096Role>().Calm(true);
                    player.Role.As<Scp096Role>().ClearTargets();
                    yield break;
                }
                catch (Exception e)
                {
                    Log.Debug(e);
                }
                if (currentTarget != null && Vector3.Distance(player.Position, currentTarget.Position) <= attackRange)
                {
                    player.Role.As<Scp096Role>().Attack();
                }
                yield return Timing.WaitForSeconds(0.1f);
            }
        }

        public IEnumerator<float> CheckFor173Lookers(Player SCP173)
        {
            SCP173UpdateIsRunning = true;
            for(; ; )
            {
                foreach (Player ply in Player.List)
                {
                    if (ply.ReferenceHub != SCP173.ReferenceHub)
                    {
                        if (Vector3.Distance(ply.Position, SCP173.Position) > lookingAtDistanceThreshold || Physics.Linecast(ply.Position, SCP173.Position, wallMask))
                        {
                            yield return Timing.WaitForSeconds(0.5f);
                        }

                        Vector3 ScpFwd = SCP173.CameraTransform.forward;
                        Vector3 TargetFwd = ply.CameraTransform.forward;
                        float ViewAngle = Vector3.Angle(TargetFwd, (SCP173.Position - ply.Position).normalized);

                        if (ViewAngle >= lookingAtThreshold)
                        {
                            Log.Debug("No one is looking at 173");
                            Main.Instance.aihand.scp173targets = null;
                        }
                        else
                        {
                            try
                            {
                                Log.Debug($"{ply.Nickname} is looking at 173");
                                if (!Main.Instance.aihand.scp173targets.ContainsKey(ply))
                                {
                                    Main.Instance.aihand.scp173targets.Add(ply, ply);
                                }
                                MECExtensionMethods1.RunCoroutine(SCP173Update(Main.Instance.aihand.scp173targets, Main.Instance.aihand.characterController, SCP173));
                                if (!SCP173UpdateIsRunning)
                                {
                                    Log.Info("Started Update");
                                    MECExtensionMethods1.RunCoroutine(SCP173Update(Main.Instance.aihand.scp173targets, Main.Instance.aihand.characterController, SCP173));
                                }
                            }    
                            catch(Exception e)
                            {
                                Log.Info(e);
                            }
                        }                     
                    }                   
                }
                yield return Timing.WaitForSeconds(0.5f);
            }
        }

        public IEnumerator<float> SCP173Update(Dictionary<Player, Player> scp173targets, CharacterController controller, Player SCP173)
        {
            Log.Info("Started");
            Player closestPlayer = null;
            float closestDistance = float.MaxValue;

            foreach (Player player in scp173targets.Keys)
            {
                float distance = Vector3.Distance(controller.transform.position, player.Position);
                if (distance < closestDistance)
                {
                    closestPlayer = player;
                    closestDistance = distance;
                }
            }
            Log.Info($"{closestPlayer.Nickname} is closest");
            if (closestPlayer != null)
            {
                if (!SCP173.Role.As<Scp173Role>().BlinkReady)
                {
                    Log.Info("Isnt ready");
                    yield return Timing.WaitForSeconds(SCP173.Role.As<Scp173Role>().BlinkCooldown);
                }
                if(closestDistance <= SCP173.Role.As<Scp173Role>().BlinkDistance)
                {
                    Log.Info("Blinked");
                    SCP173.Role.As<Scp173Role>().Blink(closestPlayer.Position);
                }
            }
        }

    }
}