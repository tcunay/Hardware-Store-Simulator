using System.Collections.Generic;
using UnityEngine;

#if (GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM) && ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

namespace Gley.UrbanSystem
{
    [System.Serializable]
    public class AxleInfo
    {
        public WheelCollider leftWheel;
        public WheelCollider rightWheel;
        public bool motor;
        public bool steering;
    }

    public class PlayerCar : MonoBehaviour
    {
        public List<AxleInfo> axleInfos;
        public Transform centerOfMass;
        public float maxMotorTorque;
        public float maxSteeringAngle;

        private IVehicleLightsComponent lightsComponent;
        private bool mainLights;
        private bool brake;
        private bool reverse;
        private bool blinkLeft;
        private bool blinkRight;
        private float realtimeSinceStartup;
        private Rigidbody rb;
        private IUIInput inputScript;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            rb.centerOfMass = centerOfMass.localPosition;

#if ENABLE_LEGACY_INPUT_MANAGER
            inputScript = gameObject.AddComponent<UIInputOld>().Initialize();
#else
            inputScript = gameObject.AddComponent<UIInputNew>().Initialize();
#endif

            lightsComponent = gameObject.GetComponent<IVehicleLightsComponent>();
            lightsComponent.Initialize();
        }

        public void ApplyLocalPositionToVisuals(WheelCollider collider)
        {
            if (collider.transform.childCount == 0)
                return;

            Transform visualWheel = collider.transform.GetChild(0);
            collider.GetWorldPose(out Vector3 position, out Quaternion rotation);
            visualWheel.position = position;
            visualWheel.rotation = rotation;
        }

        public void FixedUpdate()
        {
            float motor = maxMotorTorque * inputScript.GetVerticalInput();
            float steering = maxSteeringAngle * inputScript.GetHorizontalInput();

#if UNITY_6000_0_OR_NEWER
            var velocity = rb.linearVelocity;
#else
            var velocity = rb.velocity;
#endif

            float localVelocity = transform.InverseTransformDirection(velocity).z + 0.1f;
            reverse = false;
            brake = false;

            if (localVelocity < 0)
            {
                reverse = true;
            }

            if (motor < 0 && localVelocity > 0)
            {
                brake = true;
            }
            else if (motor > 0 && localVelocity < 0)
            {
                brake = true;
            }

            foreach (AxleInfo axleInfo in axleInfos)
            {
                if (axleInfo.steering)
                {
                    axleInfo.leftWheel.steerAngle = steering;
                    axleInfo.rightWheel.steerAngle = steering;
                }
                if (axleInfo.motor)
                {
                    axleInfo.leftWheel.motorTorque = motor;
                    axleInfo.rightWheel.motorTorque = motor;
                }
                ApplyLocalPositionToVisuals(axleInfo.leftWheel);
                ApplyLocalPositionToVisuals(axleInfo.rightWheel);
            }
        }

        private void Update()
        {
            realtimeSinceStartup += Time.deltaTime;

            if (GetKeyDown(KeyCode.Space))
            {
                mainLights = !mainLights;
                lightsComponent.SetMainLights(mainLights);
            }

            if (GetKeyDown(KeyCode.Q))
            {
                blinkLeft = !blinkLeft;
                if (blinkLeft)
                {
                    blinkRight = false;
                    lightsComponent.SetBlinker(BlinkType.Left);
                }
                else
                {
                    lightsComponent.SetBlinker(BlinkType.Stop);
                }
            }

            if (GetKeyDown(KeyCode.E))
            {
                blinkRight = !blinkRight;
                if (blinkRight)
                {
                    blinkLeft = false;
                    lightsComponent.SetBlinker(BlinkType.Right);
                }
                else
                {
                    lightsComponent.SetBlinker(BlinkType.Stop);
                }
            }

            lightsComponent.SetBrakeLights(brake);
            lightsComponent.SetReverseLights(reverse);
            lightsComponent.UpdateLights(realtimeSinceStartup);
        }

        private bool GetKeyDown(KeyCode key)
        {
#if (GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM) && ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            if (Keyboard.current == null)
                return false;

            return key switch
            {
                KeyCode.Space => Keyboard.current.spaceKey.wasPressedThisFrame,
                KeyCode.Q     => Keyboard.current.qKey.wasPressedThisFrame,
                KeyCode.E     => Keyboard.current.eKey.wasPressedThisFrame,
                _             => false,
            };
#else
            return Input.GetKeyDown(key);
#endif
        }
    }
}