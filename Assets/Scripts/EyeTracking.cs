using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ViveSR.anipal.Eye;
using System.Runtime.InteropServices;


namespace GazeTracker
{
    public static class EyeTracking
    {
        private class eyeDataPoint
        {
            public Vector3 gazeDirection_right;
            public Vector3 gazeDirection_left;
            public float velocity;
            public float time;

            public eyeDataPoint(Vector3 gazeDirection_r, Vector3 gazeDirection_l, float velocity, float time)
            {
                this.gazeDirection_right = gazeDirection_r;
                this.gazeDirection_left = gazeDirection_l;
                this.velocity = velocity;
                this.time = time;
            }
        }

        public enum MovementType
        {
            None,
            Saccade,
            Blink
        }

        public static EyeData_v2 _eyeData;
        public static Vector3 _eyeGazeDirection_r = new Vector3(0, 0, 1);
        public static Vector3 _eyeGazeDirection_l = new Vector3(0, 0, 1);
        public static MovementType _currentMovement = MovementType.None;
        private static bool _eye_callback_registered;

        private static FixedSizeList<eyeDataPoint>
            eyeSpeeds = new FixedSizeList<eyeDataPoint>(10000); // degrees per second

        private static float alpha = 0.3f;
        private static float saccadeThreshold = 70f;

        public static void updateEyeData(float deltaTime)
        {
            // * set eye data
            if (SRanipal_Eye_Framework.Status == SRanipal_Eye_Framework.FrameworkStatus.WORKING)
            {
                if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && !_eye_callback_registered)
                {
                    SRanipal_Eye_v2.WrapperRegisterEyeDataCallback(
                        Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback));
                    _eye_callback_registered = true;
                }
                else if (!SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && _eye_callback_registered)
                {
                    SRanipal_Eye_v2.WrapperUnRegisterEyeDataCallback(
                        Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback));
                    _eye_callback_registered = false;
                }

                if (_eye_callback_registered)
                {
                    Vector3 oldGazeDirection_r = _eyeGazeDirection_r;
                    Vector3 oldGazeDirection_l = _eyeGazeDirection_l;
                    Vector3 gazeOriginCombinedLocal;
                    if (SRanipal_Eye_v2.GetGazeRay(GazeIndex.RIGHT, out gazeOriginCombinedLocal,
                            out _eyeGazeDirection_r, _eyeData))
                    {
                    }

                    if (SRanipal_Eye_v2.GetGazeRay(GazeIndex.LEFT, out gazeOriginCombinedLocal,
                            out _eyeGazeDirection_l, _eyeData))
                    {
                    }

                    _eyeGazeDirection_r = _eyeGazeDirection_r * alpha + oldGazeDirection_r * (1 - alpha);
                    _eyeGazeDirection_l = _eyeGazeDirection_l * alpha + oldGazeDirection_l * (1 - alpha);
                    float velocity = Vector3.Angle(oldGazeDirection_r, _eyeGazeDirection_r) / deltaTime;
                    // if (eyeSpeeds.Count > 0)
                    // {
                    //     velocity = eyeSpeeds.Get(0).velocity * (1 - alpha) + velocity * alpha;
                    // }
                    eyeSpeeds.Push(new eyeDataPoint(_eyeGazeDirection_r, _eyeGazeDirection_l, velocity, Time.time));
                    
                    if (_eyeData.verbose_data.left.eye_openness < .9f && _eyeData.verbose_data.right.eye_openness < .9f)
                        _currentMovement = MovementType.Blink;
                    else
                    {
                        if (eyeSpeeds.Get(0).velocity > saccadeThreshold)
                            _currentMovement = MovementType.Saccade;
                        else
                            _currentMovement = MovementType.None;
                    }
                }
            }
            else
            {
                Debug.LogError("Eye tracking is not working - Is the SRanipal Eye Framework included in the scene?");
            }
        }

        private static void EyeCallback(ref EyeData_v2 eye_data)
        {
            _eyeData = eye_data;
        }

        public static void writeVelocities()
        {
            using (StreamWriter writer = new StreamWriter("velocities.csv"))
            {
                for (int i = eyeSpeeds.Count - 1; i >= 0; i--)
                {
                    writer.WriteLine(eyeSpeeds.Get(i).time + "," + eyeSpeeds.Get(i).velocity);
                }
            }
        }

        public static void runCalibration()
        {
            bool success = SRanipal_Eye_v2.LaunchEyeCalibration();
            Debug.Log("Calibration successful: " + success);
        }
    }

    class FixedSizeList<T>
    {
        private List<T> list = new List<T>();
        private int maxSize;

        public FixedSizeList(int maxSize)
        {
            this.maxSize = maxSize;
        }

        public void Push(T item)
        {
            list.Add(item);
            if (list.Count > maxSize)
            {
                list.RemoveAt(0); // Remove the oldest item
            }
        }

        public T Get(int i)
        {
            return list[list.Count - 1 - i];
        }

        public int Count
        {
            get { return list.Count; }
        }
    }
}