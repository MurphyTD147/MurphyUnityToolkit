using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider))]
public class PlayerShipController : MonoBehaviour
{
    [Header("Movement Parameters")]
    public float idleSpeed = 5f;                 // speed when there's no forward/back input
    public float forwardSpeed = 20f;             // forward movement speed
    public float backwardSpeed = 10f;            // backward movement speed (negative direction)
    public float accelerationRate = 8f;          // acceleration rate when input applied
    public float decelerationRate = 5f;          // deceleration rate when input released
    private float currentSpeed;                  // runtime computed current speed

    [Header("Rotation Parameters")]
    public float rotationSpeed = 100f;           // base rotation sensitivity (yaw/pitch)
    public float rollSpeed = 75f;                // roll coefficient (used to compute target roll)
    public float mouseSmoothingFactor = 2f;      // smoothing factor for look input

    [Header("Nitro Fuel")]
    public float nitroMax = 100f;                // maximum nitro capacity
    public float nitroCurrent = 100f;            // current nitro amount
    public float nitroDrainRate = 20f;           // nitro consumption per second
    public float nitroBoost = 150f;              // target speed while nitro is active
    [HideInInspector] public bool nitroActive;   // nitro active flag, exposed for external controllers

    [Header("Turret / Camera")]
    public Transform turretGun;                  // turret root used for aiming
    public Camera mainCamera;                    // camera used for raycasting to cursor
    public float turretRotationSpeed = 50f;      // turret rotation speed
    public float turretPitchMin = -40f;          // minimum turret pitch (degrees)
    public float turretPitchMax = 0f;            // maximum turret pitch (degrees)

    [Header("Firing Parameters")]
    public GameObject projectilePrefab;          // projectile prefab to spawn
    public Transform firePoint;                  // spawn point for projectiles
    public float firingForce = 800f;             // impulse applied to projectile
    public float spawnOffset = 1.5f;             // forward offset from firePoint for spawn position

    [Header("Weapon Overheat")]
    public float currentHeat = 0f;               // current weapon heat
    public float heatPerShot = 20f;              // heat added per shot
    public float maxHeat = 100f;                 // maximum heat capacity
    public float coolDownRate = 15f;             // passive cooldown per second
    public float overheatDelay = 2f;             // delay before passive cooling starts
    private float overheatTimer;                 // internal timer for cooling delay
    private bool isOverheated;                   // when true, firing is blocked

    [Header("References")]
    public ShipHealth shipHealth;                // optional shared health component
    public NitroController nitroController;      // optional visual controller for nitro particles

    private InputSystem_Actions inputActions;    // generated InputActions wrapper
    [HideInInspector] public Vector2 moveInput;  // movement input (X: strafe, Y: forward/back) — exposed for NitroController
    private Vector2 lookInput;                   // raw look input (mouse / right stick)
    private Vector2 smoothedLookInput;           // smoothed look input for stable rotation
    private float rollInput;                     // roll input value
    private bool upDownInput;                    // vertical ascend/descend input

    void Awake()
    {
        inputActions = new InputSystem_Actions();
    }

    void OnEnable() => inputActions.Enable();
    void OnDisable() => inputActions.Disable();

    void Start()
    {
        currentSpeed = idleSpeed;
        smoothedLookInput = Vector2.zero;
        Cursor.lockState = CursorLockMode.Locked;

        if (!shipHealth) shipHealth = GetComponent<ShipHealth>();
        if (!nitroController) nitroController = GetComponentInChildren<NitroController>();
    }

    void Update()
    {
        ReadInput();

        bool nitroKey = inputActions.Player.Nitro.IsPressed();
        ProcessNitro(nitroKey);            // update nitroActive flag and drain resource

        HandleMovement();                  // move ship based on currentSpeed
        HandleRotation();                  // apply pitch/yaw/roll

        if (inputActions.Player.Fire.WasPressedThisFrame())
            Fire();                        // spawn projectile if weapon not overheated

        ProcessCooling();                  // passive weapon cooling and overheat reset
        HandleTurretControl();             // aim turret using camera raycast

        if (nitroController)
            nitroController.shipController = this; // provide reference for nitro visuals
    }

    // Read inputs from Input System
    void ReadInput()
    {
        moveInput = inputActions.Player.Move.ReadValue<Vector2>();
        lookInput = inputActions.Player.Look.ReadValue<Vector2>();
        rollInput = inputActions.Player.Roll.ReadValue<float>();
        upDownInput = inputActions.Player.UpDown.IsPressed();
    }

