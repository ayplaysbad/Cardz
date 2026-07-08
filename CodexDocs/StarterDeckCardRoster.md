# Starter Deck Card Roster

This document tracks the baseline stat cards for the default starting cities:

- `Free Haven`
- `Iron Citadel`

For now, this roster only records:

- `Name`
- `HP`
- `AT`
- `Cost`
- `Attack Range`
- `Movement Range`
- `Copies per deck`

Abilities, ordinances, and items are tracked separately and are not baked into these unit bodies by default.

## Naming Rules

- `Units`, `Champions`, and `Infrastructure` use one-word names only.
- Do not use `The` at the start of any `Unit`, `Champion`, or `Infrastructure` name.
- `Ordinances` and `Items` should stay at `1-2` words maximum.

## Deck Structure Reminder

Each city deck is planned as:

- `6` Civilian cards
- `11` Military cards
- `1` Champion
- `5` Infrastructure
- `5` Ordinances
- `4` Items

Civilian structure:

- `Type A`: `2` copies
- `Type B`: `3` copies
- `Type C`: `1` copy

Military structure:

- `Type A`: `4` copies
- `Type B`: `5` copies
- `Type C`: `2` copies

## City Identity

### Free Haven

- Protective
- Healing
- Farming / treasury growth
- Civilian-heavy
- Efficient and resilient over raw force

### Iron Citadel

- Military-heavy
- Pushy
- Expensive
- Direct
- Stronger in raw combat and pressure

## Civilian Roster

### Civ Type A

Locked in as the baseline cheap civilian counterpart pair.

| City | Card Name | HP | AT | Cost | Attack Range | Movement Range | Copies |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Free Haven | Farmer | 9 | 2 | 6 | 1 | 2 | 2 |
| Iron Citadel | Worker | 11 | 2 | 10 | 2 | 1 | 2 |

Design read:

- `Farmer` is cheaper and more mobile.
- `Worker` is slower, tougher, and now reaches farther so Iron Citadel has an earlier ranged answer.
- Neither depends on innate ability text to feel useful.

### Civ Type B

Locked in as the backbone civilian counterpart pair that appears `3` times per deck.

| City | Card Name | HP | AT | Cost | Attack Range | Movement Range | Copies |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Free Haven | Gatherer | 8 | 2 | 8 | 2 | 1 | 3 |
| Iron Citadel | Foreman | 12 | 3 | 10 | 1 | 1 | 3 |

Current reasoning:

- `Gatherer` gives Free Haven a civilian with natural reach, so its body matters without needing attached abilities.
- `Foreman` is the sturdier, closer-range counterpart that keeps Iron Citadel's civilians blunt and pressure-oriented.
- This pair escalates cleanly from Civ Type A without making Civ Type A obsolete.
- `2 range` is the main source of variety here, while Iron keeps its identity through body and raw attack.

Open balance notes:

- If `Gatherer` feels too weak, first buff would likely be `12 HP` rather than `3 AT`.
- If `Foreman` feels too close to `Worker`, we can either push it to `4 AT` and raise cost, or give it `2 movement` and lower HP.
- Civilians should remain useful, but should not crowd out the military line once that roster is built.

### Civ Type C

Locked in as the single-copy heavy civilian counterpart pair.

| City | Card Name | HP | AT | Cost | Attack Range | Movement Range | Copies |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Free Haven | Homesteader | 13 | 3 | 11 | 1 | 1 | 1 |
| Iron Citadel | Overseer | 15 | 4 | 11 | 1 | 1 | 1 |

Current reasoning:

- `Homesteader` is Free Haven's sturdy top-end civilian: efficient, durable, and useful without ability text.
- `Overseer` is the harsher Iron Citadel counterpart: still punishing in direct combat, but now reaches the board earlier in the match.
- This pair creates a real stat jump from Civ Type B without reaching military-only levels of threat.
- Both remain readable baseline bodies, leaving the real deck personality to future ordinances and items.

Open balance notes:

- If `Homesteader` feels too close to military, first nerf should be `17 HP` before touching `AT`.
- If `Overseer` still feels too oppressive for a civilian, first nerf should be `19 HP` or `3 AT`.
- Civ Type C should feel like the top civilian body, but still clearly sit below the city's dedicated military cards in ceiling and pressure.

