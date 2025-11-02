using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ShipHealth : MonoBehaviour
{
    [Header("Health Parameters")]
    public float maxHealth = 100f;        // maximum health value
    public float currentHealth;           // current health value at runtime

    [Header("Destruction Effects")]
    public GameObject explosionPrefab;    // prefab spawned on destruction (explosion effect)
    public GameObject smokePrefab;        // prefab spawned on destruction (smoke effect)

    [Header("Debug")]
    public bool verboseLogging = false;   // enable detailed debug logs for health events

    // Events for subscribers (UI, managers, etc.)
    public event System.Action<float, float> OnHealthChanged; // invoked when health changes (current, max)
    public event System.Action OnDeath;                       // invoked when object is destroyed

    // References set externally (e.g. by a manager)
    [HideInInspector] public Image healthBarFill;             // UI health bar fill reference
    [HideInInspector] public GameOverManager gameOverManager; // reference to game over manager

    /// <summary>
    /// Assigns the UI health bar reference.
    /// </summary>
    public void SetHealthBarUI(Image uiImage)
    {
        healthBarFill = uiImage;
        UpdateHealthUI();
    }

    /// <summary>
    /// Assigns the GameOverManager reference.
    /// </summary>
    public void SetGameOverManager(GameOverManager manager)
    {
        gameOverManager = manager;
    }

    /// <summary>
    /// Initializes health on start.
    /// </summary>
    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Log($"ShipHealth initialized: {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// Applies damage and checks for destruction.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (damage <= 0f || currentHealth <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        UpdateHealthUI();
        Log($"Damage: {damage}, health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0f)
            ShipDestroyed();
    }

    /// <summary>
    /// Restores health (e.g. repair kit).
    /// </summary>
    public void UseRepairKit(float repairAmount)
    {
        if (repairAmount <= 0f || currentHealth <= 0f) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + repairAmount);
        UpdateHealthUI();
        Log($"Repair: +{repairAmount}, health: {currentHealth}/{maxHealth}");
    }

    /// <summary>
    /// Updates the UI health bar and triggers the event.
    /// </summary>
    void UpdateHealthUI()
    {
        if (healthBarFill)
            healthBarFill.fillAmount = currentHealth / maxHealth;

        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Handles ship destruction: physics stop, effects, events, GameOver.
    /// </summary>
    void ShipDestroyed()
    {
        Log($"Ship destroyed: {gameObject.name}");

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
        }

        if (explosionPrefab)
            Instantiate(explosionPrefab, transform.position, transform.rotation);

        if (smokePrefab)
            StartCoroutine(SpawnSmoke());

        OnDeath?.Invoke();

        if (gameOverManager)
            gameOverManager.ShowGameOver();

        Destroy(gameObject);
    }

    /// <summary>
    /// Spawns smoke after a short delay.
    /// </summary>
    IEnumerator SpawnSmoke()
    {
        yield return new WaitForSeconds(0.5f);
        Instantiate(smokePrefab, transform.position, Quaternion.identity);
    }

    /// <summary>
    /// Logs a message if verbose logging is enabled.
    /// </summary>
    void Log(string msg)
    {
        if (verboseLogging) Debug.Log(msg, this);
    }
}
