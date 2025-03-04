using JetBrains.Annotations;
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


namespace ECS
{

	[UpdateBefore(typeof(GridSystem))]
	[BurstCompile]
	public partial struct BoidsSpawningSystem : ISystem
	{
		public const float SpawningSizeProp = 0.95f;

        private EntityQuery boidsQuery;
		private uint seed;

		public void OnCreate(ref SystemState state)
		{
			boidsQuery = new EntityQueryBuilder(AllocatorManager.Persistent).WithAll<BoidData, LocalTransform>().Build(ref state);

			seed = (uint)DateTime.Now.Ticks;

            state.RequireForUpdate<BoidsECSManagedInfo>();
		}

		public void OnDestroy(ref SystemState state)
		{
		}

		public void OnUpdate(ref SystemState state)
		{
			// get the managed info
            Entity entity = SystemAPI.GetSingletonEntity<BoidsECSControllerInfo>();
            BoidsECSManagedInfo managedInfo = state.EntityManager.GetComponentObject<BoidsECSManagedInfo>(entity);

			// update the number of boids
			int numBoids = boidsQuery.CalculateEntityCount();
			managedInfo.numBoids = numBoids;
			managedInfo.managedBoidsObj.NumBoids = numBoids;

			// check if we have to spawn new boids
			int idealNumBoids = managedInfo.managedBoidsObj.IdealNumBoids;
			if (idealNumBoids > numBoids)
			{
				// spawn new boids
				SpawnNewBoids(ref state, idealNumBoids - numBoids);
			}
            else if (idealNumBoids < numBoids) 
            {
                // clear all boids and maybe next tick we'll spawn new ones
            }
        }

		private void SpawnNewBoids(ref SystemState state, int numBoids)
		{
			if (numBoids <= 0)
				return;

			// get the boids controller info and create a random object
			BoidsECSControllerInfo boidsCtrlInfo = SystemAPI.GetSingleton<BoidsECSControllerInfo>();
			BoidBehaviourInfo boidBehaviour = SystemAPI.GetSingleton<BoidBehaviourInfo>();
			Unity.Mathematics.Random random = new Unity.Mathematics.Random(seed);

			// create the command buffer to pack all creation operations in one go
			var ecb = new EntityCommandBuffer(Allocator.Temp);

			for (int i = 0; i < numBoids; ++i)
			{
				// instantiate the boid visuals
				Entity boidEntity = ecb.Instantiate(boidsCtrlInfo.boidPrefab);

				// calculate a random position, rotation and with it calculate the velocity
				float2 pos = random.NextFloat2();
                pos.x = SpawningSizeProp * math.lerp(boidsCtrlInfo.boundsMin.x, boidsCtrlInfo.boundsMax.x, pos.x);
                pos.y = SpawningSizeProp * math.lerp(boidsCtrlInfo.boundsMin.y, boidsCtrlInfo.boundsMax.y, pos.y);
				float angle = random.NextFloat() * math.PI2;
				float2 dir = new float2(math.cos(angle), math.sin(angle));
                float2 vel = dir * boidBehaviour.minSpeed;
				quaternion rot = quaternion.AxisAngle(new float3(0.0f, 0.0f, 1.0f), angle);

				// add the boid data
				ecb.AddComponent(boidEntity, new BoidData
				{
					pos = pos,
					vel = vel,
                    cellIndex = -1,
                });
				ecb.SetComponent<LocalTransform>(boidEntity, new LocalTransform
				{
					Position = new float3(pos.x, pos.y, 0.0f),
					Rotation = rot,
					Scale = 1.0f,
				});
            }

			// update the seed
			seed = random.NextUInt();

			// execute and dispose of the ECB
			ecb.Playback(state.EntityManager);
			ecb.Dispose();
		}
    }

}
