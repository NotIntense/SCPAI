using Exiled.API.Features;
using Exiled.API.Features.Roles;
using MEC;
using PlayerRoles.FirstPersonControl;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

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

        public IEnumerator<float> SCPWander(Player player, CharacterController controller)
        {
            yield break; // Temporary as its not started lol
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
    }
}