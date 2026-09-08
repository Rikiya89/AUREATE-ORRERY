# Photographic polish

This pass keeps URP 17.6, the generated mechanism, mathematical geometry, choreography, and capture commands. Existing uncommitted artwork edits were retained. Open `AureateOrrery.unity`; the existing CameraController now owns the photographic setup in edit mode and Play mode. Do not recreate the scene to apply the pass.

## Lighting and materials

| Setting | Value |
|---|---|
| Warm directional key | RGB (1, .88, .72), intensity 2.6, rotation (35, -35, 0), soft shadows |
| Cold directional rim | RGB (.55, .72, 1), intensity .45, rotation (-25, 145, 0) |
| Central planet key | RGB (.95, .72, .48), intensity 3.2, range 1.35, position (−.42, .34, −.52) |
| Ambient fill | RGB (.018, .022, .030), flat |
| Camera background | RGB (.003, .0035, .0045) |
| Reflection environment | Local custom probe, 64 px HDR cubemap, intensity 1; dim warm/cold broad softboxes over a near-black base |
| Brass base | RGB (.54, .43, .30) |
| Recess base | RGB (.32, .23, .11) |
| Polished detail base | RGB (.68, .57, .41) |
| Metal | .86 metallic, oxidation reduces it by up to .13 |
| Smoothness | .48, recessed parts .32; oxidation reduces it by up to .12, filtered microvariation ±.0325 |
| Metal emission | Zero; light defines the form |
| Central planet | RGB (.31, .345, .38), metallic .20, roughness .62, procedural tonal detail .42 |
| Central emission floor | Base RGB × (.028 + alignment × .018) |
| Central atmosphere | Directional cool Fresnel rim, strength .12, power 5.5 |
| Secondary planets | Warm stone / muted ivory / charcoal; roughness .72–.76; emission floor ≤ .018 |

The new URP PBR shader uses object-space procedural patina and directional microvariation, so the existing untextured tube meshes need no UV or geometry changes. Subpixel variation fades to reduce shimmer. Recessed layers remain rougher and darker. The small curvature-based polish term is a visual approximation, not baked edge-wear analysis; scratches affect material response, not geometry. Reflection softboxes are an authored lighting approximation, not a captured HDR location or ray-traced interreflection. The mip chain is not a full GGX convolution.

`PlanetSurface.shader` separates planets from intentionally luminous stars. It uses broad object-space noise for albedo and roughness, a restrained finite-difference normal perturbation, and a thin directionally weighted Fresnel edge. Planet emission is only a shadow-information floor; the warm directional key, cool fill/rim, repositioned local hero light, ambient fill, and ACES response define their spherical volume. The local point light previously sat inside the central sphere, where it could not illuminate the exterior surface. Bloom remains unchanged because the clipping originated in the former HDR planet emission, upstream of bloom.

Opaque parts now cast shadows. Transparent trails and stars do not. Main-light shadows are supported by the existing pipeline settings. The distant key uses directional lighting; only the local core light has distance falloff. Shadow softness is URP filtering, not area-light penumbra simulation.

## Camera and exact Volume values

The runtime Volume has priority 20 and overrides the saved legacy `OrreryVolume.asset`. Edit the defaults in `Runtime/PhotographicSetup.cs`; changing only the older profile will not override this pass. The transient Volume, dust mesh, material and reflection map are disposed when CameraController is disabled. `Create Scene` also uses the updated physical lighting defaults.

| Setting | Value |
|---|---|
| Sensor / lens | 36 × 24 mm, vertical gate fit, 75 mm (Inspector range 50–85 mm) |
| Aperture / ISO / shutter metadata | f/2.8, ISO 100, 1/48 s (180° at 24 fps) |
| Framing | .94 frame fill at portrait aspect ≤ .7, smoothly blended to .82 at square and landscape; distance follows the optical vertical FOV |
| Movement | Parallax .018, breathing dolly .002; existing phase relationships |
| DOF | Bokeh, f/2.8, 7 blades, focal length synchronized with camera |
| Focus | Camera-axis distance to system center, updated after framing |
| Exposure | Color Adjustments post exposure +.25 EV |
| Tonemapping | ACES |
| Contrast / saturation | +4 / −9 |
| White balance | Temperature −3, tint +1 |
| Bloom | Threshold 2.4, intensity .075, scatter .28, clamp 5, high-quality filtering |
| Vignette | Intensity .13, smoothness .65 |
| Chromatic aberration | .008 |
| Lens distortion | −.012; remaining component defaults retained |
| Grain | Thin1, intensity .025, response .85 |
| Motion blur | CameraAndObjects, Medium, intensity .06, clamp .008 |

