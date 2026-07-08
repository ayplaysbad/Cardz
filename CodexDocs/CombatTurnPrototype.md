# Combat Turn Prototype

This note tracks the current gameplay loop implemented in `UIManager` so the prototype state is clear without digging back through chat history.

## Current Turn Flow

1. Each round starts with a bold awareness callout in the form `Round N - <City> starts.`
2. `Round 1` randomly chooses which city starts in play mode.
3. The starting city flips after every fully resolved round, so deploy and attack order alternate round by round.
4. `Deploy` planning starts with that round's starting city and gives that seat up to `60` seconds, with a short intro pause so the round banner can be read first.
5. During deploy planning, players can both place cards and assign movement destinations for movable units.
6. The opposing seat then gets its own `60` second deploy planning window, while both sides' movement arrows remain visible.
7. Movement resolves after both deploy windows finish.
8. `Combat` planning then starts from the moved board state, using the same round starter first. Each seat gets up to `60` seconds to assign attacks.
9. Attack resolution then plays those attacks out one by one.
10. After attack resolution completes, both seats draw their turn-start cards and a new round begins.

## Attack Rules Implemented

- Units can manually target any enemy occupant or enemy base tile within range.
- Occupied enemy base tiles are not siegeable; the occupying unit or structure is attacked first.
- If no manual target is assigned, the unit auto-attacks straight forward.
- Auto-attacks stop on the first blocking tile in that lane.
- Live enemy base tiles are hard blockers, so enemy units cannot step into them while they still have health.
- Units attack a live enemy base tile from outside the base zone until that lane is broken open.
- Once that base tile is broken and becomes freeplay, a unit can step into that cleared frontline lane and attack the city directly from there.
- City-capable units now show an `Attack City` bubble instead of an arrow, and the target city health previews its post-resolve value in red during combat planning. A unit on the frontline city-facing row defaults to attacking the city even if it is standing on a base tile.
- Attack order is:
  - Units furthest forward first
  - Higher attack first when units are equally advanced
- Damage reduces runtime HP, not asset values.
- Destroyed units are removed from the board immediately.
- Destroyed base tiles convert into neutral freeplay tiles.
- Infrastructure placed on a base tile merges with that tile into one health pool. Destroying that merged pool removes both the infrastructure and the base tile.
- City hits now reduce runtime city health and briefly flash the city name and health red in the HUD.

## Movement Rules Implemented

- Units are planned during deploy phase and resolve before attack planning begins.
- Locked units do not move.
- Units now have a separate `movementRange`, while `range` is attack-only.
- If the tile straight ahead is empty, the unit defaults to moving forward by one tile.
- Units with higher movement can be manually redirected farther forward during deploy planning as long as the lane stays clear.
- If the tile straight ahead is a live enemy base tile, movement is cancelled and the lane stays blocked.
- If the tile straight ahead still contains another unit, movement is cancelled.
- If the tile straight ahead contains infrastructure, detour preview and movement now evaluate that infrastructure before the normal live-base blocker rule, but the sidestep is only legal onto an empty `Freeplay` tile. Intact base tiles beside the structure are still blocked.
- Friendly movement arrows remain visible during both seats' deploy windows.
- Same-side units competing for the same fallback lane now resolve to one default mover, while opposing-seat collisions still preview as a `STRUGGLE`.

## Deployment Rules Implemented

- Cards can only be deployed onto the controlling seat's intact base tiles.
- Freeplay tiles and enemy base tiles are not valid deployment targets.
- Each seat receives two temporary `Lock` cards during deploy planning so movement can be cancelled deliberately for that round.

## UI Feedback Implemented

- A full-width timer bar sits above the hand and tracks the current planning window.
- The old ability box now acts as the `Awareness` section, showing card/base text during planning and live turn narration or temporary invalid-action messages when needed.
- Selected units now use seat-coloured borders so active planning reads more clearly.
- Free Haven attack intents render blue and Iron Citadel attack intents render red.
- Assigned targets stay visible while planning continues, and default auto-attacks now draw their arrows during attack planning before any manual override is chosen.
- Deploy-selected units show their planned move tile plus any optional longer move destinations when their movement range allows it.
- Deploy planning now marks contested destination tiles with a `STRUGGLE` bubble before movement resolves.
- During combat planning, the hand area stays reserved for layout stability but the actual cards are hidden, so cards only appear during deploy planning.
- Combat planning now shows `SIEGE` and `Attack City` badges on units whose current default attack will hit a base tile or city directly.
- In combat planning, units show predicted post-attack HP before the display phase begins.
- Selected units show valid in-range targets without dimming the enemy unit itself, while invalid empty in-range tiles show an `X`.
- Locked units show an `L` badge in their tile stats bar.
- Damage and status messages now linger longer, render larger, and animate in more visibly so the display phase is easier to follow in live mode.
- City names stay pinned to their original top/bottom positions while inline state tags show whose current `DEPLOY`, `ATTACK`, `MOVE`, or `STRUGGLE` step is active.
- The ability strip above the player city now stays reserved in the layout and doubles as live combat narration during display resolution.
- Planning phases can auto-ready after showing an awareness message if that seat has no valid actions to take, but deploy planning stays open while lock cards still have legal targets.
- If neither side has any valid attack that round, the display phase shows a single `No valid attacks this round.` message and skips the individual `MISS` sequence.

## Intentional Gaps

- The old per-card passive abilities have been removed. Keyword effects are the new ability foundation, with ordinances/statuses planned as the main way to apply them.
- There is no network authority layer yet; this is still a local/hotseat prototype built on canonical seat ownership and local perspective flipping.
