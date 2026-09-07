# AUREATE ORRERY

**Current rendering setup:** [Photographic polish settings and limitations](PHOTOGRAPHIC-POLISH.md). This supersedes the original lighting, materials, camera and performance defaults documented below. Geometry and animation documentation remains applicable.

An imaginary astronomical instrument: a gold ecliptic dial holds five tilted brass meridians, a cold stellar core, and fine mathematical records. Twelve diamond-shaped sighting stations give the perimeter an engraved-manuscript character without imitating historical writing. The sacred geometry remains behind the astronomical mechanism.

Built for this project's **Unity 6000.6.0f1 / Universal Render Pipeline 17.6.0**. No added dependencies or VFX Graph. The photographic pass generates a small reflection cubemap and sparse dust mesh at runtime.

## Open and play

1. Open `Assets/AureateOrrery/AureateOrrery.unity` and press Play.
2. Set the Game view to a **1080 × 1920** fixed resolution. The camera also automatically fits landscape and square viewports.
3. Select **AUREATE ORRERY** to adjust the Inspector. For geometry settings, use the component's **Rebuild instrument** context menu afterward.
4. To reconstruct the scene, choose **Art → Aureate Orrery → Create Scene**. This asks before replacing the generated scene and respects unsaved scenes. The original SampleScene is untouched.
5. **Art → Aureate Orrery → Validate Loop** checks the loop endpoints, 49 animation samples, finite mesh vertices, and shader import errors.
6. **Art → Aureate Orrery → Render Portrait Preview** validates and renders a 1080 × 1920 PNG into `ArtworkPreviews/AureateOrrery.png` at the project root.

The generated meshes and materials are transient and rebuilt on enable, including on scene load and Play mode entry. Edit the Inspector parameters, not generated children. Save the scene to retain parameter changes. Shader references are serialized on the controller so player builds can include them. Add the new scene to your Build Profile scene list if creating a player.

## Hierarchy

```text
AUREATE ORRERY [CelestialSystem]
  Generated instrument (transient)
    Armillary mechanism
      180-division ecliptic dial / 12 sighting stations
      Armillary ring 1–5 / attached sight beads
      Hexagonal calculation plate
      1-2-3 harmonic observation curve / marker
      2-3-5 celestial curve / marker
      Epicycle record / pointer / moving deferent
      Cold celestial core / core reticle / faint scattering
      Spherical star catalogue / reference points
    Distant fixed stars
Portrait Camera [Camera, UniversalAdditionalCameraData, CameraController]
Warm key [Directional Light]
Silver rim [Directional Light]
Core illumination [localized warm Point Light]
Restrained bloom + tonal response [Volume]
```

## Mathematics and visible purpose

| System | Formula / implementation | Visual role |
|---|---|---|
| Master clock | q = 2π · repeat(t · speed / 12, 1) | Shared synchronization and exact periodic return |
| Ring movement | Staggered sinusoidal tilt/yaw, with offsets i·2π/φ, plus counter-travelling sight beads | Independent spatial precession and readable orbital motion |
| Global movement | Phase-offset tilt, yaw and roll | Gentle three-dimensional reveal without synchronized rocking |
| Orbital harmonic | (R cos u, 0.72R sin 2u, 0.23R sin 3u) | A closed astronomical observation curve |
| Lissajous | (R sin(2u+π/2), 0.8R sin 3u, 0.2R sin 5u) | A second, finer 2:3:5 calculation record |
| Epicycle | 1.12(cos u,sin u) + 0.32(cos −3u,sin −3u) + 0.10(cos 5u,sin 5u) | A nested celestial pointer and deferent |
| Golden ratio | φ=(1+√5)/2; blend(u,u^(1/φ),influence) | Nonuniform spacing between meridians |
| Golden-angle sphere | y=1−2(i+0.5)/N; azimuth=iπ(3−√5) | Controlled spherical star catalogue and background distribution |
| Hexagonal construction | Six circles around a central circle; nested triangular chords | A restrained calculation plate behind the instrument |

Markers now traverse the full closed curves, with monotonic eased phase `u=q−0.24 sin(q)`; the Lissajous marker travels in the opposite direction. Analytic fading trails sample earlier phase values, so seeking and restarting never depend on a particle simulation's history. Ring precession has golden-ratio phase offsets, and small sighting beads counter-orbit along the brass meridians. Core illumination builds smoothly once per cycle, with a subtle camera dolly and independently tilted core reticle.

This is mathematical choreography, not a gravitational simulation. The ring orientations oscillate; the celestial markers complete their trajectories. All animated terms return with matching endpoint velocities. Increasing global speed changes the loop duration to `12 / speed`; speed zero pauses the clock. Keep settings constant while recording.

## Implementation and complete source

All files contain complete executable source; no pseudocode or missing graph assets.

- `Runtime/CelestialSystem.cs`: scene generation, owned resource cleanup, Inspector settings, one shared motion update, materials.
- `Runtime/CelestialGeometry.cs`: combined tube meshes, engraved segments, orbital equations and spherical coordinates.
- `Runtime/SacredGeometryGenerator.cs`: circular hexagonal lattice and triangular measuring chords.
- `Runtime/StarFieldGenerator.cs`: one combined mesh of small star quads, seeded brightness and size; no per-star updates.
- `Runtime/CelestialTrails.cs`: three analytical orbital histories in one dynamic mesh with reused buffers.
- `Runtime/CameraController.cs`: perspective fitting using the smaller angular field of view; periodic parallax.
- `Resources/PlanetSurface.shader`: URP PBR planet shading with broad procedural tone/roughness, restrained bump, and directional atmospheric edge.
- `Shaders/CelestialGlow.shader`: URP additive radial glow, smooth edge falloff and periodic star brightness. Shader time is supplied by the shared clock rather than `_Time`.
- `Editor/OrrerySceneBuilder.cs`: scene and Volume asset creation, validation, direct camera preview render.

