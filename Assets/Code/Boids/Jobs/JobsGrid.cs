using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using System.Runtime.CompilerServices;
using UnityEngine.Profiling;


/// <summary>
/// The grid that can be used in jobs
/// </summary>
public struct JobsGrid
{
    #region Typedefs

    /// <summary>
    /// The cell of the grid that contains the boids
    /// </summary>
    public struct Cell : IDisposable
    {
        public const int DefaultListCapacity = 16;

        public float2 min;
        public float2 max;
        public NativeList<BoidInCellInfo> boids;

        public void Init(float2 min, float2 max)
        {
            this.min = min;
            this.max = max;
            boids = new NativeList<BoidInCellInfo>(DefaultListCapacity, AllocatorManager.Persistent);
        }

        public void Dispose()
        {
            boids.Dispose();
        }
    }

    /// <summary>
    /// Structure holding the information we need from a boid for the grid
    /// </summary>
    public struct BoidInCellInfo
    {
        public int index;
        public float2 pos;
    }

    /// <summary>
    /// This structure holds the information we need from a boid plus the distance, used when calculating the X boids within a radius
    /// </summary>
    public struct BoidInCellPlusDist
    {
        public int index;
        public float2 pos;
        public float distance;

        public BoidInCellPlusDist(BoidInCellInfo boid, float dist)
        {
            index = boid.index;
            pos = boid.pos;
            distance = dist;
        }
    }

    /// <summary>
    /// This structure holds the information regarding cells that we store to sort them and iterate over them in order
    /// </summary>
    public struct CellInRadiusInfo
    {
        public int cellIndex;
        public float distSq;
    }

    /// <summary>
    /// Comparer class for the CellInRadiusInfo struct
    /// </summary>
    public struct CellInRadiusInfoComparer : IComparer<CellInRadiusInfo>
    {
        public int Compare(CellInRadiusInfo a, CellInRadiusInfo b)
        {
            float diffDists = (b.distSq - a.distSq);
            int diff = math.clamp((int)math.round(diffDists * 1000.0f), -1, 1);

            return diff;
        }
    }

    #endregion

    #region Attributes

    private int2 size;
    private float2 boundsMin;
    private float2 boundsMax;
    private float2 boundsSize;

    private NativeArray<Cell> cells;

    #endregion

    #region Properties

    public int2 Size => size;
    public float2 BoundsMin => boundsMin;
    public float2 BoundsMax => boundsMax;
    public float2 BoundsSize => boundsSize;

    #endregion

    #region Initialization

