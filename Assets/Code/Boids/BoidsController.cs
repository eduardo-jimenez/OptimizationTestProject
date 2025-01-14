using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;


public enum BoidType
{
	Basic,
	ReuseBoids,

	Count
}

/// <summary>
/// This class manages the boids in the scene: creates them, updates them, etc.
/// </summary>
public class BoidsController : MonoBehaviour
{
	#region Public Attributes

	public const int InitialMaxCapacityBoidsList = 32 * 1024;

	[Header("Zone Parameters")]
	public int initialNumBoids = 0;
	public Bounds bounds = new Bounds(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(160.0f / 9.0f, 10.0f, 0.0f));

	[Header("Boid Prefabs")]
    public BaseBoid boidPrefab = null;
    public BaseBoid reuseBoidPrefab = null;

    #endregion

    #region Private Attributes

    private int boidCreationCount = 0;
	private List<BaseBoid> boids = new List<BaseBoid>(InitialMaxCapacityBoidsList);

	#endregion
	
	#region Properties

	public int NumBoids => (boids != null) ? boids.Count : 0;
	public List<BaseBoid> Boids => boids;

	#endregion
	
	#region MonoBehaviour Methods

	void Start()
	{
		Init();
	}

    private void OnDrawGizmos()
    {
		Gizmos.color = new Color(0.7f, 0.7f, 0.7f);
		Gizmos.DrawWireCube(bounds.center, bounds.size);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Initialization
    /// </summary>
    public void Init()
	{
		// try to get the number of boids from a config file
		//TryToLoadConfigFile();

		// add the initial number of boids
		AddBoids(initialNumBoids, BoidType.Basic);
	}

	/// <summary>
	/// Adds the given number of boids
	/// </summary>
	/// <param name="numBoidsToAdd"></param>
	public void AddBoids(int numBoidsToAdd, BoidType boidType)
	{
        Vector3 minPos = bounds.center - 0.9f * bounds.extents;
        Vector3 maxPos = bounds.center + 0.9f * bounds.extents;

        for (int i = 0; i < numBoidsToAdd; ++i)
        {
			// create the boid
			BaseBoid boid = CreateBoid(boidType);

            // find a random position and direction for the new boid
            float x = Random.Range(minPos.x, maxPos.x);
            float y = Random.Range(minPos.y, maxPos.y);
            float angle = Random.Range(-Mathf.PI, Mathf.PI);
            Vector2 pos = new Vector2(x, y);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            // set the position and direction to the boid and initialize it
            boid.Init(this);
            boid.Pos = pos;
            boid.Dir = dir;
            boid.Vel = dir * boid.minSpeed;

			// add the boid to the list
			boids.Add(boid);
        }
    }

	/// <summary>
	/// Creates and returns a boid of the given type
	/// </summary>
	/// <param name="type"></param>
	/// <returns></returns>
	private BaseBoid CreateBoid(BoidType type)
	{
		BaseBoid boid;

        switch (type)
		{
			case BoidType.Basic:
                boid = GameObject.Instantiate<BaseBoid>(boidPrefab);
                break;
			case BoidType.ReuseBoids:
				boid = GameObject.Instantiate<BaseBoid>(reuseBoidPrefab);
				break;
			default:
				boid = null;
				Debug.LogErrorFormat("Unknown boid type: {0}", type);
				break;
		}

		if (boid != null)
		{
			boid.name = $"Boid {boidCreationCount++}";
			boid.transform.SetParent(transform);
		}

        return boid;
	}

	/// <summary>
	/// Clears the boids from the screen
	/// </summary>
	public void ClearBoids()
	{
		// destroy all the boids
		foreach (BaseBoid boid in boids)
			Destroy(boid.gameObject);

		// clear the list of boids
		boids.Clear();
	}

    /// <summary>
    /// Tries to load the config file 'boids.config' with just a number (the number of boids)
    /// </summary>
    private void TryToLoadConfigFile()
	{
		try
		{
			string contents = File.ReadAllText("boids.config");
			if (int.TryParse(contents, out int num))
			{
				if (num > 0)
					initialNumBoids = num;
			}
		}
		catch (System.Exception e)
		{
			Debug.LogWarning("Error loading config file for boids");
			Debug.LogWarning(e.ToString());
		}
	}

    /// <summary>
    /// Returns the list of boids that are within a given radius.
    /// This method uses the brute force approach
    /// </summary>
    public virtual List<BaseBoid> FindBoidsInCircleBruteForce(Vector2 pos, float radius, BaseBoid boidToIgnore)
	{
        Profiler.BeginSample("FindBoidsInCircle Brute Force");

        List<BaseBoid> nearBoids = new List<BaseBoid>();

		foreach (BaseBoid boid in boids)
		{
			if (boid == boidToIgnore)
				continue;

			Vector2 boidPos = boid.Pos;
			float dist = (boidPos - pos).magnitude;
			if (dist <= radius)
				nearBoids.Add(boid);
		}

		Profiler.EndSample();

		return nearBoids;
	}

	/// <summary>
	/// Fills the given list of boids with the ones that are within a given radius.
	/// This method uses the brute force approach
	/// </summary>
	public virtual void FindBoidsInCircleBruteForce(Vector2 pos, float radius, BaseBoid boidToIgnore, ref List<BaseBoid> nearBoids)
    {
        Profiler.BeginSample("FindBoidsInCircle Brute Force");

		nearBoids.Clear();

        foreach (BaseBoid boid in boids)
        {
            if (boid == boidToIgnore)
                continue;

            Vector2 boidPos = boid.Pos;
            float dist = (boidPos - pos).magnitude;
            if (dist <= radius)
                nearBoids.Add(boid);
        }

        Profiler.EndSample();
    }

    #endregion
}