## Military Roster

### Military Type A

Locked in as the light military counterpart pair that appears `4` times per deck.

| City | Card Name | HP | AT | Cost | Attack Range | Movement Range | Copies |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Free Haven | Ranger | 10 | 3 | 11 | 2 | 1 | 4 |
| Iron Citadel | Shocktrooper | 12 | 4 | 11 | 1 | 1 | 4 |

Current reasoning:

- `Ranger` gives Free Haven a true military body without abandoning its softer identity: lower raw pressure, but better reach and safer positioning, now with a steeper price for that early control.
- `Shocktrooper` is the direct Iron Citadel answer: stronger, heavier, and better at winning straightforward lane fights.
- This makes the military split feel like `tactical pressure` versus `brute pressure`, which suits the two-city premise well.
- Both clearly outfight the civilian line, but neither should make later medium/heavy military types feel redundant.

Open balance notes:

- If `Ranger` feels too soft for a military unit, first buff should be `14 HP` before raising `AT`.
- If `Shocktrooper` is too efficient for a 4-copy common military, first nerf should be `15 HP` or `12 Cost`.
- Free Haven military should feel capable, but still not equal Iron Citadel in raw front-line brutality.

### Military Type B

Locked in as the backbone military counterpart pair that appears `5` times per deck.

| City | Card Name | HP | AT | Cost | Attack Range | Movement Range | Copies |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Free Haven | Sentinel | 12 | 4 | 12 | 1 | 2 | 5 |
| Iron Citadel | Vanguard | 14 | 5 | 14 | 1 | 1 | 5 |

Current reasoning:

- `Sentinel` gives Free Haven a proper midline military body that feels more tactical than brutal: solid stats, but its edge comes from movement rather than raw smashing power.
- `Vanguard` is Iron Citadel's workhorse assault unit: heavier, meaner, and better at winning direct board fights.
- This keeps the city split clear at the military level too: Free Haven repositions better, Iron Citadel punches harder.
- Type B should feel like the most common serious military body in each deck, so this pair is intentionally more central and dependable than Type A.

Open balance notes:

- If `Sentinel` feels too slippery for a 5-copy card, first nerf should be `1 movement` before touching `AT`.
- If `Vanguard` feels too stat-dense for its cost, first nerf should be `18 HP` or `15 Cost`.
- Military Type B should be stronger than every civilian body, but still leave visible room above it for Military Type C.

### Military Type C

Locked in as the heavy military counterpart pair that appears `2` times per deck.

| City | Card Name | HP | AT | Cost | Attack Range | Movement Range | Copies |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Free Haven | Warden | 14 | 5 | 16 | 2 | 1 | 2 |
| Iron Citadel | Juggernaut | 17 | 6 | 18 | 1 | 1 | 2 |

Current reasoning:

- `Warden` gives Free Haven a true elite military body without trying to out-brute Iron Citadel. Its power comes from reach and discipline rather than raw mass.
- `Juggernaut` is Iron Citadel at full volume: expensive, brutal, and built to dominate direct engagements.
- This keeps the same city contrast all the way up the military ladder: Free Haven gets positional strength, Iron Citadel gets crushing front-line force.
- Type C should feel elite and exciting, but still not be the final ceiling of the deck because the Champion still needs to stand above it.

Open balance notes:

- If `Warden` feels too efficient with `2 range`, first nerf should be `17 HP` or `17 Cost`.
- If `Juggernaut` feels too oppressive as a repeated heavy, first nerf should be `21 HP` before touching `AT`.
- Military Type C should clearly beat the Type B line in presence, but should not make the Champion feel redundant.

## Champion Roster

Champions are:

- `1` copy per deck
- Burned / removed from the match when killed
- The strongest baseline units in the city roster
- Allowed to carry `1` signature permanent keyword ability by default
- Currently expected to share a movement keyword so their signature slot is not consumed by omni-direction movement

### Champion

Current recommended discussion pair: the city-defining champion counterpart pair.

| City | Card Name | HP | AT | Cost | Attack Range | Movement Range | Permanent Keyword Ability | Copies |
| --- | --- | ---: | ---: | ---: | ---: | ---: | --- | ---: |
| Free Haven | Steward | 17 | 7 | 22 | 2 | 2 | `Gather 2` | 1 |
| Iron Citadel | Marshal | 20 | 8 | 24 | 1 | 2 | `Secure 1 (Cooldown 5)` | 1 |

