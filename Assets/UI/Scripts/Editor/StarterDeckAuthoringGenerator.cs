using System.Collections.Generic;
using LastFreeCity.Gameplay;
using LastFreeCity.UI;
using UnityEditor;
using UnityEngine;

namespace LastFreeCity.EditorTools
{
    [InitializeOnLoad]
    public static class StarterDeckAuthoringGenerator
    {
        private const string CardFolder = "Assets/UI/CardTemplates/Starter";
        private const string FreeDeckPath = "Assets/UI/GameData/Decks/Deck_FreeHaven.asset";
        private const string IronDeckPath = "Assets/UI/GameData/Decks/Deck_IronCitadel.asset";

        static StarterDeckAuthoringGenerator()
        {
            EditorApplication.delayCall += RebuildIfStarterDecksAreStale;
        }

        [MenuItem("Last Free City/Authoring/Rebuild Starter Decks")]
        public static void RebuildStarterDecks()
        {
            EnsureFolder("Assets/UI/CardTemplates", "Starter");

            List<CardTemplate> freeHavenCards = new List<CardTemplate>();
            List<CardTemplate> ironCitadelCards = new List<CardTemplate>();

            BuildFreeHaven(freeHavenCards);
            BuildIronCitadel(ironCitadelCards);

            UpdateDeck(FreeDeckPath, "deck.free_haven", "FREE HAVEN DECK", freeHavenCards);
            UpdateDeck(IronDeckPath, "deck.iron_citadel", "IRON CITADEL DECK", ironCitadelCards);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Starter decks rebuilt from CodexDocs/StarterDeckCardRoster.md.");
        }

        private static void RebuildIfStarterDecksAreStale()
        {
            if (!IsDeckStale(FreeDeckPath, "card.free_haven.farmer") && !IsDeckStale(IronDeckPath, "card.iron_citadel.worker"))
            {
                return;
            }

            RebuildStarterDecks();
        }

        private static bool IsDeckStale(string path, string expectedFirstCardId)
        {
            DeckDefinition deck = AssetDatabase.LoadAssetAtPath<DeckDefinition>(path);
            if (deck == null
                || deck.cards == null
                || deck.cards.Count != 32
                || deck.cards[0] == null
                || deck.cards[0].cardId != expectedFirstCardId)
            {
                return true;
            }

            if (expectedFirstCardId == "card.free_haven.farmer")
            {
                CardTemplate ranger = deck.cards.Find(card => card != null && card.cardId == "card.free_haven.ranger");
                CardTemplate gatehouse = deck.cards.Find(card => card != null && card.cardId == "card.free_haven.gatehouse");
                CardTemplate workshop = deck.cards.Find(card => card != null && card.cardId == "card.free_haven.workshop");
                CardTemplate truceBell = deck.cards.Find(card => card != null && card.cardId == "card.free_haven.truce_bell");
                CardTemplate belfry = deck.cards.Find(card => card != null && card.cardId == "card.free_haven.belfry");
                return ranger == null || ranger.treasuryCost != 11 || ranger.health != 10
                    || gatehouse == null || gatehouse.health != 16
                    || workshop == null
                    || truceBell == null
                    || belfry == null;
            }

            CardTemplate worker = deck.cards.Find(card => card != null && card.cardId == "card.iron_citadel.worker");
            CardTemplate outpost = deck.cards.Find(card => card != null && card.cardId == "card.iron_citadel.outpost");
            CardTemplate taxOffice = deck.cards.Find(card => card != null && card.cardId == "card.iron_citadel.tax_office");
            CardTemplate ashBrand = deck.cards.Find(card => card != null && card.cardId == "card.iron_citadel.ash_brand");
            CardTemplate demolitionRig = deck.cards.Find(card => card != null && card.cardId == "card.iron_citadel.demolition_rig");
            return worker == null || worker.attack != 2 || worker.range != 2 || worker.treasuryCost != 10 || worker.health != 11
                || outpost == null || outpost.health != 18
                || taxOffice == null
                || ashBrand == null
                || demolitionRig == null;
        }

