using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class WaypointData : MonoBehaviour
{

    public bool fixedSpeed = false;
    public ViewDirection viewDirection = ViewDirection.AtWaypoint;
    public WaypointAction waypointAction = WaypointAction.None;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    /*void Update()
    {
        
    }*/
}
