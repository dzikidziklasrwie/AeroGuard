# Documentation assets

## `aeroguard-hero.png`

This image is the README banner for AeroGuard. It presents the project as a
fictional, civilian-neutral health-monitoring simulation. The aircraft and six
subsystem modules are concept art, not an aircraft design, cockpit display, or
certified system.

OpenAI image generation created the image on 2026-11-19. The generation input
contained no company logo, proprietary reference image, or real aircraft
telemetry.

Generation brief:

> Use case: stylized-concept
>
> Asset type: README and portfolio hero banner for a C++ embedded systems
> project
>
> Primary request: a sophisticated visual metaphor for a fictional avionics
> health monitoring simulator, showing a sleek generic non-military aircraft
> silhouette as a subtle wireframe surrounded by six clearly distinct
> subsystem modules connected through an elegant deterministic data-flow
> network
>
> Scene/backdrop: deep navy-to-charcoal technical studio space with faint
> engineering grid lines and restrained telemetry traces
>
> Subject: generic aircraft health-monitoring system, abstract power, cooling,
> temperature, communications, onboard computing, and equipment-status nodes;
> visual emphasis on software architecture and diagnostic observability
>
> Style/medium: premium cinematic 3D technical illustration blended with crisp
> vector-like interface geometry, polished engineering portfolio aesthetic
>
> Composition/framing: ultra-wide 16:9 banner, aircraft centered, balanced
> connected modules around it, strong hierarchy, generous safe margins,
> suitable for a GitHub README header
>
> Lighting/mood: controlled cyan edge lighting with subtle amber status
> accents, calm, precise, trustworthy, modern
>
> Color palette: midnight navy, graphite, slate, cool cyan, small restrained
> amber accents
>
> Materials/textures: brushed dark metal, translucent glass-like data panels,
> precise luminous connection lines
>
> Constraints: no text, no letters, no numbers, no logos, no trademarks, no
> watermark; fictional and civilian-neutral; avoid cockpit UI screenshots;
> avoid suggesting real-time guarantees or certification
>
> Avoid: weapons, missiles, countermeasures, tactical symbols, military
> insignia, radar screens, targeting reticles, threat maps, combat imagery,
> excessive neon, decorative clutter

Keep the source image at its original aspect ratio in documentation. If the
asset changes, update the date, tool, exact prompt, and any source-reference
provenance in this file.

## `dashboard-preview.png`

Headless Google Chrome captured this 1600 x 1200 image on 2026-11-19 from the
production `dashboard` Docker stage at `http://127.0.0.1:8080/`. The container
was healthy and returned HTTP 200 before capture. The visible
communication-interruption replay came from the C++ executable's seed-42
`--export-all` output.

Chrome used a 1600 x 1200 viewport and waited 4.5 seconds after the archive
became visible. The image contains the real React interface and generated
JSONL data. It is not an image-generation result or a hand-edited mock-up.

## `operations-map-preview.png`

Headless Google Chrome captured this image on 2026-11-19 from the production
Docker stack at `http://127.0.0.1:8080/`. The browser used a 1920 x 1400
viewport and captured the 1446 x 904 operations-map workspace after an
administrator signed in.

Three local demonstration aircraft were created through the REST API and
placed on the map with different archived scenarios. The screenshot shows the
published Natural Earth geometry, server-calculated positions, a connected
SignalR stream, and telemetry imported from C++ JSONL output. It is an
unedited capture of the React interface.

Chrome performance events recorded one `/hubs/operations` WebSocket creation,
one completed handshake through nginx, and four received frames during the
same local verification session.
