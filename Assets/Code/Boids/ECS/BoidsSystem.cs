using ECS;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.VisualScripting;
using UnityEngine;
using static JobsGrid;
using static UnityEditor.PlayerSettings;


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

            JobHandle jobHandle = job.ScheduleParallel(boidsQuery, state.Dependency);
            jobHandle.Complete();
        }
    }

    [BurstCompile]
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
            float2 pos = transform.Position.xy;
            float2 dir = transform.Right().xy;

            // gather the nearby boids
            NativeList<BoidInCellInfoPlusDist> nearbyBoids = new NativeList<BoidInCellInfoPlusDist>(boidInfo.numBoidsToHandle * 2, AllocatorManager.TempJob);

            // update the forces with them
            UpdateForces(pos, dir, vel, nearbyBoids, ref data, ref transform);

            // release the created lists
            nearbyBoids.Dispose();
        }

        private void UpdateForces(float2 pos, float2 dir, float2 vel, in NativeList<BoidInCellInfoPlusDist> nearbyBoids,
                                  ref BoidData data, ref LocalTransform transform)
        {
            // calculate the forces to apply
            float2 cohesionForce = UpdateCohesion(pos, boidInfo.cohesionRadius, boidInfo.maxCohesionForce, nearbyBoids);
            float2 alignmentForce = UpdateAlignment(pos, boidInfo.alignmentRadius, boidInfo.defaultAlignmentForce, nearbyBoids);
            float2 separationForce = UpdateSeparation(pos, boidInfo.maxSeparationRadius, boidInfo.radiusForMaxSeparationForce, boidInfo.maxSeparationForce, nearbyBoids);
            float2 repulsionForce = UpdateBorderRepulsion(pos, boundsMin, boundsMax, boidInfo.distToStartRepulsion, boidInfo.distForMaxRepulsion, boidInfo.maxRepulsionForce);
            float2 totalForce = cohesionForce + alignmentForce + separationForce + repulsionForce;

            // apply the force to the velocity
            vel += totalForce * deltaTime;

            // make sure the velocity is within the minimum and maximum
            float speed = math.length(vel);
            if (speed < Mathf.Epsilon)
                vel = dir * boidInfo.minSpeed;
            else if (speed < boidInfo.minSpeed)
                vel *= boidInfo.minSpeed / speed;
            else if (speed > boidInfo.maxSpeed)
                vel *= boidInfo.maxSpeed / speed;

            // update the position with the current velocity
            float3 offset = new float3(deltaTime * vel.x, deltaTime * vel.y, 0.0f);
            transform.Position += offset;

            // update the direction
            dir = math.normalize(vel);
            float angle = math.atan2(dir.y, dir.x);
            transform.Rotation = quaternion.AxisAngle(new float3(0.0f, 0.0f, 1.0f), angle);

            // update the velocity in the boid data
            data.vel = vel;
        }

        #region Forces Methods

        /// <summary>
        /// Returns a force to try to get boids to 'fly' or 'swim' in a flock/bank
        /// </summary>
        private float2 UpdateCohesion(float2 pos, float cohesionRadius, float maxCohesionForce,
                                      in NativeList<BoidInCellInfoPlusDist> nearbyBoidsInfo)
        {
            float2 cohesionForce = new float2(0.0f, 0.0f);
            float2 flockCenter = new float2(0.0f, 0.0f);
            int numCohesionBoids = 0;

            // calculate the center of the boids around
            foreach (var boidInfo in nearbyBoidsInfo)
            {
                if (boidInfo.distance < cohesionRadius)
                {
                    ++numCohesionBoids;
                    flockCenter += boidInfo.pos;
                }
            }

            if (numCohesionBoids > 0)
            {
                flockCenter *= 1.0f / (float)numCohesionBoids;

                // create a force towards the flock center
                float2 dirToCenter = flockCenter - pos;
                if (math.lengthsq(dirToCenter) > 1.0f)
                    dirToCenter = math.normalize(dirToCenter);
                cohesionForce = dirToCenter * maxCohesionForce;
            }

            //Profiler.EndSample();

            return cohesionForce;
        }

        /// <summary>
        /// Returns a force to keep boids from colliding with each other
        /// </summary>
        private float2 UpdateSeparation(float2 pos, float maxSeparationRadius, float radiusForMaxSeparationForce, float maxSeparationForce,
                                        in NativeList<BoidInCellInfoPlusDist> nearbyBoidsInfo)
        {
            float2 separationForce = new float2(0.0f, 0.0f);

            foreach (var boidInfo in nearbyBoidsInfo)
            {
                // go adding the forces to separate the boid from nearby boids
                float distToBoid = boidInfo.distance;
                if (distToBoid < maxSeparationRadius)
                {
                    // calculate the direction and distance to this boid
                    float2 boidPos = boidInfo.pos;
                    float2 dirToBoid = boidPos - pos;

                    // if at the exact same position we don't do calcs since they become unstable
                    if (distToBoid > math.EPSILON)
                    {
                        // calculate the force to apply 
                        dirToBoid *= 1.0f / distToBoid;
                        float forceT = 1.0f - math.clamp((distToBoid - radiusForMaxSeparationForce) / (maxSeparationRadius - radiusForMaxSeparationForce), 0.0f, 1.0f);
                        float forceAmount = maxSeparationForce * forceT;
                        float2 force = -dirToBoid * forceAmount;

                        // add it to the total amount
                        separationForce += force;
                    }
                }
            }

            return separationForce;
        }

        /// <summary>
        /// Returns a force to try to get all the boids looking in the same direction
        /// </summary>
        private float2 UpdateAlignment(float2 pos, float alignmentRadius, float defaultAlignmentForce,
                                       in NativeList<BoidInCellInfoPlusDist> nearbyBoidsInfo)
        {
            float2 alignmentForce = new float2(0.0f, 0.0f);

            // average the direction of the nearby boids
            float2 avgDir = new float2(0.0f, 0.0f);
            int numAlignmentBoids = 0;
            foreach (var boidInfo in nearbyBoidsInfo)
            {
                if (boidInfo.distance <= alignmentRadius)
                {
                    avgDir += boidInfo.dir;
                    ++numAlignmentBoids;
                }
            }

            if (numAlignmentBoids > 0)
            {
                // add a force in that direction
                avgDir = math.normalize(avgDir);
                alignmentForce = avgDir * defaultAlignmentForce;
            }

            return alignmentForce;
        }

        /// <summary>
        /// If close to the borders this will return a force to move them away
        /// </summary>
        private float2 UpdateBorderRepulsion(float2 pos, float2 min, float2 max,
                                             float distToStartRepulsion, float distForMaxRepulsion, float maxRepulsionForce)
        {
            float2 repulsionForce = new float2(0.0f, 0.0f);

            float maxRepulsionDist = distToStartRepulsion - distForMaxRepulsion;

            // check first left and right
            float repulsionDistLeft = (min.x + distToStartRepulsion) - pos.x;
            float repulsionDistRight = pos.x - (max.x - distToStartRepulsion);
            if (repulsionDistLeft > 0.0f)
            {
                float forceT = Mathf.Clamp01(repulsionDistLeft / maxRepulsionDist);
                float forceAmount = maxRepulsionForce * forceT;
                repulsionForce.x += forceAmount;
            }
            else if (repulsionDistRight > 0.0f)
            {
                float forceT = Mathf.Clamp01(repulsionDistRight / maxRepulsionDist);
                float forceAmount = maxRepulsionForce * forceT;
                repulsionForce.x -= forceAmount;
            }

            // check now top and bottom
            float repulsionDistTop = (min.y + distToStartRepulsion) - pos.y;
            float repulsionDistBottom = pos.y - (max.y - distToStartRepulsion);
            if (repulsionDistTop > 0.0f)
            {
                float forceT = Mathf.Clamp01(repulsionDistTop / maxRepulsionDist);
                float forceAmount = maxRepulsionForce * forceT;
                repulsionForce.y += forceAmount;
            }
            else if (repulsionDistBottom > 0.0f)
            {
                float forceT = Mathf.Clamp01(repulsionDistBottom / maxRepulsionDist);
                float forceAmount = maxRepulsionForce * forceT;
                repulsionForce.y -= forceAmount;
            }

            return repulsionForce;
        }

        #endregion
    }

}