Mechanism metal uses the custom physical brass shader with zero emission. Planets use a separate PBR surface shader and reflected light; only reference stars, trails, and designated markers retain visible emission. The central planet has broad procedural surface structure, roughness variation, a very low emission floor, and a thin directional cool rim. Glow uses additive blending, disabled depth writes, and backface rendering; it retains depth testing. The central scattering is a subtle billboard approximation, not volumetric ray marching.

## Inspector starting values

| Control | Recommended value | Effect |
|---|---:|---|
| Master Scale | 1 | Scales the instrument; camera fits automatically |
| Orbital Rings | 5 | Range 3–8; rebuild afterward |
| Golden Ratio Influence | 0.65 | Meridian spacing; rebuild afterward |
| Ring Tilt | 52° | Spatial separation; rebuild afterward |
| Geometry Complexity | 1 | Keep the sacred lattice subordinate; rebuild afterward |
| Star Count | 1100 | Range 200–3000; rebuild afterward |
| Seed | 1729 | Repeatable star brightness and size; rebuild afterward |
| Loop Seconds | 12 | Base cycle length |
| Global Animation Speed | 1 | Cycle duration is loopSeconds / speed |
| Ring Swing Degrees | 7 | Small ring rotation amplitude |
| Precession Degrees | 14 | Staggered three-dimensional tilt excursion |
| Trail Length | 0.38 | History length in master-clock radians |
| Trail Strength | 1 | Trail point size; zero hides the trails |
| Alignment Glow | 0.45 | Broad once-per-cycle core light crest |
| Emission Strength | 1.25 | Moving celestial points |
| Central Planet Roughness | 0.62 | Broadens and softens the hero highlight |
| Central Planet Surface Detail | 0.42 | Controls broad tone, roughness, and restrained bump |
| Central Planet Rim Strength | 0.12 | Thin cool silhouette separation; avoid turning it into a halo |
| Secondary Planet Emission | 0.018 | Shadow-information floor; keep below 0.05 |
| Manual Phase | Off | Enable for deterministic posing or frame capture |
| Normalized Phase | 0–1 | Manual cycle position |
| Camera FOV | 32° | Cinematic perspective |
| Frame Fill | 0.82 | Safe border around instrument |
| Parallax | 0.055 | Small periodic camera displacement |
| Breathing Dolly | 0.008 | Subtle periodic change in camera distance |

Keep the controller's Transform scale at (1,1,1); use Master Scale for camera-aware sizing. The perspective fit assumes the generated default dimensions.

## Lighting and post-processing

Scene creation configures:

- Solid near-black camera background, HDR enabled, SMAA, post-processing enabled.
- Warm directional key: RGB (1, 0.79, 0.49), intensity 2.2.
- Cold directional rim: RGB (0.55, 0.72, 1), intensity 1.5.
- Central blue point light: intensity 2, range 4.
- Low ambient fill: RGB (0.06, 0.065, 0.085); no skybox.
- Bloom: threshold 1.1, intensity 0.32, scatter 0.58.
- ACES tonemapping; vignette intensity 0.16 and smoothness 0.6.

The profile is saved as `OrreryVolume.asset`. Change bloom there. Keep the URP asset's HDR and post-processing support enabled. Metallic rings are directly lit and do not require reflection probes. The current brass is stylized; there is no claim of physically simulated patina.

## Performance and final polish

Rings and scales are combined per layer; all background stars share one mesh and material. Meshes are built only on enable/rebuild. A small number of reference stars use ordinary sphere renderers; GPU instancing is unnecessary at this count. Motion changes transforms and material parameters. The three trails update 768 vertices in a single reused mesh; the engraved paths remain static and no particle arrays are allocated per frame. Shadows are disabled on artwork meshes and lights.

- First adjust framing and exposure, then emission; excess bloom erases the small divisions.
- At small playback sizes, keep complexity at 1 and reduce rings to 4 if visual overlap dominates.
- Inspect phase 0, 0.25, 0.5 and 0.75 after altering ring tilt.
- Record 720 frames at 60 fps for the default 12-second cycle. For deterministic external capture, use Manual Phase and `frameIndex / 720f`, indices 0–719; do not append phase 1 as an extra duplicate frame.
- **Art → Aureate Orrery → Render Motion Study** renders 96 preview frames at 432 × 768. Play them at 8 fps for the default 12-second cycle. This is a lightweight motion study, not a full-resolution final export. Unity Recorder remains optional and was not installed.
- Profile the target GPU at 1080 × 1920 before claiming a stable frame rate; real-time design does not establish measured performance.

## Validation

The live Unity editor compiled the scripts, created and saved the scene, and rendered a portrait preview. Validation checks endpoint transforms, orbital seam velocities, trail seam continuity, sampled animation transforms, mesh coordinates, and shader import errors. The preview was visually inspected. A brief Play mode smoke check produced no reported runtime errors. No standalone player build, video export, or target-device frame-rate benchmark has been performed.
