using Exiled.API.Features;
using Exiled.API.Features.Roles;
using MEC;
using PlayerRoles.FirstPersonControl;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using SCPAI;

namespace SCPAI.Dumpster
{
    public class AINav
    {
        public Room currentRoamingRoom;
        public Vector3 currentRoamingRoomPOS;
        public Vector3 doorToMoveTo;
        public AINav Instance;
        public Player currentTarget;
        public float gravity = 9.81f;
        public NavMeshAgent navMeshAgent;
        public int numoftargets;

        public List<GameObject> generateNav = new List<GameObject>();
        public List<Door> currentRoomDoors = new List<Door>();
        private int doortoPick;

        public void AddAgent()
        {
            ReferenceHub newPlayer = Main.Instance.aihand.hubPlayer;
            navMeshAgent = newPlayer.gameObject.AddComponent<NavMeshAgent>();
            navMeshAgent.radius = 0.1f;
            navMeshAgent.stoppingDistance = 4f;
            navMeshAgent.acceleration = 15f;
            navMeshAgent.speed = 25f;
            navMeshAgent.angularSpeed = 120f;
            navMeshAgent.baseOffset = 1f;
        }
        public void GenerateNavMesh()
        {
            foreach (Door door in Door.List) { door.GameObject.AddComponent<NavMeshLink>(); }
        }
        public IEnumerator<float> SCPWander(Player player, CharacterController controller)
        {
            Log.Info("cyz");
            for(; ;)
            {
                Log.Info("started 1 ");
                //Update the current room and find doors within that room, then pick a random one and move to it
                if (currentRoamingRoom != player.CurrentRoom)
                {
                    Log.Info("started 2 ");
                    System.Random rnd = new();
                    currentRoamingRoom = player.CurrentRoom;
                    currentRoamingRoomPOS = player.CurrentRoom.Position;
                    Log.Info($"Current room = {currentRoamingRoom}");
                    foreach (Door door in currentRoamingRoom.Doors)
                    {
                        currentRoomDoors.Add(door);
                        Log.Info(door);
                    }
                    doortoPick = rnd.Next(1, currentRoomDoors.Count);
                    doorToMoveTo = currentRoomDoors.ElementAt(doortoPick).Position;
                    //Move and look at the door position
                    controller.Move(doorToMoveTo);
                    Main.Instance.aihand.newPlayer.transform.LookAt(doorToMoveTo);                  
                }
                RaycastHit hit;
                if (Physics.Raycast(Main.Instance.aihand.newPlayer.transform.position, Vector3.forward, out hit, 1f))
                {
                    if(hit.transform.gameObject.Equals(currentRoomDoors.ElementAt(doortoPick)) && !currentRoomDoors.ElementAt(doortoPick).IsOpen)
                    {
                        Log.Info("doorippe");
                        currentRoomDoors.ElementAt(doortoPick).IsOpen = true;
                    }
                }
                Log.Info($"Door position : '{doorToMoveTo}' Controller position : '{controller.transform.position}'");
                yield return Timing.WaitForSeconds(0.5f);
            }
        }
        public IEnumerator<float> SCP096Update(Player player, CharacterController controller)
        {
            Log.Info("Ran");
            for (; ;)
            {
                numoftargets = Main.Instance.aihand.scp096targets.Values.Count;
                if (currentTarget == null)
                {
                    System.Random rnd = new();
                    int randomIndex = rnd.Next(0, Main.Instance.aihand.scp096targets.Count);
                    currentTarget = Main.Instance.aihand.scp096targets.ElementAt(randomIndex).Value;
                }
                Log.Info(currentTarget);
                if (!currentTarget.IsDead)
                {
                    Vector3 targetPosition = currentTarget.Position;
                    Vector3 direction = (targetPosition - controller.transform.position).normalized;
                    float distanceToTarget = Vector3.Distance(targetPosition, controller.transform.position);
                    float moveDistance = Mathf.Min(navMeshAgent.speed * Time.deltaTime, distanceToTarget);
                    Vector3 movement = direction * moveDistance;
                    controller.Move(movement);
                    int layerToIgnore = LayerMask.NameToLayer("Player");
                    int layerMask = 8 << layerToIgnore;
                    layerMask = ~layerMask;
                    RaycastHit hit;
                    Log.Info("sus");
                    if (Physics.Raycast(player.Position, Vector3.down, out hit, 10f, layerMask))
                    {
                        GameObject hitw = hit.collider.gameObject;
                        if (!hitw.name.Contains("mixamorig"))
                        {
                            GameObject hitObject = hit.collider.gameObject;
                            NavMeshSurface navSurface = hitObject.GetComponent<NavMeshSurface>();
                            if (navSurface == null)
                            {
                                navSurface = hitObject.AddComponent<NavMeshSurface>();
                                navSurface.collectObjects = CollectObjects.Children;
                                navSurface.BuildNavMesh();
                            }
                        }
                    }
                    Main.Instance.aihand.AIPlayer.Rotation = currentTarget.CameraTransform.position;
                }                
                yield return Timing.WaitForSeconds(0.1f);
            }
        }
    }
}