using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor.Rendering;
using UnityEngine;
using Debug = UnityEngine.Debug;
using GazeTracker;

public class WaypointMovement : MonoBehaviour
{
    [Header("Experiment Settings")]
    [SerializeField]
    private int id;
    public int counter = 0;
    private string filePath = Application.dataPath + "/../experimentData/";
    Stopwatch stopwatch = new Stopwatch();


    [Header("Path Movement Settings")]
    // current movement state
    public PlayerState state = PlayerState.Moving;
    // parent object containing the waypoints as childs in the scene
    public GameObject waypointParent;
    // list of waypoints
    public List<GameObject> waypoints;
    private List<WaypointData> waypointData;
    // index of next waypoint
    public int current = 0;
    // movement speed when using fixed speed
    public float speed = 2f;
    // speed when doing full 365 rotation at waypoint
    public float rotationSpeed = 40f;
    // rotation speed when moving from waypoint to waypoint 
    public float turnSpeed = 5;
    // minimum speed when using sin speed
    public float minSpeed = 1f;
    // maximum speed when using sin speed
    public float maxSpeed = 10f;
    // amplitude of sin curve
    public float amplitude = 100.0f;
    // frequency of sin curve
    public float frequency = 2.0f;
    public float rotationSmoothness = 5f;
    // toggle if sin speed or fixed speed should be used
    public bool fixedSpeed = false;
    // number of rounds around the path
    public int rounds = 1;

    private float accumulatedRotation = 0f;
    private float accuLookRotation = 0;
    
    private bool moving = false;

    private int completedRounds = 0;
    public float degreeRamping = 30.0f;
    public float degreeLookRamp = 10.0f;

    // Start is called before the first frame update
    void Start()
    {

        Stopwatch stopwatch = new Stopwatch();

        waypointData = new List<WaypointData>();

        if (waypointParent != null)
        {
            foreach (Transform child in waypointParent.transform)
            {
                waypoints.Add(child.gameObject);
                waypointData.Add(child.gameObject.GetComponent<WaypointData>());
            }
        }

        UnityEngine.Debug.Log(waypointData.Count);
        UnityEngine.Debug.Log(waypoints.Count);
        transform.position = waypoints[current].transform.position;
        increaseCurrent();
        Vector3 target = waypoints[current].transform.position;
        transform.LookAt(target);
    }

    void increaseCurrent()
    {
        current++;

        if (current >= waypoints.Count)
        {
            current = 0;
            completedRounds++;
            if (completedRounds >= rounds)
            {
                moving = false;
                stopwatch.Stop();
            }
        }
    }

