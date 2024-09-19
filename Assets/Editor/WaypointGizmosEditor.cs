using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Transform))]
public class WaypointGizmosEditor : Editor
{
    // Reference to the parent object whose children are waypoints
    public Transform parentObject;

    // This method is called when the Scene view is drawn
    private void OnSceneGUI()
    {
        if (parentObject != null)
        {
            foreach (Transform child in parentObject)
            {
                // Draw a wire sphere at each child waypoint's position with a radius of 0.5
                Handles.color = Color.green;
                Handles.DrawWireDisc(child.position, Vector3.up, 0.5f);
            }
        }
    }
}
