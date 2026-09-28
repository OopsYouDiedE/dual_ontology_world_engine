# DualOntologyWorldEngine.Core

An engine-independent C# library for the project's Essence–Projection world model. It targets .NET 8, uses C# 12, and has no third-party package dependencies.

## What is implemented

- `Essence` represents an objective identity and stores `Property` instances. `Projection` represents an object in a `ProjectionWorld`; `WorldRuntime` creates and synchronizes projections and resolves their relationship to objective objects.
- `SoulEssence` owns a private `ProjectionWorld`. `Observation.Observe` updates that world through recorded observation inputs. `Cognition.Infer` submits supported explicit propositions, while `Cognition.Corrupt` applies an `IProjectionTransform` through the observation path.
- `Condition`, `Effect`, and `Rule` form a typed AST over projection references and actor/target/tool/scene slots. `AstDecoder` parses the textual rule notation into this AST.
- `DecisionSystem` and `SocialSystem` evaluate candidates and requests using a subject's internal world. They do not choose outcomes from objective state.
- `SimulationSession` coordinates submitted or autonomous actions with `CTBSystem`. `ActionRuntime` prepares each action stage before committing it; nested `Effect.Wait` stages resume through the discrete action-value timeline.

The library does not implement room geometry, collision, perception range, rendering, UI, or a real-time loop. Those belong to the game host. The complete maintenance constraints are in [AGENTS.md](AGENTS.md); the design overview is in the repository's [Core operation notes](../docs/双重引擎运作原理.md).

## Build and pack

From the repository root:

```powershell
dotnet build csharp/Core.sln -c Release
dotnet pack csharp/DualOntologyWorldEngine.Core.csproj -c Release -o artifacts/releases/core
```

The package is `DualOntologyWorldEngine.Core` and targets `net8.0`. Its NuGet package includes this README. To use the project directly, reference `csharp/DualOntologyWorldEngine.Core.csproj`; to consume a locally packed `.nupkg`, add `artifacts/releases/core` as a local package source.

No license has been declared for this project. The generated package is a local build artifact and is not suitable for public distribution until the project owner chooses and records a license.

## Current scope

This README documents the public implementation in the current source. It is not a claim that every proposed world mechanic or game scenario is complete. For authoritative behavior, consult the C# source files in this directory.