Current reasoning:

- `Steward` now feels like Free Haven's true rally point: stronger body, better mobility, and an economy-focused permanent keyword that reinforces the city's survival-through-resourcefulness identity.
- `Marshal` now feels more like a conqueror than a buff totem. `Secure 1` with a cooldown gives it a real expansion / occupation identity without letting it swallow the board too fast.
- This split makes the champions more distinct from ordinary ordinance effects: Free Haven compounds resources, Iron Citadel claims ground.
- Their bodies are intentionally above Military Type C, but not so absurd that they replace the need for the rest of the deck.
- Shared champion movement should likely be `Maneuver`: champions may move in any orthogonal direction during Deploy, while still using their normal `Movement Range` stat for distance.

Open balance notes:

- If `Steward` snowballs too hard with `Gather 2`, first nerf should be `Gather 1` before touching its body.
- If `Marshal` still gains too much permanent territory pressure from `Secure 1 (Cooldown 5)`, first nerf should be a stricter trigger or a longer cooldown before touching its body.
- `Secure` should only ever work when the card is physically standing on a neutral Freespace tile that is orthogonally adjacent to the friendly base network.
- `Secure` should only complete if that card remains on that same valid tile for `2` friendly Deploy-start checks.
- Secured tiles should enter weakened instead of at full base strength, so expansion still creates pressure but remains breakable.
- Champions should feel like the city's centerpiece, but still need support and board context to win cleanly.

## Infrastructure Roster

Infrastructure should:

- Be persistent board anchors, not burst tricks
- Use clear spatial language like `on this tile`, `adjacent`, `diagonal`, or `row in front`
- Reward positioning and lane planning more than raw stat stacking
- Stay distinct from ordinances by affecting an area while alive, instead of acting like one-shot unit upgrades

### Infrastructure Type A

Locked first counterpart pair: one defensive Free Haven anchor and one aggressive Iron Citadel anchor.

| City | Card Name | HP | AT | Cost | Effect Scope | Permanent Keyword Ability | Copies |
| --- | --- | ---: | ---: | ---: | --- | --- | ---: |
| Free Haven | Gatehouse | 16 | 0 | 12 | Friendly units on orthogonally adjacent tiles | `Intercept 1` | 1 |
| Iron Citadel | Outpost | 18 | 0 | 14 | Friendly units on orthogonally adjacent tiles | `Garrison (+2 AT)` | 1 |

Current reasoning:

- `Gatehouse` fits Free Haven's identity as a hold-the-line city. It does not make units hit harder; it makes the nearby formation harder to crack.
- `Outpost` fits Iron Citadel's pushy military identity. It turns a lane into a pressure zone and rewards committing troops around it.
- Both effects are spatial and permanent while the structure stands, which makes them feel like true infrastructure instead of disguised ordinances.
- Both effects are local rather than global, so board placement still matters and later ordinances still have room to create surprise swings on individual units.
- `Adjacent` here means only the four orthogonal tiles: up, down, left, and right. Diagonal tiles are not affected unless a future card says so explicitly.

Open balance notes:

- `Intercept 1` on a persistent area is strong, so `Gatehouse` should stay cheaper than a champion but not cheap enough to become automatic every game.
- `Garrison (+2 AT)` is intentionally sharper and more aggressive than `Gatehouse`, so the higher cost and HP help keep the pair feeling fair.
- Early infrastructure should stay readable. One permanent area effect per building is cleaner than stacking multiple passive rules onto the same card.

### Infrastructure Type B

Locked second counterpart pair: one Free Haven economy utility building and one siege-focused Iron Citadel pressure piece.

| City | Card Name | HP | AT | Cost | Effect Scope | Permanent Keyword Ability | Copies |
| --- | --- | ---: | ---: | ---: | --- | --- | ---: |
| Free Haven | Workshop | 14 | 0 | 13 | Friendly Orders and Items while this structure stands | `Discount 1` | 1 |
| Iron Citadel | Smelter | 16 | 0 | 15 | Friendly units in the same column / lane as this structure | `Shatter` | 1 |

