using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Assertions;


/// <summary>
/// This class shows the UI of the app
/// </summary>
public class UI : MonoBehaviour
{
    #region Typedefs

	public enum FPSInfoToShow
	{
		AvgFPS,
		MinFPS,
		MaxFPS,
		None,

        Count
    }

    #endregion

    #region Public Attributes

    [Header("Object References")]
	public FPSCounter fpsCounter;

	[Header("Widgets")]
	public TextMeshProUGUI fpsLabel;
	public TextMeshProUGUI gridInfoLabel;
	public TextMeshProUGUI boidsInfoLabel;

	[Header("Misc")]
	public float minTimeBetweenInputs = 0.2f;

	#endregion

	#region Private Attributes

	private float timer = 0.0f;

    private FPSInfoToShow fpsInfo = FPSInfoToShow.AvgFPS;
	private string gridInfoStr = "";
	private string boidsInfoStr = "";

    #endregion

    #region Properties
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
		// check inputs
		UpdateInputs();

        // update the FPS
        UpdateFPS();
    }
	
	#endregion
	
	#region Methods

	/// <summary>
	/// Initialization
	/// </summary>
	public void Init()
	{
		// get the FPS counter
		if (fpsCounter == null)
			fpsCounter = FindFirstObjectByType<FPSCounter>();
		Assert.IsNotNull(fpsCounter, "You need to have a valid FPS counter to show the FPS on screen");

		// compose the grid info string
		FluidGrid grid = FindFirstObjectByType<FluidGrid>();
		if (grid != null)
		{
			gridInfoStr = $"Grid Divs: {grid.numDivsX}x{grid.numDivsY}\n" +
				$"Solver Iters: {grid.numIters}\n" +
				$"Grid Size: {grid.bounds.size}";
			gridInfoLabel.text = gridInfoStr;
		}

		// compose the boids info string
		BoidsController boidsCtrl = FindFirstObjectByType<BoidsController>();
		if (boidsCtrl != null)
		{
			boidsInfoStr = $"Num Boids = {0}";
			boidsInfoLabel.text = boidsInfoStr;
		}
	}

	/// <summary>
	/// Check the inputs to change what's shown on screen
	/// </summary>
	private void UpdateInputs()
	{
		float dt = Time.deltaTime;
		timer += dt;
		if (timer < minTimeBetweenInputs)
			return;

        if (Input.GetKey(KeyCode.F))
        {
            // change the FPS visualization
            fpsInfo = (FPSInfoToShow)(((int)fpsInfo + 1) % (int)FPSInfoToShow.Count);

            // reset the timer
            timer = 0.0f;
        }

        if (Input.GetKey(KeyCode.G))
		{
			// show/hide the grid info
			gridInfoLabel.gameObject.SetActive(!gridInfoLabel.gameObject.activeSelf);

			// reset the timer
			timer = 0.0f;
		}

		if (Input.GetKey(KeyCode.B))
		{
			// show/hide the boids info
			boidsInfoLabel.gameObject.SetActive(!boidsInfoLabel.gameObject.activeSelf);

			// reset the timer
			timer = 0.0f;
		}
    }

	/// <summary>
	/// Update the FPS related info
	/// </summary>
	private void UpdateFPS()
	{
		string fpsStr;
		switch (fpsInfo)
		{
			case FPSInfoToShow.AvgFPS:
				fpsStr = $"{fpsCounter.AvgFrameRate:00.0} ({1000.0f * fpsCounter.AvgFrameTime:00.0} ms)";
				break;
            case FPSInfoToShow.MinFPS:
                fpsStr = $"{fpsCounter.MinFrameRate:00.0} ({1000.0f * fpsCounter.MaxFrameTime:00.0} ms)";
                break;
            case FPSInfoToShow.MaxFPS:
                fpsStr = $"{fpsCounter.MaxFrameRate:00.0} ({1000.0f * fpsCounter.MinFrameTime:00.0} ms)";
                break;
			case FPSInfoToShow.None:
				fpsStr = "";
				break;
            default:
				fpsStr = "";
				Debug.LogErrorFormat("Unknown FPS info to show: {0}", fpsInfo);
				break;
		}

		// update the fps string
		bool fpsVisible = !string.IsNullOrEmpty(fpsStr);
		fpsLabel.gameObject.SetActive(fpsVisible);
		fpsLabel.text = fpsStr;
	}

	#endregion
}
