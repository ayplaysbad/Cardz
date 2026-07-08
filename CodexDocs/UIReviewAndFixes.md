# UI Review And Fixes

This folder is for Codex working notes and implementation checkpoints.

Initial pass completed on the current mobile UI prototype with these goals:

- Keep the existing interaction model intact while fixing build-risk issues.
- Preserve the intentional `4x6` board layout.
- Make the current UITK prototype safer for a phone export pass.

Fixes applied in this pass:

- Replaced editor-only board tile background loading with runtime-safe USS tile variants.
- Added the missing inspector cost field so overlay data binding is complete.
- Wired the contextual deploy bar so it appears for selected hand cards without breaking tap-to-deploy.
- Prevented repeated board geometry callback registration.
- Added safe-area padding handling at the UI root for phone screens.
- Updated hide/show classes so overlay and action-bar transitions are functional instead of instantly disappearing.

Known verification limits from this environment:

- No local .NET SDK is installed here, so `dotnet` compile validation is unavailable.
- Final validation should still be done in the Unity Editor and with an actual device build.

Domain work added after the initial UI cleanup:

- Added absolute match-seat and tile-ownership enums so future networking can separate canonical game state from local screen perspective.
- Added `CityDefinition` as a real asset type for city identity and starting health/treasury.
- Added `BoardLayoutDefinition` and per-tile definitions for board zones, base/freeplay semantics, base-tile health, and city-blocking rules.
- Added `MatchPrototypeDefinition` so the UI can be driven by real setup data instead of only scene fields.
- Upgraded `UIManager` to initialize runtime city stats, card hand, tile ownership, tile types, and base-tile health from those definitions when a match prototype is assigned.

Combat prototype work added after the data pass:

- Added a runtime turn-resolution loop that resolves the active side's attacks first and movement second before handing the turn to the opposing side.
- Added temporary `Lock` command cards each turn for the active seat. They cost `0`, can only target friendly units, and prevent movement for one turn while still allowing attacks.
- Added persistent per-unit attack assignment lines and source/target highlighting so planned attacks stay visible until the turn resolves.
- Added runtime occupant HP tracking, floating damage/status popups, and base-tile conversion from `Base` to `Freeplay` when a base tile is destroyed.
- Tightened attack targeting so manual and automatic attacks only resolve against enemy occupants or enemy base tiles, which keeps the turn model aligned with future multiplayer authority rules.

Current prototype limits to remember:

- City-direct damage after the front base tiles are broken is not implemented yet; the current loop stops at tile and occupant resolution.
- Support/building passive effects are still data-only for now and are not yet applied during deployment, combat, or movement resolution.
- Validation from this environment is static/source-level only. Unity play testing and device verification still need to be done in the editor or on build.
