using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Profiling;


/// <summary>
/// This class represents a grid where boids are going to be distributed
/// </summary>
public class Grid
{
    #region Typedefs

	/// <summary>
	/// The cell of the grid that contains the boids
	/// </summary>
	public class Cell
	{
		public Vector2 min;
		public Vector2 max;
		public List<BaseBoid> boids = new List<BaseBoid>();

		public Cell()
		{
		}

		public void Init(Vector2 min, Vector2 max)
		{
			this.min = min;
			this.max = max;
			boids.Clear();
		}
	}

    #endregion

    #region Attributes

    private BoidsController boidsCtrl;
	private Vector2Int size;
	private Vector2 boundsMin;
	private Vector2 boundsMax;
	private Vector2 boundsSize;

	private Cell[] cells;

    private List<(Cell, float)>[] cellsInRadiusArray;

    #endregion

    #region Properties

    public BoidsController BoidsCtrl => boidsCtrl;
	public Vector2Int Size => size;
	public Vector2 BoundsMin => boundsMin;
	public Vector2 BoundsMax => boundsMax;
	public Vector2 BoundsSize => boundsSize;

    #endregion

    #region Initialization

    /// <summary>
    /// Default constructor
    /// </summary>
    public Grid()
	{
	}

	/// <summary>
	/// Initialization
	/// </summary>
	public void Init(BoidsController boidsCtrl, Vector2Int size)
	{
		// set the parameters
		this.boidsCtrl = boidsCtrl;
		this.size = size;

        // create the array of the lists in cells
        int cellsInRadiusArraySize;
        BoidsControllerBasicMultithread multithreadBoidsCtrl = boidsCtrl as BoidsControllerBasicMultithread;
        cellsInRadiusArraySize = (multithreadBoidsCtrl != null) ? multithreadBoidsCtrl.numThreads : 1;
        cellsInRadiusArray = new List<(Cell, float)>[cellsInRadiusArraySize];
        for (int i = 0; i < cellsInRadiusArraySize; ++i)
            cellsInRadiusArray[i] = new List<(Cell, float)>();

        // get the bounds where the boids exist
        boundsMin = boidsCtrl.bounds.min;
		boundsMax = boidsCtrl.bounds.max;
        boundsSize = boundsMax - boundsMin;

        // create the cells
        cells = new Cell[size.x * size.y];
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
				cells[i] = new Cell();
				cells[i].Init(new Vector2(minX, minY), new Vector2(maxX, maxY));
            }
        }
	}

	/// <summary>
	/// Builds the grid with all the boids in the controller
	/// </summary>
	public void BuildGrid()
	{
		// clear the grid
		Clear();

		// get the list of boids and distribute them
		List<BaseBoid> boids = boidsCtrl.Boids;
		foreach (BaseBoid boid in boids)
			AddBoid(boid);
	}

    #endregion

    #region Methods

    /// <summary>
    /// Returns the index to use for the given position in the grid
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(Vector2Int pos)
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
    public Vector2Int GetCell(float x, float y)
	{
		return GetCell(new Vector2(x, y));
    }

    /// <summary>
    /// Returns the cell position for the given 2D position in the world
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2Int GetCell(Vector2 pos)
    {
        Vector2 posNorm = (pos - boundsMin) / boundsSize;
        int ix = Mathf.Clamp(Mathf.FloorToInt(posNorm.x * size.x), 0, size.x - 1);
        int iy = Mathf.Clamp(Mathf.FloorToInt(posNorm.y * size.y), 0, size.y - 1);

        return new Vector2Int(ix, iy);
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
    public void AddBoid(BaseBoid boid)
	{
        // find the cell for the boid
        Vector2Int cellPos = GetCell(boid.Pos);
        int cellIndex = GetIndex(cellPos);
        Cell cell = cells[cellIndex];

        // add it to the cell
        cell.boids.Add(boid);
    }

    /// <summary>
    /// Removes the given boid from the grid
    /// </summary>
    /// <param name="boid"></param>
    public void RemoveBoid(BaseBoid boid)
    {
        // find the cell for the boid
        Vector2Int cellPos = GetCell(boid.Pos);
        int cellIndex = GetIndex(cellPos);
        Cell cell = cells[cellIndex];

        // add it to the cell
        cell.boids.Remove(boid);
    }

    /// <summary>
    /// Returns true if the cell (or any part of it) is within a radius of the given pos
    /// </summary>
    public bool IsCellInRadius(Cell cell, Vector2 pos, float radius)
    {
        float distSq = GetCellDistanceSq(cell, pos);
        return (distSq <= radius * radius);
    }

    /// <summary>
    /// Returns the minimum distance squared between the given cell and the given position
    /// </summary>
    public float GetCellDistanceSq(Cell cell, Vector2 pos)
    {
        Vector2 nearPos;

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

        float distSq = (pos - nearPos).sqrMagnitude;

        return distSq;

    }

    /// <summary>
    /// Fills (but doesn't clear) the given list of boids with the ones in the radius
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="radius"></param>
    public void FindBoidsInRadius(Vector2 pos, float radius, BaseBoid boidToIgnore, ref List<BaseBoid> nearbyBoids)
	{
        Profiler.BeginSample("Grid.FindBoidsInRadius");

		// we'll first need to get the cell positions we have to iterate over
		float minX = pos.x - radius;
		float maxX = pos.x + radius;
		float minY = pos.y - radius;
		float maxY = pos.y + radius;

		Vector2Int minPos = GetCell(minX, minY);
		Vector2Int maxPos = GetCell(maxX, maxY);

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
					foreach (BaseBoid boid in cell.boids)
					{
						if (boid == boidToIgnore)
							continue;

						// if the boid is within radius add it to the list
						Vector2 boidPos = boid.Pos;
						float distSq = (boidPos - pos).sqrMagnitude;
						if (distSq <= radiusSq)
							nearbyBoids.Add(boid);
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
    public void FindBoidsInRadius(Vector2 pos, float radius, BaseBoid boidToIgnore, ref List<(BaseBoid, float)> nearbyBoids)
    {
        Profiler.BeginSample("Grid.FindBoidsInRadius Limits");

        // we'll first need to get the cell positions we have to iterate over
        float minX = pos.x - radius;
        float maxX = pos.x + radius;
        float minY = pos.y - radius;
        float maxY = pos.y + radius;

        Vector2Int minPos = GetCell(minX, minY);
        Vector2Int maxPos = GetCell(maxX, maxY);

        // let's iterate over all the cells
        float radiusSq = radius * radius;
        for (int iy = minPos.y; iy <= maxPos.y; ++iy)
        {
            for (int ix = minPos.x; ix <= maxPos.x; ++ix)
            {
                int index = GetIndex(ix, iy);
                Cell cell = cells[index];

                // first check the cell is within the radius
                if (cell.boids.Count > 0 &&
                    IsCellInRadius(cell, pos, radius))
                {
                    // now iterate over all the boids in the cell
                    foreach (BaseBoid boid in cell.boids)
                    {
                        if (boid == boidToIgnore)
                            continue;

                        // if the boid is within radius add it to the list
                        Vector2 boidPos = boid.Pos;
                        float distSq = (boidPos - pos).sqrMagnitude;
                        if (distSq <= radiusSq)
                        {
                            float dist = Mathf.Sqrt(distSq);
                            nearbyBoids.Add((boid, dist));
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
    public void FindNearestBoidsInRadius(Vector2 pos, float radius, BaseBoid boidToIgnore, int maxBoids, ref List<(BaseBoid, float)> nearbyBoids, int threadIndex = 0)
    {
        Profiler.BeginSample("Grid.FindBoidsInRadius Limits");

        Profiler.BeginSample("Find and Sort Cells");

        // we'll first need to get the cell positions we have to iterate over
        float minX = pos.x - radius;
        float maxX = pos.x + radius;
        float minY = pos.y - radius;
        float maxY = pos.y + radius;

        Vector2Int minPos = GetCell(minX, minY);
        Vector2Int maxPos = GetCell(maxX, maxY);

        // reset the cells list
        List<(Cell, float)> cellsInRadius = cellsInRadiusArray[threadIndex];
        cellsInRadius.Clear();

        // get the list of all cells in the radius
        float radiusSq = radius * radius;
        for (int iy = minPos.y; iy <= maxPos.y; ++iy)
        {
            for (int ix = minPos.x; ix <= maxPos.x; ++ix)
            {
                Cell cell = cells[GetIndex(ix, iy)];
                float distSq = GetCellDistanceSq(cell, pos);
                if (distSq <= radiusSq && cell.boids.Count > 0)
                    cellsInRadius.Add((cell, distSq));
            }
        }

        // sort them by their distance to the position
        cellsInRadius.Sort(delegate ((Cell cell, float distSq) a, (Cell cell, float distSq) b)
        {
            int diff = Mathf.RoundToInt(1000.0f * (a.distSq - b.distSq));

            return diff;
        });

        Profiler.EndSample();

        Profiler.BeginSample("Find Boids in Cells");

        // let's iterate over all the cells in order
        float maxDistSq = 0.0f;
        for (int i = 0; i < cellsInRadius.Count; ++i)
        {
            // if we have already all the boids we need and we're far away let's quit already
            (Cell cell, float distSq) info = cellsInRadius[i];
            if (nearbyBoids.Count >= maxBoids && info.distSq > maxDistSq)
                break;

            foreach (BaseBoid boid in info.cell.boids)
            {
                if (boid == boidToIgnore)
                    continue;

                // if the boid is within radius add it to the list
                Vector2 boidPos = boid.Pos;
                float distSq = (boidPos - pos).sqrMagnitude;
                if ((nearbyBoids.Count < maxBoids && distSq <= radiusSq) ||
                    (distSq < maxDistSq))
                {
                    float dist = Mathf.Sqrt(distSq);

                    // find the position to insert it
                    int insertPos = 0;
                    for (int j = 0; j < nearbyBoids.Count; ++j, ++insertPos)
                    {
                        if (nearbyBoids[j].Item2 > dist)
                            break;
                    }

                    if (insertPos < nearbyBoids.Count)
                    {
                        // insert it
                        nearbyBoids.Insert(insertPos, (boid, dist));
                    }
                    else
                    {
                        // insert it at the end and update the maximum distance
                        nearbyBoids.Add((boid, dist));
                        maxDistSq = distSq;
                    }
                }
            }

            if (nearbyBoids.Count > maxBoids)
            {
                // remove the unnecessary boids
                int numBoidsToRemove = nearbyBoids.Count - maxBoids;
                nearbyBoids.RemoveRange(nearbyBoids.Count - numBoidsToRemove, numBoidsToRemove);

                // update the max distance
                float maxDist = nearbyBoids[nearbyBoids.Count - 1].Item2;
                maxDistSq = maxDist * maxDist;
            }
        }

        Profiler.EndSample();

        Profiler.EndSample();
    }

    #endregion
}
