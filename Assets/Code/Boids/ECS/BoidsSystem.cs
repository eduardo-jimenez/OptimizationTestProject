using ECS;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;


namespace ECS
{

    [UpdateAfter(typeof(BoidsSpawningSystem))]
    [BurstCompile]
    public partial struct BoidsSystem : ISystem
    {
        private EntityQuery boidsQuery;

        public void OnCreate(ref SystemState state)
        {
            boidsQuery = new EntityQueryBuilder(AllocatorManager.Persistent).WithAll<BoidData, LocalTransform>().Build(ref state);

            state.RequireForUpdate<BoidsECSControllerInfo>();
        }

        public void OnDestroy(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            // if delta time is not positive let's not do anything (we're paused)
            if (SystemAPI.Time.DeltaTime <= 0.0f)
                return;

            // get the boids controller info
            BoidsECSControllerInfo boidsCtrlInfo = SystemAPI.GetSingleton<BoidsECSControllerInfo>();
            BoidBehaviourInfo boidsBehaviourInfo = SystemAPI.GetSingleton<BoidBehaviourInfo>();

            // schedule the job to update the boids
            UpdateBoidsJob job = new UpdateBoidsJob
            {
                deltaTime = SystemAPI.Time.DeltaTime,
                boundsMin = boidsCtrlInfo.boundsMin,
                boundsMax = boidsCtrlInfo.boundsMax,
                gridSize = boidsCtrlInfo.gridSize,

                boidInfo = boidsBehaviourInfo,
            };

            JobHandle jobHandle = job.Schedule(boidsQuery, state.Dependency);
            jobHandle.Complete();
        }
    }

    public partial struct UpdateBoidsJob : IJobEntity
    {
        public float deltaTime;
        public float2 boundsMin;
        public float2 boundsMax;
        public int2 gridSize;

        [ReadOnly] public BoidBehaviourInfo boidInfo;

        public void Execute(ref BoidData data, 
                            ref LocalTransform transform,
                            [ChunkIndexInQuery] int sortKey, Entity entity)
        {
            float2 vel = data.vel;

            // update the position with the current velocity
            float3 offset = new float3(deltaTime * vel.x, deltaTime * vel.y, 0.0f);
            transform.Position += offset;

            // update the velocity in the boid data
            data.vel = vel;
        }
    }

}
