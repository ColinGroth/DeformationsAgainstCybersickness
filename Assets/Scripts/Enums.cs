using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ViewDirection
{
    AtWaypoint,
    LeftGlobal,
    RightGlobal,
    StraightGlobal,
    LeftToMoveDir,
    RightToMoveDir,
    KeepRotation
}

public enum WaypointAction
{
    None,
    RotateHorizontal,
    RotateVertical,
    UpDown
}

public enum PlayerState
{
    Moving,
    Rotation,
}
