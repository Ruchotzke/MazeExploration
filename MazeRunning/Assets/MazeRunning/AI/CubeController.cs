using MazeRunning.Player;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace MazeRunning.AI
{
    /// <summary>
    /// A class used to control the movement and behavior of
    /// the cage-cube.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class CubeController : MonoBehaviour
    {
        private NavMeshAgent m_Agent;   /* The agent controlling this box's navigation */
        private Transform target;       /* The target of this agent's movement */
        private PlayerManager player;   /* A reference to the player's gameobject */

        private void Awake()
        {
            m_Agent = GetComponent<NavMeshAgent>();
            player = FindObjectOfType<PlayerManager>();
        }

        private void Update()
        {
            /* Update the target */
            target = player.transform;

            /* Update the agent destination */
            m_Agent.SetDestination(target.position);
        }
    }
}
