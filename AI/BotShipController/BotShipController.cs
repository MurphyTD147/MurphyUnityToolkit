#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public enum BotState
{
    Patrol,
    Chase,
    Attack,
    BreakOff,
    Evade
}

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class BotShipController : MonoBehaviour
{
    [Header("References")]
    public Transform firePoint;
    public GameObject projectilePrefab;
    public ParticleSystem nitroRearLeft, nitroRearRight, nitroFrontLeft, nitroFrontRight;

    [Header("Speeds")]
    public float baseSpeed = 20f;
    public float nitroSpeed = 60f;
    public float rotationSpeed = 180f;
    public float evadeSpeed = 40f;
    public float breakOffSpeed = 30f;

    [Header("Ranges")]
    public float detectionRange = 200f;
    public float chaseRange = 120f;
    public float desiredAttackRange = 80f;
    public float breakOffRange = 150f;

    [Header("Attack Strafing")]
    public float circleTime = 6f;

    [Header("Nitro")]
    public float nitroMax = 100f;
    public float nitroDrainRate = 40f;
    public float nitroRechargeRate = 30f;
    public float nitroRechargeDelay = 1f;

    [Header("Weapon & Overheat")]
    public float fireRate = 0.3f;
    public float firingForce = 800f;
    public float projectileOffset = 2f;
    public float heatPerShot = 15f;
    public float maxHeat = 100f;
    public float coolDownRate = 20f;
    public float overheatDelay = 0.5f;

    [Header("Evade")]
    public float evadeDuration = 2f;
    public float evadeRotationSpeed = 180f;

    [Header("Patrol")]
    public float patrolRadius = 50f;
    public float patrolThreshold = 5f;

    [Header("Attention")]
    [Tooltip("How fast the bot additionally turns to face the target during Attack.")]
    public float attentionRotationSpeed = 120f;

    [Header("Separation")]
    [Tooltip("Minimum distance from target to avoid ramming.")]
    public float minSeparationDistance = 25f;

    [Header("Hysteresis & Visibility")]
    [Tooltip("Tolerance added/subtracted to range thresholds to avoid state jitter.")]
    public float rangeTolerance = 5f;
    [Tooltip("LayerMask used for visibility raycasts.")]
    public LayerMask visibilityMask = ~0;

    [Header("Debug")]
    public bool verboseLogging = false;

    // Internal state
    private BotState state = BotState.Patrol;
    private Vector3 patrolTarget;
    private float circleTimer;
    private float fireTimer;
    private float currentHeat;
    private float overheatTimer;
    private bool isOverheated;
    private float nitroCurrent;
    private float nitroRechargeTimer;
    private bool nitroActive;
    private float evadeTimer;
    private float lastHealth;
    private Rigidbody rb;

    /// <summary>
    /// Initializes references and sets initial patrol target.
    /// </summary>
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        var sh = GetComponent<ShipHealth>();
        if (sh != null) lastHealth = sh.currentHealth;

        nitroCurrent = nitroMax;
        SetNewPatrolTarget();
    }

    /// <summary>
    /// Main update loop: timers, damage detection, target acquisition, state execution, attention, nitro, cooling.
    /// </summary>
    void Update()
    {
        fireTimer += Time.deltaTime;
        overheatTimer += Time.deltaTime;

        DetectDamage();

        Transform visibleTarget = GetVisiblePlayer();
        if (visibleTarget == null)
        {
            state = BotState.Patrol;
            Patrol();
            ProcessNitroRecharge();
            ProcessCooling();
            return;
        }

        Vector3 targetPos = visibleTarget.position;
        float dist = Vector3.Distance(Flat(transform.position), Flat(targetPos));

        StateTransitions(dist);

        switch (state)
        {
            case BotState.Patrol: Patrol(); break;
            case BotState.Chase: Chase(targetPos, dist); break;
            case BotState.Attack: Attack(targetPos); break;
            case BotState.BreakOff: BreakOff(targetPos); break;
            case BotState.Evade: Evade(targetPos); break;
        }

        if (state == BotState.Attack)
            ApplyAttention(targetPos);

        ProcessNitroRecharge();
        ProcessCooling();
    }

    /// <summary>
    /// Switches to Evade when health drops since last frame.
    /// </summary>
    void DetectDamage()
    {
        var sh = GetComponent<ShipHealth>();
        if (sh == null) return;

        if (sh.currentHealth < lastHealth)
        {
            state = BotState.Evade;
            evadeTimer = 0f;
        }
        lastHealth = sh.currentHealth;
    }

    /// <summary>
    /// Handles state transitions with hysteresis to avoid oscillation.
    /// </summary>
    void StateTransitions(float dist)
    {
        if (state == BotState.Evade) return;

        if (state == BotState.Attack && circleTimer >= circleTime)
            state = BotState.BreakOff;
        else if (state == BotState.BreakOff && dist >= breakOffRange + rangeTolerance)
            state = BotState.Chase;
        else if (dist > detectionRange + rangeTolerance)
            state = BotState.Patrol;
        else if (dist > chaseRange + rangeTolerance)
            state = BotState.Chase;
        else if (dist <= desiredAttackRange - rangeTolerance)
            state = BotState.Attack;
        else
            state = BotState.Chase;
    }

    /// <summary>
    /// Patrols within a radius, sets new targets when reaching threshold.
    /// </summary>
    void Patrol()
    {
        if (Vector3.Distance(transform.position, patrolTarget) < patrolThreshold)
            SetNewPatrolTarget();

        InstantTurnToward(patrolTarget, rotationSpeed);
        MoveForward(baseSpeed);
        UseNitro(false);
    }

    /// <summary>
    /// Generates a new random patrol target on XZ plane.
    /// </summary>
    void SetNewPatrolTarget()
    {
        Vector3 rnd = Random.insideUnitSphere;
        rnd.y = 0f;
        patrolTarget = transform.position + rnd.normalized * patrolRadius;
    }

    /// <summary>
    /// Chases the target, optionally using nitro; keeps separation if too close.
    /// </summary>
    void Chase(Vector3 targetPos, float dist)
    {
        circleTimer = 0f;
        bool needNitro = dist > chaseRange;
        UseNitro(needNitro);

        InstantTurnToward(targetPos, rotationSpeed);

        if (dist > desiredAttackRange)
        {
            MoveForward(nitroActive ? nitroSpeed : baseSpeed);
        }
        else if (dist < minSeparationDistance)
        {
            Vector3 retreatDir = (transform.position - targetPos).normalized;
            Vector3 retreatPoint = transform.position + retreatDir * 5f;
            InstantTurnToward(retreatPoint, rotationSpeed);
            MoveForward(baseSpeed * 0.5f);
        }
    }

    /// <summary>
    /// Performs circular strafing around the target and fires when ready; maintains separation.
    /// </summary>
    void Attack(Vector3 targetPos)
    {
        circleTimer += Time.deltaTime;
        UseNitro(false);

        float angle = circleTimer / circleTime * Mathf.PI * 2f;
        Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * desiredAttackRange;
        Vector3 aimPos = targetPos + offset;

        float dist = Vector3.Distance(Flat(transform.position), Flat(targetPos));
        if (dist < minSeparationDistance)
        {
            Vector3 retreatDir = (transform.position - targetPos).normalized;
            aimPos = transform.position + retreatDir * minSeparationDistance;
        }

        InstantTurnToward(aimPos, rotationSpeed);
        MoveForward(baseSpeed);

        if (fireTimer >= fireRate && !isOverheated)
        {
            Fire(targetPos);
            fireTimer = 0f;
        }
    }

    /// <summary>
    /// Breaks off from the target with nitro and random spread, then resumes chase.
    /// </summary>
    void BreakOff(Vector3 targetPos)
    {
        UseNitro(true);
        float spread = Random.Range(-20f, 20f);
        Vector3 dirAway = Quaternion.Euler(0f, spread, 0f) * (transform.position - targetPos).normalized;
        Vector3 dest = targetPos + dirAway * breakOffRange;

        InstantTurnToward(dest, rotationSpeed);
        MoveForward(breakOffSpeed);
    }

    /// <summary>
    /// Evades by turning away and moving fast with nitro for a duration, then returns to Chase.
    /// </summary>
    void Evade(Vector3 targetPos)
    {
        evadeTimer += Time.deltaTime;
        Vector3 dir = transform.position - targetPos;
        InstantTurnToward(transform.position + dir, evadeRotationSpeed);
        MoveForward(evadeSpeed);
        UseNitro(true);

        if (evadeTimer >= evadeDuration)
            state = BotState.Chase;
    }

    /// <summary>
    /// Fires a projectile towards the target direction, applies heat and collision filtering.
    /// </summary>
    void Fire(Vector3 targetPos)
    {
        if (currentHeat + heatPerShot > maxHeat)
        {
            isOverheated = true;
            return;
        }

        if (!firePoint || !projectilePrefab) return;

        Vector3 dir = (targetPos - firePoint.position).normalized;
        Vector3 spawnPos = firePoint.position + dir * projectileOffset;
        Quaternion spawnRot = Quaternion.LookRotation(dir);
        var proj = Instantiate(projectilePrefab, spawnPos, spawnRot);

        if (proj.TryGetComponent<Rigidbody>(out var prb))
        {
            prb.mass = 0.1f;
            prb.useGravity = false;
            prb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            prb.velocity = dir * firingForce;
        }

        if (proj.TryGetComponent<Collider>(out var bc) && TryGetComponent<Collider>(out var sc))
        {
            Physics.IgnoreCollision(bc, sc);
        }

        currentHeat += heatPerShot;
        overheatTimer = 0f;
    }

    /// <summary>
    /// Finds the nearest player with direct line of sight within detection range.
    /// </summary>
    Transform GetVisiblePlayer()
    {
        if (!firePoint) return null;

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Transform closest = null;
        float minDist = detectionRange;

        foreach (var p in players)
        {
            if (!p) continue;
            Vector3 dir = p.transform.position - firePoint.position;
            float d = dir.magnitude;
            if (d > detectionRange) continue;

            float angle = Vector3.Angle(firePoint.forward, dir.normalized);
            if (angle > 90f) continue;

            if (Physics.Raycast(firePoint.position, dir.normalized, out RaycastHit hit, d, visibilityMask, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform == p.transform || hit.transform.IsChildOf(p.transform))
                {
                    if (d < minDist)
                    {
                        closest = p.transform;
                        minDist = d;
                    }
                }
            }
        }

        return closest;
    }

    /// <summary>
    /// Cools weapon heat over time and resets overheated flag when below max.
    /// </summary>
    void ProcessCooling()
    {
        if (overheatTimer >= overheatDelay && currentHeat > 0f)
            currentHeat = Mathf.Max(0f, currentHeat - coolDownRate * Time.deltaTime);

        if (isOverheated && currentHeat < maxHeat)
            isOverheated = false;
    }

    /// <summary>
    /// Enables/disables nitro, plays particle effects, and manages nitro drain.
    /// </summary>
    void UseNitro(bool on)
    {
        if (!on && nitroActive) nitroRechargeTimer = nitroRechargeDelay;
        nitroActive = on;

        if (nitroActive && nitroCurrent > 0f)
        {
            nitroCurrent = Mathf.Max(0f, nitroCurrent - nitroDrainRate * Time.deltaTime);
            if (nitroRearLeft) nitroRearLeft.Play();
            if (nitroRearRight) nitroRearRight.Play();
            if (nitroFrontLeft) nitroFrontLeft.Stop();
            if (nitroFrontRight) nitroFrontRight.Stop();
        }
        else
        {
            if (nitroRearLeft) nitroRearLeft.Stop();
            if (nitroRearRight) nitroRearRight.Stop();
            if (nitroFrontLeft) nitroFrontLeft.Play();
            if (nitroFrontRight) nitroFrontRight.Play();
        }
    }

    /// <summary>
    /// Recharges nitro over time when nitro is inactive after a delay.
    /// </summary>
    void ProcessNitroRecharge()
    {
        if (nitroActive) return;
        nitroRechargeTimer -= Time.deltaTime;
        if (nitroRechargeTimer <= 0f && nitroCurrent < nitroMax)
            nitroCurrent = Mathf.Min(nitroMax, nitroCurrent + nitroRechargeRate * Time.deltaTime);
    }

    /// <summary>
    /// Instantly turns towards a point with rotation speed, using Rigidbody when available.
    /// </summary>
    void InstantTurnToward(Vector3 point, float speed)
    {
        Vector3 dir = point - transform.position;
        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion look = Quaternion.LookRotation(dir.normalized);
        if (rb)
            rb.MoveRotation(Quaternion.RotateTowards(transform.rotation, look, speed * Time.deltaTime));
        else
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, speed * Time.deltaTime);
    }

    /// <summary>
    /// Moves forward by speed using Rigidbody when available.
    /// </summary>
    void MoveForward(float spd)
    {
        if (rb)
            rb.MovePosition(rb.position + transform.forward * spd * Time.deltaTime);
        else
            transform.Translate(Vector3.forward * spd * Time.deltaTime, Space.Self);
    }

    /// <summary>
    /// Smoothly aligns the hull towards the player only during Attack.
    /// </summary>
    void ApplyAttention(Vector3 targetPos)
    {
        if (attentionRotationSpeed <= 0f) return;

        float dist = Vector3.Distance(Flat(transform.position), Flat(targetPos));
        if (dist > detectionRange) return;

        Vector3 dirToPlayer = (targetPos - transform.position).normalized;
        Quaternion look = Quaternion.LookRotation(dirToPlayer);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            look,
            attentionRotationSpeed * Time.deltaTime
        );
    }

    /// <summary>
    /// Logs a message if verbose logging is enabled.
    /// </summary>
    void Log(string msg)
    {
        if (verboseLogging) Debug.Log(msg, this);
    }

    /// <summary>
    /// Returns the XZ-flattened vector.
    /// </summary>
    Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