    // Update is called once per frame
    void Update()
    {

        // Start Experiment with space
        if (!moving && Input.GetKeyDown(KeyCode.Space))
        {
            if (id != 0)
            {
                string fileName = DateTime.Now.ToString("yyyy-MM-dd-HH-mm") + "_" + id.ToString();
                filePath = Path.Combine(filePath, fileName + ".txt");
                UnityEngine.Debug.Log(filePath);
                if (!File.Exists(filePath))
                {
                    File.Create(filePath).Close();
                    UnityEngine.Debug.Log("Created file " + fileName);
                }
                moving = true;
                stopwatch.Start();
            }
            else
            {
                UnityEngine.Debug.LogError("Id is not set!");
            }
        }

        // Stop Experiment with S
        if (moving && Input.GetKeyDown(KeyCode.S))
        {
            moving = false;
            stopwatch.Stop();
            WriteDataToFile(BuildEarlyStopString());
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            EyeTracking.runCalibration();
        }
        
        if (moving)
        {

            if (Input.GetKeyDown(KeyCode.DownArrow))
            {
                counter++;
                WriteDataToFile(BuildCounterString());
            }

            if (Input.GetKeyDown(KeyCode.UpArrow))
            {
                counter--;
                WriteDataToFile(BuildCounterString());
            }

            if (state == PlayerState.Moving)
            {
                Vector3 target = waypoints[current].transform.position;
                Vector3 directionToTarget = target - transform.position;
                Vector3 newPos = Vector3.forward;

                if (waypointData[current].fixedSpeed)
                {
                    newPos = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);

                }
                else
                {
                    float time = Time.time;
                    float sinSpeed = Mathf.Clamp(amplitude * Mathf.Sin(frequency * time) + amplitude + minSpeed, minSpeed, maxSpeed);
                    //Debug.LogError(sinSpeed);
                    newPos = Vector3.MoveTowards(transform.position, target, sinSpeed * Time.deltaTime);
                }

                transform.position = newPos;
                //transform.LookAt(target);

                //Quaternion targetRotation = Quaternion.LookRotation(target - transform.position);
                //transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);

                Quaternion rotationTowardsTarget;

                switch (waypointData[current].viewDirection)
                {
                    case ViewDirection.AtWaypoint:
                        float step = turnSpeed * Time.deltaTime;
                        rotationTowardsTarget = Quaternion.LookRotation(directionToTarget);
                        float angleToTarget = Vector3.Angle(transform.forward, directionToTarget);
                        float angle = rotationSmoothness * Time.deltaTime *
                                      Mathf.Max(Mathf.Min(accuLookRotation / degreeLookRamp, angleToTarget / accuLookRotation, 1), 0.03f);
                        
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, rotationTowardsTarget, angle);
                        accuLookRotation += angle;
                        break;
                    case ViewDirection.LeftGlobal:
                        rotationTowardsTarget = Quaternion.LookRotation(Vector3.back);
                        transform.rotation = rotationTowardsTarget;
                        break;
                    case ViewDirection.RightGlobal:
                        rotationTowardsTarget = Quaternion.LookRotation(Vector3.forward);
                        transform.rotation = rotationTowardsTarget;
                        break;
                    case ViewDirection.StraightGlobal:
                        rotationTowardsTarget = Quaternion.LookRotation(Vector3.left);
                        transform.rotation = rotationTowardsTarget;
                        break;
                    case ViewDirection.LeftToMoveDir:
                        Quaternion rotationLeft = Quaternion.Euler(0, -90, 0);
                        rotationTowardsTarget = Quaternion.LookRotation(directionToTarget);
                        Quaternion finalRotationLeft = rotationLeft * rotationTowardsTarget;
                        transform.rotation = finalRotationLeft;
                        break;
                    case ViewDirection.RightToMoveDir:
                        Quaternion rotationRight = Quaternion.Euler(0, 90, 0);
                        rotationTowardsTarget = Quaternion.LookRotation(directionToTarget);
                        Quaternion finalRotationRight = rotationRight * rotationTowardsTarget;
                        transform.rotation = finalRotationRight;
                        break;
                    case ViewDirection.KeepRotation:
                        break;
                    default:
                        break;
                }

                float distance = Vector3.Distance(transform.position, target);
                if (distance <= 0.05)
                {
                    //Debug.LogError(waypoints[current].tag);
                    if (waypointData[current].waypointAction != WaypointAction.None && state == PlayerState.Moving)
                    //if (waypoints[current].tag == "Rotation" && state != PlayerState.Rotation)
                    {
                        //Debug.LogError("Rotating");
                        state = PlayerState.Rotation;

                        // float yRotation = transform.eulerAngles.y;
                        // Quaternion newYRotation = Quaternion.Euler(0f, yRotation, 0f);
                        // transform.rotation = newYRotation;

                        accumulatedRotation = 0f;
                    }
                    else
                    {
                        accuLookRotation = 0;
                        increaseCurrent();
                    }
                }
            }
            else if (state == PlayerState.Rotation)
            {
                float rotAngle = rotationSpeed * Time.deltaTime *
                                 Mathf.Max(Mathf.Min(accumulatedRotation / degreeRamping, 1), 0.15f) *
                                 Mathf.Max(Mathf.Min((365 - accumulatedRotation) / (degreeRamping * 1.5f), 1), 0.1f);
                switch (waypointData[current].waypointAction)
                {
                    case WaypointAction.RotateHorizontal:
                        transform.Rotate(Vector3.up, rotAngle);
                        break;
                    case WaypointAction.RotateVertical:
                        transform.Rotate(Vector3.right, rotAngle);
                        break;
                    default:
                        UnityEngine.Debug.Log("Unknown Waypoint Action");
                        break;
                }

                accumulatedRotation += rotAngle;

                if (accumulatedRotation >= 365f)
                {
                    state = PlayerState.Moving;
                    increaseCurrent();
                }
            }

        }

    }

    string BuildEarlyStopString()
    {
        string data = BuildCounterString();
        string dataToWrite = data + ", stopped manually";
        return dataToWrite;
    }

    string BuildCounterString()
    {
        long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
        string timestamp = elapsedMilliseconds.ToString();
        string currentWaypoint = current.ToString();
        string waypointAction = (state == PlayerState.Moving) ? PlayerState.Moving.ToString() : waypointData[current].waypointAction.ToString();
        string viewDirection = waypointData[current].viewDirection.ToString();
        string dataToWrite = $"Timer: {timestamp}, Counter: {counter}, Waypoint: {currentWaypoint}, Round: {completedRounds}, Action: {waypointAction}, View Direction: {viewDirection}";
        return dataToWrite;
    }

    void WriteDataToFile(string dataToWrite)
    {
        using (StreamWriter writer = File.AppendText(filePath))
        {
            writer.WriteLine(dataToWrite);
        }
        UnityEngine.Debug.Log("Data written to file: " + dataToWrite);
    }
}
