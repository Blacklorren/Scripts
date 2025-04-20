# Physics

This folder contains all scripts related to physical calculations (movement, collisions, trajectories) used in the simulation layer of the game.

## Purpose
- Centralize all deterministic, engine-independent physics logic for the simulation.
- Ensure that all calculations (ball movement, player collisions, rebounds, etc.) are consistent and reproducible.

## Main Files
- **SimBall.cs**: Handles the physical simulation of the ball (movement, velocity, bounces).
- **CollisionUtils.cs**: Utility methods for collision detection between objects.
- **PhysicsEngine.cs**: (Recommended) Main entry point for physics calculations, if present.
- Any script for trajectory, rebound, or collision calculations should be placed here.

## Best Practices
- Scripts in this folder **must be deterministic** (same input always produces same output).
- Code here should be **decoupled from UnityEngine** as much as possible (avoid using Unity physics or MonoBehaviour).
- Favor pure functions and stateless utility classes for testability.
- Keep logic modular and well-documented for future contributors.

## Extensibility & Contribution
- To add new physics logic, create a new script in this folder and document its purpose at the top of the file.
- If your script depends on Unity types (e.g., Vector2, Vector3), only use them for data representation, not for engine calls.
- Write unit tests for all new physics utilities whenever possible.

## Current Files in This Folder

- **DefaultBallPhysicsCalculator.cs**: Concrete implementation for ball physics calculations.
- **IBallPhysicsCalculator.cs**: Interface for ball physics calculators (ensures modularity and testability).
- **MovementSimulator.cs**: Simulates physical movement of objects (e.g., players, ball) in the simulation.
- **IMovementSimulator.cs**: Interface for movement simulation logic.
- **README.md**: This documentation file.

> **Unity .meta files** are present for asset management and should not be edited manually.

## What Belongs in This Folder?
- Any script responsible for deterministic, engine-independent physical calculations (movement, collisions, trajectories, rebounds, etc.).
- Interfaces and implementations for physics-related services.

## What Does NOT Belong Here?
- Scripts that handle gameplay logic, UI, or depend on UnityEngine physics or MonoBehaviour.
- Anything not directly related to core physics simulation.

---

**Note:**
Physics scripts here are intended for use by the simulation/AI/game logic, not for direct use by Unity's physics engine. Keep all logic deterministic and engine-agnostic for maximum portability and testability.
