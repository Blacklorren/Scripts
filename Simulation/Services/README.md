# Services (Simulation)

This folder contains cross-cutting services specific to the simulation layer (e.g., time management, randomization, service bundles).

## Current Files in This Folder
- **ISimulationServiceBundle.cs**: Interface that aggregates and exposes all major simulation services (geometry, ball physics, movement, etc.).
- **SimulationServiceBundle.cs**: Concrete implementation of the service bundle, providing unified access to simulation services.
- **README.md**: This documentation file.

> **Unity .meta files** are present for asset management and should not be edited manually.

## What Belongs in This Folder?
- Any service that is global or utility-like for the simulation (e.g., time, random, service aggregators).
- Interfaces and their implementations for simulation-wide services.
- Services that are shared between multiple engines or simulation subsystems.

## What Does NOT Belong Here?
- Domain/gameplay-specific logic (e.g., player AI, match rules, event handling).
- Services tied to a single subsystem (should be in that subsystem's folder).

## Best Practices
- All services should be exposed via interfaces for testability and flexibility.
- Keep services stateless if possible, or manage state in a predictable, simulation-safe way.
- Document each service's purpose and usage at the top of its file.

---

**Note:**
This folder is for simulation-layer utilities only. Do not mix global simulation services with gameplay/domain logic.