Current reasoning:

- `Workshop` gives Free Haven a cleaner support economy piece than another movement building. It helps the city keep layering upgrades and equipment without directly adding raw damage.
- `Smelter` keeps Iron Citadel's lane-siege identity sharp. It helps nearby units crack enemy structures and base lines faster.
- This pair now pushes the factions apart more clearly: one improves your support economy, the other sharpens battlefield demolition.

Open balance notes:

- `Workshop` real meaning: while it is alive, your Orders and Items cost `1` less to play.
- `Smelter` real meaning: while it is alive, friendly units in its lane deal double damage against enemy buildings and enemy base tile health pools. It does nothing extra to unit HP or city health.
- `Same column / lane` means every tile vertically aligned with that structure from one base side to the other.

### Infrastructure Type C

Locked third counterpart pair: one fragile global Free Haven support engine and one Iron Citadel enemy-lane pressure structure.

| City | Card Name | HP | AT | Cost | Effect Scope | Permanent Keyword Ability | Copies |
| --- | --- | ---: | ---: | ---: | --- | --- | ---: |
| Free Haven | Beacon | 10 | 0 | 19 | All friendly units on the board | `Maneuver` | 1 |
| Iron Citadel | Ballista | 12 | 0 | 20 | Enemy units in the same column / lane as this structure at the start of each Attack phase | `Strike 1` | 1 |

Current reasoning:

- `Beacon` gives Free Haven a true boardwide support piece, but it is intentionally fragile. While it stands, every friendly unit can reposition more intelligently without needing to rely on raw damage buffs.
- `Ballista` gives Iron Citadel a hostile pressure structure instead of another friendly aura. It makes one lane feel dangerous for the enemy even before direct combat starts.
- This pair is more varied than the earlier ones because it is no longer just "friendly units in shape X gain stat Y." One affects the whole board, the other punishes enemies in a committed lane.
- Both still stay within the infrastructure identity: permanent effects tied to survival on a base tile, not disposable ordinance tricks.

Open balance notes:

- `Beacon` is powerful because it is global, so its low HP is the main balancing lever. If it proves too reliable, the first nerf should be more cost or less HP.
- `Ballista` should only apply its `Strike 1` ping to enemy units, not structures, base tiles, or city health.
- `Same column / lane` still means every tile vertically aligned with that structure from one base side to the other.
- If `Strike 1` is too light to matter, the first buff should be its timing clarity or cost, not jumping straight to a large damage number.

### Infrastructure Type D

Locked fourth counterpart pair: one Free Haven treasury landmark and one Iron Citadel production engine.

| City | Card Name | HP | AT | Cost | Effect Scope | Permanent Keyword Ability | Copies |
| --- | --- | ---: | ---: | ---: | --- | --- | ---: |
| Free Haven | Granary | 12 | 0 | 18 | Your city at the start of each friendly Deploy phase | `Gather 3` | 1 |
| Iron Citadel | Warforge | 14 | 0 | 19 | All friendly Military cards while this structure stands | `Discount 2` | 1 |

Current reasoning:

- `Granary` fits Free Haven's farming and long-game identity. It does not help win fights directly; it steadily increases the city's ability to field better turns over time.
- `Warforge` fits Iron Citadel's industrial identity. Instead of generating raw coins, it turns military production more efficient and helps the city keep pushing heavier bodies onto the board.
- This pair is money-centric without feeling mirrored. Free Haven gains actual treasury growth, while Iron Citadel gains spending efficiency.
- Because both are persistent while alive, they still feel like infrastructure rather than one-shot ordinance boosts.

Open balance notes:

- `Granary` real meaning: while it is alive, at the start of your Deploy phase you gain `+3` coins before making plays that turn.
- `Warforge` real meaning: while it is alive, your Military cards cost `2` less to deploy. This does not have to affect Civilians, Infrastructure, Champions, Ordinances, or Items unless we later choose to widen it.
- If `+5` base treasury income remains the game standard, this pair should feel impactful without forcing every deck to become economy-first. If economy feels too slow later, first buff should still be the baseline income moving from `+5` to `+6`, not immediate large jumps.
- If `Warforge` creates too much tempo, first nerf should be `Discount 1` or a higher cost rather than changing the identity.

