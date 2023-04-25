using Exiled.API.Features;
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
    public class AINav
    {
        public Room currentRoamingRoom;
        public Vector3 currentRoamingRoomPOS;
        public Vector3 doorToMoveTo;
        public AINav Instance;
        public Player currentTarget;
        public NavMeshAgent scp096navMeshAgent;
        public NavMeshSurface currentNavSurface;
        public int numoftargets;
        public float distanceThreshold = 0.5f;
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
            scp096navMeshAgent.radius = 1f;
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
                    currentRoamingRoom = player.CurrentRoom;
                    currentRoomDoors = (List<Door>)player.CurrentRoom.Doors;
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
                    if (scp096navMeshAgent.isOnOffMeshLink)
                    {
                        OffMeshLinkData data = scp096navMeshAgent.currentOffMeshLinkData;
                        Vector3 endPos = data.endPos + Vector3.up * scp096navMeshAgent.baseOffset;
                        scp096navMeshAgent.transform.position = Vector3.MoveTowards(scp096navMeshAgent.transform.position, endPos, scp096navMeshAgent.speed * Time.deltaTime);
                        if (scp096navMeshAgent.transform.position == endPos)
                        {
                            scp096navMeshAgent.CompleteOffMeshLink();
                        }
                    }
                    scp096navMeshAgent.SetDestination(currentTarget.Position);
                    if(currentRoamingRoom = currentTarget.CurrentRoom)
                    {
                        var mouseLookInsameroom = ((IFpcRole)Main.Instance.aihand.hubPlayer.roleManager.CurrentRole).FpcModule.MouseLook;
                        var eulerAnglesinsameroom = Quaternion.LookRotation(currentTarget.Position - player.Position, Vector3.up).eulerAngles;
                        mouseLookInsameroom.CurrentHorizontal = eulerAnglesinsameroom.y;
                        mouseLookInsameroom.CurrentVertical = eulerAnglesinsameroom.x;
                    }
                    else
                    {
                        var mouseLook = ((IFpcRole)Main.Instance.aihand.hubPlayer.roleManager.CurrentRole).FpcModule.MouseLook;
                        var eulerAngles = Quaternion.LookRotation(currentRoamingRoom.Position - player.Position, Vector3.up).eulerAngles;
                        mouseLook.CurrentHorizontal = eulerAngles.y;
                        mouseLook.CurrentVertical = eulerAngles.x;
                    }
                    int layerToIgnore = LayerMask.NameToLayer("Player");
                    int layerMask = 8 << layerToIgnore;
                    layerMask = ~layerMask;
                    RaycastHit hit;
                    if (Physics.Raycast(player.Position, Vector3.down, out hit, 10f, layerMask))
                    {
                        GameObject hitObject = hit.collider.gameObject;
                        NavMeshSurface navSurface = hitObject.GetComponent<NavMeshSurface>();
                        currentNavSurface = navSurface;
                        if (navSurface == null && hitObject.name != "Frame" && hitObject.name.StartsWith("LCZ"))
                        {
                            Log.Debug($"Adding NavMeshSurface for {hitObject.name}");
                            navSurface = hitObject.AddComponent<NavMeshSurface>();
                            navSurface.collectObjects = CollectObjects.Children;
                            navSurface.BuildNavMesh();
                        }
                       
                    }
                    HashSet<string> obstacleNames = new();
                    RaycastHit[] hits = Physics.SphereCastAll(player.Transform.position, radius, Vector3.forward, 0f, layerToIgnore);
                    foreach (RaycastHit Collidesr in hits)
                    {
                        if (Collidesr.transform.gameObject.GetComponent<NavMeshObstacle>()) Log.Debug("Object already has a NavMeshObstacle component!");
                        if(!Collidesr.transform.gameObject.GetComponent<NavMeshObstacle>() && Collidesr.transform.gameObject.layer != layerToIgnore)
                        {                           
                            NavMeshObstacle obst = Collidesr.transform.gameObject.AddComponent<NavMeshObstacle>();
                            obst.carving = true;
                            obst.GetComponent<NavMeshObstacle>().size = Collidesr.collider.bounds.size;
                            Log.Debug($"Adding NavMeshObstacle to {Collidesr.transform.gameObject.name} and adding data");
                            currentNavSurface.AddData();
                        }          
                    }                
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