# Balance And Pacing Plan

This is the live roadmap for balance, pacing, and readability work. We will tick items off and adjust the plan as testing teaches us more.

## Master Plan

- [x] 1. Review which existing Free Haven cards need changing first.
- [x] 2. Review which existing Iron Citadel cards need changing first.
- [x] 3. Rework `Secure` so it only happens when the unit is physically standing on a valid adjacent Freespace next to the friendly base network, make secured tiles enter weakened instead of full strength, and require a 2-turn hold on that same tile.
- [x] 4. Add a clearer board-state read so players can instantly tell whether a unit is standing on a Base Tile, Freespace, or enemy-side tile.
- [x] 5. Fix unit return-state so dead units never keep ordinances, items, or granted abilities when they go back to discard/deck.
- [x] 6. Reduce overall match duration by lowering unit HP across the roster.
- [x] 7. Reduce building durability by lowering how much health they add when merged with base tiles.
- [ ] 8. Retest city HP and base tile HP only after unit/building health changes, and keep city HP at `50` unless pacing still drags.
- [x] 9. Fix chained movement so movement planning accounts for spaces that will become free during the same deploy resolution.
- [x] 10. Check and fix `Provoke` so it never forces attacks outside legal range.
- [ ] 11. Review the early-game tempo gap between Free Haven and Iron Citadel.
- [ ] 12. Adjust one or more Iron Citadel cards so Iron has an earlier stabilizing option.
- [ ] 13. Review whether Iron Citadel needs a modest economy support tool, likely through replacing one current ability, item, or building with something that helps income or cost relief.
- [x] 14. Review the current buildings, abilities, and items for overlap so cards are not doing the same or near-same jobs when they could create more interesting dynamics.
- [ ] 15. Preserve the current treasury scaling, because it is clearly creating good comeback arcs.
- [ ] 16. Keep faction asymmetry as a design goal instead of flattening both decks into the same shape.
- [ ] 17. Add arena modifiers later so each arena changes the match feel beyond visuals.
- [x] 18. Explore future universal "war shop" attack-phase system cards as an expensive tactical layer outside the deck.
- [ ] 19. Explore future new abilities and items only after pacing and faction balance are stable.
- [x] 20. Fix minor presentation bugs that distract from readability, like subtle board scaling or shifting during interaction.
- [ ] 21. Keep monitoring whether the game remains exciting and readable for first-time players with minimal explanation.

## Current Phase

We are starting with:

- [x] Review which existing Free Haven cards need changing first.
- [x] Review which existing Iron Citadel cards need changing first.

## Notes Locked In So Far

- `Secure` should not randomly spawn a tile.
- A unit must physically stand on the valid Freespace tile to secure it.
- That Freespace tile must be adjacent to the friendly base network.
- The unit must remain on that same valid tile for `2` friendly Deploy-start checks before `Secure` completes.
- New secured tiles should enter weakened rather than at full strength.
- Economy scaling is currently working well and should be preserved.
- Late-game pacing issues seem more tied to HP density and board saturation than to treasury.
- `Provoke` has been tightened so only a living owned unit can ever be force-routed into a provoke target. Empty/dead/stale source tiles no longer generate fake forced attacks.
- The awareness shell now holds a stable height so status text swaps do not subtly change board viewport height and make the grid appear to breathe.
- A lightweight in-game `Field Guide` now exists behind the `?` button, with a short start tab plus deeper tabs for flow, units, buildings, orders, items, keywords, combat, and War Shop.
- The WebGL/PWA build path now version-busts the Unity runtime files and active service worker, and removes the old legacy service worker file so browser cache does not keep serving stale builds.
- Overlap review after the `War Levy` / `Sabotage` swap:
  - `Workshop` and `Granary` are still distinct: cost relief for Orders and Items versus raw treasury growth.
  - `Gatehouse` and `Reinforced Plating` are still distinct: aura protection versus deepening a committed `Intercept` carrier.
  - `Ballista` and `Live Fire` are still distinct: lane ping engine versus strike scaling.
  - `Smelter`, `Ram Head`, and `Demolition Rig` are still distinct: grant siege identity, deepen siege damage, or permanently attach it to one carrier.
  - `Granary` and `War Levy` are intentionally parallel economy tools, but one is a stationary building engine and the other is a vulnerable ordinance investment.
  - No further forced overlap cuts are needed this pass.
- Initial War Shop layer is locked for testing:
  - `Field Medic` (`35`)
  - `Bomb Drop` (`40`)
  - `Frontier Claim` (`70`)
  - `Rebuild Order` (`55`)
  - One purchase maximum per attack turn.
- `Ballista` is intended to ping every enemy unit in its column once for `1` damage at attack-start. That is correct behavior, not a bug to "fix."
- Locked first balance pass:
  - `Ranger` cost `9 -> 11`, HP `13 -> 10`
  - `Worker` HP `15 -> 11`, attack `3 -> 2`, cost `9 -> 10`, attack range `1 -> 2`
  - `Overseer` HP `20 -> 15`, cost `13 -> 11`
  - `Juggernaut` stays at `18` cost for now
  - First pacing slash also hit the rest of the unit roster at roughly `20-30%` lower HP
  - Building merge health has been cut down heavily so base + building walls should crack much sooner
