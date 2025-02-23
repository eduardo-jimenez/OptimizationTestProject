using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;
using UnityEngine.Profiling;


/// <summary>
/// This class represents a single boid that moves autonomously in the boids zone
/// </summary>
public class BaseBoid : MonoBehaviour
{
	#region Public Attributes

	[Header("Generic Params")]
	public float minSpeed = 0.3f;
	public float maxSpeed = 1.5f;

	[Header("Cohesion")]
	public float cohesionRadius = 1.5f;
	public float maxCohesionForce = 0.5f;

	[Header("Separation")]
	public float maxSeparationRadius = 0.5f;
	public float radiusForMaxSeparationForce = 0.1f;
	public float maxSeparationForce = 1.0f;

	[Header("Alignment")]
	public float alignmentRadius = 1.0f;
	public float defaultAlignmentForce = 0.5f;
	public int numBoidsForMaxAligmentForce = 5;

	[Header("Border Repulsion")]
	public float distToStartRepulsion = 1.0f;
	public float distForMaxRepulsion = 0.5f;
	public float maxRepulsionForce = 2.0f;

	#endregion

	#region Protected Attributes

	protected bool initialized = false;
	protected BoidsController boidsCtrl = null;
	protected Vector2 vel = Vector2.zero;

	protected Vector2 totalForce = Vector2.zero;
	protected Vector2 cohesionForce = Vector2.zero;
	protected Vector2 separationForce = Vector2.zero;
	protected Vector2 alingmentForce = Vector2.zero;
	protected Vector2 repulsionForce = Vector2.zero;

	#endregion

	#region Properties

	public bool Initialized => initialized;
	public BoidsController BoidsCtrl => boidsCtrl;

	public virtual int ThreadIndex
	{
		get => 0;
		set { }		//< do nothing
	}
	public virtual Vector2 Vel
	{
		get => vel;
		set => vel = value;
	}
	public virtual Vector2 Pos
	{ 
		get => new Vector2(transform.position.x, transform.position.y);
		set => transform.position = new Vector3(value.x, value.y, 0.0f);
	}
	public virtual Vector2 Dir
	{
		get => new Vector2(transform.right.x, transform.right.y);
		set => SetDirection(value);
	}

    public virtual int NumNearbyBoids => 0;

    #endregion

    #region MonoBehaviour Methods

#if UPDATE_IN_BOIDS
	// Update is called once per frame
	protected virtual void FixedUpdate()
	{
		float dt = Time.fixedDeltaTime;

		DoUpdate(dt);
	}
#endif

    protected void OnDrawGizmos()
    {
		Gizmos.color = Color.white;
		Gizmos.matrix = transform.localToWorldMatrix;
		Gizmos.DrawLine(new Vector3(-0.125f, 0.0625f, 0.0f), new Vector3(0.125f, 0.0f, 0.0f));
		Gizmos.DrawLine(new Vector3(0.125f, 0.0f, 0.0f), new Vector3(-0.125f, -0.0625f, 0.0f));
		Gizmos.DrawLine(new Vector3(-0.125f, -0.0625f, 0.0f), new Vector3(-0.125f, 0.0625f, 0.0f));

		Gizmos.matrix = Matrix4x4.Translate(transform.position);
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

    protected void OnDrawGizmosSelected()
    {
        Gizmos.matrix = Matrix4x4.Translate(transform.position);
        Gizmos.color = new Color(1.0f, 0.0f, 0.0f);
        DrawGizmosCircle(Vector3.zero, maxSeparationRadius);
        Gizmos.color = new Color(0.0f, 0.0f, 1.0f);
        DrawGizmosCircle(Vector3.zero, cohesionRadius);
        Gizmos.color = new Color(0.0f, 1.0f, 1.0f);
        DrawGizmosCircle(Vector3.zero, alignmentRadius);
    }

	protected void DrawGizmosCircle(Vector3 pos, float radius, int numSteps = 16)
	{
		for (int i = 1; i <= numSteps; ++i)
		{
			// calculate the angles to join
			float t0 = (float)(i - 1) / (float)numSteps;
			float t1 = (float)i / (float)numSteps;
			float a0 = 2.0f * Mathf.PI * t0;
			float a1 = 2.0f * Mathf.PI * t1;

			// then the points
			Vector3 p0 = pos + new Vector3(Mathf.Cos(a0) * radius, Mathf.Sin(a0) * radius, 0.0f);
			Vector3 p1 = pos + new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0.0f);

			// draw a line between them
			Gizmos.DrawLine(p0, p1);
        }
	}

