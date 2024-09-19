using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class PathScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public Transform GetWaypoint(int waypointIndex)
    {
        return transform.GetChild(waypointIndex);
    }



    private void OnDrawGizmos() 
    {
        for (int waypointIndex = 0; waypointIndex < transform.childCount; waypointIndex++)
        {
            var waypoint = GetWaypoint(waypointIndex);

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(waypoint.position, 0.2f);

            int nextWaypointIndex = waypointIndex + 1;

            if (nextWaypointIndex >= transform.childCount) {
                break;
            }

            var nextWaypoint = GetWaypoint(nextWaypointIndex);

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(waypoint.position, nextWaypoint.position);
        }

        var firstWaypoint = GetWaypoint(0);
        var lastWaypoint = GetWaypoint(transform.childCount - 1);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(firstWaypoint.position, lastWaypoint.position);
    }
}
