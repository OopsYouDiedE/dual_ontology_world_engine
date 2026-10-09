# Supplemental X02 — frozen before execution

`SimulationSession.Submit` checks that target references belong to the actor's cognition (lines 38–41). Later `ActionRuntime` checks membership for Actor only (14), and Plan.Resolve (56–59) follows the engine binding without target-world membership. Prediction: forgetting a submitted target before activation may still permit a real effect. This is a detected lifecycle policy, not a design-correctness verdict. Re-observation should produce a new mental reference; rebinding that new reference to another existing objective object should be rejected by `Associate` (36–40).

The initial core/compiler observations remain sealed; this supplemental probe writes separate files.
