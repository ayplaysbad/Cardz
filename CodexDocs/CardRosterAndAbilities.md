# Card Roster And Keyword Abilities

## Current Direction

- Units and infrastructure are now plain stat cards by default: `Cost`, `HP`, `AT`, type, tags, range, and movement.
- Reusable mechanics live as keyword effects, not as bespoke per-card scripts.
- Ordinances will later choose which city/deck they belong to, which keyword effect they apply, how much they cost, and what they can target.
- Cards can still hold keyword effects directly for future special cases, but the current roster has no attached card abilities.
- The old `CardAbilityDefinition` assets are compatibility shells only. Runtime card rules now read `AbilityKeyword` / `AbilityEffectData`.

## Ordinance Attachment Rules

- Ordinances are one-shot cards that apply a keyword ability to a valid target.
- Ordinances usually target `Units`, but some may explicitly target `Infrastructure`.
- Units may carry at most `1` ability and `1` item at a time.
- Infrastructure may carry at most `1` ability total.
- If a target has no ability yet, a valid ordinance may give it its first ability.
- If a target already has that same ability, the ordinance stacks onto the existing ability instead of creating a duplicate.
- If a target already has a different ability, the ordinance cannot add a second one unless a future rule explicitly allows replacement or exceptions.
- Champions count as units for this rule. Because champions already have a permanent keyword ability, ordinances on them can only stack that same keyword under the current rules.
- Infrastructure already comes with its own permanent ability, so ordinances placed on infrastructure can only stack that existing keyword, never add a brand new one.
- Current stacking assumption: numeric keywords such as `Gather X`, `Strike X`, `Discount X`, `Sprint X`, `Lock X`, `Secure X`, `Salvage X`, and `Intercept X` stack by adding their values together.
- Current stacking assumption: non-numeric keywords such as `Maneuver`, `Provoke`, `Shatter`, `Breach`, and `Silence` cannot stack.
- If we later want a stronger non-numeric effect, we should define a distinct upgraded version explicitly rather than pretending the base keyword stacks.

## Item Attachment Rules

- Items attach to `Units` only. They never attach to `Infrastructure`.
- Champions count as units for item attachment unless a future item says otherwise.
- Units may carry at most `1` item at a time.
- Items either:
  - grant permanent stat buffs while attached, or
  - stack onto an existing ability already on that unit
- Items do not create a second ability slot on a unit.
- If an item interacts with an ability, it must follow the same stack rules as ordinances:
  - numeric keywords stack by value
  - non-numeric keywords cannot stack
- If the carrier unit dies, its attached item returns to the discard pile.
- Items stay attached until the carrier dies or a future effect explicitly removes them.

## Ordinance And Item Targeting UX

- When the player clicks an `Ordinance` card in hand, all valid board targets for that card should highlight immediately.
- When the player clicks an `Item` card in hand, all valid board targets for that card should highlight immediately.
- Invalid targets should not use the same highlight as valid ones.
- If a card can only target units, only valid units highlight.
- If a card can target infrastructure, only valid infrastructure highlights.
- If a card can target both, both categories may highlight, but only where the current attachment rules allow placement.
- The highlight should respect existing ability/item limits. For example, a unit with a different existing ability should not highlight for an ordinance that cannot legally replace it.
- This targeting highlight is part of the gameplay language, not just a visual extra. Players should be able to understand legality before clicking the board.

## Keyword List

- `Gather X`: generate `X` coins for your city when its trigger resolves.
- `Siphon X`: steal `X` coins from the enemy city and add them to yours.
- `Discount X`: reduce deployment cost for a target card/category by `X`.
- `Strike X`: deal `X` flat damage.
- `Shatter`: double damage only against infrastructure/base tile health pools.
- `Breach`: excess damage beyond a killed unit carries into the next tile or structure in the exact direction of that attack. It does not turn corners, fan out, or hit the city unless a future rule explicitly says so.
- `Intercept X`: block/reduce the first `X` incoming damage instances.
- `Secure X`: convert up to `X` directly adjacent up/down/left/right Freespace tiles into friendly base tiles. This should be tied to a trigger or cooldown, not assumed to fire every turn by default.
- `Reclaim X`: when this card destroys an enemy base tile, that destroyed tile becomes your friendly base tile instead of neutral Freespace, up to `X` reclaimed tiles per round. This is a siege-result effect, not a free flip on healthy enemy tiles.
- `Sprint X`: increase movement range by `X` for the relevant turn/phase.
- `Maneuver`: this unit may move in any orthogonal direction during Deploy instead of only moving forward. Distance still comes from the unit's normal movement range stat.
- `Provoke`: enemy units in the effect scope must attack this card if able, and cannot choose other attack targets while the effect applies.
- `Lock X`: prevent movement, attacks, and ability use/receiving for `X` turns.
- `Silence X`: disable active/passive keyword effects for `X` turns without changing base stats.
- `Garrison`: grant a localized stat aura to friendly units on/adjacent to the source tile.
- `Spawn Card`: place a specified card/token directly onto the board.
- `Burn`: remove the card from the match instead of sending it to discard.
- `Salvage X`: return `X` cards from permanent discard into hand or deck.

## Active Plain Decks

Canonical baseline names, stats, ranges, copy counts, ordinances, and items now live in `StarterDeckCardRoster.md`.

This file should stay focused on:

- keyword definitions
- attachment rules
- targeting rules
- health model and runtime behavior notes

Do not maintain a second duplicate card stat list here.

## Health Model

- City health is its own pool.
- Each base tile has its own health pool.
- When infrastructure is placed onto a base tile, the infrastructure and tile merge into one combined tile health pool.
- When that merged infrastructure tile pool reaches zero, both the infrastructure and the base tile are destroyed, and the tile becomes neutral Freespace.
- Unit HP remains separate from base tile HP.

## Implementation Notes

- The temporary `Lock` testing card now carries a `Lock` keyword effect, but still uses the existing command-card placement path for now.
- Old passive effects like Farmer income, Gatherer discounts, Outpost aura, Warden intercept, and Shocktrooper breach no longer fire from the plain cards.
- Future ordinance work should add runtime statuses/effects rather than adding card-specific checks back into `UIManager`.