### Infrastructure Type E

Locked fifth counterpart pair: one Free Haven summon engine and one Iron Citadel economy extractor.

| City | Card Name | HP | AT | Cost | Effect Scope | Permanent Keyword Ability | Copies |
| --- | --- | ---: | ---: | ---: | --- | --- | ---: |
| Free Haven | Belfry | 14 | 0 | 17 | Empty orthogonally adjacent tiles every 3 friendly Deploy phases | `Spawn 1` | 1 |
| Iron Citadel | Tax Office | 14 | 0 | 17 | Enemy city treasury at the start of each friendly Deploy phase | `Siphon 1` | 1 |

Current reasoning:

- `Belfry` gives Free Haven a quirky pressure tool that still feels defensive and positional. It does not create a normal body; it creates a disposable attack token only if the board is ready for it.
- `Tax Office` gives Iron Citadel a small but persistent economy squeeze. It supports the city's long pressure game without softening its identity into farming.
- This pair makes the final building slot feel weirder and more memorable than just another aura structure.

Open balance notes:

- `Belfry` real meaning: every `3` friendly Deploy phases, if you do not already control a Belfry Token, it spawns one onto an empty orthogonally adjacent tile. The token cannot be attacked and burns after its attack.
- `Tax Office` real meaning: while it is alive, at the start of each of your Deploy phases, steal `1` coin from the enemy treasury and add it to your own.

## Ordinance Roster

Ordinances are:

- `1` copy each by default unless later balance says otherwise
- One-shot cards that apply a keyword ability to a valid target
- Usually aimed at units, but some can also target infrastructure
- Sent to discard after use

### Ordinance Type A

Locked first counterpart pair: one Free Haven protection decree and one Iron Citadel march decree.

| City | Card Name | Cost | Valid Targets | Applied Keyword Ability | Copies |
| --- | --- | ---: | --- | --- | ---: |
| Free Haven | Shelter Order | 6 | Friendly Unit with no ability or existing `Intercept`; Friendly Infrastructure with existing `Intercept` | `Intercept 1` | 1 |
| Iron Citadel | Marching Orders | 6 | Friendly Unit with no ability or existing `Sprint`; Friendly Infrastructure with existing `Sprint` | `Sprint 1` | 1 |

Current reasoning:

- `Shelter Order` gives Free Haven a clean defensive ordinance that works on plain units and also naturally stacks onto `Gatehouse` if the player wants to deepen that line of protection.
- `Marching Orders` gives Iron Citadel a clean pressure ordinance that helps one unit surge forward faster.
- Both ordinances use numeric keywords, which makes them excellent first examples of the stacking system.
- Both are simple enough to teach the ordinance rules without dragging in extra exceptions too early.

Open balance notes:

- `Shelter Order` real meaning: if placed on a plain unit, that unit now ignores the first damage event it would take each round. If placed on a target that already has `Intercept`, its `Intercept` value increases by `1`.
- `Marching Orders` real meaning: if placed on a plain unit, that unit gets `+1` movement range during Deploy. If placed on a target that already has `Sprint`, its `Sprint` value increases by `1`.
- Because infrastructure can only stack their existing keyword, `Shelter Order` works naturally with `Gatehouse`, while `Marching Orders` now stays focused on unit tempo.
- Under the current rules, these ordinances cannot be placed on units or infrastructure that already carry a different ability.

### Ordinance Type B

Locked second counterpart pair: one Free Haven protection decree and one Iron Citadel levy decree.

| City | Card Name | Cost | Valid Targets | Applied Keyword Ability | Copies |
| --- | --- | ---: | --- | --- | ---: |
| Free Haven | Stand Fast | 7 | Friendly Unit with no ability | `Provoke` | 1 |
| Iron Citadel | War Levy | 7 | Friendly Unit with no ability or existing `Gather`; Friendly Infrastructure with existing `Gather` | `Gather 2` | 1 |

Current reasoning:

- `Stand Fast` gives Free Haven a clean way to turn one plain unit into a deliberate bodyguard / blocker piece. It fits the city's defensive, hold-the-line identity without repeating the deck's existing `Maneuver` saturation.
- `War Levy` gives Iron Citadel a true economy decree without softening the city's identity too much. It reads less like community support and more like extraction under pressure.
- This pair now teaches that ordinances do not all have to be combat-facing. One controls aggression, the other builds long-game treasury.
- Because `Gather` is numeric, `War Levy` can deepen an existing `Gather` source instead of only creating a new one.

