using GazeTracker;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR.Features.Interactions;

public class SimpleCameraMovement : MonoBehaviour
{
    public float movementSpeed = 8f;
    public bool autoMovement = false;
    public GameObject movementObject;
    public GameObject cameraObject;

    private Vector3 _moveDirection = new Vector3(0, 0, 0);

    void Start()
    {
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.depthTextureMode |= (DepthTextureMode.MotionVectors | DepthTextureMode.Depth);
    }


    void Update()
    {
        // Debug.Log("fps: " + 1 / Time.deltaTime);

        /*
         * handle movement
         */
        bool keyPressed = true;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S))
        {
            _moveDirection = movementObject.transform.forward;
        }
        else if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.A))
        {
            _moveDirection = movementObject.transform.right;
        }
        else if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Q))
        {
            _moveDirection = movementObject.transform.up;
        }
        else
        {
            keyPressed = false;
        }

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q))
        {
            _moveDirection *= -1;
        }

        if (!keyPressed && !autoMovement)
        {
            _moveDirection *= 0;
        }

        float playerSpeed = movementSpeed * Time.deltaTime;
        if (_moveDirection.magnitude > 0)
        {
            movementObject.transform.Translate(playerSpeed * _moveDirection, Space.World);
        }
        
        if(Input.GetKey(KeyCode.C))
        {
            EyeTracking.runCalibration();
        }
    }
}