        private static void BuildFreeHaven(List<CardTemplate> deck)
        {
            AddCopies(deck, Unit("card.free_haven.farmer", "Farmer", 9, 2, 6, 1, 2, UnitTag.Civilian, "new/characters/farmer_doodle.png"), 2);
            AddCopies(deck, Unit("card.free_haven.gatherer", "Gatherer", 8, 2, 8, 2, 1, UnitTag.Civilian, "new/characters/gatherer_doodle.png"), 3);
            AddCopies(deck, Unit("card.free_haven.homesteader", "Homesteader", 13, 3, 11, 1, 1, UnitTag.Civilian, "new/characters/homesteader_doodle.png"), 1);

            AddCopies(deck, Unit("card.free_haven.ranger", "Ranger", 10, 3, 11, 2, 1, UnitTag.Military, "new/characters/ranger_doodle.png"), 4);
            AddCopies(deck, Unit("card.free_haven.sentinel", "Sentinel", 12, 4, 12, 1, 2, UnitTag.Military, "new/characters/sentinel_doodle.png"), 5);
            AddCopies(deck, Unit("card.free_haven.warden", "Warden", 14, 5, 16, 2, 1, UnitTag.Military, "new/characters/warden_doodle.png"), 2);

            CardTemplate steward = Unit("card.free_haven.steward", "Steward", 17, 7, 22, 2, 2, UnitTag.Special, "new/characters/high_steward_doodle.png");
            steward.keywordEffects.Add(Effect(AbilityKeyword.Gather, 2, "Gather 2: gain +2 coins at Deploy.", "At the start of each friendly Deploy phase, gain +2 coins while Steward is on the board."));
            steward.keywordEffects.Add(Effect(AbilityKeyword.Maneuver, 1, "Maneuver: move orthogonally.", "Steward may move orthogonally in any direction during Deploy using its movement range."));
            AddCopies(deck, steward, 1);

            AddCopies(deck, Infrastructure("card.free_haven.gatehouse", "Gatehouse", 16, 12, "new/infrastructure/gatehouse_doodle.png", Effect(AbilityKeyword.Intercept, 1, "Adjacent allies gain Intercept 1.", "Friendly units on orthogonally adjacent tiles block the first damage event each round while Gatehouse stands.")), 1);
            AddCopies(deck, Infrastructure("card.free_haven.workshop", "Workshop", 14, 13, "new/new/infrastructure/workshop_doodle.png", Effect(AbilityKeyword.Discount, 1, "Orders and items cost 1 less.", "Friendly Order and Item cards cost 1 less to play while Workshop stands.")), 1);
            AddCopies(deck, Infrastructure("card.free_haven.beacon", "Beacon", 10, 19, "new/infrastructure/beacon_doodle.png", Effect(AbilityKeyword.Maneuver, 1, "All allies gain Maneuver.", "All friendly units may move orthogonally in any direction while Beacon stands.")), 1);
            AddCopies(deck, Infrastructure("card.free_haven.granary", "Granary", 12, 18, "new/infrastructure/granary_doodle.png", Effect(AbilityKeyword.Gather, 3, "Gather 3: gain +3 coins.", "At the start of each friendly Deploy phase, gain +3 coins while Granary stands.")), 1);
            AddCopies(deck, Infrastructure("card.free_haven.belfry", "Belfry", 14, 17, "new/new/infrastructure/belfry_doodle.png", Effect(AbilityKeyword.Spawn, 1, "Every 3 friendly Deploys, spawn a Belfry Token.", "Every 3 friendly Deploy phases, if no Belfry Token is already on the board, Belfry spawns one on an empty orthogonally adjacent tile. The token cannot be attacked and burns after its attack.")), 1);

            AddCopies(deck, Ordinance("card.free_haven.shelter_order", "Shelter Order", 6, "new/ordinance/emergency_shelter_order_doodle.png", Effect(AbilityKeyword.Intercept, 1, "Add Intercept 1.", "Give a friendly plain unit Intercept 1, or stack +1 Intercept onto an existing Intercept target.")), 1);
            AddCopies(deck, Ordinance("card.free_haven.stand_fast", "Stand Fast", 7, "new/ordinance/stand_fast_order_doodle.png", Effect(AbilityKeyword.Provoke, 1, "Add Provoke.", "Give a friendly plain unit Provoke. Enemies that can attack it must do so.")), 1);
            AddCopies(deck, Ordinance("card.free_haven.sabotage", "Sabotage", 8, "new/ordinance/demolition_orders_doodle.png", Effect(AbilityKeyword.Shatter, 1, "Add Shatter.", "Give a friendly plain unit Shatter. It deals double damage to buildings and base tiles.")), 1);
            AddCopies(deck, Ordinance("card.free_haven.recovery_network", "Recovery Network", 10, "new/ordinance/recovery_network_doodle.png", Effect(AbilityKeyword.Salvage, 1, "Add Salvage 1.", "At Deploy start, return 1 random discard card to the deck while this upgraded card is alive.")), 1);
            AddCopies(deck, Ordinance("card.free_haven.restoration_mandate", "Restoration Mandate", 10, "new/ordinance/restoration_mandate_doodle.png", Effect(AbilityKeyword.Reclaim, 1, "Add Reclaim 1.", "The next enemy base tile this unit destroys becomes your base instead of neutral Freespace.")), 1);

            AddCopies(deck, Item("card.free_haven.padded_overcoat", "Padded Overcoat", 6, "new/items/padded_overcoat_doodle.png", 6, 0, 0, 0, 0, null), 1);
            AddCopies(deck, Item("card.free_haven.reinforced_plating", "Reinforced Plating", 7, "new/items/reinforced_plating_doodle.png", 0, 0, 0, 0, 0, Effect(AbilityKeyword.Intercept, 1, "Intercept +1.", "Attach only to a unit with Intercept. Increase its Intercept value by 1.")), 1);
            CardTemplate truceBell = Item("card.free_haven.truce_bell", "Truce Bell", 7, "new/new/items/truce_bell_doodle.png", 0, 0, 0, 0, 0, Effect(AbilityKeyword.Silence, 1, "Silence cards it damages.", "Attach to a friendly unit with no item. Cards damaged by this carrier are Silenced for 1 turn.")); 
            truceBell.abilityText = "Attach to a friendly unit with no item. Cards damaged by this carrier are Silenced for 1 turn.";
            truceBell.detailedAbilityText = truceBell.abilityText;
            AddCopies(deck, truceBell, 1);
            AddCopies(deck, Item("card.free_haven.guardians_satchel", "Guardian's Satchel", 10, "new/items/guardians_satchel_doodle.png", 4, 0, 1, 0, 0, null), 1);
        }