    // Forward/back movement with acceleration and deceleration
    void HandleMovement()
    {
        float targetSpeed = idleSpeed;

        if (nitroActive)
            targetSpeed = nitroBoost;
        else if (moveInput.y > 0.1f)
            targetSpeed = forwardSpeed;
        else if (moveInput.y < -0.1f)
            targetSpeed = -backwardSpeed;

        float rate = (Mathf.Abs(moveInput.y) > 0.1f && !nitroActive) ? accelerationRate : decelerationRate;
        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * Time.deltaTime);

        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime, Space.Self);

        if (upDownInput)
            transform.Translate(Vector3.up * forwardSpeed * Time.deltaTime, Space.World);
    }

    // Rotation handling: smoothed look input plus roll for maneuver feel
    void HandleRotation()
    {
        smoothedLookInput = Vector2.Lerp(smoothedLookInput, lookInput, Time.deltaTime * mouseSmoothingFactor);

        Vector3 e = transform.localEulerAngles;
        float pitch = e.x > 180f ? e.x - 360f : e.x;
        float yaw = e.y;
        float roll = e.z > 180f ? e.z - 360f : e.z;

        float dYaw = (smoothedLookInput.x + moveInput.x) * rotationSpeed * Time.deltaTime;
        float dPitch = -smoothedLookInput.y * rotationSpeed * Time.deltaTime * 1.5f;

        float targetRoll = Mathf.Abs(rollInput) > 0.01f ? rollInput * 45f : 0f;
        float smoothedRoll = Mathf.LerpAngle(roll, targetRoll, Time.deltaTime * 3f);

        transform.localEulerAngles = new Vector3(
            Mathf.LerpAngle(pitch, pitch + dPitch, Time.deltaTime * 3f),
            Mathf.LerpAngle(yaw, yaw + dYaw, Time.deltaTime * 3f),
            smoothedRoll
        );
    }

    // Spawn projectile with physics; prevent self-collision; handle heat
    void Fire()
    {
        if (isOverheated) return;
        if (currentHeat + heatPerShot > maxHeat)
        {
            isOverheated = true;
            return;
        }

        if (firePoint == null || projectilePrefab == null) return;

        Vector3 spawnPos = firePoint.position + firePoint.forward * spawnOffset;
        GameObject proj = Instantiate(projectilePrefab, spawnPos, firePoint.rotation);

        if (proj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.mass = 0.1f;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.useGravity = false;
            rb.AddForce(firePoint.forward * firingForce, ForceMode.Impulse);
        }

        if (proj.TryGetComponent<Collider>(out var bulletCol) && TryGetComponent<Collider>(out var shipCol))
            Physics.IgnoreCollision(bulletCol, shipCol);

        currentHeat += heatPerShot;
        overheatTimer = 0f;
    }

    // Passive cooling logic and overheat state reset
    void ProcessCooling()
    {
        overheatTimer += Time.deltaTime;
        if (overheatTimer >= overheatDelay && currentHeat > 0f)
            currentHeat = Mathf.Max(0f, currentHeat - coolDownRate * Time.deltaTime);

        if (isOverheated && currentHeat < maxHeat)
            isOverheated = false;
    }

    // Update nitro active flag and drain nitro resource
    void ProcessNitro(bool nitroKey)
    {
        nitroActive = nitroKey && nitroCurrent > 0f;
        if (nitroActive)
            nitroCurrent = Mathf.Max(0f, nitroCurrent - nitroDrainRate * Time.deltaTime);
    }

    // Aim turret by raycasting from camera through cursor to world point
    void HandleTurretControl()
    {
        if (turretGun == null || mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out var hit, 200f))
        {
            Vector3 dir = (hit.point - turretGun.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(dir);
            Vector3 e = targetRot.eulerAngles;
            float pitch = e.x > 180f ? e.x - 360f : e.x;
            pitch = Mathf.Clamp(pitch, turretPitchMin, turretPitchMax);
            Quaternion limited = Quaternion.Euler(pitch, targetRot.eulerAngles.y, 0f);
            turretGun.rotation = Quaternion.RotateTowards(turretGun.rotation, limited, turretRotationSpeed * Time.deltaTime);
        }
    }
}
