# ShipHealth

Manages the health system of a ship, including damage, repair, destruction effects, and UI updates.  
Provides events for other systems (UI, managers, gameplay logic) to react to health changes or destruction.

## ✨ Features
- Health management with configurable `maxHealth` and runtime `currentHealth`.
- Damage handling (`TakeDamage`) and repair handling (`UseRepairKit`).
- Automatic UI updates for a health bar (if assigned).
- Destruction sequence with explosion and smoke effects.
- Events:
  - `OnHealthChanged(current, max)` — triggered whenever health changes.
  - `OnDeath` — triggered when the ship is destroyed.
- Optional integration with `GameOverManager` to trigger game over logic.

## 🔧 Setup
1. Attach `ShipHealth` to your ship GameObject.
2. Configure parameters in the Inspector:
   - **Health Parameters**: `maxHealth` (default 100).
   - **Destruction Effects**: assign `explosionPrefab` and/or `smokePrefab`.
   - **Debug**: enable `verboseLogging` for detailed logs.
3. (Optional) Assign references at runtime:
   - `SetHealthBarUI(Image uiImage)` to link a UI health bar.
   - `SetGameOverManager(GameOverManager manager)` to connect game over logic.

## 📌 Usage
```csharp
// Apply damage
shipHealth.TakeDamage(25f);

// Repair health
shipHealth.UseRepairKit(15f);

// Subscribe to events
shipHealth.OnHealthChanged += (current, max) => Debug.Log($"Health: {current}/{max}");
shipHealth.OnDeath += () => Debug.Log("Ship destroyed!");
