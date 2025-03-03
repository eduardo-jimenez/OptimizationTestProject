using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using static JobsGrid;


/// <summary>
/// Structure holding the information of a boid for the jobs system
/// </summary>
[BurstCompile]
public struct JobsBoid
{
    #region Public Attributes

    // Generic Params
    private float minSpeed;
    private float maxSpeed;

    // Cohesion
    private float cohesionRadius;
    private float maxCohesionForce;

    // Separation
    private float maxSeparationRadius;
    private float radiusForMaxSeparationForce;
    private float maxSeparationForce;

    // Alignment
    private float alignmentRadius;
    private float defaultAlignmentForce;
    private int numBoidsForMaxAligmentForce;

    // Border Repulsion
    private float distToStartRepulsion;
    private float distForMaxRepulsion;
    private float maxRepulsionForce;

    // Grid Parameters
    private int maxBoidsToHandle;


    // State
    private bool initialized;
    private int index;

    private float2 pos;
    private float2 dir;
    private float2 vel;

    private float2 totalForce;
    private float2 cohesionForce;
    private float2 separationForce;
    private float2 alignmentForce;
    private float2 repulsionForce;

    #endregion

    #region Properties

    public bool Initialized => initialized;

    public float MinSpeed => minSpeed;
    public float MaxSpeed => maxSpeed;
    public float CohesionRadius => cohesionRadius;
    public float MaxCohesionForce => maxCohesionForce;
    public float MaxSeparationRadius => maxSeparationRadius;
    public float RadiusForMaxSeparationForce => radiusForMaxSeparationForce;
    public float MaxSeparationForce => maxSeparationForce;
    public float AlignmentRadius => alignmentRadius;
    public float DefaultAlignmentForce => defaultAlignmentForce;
    public int NumBoidsForMaxAlignmentForce => numBoidsForMaxAligmentForce;
    public float DistToStartRepulsion => distToStartRepulsion;
    public float DistForMaxRepulsion => distForMaxRepulsion;
    public float MaxRepulsionForce => maxRepulsionForce;

    public int Index => index;

    public float2 Vel
    {
        get => vel;
        set => vel = value;
    }
    public float2 Pos
    {
        get => pos;
        set => pos = value;
    }
    public float2 Dir
    {
        get => dir;
        set => dir = value;
    }
    public float2 TotalForce
    {
        get => totalForce;
        set => totalForce = value;
    }
    public float2 CohesionForce
    {
        get => cohesionForce;
        set => cohesionForce = value;
    }
    public float2 SeparationForce
    {
        get => separationForce;
        set => separationForce = value;
    }
    public float2 AlignmentForce
    {
        get => alignmentForce;
        set => alignmentForce = value;
    }
    public float2 RepulsionForce
    {
        get => repulsionForce;
        set => repulsionForce = value;
    }

    #endregion

    #region Initialization

    public void Init(JobsBoidObj boid, int index)
    {
        // assign the index
        this.index = index;

        // assign the public parameters of the boid first
        minSpeed = boid.minSpeed;
        maxSpeed = boid.maxSpeed;
        cohesionRadius = boid.cohesionRadius;
        maxCohesionForce = boid.maxCohesionForce;
        maxSeparationRadius = boid.maxSeparationRadius;
        radiusForMaxSeparationForce = boid.radiusForMaxSeparationForce;
        maxSeparationForce = boid.maxSeparationForce;
        alignmentRadius = boid.alignmentRadius;
        defaultAlignmentForce = boid.defaultAlignmentForce;
        numBoidsForMaxAligmentForce = boid.numBoidsForMaxAligmentForce;
        distToStartRepulsion = boid.distToStartRepulsion;
        distForMaxRepulsion = boid.distForMaxRepulsion;
        maxRepulsionForce = boid.maxRepulsionForce;
        maxBoidsToHandle = boid.maxBoidsToHandle;

        // set the position and orientation
        pos = boid.Pos;
        dir = boid.Dir;
        vel = boid.Vel;

        // reset the state
        totalForce = new float2(0.0f, 0.0f);
        cohesionForce = new float2(0.0f, 0.0f);
        separationForce = new float2(0.0f, 0.0f);
        alignmentForce = new float2(0.0f, 0.0f);
        repulsionForce = new float2(0.0f, 0.0f);

        // mark as initialized
        initialized = true;
    }

