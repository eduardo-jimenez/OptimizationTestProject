using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Assertions;
using static UnityEditor.PlayerSettings;
using static UnityEditor.Progress;


public class FluidGrid : MonoBehaviour
{
    #region Typedefs

    public enum RenderMethods
    {
        Densities,
        Velocities,
    }

    #endregion

    #region Public Attributes

    [Header("Grid Parameters")]
    public Bounds bounds = new Bounds(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(160.0f / 9.0f, 10.0f, 0.0f));
	public int numDivsX = 320;
	public int numDivsY = 180;

    [Header("Fluid Params")]
    public float viscosity = 0.05f;
    public float diffusion = 1.0f;
    public int numIters = 10;
    public float maxSpeed = 10.0f;

    [Header("Interactivity")]
    public float inputIncDensity = 1.0f;
    public float mouseDeltaFactor = 0.1f;

    [Header("Rendering")]
    public MeshRenderer gridRenderer;
    public RenderMethods renderMethod = RenderMethods.Densities;
    public float velForMaxCol = 0.5f;
    public float hueVel = 45.0f;

    #endregion

    #region Private Attributes

    private GridCell[] cells;
    private int maxNumDivs = 80;

    private Texture2D image;
    private Color[] pixels;

    private float hue = 0.0f;

    private float[] p;
    private float[] div;

	#endregion
	
	#region Properties
	#endregion
	
	#region MonoBehaviour Methods

	void Start()
	{
		Init();
	}

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;

        // check for external inputs (from the mouse)
        UpdateInputs(dt);

        // copy the velocities and densities
        foreach (GridCell cell in cells)
        {
            cell.PrevDensity = cell.Density;
            cell.PrevVel = cell.Vel;
        }

        // do the simulation
        VelocityStep(dt);

