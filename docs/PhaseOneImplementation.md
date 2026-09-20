# Phase 1: detailed motherboard and case assembly

The existing Unity project now has an additive Phase 1 assembly runtime. It uses the retained PC meshes, XR grip selection, keyed sockets, placement previews, tutorial controller training, and existing guide boards. No scene replacement or reference-project asset import was used.

## Restore point and reference audit

- Pre-change snapshot: `e815ee97d805023d49e9b181480422653aa9c7e9`, retained at `refs/restore-points/phase1-20260913`. The snapshot includes the pre-existing uncommitted Unity work; the normal Git index and branch were not changed to create it.
- Snapshot metadata: `D:/Jared/UnityProject/TechWise360-RestorePoints/Phase1-20260913/`.
- [Reference repository](https://github.com/CharlieUK04/VRPCBuildingSim), commit `b610cd7896ef97fa5922cd745d8427d84ef77ab9`, cloned separately into `D:/Jared/UnityProject/References/VRPCBuildingSim`.
- Studied `ComponentSnapper`, `MenuHandler`, `FanHandler`, and the main scene's CPU, paste, screw, component, and cable objects. Its tag/distance/rotation checks, prerequisite checks, clip movement, and visual-state changes informed the design. Its screw action is a one-shot screwdriver trigger; this implementation instead tracks each screw's progress. Its destruction of grab/physics components on snap was not copied.

## Implemented sequence

1. Prepare the retained motherboard outside the case.
2. Start with its real CPU cover and retention arm open.
3. Align and release the CPU into its socket.
4. Grip and lower the cover, then grip and lower the locking arm; release near closed to commit.
5. Hold the paste applicator's tip over the CPU and press trigger to apply a visible mesh blob.
6. Align and release the cooler.
7. Tighten four cooler screws individually with the held screwdriver and trigger.
8. Install all four RAM sticks with correct keyed orientation.
9. Insert the M.2 SSD at 25 degrees, then grip its free-end handle and lower it onto the existing support.
10. Tighten its retention screw.
11. Remove the case side panel; the mounting supports are already positioned at the PCB's real holes.
12. Align and release the prepared motherboard into the case.
13. Tighten all nine motherboard screws.
14. Install the CPU-area rear exhaust fan.
15. Install both front intake fans.
16. Validate all three airflow orientations.
17. Display Phase 1 completion on the existing guide.

Preparation/open-socket and final airflow validation are initial/final state checks; they do not add artificial button presses. Later GPU, PSU, and storage objects remain available through the existing simulation. No new cabling or boot mechanics were added.

## Placement and geometry

Placement validates the compatible key, occupancy, attach-point distance, direct quaternion alignment, and prerequisites before changing the transform. Hovering cannot bypass validation. A controller-held part cannot be socket-selected; the existing release path performs the final snap. CPU locking and all required screws gate completion, including the existing competition assessment snapshot.

Final component poses come from the project's assembled `maya2sketchfab.fbx`. The authoring tool extracted nine PCB mounting holes, four cooler screw heads, and the motherboard-to-case pose. M.2 seating uses the existing support at the SSD's free end (`pCylinder65`), with height corrected to rest the PCB on it; the old socket was positioned at a middle support. The insertion pivot follows the SSD connector edge.

The office enclosure adds neutral sheet-metal panels, actual perforated grille geometry, a removable side panel, and three grille-derived fan mounts around the retained tray/brackets. The rear fan exhausts through the CPU-height grille; front fans direct air into the case. The screw and screwdriver meshes use the user's `new_assets` FBXs. The paste tool reuses the existing tube prefab with its cap removed, and the fans reuse the existing fan mesh. PC components were not replaced with cubes.

## Files

Added runtime scripts in `Assets/Script/`:

- `TechWiseDetailedAssemblyRuntime.cs`: phase state, prerequisites, runtime model authoring, reset/cleanup, and guide text.
- `TechWiseAssemblyHinge.cs`: constrained grip/wrist motion and release-to-lock.
- `TechWiseAssemblyTool.cs`: held-tool trigger input using existing XR actions.
- `TechWiseFastener.cs`: individual loose/partial/tight states and contact/alignment checks.
- `TechWiseAssemblyPartId.cs`: identifiers for supplemental keyed fans.
- `TechWisePhaseOneAssets.cs`: source-derived authoring data.

Extended existing scripts: `TechWiseSimulationRuntime.cs`, `TechWiseSimulationModeManager.cs`, `TechWisePlacementRule.cs`, `TechWiseTutorialRuntime.cs`, `TechWiseGuideAssistant.cs`, and `TechWisePracticeGuidePanel.cs`. `TechWiseTutorialView.cs` also receives a guard against drawing a marker after its renderer has been destroyed during teardown.

Added Editor tools: `TechWisePhaseOneAssetAudit.cs`, `TechWisePhaseOneAuthoring.cs`, and `TechWisePhaseOneVerification.cs`. Generated assets are in `Assets/PhaseOne/` and `Assets/Resources/TechWisePhaseOneAssets.asset`. The runtime loads its configuration automatically; no Inspector wiring or additional 3D asset is required for the implemented mechanics. The enclosure is a generated extension of the existing chassis, rather than a separately commissioned case model.

## Verification and limits

Final verification: **425 Phase 1 checks passed**, covering the full assembly in both Practice and Tutorial Mode plus teardown. The final log contains **0 C# compilation errors and 0 null/missing-reference exceptions**. It also contains **5 XR UI Toolkit registration-capacity errors and 40 accompanying slot diagnostics**, reported separately below; this is not a globally error-free Editor run.

The latest automated result and complete Unity output are in `Logs/PhaseOne/results.txt` and `Logs/PhaseOne/verification.log`. The checks cover scene setup, real XR interactable/collider registration, simulated controller grip/release, invalid rotations and distances, hover feedback, prerequisites, independent screw progression, visible paste, case access, airflow, preservation of later components, cleanup, and Tutorial Mode startup.

Rendered checks: `Logs/PhaseOne/open-socket.png`, `paste.png`, `prepared-board.png`, and `assembled-case.png`.

These are automated Editor/XR-event checks, not a physical Meta Quest controller test. Headset reach, comfort, final tolerance tuning, and unassisted Quest scene transitions still require device testing.

The installed Unity packages exposed stale XR UI Toolkit registrations and a duplicate Unity Learn editor GUID during a multi-scene headless test. The verification harness resets Unity Learn's editor-only GUID registry before changing scenes. XR UI Toolkit capacity errors are retained in the log and reported separately from Phase 1 feature checks; the application uses the existing world-space Unity UI raycaster. Production XR registration code and package sources were not replaced, and a globally error-free scene transition is not claimed. The Editor also logs unavailable OpenXR/hand-tracking services and an existing video color-profile warning in headless runs. No APK build or device installation is claimed for this Phase 1 work.
