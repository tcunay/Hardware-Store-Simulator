using UnityEngine;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER && (GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM)
using UnityEngine.InputSystem;
#endif

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR && (GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM)
using UnityEngine.SceneManagement;
#endif

namespace Gley.UrbanSystem
{
    public class UIInputNew : MonoBehaviour, IUIInput
    {
        private float horizontalInput;
        private float verticalInput;

#if (GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM)

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        public delegate void ButtonDown(string button);
        public static event ButtonDown onButtonDown;
        public static void TriggerButtonDownEvent(string button) => onButtonDown?.Invoke(button);

        public delegate void ButtonUp(string button);
        public static event ButtonUp onButtonUp;
        public static void TriggerButtonUpEvent(string button) => onButtonUp?.Invoke(button);

        private bool left, right, up, down;

#elif ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        private InputAction _moveAction;
#endif

#endif // GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM

        public UIInputNew Initialize()
        {
#if GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            onButtonDown += PointerDown;
            onButtonUp += PointerUp;
#else
            GameObject steeringUI = GameObject.Find("SteeringUI");
            if (steeringUI)
            {
                steeringUI.SetActive(false);
            }

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            SetupDesktopInput();
#endif
#endif

#endif // GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM
            return this;
        }

#if (GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM) && ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER && (!(UNITY_ANDROID || UNITY_IOS) || UNITY_EDITOR)
        private void SetupDesktopInput()
        {
            _moveAction = new InputAction("Move", InputActionType.Value);

            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");

            _moveAction.AddBinding("<Gamepad>/leftStick");
            _moveAction.Enable();
        }
#endif

        public float GetHorizontalInput() => horizontalInput;
        public float GetVerticalInput() => verticalInput;

        private void Update()
        {
#if GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (left)
                horizontalInput -= Time.deltaTime;
            else if (right)
                horizontalInput += Time.deltaTime;
            else
                horizontalInput = Mathf.MoveTowards(horizontalInput, 0, 5 * Time.deltaTime);

            horizontalInput = Mathf.Clamp(horizontalInput, -1f, 1f);

            if (up)
                verticalInput += Time.deltaTime;
            else if (down)
                verticalInput -= Time.deltaTime;
            else
                verticalInput = 0;

            verticalInput = Mathf.Clamp(verticalInput, -1f, 1f);

#elif ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            Vector2 input = _moveAction.ReadValue<Vector2>();
            horizontalInput = input.x;
            verticalInput = input.y;
#endif

#endif // GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM
        }

#if (GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM) && (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        private void PointerDown(string name)
        {
            if (name == "Restart")
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            if (name == "Left") { left = true; right = false; }
            if (name == "Right") { right = true; left = false; }
            if (name == "Up") { up = true; down = false; }
            if (name == "Down") { down = true; up = false; }
        }

        private void PointerUp(string name)
        {
            if (name == "Left") left = false;
            if (name == "Right") right = false;
            if (name == "Up") up = false;
            if (name == "Down") down = false;
        }
#endif

        private void OnDestroy()
        {
#if GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            onButtonDown -= PointerDown;
            onButtonUp -= PointerUp;
#elif ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            _moveAction?.Disable();
#endif

#endif // GLEY_PEDESTRIAN_SYSTEM || GLEY_TRAFFIC_SYSTEM
        }
    }
}