        private static void BuildIronCitadel(List<CardTemplate> deck)
        {
            AddCopies(deck, Unit("card.iron_citadel.worker", "Worker", 11, 2, 10, 2, 1, UnitTag.Civilian, "new/characters/worker_doodle.png"), 2);
            AddCopies(deck, Unit("card.iron_citadel.foreman", "Foreman", 12, 3, 10, 1, 1, UnitTag.Civilian, "new/characters/foreman_doodle.png"), 3);
            AddCopies(deck, Unit("card.iron_citadel.overseer", "Overseer", 15, 4, 11, 1, 1, UnitTag.Civilian, "new/characters/overseer_doodle.png"), 1);

            AddCopies(deck, Unit("card.iron_citadel.shocktrooper", "Shocktrooper", 12, 4, 11, 1, 1, UnitTag.Military, "new/characters/shock_trooper_doodle.png"), 4);
            AddCopies(deck, Unit("card.iron_citadel.vanguard", "Vanguard", 14, 5, 14, 1, 1, UnitTag.Military, "new/characters/vanguard_doodle.png"), 5);
            AddCopies(deck, Unit("card.iron_citadel.juggernaut", "Juggernaut", 17, 6, 18, 1, 1, UnitTag.Military, "new/characters/juggernaut_doodle.png"), 2);

            CardTemplate marshal = Unit("card.iron_citadel.marshal", "Marshal", 20, 8, 24, 1, 2, UnitTag.Special, "new/characters/iron_marshal_doodle.png");
            marshal.keywordEffects.Add(Effect(AbilityKeyword.Secure, 1, "Secure 1 every 5 deploys.", "Every fifth friendly Deploy phase after Marshal enters play, if it remains on the same connected Freespace tile for 2 checks, that tile becomes a weakened base tile in your network."));
            marshal.keywordEffects.Add(Effect(AbilityKeyword.Maneuver, 1, "Maneuver: move orthogonally.", "Marshal may move orthogonally in any direction during Deploy using its movement range."));
            AddCopies(deck, marshal, 1);

            AddCopies(deck, Infrastructure("card.iron_citadel.outpost", "Outpost", 18, 14, "new/infrastructure/outpost_doodle.png", Effect(AbilityKeyword.Garrison, 2, "Adjacent allies gain +2 AT.", "Friendly units on orthogonally adjacent tiles gain +2 AT during Attack while Outpost stands.")), 1);
            AddCopies(deck, Infrastructure("card.iron_citadel.smelter", "Smelter", 16, 15, "new/infrastructure/smelter_doodle.png", Effect(AbilityKeyword.Shatter, 1, "Same-lane allies gain Shatter.", "Friendly units in this column deal double damage to buildings and base tiles while Smelter stands.")), 1);
            AddCopies(deck, Infrastructure("card.iron_citadel.ballista", "Ballista", 12, 20, "new/infrastructure/ballista_tower_doodle.png", Effect(AbilityKeyword.Strike, 1, "Strike 1 in lane.", "At the start of friendly Attack planning, deal 1 damage to enemy units in this column.")), 1);
            AddCopies(deck, Infrastructure("card.iron_citadel.warforge", "Warforge", 14, 19, "new/infrastructure/war_forge_doodle.png", Effect(AbilityKeyword.Discount, 2, "Military cards cost 2 less.", "Friendly Military cards cost 2 less to deploy while Warforge stands.")), 1);
            AddCopies(deck, Infrastructure("card.iron_citadel.tax_office", "Tax Office", 14, 17, "new/new/infrastructure/tax_office_doodle.png", Effect(AbilityKeyword.Siphon, 1, "Siphon 1 at Deploy start.", "At the start of each friendly Deploy phase, steal 1 coin from the enemy city while Tax Office stands.")), 1);

            AddCopies(deck, Ordinance("card.iron_citadel.marching_orders", "Marching Orders", 6, "new/ordinance/marching_orders_doodle.png", Effect(AbilityKeyword.Sprint, 1, "Add Sprint 1.", "Give a friendly plain unit Sprint 1, or stack +1 Sprint onto an existing Sprint target.")), 1);
            AddCopies(deck, Ordinance("card.iron_citadel.war_levy", "War Levy", 7, "new/ordinance/community_collection_doodle.png", Effect(AbilityKeyword.Gather, 2, "Add Gather 2.", "Give a friendly plain unit Gather 2, or stack +2 Gather onto an existing Gather target.")), 1);
            AddCopies(deck, Ordinance("card.iron_citadel.live_fire", "Live Fire", 8, "new/ordinance/live_fire_doctrine_doodle.png", Effect(AbilityKeyword.Strike, 1, "Add Strike 1.", "Give a friendly plain unit +1 flat attack damage, or stack +1 Strike onto an existing Strike target.")), 1);
            AddCopies(deck, Ordinance("card.iron_citadel.breakthrough_doctrine", "Breakthrough Doctrine", 11, "new/ordinance/breakthrough_doctrine_doodle.png", Effect(AbilityKeyword.Breach, 1, "Add Breach.", "Give a friendly plain unit Breach. Excess unit-kill damage carries forward in the attack direction.")), 1);
            AddCopies(deck, Ordinance("card.iron_citadel.annexation_orders", "Annexation Orders", 11, "new/ordinance/annexation_orders_doodle.png", Effect(AbilityKeyword.Secure, 1, "Add Secure 1.", "At Deploy start, if this upgraded card remains on the same connected Freespace tile for 2 checks, that tile becomes a weakened base tile.")), 1);

            AddCopies(deck, Item("card.iron_citadel.tempered_bayonet", "Tempered Bayonet", 7, "new/items/tempered_bayonet_doodle.png", 0, 2, 0, 0, 0, null), 1);
            AddCopies(deck, Item("card.iron_citadel.ram_head", "Ram Head", 8, "new/items/ram_head_doodle.png", 0, 0, 0, 0, 2, null), 1);
            CardTemplate ashBrand = Item("card.iron_citadel.ash_brand", "Ash Brand", 7, "new/new/other/ash_brand_doodle.png", 0, 0, 0, 0, 0, Effect(AbilityKeyword.Burn, 1, "Kills are burned.", "Attach to a friendly unit with no item. Units killed by this carrier are burned instead of returning to discard.")); 
            ashBrand.abilityText = "Attach to a friendly unit with no item. Units killed by this carrier are burned instead of returning to discard.";
            ashBrand.detailedAbilityText = ashBrand.abilityText;
            AddCopies(deck, ashBrand, 1);
            CardTemplate demolitionRig = Item("card.iron_citadel.demolition_rig", "Demolition Rig", 10, "new/new/other/demolition_rig_doodle.png", 0, 0, 0, 0, 0, Effect(AbilityKeyword.Shatter, 1, "Gain Shatter.", "Attach to a friendly unit with no item. This carrier deals double damage to buildings and base tiles."));
            demolitionRig.abilityText = "Attach to a friendly unit with no item. This carrier deals double damage to buildings and base tiles.";
            demolitionRig.detailedAbilityText = demolitionRig.abilityText;
            AddCopies(deck, demolitionRig, 1);
        }

