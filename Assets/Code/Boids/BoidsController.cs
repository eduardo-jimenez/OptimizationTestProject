using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// This class manages the boids in the scene: creates them, updates them, etc.
/// </summary>
public class BoidsController : MonoBehaviour
{
	#region Public Attributes

	public Boid boidPrefab = null;
	public int numBoids = 1000;
	public Bounds bounds = new Bounds(new Vector3(0.0f, 0.0f, 0.0f), new Vector3(160.0f / 9.0f, 10.0f, 0.0f));

	#endregion

	#region Private Attributes

	private Boid[] boids = null;

	#endregion
	
	#region Properties

	public int NumBoids => (boids != null) ? boids.Length : 0;
	public Boid[] Boids => boids;

	#endregion
	
	#region MonoBehaviour Methods

	// Use this for initialization
	void Start()
	{
		Init();
	}
	
	// Update is called once per frame
	void Update()
	{
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
    private void Init()
	{
		// create the boids
		boids = new Boid[numBoids];

		Vector3 minPos = bounds.center - 0.9f * bounds.extents;
        Vector3 maxPos = bounds.center + 0.9f * bounds.extents;

        for (int i = 0; i < boids.Length; ++i)
		{
			// create the boid
			boids[i] = GameObject.Instantiate<Boid>(boidPrefab);
			boids[i].name = $"Boid {i + 1}";
			boids[i].transform.SetParent(transform);

			// find a random position and direction for the new boid
			float x = Random.Range(minPos.x, maxPos.x);
			float y = Random.Range(minPos.y, maxPos.y);
			float angle = Random.Range(-Mathf.PI, Mathf.PI);
			Vector2 pos = new Vector2(x, y);
			Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            // set the position and direction to the boid and initialize it
            boids[i].Init(this);
            boids[i].Pos = pos;
			boids[i].Dir = dir;
			boids[i].Vel = dir * boids[i].minSpeed;
		}
	}

	/// <summary>
	/// Returns the list of boids that are within a given radius.
	/// This method uses the brute force approach
	/// </summary>
	public List<Boid> FindBoidsInCircle(Vector2 pos, float radius, Boid boidToIgnore)
	{
		List<Boid> nearBoids = new List<Boid>();

		foreach (Boid boid in boids)
		{
			if (boid == boidToIgnore)
				continue;

			Vector2 boidPos = boid.Pos;
			float dist = (boidPos - pos).magnitude;
			if (dist <= radius)
				nearBoids.Add(boid);
		}

		return nearBoids;
	}

	#endregion
}
