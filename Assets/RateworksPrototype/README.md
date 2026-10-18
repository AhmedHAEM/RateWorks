# Rateworks — Assignment 2 prototype

One engineer, one industrial room, one contract: deliver **1 gear per second for 20 seconds**, with a soft cost target of **$2,800**. Build a layout, run it, inspect bottlenecks, stop to revise it, and complete the contract. Assignment 2 Part 3 supersedes the earlier assignment's camera restriction: this version uses **Cinemachine 3.1.7** and URP.

The room retains its existing object positions, rotations, and scales. Hitboxes were repaired without rearranging the level. The original 27 imported prop models, 24 distinct prop materials, floor material, and reusable production prefabs remain in use.

## Quick demonstration

1. Open `Assets/RateworksPrototype/Scenes/RateworksPrototype.unity`, press Play, then **Start Shift**.
2. Press **Tab**, choose **Load example**, then press **Enter** to run the two production rows.
3. Try **V** to change camera and **F** to toggle the flashlight. Watch the five ceiling fixtures change between cool Build and warm Run lighting.
4. Press **Space** to pause; the status light pulses amber. Resume to complete the contract, see the green status light and completion camera, then view the results.
5. Choose **Keep optimizing** to return to Build with the same layout and cost. Stopping clears products, buffers, and verification timers.
6. Find the **CEILING LIGHTS** switch on the rear wall, just right of the starting area. Walk close, face it, and press **E**. The ceiling lights and their glowing surfaces turn off together; **F** still controls the flashlight independently. Press **E** again to restore the room lights.

## Controls and player actions

| Control | Action |
| --- | --- |
| WASD / mouse | Walk / look around |
| Tab | Toggle the cursor and build menu |
| B / M | Logistics: select a $50 belt / Machines: select a $1,000 assembler |
| Left click | Place the selected tool, or inspect a part when no tool is active |
| Right click | Cancel the tool and inspect the pointed-at part |
| R | Rotate the placement preview or selected part |
| Delete / Backspace | Remove the selected part with a full refund |
| Enter | Start Run / stop and return to Build |
| Space | Pause or resume an active Run |
| 0 / 1 / 2 | Set simulation speed to 0.5x / 1x / 2x |
| V / F | Switch first-/third-person camera / toggle flashlight |
| E near the wall switch | Toggle ceiling lights (within 2.7 m, facing it, with clear line of sight) |
| H / Escape | Show a hint / cancel tool and release cursor |

These include more than five player actions. Placement, rotation, deletion, and example loading work only in **Build**; pausing a Run does not enable editing. Fixed input/output ports cannot be removed. The placement preview shares the actual placement collision check, including the engineer's body. The inspector appears on selection; the build toolbar appears with Tab.

## Camera and lighting requirements

Three Cinemachine cameras demonstrate distinct behaviors:

- **First person:** follows the engineer's head/look target.
- **Third person:** follows behind the engineer and adjusts for obstacles.
- **Completion:** takes priority on contract success and blends into a fixed overview; returning to gameplay blends back.

Four lighting behaviors make the assignment's minimum of three visible in play:

- **Flashlight:** player-controlled spotlight toggled with F. A separate URP shadow mask excludes the engineer from this light's shadow casters, but keeps world shadows and the character's ceiling-light shadows.
- **Ceiling switch:** E toggles five real lights positioned beneath the existing hanging fixtures, together with their emissive materials. The switch rocker and indicator show the state. Flashlight and status beacon remain independent; room-off persists across Build/Run changes, and Restart restores the lights.
- **Work lights:** smoothly change color and intensity between cool Build lighting and warm Run lighting when switched on. Balanced ambient/daylight fill keeps machine faces and room corners readable without hiding the ceiling lights' on/off change.
- **Status signal:** pulses amber for paused or underperforming production and turns green on contract completion.

## Verification and scope

Live checks passed for belt/assembler placement, rotation, removal, full refunds, editing restrictions during Run and paused Run, stop/reset behavior, player-edge overlap rejection, and the speed cycle. The example sustained 1 gear/s for 20 seconds and completed at approximately **28.7 simulation seconds**, including startup time.

Lighting checks cover E-key activation, repeated on/off toggles, blocked/out-of-range/away-facing/menu interactions, room-off persistence through Run/Stop, independent flashlight use, and the warm Run/amber warning/green success states. The flashlight no longer projects the animated face/hat in third person. Existing room-model transforms remain unchanged.

This is a small prototype, not the complete design document. It omits mining, power networks, multiple contracts, combat, and a persistent economy. Products use a deterministic grid simulation rather than rigidbody physics. Throughput is measured over six seconds so two synchronized two-second assemblers give stable feedback; a five-second window would alternate between 0.8 and 1.2 gear/s. Going over the recommended budget still permits completion; the lowest successful cost is saved locally.
