# NitroController

Controls nitro particle effects for a ship, based on the current movement direction and nitro state provided by a `PlayerShipController`.

## ✨ Features
- Activates rear nitro particles when moving forward with nitro.
- Activates front nitro particles when moving backward with nitro.
- Automatically stops all particles when nitro is inactive or no movement input is detected.
- Simple helper methods to safely play/stop particle systems.

## 🔧 Setup
1. Attach `NitroController` to your ship GameObject (or a child object containing particle systems).
2. Assign particle references in the Inspector:
   - `nitroRearLeft` / `nitroRearRight` → rear thruster effects
   - `nitroFrontLeft` / `nitroFrontRight` → front thruster effects (used when moving backward)
3. Assign the `shipController` reference to the `PlayerShipController` component on the same ship.

## 📌 Requirements
- A `PlayerShipController` (or compatible controller) that exposes:
  - `bool nitroActive`
  - `Vector2 moveInput`
- Particle systems configured for thruster effects.

## 🧩 Example
```csharp
// Automatically handled by NitroController
// When nitroActive = true and moveInput.y > 0 → rear particles play
// When nitroActive = true and moveInput.y < 0 → front particles play
// Otherwise → all particles stop
