using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


public class GridCell
{
	#region Attributes

	private FluidGrid grid;
    private Vector2Int pos;

    private Color density = new Color(0.0f, 0.0f, 0.0f);
	private Color prevDensity = new Color(0.0f, 0.0f, 0.0f);
	private Vector2 vel = Vector2.zero;
    private Vector2 prevVel = Vector2.zero;

    #endregion

    #region Properties

    public Vector2Int Pos => pos;

    public Color Density
	{
		get => density;
		set => density = value;
	}
	public Color PrevDensity
	{
		get => prevDensity;
		set => prevDensity = value;
	}
	public Vector2 Vel
	{
		get => vel;
		set => vel = value;
	}
    public Vector2 PrevVel
    {
        get => prevVel;
        set => prevVel = value;
    }

	public GridCell North => pos.y > 0 ? grid.GetCell(pos.x, pos.y - 1) : null;
	public GridCell South => pos.y < grid.numDivsY - 1 ? grid.GetCell(pos.x, pos.y + 1) : null;
    public GridCell West => pos.x > 0 ? grid.GetCell(pos.x - 1, pos.y) : null;
    public GridCell East => pos.x < grid.numDivsX - 1 ? grid.GetCell(pos.x + 1, pos.y) : null;

    #endregion

    #region Initialization

    /// <summary>
    /// Default constructor
    /// </summary>
    public GridCell()
	{
	}

	/// <summary>
	/// Initialization
	/// </summary>
	/// <param name="grid"></param>
	/// <param name="pos"></param>
	public void Init(FluidGrid grid, Vector2Int pos)
	{
		// assign the information on the cell
		this.grid = grid;
		this.pos = pos;

		// reset the default fluid parameters
		density = new Color(0.0f, 0.0f, 0.0f);
        prevDensity = new Color(0.0f, 0.0f, 0.0f);
        vel = Vector2.zero;
        prevVel = Vector2.zero;
	}
	
	#endregion
	
	#region Methods

	/// <summary>
	/// Draws info of this cell using Gizmos
	/// </summary>
	public void DrawGizmos()
	{
		Vector3 p = grid.Get3DPos(pos);
        Gizmos.color = new Color(0.0f, 0.2f, 1.0f);
        Gizmos.DrawLine(p, p + new Vector3(vel.x, vel.y, 0.0f));
        Gizmos.color = new Color(1.0f, 0.8f, 0.0f);
        Gizmos.DrawLine(p, p + new Vector3(prevVel.x, prevVel.y, 0.0f));
    }

    /// <summary>
    /// Returns the current or previous velocity
    /// </summary>
    /// <param name="getCurrentVel"></param>
    /// <returns></returns>
    public Vector2 GetVel(bool getCurrentVel)
    {
        return getCurrentVel ? vel : prevVel;
    }

    /// <summary>
    /// Sets the current or previous velocity
    /// </summary>
    /// <param name="getCurrentVel"></param>
    /// <returns></returns>
    public void SetVel(bool setCurrentVel, Vector2 value)
    {
        if (setCurrentVel)
            vel = value;
        else
            prevVel = value;
    }

    #endregion
}