Open balance notes:

- `Stand Fast` real meaning: if placed on a plain unit, enemy units that are able to attack that unit must attack it instead of choosing another target. It does not increase the unit's HP or damage by itself; it changes enemy target priority.
- `War Levy` real meaning: if placed on a plain unit, that unit generates `+2` coins for your city at the start of each of your Deploy phases while it remains alive. If placed on a target that already has `Gather`, its `Gather` value increases by `2`.
- Because `Gather` is numeric, `War Levy` can be used either to create an income unit or deepen an existing income source.
- Under the current rules, `Stand Fast` cannot be placed on already-upgraded targets, while `War Levy` may also stack onto valid `Gather` infrastructure.

### Ordinance Type C

Locked third counterpart pair: one Free Haven sabotage decree and one Iron Citadel damage engine.

| City | Card Name | Cost | Valid Targets | Applied Keyword Ability | Copies |
| --- | --- | ---: | --- | --- | ---: |
| Free Haven | Sabotage | 8 | Friendly Unit with no ability | `Shatter` | 1 |
| Iron Citadel | Live Fire | 8 | Friendly Unit with no ability or existing `Strike`; Friendly Infrastructure with existing `Strike` | `Strike 1` | 1 |

Current reasoning:

- `Sabotage` gives Free Haven one true siege ordinance without changing the whole city's defensive identity. It feels more like clever disruption than brute-force demolition.
- `Live Fire` gives Iron Citadel a true pressure ordinance. It can turn a plain unit into a harder-hitting attacker or intensify a `Ballista` lane if the player wants to build around that structure.
- This pair broadens the ordinance suite beyond protection and movement. One grants structure-breaking pressure, the other grants flat damage pressure.
- `Sabotage` stays a non-stacking choice, while `Live Fire` remains a numeric stacker.

Open balance notes:

- `Sabotage` real meaning: if placed on a plain unit, that unit now deals double damage against enemy infrastructure and enemy base tile health pools. It does not deal extra damage to unit HP or city health.
- `Live Fire` real meaning: if placed on a plain unit, that unit deals `+1` flat damage when it attacks. If placed on a target that already has `Strike`, its `Strike` value increases by `1`. On `Ballista`, this would increase the tower's lane ping from `1` to `2` at the start of each Attack phase.
- Because `Shatter` cannot stack, `Sabotage` is only valid on friendly units that currently have no ability.
- Under the current rules, these ordinances cannot be placed on units or infrastructure that already carry a different ability unless their keyword specifically allows stacking.

### Ordinance Type D

Locked fourth counterpart pair: one Free Haven recovery engine and one Iron Citadel breakthrough doctrine.

| City | Card Name | Cost | Valid Targets | Applied Keyword Ability | Copies |
| --- | --- | ---: | --- | --- | ---: |
| Free Haven | Recovery Network | 10 | Friendly Unit with no ability or existing `Salvage`; Friendly Infrastructure with existing `Salvage` | `Salvage 1` | 1 |
| Iron Citadel | Breakthrough Doctrine | 11 | Friendly Unit with no ability | `Breach` | 1 |

Current reasoning:

- `Recovery Network` gives Free Haven an expensive long-game ordinance that focuses on endurance and resource recovery instead of raw board stats. It fits the city's communal, resilient identity.
- `Breakthrough Doctrine` gives Iron Citadel an expensive aggression ordinance that rewards killing through the front line and continuing pressure behind it. It fits the city's forceful military identity.
- This pair uses keyword space we have not touched yet in either infrastructure or ordinances, which keeps the deck language fresh.
- Both feel expensive because they do not just add a small stat edge; they open a new line of play around recursion and overflow damage.

Open balance notes:

