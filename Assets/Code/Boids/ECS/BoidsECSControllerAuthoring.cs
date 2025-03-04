using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


namespace ECS
{

	public class BoidsECSControllerAuthoring : MonoBehaviour
	{
		#region Public Attributes

        [Header("Zone & Grid Parameters")]
        public Bounds bounds = new Bounds(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(320.0f / 9.0f, 20.0f, 0.0f));
        public Vector2Int gridSize = new Vector2Int(64, 36);

        [Header("Boid")]
        public GameObject boidVisualsPrefab;

        [Space(10)]
        public int numBoidsToHandle = 10;
        public float boidMinSpeed = 0.3f;
        public float boidMaxSpeed = 1.5f;

        [Space(10)]
        public float boidCohesionRadius = 1.5f;
        public float boidMaxCohesionForce = 0.5f;

        [Space(10)]
        public float boidMaxSeparationRadius = 0.5f;
        public float boidRadiusForMaxSeparationForce = 0.1f;
        public float boidMaxSeparationForce = 1.0f;

        [Space(10)]
        public float boidAlignmentRadius = 1.0f;
        public float boidDefaultAlignmentForce = 0.5f;
        public int boidNumBoidsForMaxAligmentForce = 5;

        [Space(10)]
        public float boidDistToStartRepulsion = 1.0f;
        public float boidDistForMaxRepulsion = 0.5f;
        public float boidMaxRepulsionForce = 2.0f;


        [Header("Jobs Config")]
        public int numBoidsPerJob = 8;

        #endregion

        #region Private Attributes

        private int numBoids = 0;
        private int idealNumBoids;

        #endregion

        #region Properties

        public int NumBoids
        {
            get => numBoids;
            set => numBoids = value;
        }
        public int IdealNumBoids => idealNumBoids;

        #endregion

        #region Methods

        public void AddBoids(int numBoids)
        {
            idealNumBoids += numBoids;
        }

        public void ClearBoids()
        {
            idealNumBoids = 0;
        }

        #endregion

        #region Baker

        class Baker : Baker<BoidsECSControllerAuthoring>
		{
			public override void Bake(BoidsECSControllerAuthoring authoring)
			{
				var entity = GetEntity(TransformUsageFlags.Dynamic);
                var boidPrefabEntity = GetEntity(authoring.boidVisualsPrefab, TransformUsageFlags.Dynamic | TransformUsageFlags.NonUniformScale);

                AddComponent(entity, new BoidsECSControllerInfo
				{
                    boundsMin = new float2(authoring.bounds.min.x, authoring.bounds.min.y),
                    boundsMax = new float2(authoring.bounds.max.x, authoring.bounds.max.y),
                    gridSize = new int2(authoring.gridSize.x, authoring.gridSize.y),

                    boidPrefab = boidPrefabEntity,

                    numBoidsPerJob = authoring.numBoidsPerJob,
				});

                AddComponent(entity, new BoidBehaviourInfo
                {
                    numBoidsToHandle = authoring.numBoidsToHandle,

                    minSpeed = authoring.boidMinSpeed,
                    maxSpeed = authoring.boidMaxSpeed,

                    cohesionRadius = authoring.boidCohesionRadius,
                    maxCohesionForce = authoring.boidMaxCohesionForce,

                    maxSeparationRadius = authoring.boidMaxSeparationRadius,
                    radiusForMaxSeparationForce = authoring.boidRadiusForMaxSeparationForce,
                    maxSeparationForce = authoring.boidMaxSeparationForce,

                    alignmentRadius = authoring.boidAlignmentRadius,
                    defaultAlignmentForce = authoring.boidDefaultAlignmentForce,
                    numBoidsForMaxAligmentForce = authoring.boidNumBoidsForMaxAligmentForce,

                    distToStartRepulsion = authoring.boidDistToStartRepulsion,
                    distForMaxRepulsion = authoring.boidDistForMaxRepulsion,
                    maxRepulsionForce = authoring.boidMaxRepulsionForce,
                });

                AddComponentObject(entity, new BoidsECSManagedInfo
                {
                    managedBoidsObj = authoring,
                    numBoids = 0,
                });

                AddComponent(entity, new GridInfo
                {
                    min = new float2(authoring.bounds.min.x, authoring.bounds.min.y),
                    max = new float2(authoring.bounds.max.x, authoring.bounds.max.y),
                    size = new float2(authoring.bounds.max.x - authoring.bounds.min.x, authoring.bounds.max.y - authoring.bounds.min.y),
                    gridSize = new int2(authoring.gridSize.x, authoring.gridSize.y),
                });

                AddComponentObject(entity, new GridData()
                {
                    initialized = false,
                });
            }
        }

		#endregion
	}

	public struct BoidsECSControllerInfo : IComponentData
	{
        public float2 boundsMin;
        public float2 boundsMax;
        public int2 gridSize;

        public Entity boidPrefab;

        public int numBoidsPerJob;
    }

    public class BoidsECSManagedInfo : IComponentData
    {
        public BoidsECSControllerAuthoring managedBoidsObj;

        public int numBoids;
    }

    public struct BoidBehaviourInfo : IComponentData
    {
        public int numBoidsToHandle;

        public float minSpeed;
        public float maxSpeed;

        public float cohesionRadius;
        public float maxCohesionForce;

        public float maxSeparationRadius;
        public float radiusForMaxSeparationForce;
        public float maxSeparationForce;

        public float alignmentRadius;
        public float defaultAlignmentForce;
        public int numBoidsForMaxAligmentForce;

        public float distToStartRepulsion;
        public float distForMaxRepulsion;
        public float maxRepulsionForce;
    }

}