    #endregion

    #region Update Forces Methods

    /// <summary>
    /// Updates the simulation of this boid
    /// </summary>
    /// <param name="dt"></param>
    public void UpdateForces(float dt, Bounds bounds, in JobsGrid.GridInfo gridInfo, in NativeArray<JobsGrid.Cell> cells, in SharedLists<JobsGrid.BoidInCellInfo> boidsInCells,
                             in NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist> nearbyBoidsInfoPerBoid)
    {
        //Profiler.BeginSample("Update Forces");

        // generate the forces
        var nearbyBoids = nearbyBoidsInfoPerBoid.GetValuesForKey(index);
        UpdateCohesion(nearbyBoids);
        UpdateAlignment(nearbyBoids);
        UpdateSeparation(nearbyBoids);
        UpdateBorderRepulsion(bounds);
        totalForce = cohesionForce + alignmentForce + separationForce + repulsionForce;

        // apply the force to the velocity
        vel += totalForce * dt;

        // make sure the velocity is within the minimum and maximum
        float speed = math.length(vel);
        if (speed < Mathf.Epsilon)
            vel = Dir * minSpeed;
        else if (speed < minSpeed)
            vel *= minSpeed / speed;
        else if (speed > maxSpeed)
            vel *= maxSpeed / speed;

        // update the movement
        pos += vel * dt;
        dir = math.normalize(vel);

        //Profiler.EndSample();
    }

    #endregion

    #region Forces Methods

    /// <summary>
    /// Returns a force to try to get boids to 'fly' or 'swim' in a flock/bank
    /// </summary>
    private void UpdateCohesion(in NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist>.Enumerator nearbyBoidsInfo)
    {
        //Profiler.BeginSample("UpdateCohesion");

        cohesionForce = new float2(0.0f, 0.0f);

        float2 pos = Pos;
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
    }