- `Recovery Network` real meaning: if placed on a plain unit, at the start of each of your Deploy phases you return `1` card from your discard pile into your deck while that unit remains alive. If placed on a target that already has `Salvage`, its `Salvage` value increases by `1`.
- `Breakthrough Doctrine` real meaning: if placed on a plain unit, whenever that unit kills an enemy unit, any leftover attack damage carries into the next tile or structure in that exact attack direction. If the attack came from the left, the overflow continues left; if from behind, it continues behind; if diagonal attacks are later allowed, it would continue diagonally as well. It does not turn corners, spill sideways, hit the city, or create duplicate `Breach`.
- Because `Salvage` is numeric, `Recovery Network` can stack if we later introduce a `Salvage` infrastructure or another salvage ordinance line.
- Because `Breach` cannot stack, `Breakthrough Doctrine` is only valid on friendly units that currently have no ability.

### Ordinance Type E

Locked fifth counterpart pair: one Free Haven restoration decree and one Iron Citadel expansion decree.

| City | Card Name | Cost | Valid Targets | Applied Keyword Ability | Copies |
| --- | --- | ---: | --- | --- | ---: |
| Free Haven | Restoration Mandate | 10 | Friendly Unit with no ability or existing `Reclaim` | `Reclaim 1` | 1 |
| Iron Citadel | Annexation Orders | 11 | Friendly Unit with no ability or existing `Secure`; Friendly Champion with existing `Secure` | `Secure 1` | 1 |

Current reasoning:

- `Restoration Mandate` gives Free Haven a way to slowly restore broken ground instead of only defending what still stands. It fits the city's rebuilding, reclaiming identity.
- `Annexation Orders` gives Iron Citadel a true frontier-push ordinance. It fits the city's occupying, expansionist identity and naturally speaks to Marshal's existing territory theme.
- This pair finally opens the territorial keyword space without stepping on the earlier ordinance roles of defense, mobility, economy, recursion, or direct damage.
- Both are intentionally expensive because territory control changes the shape of the board, not just one fight.

Open balance notes:

- `Restoration Mandate` real meaning: if placed on a plain unit, then while that unit is alive, when it destroys an enemy base tile, that destroyed tile becomes your friendly base tile instead of neutral Freespace. It does not directly flip an intact enemy base tile and it does not bypass normal base destruction rules.
- `Annexation Orders` real meaning: if placed on a plain unit, then at the start of each of your Deploy phases, while that unit is alive, if it remains on the same connected neutral Freespace tile for `2` Deploy-start checks, that tile becomes a weakened friendly base tile. If placed on Marshal, its existing `Secure` value increases by `1`.
- Because `Reclaim` and `Secure` are numeric, these ordinances can stack with future same-keyword effects if we later add more of them.
- Under the current rules, `Restoration Mandate` and `Annexation Orders` cannot be placed on targets that already carry a different ability.

## Item Roster

Items are:

- `1` copy each by default unless later balance says otherwise
- Attached to `Units` only
- Permanent while attached
- Used to grant stat buffs or stack an existing ability
- Sent to discard when the carrier unit dies

### Item Type A

Locked first counterpart pair: one Free Haven survival item and one Iron Citadel damage item.

| City | Card Name | Cost | Valid Targets | Permanent Effect | Copies |
| --- | --- | ---: | --- | --- | ---: |
| Free Haven | Padded Overcoat | 6 | Friendly Unit with no item | `+6 HP` | 1 |
| Iron Citadel | Tempered Bayonet | 7 | Friendly Unit with no item | `+2 AT` | 1 |

Current reasoning:

- `Padded Overcoat` gives Free Haven a clean durability item that supports the city's protective, keep-things-alive identity without overlapping too hard with ordinance ability space.
- `Tempered Bayonet` gives Iron Citadel a clean aggression item that turns any unit into a more dangerous attacker without needing a whole new ability keyword.
- This pair teaches the basic item language: items can be simple permanent stat buffs, and they live in the item slot rather than the ability slot.
- Because these are stat items, they combine naturally with any valid ordinance-upgraded unit later without muddying the attachment rules.

Open balance notes:

- `Padded Overcoat` real meaning: while attached, the unit permanently has `+6 HP`. That bonus is part of the unit's current health pool for as long as the item stays attached.
- `Tempered Bayonet` real meaning: while attached, the unit permanently has `+2 AT`. That extra attack applies to normal attacks, preview damage, and any direction the unit is allowed to attack in.
- Both items may be attached to units that already have an ability, as long as they do not already carry another item.
- When the carrier dies, the item returns to the discard pile.