        private static CardTemplate Unit(string id, string name, int hp, int at, int cost, int range, int move, UnitTag tag, string spritePath)
        {
            CardTemplate card = UpsertCard(id, name);
            card.cardType = CardType.Unit;
            card.unitTag = tag;
            card.infrastructureKind = InfrastructureKind.None;
            SetStats(card, hp, at, cost, range, move, spritePath);
            card.abilityText = string.Empty;
            card.detailedAbilityText = "Plain unit slate. Upgrade with orders or items.";
            return card;
        }

        private static CardTemplate Infrastructure(string id, string name, int hp, int cost, string spritePath, AbilityEffectData effect)
        {
            CardTemplate card = UpsertCard(id, name);
            card.cardType = CardType.Infrastructure;
            card.unitTag = UnitTag.None;
            card.infrastructureKind = InfrastructureKind.Building;
            SetStats(card, hp, 0, cost, 0, 0, spritePath);
            card.keywordEffects.Add(effect);
            return card;
        }

        private static CardTemplate Ordinance(string id, string name, int cost, string spritePath, AbilityEffectData effect)
        {
            CardTemplate card = UpsertCard(id, name);
            card.cardType = CardType.Ordinance;
            card.unitTag = UnitTag.None;
            card.infrastructureKind = InfrastructureKind.None;
            SetStats(card, 0, 0, cost, 0, 0, spritePath);
            card.keywordEffects.Add(effect);
            return card;
        }

