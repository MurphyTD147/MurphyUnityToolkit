# PlayerShipController

Controls player ship movement, rotation, nitro boost, turret aiming, and weapon firing.

## Features
- Forward/backward movement with acceleration and deceleration
- Nitro boost with fuel drain
- Pitch, yaw, and roll rotation with smoothed mouse input
- Turret aiming using camera raycast
- Weapon firing with projectile physics and overheat system
- Optional integration with `ShipHealth` and `NitroController`

## Setup
1. Add `PlayerShipController` to your player ship prefab.
2. Assign references:
   - `firePoint` and `projectilePrefab`
   - `turretGun` and `mainCamera`
   - (optional) `ShipHealth` and `NitroController`
3. Configure movement, nitro, and weapon parameters in the inspector.