### Item Type B

Locked second counterpart pair: one Free Haven defense-enhancing item and one Iron Citadel breakthrough-enhancing item.

| City | Card Name | Cost | Valid Targets | Permanent Effect | Copies |
| --- | --- | ---: | --- | --- | ---: |
| Free Haven | Reinforced Plating | 7 | Friendly Unit with existing `Intercept` and no item | `Intercept +1` | 1 |
| Iron Citadel | Ram Head | 8 | Friendly Unit with existing `Shatter` and no item | `Shatter` support: `+2 AT against infrastructure/base tiles` | 1 |

Current reasoning:

- `Reinforced Plating` gives Free Haven an item that meaningfully deepens a protection plan instead of just adding raw stats again. It rewards committing to an `Intercept` unit.
- `Ram Head` gives Iron Citadel an item that meaningfully deepens a siege plan instead of just adding generic attack again. It rewards committing to a `Shatter` unit.
- This pair teaches the other half of the item system: some items are not broad stat sticks, they are build-around attachments that only matter on the correct carrier.
- Both stay distinct from ordinances because the item occupies the unit's item slot permanently while the unit lives, instead of consuming the ability slot itself.

Open balance notes:

- `Reinforced Plating` real meaning: it can only be attached to a friendly unit that already has `Intercept`. While attached, that unit's `Intercept` value increases by `1`.
- `Ram Head` real meaning: it can only be attached to a friendly unit that already has `Shatter`. While attached, that unit gains `+2 AT` specifically when attacking enemy infrastructure or enemy base tile health pools. It does not gain `+2 AT` against unit HP or city health.
- Both items may only be attached to units with no existing item.
- When the carrier dies, the item returns to the discard pile.

### Item Type C

Locked third counterpart pair: one Free Haven control item and one Iron Citadel kill-conversion item.

| City | Card Name | Cost | Valid Targets | Permanent Effect | Copies |
| --- | --- | ---: | --- | --- | ---: |
| Free Haven | Truce Bell | 7 | Friendly Unit with no item | Cards damaged by this carrier are `Silenced 1` | 1 |
| Iron Citadel | Ash Brand | 7 | Friendly Unit with no item | Units killed by this carrier are `Burned` | 1 |

Current reasoning:

- `Truce Bell` gives Free Haven a clever disruption item instead of more raw range. It rewards landing chip hits by temporarily shutting off enemy rule text.
- `Ash Brand` gives Iron Citadel a brutal finisher item. When it kills, the victim does not recycle back into the war of attrition.
- This pair pushes both decks toward sharper identity play instead of more generic stat growth.

Open balance notes:

- `Truce Bell` real meaning: while attached, any card damaged by this carrier is `Silenced` for `1` turn.
- `Ash Brand` real meaning: while attached, any enemy unit killed by this carrier is burned instead of returning to discard.
- Both items may be attached to units that already have an ability, as long as they do not already carry another item.
- When the carrier dies, the item returns to the discard pile.

### Item Type D

Locked fourth counterpart pair: one premium Free Haven support item and one premium Iron Citadel siege item.

| City | Card Name | Cost | Valid Targets | Permanent Effect | Copies |
| --- | --- | ---: | --- | --- | ---: |
| Free Haven | Guardian's Satchel | 10 | Friendly Unit with no item | `+4 HP and +1 Attack Range` | 1 |
| Iron Citadel | Demolition Rig | 10 | Friendly Unit with no item | Carrier gains `Shatter` | 1 |

Current reasoning:

- `Guardian's Satchel` still gives Free Haven a strong all-purpose support item that helps a unit endure and contribute from safer range.
- `Demolition Rig` gives Iron Citadel a cleaner premium siege item than a mixed stat package. It turns one unit into a dedicated wall-breaker.
- This pair now ends the item suite with one broad support tool and one narrow but high-impact specialist tool.

Open balance notes:

- `Guardian's Satchel` real meaning: while attached, the unit permanently has `+4 HP` and `+1` attack range.
- `Demolition Rig` real meaning: while attached, the carrier deals double damage to enemy buildings and enemy base tile health pools.
- Both items may be attached to units that already have an ability, as long as they do not already carry another item.
- When the carrier dies, the item returns to the discard pile.
