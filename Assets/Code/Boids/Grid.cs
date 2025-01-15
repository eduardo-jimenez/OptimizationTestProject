using System;
using System.Collections.Generic;
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
    public int GetIndex(Vector2Int pos)
	{
		return GetIndex(pos.x, pos.y);
	}

	/// <summary>
	/// Returns the index to use for the given position in the grid
	/// </summary>
	public int GetIndex(int x, int y)
	{
		return y * size.x + x;
	}

	/// <summary>
	/// Returns the cell position for the given 2D position in the world
	/// </summary>
	public Vector2Int GetCell(float x, float y)
	{
		return GetCell(new Vector2(x, y));
    }

    /// <summary>
    /// Returns the cell position for the given 2D position in the world
    /// </summary>
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

        return (distSq <= radius * radius);
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

    #endregion
}