URP exposure is controlled by postExposure; ISO/shutter metadata does not automatically set photometric exposure. Motion blur is a deliberately restrained velocity-buffer approximation, not a calibrated 180° temporal integration. Opaque motion vectors use URP's standard pass; transparent marker trails retain their original analytic animation. Grain and temporal rendering effects are not guaranteed bit-identical at the loop seam.

At the existing roughly six-unit instrument scale, a 75 mm lens is well away from macro distances. The physically related DOF is consequently subtle across the instrument. It deliberately does not fake a miniature with excessive blur. Changing masterScale changes optical depth of field naturally. URP transparent stars/dust do not provide their own depth to bokeh DOF; foreground softness is supplied by particle profiles, and star softness retains the existing radial shader.

## Dust and atmospheric depth

- One dynamic mesh: 54 quads / 216 vertices / 108 triangles, one transparent draw.
- Six foreground motes: initial camera depth .7–1.8 units, half-size .025–.06, alpha .018, soft elliptical profile.
- Forty-eight midground motes: hero distance plus −3 to +5 × masterScale; half-size .003–.015 × masterScale; alpha .012–.06.
- Seed is the existing system seed + 391. Locations extend beyond the frame for low visible density. Each mote has distinct size/depth/opacity and noise coordinates.
- World-anchored positions drift using low-frequency Perlin noise, with a small closed noise-domain path driven by the existing phase. This retains the loop convention without obvious orbital dust paths; it is not nonperiodic simulation.
- Alpha blending, no depth writes, soft scene-depth intersections. Very weak key-colored illumination keeps particles mostly invisible; this is not full per-particle light transport or shadowing.
- Existing core scattering reduced to RGB (.012, .018, .029) × (1 + alignment). Global fog stays off. No volumetric ray marcher or VFX Graph dependency is added.
- Background stars now use seeded random directions and noise-based density rejection, leaving quiet regions. Brightness is heavily weighted toward faint stars (.08–1.1). The mathematical internal star catalogue is unchanged.

## Performance and verification

The added costs are shadow rendering, PBR procedural noise, bokeh DOF, velocity-based motion blur, and one small transparent dust draw. Cubemap generation occurs when the presentation is created (and when aspect or master scale changes), never per frame. Dust reuses vertex buffers. The custom material's stock auxiliary passes may limit SRP batching; profile rather than assuming every pass batches. No packages or geometry subdivisions were added.

Prioritize reducing motion blur and DOF quality on constrained GPUs. Do not increase particles or haze to compensate for weak lighting. A 1080 × 1920 target-device frame-rate claim requires profiling.

Verification for the photographic pass: Unity 6000.6.0f1 completed script reload and the existing Validate Loop checks (endpoint transforms, seam velocities, trail seam, 49 animation samples, finite mesh vertices, and shader import). The 1080 × 1920 `ArtworkPreviews/AureateOrrery.png` was rendered in the live editor and visually inspected. Scoped `git diff --check` passed. The later planet-material polish requires a fresh Unity shader validation and preview render; the existing PNG and motion GIF/MP4 still represent the earlier rendering setup.

Changed by the planet polish: `Runtime/CelestialSystem.cs` and the shader validation list; added `Resources/PlanetSurface.shader`. Geometry, orbit mathematics, animation, camera, dust, global lighting, exposure, bloom, ACES, and capture behavior were preserved.

## Mobile readability polish — 2026-09-08

Portrait framing is approximately 15% larger (.82 → .94 fill), retaining a border around the instrument. Square and landscape use the existing .82 fill, with continuous interpolation between aspect .7 and 1. Exposure rises by .6 EV; lower grain and motion blur preserve fine engraved details at small playback sizes. Geometry, materials, lighting, bloom, and orbital choreography are unchanged.

Unity compiled the changes and Validate Loop passed, including 49 animation samples, seam checks, mesh coordinates, and shader imports. The updated 1080 × 1920 portrait PNG was rendered and visually inspected; the perimeter remains inside the frame. Actual phone playback, other viewport renders, a player build, and a refreshed motion export have not been verified.

## Secondary sphere and trail balance — 2026-09-08

Warm stone base RGB is now (.30, .25, .19), roughness .76. Muted ivory uses warmer RGB (.33, .30, .245), roughness .74 and rim strength .02. These material changes soften the small bodies while preserving their lit and shadowed sides. The central planet and charcoal bodies retain their settings. Harmonic and epicycle trail RGB changes from (.48, .73, 1) to (.30, .40, .48), retaining the existing fade, geometry and motion; the gold trail stays unchanged. Exposure, lighting, framing and brass materials are unchanged by this adjustment.

Unity loop and shader validation passed; the refreshed portrait PNG and live Game view were visually inspected. Scoped source whitespace checks passed. No new video export or target-device test was performed.

## Connected reference spheres — 2026-09-08

