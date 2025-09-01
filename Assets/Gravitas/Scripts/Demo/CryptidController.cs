using UnityEngine;
using UnityEngine.AI;

namespace Gravitas
{
    public class CryptidController : MonoBehaviour
    {
        [Header("Chase Settings")]
        [SerializeField] private float chaseSpeed = 3.5f;
        [SerializeField] private float stoppingDistance = 2f;

        private NavMeshAgent navMeshAgent;
        private Transform target;
        private bool isChasing = false;

        void Start()
        {
            navMeshAgent = GetComponent<NavMeshAgent>();
            if (navMeshAgent == null)
            {
                navMeshAgent = gameObject.AddComponent<NavMeshAgent>();
            }

            // Configure NavMeshAgent
            navMeshAgent.speed = chaseSpeed;
            navMeshAgent.stoppingDistance = stoppingDistance;
            navMeshAgent.autoBraking = true;
        }

        void Update()
        {
            if (isChasing && target != null && navMeshAgent != null)
            {
                // Set destination to target's position
                navMeshAgent.SetDestination(target.position);
            }
        }

        /// <summary>
        /// Start chasing the specified target
        /// </summary>
        /// <param name="targetTransform">The transform to chase</param>
        public void StartChasing(Transform targetTransform)
        {
            target = targetTransform;
            isChasing = true;

            if (navMeshAgent != null)
            {
                navMeshAgent.isStopped = false;
                Debug.Log($"Cryptid started chasing {targetTransform.name}");
            }
        }

        /// <summary>
        /// Stop chasing
        /// </summary>
        public void StopChasing()
        {
            isChasing = false;
            target = null;

            if (navMeshAgent != null)
            {
                navMeshAgent.isStopped = true;
                Debug.Log("Cryptid stopped chasing");
            }
        }

        /// <summary>
        /// Check if currently chasing
        /// </summary>
        public bool IsChasing => isChasing;
    }
}
