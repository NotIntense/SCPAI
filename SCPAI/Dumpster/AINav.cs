﻿using Exiled.API.Features;
using Exiled.API.Features.Roles;
using MEC;
using PlayerRoles.FirstPersonControl;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using System;

namespace SCPAI.Dumpster
{
    public class AINav : MonoBehaviour
    {
        public Room currentRoamingRoom;
        public Door doorToMove = null;
        public Vector3 currentRoamingRoomPOS;
        public Vector3 doorToMoveTo;
        public AINav Instance;
        public Player currentTarget;
        public int buttonIndex;
        public NavMeshAgent scp096navMeshAgent;
        public NavMeshSurface currentNavSurface;
        public int numoftargets;
        public float distanceThreshold = 0.5f;
        public float attackRange = 3.0f;
        public float radius = 2.0f; 
        public int numRays = 16;
        public float rayDistance = 2.0f;
        SphereCollider obstacleDetectionCollider;


        public List<GameObject> generateNav = new List<GameObject>();
        public List<Door> currentRoomDoors = new List<Door>();
        private int randomIndex;

        public void AddAgent()
        {
            ReferenceHub newPlayerhub = Main.Instance.aihand.hubPlayer;
            scp096navMeshAgent = newPlayerhub.gameObject.AddComponent<NavMeshAgent>();
            obstacleDetectionCollider = Main.Instance.aihand.newPlayer.AddComponent<SphereCollider>();
            obstacleDetectionCollider.radius = radius;
            scp096navMeshAgent.radius = 0.1f;
            scp096navMeshAgent.acceleration = 40f;
            scp096navMeshAgent.speed = 8.5f;
            scp096navMeshAgent.angularSpeed = 120f;
            scp096navMeshAgent.stoppingDistance = 0.3f;
            scp096navMeshAgent.baseOffset = 1f;
            scp096navMeshAgent.autoRepath = true;
            scp096navMeshAgent.autoTraverseOffMeshLink = false;
        }
        public void GenerateNavMesh()
        {
            foreach(Room room in Room.List)
            {
                room.GameObject.AddComponent<NavMeshSurface>();
                var navSurface = room.gameObject.GetComponent<NavMeshSurface>();
                navSurface.voxelSize = 0.1f;
                navSurface.collectObjects = CollectObjects.Children;
                navSurface.BuildNavMesh();
            }

            foreach(Lift lift in Lift.List)
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
            for(; ;)
            {
                if(currentRoamingRoom != player.CurrentRoom)
                {
                    currentRoamingRoom = player.CurrentRoom;
                    currentRoamingRoomPOS = player.CurrentRoom.Position;
                    currentRoomDoors.Clear();
                    foreach(Door door in currentRoamingRoom.Doors)
                    {
                        currentRoomDoors.Add(door);
                    }
                }
                foreach (Door door in currentRoamingRoom.Doors)
                {
                    if (!currentRoomDoors.Contains(door) && door.RequiredPermissions.RequiredPermissions == Interactables.Interobjects.DoorUtils.KeycardPermissions.None || door.RequiredPermissions.RequiredPermissions == Interactables.Interobjects.DoorUtils.KeycardPermissions.None || door.IsOpen)
                    {
                        currentRoomDoors.Add(door);
                    }
                }
                Door randomDoor = currentRoomDoors[UnityEngine.Random.Range(0, currentRoomDoors.Count)];
                scp096navMeshAgent.SetDestination(randomDoor.Position);
                var mouseLook = ((IFpcRole)Main.Instance.aihand.hubPlayer.roleManager.CurrentRole).FpcModule.MouseLook;
                var eulerAngles = Quaternion.LookRotation(randomDoor.Position - player.Position, Vector3.up).eulerAngles;
                mouseLook.CurrentHorizontal = eulerAngles.y;
                mouseLook.CurrentVertical = eulerAngles.x;                
                yield return Timing.WaitForSeconds(0.5f);
            }
        }
        public IEnumerator<float> SCP096Update(Player player, CharacterController controller)
        {
            for (; ; )
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
                        Log.Debug("Target is dead but list is above 1");
                        Main.Instance.aihand.scp096targets.Remove(currentTarget);

                        System.Random rnd = new();
                        int randomIndex = rnd.Next(0, Main.Instance.aihand.scp096targets.Count);
                        currentTarget = Main.Instance.aihand.scp096targets.ElementAt(randomIndex).Value;
                    }
                    else if (currentTarget != null && currentTarget.IsDead && Main.Instance.aihand.scp096targets.Count == 0)
                    {
                        Log.Debug("All targets dead");
                        Main.Instance.aihand.scp096targets.Clear();
                        currentTarget = null;
                        player.Role.As<Scp096Role>().Calm(true);
                        player.Role.As<Scp096Role>().ClearTargets();
                        yield break;
                    }
                    if (Vector3.Distance(currentTarget.Position, player.Position) <= attackRange)
                    {
                        Log.Debug("Player In attack range");
                        if(player.Role.As<Scp096Role>().AttackPossible)
                        {
                            player.Role.As<Scp096Role>().Attack();
                        }
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

                    if (scp096navMeshAgent.pathStatus == NavMeshPathStatus.PathPartial || scp096navMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid && currentTarget != null && doorToMove == null)
                    {
                        Log.Info("Path Invalid");
                        float closestDistance = float.MaxValue;
                        foreach (Door door in player.CurrentRoom.Doors)
                        {
                            float distance = Vector3.Distance(currentTarget.Position, door.Transform.position);
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                doorToMove = door;
                            }
                        }
                        if(doorToMove == null) // if the door still equals null, give up and just do something lmao
                        {
                            foreach(Door door in player.CurrentRoom.Doors)
                            {
                                float distance = Vector3.Distance(player.Position, door.Transform.position);
                                if (distance < closestDistance)
                                {
                                    closestDistance = distance;
                                    doorToMove = door;
                                }
                            }
                        }
                        if (doorToMove != null)
                        {
                            scp096navMeshAgent.SetDestination(doorToMove.Transform.position);
                            if (Vector3.Distance(scp096navMeshAgent.transform.position, doorToMove.Transform.position) < attackRange)
                            {
                                doorToMove.DamageDoor(100, type: Interactables.Interobjects.DoorUtils.DoorDamageType.Scp096);
                                try
                                {
                                    Destroy(doorToMove.Transform.gameObject.GetComponent<NavMeshObstacle>());
                                    doorToMove.Transform.gameObject.AddComponent<NavMeshLink>();
                                    NavMeshLink navLink = doorToMove.Transform.gameObject.GetComponent<NavMeshLink>();
                                    navLink.bidirectional = true;
                                    navLink.width = doorToMove.Transform.position.z;
                                    scp096navMeshAgent.ResetPath();
                                    doorToMove = null;
                                    scp096navMeshAgent.SetDestination(currentTarget.Position);
                                }
                                catch(Exception e)
                                {
                                    Log.Error(e);
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

                    if (currentRoamingRoom = currentTarget.CurrentRoom)
                    {
                        var mouseLookInsameroom = ((IFpcRole)Main.Instance.aihand.hubPlayer.roleManager.CurrentRole).FpcModule.MouseLook;
                        var eulerAnglesinsameroom = Quaternion.LookRotation(currentTarget.Position - player.Position, Vector3.up).eulerAngles;
                        mouseLookInsameroom.CurrentHorizontal = eulerAnglesinsameroom.y;
                        mouseLookInsameroom.CurrentVertical = eulerAnglesinsameroom.x;
                        Vector3 rotation = new Vector3(mouseLookInsameroom.CurrentVertical, mouseLookInsameroom.CurrentHorizontal, 0f);
                        player.Rotation = rotation;
                    }
                    else
                    {
                        var mouseLook = ((IFpcRole)Main.Instance.aihand.hubPlayer.roleManager.CurrentRole).FpcModule.MouseLook;
                        var eulerAngles = Quaternion.LookRotation(currentRoamingRoom.Position - player.Position, Vector3.up).eulerAngles;
                        mouseLook.CurrentHorizontal = eulerAngles.y;
                        mouseLook.CurrentVertical = eulerAngles.x;
                        Vector3 rotation = new Vector3(mouseLook.CurrentHorizontal, mouseLook.CurrentHorizontal, 0f);
                        player.Rotation = rotation;
                    }
                    int layerToIgnore = LayerMask.NameToLayer("Player");
                    int layerMask = 8 << layerToIgnore;
                    layerMask = ~layerMask;
                    RaycastHit hit;
                    if (Physics.Raycast(player.Position, Vector3.down, out hit, 5f))
                    {
                        GameObject hitObject = hit.collider.gameObject;
                        NavMeshSurface navSurface = hitObject.GetComponent<NavMeshSurface>();
                        currentNavSurface = navSurface;
                        if (navSurface == null && hitObject.name != "Frame" && !hitObject.name.StartsWith("LCZ") && hitObject.layer != layerToIgnore && !hitObject.name.StartsWith("Collider"))
                        {
                            Log.Debug($"Adding NavMeshSurface for {hitObject.name}");
                            navSurface = hitObject.AddComponent<NavMeshSurface>();
                            navSurface.size = hitObject.transform.localScale;
                            navSurface.collectObjects = CollectObjects.Children;
                            navSurface.BuildNavMesh();
                        }
                       
                    }                   
                }
                catch(ArgumentOutOfRangeException ar)
                {
                    Log.Debug(ar);
                    currentTarget = null;
                    Main.Instance.aihand.scp096targets.Clear();
                    player.Role.As<Scp096Role>().Calm(true);
                    player.Role.As<Scp096Role>().ClearTargets();
                    yield break;
                }
                catch (Exception e)
                {
                    Log.Debug("Unless AI in-game is not functioning properly, ignore these messages :" + e);
                }
                yield return Timing.WaitForSeconds(0.1f);
            }
        }

    }
}