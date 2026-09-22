# Rateworks prototype reflection

This prototype focuses on one authored contract bay rather than a full game. The player is an engineer in a single industrial room. The brief asks for 1 gear per second for 20 seconds; the player can place and rotate conveyors and Basic Assemblers, inspect machine state, run the simulation, and see the final factory cost against a soft recommended budget. A Load example button makes the intended flow immediately demonstrable.

The prototype follows the assignment constraints: the project uses URP, does not use Cinemachine, includes first-person movement, object rotation, simple scene setup, and reusable prefabs. The room contains 27 different imported prop models, a unique floor material, and 24 distinct prop materials created for the room. The industrial asset packs are used for the visual language while the authored teal input ports, orange dispatch ports, yellow grid, and white signage keep the contract readable.

## Controls

WASD moves the engineer. Mouse look is active after starting the shift; Tab frees the cursor for UI. B selects a $50 belt, M selects a $1,000 assembler, R rotates the selected tool or part, left click places/selects, right click cancels, Delete removes a placed part and refunds its full cost, Space pauses, 1/2 select simulation speed, H shows a hint. The example button loads two parallel production rows.

The build buttons appear when the cursor is free (Tab). The inspector appears after selecting a part, and briefing/results screens hide the gameplay HUD. Hold H to read the hint while walking. Input/output labels are compact signs on the ports; conveyor meshes align with their transport arrows.

## What is intentionally simplified

The production model uses deterministic item tokens and a small fixed grid. There is no power, mining, persistent money, level menu, or combat. The six-level plan from the design document is represented by one teaching contract so the assignment prototype remains a single-room demonstration. The five-second design window is represented by a stable six-second window in this prototype because two 2-second assemblers otherwise alternate between 0.8 and 1.2 items per second at the sample boundary; the contract and feedback remain the same.

## Assignment checklist

- Universal Render Pipeline: `RateworksPrototype/Scenes/RateworksPrototype.unity`.
- No Cinemachine package or component is used.
- Floor: `Materials/Floor - warm concrete.mat`.
- 20+ different models: 27 imported industrial props plus authored machine, belt, source, dispatch, and product prefabs.
- 20+ distinct object materials: 24 named `Prop 01` through `Prop 24`, plus authored machine, input, dispatch, product, safety, and preview materials.
- Prefabs: `Prefabs/Conveyor.prefab`, `Basic Assembler.prefab`, `Plate input.prefab`, `Gear dispatch.prefab`, `Iron plate.prefab`, and `Gear product.prefab`.
- Original pre-prototype scene snapshot: `Scenes/OriginalSceneSnapshot.unity`.
