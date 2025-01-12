using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Assertions;


public class FluidGrid : MonoBehaviour
{
    #region Typedefs

    /// <summary>
    /// Thd different render methods for the fluid simulation
    /// </summary>
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
    [Range(0.0f, 1.0f)]
    public float dissolveRate = 0.25f;
    public int numIters = 10;
    public float maxSpeed = 10.0f;

    [Header("Interactivity")]
    public float densityInputRadius = 0.05f;
    public float velocityInputRadius = 0.025f;
    public float mouseDeltaFactor = 0.1f;
    public float hueVel = 45.0f;

    [Header("Rendering")]
    public MeshRenderer gridRenderer;
    public RenderMethods renderMethod = RenderMethods.Densities;
    public float velForMaxCol = 0.5f;

    #endregion

    #region Protected Attributes

    // cells grid
    protected GridCell[] cells;
    protected int maxNumDivs = 80;

    // the image we'll use to show the fluid
    protected Texture2D image;
    protected Color[] pixels;

    // inputs stuff
    protected float hue = 0.0f;
    
    // Poisson-pressure helper arrays
    protected float[] p;
    protected float[] div;

	#endregion
	
	#region Properties
	#endregion
	
	#region MonoBehaviour Methods

	void Start()
	{
		Init();
	}

    protected void FixedUpdate()
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
        DensityStep(dt);
    }

    protected void Update()
    {
        // update the image
        UpdateImage();
    }

    protected void OnDrawGizmosSelected()
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
            float screenX = pos.x / Screen.width;
            float screenY = pos.y / Screen.height;

            // add some velocity
            Vector2 mouseDelta = Input.mousePositionDelta * mouseDeltaFactor;
            AddVelocityInRadius(screenX, screenY, velocityInputRadius, mouseDelta);

            if (leftButton)
            {
                // get the color to add
                Color col = GetCurrInputColor();

                // add a colour to the cell
                AddColorInRadius(screenX, screenY, densityInputRadius, col);
            }
        }
    }

    /// <summary>
    /// Returns the color of the dye to add to the simulation
    /// </summary>
    /// <returns></returns>
    protected Color GetCurrInputColor()
    {
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

        return col;
    }

    /// <summary>
    /// Applies the given function to all the cells within the given radius of the given position
    /// </summary>
    protected void ApplyFuncInRadius(float screenX, float screenY, float radius, Action<GridCell> cellFunc)
    {
        // calculate the min and max positions in local screen space
        float minX = Mathf.Max(0.0f, screenX - radius);
        float minY = Mathf.Max(0.0f, screenY - radius);
        float maxX = Mathf.Min(1.0f, screenX + radius);
        float maxY = Mathf.Min(1.0f, screenY + radius);

        // convert these positions to cell positions
        int minCellX = Mathf.Clamp(Mathf.RoundToInt(minX * numDivsX), 0, numDivsX - 1);
        int minCellY = Mathf.Clamp(Mathf.RoundToInt(minY * numDivsY), 0, numDivsY - 1);
        int maxCellX = Mathf.Clamp(Mathf.RoundToInt(maxX * numDivsX), 0, numDivsX - 1);
        int maxCellY = Mathf.Clamp(Mathf.RoundToInt(maxY * numDivsY), 0, numDivsY - 1);

        // now iterate over all of them
        float radiusSq = radius * radius;
        for (int iy = minCellY; iy <= maxCellY; ++iy)
        {
            float y = (iy + 0.5f) / (float)numDivsY;
            for (int ix = minCellX; ix <= maxCellX; ++ix)
            {
                float x = (ix + 0.5f) / (float)numDivsX;

                float distSq = (x - screenX) * (x - screenX) + (y - screenY) * (y - screenY);
                if (distSq <= radiusSq)
                {
                    GridCell cell = GetCell(ix, iy);
                    cellFunc(cell);
                }
            }
        }
    }

    /// <summary>
    /// Adds the color to all cells within the given radius
    /// </summary>
    protected void AddColorInRadius(float screenX, float screenY, float radius, Color col)
    {
        ApplyFuncInRadius(screenX, screenY, radius, (cell) => { cell.Density += col; } );
    }

    /// <summary>
    /// Adds the given velocity to all cells within the given radius
    /// </summary>
    protected void AddVelocityInRadius(float screenX, float screenY, float radius, Vector2 vel)
    {
        ApplyFuncInRadius(screenX, screenY, radius, (cell) => { cell.Vel += vel; });
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
    protected Color GetCellColor(GridCell cell)
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

    #region Velocity Step

    /// <summary>
    /// This is the velocity part of the simulation. It first diffuses the velocities and does a projection (on the 
    /// previous velocities) to ensure they are correct, then advects the velocities and does another projection to
    /// leave the velocities in a correct state
    /// </summary>
    /// <param name="dt"></param>
    protected void VelocityStep(float dt)
    {
        DiffuseVelocities(dt);
        Project(false);

        AdvectVelocities(dt);
        Project(true);
    }

    /// <summary>
    /// Sets the value of density of the boundary cells based on their neighbours
    /// </summary>
    protected void SetBoundCellsVelocities(bool currVel)
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

    #region Diffuse Velocities

    /// <summary>
    /// Diffuses the velocities of the cells with their neighbouring cells
    /// </summary>
    /// <param name="dt"></param>
    protected void DiffuseVelocities(float dt)
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

    #endregion

    #region Projection

    /// <summary>
    /// Ensure the density in the liquid stays the same, that is, the amount of liquid going in is the same as 
    /// the amount going out. We're simulating an incompressible liquid so this is a constant. The dye we put on
    /// top is considered to be part of the incompressive fluid and thus not considered as extra density.
    /// What we try to do here is ensure that the densities going in from different cells are the same as the ones
    /// going out of this cell. To do so we're using an algorithm from here:
    /// https://mikeash.com/pyblog/fluid-simulation-for-dummies.html
    /// He doesn't seem to have a clear understanding of how this algorithm translates the theory of the Poisson
    /// equations. It seems we're solving the Poisson-pressure equations using Gauss-Seidel.
    /// </summary>
    protected void Project(bool currVel)
    {
        // prepare the values to solve the Poisson-pressure equations
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

        // fill the bounds
        SetBounds(div);
        SetBounds(p);

        // use Gauss-Seidel to solve them
        LinearSolve(p, div, 1.0f, 4.0f);

        // convert the pressure field to velocity increments
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

        // update the bounds
        SetBoundCellsVelocities(currVel);
    }

    /// <summary>
    /// Sets the bounds of the given array, which represents a 2D map of size numDivsX * numDivsY. In it we
    /// fill the boundary cells with values equivalent to their neighbours
    /// </summary>
    /// <param name="d"></param>
    protected void SetBounds(float[] d)
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

    /// <summary>
    /// Uses the Gauss-Seidel iterative method to solve a set of equations that basically are:
    /// f(x, y) = (1.0 / c) * (f0(x, y) + a * (f(x + 1, y) + f(x - 1, y) + f(x, y + 1) + f(x, y - 1)))
    /// where a and c are given constants and f(x, y) is the set of values we're looking for and 
    /// f0(x, y) is the current values in the grid
    /// </summary>
    protected void LinearSolve(float[] x, float[] x0, float a, float c)
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

    #region Advection

    /// <summary>
    /// This method advects the velocities of all cells in the grid using the calculated velocities. Then it goes 'back
    /// in time' to the point where this velocity is coming from and sets the velocity
    /// </summary>
    /// <param name="dt"></param>
    protected void AdvectVelocities(float dt)
    {
        float avgNumDivs = 0.5f * (numDivsX + numDivsY);

        float dtNormVel = dt * (maxNumDivs - 2);

        float NfloatX = numDivsX - 2;
        float NfloatY = numDivsY - 2;
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

    #endregion

    #endregion

    #region Density Step

    /// <summary>
    /// Performs the density step for the fluid simulation. It includes the diffusion of the density (of the dye we
    /// put in the fluid), and then the advection of the densities in the different cells based on the current velocities
    /// (that are defined as a vector field with the cells).
    /// This method may include a dissolution step too which will progressively reduce the dye in the liquid to progressively
    /// dissolve the dye and thus not have it linger there forever
    /// </summary>
    protected void DensityStep(float dt)
    {
        DiffuseDensity(dt);
        AdvectDensities(dt);

        DissolveDensity(dt);
    }

    #region Diffusion

    /// <summary>
    /// Diffuses the density of the cells with their neighbours
    /// </summary>
    /// <param name="dt"></param>
    protected void DiffuseDensity(float dt)
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
    protected void SetBoundCellsDensity()
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

    #region Advection

    /// <summary>
    /// This method advects the densities in the grid considering the velocity of the cells 
    /// </summary>
    /// <param name="dt"></param>
    protected void AdvectDensities(float dt)
    {
        float avgNumDivs = 0.5f * (numDivsX + numDivsY);

        float dtNormVel = dt * (maxNumDivs - 2);

        float NfloatX = numDivsX - 2;
        float NfloatY = numDivsY - 2;
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

    #region Dissolution

    /// <summary>
    /// Dissolves the densities of the cells by a fixed rate (dissolveRate) over the given time 
    /// </summary>
    /// <param name="dt"></param>
    protected void DissolveDensity(float dt)
    {
        // calculate the 'persistence', that is, how much of the density to keep
        float loss = dissolveRate * dt;
        float persistence = 1.0f - loss;

        // apply it to all cells
        foreach (GridCell cell in cells)
            cell.Density *= persistence;
    }

    #endregion

    #endregion
}