The 26 Reference star spheres connected by short catalogue chords now use PlanetSurface with muted stone RGB (.28, .265, .23), metallic .08, roughness .78 and rim strength .01. Their animated emission is reduced to a .018 shadow floor multiplied by the existing emission control and phase modulation. Both material creation and animation updates use the new values. This corrects the separate connected nodes that the moving-planet adjustment did not cover. Node sizes, chord geometry, central planet, trails and global exposure are unchanged. Unity loop/shader validation and scoped whitespace checks passed; the refreshed portrait PNG was visually inspected.

## Live Inspector controls and automatic playback — 2026-09-08

Select **AUREATE ORRERY** in the Hierarchy, then edit **Celestial System → Live presentation — no rebuild needed**. These controls apply in Edit mode and during playback:

| Inspector field | Starting value | Adjustment |
|---|---:|---|
| Reflection Strength | 1.65 | Raises broad reflected light on surfaces |
| Exposure | .25 | Overall exposure in EV |
| Engraving Brightness | .8 | Keeps the fine construction lines behind the main rings |
| Brass Roughness | .58 | Higher values broaden and soften metal highlights |
| Central Planet Color | RGB (.38, .405, .42) | Makes the central body more readable |
| Reference Sphere Color | RGB (.36, .32, .25) | Warm shading for connected beads |
| Reference Sphere Emission | .035 | Subtle visibility floor; avoid high values |
| Reference Sphere Roughness | .68 | Softens connected bead highlights |
| Trail Brightness | .85 | Changes brightness independently from Trail Strength, which sizes the trail |

The existing central roughness, surface detail, rim strength and Secondary Planet Emission controls also update live. Broad reflection softboxes now use angular powers 10 and 16, previously 18 and 26. Geometry remains unchanged; layer separation comes from the quieter engraving and trail materials rather than a new depth effect.

Under **Clock**, leave **Auto Play On Start** enabled. Entering Play clears Manual Phase, resets the clock to phase zero, and starts the loop. A saved speed of zero becomes 1 at startup; positive speeds are retained. Once playing, speed zero can still pause the loop, and Manual Phase can be enabled for posing. Disable Auto Play On Start when an external capture workflow must retain a manual pose at startup.

To retain your settings, stop Play, edit the Inspector, and save the scene with Cmd+S. Unity normally discards Inspector changes made during Play. Composition controls still require the component context menu **Rebuild instrument**; camera framing lives on **Portrait Camera → Camera Controller**.

Verification: Unity compilation and existing loop/shader validation passed. A fresh portrait preview was inspected, and Play was observed advancing through different ring orientations before returning to Edit mode. Alternative Enter Play Mode reload configurations and a standalone player build were not tested.

## Focal point and reflected fill — 2026-09-08

Central Planet Size is a new live Inspector multiplier, default 1.12 (12% larger), applied to the existing breathing animation. Reflection Strength now defaults to 1.65 and is serialized in the scene. The reflection cubemap base rises from RGB (.014, .017, .022) to (.020, .024, .030), lifting reflected shadow detail without increasing exposure. Polished meridians use smoothness 0.06 above the aged brass setting for a slightly tighter highlight. Geometry paths, camera framing, and automatic playback are preserved.

Source whitespace checks passed. Unity scene reload was blocked by automatic approval review because it could discard unsaved editor changes. The latest changes still require scene reload and fresh visual/loop validation; the existing portrait PNG predates this adjustment.

## Celestial background — 2026-09-08

The camera now carries a procedural blue/mauve nebula and sparse warm/cool stars, rendered behind the instrument in one draw. An asymmetric diagonal cloud and darker central region keep the mechanism readable. The background is seeded and static, so it introduces no animation seam. It fits the camera aspect every frame and releases its mesh and material with the existing photographic setup. This is a stylized shader backdrop, not a volumetric simulation.

Select **AUREATE ORRERY → Celestial System → Live celestial background**:

- **Nebula Brightness: 1** — controls the colored mist and deep-space base; range 0–2.
- **Background Star Brightness: .65** — controls the backdrop stars separately; range 0–2.
- Set both to zero for a black backdrop. Existing world-space stars remain separate.
- Edit outside Play mode and save the scene to retain changes.

Added Resources/CelestialBackdrop.shader; updated CelestialSystem.cs, PhotographicSetup.cs and the shader validation list in OrrerySceneBuilder.cs. Unity compiled successfully after correcting a local variable name collision. The new shader passed validation, along with the existing 49 animation samples, seams and mesh checks. A fresh 1080 × 1920 portrait PNG was rendered and visually inspected. Source whitespace checks passed. Target-device GPU performance, landscape rendering and standalone builds remain untested.

The editor was available without the earlier scene-reload dialog on resuming verification; no rejected Reload action was retried. The current preview supersedes the earlier black-background PNG.
