using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// This class counts the FPS the app is running at. Allows showing the maximum (in a time), the average, etc.
/// </summary>
public class FPSCounter : MonoBehaviour
{
	#region Public Attributes

	public int poolSize = 150;

	#endregion

	#region Private Attributes

	// data
	private float[] frameTimes = null;
	private int pointer = 0;
	private float lastRealTime = 0.0f;

	// cached info
	private float avgFrameTime = 1.0f / 30.0f;
	private float minFrameTime = 1.0f / 30.0f;
    private float maxFrameTime = 1.0f / 30.0f;

    #endregion

    #region Properties

	public float AvgFrameTime => avgFrameTime;
	public float MinFrameTime => minFrameTime;
	public float MaxFrameTime => maxFrameTime;

	public float AvgFrameRate => 1.0f / avgFrameTime;
	public float MaxFrameRate => 1.0f / minFrameTime;
    public float MinFrameRate => 1.0f / maxFrameTime;

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
		// get the (real) delta time
		float time = Time.realtimeSinceStartup;
		float dt = time - lastRealTime;
		if (dt <= 0.0f)
			return;

		// add the time to the pool
		frameTimes[pointer] = dt;
		pointer = (pointer + 1) % poolSize;
		lastRealTime = time;

		// re-calculate the cached params
		UpdateCachedParams();
	}
	
	#endregion
	
	#region Methods

	/// <summary>
	/// Initialization
	/// </summary>
	public void Init()
	{
		// create the pool
		pointer = 0;
		frameTimes = new float[poolSize];
		for (int i = 0; i < poolSize; ++i)
			frameTimes[i] = 1.0f / 30.0f;

		// set the last real time
		lastRealTime = Time.realtimeSinceStartup;

		// update the cached parameters
		UpdateCachedParams();
	}

	/// <summary>
	/// Updates the cached parameters: average, minimum and maximum frame rate
	/// </summary>
	public void UpdateCachedParams()
	{
		avgFrameTime = frameTimes[0];
		minFrameTime = frameTimes[0];
		maxFrameTime = frameTimes[0];
		for (int i = 1; i < poolSize; ++i)
		{
			avgFrameTime += frameTimes[i];
			minFrameTime = Mathf.Min(minFrameTime, frameTimes[i]);
			maxFrameTime = Mathf.Max(maxFrameTime, frameTimes[i]);
        }

		avgFrameTime /= (float)poolSize;
	}

	#endregion
}