#if UNITY_EDITOR
    /// <summary>
    /// Draws gizmos for ranges and firePoint direction in the editor.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        Vector3 p = transform.position;

        Gizmos.color = new Color(0.4f, 0.6f, 1f); // Patrol radius
        Gizmos.DrawWireSphere(p, patrolRadius);
        Handles.Label(p + Vector3.left * patrolRadius, "Patrol Radius");

        Gizmos.color = Color.green; // Detection
        Gizmos.DrawWireSphere(p, detectionRange);
        Handles.Label(p + Vector3.left * detectionRange, "Detection Range");

        Gizmos.color = Color.yellow; // Chase
        Gizmos.DrawWireSphere(p, chaseRange);
        Handles.Label(p + Vector3.left * chaseRange, "Chase Range");

        Gizmos.color = new Color(1f, 0.4f, 0f); // BreakOff
        Gizmos.DrawWireSphere(p, breakOffRange);
        Handles.Label(p + Vector3.left * breakOffRange, "BreakOff Range");

        Gizmos.color = Color.red; // Attack
        Gizmos.DrawWireSphere(p, desiredAttackRange);
        Handles.Label(p + Vector3.left * desiredAttackRange * 0.5f, "Attack Radius");

        Gizmos.color = Color.white; // Min Separation
        Gizmos.DrawWireSphere(p, minSeparationDistance);
        Handles.Label(p + Vector3.left * minSeparationDistance * 0.5f, "Min Separation");

        if (firePoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(firePoint.position, firePoint.position + firePoint.forward * 10f);
            Handles.Label(firePoint.position + firePoint.forward * 10f, "Fire Point");
        }
    }

    /// <summary>
    /// Simple state debug label in the editor.
    /// </summary>
    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20), $"Bot State: {state}");
    }
#endif
}
