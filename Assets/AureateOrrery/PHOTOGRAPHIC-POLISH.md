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
| Secondary planets | Warm stone / muted ivory / charcoal; roughness .58–.72; emission floor ≤ .018 |

The new URP PBR shader uses object-space procedural patina and directional microvariation, so the existing untextured tube meshes need no UV or geometry changes. Subpixel variation fades to reduce shimmer. Recessed layers remain rougher and darker. The small curvature-based polish term is a visual approximation, not baked edge-wear analysis; scratches affect material response, not geometry. Reflection softboxes are an authored lighting approximation, not a captured HDR location or ray-traced interreflection. The mip chain is not a full GGX convolution.

`PlanetSurface.shader` separates planets from intentionally luminous stars. It uses broad object-space noise for albedo and roughness, a restrained finite-difference normal perturbation, and a thin directionally weighted Fresnel edge. Planet emission is only a shadow-information floor; the warm directional key, cool fill/rim, repositioned local hero light, ambient fill, and ACES response define their spherical volume. The local point light previously sat inside the central sphere, where it could not illuminate the exterior surface. Bloom remains unchanged because the clipping originated in the former HDR planet emission, upstream of bloom.

Opaque parts now cast shadows. Transparent trails and stars do not. Main-light shadows are supported by the existing pipeline settings. The distant key uses directional lighting; only the local core light has distance falloff. Shadow softness is URP filtering, not area-light penumbra simulation.

## Camera and exact Volume values

The runtime Volume has priority 20 and overrides the saved legacy `OrreryVolume.asset`. Edit the defaults in `Runtime/PhotographicSetup.cs`; changing only the older profile will not override this pass. The transient Volume, dust mesh, material and reflection map are disposed when CameraController is disabled. `Create Scene` also uses the updated physical lighting defaults.

| Setting | Value |
|---|---|
| Sensor / lens | 36 × 24 mm, vertical gate fit, 75 mm (Inspector range 50–85 mm) |
| Aperture / ISO / shutter metadata | f/2.8, ISO 100, 1/48 s (180° at 24 fps) |
| Framing | Existing .82 frame fill; distance recalculated for the optical vertical FOV |
| Movement | Parallax .018, breathing dolly .002; existing phase relationships |
| DOF | Bokeh, f/2.8, 7 blades, focal length synchronized with camera |
| Focus | Camera-axis distance to system center, updated after framing |
| Exposure | Color Adjustments post exposure −.35 EV |
| Tonemapping | ACES |
| Contrast / saturation | +4 / −9 |
| White balance | Temperature −3, tint +1 |
| Bloom | Threshold 2.4, intensity .075, scatter .28, clamp 5, high-quality filtering |
| Vignette | Intensity .13, smoothness .65 |
| Chromatic aberration | .008 |
| Lens distortion | −.012; remaining component defaults retained |
| Grain | Thin1, intensity .055, response .85 |
| Motion blur | CameraAndObjects, Medium, intensity .12, clamp .008 |

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
