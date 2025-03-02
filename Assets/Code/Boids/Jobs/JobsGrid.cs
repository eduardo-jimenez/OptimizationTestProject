using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using System.Runtime.CompilerServices;
using UnityEngine.Profiling;
using UnityEditor;


/// <summary>
/// The grid that can be used in jobs
/// </summary>
public struct JobsGrid
{
    #region Typedefs

    /// <summary>
    /// The cell of the grid that contains the boids
    /// </summary>
    public struct Cell
    {
        public const int DefaultListCapacity = 16;

        private int index;
        private float2 min;
        private float2 max;

        public int Index => index;
        public float2 Min => min;
        public float2 Max => max;

        public void Init(int index, float2 min, float2 max)
        {
            this.index = index;
            this.min = min;
            this.max = max;
        }
    }

    /// <summary>
    /// Structure holding the information we need from a boid for the grid
    /// </summary>
    public struct BoidInCellInfo
    {
        public int index;
        public float2 pos;
        public float2 dir;
    }

    /// <summary>
    /// This structure holds the information we need from a boid plus the distance, used when calculating the X boids within a radius
    /// </summary>
    public struct BoidInCellPlusDist
    {
        public int index;
        public float2 pos;
        public float2 dir;
        public float distance;

        public BoidInCellPlusDist(BoidInCellInfo boid, float dist)
        {
            index = boid.index;
            pos = boid.pos;
            dir = boid.dir;
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

    /// <summary>
    /// This structure holds information of the grid (num divisions, bounds, etc)
    /// </summary>
    public struct GridInfo
    {
        public int2 size;
        public float2 boundsMin;
        public float2 boundsMax;
        public float2 boundsSize;
    }

    #endregion

    #region Attributes

    public const int MaxBoidsInCell = 128;

    private int2 size;
    private float2 boundsMin;
    private float2 boundsMax;
    private float2 boundsSize;

    private NativeArray<Cell> cells;
    private SharedLists<BoidInCellInfo> boidsInCells;
    //private NativeParallelMultiHashMap<int, BoidInCellInfo> boidsInCells;

    #endregion

    #region Properties

    public int2 Size => size;
    public float2 BoundsMin => boundsMin;
    public float2 BoundsMax => boundsMax;
    public float2 BoundsSize => boundsSize;

    public GridInfo Info => new GridInfo
    {
        size = size,
        boundsMin = boundsMin,
        boundsMax = boundsMax,
        boundsSize = boundsSize,
    };

    public NativeArray<Cell> Cells => cells;
    public SharedLists<BoidInCellInfo> BoidsInCells => boidsInCells;

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
        int numCells = sizeX * sizeY;
        cells = new NativeArray<Cell>(numCells, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        boidsInCells = new SharedLists<BoidInCellInfo>();
        boidsInCells.Init(MaxBoidsInCell, numCells);
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
                c.Init(i, new float2(minX, minY), new float2(maxX, maxY));
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
        for (int i = 0; i < boidsInCells.NumLists; ++i)
            boidsInCells.Clear(i);
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

        // add it to the cell
        boidsInCells.Add(cellIndex, new BoidInCellInfo
        {
            index = boid.Index,
            pos = boid.Pos,
            dir = boid.Dir,
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

        // add it to the cell
        int indexInList = -1;
        for (int i = 0; i < boidsInCells.GetLength(cellIndex); ++i)
        {
            if (boid.Index == boidsInCells[cellIndex, i].index)
            {
                indexInList = i;
                break;
            }
        }

        if (indexInList >= 0)
            boidsInCells.RemoveAt(cellIndex, indexInList);
    }

    #endregion
}
