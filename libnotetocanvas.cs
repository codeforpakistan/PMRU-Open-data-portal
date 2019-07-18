using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System.IO;
using UnityEngine.UI;

using UnityEngine.XR.iOS; // Import ARKit Library
[RequireComponent(typeof(ShapeManager))]
public class libnotetocanvas : MonoBehaviour, PlacenoteListener
{
  //public testdebug testbug;
    // Unity ARKit Session handler
    private UnityARSessionNativeInterface mSession;
    private ShapeManager shapeManager;
    // UI game object references
    private bool mFrameUpdated = false;
    private UnityARImageFrameData mImage = null;
    private bool mARKitInit = false;
    public Text notifications;
    private UnityARCamera mARCamera;
    private bool shouldRecordWaypoints = false;
    private bool mReportDebug = false;
    

    // to hold the last saved MapID
    // private string savedMapID;
    public void OnPose(Matrix4x4 outputPose, Matrix4x4 arkitPose) { }

    // Runs when LibPlacenote sends a status change message like Localized!
    public void OnStatusChange(LibPlacenote.MappingStatus prevStatus, LibPlacenote.MappingStatus currStatus)
    {
        if (currStatus == LibPlacenote.MappingStatus.RUNNING && prevStatus == LibPlacenote.MappingStatus.LOST)
        {
            notifications.text = "Localized!";
        }
    }
    void Start()
    {
        Debug.Log("start ftn");
    }
    private void StartARKit()
    {
        Application.targetFrameRate = 60;
        ARKitWorldTrackingSessionConfiguration config = new ARKitWorldTrackingSessionConfiguration();
        config.planeDetection = UnityARPlaneDetection.Horizontal;
        config.alignment = UnityARAlignment.UnityARAlignmentGravity;
        config.getPointCloudData = true;
        config.enableLightEstimation = true;
        mSession.RunWithConfig(config);
    }


    public void startbtn()
    {
        shapeManager = GetComponent<ShapeManager>();

        Input.location.Start();
        // Start ARKit using the Unity ARKit Plugin
        mSession = UnityARSessionNativeInterface.GetARSessionNativeInterface();
        UnityARSessionNativeInterface.ARFrameUpdatedEvent += ARFrameUpdated;
        //        Debug.Log("bool true"+ mFrameUpdated);
        StartARKit();

        FeaturesVisualizer.EnablePointcloud(); // Optional - to see the point features

        LibPlacenote.Instance.RegisterListener(this); // Register listener for onStatusChange and OnPose
        notifications.text = "scaning enviroment";
        Debug.Log("btn click start");
    }
    //void OnDisable()
    //{
    //    UnityARSessionNativeInterface.ARFrameUpdatedEvent -= ARFrameUpdated;
    //}

  
    private void ARFrameUpdated(UnityARCamera camera)
    {
       // testbug.ss("mframe reached");
        mFrameUpdated = true;
        Debug.Log("mframe from ar ftn" + mFrameUpdated);
        mARCamera = camera;
    }
    private void InitARFrameBuffer()
    {
        mImage = new UnityARImageFrameData();

        int yBufSize = mARCamera.videoParams.yWidth * mARCamera.videoParams.yHeight;
        mImage.y.data = Marshal.AllocHGlobal(yBufSize);
        mImage.y.width = (ulong)mARCamera.videoParams.yWidth;
        mImage.y.height = (ulong)mARCamera.videoParams.yHeight;
        mImage.y.stride = (ulong)mARCamera.videoParams.yWidth;

        // This does assume the YUV_NV21 format
        int vuBufSize = mARCamera.videoParams.yWidth * mARCamera.videoParams.yWidth / 2;
        mImage.vu.data = Marshal.AllocHGlobal(vuBufSize);
        mImage.vu.width = (ulong)mARCamera.videoParams.yWidth / 2;
        mImage.vu.height = (ulong)mARCamera.videoParams.yHeight / 2;
        mImage.vu.stride = (ulong)mARCamera.videoParams.yWidth;

        mSession.SetCapturePixelData(true, mImage.y.data, mImage.vu.data);
        Debug.Log("init buffer ftn");
    }
    private void ConfigureSession()
    {
#if !UNITY_EDITOR
		ARKitWorldTrackingSessionConfiguration config = new ARKitWorldTrackingSessionConfiguration ();

		if (UnityARSessionNativeInterface.IsARKit_1_5_Supported ()) {
			config.planeDetection = UnityARPlaneDetection.HorizontalAndVertical;
		} else {
			config.planeDetection = UnityARPlaneDetection.Horizontal;
		}

		config.alignment = UnityARAlignment.UnityARAlignmentGravity;
		config.getPointCloudData = true;
		config.enableLightEstimation = true;
		mSession.RunWithConfig (config);
#endif
    }
    void StartSavingMap()
    {
        ConfigureSession();

        if (!LibPlacenote.Instance.Initialized())
        {
            Debug.Log("SDK not yet initialized");
            return;
        }

        Debug.Log("Started Session");
        LibPlacenote.Instance.StartSession();

        if (mReportDebug)
        {
            LibPlacenote.Instance.StartRecordDataset(
                (completed, faulted, percentage) => {
                    if (completed)
                    {
                        Debug.Log("Dataset Upload Complete");
                    }
                    else if (faulted)
                    {
                        Debug.Log("Dataset Upload Faulted");
                    }
                    else
                    {
                        Debug.Log("Dataset Upload: (" + percentage.ToString("F2") + "/1.0)");
                    }
                });
            Debug.Log("Started Debug Report");
        }
        Debug.Log("saving map");
    }
    void Update()
    {
        if (mFrameUpdated)
        {
            Debug.Log("updatefirst if");

            mFrameUpdated = false;
            if (mImage == null)
            {
                InitARFrameBuffer();
            }

            if (mARCamera.trackingState == ARTrackingState.ARTrackingStateNotAvailable)
            {
                // ARKit pose is not yet initialized
                return;
            }
            else if (!mARKitInit && LibPlacenote.Instance.Initialized())
            {
                mARKitInit = true;
                Debug.Log("ARKit + placenote Initialized");
                StartSavingMap();
            }

            Matrix4x4 matrix = mSession.GetCameraPose();

            Vector3 arkitPosition = PNUtility.MatrixOps.GetPosition(matrix);
            Quaternion arkitQuat = PNUtility.MatrixOps.GetRotation(matrix);

            LibPlacenote.Instance.SendARFrame(mImage, arkitPosition, arkitQuat, mARCamera.videoParams.screenOrientation);

            if (shouldRecordWaypoints)
            {
                Transform player = Camera.main.transform;
                //create waypoints if there are none around
                Collider[] hitColliders = Physics.OverlapSphere(player.position, 1f);
                int i = 0;
                while (i < hitColliders.Length)
                {
                    if (hitColliders[i].CompareTag("waypoint"))
                    {
                        return;
                    }
                    i++;

                }
                Vector3 pos = player.position;
                Debug.Log(player.position);
                pos.y = -.5f;
                shapeManager.AddShape(pos, Quaternion.Euler(Vector3.zero), false);///////////adding shape
                Debug.Log("update last line");

                   
            }
        }
    }
    public void  builtinsceanload() {
        Application.LoadLevel(1);
    }
}