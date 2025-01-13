using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// This class represents a single boid that moves autonomously in the boids zone
/// </summary>
public class Boid : MonoBehaviour
{
	#region Public Attributes

	[Header("Generic Params")]
	public float minSpeed = 0.5f;
	public float maxSpeed = 2.0f;

	[Header("Cohesion")]
	public float cohesionRadius = 2.0f;
	public float maxCohesionForce = 0.5f;

	[Header("Separation")]
	public float maxSeparationRadius = 0.5f;
	public float radiusForMaxSeparationForce = 0.1f;
	public float maxSeparationForce = 1.0f;

	[Header("Alignment")]
	public float defaultAlignmentForce = 0.5f;
	public int numBoidsForMaxAligmentForce = 5;

	[Header("Border Repulsion")]
	public float distToStartRepulsion = 1.0f;
	public float distForMaxRepulsion = 0.5f;
	public float maxRepulsionForce = 2.0f;

	#endregion

	#region Private Attributes

	private bool initialized = false;
	private BoidsController boidsCtrl = null;
	private Vector2 vel = Vector2.zero;

	private Vector2 totalForce = Vector2.zero;
	private Vector2 cohesionForce = Vector2.zero;
	private Vector2 separationForce = Vector2.zero;
	private Vector2 alingmentForce = Vector2.zero;
	private Vector2 repulsionForce = Vector2.zero;

	#endregion

	#region Properties

	public bool Initialized => initialized;
	public BoidsController BoidsCtrl => boidsCtrl;

	public Vector2 Vel
	{
		get => vel;
		set => vel = value;
	}
	public Vector2 Pos
	{ 
		get => new Vector2(transform.position.x, transform.position.y);
		set => transform.position = new Vector3(value.x, value.y, 0.0f);
	}
	public Vector2 Dir
	{
		get => new Vector2(transform.right.x, transform.right.y);
		set => SetDirection(value);
	}

	#endregion
	
	#region MonoBehaviour Methods

	// Update is called once per frame
	void FixedUpdate()
	{
		float dt = Time.fixedDeltaTime;

		DoUpdate(dt);
	}

