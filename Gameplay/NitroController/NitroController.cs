using UnityEngine;

public class NitroController : MonoBehaviour
{
    [Header("Particle References")]
    public ParticleSystem nitroRearLeft;   // rear-left nitro particle effect
    public ParticleSystem nitroRearRight;  // rear-right nitro particle effect
    public ParticleSystem nitroFrontLeft;  // front-left nitro particle effect (used when moving backward)
    public ParticleSystem nitroFrontRight; // front-right nitro particle effect (used when moving backward)

    // Reference to the ship controller (player or other controller exposing nitroActive and moveInput)
    public PlayerShipController shipController;

    void Update()
    {
        if (shipController == null) return;

        bool nitroActive = shipController.nitroActive;
        bool movingForward = shipController.moveInput.y > 0f;
        bool movingBackward = shipController.moveInput.y < 0f;

        if (nitroActive)
            ApplyNitroEffects(movingForward, movingBackward);
        else
            StopNitroEffects();
    }

    // Play appropriate particle sets depending on movement direction
    void ApplyNitroEffects(bool movingForward, bool movingBackward)
    {
        if (movingForward)
        {
            PlayParticles(nitroRearLeft);
            PlayParticles(nitroRearRight);
            StopParticles(nitroFrontLeft);
            StopParticles(nitroFrontRight);
        }
        else if (movingBackward)
        {
            PlayParticles(nitroFrontLeft);
            PlayParticles(nitroFrontRight);
            StopParticles(nitroRearLeft);
            StopParticles(nitroRearRight);
        }
        else
        {
            StopNitroEffects();
        }
    }

    // Safe play / stop helpers
    void PlayParticles(ParticleSystem ps)
    {
        if (ps != null && !ps.isPlaying) ps.Play();
    }

    void StopParticles(ParticleSystem ps)
    {
        if (ps != null && ps.isPlaying) ps.Stop();
    }

    // Stop all nitro particle systems
    void StopNitroEffects()
    {
        StopParticles(nitroRearLeft);
        StopParticles(nitroRearRight);
        StopParticles(nitroFrontLeft);
        StopParticles(nitroFrontRight);
    }
}