        DiffuseDensity(dt);
        AdvectDensities(dt);
    }

    private void Update()
    {
        // update the image
        UpdateImage();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minY = bounds.min.y;
        float maxY = bounds.max.y;

        for (int iy = 0; iy <= numDivsY; ++iy)
        {
            float ty = (float)iy / (float)numDivsY;
            float y = Mathf.Lerp(bounds.min.y, bounds.max.y, ty);
            Gizmos.DrawLine(new Vector3(minX, y, 0.0f), new Vector3(maxX, y, 0.0f));
        }

        for (int ix = 0; ix <= numDivsX; ++ix)
        {
            float tx = (float)ix / (float)numDivsX;
            float x = Mathf.Lerp(bounds.min.x, bounds.max.x, tx);
            Gizmos.DrawLine(new Vector3(x, minY, 0.0f), new Vector3(x, maxY, 0.0f));
        }

        if (cells != null)
        {
            foreach (GridCell cell in cells)
                cell.DrawGizmos();
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initialization
    /// </summary>
    public void Init()
	{
        // create the grid cells
        maxNumDivs = Mathf.Max(numDivsX, numDivsY);
        int numCells = numDivsX * numDivsY;
		cells = new GridCell[numCells];
        p = new float[numCells];
        div = new float[numCells];
		for (int iy = 0; iy < numDivsY; ++iy)
		{
			int index = iy * numDivsX;
			for (int ix = 0; ix < numDivsX; ++ix, ++index)
			{
				cells[index] = new GridCell();
				cells[index].Init(this, new Vector2Int(ix, iy));
			}
		}

        // create the texture we'll use to represent the fluid
        image = new Texture2D(numDivsX, numDivsY, TextureFormat.RGB24, true);
        pixels = new Color[cells.Length];
        for (int i = 0; i < pixels.Length; ++i)
            pixels[i] = new Color32(16, 16, 16, 255);
        image.SetPixels(pixels);
        image.Apply();

        // set it to the renderer
        gridRenderer.material.mainTexture = image;
    }

    #endregion

    #region Cell Access

    /// <summary>
    /// Returns the index of the cell corresponding the given position
    /// </summary>
    public int GetIndex(Vector2Int pos)
	{
		return GetIndex(pos.x, pos.y);
	}

    /// <summary>
    /// Returns the index of the cell corresponding the given position
    /// </summary>
    public int GetIndex(int x, int y)
	{
        int index = y * numDivsX + x;
        return index;
    }

    /// <summary>
    /// Returns the position of the cell with the given index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public Vector2Int GetPosition(int index)
    {
        int x = index % numDivsX;
        int y = index / numDivsX;
        return new Vector2Int(x, y);
    }

    /// <summary>
    /// Returns the 3D position for the cell at the given position
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public Vector3 Get3DPos(Vector2Int pos)
    {
        float tx = (pos.x + 0.5f) / (float)numDivsX;
        float ty = (pos.y + 0.5f) / (float)numDivsY;

        float x = Mathf.Lerp(bounds.min.x, bounds.max.x, tx);
        float y = Mathf.Lerp(bounds.min.y, bounds.max.y, ty);

        Vector3 p = new Vector3(x, y, bounds.center.z);

        return p;
    }

    /// <summary>
    /// Returns the cell at the given position or null if the position is not valid
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public GridCell GetCellSafe(Vector2Int pos)
	{
		Assert.IsTrue(pos.x >= 0 && pos.x < numDivsX &&
			          pos.y >= 0 && pos.y < numDivsY, $"Invalid position: {pos}");
		if (pos.x < 0 || pos.x >= numDivsX ||
			pos.y < 0 || pos.y >= numDivsY)
			return null;

		int index = GetIndex(pos);

		return cells[index];
	}

    /// <summary>
    /// Returns the cell at the given position or null if the position is not valid
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public GridCell GetCellSafe(int x, int y)
    {
        Assert.IsTrue(x >= 0 && x < numDivsX &&
                      y >= 0 && y < numDivsY, $"Invalid position: ({x}, {y})");
        if (x < 0 || x >= numDivsX ||
            y < 0 || y >= numDivsY)
            return null;

        int index = GetIndex(x, y);

        return cells[index];
    }

    /// <summary>
    /// Returns the cell at the given position or null if the position is not valid
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public GridCell GetCell(Vector2Int pos)
    {
        int index = GetIndex(pos);
        return cells[index];
    }

    /// <summary>
    /// Returns the cell at the given position or null if the position is not valid
    /// </summary>
    /// <param name="pos"></param>
    /// <returns></returns>
    public GridCell GetCell(int x, int y)
    {
        int index = GetIndex(x, y);
        return cells[index];
    }

    /// <summary>
    /// Returns the cell at the given index
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public GridCell GetCell(int index)
	{
		return cells[index];
	}

    #endregion

    #region External Inputs

    /// <summary>
    /// Updates the state of the cells based on external inputs such as the mouse
    /// </summary>
    /// <param name="dt"></param>
    public void UpdateInputs(float dt)
    {
        hue += hueVel * dt;
        if (hue > 360.0f)
            hue -= 360.0f;

        bool leftButton = Input.GetMouseButton(0);
        bool rightButton = Input.GetMouseButton(1);
        if (leftButton || rightButton)
        {
            // calculate the cell under the mouse
            Vector2 pos = Input.mousePosition;
            float tx = pos.x / Screen.width;
            float ty = pos.y / Screen.height;
            int cellX = Mathf.Clamp(Mathf.RoundToInt(tx * numDivsX), 0, numDivsX - 1);
            int cellY = Mathf.Clamp(Mathf.RoundToInt(ty * numDivsY), 0, numDivsY - 1);

            // add some velocity
            GridCell cell = GetCell(cellX, cellY);
            Vector2 mouseDelta = Input.mousePositionDelta * mouseDeltaFactor;
            cell.Vel += mouseDelta;

            if (leftButton)
            {
                // get the color to add
                float hp = hue / 60.0f;
                float c = 1.0f;
                float x = c * (1.0f - Mathf.Abs((hp % 2.0f) - 1.0f));
                Color col;
                if (hp < 1.0f)
                    col = new Color(c, x, 0.0f);
                else if (hp < 2.0f)
                    col = new Color(x, c, 0.0f);
                else if (hp < 3.0f)
                    col = new Color(0.0f, c, x);
                else if (hp < 4.0f)
                    col = new Color(0.0f, x, c);
                else if (hp < 5.0f)
                    col = new Color(x, 0.0f, c);
                else
                    col = new Color(c, 0.0f, x);

                // add a colour to the cell
                cell.Density = cell.Density + col;
            }
        }
    }

    /// <summary>
    /// Enforces the border limits to velocities so that they are never going out of bounds
    /// </summary>
    public void EnforceBorderLimits()
    {
        int lastRowI = (numDivsY - 1) * numDivsX;
        for (int i = 0; i < numDivsX; ++i, ++lastRowI)
        {
            Vector2 vel = cells[i].Vel;
            vel.y = Mathf.Max(0.0f, vel.y);
            cells[i].Vel = vel;

            vel = cells[lastRowI].Vel;
            vel.y = Mathf.Min(0.0f, vel.y);
            cells[lastRowI].Vel = vel;
        }

        for (int i = 0; i < numDivsY; ++i)
        {
            int i0 = numDivsY * i;
            int i1 = i0 + numDivsX - 1;

            Vector2 vel = cells[i0].Vel;
            vel.x = Mathf.Max(0.0f, vel.x);
            cells[i0].Vel = vel;

            vel = cells[i1].Vel;
            vel.x = Mathf.Min(0.0f, vel.x);
            cells[i1].Vel = vel;
        }
    }

    #endregion

    #region Image Methods

    /// <summary>
    /// Updates the image based on the density of the cells
    /// </summary>
    public void UpdateImage()
    {
        for (int iy = 0; iy < image.height; ++iy)
        {
            for (int ix = 0; ix < image.width; ++ix)
            {
                int i = iy * image.width + ix;
                GridCell cell = cells[i];
                Color c = GetCellColor(cell);
                pixels[i] = c;
            }
        }

        image.SetPixels(pixels);
        image.Apply();
    }

    /// <summary>
    /// Returns the color to use to represent the given cell with the current render method
    /// </summary>
    /// <param name="cell"></param>
    /// <returns></returns>
    private Color GetCellColor(GridCell cell)
    {
        Color c;
        switch (renderMethod)
        {
            case RenderMethods.Densities:
                c = cell.Density;
                break;
            case RenderMethods.Velocities:
                Vector2 vel = cell.Vel;
                float tx = Mathf.Clamp(vel.x / velForMaxCol, -1.0f, 1.0f);
                float ty = Mathf.Clamp(vel.y / velForMaxCol, -1.0f, 1.0f);
                float r = 0.5f * (tx + 1.0f);
                float g = 0.5f * (ty + 1.0f);
                c = new Color(r, g, 0.0f);
                break;
            default:
                c = new Color(0.0f, 0.0f, 0.0f);
                Debug.LogErrorFormat("Unknown render method: {0}", renderMethod);
                break;
        }

        return c;
    }

    #endregion

    #region Old Simulation

    ///// <summary>
    ///// Updates the densities of all the cells. Uses the method described here:
    ///// https://www.youtube.com/watch?v=qsYE1wMEMPA
    ///// This step is called diffusion
    ///// </summary>
    ///// <param name="dt"></param>
    //private void UpdateDensity(float dt)
    //{
    //    // info in:
    //    // https://www.youtube.com/watch?v=qsYE1wMEMPA
    //    // we have to calculate the density as:
    //    // d_n(x, y) = (d_c(x, y) + k * s_n(x, y)) / (1 + k)
    //    // where
    //    // k is the changing constant, calculated as the viscosity times the delta time and
    //    // d_n(x, y) is the density in the next iteration of the cell at (x, y)
    //    // d_c(x, y) is the density in the current iteration of the cell at (x, y)
    //    // s_n(x, y) is the average of the density of the surrounding cells of (x, y) in the next iteration
    //    // 
    //    // s_n = (d_n(x + 1, y) + d_n(x - 1, y) + d_n(x, y + 1) + d_n(x, y - 1)) / 4
    //    // 
    //    // we'll use an iterative approximate method to calculate the solution of the resulting set of equations,
    //    // the Gauss-Seidel
    //
    //    float k = dt * viscosity;
    //    for (int iter = 0; iter < numIters; ++iter)
    //    {
    //        // update the previous densities of all cells
    //        foreach (GridCell cell in cells)
    //            cell.PrevDensity = cell.Density;
    //
    //        foreach (GridCell cell in cells)
    //            cell.UpdateDensityGaussSeidel(k);
    //    }
    //}
    //
    ///// <summary>
    ///// Calculates the velocities of the cells in the grid
    ///// </summary>
    ///// <param name="dt"></param>
    //private void UpdateVelocities(float dt)
    //{
    //    foreach (GridCell cell in cells)
    //        cell.CalculateVelocity(dt);
    //}
    //
    ///// <summary>
    ///// Removes the divergence in the fluid velocities
    ///// </summary>
    //private void RemoveDivergence()
    //{
    //    foreach (GridCell cell in cells)
    //        cell.PrevDivergence = cell.Vel;
    //
    //    for (int i = 0; i < numIters; ++i)
    //    {
    //        foreach (GridCell cell in cells)
    //            cell.ReduceDivergence();
    //
    //        foreach (GridCell cell in cells)
    //            cell.PrevDivergence = cell.Divergence;
    //    }
    //
    //    foreach (GridCell cell in cells)
    //        cell.Vel -= cell.Divergence;
    //}

    #endregion

    #region Velocity Step

    private void VelocityStep(float dt)
    {
        //foreach (GridCell cell in cells)
        //    cell.Vel = dt * cell.PrevVel;

        DiffuseVelocities(dt);
        Project(false);

        AdvectVelocities(dt);
        Project(true);
    }

    #endregion

    #region Diffuse Velocities

    /// <summary>
    /// Diffuses the velocities of the cells with their neighbouring cells
    /// </summary>
    /// <param name="dt"></param>
    private void DiffuseVelocities(float dt)
    {
        // NOTE: [Barkley] I'm copying the code from https://mikeash.com/pyblog/blog/images/fluid.c.file
        // Anyway I believe the a constant is equivalent to the k in https://www.youtube.com/watch?v=qsYE1wMEMPA
        // but the simulation in the code we're copying from has 6 neighbours (in 3 dimensions) in our simulation
        // we have 2 dimensions so we have only 4 neighbours
        float a = dt * viscosity * (maxNumDivs - 2) * (maxNumDivs - 2);
        float a2 = 1.0f / (1.0f + 4.0f * a);

        for (int iter = 0; iter < numIters; ++iter)
        {
            for (int iy = 1; iy < numDivsY - 1; ++iy)
            {
                for (int ix = 1; ix < numDivsX - 1; ++ix)
                {
                    GridCell c = GetCell(ix, iy);
                    GridCell north = GetCell(ix, iy - 1);
                    GridCell south = GetCell(ix, iy + 1);
                    GridCell west = GetCell(ix - 1, iy);
                    GridCell east = GetCell(ix + 1, iy);
                    c.Vel = (c.PrevVel + a * (north.Vel + south.Vel + west.Vel + east.Vel)) * a2;
                }
            }

            SetBoundCellsVelocities(true);
        }
    }

    /// <summary>
    /// Sets the value of density of the boundary cells based on their neighbours
    /// </summary>
    private void SetBoundCellsVelocities(bool currVel)
    {
        // set the density in the left and right bounds first
        for (int iy = 1; iy < numDivsY - 1; ++iy)
        {
            GridCell c0 = GetCell(0, iy);
            GridCell c1 = GetCell(numDivsX - 1, iy);
            Vector2 v0 = GetCell(1, iy).Vel;
            Vector2 v1 = GetCell(numDivsX - 2, iy).Vel;
            c0.SetVel(currVel, new Vector2(-v0.x, v0.y));
            c1.SetVel(currVel, new Vector2(-v1.x, v0.y));
        }
        // then in the upper and lower bounds
        for (int ix = 1; ix < numDivsX - 1; ++ix)
        {
            GridCell c0 = GetCell(ix, 0);
            GridCell c1 = GetCell(ix, numDivsY - 1);
            Vector2 v0 = GetCell(ix, 1).Vel;
            Vector2 v1 = GetCell(ix, numDivsY - 2).Vel;
            c0.SetVel(currVel, new Vector2(v0.x, -v0.y));
            c1.SetVel(currVel, new Vector2(v1.x, -v0.y));
        }

        // finally let's calculate the 4 corners as the average of their 2 neighbours
        GetCell(0, 0).SetVel(currVel, 0.5f * (GetCell(1, 0).GetVel(currVel) + GetCell(0, 1).GetVel(currVel)));
        GetCell(numDivsX - 1, 0).SetVel(currVel, 0.5f * (GetCell(numDivsX - 2, 0).GetVel(currVel) + GetCell(numDivsX - 1, 1).GetVel(currVel)));
        GetCell(0, numDivsY - 1).SetVel(currVel, 0.5f * (GetCell(1, numDivsY - 1).GetVel(currVel) + GetCell(0, numDivsY - 2).GetVel(currVel)));
        GetCell(numDivsX - 1, numDivsY - 1).SetVel(currVel, 0.5f * (GetCell(numDivsX - 2, numDivsY - 1).GetVel(currVel) + GetCell(numDivsX - 1, numDivsY - 2).GetVel(currVel)));
    }

    #endregion

    #region Diffuse Densities

    /// <summary>
    /// Diffuses the density of the cells with their neighbours
    /// </summary>
    /// <param name="dt"></param>
    private void DiffuseDensity(float dt)
    {
        float a = dt * diffusion * (maxNumDivs - 2) * (maxNumDivs - 2);
        float a2 = 1.0f / (1.0f + 4.0f * a);

        for (int k = 0; k < numIters; k++)
        {
            for (int iy = 1; iy < numDivsY - 1; iy++)
            {
                for (int ix = 1; ix < numDivsX - 1; ix++)
                {
                    GridCell c = GetCell(ix, iy);
                    c.Density = (c.PrevDensity + a *
                                  (GetCell(ix + 1, iy).Density +
                                   GetCell(ix - 1, iy).Density +
                                   GetCell(ix, iy - 1).Density +
                                   GetCell(ix, iy + 1).Density)) * a2;
                }
            }

            SetBoundCellsDensity();
        }
    }

    /// <summary>
    /// Sets the value of density of the boundary cells based on their neighbours
    /// </summary>
    private void SetBoundCellsDensity()
    {
        // set the density in the left and right bounds first
        for (int iy = 1; iy < numDivsY - 1; ++iy)
        {
            int i0 = GetIndex(0, iy);
            int i1 = GetIndex(numDivsX - 1, iy);
            GetCell(i0).Density = GetCell(i0 + 1).Density;
            GetCell(i1).Density = GetCell(i1 - 1).Density;
        }
        // then in the upper and lower bounds
        for (int ix = 1; ix < numDivsX - 1; ++ix)
        {
            int i0 = GetIndex(ix, 0);
            int i1 = GetIndex(ix, numDivsY - 1);
            GetCell(i0).Density = GetCell(i0 + numDivsX).Density;
            GetCell(i1).Density = GetCell(i1 - numDivsX).Density;
        }

        // finally let's calculate the 4 corners as the average of their 2 neighbours
        GetCell(0, 0).Density = 0.5f * (GetCell(1, 0).Density + GetCell(0, 1).Density);
        GetCell(numDivsX - 1, 0).Density = 0.5f * (GetCell(numDivsX - 2, 0).Density + GetCell(numDivsX - 1, 1).Density);
        GetCell(0, numDivsY - 1).Density = 0.5f * (GetCell(1, numDivsY - 1).Density + GetCell(0, numDivsY - 2).Density);
        GetCell(numDivsX - 1, numDivsY - 1).Density = 0.5f * (GetCell(numDivsX - 2, numDivsY - 1).Density + GetCell(numDivsX - 1, numDivsY - 2).Density);
    }

    #endregion

    #region Project

    private void Project(bool currVel)
    {
        float invSize = 1.0f / maxNumDivs;
        float k = 0.5f * invSize;
        for (int iy = 1; iy < numDivsY - 1; ++iy)
        {
            for (int ix = 1; ix < numDivsX - 1; ++ix)
            {
                int i = GetIndex(ix, iy);
                p[i] = 0.0f;
                div[i] = -k * ((GetCell(ix + 1, iy).GetVel(currVel).x - GetCell(ix - 1, iy).GetVel(currVel).x) +
                              (GetCell(ix, iy + 1).GetVel(currVel).y - GetCell(ix, iy - 1).GetVel(currVel).y));
            }
        }
        SetBounds(div);
        SetBounds(p);

        LinearSolve(p, div, 1.0f, 4.0f);

        for (int iy = 1; iy < numDivsY - 1; ++iy)
        {
            for (int ix = 1; ix < numDivsX - 1; ++ix)
            {
                GridCell c = GetCell(ix, iy);
                Vector2 vel = c.GetVel(currVel);
                vel.x -= 0.5f * (p[GetIndex(ix + 1, iy)] - p[GetIndex(ix - 1, iy)]) * maxNumDivs;
                vel.y -= 0.5f * (p[GetIndex(ix, iy + 1)] - p[GetIndex(ix, iy - 1)]) * maxNumDivs;
                c.SetVel(currVel, vel);
            }
        }

        SetBoundCellsVelocities(currVel);
    }
    
    private void SetBounds(float[] d)
    {
        // set the density in the left and right bounds first
        for (int iy = 1; iy < numDivsY - 1; ++iy)
        {
            int i0 = GetIndex(0, iy);
            int i1 = GetIndex(numDivsX - 1, iy);
            d[i0] = d[i0 + 1];
            d[i1] = d[i1 - 1];
        }
        // then in the upper and lower bounds
        for (int ix = 1; ix < numDivsX - 1; ++ix)
        {
            int i0 = GetIndex(ix, 0);
            int i1 = GetIndex(ix, numDivsY - 1);
            d[i0] = d[i0 + numDivsX];
            d[i1] = d[i1 - numDivsX];
        }

        // finally let's calculate the 4 corners as the average of their 2 neighbours
        d[GetIndex(0, 0)] = 0.5f * (d[GetIndex(1, 0)] + d[GetIndex(0, 1)]);
        d[GetIndex(numDivsX - 1, 0)] = 0.5f * (d[GetIndex(numDivsX - 2, 0)] + d[GetIndex(numDivsX - 1, 1)]);
        d[GetIndex(0, numDivsY - 1)] = 0.5f * (d[GetIndex(1, numDivsY - 1)] + d[GetIndex(0, numDivsY - 2)]);
        d[GetIndex(numDivsX - 1, numDivsY - 1)] = 0.5f * (d[GetIndex(numDivsX - 2, numDivsY - 1)] + d[GetIndex(numDivsX - 1, numDivsY - 2)]);
    }

    private void LinearSolve(float[] x, float[] x0, float a, float c)
    {
        float cRecip = 1.0f / c;
        for (int k = 0; k < numIters; k++)
        {
            for (int iy = 1; iy < numDivsY - 1; iy++)
            {
                for (int ix = 1; ix < numDivsX - 1; ix++)
                {
                    x[GetIndex(ix, iy)] =
                        (x0[GetIndex(ix, iy)]
                            + a * (x[GetIndex(ix + 1, iy)]
                                 + x[GetIndex(ix - 1, iy)]
                                 + x[GetIndex(ix, iy + 1)]
                                 + x[GetIndex(ix, iy - 1)]
                            )) * cRecip;
                }
            }

            SetBounds(x);
        }
    }

    #endregion

    #region Advect

    private void AdvectVelocities(float dt)
    {
        float avgNumDivs = 0.5f * (numDivsX + numDivsY);

        float dtNormVel = dt * (maxNumDivs - 2);

        float NfloatX = numDivsX;
        float NfloatY = numDivsY;
        float ifloat, jfloat;
        int i, j;

        for (j = 1, jfloat = 1; j < numDivsY - 1; j++, jfloat++)
        {
            for (i = 1, ifloat = 1; i < numDivsX - 1; i++, ifloat++)
            {
                int index = GetIndex(i, j);
                GridCell cell = GetCell(index);
                Vector2 vel = cell.PrevVel;

                // calculate the original point doing the inverse of the velocity times delta time
                float offsetX = dtNormVel * vel.x;
                float offsetY = dtNormVel * vel.y;
                float x = ifloat - offsetX;
                float y = jfloat - offsetY;

                // ensure this point is within bounds
                x = Mathf.Clamp(x, 0.5f, NfloatX + 0.5f);
                y = Mathf.Clamp(y, 0.5f, NfloatY + 0.5f);

                // calculate the indices to use for the interpolation
                int iX0 = Mathf.FloorToInt(x);
                int iX1 = iX0 + 1;
                int iY0 = Mathf.FloorToInt(y);
                int iY1 = iY0 + 1;

                // calculate the progresses for the interpolation
                float tX = x - (float)iX0;
                float tY = y - (float)iY0;

                // interpolate the velocity
                Vector2 velY0 = Vector2.Lerp(GetCell(iX0, iY0).PrevVel, GetCell(iX1, iY0).PrevVel, tX);
                Vector2 velY1 = Vector2.Lerp(GetCell(iX0, iY1).PrevVel, GetCell(iX1, iY1).PrevVel, tX);
                Vector2 newVel = Vector2.Lerp(velY0, velY1, tY);

                // set it in the cell
                cell.Vel = newVel;
            }
        }

        SetBoundCellsVelocities(true);
    }

    private void AdvectDensities(float dt)
    {
        float avgNumDivs = 0.5f * (numDivsX + numDivsY);

        float dtNormVel = dt * (maxNumDivs - 2);

        float NfloatX = numDivsX;
        float NfloatY = numDivsY;
        float ifloat, jfloat;
        int i, j;

        Color[] newDensities = new Color[cells.Length];
        for (int k = 0; k < cells.Length; ++k)
            newDensities[k] = cells[k].Density;

        for (j = 1, jfloat = 1; j < numDivsY - 1; j++, jfloat++)
        {
            for (i = 1, ifloat = 1; i < numDivsX - 1; i++, ifloat++)
            {
                int index = GetIndex(i, j);
                GridCell cell = GetCell(index);
                Vector2 vel = cell.Vel;

                // calculate the original point doing the inverse of the velocity times delta time
                float offsetX = dtNormVel * vel.x;
                float offsetY = dtNormVel * vel.y;
                float x = ifloat - offsetX;
                float y = jfloat - offsetY;

                // ensure this point is within bounds
                x = Mathf.Clamp(x, 0.5f, NfloatX + 0.5f);
                y = Mathf.Clamp(y, 0.5f, NfloatY + 0.5f);

                // calculate the indices to use for the interpolation
                int iX0 = Mathf.FloorToInt(x);
                int iX1 = iX0 + 1;
                int iY0 = Mathf.FloorToInt(y);
                int iY1 = iY0 + 1;

                // calculate the progresses for the interpolation
                float tX = x - (float)iX0;
                float tY = y - (float)iY0;

                // interpolate the velocity
                Color dY0 = Color.Lerp(GetCell(iX0, iY0).Density, GetCell(iX1, iY0).Density, tX);
                Color dY1 = Color.Lerp(GetCell(iX0, iY1).Density, GetCell(iX1, iY1).Density, tX);
                newDensities[index] = Color.Lerp(dY0, dY1, tY);
            }
        }

        // assign the densities to the cells
        for (int k = 0; k < newDensities.Length; ++k)
            cells[k].Density = newDensities[k];

        // set the bounds
        SetBoundCellsDensity();
    }

    #endregion
}