    private void OnDrawGizmos()
    {
		Gizmos.color = Color.white;
		Gizmos.matrix = transform.localToWorldMatrix;
		Gizmos.DrawLine(new Vector3(-0.25f, 0.125f, 0.0f), new Vector3(0.25f, 0.0f, 0.0f));
		Gizmos.DrawLine(new Vector3(0.25f, 0.0f, 0.0f), new Vector3(0.25f, 0.125f, 0.0f));
		Gizmos.DrawLine(new Vector3(-0.25f, -0.125f, 0.0f), new Vector3(-0.25f, 0.125f, 0.0f));

		Gizmos.color = new Color(1.0f, 0.0f, 0.0f);
		Gizmos.DrawLine(new Vector3(0.0f, 0.0f, 0.0f), separationForce);
		Gizmos.color = new Color(0.0f, 0.0f, 1.0f);
		Gizmos.DrawLine(new Vector3(0.0f, 0.0f, 0.0f), cohesionForce);
        Gizmos.color = new Color(0.0f, 1.0f, 1.0f);
        Gizmos.DrawLine(new Vector3(0.0f, 0.0f, 0.0f), alingmentForce);
        Gizmos.color = new Color(1.0f, 0.0f, 1.0f);
        Gizmos.DrawLine(new Vector3(0.0f, 0.0f, 0.0f), repulsionForce);
		Gizmos.color = new Color(0.0f, 1.0f, 0.0f);
		Gizmos.DrawLine(new Vector3(0.0f, 0.0f, 0.0f), totalForce);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Initialization
    /// </summary>
    public void Init(BoidsController boidsCtrl)
	{
		if (initialized)
			return;

		// set the parameters
		this.boidsCtrl = boidsCtrl;

		// reset some attributes
		vel = new Vector2(0.0f, 0.0f);

		// mark as initialized
		initialized = true;
	}

	/// <summary>
	/// Sets the orientation of the boid to the given one
	/// </summary>
	/// <param name="dir"></param>
	private void SetDirection(Vector2 dir)
	{
		float angle = Mathf.Atan2(dir.y, dir.x);
		float angleDegs = Mathf.Rad2Deg * angle;
		transform.eulerAngles = new Vector3(0.0f, 0.0f, angleDegs);
	}

    #endregion

    #region Simulation

	/// <summary>
	/// Updates the simulation of this boid
	/// </summary>
	/// <param name="dt"></param>
	private void DoUpdate(float dt)
	{
		// generate the forces
		Vector2 pos = Pos;
        totalForce = new Vector2(0.0f, 0.0f);
        totalForce += UpdateCohesion();
        totalForce += UpdateAlignment();
        totalForce += UpdateSeparation();
        totalForce += UpdateBorderRepulsion();

		// apply the force to the velocity
		vel += totalForce * dt;

		// make sure the velocity is within the minimum and maximum
		float speed = vel.magnitude;
		if (speed < Mathf.Epsilon)
			vel = Dir * minSpeed;
		else if (speed < minSpeed)
			vel *= minSpeed / speed;
		else if (speed > maxSpeed)
			vel *= maxSpeed / speed;

		// update the movement
		pos += vel * dt;
		Pos = pos;

		// finally update the direction
		SetDirection(vel);
	}

    /// <summary>
    /// Returns a force to try to get boids to 'fly' or 'swim' in a flock/bank
    /// </summary>
    private Vector2 UpdateCohesion()
    {
        cohesionForce = new Vector2(0.0f, 0.0f);

		Vector2 pos = Pos;
		List<Boid> nearbyBoids = boidsCtrl.FindBoidsInCircle(pos, cohesionRadius, this);
		if (nearbyBoids.Count > 0)
		{
			// calculate the center of the boids around
			Vector2 flockCenter = new Vector2(0.0f, 0.0f);
			foreach (Boid boid in nearbyBoids)
				flockCenter += boid.Pos;
			flockCenter *= 1.0f / (float)nearbyBoids.Count;

			// create a force towards the flock center
			Vector2 dirToCenter = flockCenter - pos;
			if (dirToCenter.magnitude > 1.0f)
				dirToCenter.Normalize();
            cohesionForce = dirToCenter * maxCohesionForce;
		}

		return cohesionForce;
    }

    /// <summary>
    /// Returns a force to keep boids from colliding with each other
    /// </summary>
    private Vector2 UpdateSeparation()
    {
        separationForce = new Vector2(0.0f, 0.0f);

        Vector2 pos = Pos;
        List<Boid> nearbyBoids = boidsCtrl.FindBoidsInCircle(pos, maxSeparationRadius, this);
        if (nearbyBoids.Count > 0)
        {
            // go adding the forces to separate the boid from nearby boids
            foreach (Boid boid in nearbyBoids)
			{
				// calculate the direction and distance to this boid
				Vector2 boidPos = boid.Pos;
				Vector2 dirToBoid = boidPos - pos;
				float distToBoid = dirToBoid.magnitude;

				// if at the exact same position we don't do calcs since they become unstable
				if (distToBoid > Mathf.Epsilon)
				{
					// calculate the force to apply 
					dirToBoid *= 1.0f / distToBoid;
					float forceT = Mathf.Clamp01(1.0f - (distToBoid - radiusForMaxSeparationForce) / (maxSeparationRadius - radiusForMaxSeparationForce));
					float forceAmount = maxSeparationForce * forceT;
					Vector2 separationForce = dirToBoid * forceAmount;

                    // add it to the total amount
                    separationForce += separationForce;
				}
			}
        }

        return separationForce;
    }

    /// <summary>
    /// Returns a force to try to get all the boids looking in the same direction
    /// </summary>
    private Vector2 UpdateAlignment()
	{
        Vector2 pos = Pos;
        List<Boid> nearbyBoids = boidsCtrl.FindBoidsInCircle(pos, maxSeparationRadius, this);
        if (nearbyBoids.Count > 0)
		{
			// average the direction of the nearby boids
			Vector2 avgDir = new Vector2(0.0f, 0.0f);
			foreach (Boid boid in nearbyBoids)
				avgDir += boid.Dir;
			avgDir.Normalize();

			// add a force in that direction
			alingmentForce = avgDir * defaultAlignmentForce;
		}

        return alingmentForce;
    }

    /// <summary>
    /// If close to the borders this will return a force to move them away
    /// </summary>
    private Vector2 UpdateBorderRepulsion()
	{
        repulsionForce = new Vector2(0.0f, 0.0f);

        // get the position and bounds
		Vector2 pos = Pos;
        Bounds bounds = boidsCtrl.bounds;
        Vector2 min = new Vector2(bounds.min.x, bounds.min.y);
        Vector2 max = new Vector2(bounds.max.x, bounds.max.y);
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

        return repulsionForce;
    }

    #endregion
}
