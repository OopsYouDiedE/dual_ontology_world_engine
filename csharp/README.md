# DualOntologyWorldEngine.Core

An engine-independent C# library for the project's Essence–Projection world model. It targets .NET 8, uses C# 12, and has no third-party package dependencies.

## What is implemented

- `Essence` represents an objective identity and stores `Property` instances. `Projection` represents an object in a `ProjectionWorld`; `WorldRuntime` creates and synchronizes projections and resolves their relationship to objective objects.
- `SoulEssence` owns a private `ProjectionWorld`. `Observation.Observe` updates that world through recorded observation inputs. `Cognition.Infer` submits supported explicit propositions, while `Cognition.Corrupt` applies an `IProjectionTransform` through the observation path.
- `Condition`, `Effect`, and `Rule` form a typed AST over projection references and actor/target/tool/scene slots. `AstDecoder` parses the textual rule notation into this AST.
- `DecisionSystem` and `SocialSystem` evaluate candidates and requests using a subject's internal world. They do not choose outcomes from objective state.
- `SubjectiveDecisionMemory` records attempts, success, failure, no progress and interruptions in a versioned, strictly decoded history. `SubjectiveDecision` combines personal rules, expected information, effort and that experience without access to geometry or the objective world. `ActionLearning` adapts actual observed execution feedback into the actor's cognition and reorders `DecisionSystem` assessments without bypassing prohibitions; its ordinary cognition nodes survive the existing save protocol. Unrelated observations do not clear retry costs; changes to known objects referenced by an action give it a new context. Callers retain their own reasoning budgets and decide which actions may be interrupted.
- `SimulationSession` coordinates submitted or autonomous actions with `CTBSystem`. `ActionRuntime` prepares each action stage before committing it; nested `Effect.Wait(double, Effect)` stages resume through the discrete action-value timeline.
- `Effect.Wait(Condition, Effect)` suspends the parent action until its subject's own knowledge evaluates the condition as `True`. Only observations in that subject's `ProjectionWorld` trigger reevaluation; objective synchronization and other subjects' observations do not wake it. A satisfied condition queues its continuation at the current CTB time. `ActionExecution.WaitingFor` exposes the suspended condition, and the parent retains its body's action slot throughout suspension. The text decoder accepts `wait_until(condition, effect)`.
- `SimulationSession.Cancel` and `Dispose` unsubscribe suspended continuations, cancel their schedules, and remove their committed `TemporarySet` layers and expiry schedules. Cancellation is idempotent and preserves permanent committed changes and observation history. Temporary layers retain their ordinary expiry after action completion; the session can still cancel their owning execution until its final layer expires.
- `Effect.TemporarySetUntilEnd(Noun, Tag, int)` creates an execution-scoped state override without a time limit or expiry schedule (`StateLease.ExpiresAt` is `null`). It survives nested waits and is removed on completion, failure, cancellation, or disposal. The decoder accepts `temporary_set_until_end(target, tag, value)`. Failed continuation stages also remove ordinary timed temporary layers owned by that execution while preserving earlier permanent commits.

The library does not implement room geometry, collision, perception range, rendering, UI, or a real-time loop. Those belong to the game host. The complete maintenance constraints are in [AGENTS.md](AGENTS.md); the design overview is in the repository's [Core operation notes](../docs/design-standard.md#第三部分core-独立世界引擎运作原理).

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
