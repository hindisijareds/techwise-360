# Complete build audit and implementation record

Request: 2026-09-20, attached 31-section specification. Work is in progress; this document is not a completion claim.

## Existing project audit (before implementation)

- Unity 6000.4.8f1, XRI 3.4.1, OpenXR 1.16.1; build scenes MainMenu, Singleplayer, Multiplayer. Existing dirty assets and portal work are preserved. Baseline script copies: Logs/CompleteBuildAudit/Baseline.
- Live parts and keyed sockets are owned by TechWiseSimulationRuntime. Position/orientation validation already exists; release handling records one nearby invalid attempt, and component recovery supports loose-part physics.
- Detailed assembly uses retained source-model poses and procedural additions: CPU cover/arm, explicit paste, four cooler screws, four RAM modules, angled M.2 with one screw, nine real PCB holes, case access panel, rear exhaust plus two front intake fans, GPU connector, GPU including attached cooler, SATA SSD and PSU.
- Contradiction: ModeManager/assessment still use eight groups; supplemental fans and GPU connector are omitted. Detailed mechanics are explicitly assembly-only, so disassembly lacks them.
- Screws are not grab interactables: they appear already seated when their component is placed, with only tightening recorded. Fans have no screw validation. Other case-mounted components rely only on sockets.
- Hinge code only closes, never reopens. Side panel has a kinematic body and no reinstall validation.
- Competition recorder registers before runtime additions, records socket selection before fastening, treats one RAM selection as group completion, auto-finishes at group count, and calculates from a reduced available list. It does not consume shared mistake events or centralized scoring settings. LocalVerificationMode does not currently guard queue submission.
- Tutorial has six control exercises and partial mechanics instructions. It is assembly-only; its detailed branch never enters Finished. Text has ellipsis overflow and hard-coded Grip/Trigger wording. Fans are instantiated active. Recovery reactivates hidden parts, conflicting with conditional availability.
- Actual XR actions: Select=GripButton; Activate/UI Press=TriggerButton; Move/Turn/Manipulation/UI Scroll=Primary2DAxis. Inputs are mediated while grabbing/UI scrolling. Existing rigs have smoothTurnEnabled=false; continuous turn provider exists (multiplayer speed 180 degrees/sec).
- Tutorial/practice controls panels already support grip repositioning. Competition HUD follows POV every frame. Monitor body has no scroll container; base height is inferred from case bounds and all monitor colliders are destroyed.
- Source authoring contains CPU cooler hardware, PCB holes, M.2 standoff, fan frames, PSU/GPU brackets and side sheets. The GPU cooler is part of the GPU model, not another independent component. Do not add a fictitious second cooler.

## Implementation sequence

1. Shared complete-build catalog and live requirements; preserve source-derived target poses and the existing keyed socket/hand interaction system.
2. Individual loose screws, dedicated holes, deliberate grab/release insertion, per-screw driver progress and reversible removal. Full assembly/disassembly state shared across modes.
3. Structured binding-aware tutorial, each hole highlighted, strict completion, fan availability, reusable tools, detailed corrective feedback.
4. Immutable full Done validation, centralized completion/accuracy scoring, previous-action rollback and new-attempt reset without a direct penalty.
5. Existing continuous-turn provider, panel placement/scrolling/layout, side-panel physics/reinstallation and monitor/table contact.
6. New end-to-end regressions for all six mode/activity combinations, invalid placements/partial fasteners/reset/scoring/input/UI/physics; compile/build and headset smoke check. Physical controller walkthrough must be reported separately from automated simulation.

## Validation status

Pending. Earlier test totals are not evidence for this new specification.