    /// <summary>
    /// Returns a force to keep boids from colliding with each other
    /// </summary>
    private void UpdateSeparation(in NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist>.Enumerator nearbyBoidsInfo)
    {
        //Profiler.BeginSample("UpdateSeparation");

        separationForce = new float2(0.0f, 0.0f);

        float2 pos = Pos;
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
                if (distToBoid > Mathf.Epsilon)
                {
                    // calculate the force to apply 
                    dirToBoid *= 1.0f / distToBoid;
                    float forceT = 1.0f - Mathf.Clamp01((distToBoid - radiusForMaxSeparationForce) / (maxSeparationRadius - radiusForMaxSeparationForce));
                    float forceAmount = maxSeparationForce * forceT;
                    float2 force = -dirToBoid * forceAmount;

                    // add it to the total amount
                    separationForce += force;
                }
            }
        }

        //Profiler.EndSample();
    }

    /// <summary>
    /// Returns a force to try to get all the boids looking in the same direction
    /// </summary>
    private void UpdateAlignment(in NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist>.Enumerator nearbyBoidsInfo)
    {
        //Profiler.BeginSample("UpdateAlignment");

        // average the direction of the nearby boids
        float2 avgDir = new float2(0.0f, 0.0f);
        float2 pos = Pos;
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

        //Profiler.EndSample();
    }

    /// <summary>
    /// If close to the borders this will return a force to move them away
    /// </summary>
    private void UpdateBorderRepulsion(Bounds bounds)
    {
        //Profiler.BeginSample("UpdateBorderRepulsion");

        repulsionForce = new float2(0.0f, 0.0f);

        // get the position and bounds
        float2 pos = Pos;
        float2 min = new float2(bounds.min.x, bounds.min.y);
        float2 max = new float2(bounds.max.x, bounds.max.y);
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

        //Profiler.EndSample();
    }

    #endregion

    #region Find Cells in Radius

    [BurstCompile]
    public void FindCellsInRadius(in JobsGrid.GridInfo gridInfo, in NativeArray<JobsGrid.Cell> cells,
                                   in SharedLists<JobsGrid.BoidInCellInfo> boidsInCells,
                                   in NativeParallelMultiHashMap<int, JobsGrid.CellInRadiusInfo>.ParallelWriter cellsInRadiusPerBoid)
    {
        float maxRadius = math.max(math.max(maxSeparationRadius, cohesionRadius), alignmentRadius);
        FillCellsInRadius(pos, maxRadius, this, maxBoidsToHandle, gridInfo, cells, boidsInCells, cellsInRadiusPerBoid);
    }

    [BurstCompile]
    private void FillCellsInRadius(float2 pos, float radius, JobsBoid boidToIgnore, int maxBoids,
                                   in JobsGrid.GridInfo gridInfo, in NativeArray<JobsGrid.Cell> cells,
                                   in SharedLists<JobsGrid.BoidInCellInfo> boidsInCells,
                                   in NativeParallelMultiHashMap<int, JobsGrid.CellInRadiusInfo>.ParallelWriter cellsInRadiusPerBoid)
    {
        //Profiler.BeginSample("Fill Cells in Radius");

        // we'll first need to get the cell positions we have to iterate over
        float minX = pos.x - radius;
        float maxX = pos.x + radius;
        float minY = pos.y - radius;
        float maxY = pos.y + radius;

        int2 minPos = GetCell(minX, minY, gridInfo);
        int2 maxPos = GetCell(maxX, maxY, gridInfo);

        // get the list of all cells in the radius
        float radiusSq = radius * radius;
        for (int iy = minPos.y; iy <= maxPos.y; ++iy)
        {
            for (int ix = minPos.x; ix <= maxPos.x; ++ix)
            {
                int cellIndex = GetIndex(ix, iy, gridInfo);
                JobsGrid.Cell cell = cells[cellIndex];
                float distSq = GetCellDistanceSq(cell, pos);
                if (distSq <= radiusSq && boidsInCells.GetLength(cellIndex) > 0)
                    cellsInRadiusPerBoid.Add(index, new JobsGrid.CellInRadiusInfo
                    {
                        cellIndex = cellIndex,
                        distSq = distSq,
                    });
            }
        }

        //Profiler.EndSample();
    }

    #endregion

    #region Find Nearby Boids Methods

    [BurstCompile]
    public void FindBoidsInRadius(in JobsGrid.GridInfo gridInfo, in NativeArray<JobsGrid.Cell> cells,
                                  in SharedLists<JobsGrid.BoidInCellInfo> boidsInCells,
                                  in NativeParallelMultiHashMap<int, JobsGrid.CellInRadiusInfo> cellsInRadiusPerBoid,
                                  in NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist>.ParallelWriter nearbyBoidsInfoPerBoid)
    {
        // ask the grid for the nearby boids infos
        float maxRadius = math.max(math.max(maxSeparationRadius, cohesionRadius), alignmentRadius);
        var cellsInRadius = cellsInRadiusPerBoid.GetValuesForKey(index);
        FindNearestBoidsInRadius(pos, maxRadius, this, maxBoidsToHandle, gridInfo, cells, boidsInCells, index, cellsInRadius, nearbyBoidsInfoPerBoid);
    }

    [BurstCompile]
    public void FindNearestBoidsInRadius(float2 pos, float radius, JobsBoid boidToIgnore, int maxBoids,
                                         in JobsGrid.GridInfo gridInfo, in NativeArray<JobsGrid.Cell> cells, in SharedLists<JobsGrid.BoidInCellInfo> boidsInCells,
                                         int index, in NativeParallelMultiHashMap<int, JobsGrid.CellInRadiusInfo>.Enumerator cellsInRadius,
                                         in NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist>.ParallelWriter nearbyBoidsPerBoid)
    {
        //Profiler.BeginSample("Find Boids in Cells");

        // let's iterate over all the cells in order
        float maxDistSq = 0.0f;
        float radiusSq = radius * radius;
        NativeList<JobsGrid.BoidInCellPlusDist> nearbyBoids = new NativeList<BoidInCellPlusDist>(maxBoids * 2, AllocatorManager.TempJob);
        foreach (var cellInfo in cellsInRadius)
        {
            if (nearbyBoids.Length >= maxBoids && cellInfo.distSq > maxDistSq)
                continue;

            NativeSlice<BoidInCellInfo> boidsInCell = boidsInCells.GetSlice(cellInfo.cellIndex);
            foreach (var boidInfo in boidsInCell)
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
                        nearbyBoids.Add(new BoidInCellPlusDist(boidInfo, dist));
                        maxDistSq = distSq;
                    }
                    else
                    {
                        // insert it in the given position
                        nearbyBoids.InsertRange(indexToInsert, 1);
                        nearbyBoids[indexToInsert] = new BoidInCellPlusDist(boidInfo, dist);
                    }
                }
            }

            // remove the unnecessary boids
            if (nearbyBoids.Length > maxBoids)
            {
                nearbyBoids.RemoveRange(maxBoids, nearbyBoids.Length - maxBoids);
            }
        }

        // add the list of boids to the hash map
        foreach (var boidInfo in nearbyBoids)
            nearbyBoidsPerBoid.Add(index, boidInfo);

        // release the used memory
        nearbyBoids.Dispose();

        //Profiler.EndSample();
    }

    /// <summary>
    /// Returns the cell position for the given 2D position in the world
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int2 GetCell(float x, float y, in JobsGrid.GridInfo gridInfo)
    {
        float2 posNorm = (new float2(x, y) - gridInfo.boundsMin) / gridInfo.boundsSize;
        int ix = math.clamp((int)math.floor(posNorm.x * gridInfo.size.x), 0, gridInfo.size.x - 1);
        int iy = math.clamp((int)math.floor(posNorm.y * gridInfo.size.y), 0, gridInfo.size.y - 1);

        return new int2(ix, iy);
    }

    /// <summary>
    /// Returns the index to use for the given position in the grid
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(int x, int y, in JobsGrid.GridInfo gridInfo)
    {
        return y * gridInfo.size.x + x;
    }

    /// <summary>
    /// Returns the minimum distance squared between the given cell and the given position
    /// </summary>
    public float GetCellDistanceSq(JobsGrid.Cell cell, float2 pos)
    {
        float2 nearPos;

        if (pos.x < cell.Min.x)
            nearPos.x = cell.Min.x;
        else if (pos.x > cell.Max.x)
            nearPos.x = cell.Max.x;
        else
            nearPos.x = pos.x;

        if (pos.y < cell.Min.y)
            nearPos.y = cell.Min.y;
        else if (pos.y > cell.Max.y)
            nearPos.y = cell.Max.y;
        else
            nearPos.y = pos.y;

        float distSq = math.distancesq(pos, nearPos);

        return distSq;
    }

    #endregion

    #region Full Find Neraby Boids

    [BurstCompile]
    public void FullFindBoidsInRadius(in JobsGrid.GridInfo gridInfo, in NativeArray<JobsGrid.Cell> cells,
                                      in SharedLists<JobsGrid.BoidInCellInfo> boidsInCells,
                                      in NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist>.ParallelWriter nearbyBoidsInfoPerBoid)
    {
        NativeList<CellInRadiusInfo> cellsInRadius = new NativeList<CellInRadiusInfo>(64, AllocatorManager.TempJob);

        // get the cells in radius
        float maxRadius = math.max(math.max(maxSeparationRadius, cohesionRadius), alignmentRadius);
        FillCellsInRadius(pos, maxRadius, this, maxBoidsToHandle, gridInfo, cells, boidsInCells, ref cellsInRadius);

        // ask the grid for the nearby boids infos
        FindNearestBoidsInRadius(pos, maxRadius, this, maxBoidsToHandle, gridInfo, cells, boidsInCells, index, cellsInRadius, nearbyBoidsInfoPerBoid);

        // dispose the cells list
        cellsInRadius.Dispose();
    }

    [BurstCompile]
    private void FillCellsInRadius(float2 pos, float radius, JobsBoid boidToIgnore, int maxBoids,
                                   in GridInfo gridInfo, in NativeArray<Cell> cells,
                                   in SharedLists<BoidInCellInfo> boidsInCells,
                                   ref NativeList<CellInRadiusInfo> cellsInRadius)
    {
        //Profiler.BeginSample("Fill Cells in Radius");

        // we'll first need to get the cell positions we have to iterate over
        float minX = pos.x - radius;
        float maxX = pos.x + radius;
        float minY = pos.y - radius;
        float maxY = pos.y + radius;

        int2 minPos = GetCell(minX, minY, gridInfo);
        int2 maxPos = GetCell(maxX, maxY, gridInfo);

        // get the list of all cells in the radius
        float radiusSq = radius * radius;
        for (int iy = minPos.y; iy <= maxPos.y; ++iy)
        {
            for (int ix = minPos.x; ix <= maxPos.x; ++ix)
            {
                int cellIndex = GetIndex(ix, iy, gridInfo);
                Cell cell = cells[cellIndex];
                float distSq = GetCellDistanceSq(cell, pos);
                if (distSq <= radiusSq && boidsInCells.GetLength(cellIndex) > 0)
                    cellsInRadius.Add(new CellInRadiusInfo
                    {
                        cellIndex = cellIndex,
                        distSq = distSq,
                    });
            }
        }

        //Profiler.EndSample();
    }

    [BurstCompile]
    public void FindNearestBoidsInRadius(float2 pos, float radius, JobsBoid boidToIgnore, int maxBoids,
                                         in JobsGrid.GridInfo gridInfo, in NativeArray<JobsGrid.Cell> cells, in SharedLists<JobsGrid.BoidInCellInfo> boidsInCells,
                                         int index, in NativeList<JobsGrid.CellInRadiusInfo> cellsInRadius,
                                         in NativeParallelMultiHashMap<int, JobsGrid.BoidInCellPlusDist>.ParallelWriter nearbyBoidsPerBoid)
    {
        //Profiler.BeginSample("Find Boids in Cells");

        // let's iterate over all the cells in order
        float maxDistSq = 0.0f;
        float radiusSq = radius * radius;
        NativeList<JobsGrid.BoidInCellPlusDist> nearbyBoids = new NativeList<BoidInCellPlusDist>(maxBoids * 2, AllocatorManager.TempJob);
        foreach (var cellInfo in cellsInRadius)
        {
            if (nearbyBoids.Length >= maxBoids && cellInfo.distSq > maxDistSq)
                continue;

            NativeSlice<BoidInCellInfo> boidsInCell = boidsInCells.GetSlice(cellInfo.cellIndex);
            foreach (var boidInfo in boidsInCell)
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
                        nearbyBoids.Add(new BoidInCellPlusDist(boidInfo, dist));
                        maxDistSq = distSq;
                    }
                    else
                    {
                        // insert it in the given position
                        nearbyBoids.InsertRange(indexToInsert, 1);
                        nearbyBoids[indexToInsert] = new BoidInCellPlusDist(boidInfo, dist);
                    }
                }
            }

            // remove the unnecessary boids
            if (nearbyBoids.Length > maxBoids)
            {
                nearbyBoids.RemoveRange(maxBoids, nearbyBoids.Length - maxBoids);
            }
        }

        // add the list of boids to the hash map
        foreach (var boidInfo in nearbyBoids)
            nearbyBoidsPerBoid.Add(index, boidInfo);

        // release the used memory
        nearbyBoids.Dispose();

        //Profiler.EndSample();
    }

    #endregion

    #region Full Update

    /// <summary>
    /// Updates the full simulation of this boid
    /// </summary>
    /// <param name="dt"></param>
    public void FullUpdate(float dt, Bounds bounds, in JobsGrid.GridInfo gridInfo, in NativeArray<JobsGrid.Cell> cells, 
                           in SharedLists<JobsGrid.BoidInCellInfo> boidsInCells)
    {
        NativeList<CellInRadiusInfo> cellsInRadius = new NativeList<CellInRadiusInfo>(64, AllocatorManager.TempJob);
        NativeList<BoidInCellPlusDist> nearbyBoids = new NativeList<BoidInCellPlusDist>(2 * maxBoidsToHandle, AllocatorManager.TempJob);

        // get the cells in radius
        float maxRadius = math.max(math.max(maxSeparationRadius, cohesionRadius), alignmentRadius);
        FillCellsInRadius(pos, maxRadius, this, maxBoidsToHandle, gridInfo, cells, boidsInCells, ref cellsInRadius);

        // ask the grid for the nearby boids infos
        FindNearestBoidsInRadius(pos, maxRadius, this, maxBoidsToHandle, gridInfo, cells, boidsInCells, cellsInRadius, ref nearbyBoids);

        // update the forces with the nearby boids
        UpdateForces(dt, bounds, gridInfo, cells, boidsInCells, nearbyBoids);

        // dispose the cells list
        cellsInRadius.Dispose();
        nearbyBoids.Dispose();
    }

    [BurstCompile]
    public void FindNearestBoidsInRadius(float2 pos, float radius, JobsBoid boidToIgnore, int maxBoids,
                                         in GridInfo gridInfo, in NativeArray<Cell> cells, in SharedLists<BoidInCellInfo> boidsInCells,
                                         in NativeList<CellInRadiusInfo> cellsInRadius, ref NativeList<BoidInCellPlusDist> nearbyBoids)
    {
        //Profiler.BeginSample("Find Boids in Cells");

        // let's iterate over all the cells in order
        float maxDistSq = 0.0f;
        float radiusSq = radius * radius;
        foreach (var cellInfo in cellsInRadius)
        {
            if (nearbyBoids.Length >= maxBoids && cellInfo.distSq > maxDistSq)
                continue;

            NativeSlice<BoidInCellInfo> boidsInCell = boidsInCells.GetSlice(cellInfo.cellIndex);
            foreach (var boidInfo in boidsInCell)
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
                        nearbyBoids.Add(new BoidInCellPlusDist(boidInfo, dist));
                        maxDistSq = distSq;
                    }
                    else
                    {
                        // insert it in the given position
                        nearbyBoids.InsertRange(indexToInsert, 1);
                        nearbyBoids[indexToInsert] = new BoidInCellPlusDist(boidInfo, dist);
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

    private void UpdateForces(float dt, Bounds bounds, in GridInfo gridInfo, in NativeArray<Cell> cells, in SharedLists<BoidInCellInfo> boidsInCells, 
                              in NativeList<BoidInCellPlusDist> nearbyBoids)
    {
        //Profiler.BeginSample("Update Forces");

        // generate the forces
        UpdateCohesion(nearbyBoids);
        UpdateAlignment(nearbyBoids);
        UpdateSeparation(nearbyBoids);
        UpdateBorderRepulsion(bounds);
        totalForce = cohesionForce + alignmentForce + separationForce + repulsionForce;

        // apply the force to the velocity
        vel += totalForce * dt;

        // make sure the velocity is within the minimum and maximum
        float speed = math.length(vel);
        if (speed < Mathf.Epsilon)
            vel = Dir * minSpeed;
        else if (speed < minSpeed)
            vel *= minSpeed / speed;
        else if (speed > maxSpeed)
            vel *= maxSpeed / speed;

        // update the movement
        pos += vel * dt;
        dir = math.normalize(vel);

        //Profiler.EndSample();
    }


    /// <summary>
    /// Returns a force to try to get boids to 'fly' or 'swim' in a flock/bank
    /// </summary>
    private void UpdateCohesion(in NativeList<BoidInCellPlusDist> nearbyBoidsInfo)
    {
        //Profiler.BeginSample("UpdateCohesion");

        cohesionForce = new float2(0.0f, 0.0f);

        float2 pos = Pos;
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
    }

    /// <summary>
    /// Returns a force to keep boids from colliding with each other
    /// </summary>
    private void UpdateSeparation(in NativeList<BoidInCellPlusDist> nearbyBoidsInfo)
    {
        //Profiler.BeginSample("UpdateSeparation");

        separationForce = new float2(0.0f, 0.0f);

        float2 pos = Pos;
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
                if (distToBoid > Mathf.Epsilon)
                {
                    // calculate the force to apply 
                    dirToBoid *= 1.0f / distToBoid;
                    float forceT = 1.0f - Mathf.Clamp01((distToBoid - radiusForMaxSeparationForce) / (maxSeparationRadius - radiusForMaxSeparationForce));
                    float forceAmount = maxSeparationForce * forceT;
                    float2 force = -dirToBoid * forceAmount;

                    // add it to the total amount
                    separationForce += force;
                }
            }
        }

        //Profiler.EndSample();
    }

    /// <summary>
    /// Returns a force to try to get all the boids looking in the same direction
    /// </summary>
    private void UpdateAlignment(in NativeList<BoidInCellPlusDist> nearbyBoidsInfo)
    {
        //Profiler.BeginSample("UpdateAlignment");

        // average the direction of the nearby boids
        float2 avgDir = new float2(0.0f, 0.0f);
        float2 pos = Pos;
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

        //Profiler.EndSample();
    }

    #endregion
}