        private static CardTemplate Item(string id, string name, int cost, string spritePath, int hp, int at, int range, int move, int siegeAt, AbilityEffectData effect)
        {
            CardTemplate card = UpsertCard(id, name);
            card.cardType = CardType.Item;
            card.unitTag = UnitTag.None;
            card.infrastructureKind = InfrastructureKind.None;
            SetStats(card, 0, 0, cost, 0, 0, spritePath);
            card.bonusHealth = hp;
            card.bonusAttack = at;
            card.bonusRange = range;
            card.bonusMovementRange = move;
            card.bonusSiegeAttack = siegeAt;
            if (effect != null)
            {
                card.keywordEffects.Add(effect);
            }
            card.detailedAbilityText = BuildItemDetail(card);
            card.abilityText = card.detailedAbilityText;
            return card;
        }

        private static CardTemplate UpsertCard(string id, string name)
        {
            string path = $"{CardFolder}/{id.Replace('.', '_')}.asset";
            CardTemplate card = AssetDatabase.LoadAssetAtPath<CardTemplate>(path);
            if (card == null)
            {
                card = ScriptableObject.CreateInstance<CardTemplate>();
                AssetDatabase.CreateAsset(card, path);
            }

            card.cardId = id;
            card.cardName = name;
            card.commandCardKind = CommandCardKind.None;
            if (card.keywordEffects == null)
            {
                card.keywordEffects = new List<AbilityEffectData>();
            }
            card.keywordEffects.Clear();
            card.attachedItemCard = null;
            card.bonusHealth = 0;
            card.bonusAttack = 0;
            card.bonusRange = 0;
            card.bonusMovementRange = 0;
            card.bonusSiegeAttack = 0;
            EditorUtility.SetDirty(card);
            return card;
        }

