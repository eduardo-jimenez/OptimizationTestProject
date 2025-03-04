using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using static JobsGrid;


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
            GridData gridData = SystemAPI.ManagedAPI.GetSingleton<GridData>();

            // schedule the job to update the boids
            UpdateBoidsJob job = new UpdateBoidsJob
            {
                cells = gridData.cells,
                cellsLookup = SystemAPI.GetComponentLookup<GridCellInfo>(true),
                boidsInCellsLookup = SystemAPI.GetBufferLookup<BoidInCellBufferData>(true),

                deltaTime = SystemAPI.Time.DeltaTime,
                boundsMin = boidsCtrlInfo.boundsMin,
                boundsMax = boidsCtrlInfo.boundsMax,
                boundsSize = boidsCtrlInfo.boundsMax - boidsCtrlInfo.boundsMin,
                gridSize = boidsCtrlInfo.gridSize,

                boidInfo = boidsBehaviourInfo,
            };

            JobHandle jobHandle = job.ScheduleParallel(boidsQuery, state.Dependency);
            jobHandle.Complete();
        }
    }

    /// <summary>
    /// This structure holds the information regarding cells that we store to sort them and iterate over them in order
    /// </summary>
    public struct CellInRadiusInfo
    {
        public Entity cellEntity;
        public float distSq;
    }

    [BurstCompile]
    public partial struct UpdateBoidsJob : IJobEntity
    {
        [ReadOnly] public NativeArray<Entity> cells;
        [ReadOnly] public ComponentLookup<GridCellInfo> cellsLookup;
        [ReadOnly] public BufferLookup<BoidInCellBufferData> boidsInCellsLookup;

        [ReadOnly] public BoidBehaviourInfo boidInfo;

        public float deltaTime;
        public float2 boundsMin;
        public float2 boundsMax;
        public float2 boundsSize;
        public int2 gridSize;

        public void Execute(ref BoidData data, 
                            ref LocalTransform transform,
                            [ChunkIndexInQuery] int sortKey, Entity entity)
        {
            // get the info from the boid
            float2 vel = data.vel;
            float2 pos = transform.Position.xy;
            float2 dir = transform.Right().xy;

            // gather the nearby boids
            NativeList<CellInRadiusInfo> cellsInRadius = new NativeList<CellInRadiusInfo>(64, AllocatorManager.Temp);
            NativeList<BoidInCellInfoPlusDist> nearbyBoids = new NativeList<BoidInCellInfoPlusDist>(boidInfo.numBoidsToHandle * 2, AllocatorManager.Temp);

            // get the cells in radius
            float maxRadius = math.max(math.max(boidInfo.maxSeparationRadius, boidInfo.cohesionRadius), boidInfo.alignmentRadius);
            FillCellsInRadius(pos, maxRadius, ref cellsInRadius);

            // ask the grid for the nearby boids infos
            FindNearestBoidsInRadius(pos, maxRadius, entity, boidInfo.numBoidsToHandle, cellsInRadius, ref nearbyBoids);

            // update the forces with them
            UpdateForces(pos, dir, vel, nearbyBoids, ref data, ref transform);

            // release the created lists
            cellsInRadius.Dispose();
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

        #region Nearby Boid Finding

        private void FillCellsInRadius(float2 pos, float radius, ref NativeList<CellInRadiusInfo> cellsInRadius)
        {
            //Profiler.BeginSample("Fill Cells in Radius");

            // we'll first need to get the cell positions we have to iterate over
            float minX = pos.x - radius;
            float maxX = pos.x + radius;
            float minY = pos.y - radius;
            float maxY = pos.y + radius;

            int2 minPos = GetCell(minX, minY);
            int2 maxPos = GetCell(maxX, maxY);

            // get the list of all cells in the radius
            float radiusSq = radius * radius;
            for (int iy = minPos.y; iy <= maxPos.y; ++iy)
            {
                for (int ix = minPos.x; ix <= maxPos.x; ++ix)
                {
                    int cellIndex = GetIndex(ix, iy);
                    Entity cellEntity = cells[cellIndex];
                    GridCellInfo cellInfo = cellsLookup[cellEntity];
                    float distSq = GetCellDistanceSq(cellInfo, pos);
                    if (distSq <= radiusSq)
                    {
                        cellsInRadius.Add(new CellInRadiusInfo
                        {
                            cellEntity = cellEntity,
                            distSq = distSq,
                        });
                    }
                }
            }

            //Profiler.EndSample();
        }

        public void FindNearestBoidsInRadius(float2 pos, float radius, Entity entity, int maxBoids,
                                             in NativeList<CellInRadiusInfo> cellsInRadius, ref NativeList<BoidInCellInfoPlusDist> nearbyBoids)
        {
            //Profiler.BeginSample("Find Boids in Cells");

            // let's iterate over all the cells in order
            float maxDistSq = 0.0f;
            float radiusSq = radius * radius;
            foreach (var cellInfo in cellsInRadius)
            {
                if (nearbyBoids.Length >= maxBoids && cellInfo.distSq > maxDistSq)
                    continue;

                var boidsInCell = boidsInCellsLookup[cellInfo.cellEntity];
                foreach (BoidInCellBufferData boidInfo in boidsInCell)
                {
                    // if the boid is within radius add it to the list
                    float2 boidPos = boidInfo.pos;
                    float distSq = math.distancesq(boidPos, pos);
                    if (distSq <= radiusSq)
                    {
                        float dist = Mathf.Sqrt(distSq);

                        // find the index where to insert it
                        int indexToInsert = -1;
                        for (int i = 0; i < nearbyBoids.Length; ++i)
                        {
                            if (nearbyBoids[i].distance > dist)
                            {
                                indexToInsert = i;
                                break;
                            }
                        }

                        if (indexToInsert < 0)
                        {
                            // add it at the end
                            nearbyBoids.Add(new BoidInCellInfoPlusDist
                            {
                                entity = boidInfo.boid,
                                pos = boidPos,
                                dir = boidInfo.dir,
                                distance = dist,
                            });
                            maxDistSq = distSq;
                        }
                        else
                        {
                            // insert it in the given position
                            nearbyBoids.InsertRange(indexToInsert, 1);
                            nearbyBoids[indexToInsert] = new BoidInCellInfoPlusDist
                            {
                                entity = boidInfo.boid,
                                pos = boidPos,
                                dir = boidInfo.dir,
                                distance = dist,
                            };
                        }
                    }
                }

                // remove the unnecessary boids
                if (nearbyBoids.Length > maxBoids)
                {
                    nearbyBoids.RemoveRange(maxBoids, nearbyBoids.Length - maxBoids);
                }
            }

            //Profiler.EndSample();
        }

        #endregion

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

        #region Cell Helper Methods

        public int2 GetCell(float x, float y)
        {
            float2 posNorm = (new float2(x, y) - boundsMin) / boundsSize;
            int ix = math.clamp((int)math.floor(posNorm.x * gridSize.x), 0, gridSize.x - 1);
            int iy = math.clamp((int)math.floor(posNorm.y * gridSize.y), 0, gridSize.y - 1);

            return new int2(ix, iy);
        }

        public int GetIndex(int x, int y)
        {
            return y * gridSize.x + x;
        }

        public float GetCellDistanceSq(GridCellInfo cell, float2 pos)
        {
            float2 nearPos;

            if (pos.x < cell.min.x)
                nearPos.x = cell.min.x;
            else if (pos.x > cell.max.x)
                nearPos.x = cell.max.x;
            else
                nearPos.x = pos.x;

            if (pos.y < cell.min.y)
                nearPos.y = cell.min.y;
            else if (pos.y > cell.max.y)
                nearPos.y = cell.max.y;
            else
                nearPos.y = pos.y;

            float distSq = math.distancesq(pos, nearPos);

            return distSq;
        }

        #endregion
    }

}