    /// <summary>
    /// Initialization
    /// </summary>
    public void Init(BoidsControllerJobs boidsCtrl, int sizeX, int sizeY)
    {
        // set the parameters
        size = new int2(sizeX, sizeY);

        // get the bounds where the boids exist
        boundsMin = new float2(boidsCtrl.bounds.min.x, boidsCtrl.bounds.min.y);
        boundsMax = new float2(boidsCtrl.bounds.max.x, boidsCtrl.bounds.max.y);
        boundsSize = boundsMax - boundsMin;

        // create the cells
        cells = new NativeArray<Cell>(sizeX * sizeY, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        for (int iy = 0; iy < size.y; ++iy)
        {
            float ty0 = (float)iy / (float)size.y;
            float ty1 = (float)(iy + 1) / (float)size.y;
            float minY = Mathf.Lerp(boundsMin.y, boundsMax.y, ty0);
            float maxY = Mathf.Lerp(boundsMin.y, boundsMax.y, ty1);

            for (int ix = 0; ix < size.x; ++ix)
            {
                float tx0 = (float)ix / (float)size.x;
                float tx1 = (float)(ix + 1) / (float)size.x;
                float minX = Mathf.Lerp(boundsMin.x, boundsMax.x, tx0);
                float maxX = Mathf.Lerp(boundsMin.x, boundsMax.x, tx1);

                // create the cell
                int i = GetIndex(ix, iy);
                Cell c = new Cell();
                c.Init(new float2(minX, minY), new float2(maxX, maxY));
                cells[i] = c;
            }
        }
    }

    /// <summary>
    /// Builds the grid with all the boids in the controller
    /// </summary>
    public void BuildGrid(NativeList<JobsBoid> boids)
    {
        // clear the grid
        Clear();

        // add all the boids
        foreach (JobsBoid boid in boids)
            AddBoid(boid);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Returns the index to use for the given position in the grid
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(int2 pos)
    {
        return GetIndex(pos.x, pos.y);
    }

    /// <summary>
    /// Returns the index to use for the given position in the grid
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(int x, int y)
    {
        return y * size.x + x;
    }

    /// <summary>
    /// Returns the cell position for the given 2D position in the world
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int2 GetCell(float x, float y)
    {
        return GetCell(new float2(x, y));
    }

    /// <summary>
    /// Returns the cell position for the given 2D position in the world
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int2 GetCell(float2 pos)
    {
        float2 posNorm = (pos - boundsMin) / boundsSize;
        int ix = math.clamp(Mathf.FloorToInt(posNorm.x * size.x), 0, size.x - 1);
        int iy = math.clamp(Mathf.FloorToInt(posNorm.y * size.y), 0, size.y - 1);

        return new int2(ix, iy);
    }

    #endregion

    #region Adding, Removing and Finding Boids in Grid

    /// <summary>
    /// Clear the boids from the cells
    /// </summary>
    public void Clear()
    {
        foreach (Cell cell in cells)
            cell.boids.Clear();
    }

    /// <summary>
    /// Adds the given boid to the grid
    /// </summary>
    /// <param name="boid"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddBoid(JobsBoid boid)
    {
        // find the cell for the boid
        int2 cellPos = GetCell(boid.Pos);
        int cellIndex = GetIndex(cellPos);
        Cell cell = cells[cellIndex];

        // add it to the cell
        cell.boids.Add(new BoidInCellInfo
        {
            index = boid.Index,
            pos = boid.Pos,
        });
    }

    /// <summary>
    /// Removes the given boid from the grid
    /// </summary>
    /// <param name="boid"></param>
    public void RemoveBoid(JobsBoid boid)
    {
        // find the cell for the boid
        int2 cellPos = GetCell(boid.Pos);
        int cellIndex = GetIndex(cellPos);
        Cell cell = cells[cellIndex];

        // add it to the cell
        int indexInList = -1;
        for (int i = 0; i < cell.boids.Length; ++i)
        {
            if (boid.Index == cell.boids[i].index)
            {
                indexInList = i;
                break;
            }
        }

        if (indexInList >= 0)
            cell.boids.RemoveAt(indexInList);
    }

    #endregion

    #region Find Boids in Radius Methods

    /// <summary>
    /// Fills (but doesn't clear) the given list of boids with the ones in the radius
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="radius"></param>
    public void FindBoidsInRadius(float2 pos, float radius, JobsBoid boidToIgnore, ref List<BoidInCellInfo> nearbyBoidIndexes)
    {
        Profiler.BeginSample("Grid.FindBoidsInRadius");

        // we'll first need to get the cell positions we have to iterate over
        float minX = pos.x - radius;
        float maxX = pos.x + radius;
        float minY = pos.y - radius;
        float maxY = pos.y + radius;

        int2 minPos = GetCell(minX, minY);
        int2 maxPos = GetCell(maxX, maxY);

        // let's iterate over all the cells
        float radiusSq = radius * radius;
        for (int iy = minPos.y; iy <= maxPos.y; ++iy)
        {
            for (int ix = minPos.x; ix <= maxPos.x; ++ix)
            {
                int index = GetIndex(ix, iy);
                Cell cell = cells[index];

                // first check the cell is within the radius
                if (IsCellInRadius(cell, pos, radius))
                {
                    // now iterate over all the boids in the cell
                    foreach (BoidInCellInfo boidInfo in cell.boids)
                    {
                        if (boidInfo.index == boidToIgnore.Index)
                            continue;

                        // if the boid is within radius add it to the list
                        float2 boidPos = boidInfo.pos;
                        float distSq = math.distancesq(boidPos, pos);
                        if (distSq <= radiusSq)
                            nearbyBoidIndexes.Add(boidInfo);
                    }
                }
            }
        }

        Profiler.EndSample();
    }

    /// <summary>
    /// Fills (but doesn't clear) the given list of boids with the ones in the radius
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="radius"></param>
    public void FindBoidsInRadius(float2 pos, float radius, JobsBoid boidToIgnore, ref NativeList<BoidInCellPlusDist> nearbyBoids)
    {
        Profiler.BeginSample("Grid.FindBoidsInRadius Limits");

        // we'll first need to get the cell positions we have to iterate over
        float minX = pos.x - radius;
        float maxX = pos.x + radius;
        float minY = pos.y - radius;
        float maxY = pos.y + radius;

        int2 minPos = GetCell(minX, minY);
        int2 maxPos = GetCell(maxX, maxY);

        // let's iterate over all the cells
        float radiusSq = radius * radius;
        for (int iy = minPos.y; iy <= maxPos.y; ++iy)
        {
            for (int ix = minPos.x; ix <= maxPos.x; ++ix)
            {
                int index = GetIndex(ix, iy);
                Cell cell = cells[index];

                // first check the cell is within the radius
                if (cell.boids.Length > 0 &&
                    IsCellInRadius(cell, pos, radius))
                {
                    // now iterate over all the boids in the cell
                    foreach (BoidInCellInfo boidInfo in cell.boids)
                    {
                        if (boidInfo.index == boidToIgnore.Index)
                            continue;

                        // if the boid is within radius add it to the list
                        float2 boidPos = boidInfo.pos;
                        float distSq = math.distancesq(boidPos, pos);
                        if (distSq <= radiusSq)
                        {
                            float dist = Mathf.Sqrt(distSq);
                            nearbyBoids.Add(new BoidInCellPlusDist(boidInfo, dist));
                        }
                    }
                }
            }
        }

        Profiler.EndSample();
    }

    /// <summary>
    /// Fills (but doesn't clear) the given list of boids with the ones in the radius
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="radius"></param>
    public void FindNearestBoidsInRadius(float2 pos, float radius, JobsBoid boidToIgnore, int maxBoids, 
                                         ref NativeList<BoidInCellPlusDist> nearbyBoids, ref NativeList<CellInRadiusInfo> cellsInRadius)
    {
        Profiler.BeginSample("Grid.FindBoidsInRadius Limits");

        Profiler.BeginSample("Find and Sort Cells");

        // we'll first need to get the cell positions we have to iterate over
        float minX = pos.x - radius;
        float maxX = pos.x + radius;
        float minY = pos.y - radius;
        float maxY = pos.y + radius;

        int2 minPos = GetCell(minX, minY);
        int2 maxPos = GetCell(maxX, maxY);

        // reset the cells list
        cellsInRadius.Clear();

        // get the list of all cells in the radius
        float radiusSq = radius * radius;
        for (int iy = minPos.y; iy <= maxPos.y; ++iy)
        {
            for (int ix = minPos.x; ix <= maxPos.x; ++ix)
            {
                int cellIndex = GetIndex(ix, iy);
                Cell cell = cells[cellIndex];
                float distSq = GetCellDistanceSq(cell, pos);
                if (distSq <= radiusSq && cell.boids.Length > 0)
                    cellsInRadius.Add(new CellInRadiusInfo
                    {
                        cellIndex = cellIndex,
                        distSq = distSq,
                    });
            }
        }

        // sort them by their distance to the position
        CellInRadiusInfoComparer comparer = new CellInRadiusInfoComparer();
        cellsInRadius.Sort(comparer);

        Profiler.EndSample();

        Profiler.BeginSample("Find Boids in Cells");

        // let's iterate over all the cells in order
        float maxDistSq = 0.0f;
        for (int i = 0; i < cellsInRadius.Length; ++i)
        {
            // if we have already all the boids we need and we're far away let's quit already
            CellInRadiusInfo info = cellsInRadius[i];
            if (nearbyBoids.Length >= maxBoids && info.distSq > maxDistSq)
                break;

            Cell cell = cells[info.cellIndex];
            foreach (BoidInCellInfo boidInfo in cell.boids)
            {
                if (boidInfo.index == boidToIgnore.Index)
                    continue;

                // if the boid is within radius add it to the list
                float2 boidPos = boidInfo.pos;
                float distSq = math.distancesq(boidPos, pos);
                if ((nearbyBoids.Length < maxBoids && distSq <= radiusSq) ||
                    (distSq < maxDistSq))
                {
                    float dist = Mathf.Sqrt(distSq);

                    // find the position to insert it
                    int insertPos = 0;
                    for (int j = 0; j < nearbyBoids.Length; ++j, ++insertPos)
                    {
                        if (nearbyBoids[j].distance > dist)
                            break;
                    }

                    if (insertPos < nearbyBoids.Length)
                    {
                        // insert it
                        nearbyBoids.InsertRange(insertPos, 1);
                        nearbyBoids[insertPos] = new BoidInCellPlusDist(boidInfo, dist);
                    }
                    else
                    {
                        // insert it at the end and update the maximum distance
                        nearbyBoids.Add(new BoidInCellPlusDist(boidInfo, dist));
                        maxDistSq = distSq;
                    }
                }
            }

            if (nearbyBoids.Length > maxBoids)
            {
                // remove the unnecessary boids
                int numBoidsToRemove = nearbyBoids.Length - maxBoids;
                nearbyBoids.RemoveRange(nearbyBoids.Length - numBoidsToRemove, numBoidsToRemove);

                // update the max distance
                float maxDist = nearbyBoids[nearbyBoids.Length - 1].distance;
                maxDistSq = maxDist * maxDist;
            }
        }

        Profiler.EndSample();

        Profiler.EndSample();
    }

    /// <summary>
    /// Returns true if the cell (or any part of it) is within a radius of the given pos
    /// </summary>
    public bool IsCellInRadius(Cell cell, float2 pos, float radius)
    {
        float distSq = GetCellDistanceSq(cell, pos);
        return (distSq <= radius * radius);
    }

    /// <summary>
    /// Returns the minimum distance squared between the given cell and the given position
    /// </summary>
    public float GetCellDistanceSq(Cell cell, float2 pos)
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
