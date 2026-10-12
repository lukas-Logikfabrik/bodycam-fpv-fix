# Bodycam interface implementation plan

> Execution: inline, following the user's explicit instruction to implement the approved mockup.

Goal: reproduce the approved monochrome interface in the existing Windows Forms utility, with an almost grain-free room background.

Architecture: retain MainForm's device, mapping, calibration and bridge behavior; introduce a partial layout and small painted presentation controls. Embed the room and an OFL condensed font in the single executable. No new runtime dependencies or networking.

Design reference: approved conversation mockup, with subsequent request to remove almost all background speckling. Keep live stick diagrams, sidebar, white rectangular actions and readable status. Use an independently licensed similar font rather than claiming the game's original font.

- [x] Add embedded artwork/font and license to build and release packaging.
- [x] Replace layout; retain all existing axis/invert, calibration, switch, button and diagnostic controls. Clearly label Acro as physical switch input, not inferred game mode.
- [x] Handle missing device, lost connection, missing driver and warnings without stale live indicators.
- [x] Build executable; exercise all navigation pages and capture actual rendered UI at normal and minimum dimensions with DPI-aware layout. Verify resource embedding and existing behavior through a small isolated UI harness.
- [x] Review final diff and package a preview build with screenshots for the user; no Nexus upload or release publication.

Review focus: all original settings reachable; hardware absent must not look connected; throttle preview distinct from locked output; Acro cannot report actual in-game state; font/image embedded and usable offline; warnings wrap; settings and driver remain untouched by visual verification.

Validation: preview build and packaging succeeded. The complete UI smoke test passed after the font and repaint fixes, covering five pages, minimum size, warning wrapping, disconnected input and resize-font retention. User confirmed the updated UI. Physical-radio/game and high-DPI coverage are not provided by this harness. The previous temporary application-control block did not recur on the updated test run.