    #endregion

    #region Methods

    /// <summary>
    /// Initialization
    /// </summary>
    public virtual void Init(BoidsController boidsCtrl)
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
	protected void SetDirection(Vector2 dir)
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
	public virtual void DoUpdate(float dt)
	{
		BaseUpdate(dt);
	}

    /// <summary>
    /// Updates the simulation of this boid
    /// </summary>
    protected void BaseUpdate(float dt)
	{
        Profiler.BeginSample("Boid Update");

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

        Profiler.EndSample();
    }

    /// <summary>
    /// Returns a force to try to get boids to 'fly' or 'swim' in a flock/bank
    /// </summary>
    protected virtual Vector2 UpdateCohesion()
    {
		Profiler.BeginSample("UpdateCohesion");

        cohesionForce = new Vector2(0.0f, 0.0f);

		Vector2 pos = Pos;
		List<BaseBoid> nearbyBoids = boidsCtrl.FindBoidsInCircleBruteForce(pos, cohesionRadius, this);
		if (nearbyBoids.Count > 0)
		{
			// calculate the center of the boids around
			Vector2 flockCenter = new Vector2(0.0f, 0.0f);
			foreach (BaseBoid boid in nearbyBoids)
				flockCenter += boid.Pos;
			flockCenter *= 1.0f / (float)nearbyBoids.Count;

			// create a force towards the flock center
			Vector2 dirToCenter = flockCenter - pos;
			if (dirToCenter.magnitude > 1.0f)
				dirToCenter.Normalize();
            cohesionForce = dirToCenter * maxCohesionForce;
		}

		Profiler.EndSample();

		return cohesionForce;
    }

    /// <summary>
    /// Returns a force to keep boids from colliding with each other
    /// </summary>
    protected virtual Vector2 UpdateSeparation()
    {
        Profiler.BeginSample("UpdateSeparation");

        separationForce = new Vector2(0.0f, 0.0f);

        Vector2 pos = Pos;
        List<BaseBoid> nearbyBoids = boidsCtrl.FindBoidsInCircleBruteForce(pos, maxSeparationRadius, this);
        if (nearbyBoids.Count > 0)
        {
            // go adding the forces to separate the boid from nearby boids
            foreach (BaseBoid boid in nearbyBoids)
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
					float forceT = 1.0f - Mathf.Clamp01((distToBoid - radiusForMaxSeparationForce) / (maxSeparationRadius - radiusForMaxSeparationForce));
					float forceAmount = maxSeparationForce * forceT;
					Vector2 force = -dirToBoid * forceAmount;

                    // add it to the total amount
                    separationForce += force;
				}
			}
        }

        Profiler.EndSample();

        return separationForce;
    }

    /// <summary>
    /// Returns a force to try to get all the boids looking in the same direction
    /// </summary>
    protected virtual Vector2 UpdateAlignment()
	{
        Profiler.BeginSample("UpdateAlignment");

        Vector2 pos = Pos;
        List<BaseBoid> nearbyBoids = boidsCtrl.FindBoidsInCircleBruteForce(pos, alignmentRadius, this);
        if (nearbyBoids.Count > 0)
		{
			// average the direction of the nearby boids
			Vector2 avgDir = new Vector2(0.0f, 0.0f);
			foreach (BaseBoid boid in nearbyBoids)
				avgDir += boid.Dir;
			avgDir.Normalize();

			// add a force in that direction
			alingmentForce = avgDir * defaultAlignmentForce;
		}

        Profiler.EndSample();

        return alingmentForce;
    }

    /// <summary>
    /// If close to the borders this will return a force to move them away
    /// </summary>
    protected virtual Vector2 UpdateBorderRepulsion()
	{
        Profiler.BeginSample("UpdateBorderRepulsion");

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

        Profiler.EndSample();

        return repulsionForce;
    }

    #endregion
}
