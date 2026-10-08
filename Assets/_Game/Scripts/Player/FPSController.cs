using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace ProjectElimination.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FPSController : MonoBehaviour
    {
        public float HorizontalSpeed { get; private set; }
        public float WalkSpeed => walkSpeed;
        public bool IsMoving => HorizontalSpeed > 0.05f;
        public bool IsSprinting { get; private set; }
        public bool IsGrounded { get; private set; }

        [Header("References")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private Transform cameraTransform;

        [Header("Movement")]
        [SerializeField, Min(0f)] private float walkSpeed = 5f;
        [SerializeField, Min(0f)] private float sprintSpeed = 8f;
        [SerializeField, Min(0f)] private float jumpHeight = 1.5f;
        [SerializeField, Min(0.01f)] private float gravity = 20f;

        [Header("Look")]
        [Tooltip("Degrees per pixel for mouse and touch pointer delta.")]
        [SerializeField, Min(0f)] private float pointerSensitivity = 0.1f;
        [Tooltip("Degrees per second for stick input at full deflection.")]
        [SerializeField, Min(0f)] private float stickSensitivity = 180f;
        [SerializeField, Range(0f, 89f)] private float verticalLookLimit = 85f;

        private CharacterController controller;
        private InputActionAsset runtimeActions;
        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction sprintAction;
        private InputAction jumpAction;
        private float verticalVelocity;
        private float pitch;
        private Quaternion cameraBaseRotation;
        private bool cursorCaptured;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (inputActions == null || cameraTransform == null ||
                cameraTransform == transform || !cameraTransform.IsChildOf(transform))
            {
                Debug.LogError("FPSController requires an Input Actions asset and a camera beneath the Player.", this);
                enabled = false;
                return;
            }

            // Each controller owns its actions without changing the shared asset.
            runtimeActions = Instantiate(inputActions);
            playerMap = runtimeActions.FindActionMap("Player");
            moveAction = playerMap?.FindAction("Move");
            lookAction = playerMap?.FindAction("Look");
            sprintAction = playerMap?.FindAction("Sprint");
            jumpAction = playerMap?.FindAction("Jump");
            if (moveAction == null || lookAction == null || sprintAction == null || jumpAction == null)
            {
                Debug.LogError("FPSController requires Player/Move, Look, Sprint and Jump actions.", this);
                enabled = false;
                return;
            }

            cameraBaseRotation = cameraTransform.localRotation;
        }

        private void OnEnable()
        {
            if (playerMap == null) return;
            playerMap.Enable();
            SetCursorCaptured(true);
        }

        private void OnDisable()
        {
            playerMap?.Disable();
            verticalVelocity = 0f;
            HorizontalSpeed = 0f;
            IsSprinting = false;
            IsGrounded = false;
            SetCursorCaptured(false);
        }

        private void OnDestroy()
        {
            if (runtimeActions != null) Destroy(runtimeActions);
        }

        private void Update()
        {
            UpdateCursor();
            bool acceptsInput = cursorCaptured && Application.isFocused;
            Vector2 movement = acceptsInput ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            bool sprint = acceptsInput && sprintAction.IsPressed();
            bool jump = acceptsInput && jumpAction.WasPressedThisFrame();

            if (acceptsInput)
            {
                Vector2 look = lookAction.ReadValue<Vector2>();
                // Pointer delta already measures displacement this frame. Sticks measure a rate.
                bool isPointerDelta = lookAction.activeControl is DeltaControl;
                Vector2 degrees = look * (isPointerDelta
                    ? pointerSensitivity
                    : stickSensitivity * Time.deltaTime);
                ApplyLook(degrees);
            }

            Move(movement, sprint, jump, Time.deltaTime);
        }

        private void ApplyLook(Vector2 degrees)
        {
            transform.Rotate(0f, degrees.x, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch - degrees.y, -verticalLookLimit, verticalLookLimit);
            cameraTransform.localRotation = cameraBaseRotation * Quaternion.Euler(pitch, 0f, 0f);
        }

        private void Move(Vector2 input, bool sprint, bool jump, float deltaTime)
        {
            bool grounded = controller.isGrounded;
            if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;
            if (grounded && jump) verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);

            Vector2 direction = Vector2.ClampMagnitude(input, 1f);
            Vector3 velocity = (transform.right * direction.x + transform.forward * direction.y)
                * (sprint ? sprintSpeed : walkSpeed);
            float displacementY = verticalVelocity * deltaTime - 0.5f * gravity * deltaTime * deltaTime;
            verticalVelocity -= gravity * deltaTime;
            Vector3 previousPosition = transform.position;
            CollisionFlags collisions = controller.Move(velocity * deltaTime + Vector3.up * displacementY);
            Vector3 displacement = transform.position - previousPosition;
            displacement.y = 0f;
            HorizontalSpeed = deltaTime > 0f ? displacement.magnitude / deltaTime : 0f;
            IsGrounded = controller.isGrounded;
            IsSprinting = sprint && IsMoving && IsGrounded;
            if ((collisions & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;
            if ((collisions & CollisionFlags.Below) != 0 && verticalVelocity < 0f) verticalVelocity = -2f;
        }

        private void UpdateCursor()
        {
            if (!Application.isFocused) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorCaptured(false);
            else if (!cursorCaptured && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                SetCursorCaptured(true);
            else if (cursorCaptured && Cursor.lockState != CursorLockMode.Locked)
                SetCursorCaptured(false);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) SetCursorCaptured(false);
        }

        private void SetCursorCaptured(bool captured)
        {
            cursorCaptured = captured;
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