        private static void SetStats(CardTemplate card, int hp, int at, int cost, int range, int move, string spritePath)
        {
            card.health = hp;
            card.attack = at;
            card.treasuryCost = cost;
            card.range = range;
            card.movementRange = move;
            card.customArt = LoadWholeSprite($"Assets/UI/Sprites/{spritePath}");
        }

        private static AbilityEffectData Effect(AbilityKeyword keyword, int value, string shortText, string detailText)
        {
            return new AbilityEffectData
            {
                keyword = keyword,
                value = value,
                trigger = AbilityTrigger.PassiveAura,
                duration = AbilityDuration.Permanent,
                shortDescription = shortText,
                detailedDescription = detailText
            };
        }

        private static string BuildItemDetail(CardTemplate card)
        {
            List<string> parts = new List<string>();
            if (card.bonusHealth > 0) parts.Add($"+{card.bonusHealth} HP");
            if (card.bonusAttack > 0) parts.Add($"+{card.bonusAttack} AT");
            if (card.bonusRange > 0) parts.Add($"+{card.bonusRange} attack range");
            if (card.bonusMovementRange > 0) parts.Add($"+{card.bonusMovementRange} movement");
            if (card.bonusSiegeAttack > 0) parts.Add($"+{card.bonusSiegeAttack} AT against buildings/base tiles");
            return parts.Count > 0
                ? $"Attach to a friendly unit with no item. Grants {string.Join(", ", parts)} until the unit dies."
                : "Attach to a friendly unit with no item.";
        }

        private static void AddCopies(List<CardTemplate> deck, CardTemplate card, int copies)
        {
            for (int i = 0; i < copies; i++)
            {
                deck.Add(card);
            }
        }

        private static void UpdateDeck(string path, string deckId, string displayName, List<CardTemplate> cards)
        {
            DeckDefinition deck = AssetDatabase.LoadAssetAtPath<DeckDefinition>(path);
            if (deck == null)
            {
                deck = ScriptableObject.CreateInstance<DeckDefinition>();
                AssetDatabase.CreateAsset(deck, path);
            }

            deck.deckId = deckId;
            deck.displayName = displayName;
            deck.cards.Clear();
            deck.cards.AddRange(cards);
            EditorUtility.SetDirty(deck);
        }

        private static Sprite LoadWholeSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }
    }
}
