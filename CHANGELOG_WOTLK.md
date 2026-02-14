# Singular WotLK  Changelog

All changes made to port Singular from Cataclysm 4.3.4 to WoW 3.3.5a (WotLK).

---

## QC Audit Phase 6 — Full Codebase Quality Audit (2026-02-09)

Complete audit of ALL .cs files in the Singular WotLK routine. Checked every spell name, spell ID, boss ID, talent reference, settings property, and syntax pattern against WotLK 3.3.5a.

### Files Audited

- **ClassSpecific**: All 10 classes (50 files) — DK, Druid, Hunter, Mage, Paladin, Priest, Rogue, Shaman, Warlock, Warrior
- **Settings**: All 11 files — SingularSettings.cs + 10 class settings
- **Infrastructure**: BossList.cs, CataHeroicDpsList.cs, TalentManager.cs, MountManager.cs, Helpers/Common.cs, Helpers/Rest.cs, SingularRoutine.cs, Generic.cs

### MUST-FIX (Gameplay Bugs — Applied)

| File | Issue | Fix |
|------|-------|-----|
| **Druid/Balance.cs** (3 rotations, 6 locations) | Force of Nature gated behind Eclipse (Solar), Starfall behind Eclipse (Lunar) — **Cata mechanic**. In WotLK Eclipse only buffs Wrath damage or Starfire crit, NOT Nature/Arcane schools. Treants deal physical damage. | Removed all Eclipse gates from Force of Nature and Starfall. Cast on cooldown (Normal AoE) or on boss (Instance). |
| **Druid/Feral.cs** (PvP + Instance) | "Blood in the Water" FB logic at ≤25% HP — **Cata mechanic**. In WotLK, Ferocious Bite does NOT refresh Rip. Priority #9/#10 prevented Rip reapplication in 21–25% window. | Replaced with simple execute logic: FB at 5CP below 20%, Rip maintenance above 20% via existing #11. |
| **Rogue/Assassination.cs** (all 3 rotations) | **Missing Hunger for Blood** — 51-point WotLK Assassination talent (spell 51662). Removed in Cata (replaced by Vendetta). +5% damage self-buff requiring bleed on target. Must be maintained. | Added `Spell.BuffSelf("Hunger for Blood")` gated behind bleed on target (Rupture/Garrote). |
| **Rogue/Assassination.cs, Combat.cs, Subtlety.cs** (Instance rotations) | Fan of Knives gated behind `Item.RangedIsType(WoWItemWeaponClass.Thrown)` — **Cata leftover**. In WotLK, FoK works with any ranged weapon (bow, gun, thrown). | Removed thrown weapon requirement from FoK in all 3 specs. |
| **Shaman/Enhancement.cs** (Normal) | Fire Nova gated behind `HasMyAura("Flame Shock")` on target — **Cata mechanic**. In WotLK, Fire Nova activates your fire totem to emit an AoE pulse, NO Flame Shock interaction. | Changed to check for active fire totem (Searing/Magma/Flametongue) instead of Flame Shock. |
| **Shaman/Enhancement.cs** (Normal) | Flame Shock only applied in AoE to "enable Fire Nova" — **Cata synergy logic**. In WotLK Flame Shock is valuable as a standalone DoT. | Removed Fire Nova availability check from Flame Shock condition. Apply on elites always. |
| **Shaman/Enhancement.cs** (Instance AoE) | Lava Lash gated behind `HasMyAura("Flame Shock")` on target — **Cata mechanic**. In Cata, Lava Lash spread Flame Shock. In WotLK, Lava Lash bonus comes from Flametongue Weapon on offhand. | Removed Flame Shock check. Gate on offhand weapon presence only. |
| **Warlock/Destruction.cs** (all 3 rotations) | `Soul Fire` cast when `HasAura("Empowered Imp")` — **Cata mechanic**. In Cata, Empowered Imp made Soul Fire instant. In WotLK, Empowered Imp gives +100% crit on next spell — Soul Fire stays a ~6s hard cast. Better to let the proc be consumed by the next Incinerate/Conflagrate (~2s). | Removed all 3 Soul Fire + Empowered Imp lines from combat rotations. Kept Soul Fire in pull openers only. |

### SHOULD-FIX (Dead Code & Comment Cleanup — Applied)

| File | Issue | Fix |
|------|-------|-----|
| **DK/Unholy.cs** (4 locations) | Comments say "Blood Strike for Desolation" — Desolation is a deep Blood talent (Tier 7), unreachable by standard 17/0/54 Unholy builds. Blood Strike is used for Death Rune conversion via **Reaping** (Unholy Tier 5). | Changed all 4 comments to reference Reaping instead of Desolation. |
| **Shaman/Restoration.cs** | T12 (Firelands, Cata 4.2) constants, dead `NumTier12Pieces` property, and T12 comments in healing methods. | Removed dead T12 constants and property. Fixed misleading T12 2pc/4pc comments. |
| **Paladin/Retribution.cs** | T13 (Dragon Soul, Cata 4.3) dead code: `RET_T13_ITEM_SET_ID`, `NumTier13Pieces`, `Has2PieceTier13Bonus` — never referenced. | Removed all T13 dead code. |
| **Settings/WarriorSettings.cs** | `UseWarriorT12` and `UseWarriorSMF` settings — Cata abilities, orphaned (no code reads them). | Commented out both properties entirely. |
| **Settings/DruidSettings.cs** | `TreeOfLifeHealth` and `TreeOfLifeCount` — Cata cooldown thresholds. WotLK Tree of Life is permanent form. Orphaned. | Commented out both properties. |

### Verified Clean (No Issues Found)

| Class | Files | Status |
|-------|-------|--------|
| Death Knight | Blood, Frost, Unholy, Common, Lowbie | ✅ All spells WotLK-valid |
| Druid | Resto, Common, Lowbie | ✅ Tree of Life as permanent form, QC comments correct |
| Hunter | BeastMaster, Marksman, Survival, Common, Lowbie | ✅ Uses ManaPercent, no Focus. All trap IDs correct. Pet happiness checks correct. |
| Mage | Arcane, Fire, Frost, Common, Lowbie | ✅ Hot Streak, Improved Scorch (not Critical Mass), Fire Ward (not Mage Ward) |
| Paladin | Holy, Protection, Common, Lowbie | ✅ WotLK seals/judgements, 969 rotation, no Holy Power |
| Priest | Discipline, Holy, Shadow, Common, Lowbie | ✅ No Chakra/Mind Spike/Leap of Faith, "Heal" correctly mapped to Greater Heal |
| Rogue | Combat, Subtlety, Common, Lowbie, Poisons | ✅ All poison IDs correct, Killing Spree correct |
| Shaman | Elemental, Totems, Lowbie | ✅ Fulmination correctly removed, Moonkin Aura check correctly commented |
| Warlock | Affliction, Demonology, Common, Lowbie | ✅ "Curse of" naming (not "Bane of"), Decimation correct, no Fel Flame |
| Warrior | Arms, Fury, Protection, Common, Lowbie | ✅ No Colossus Smash, Blood and Thunder correctly disabled |

### Infrastructure Verified Clean

| File | Status |
|------|--------|
| BossList.cs | ✅ All 1014 IDs are Classic/TBC/WotLK (one minor data bug: ID 17808 labeled Anger'rel is actually Anetheron, duplicate entry — harmless in HashSet) |
| CataHeroicDpsList.cs | ✅ Entirely commented out, not referenced |
| TalentManager.cs | ✅ WotLK 3-tree system, Lua indices correct, Monk commented out |
| MountManager.cs | ✅ Cata mount event stubbed, Ghost Wolf + Travel Form correct |
| Helpers/Common.cs | ✅ All 14 interrupt spells WotLK-valid, Gag Order index corrected |
| Helpers/Rest.cs | ✅ Standard eat/drink, Cannibalize correct |
| SingularRoutine.cs | ✅ No LFR, no Monk, correct WotLK context detection |
| Generic.cs | ✅ All code commented out |
| Settings/SingularSettings.cs | ✅ All settings version-agnostic |
| All other Settings | ✅ Clean (see orphaned settings note below) |

### Observations (Not Fixed — Low Priority)

| Category | Details |
|----------|---------|
| **Orphaned Settings** | `PriestSettings.DontShadowFormHealth`, `PriestSettings.DpsMana`, `ShamanSettings.CastOn`, `ShamanSettings.IncludeAoeRotation`, `RogueSettings.InterruptSpells`, `RogueSettings.UseExposeArmor` — defined but never read by any rotation code. Harmless but could be wired up or removed. |
| **PaladinAura.Resistance** | Enum maps to "Shadow Resistance Aura" only; WotLK has 3 separate resistance auras. Low priority UX issue. |
| **Warrior Prot interrupt** | `CreateInterruptSpellCast` may only use Pummel (requires Berserker Stance in WotLK). Prot should use Shield Bash instead — depends on helper implementation. Worth verifying in Helpers/Common.cs. |
| **Hunter Common.cs trap IDs** | Hardcoded max-rank trap IDs (e.g., Immolation Trap Rank 8 = level 78). Leveling hunters below those levels will fail to cast. Not a Cata issue — a rank assumption. |

### QC Verification (Post-Audit Self-Review)

All changes verified against wowhead.com/wotlk. Found and corrected 3 errors in the original audit:

| Error | Details | Correction |
|-------|---------|------------|
| **Overkill talent index** | Initial audit incorrectly changed `GetCount(1, 19)` → `GetCount(0, 19)`. TalentManager uses **1-based tabs** (matching Lua `GetTalentInfo`). Tab 1 = Assassination for Rogues. `GetCount(0, 19)` would match nothing. | **Reverted** to original `GetCount(1, 19)`. Removed from MUST-FIX table. |
| **HfB percentage** | Changelog originally stated "+15% damage". Wowhead 3.3.5a shows +5% (post-3.1.0 nerf). | Fixed to +5% in changelog. Code unaffected (just calls `Spell.BuffSelf`). |
| **Empowered Imp description** | Changelog originally stated "+20% crit chance". Wowhead shows the proc gives **+100% crit on next spell** (8s duration). | Fixed description in changelog and code comments. Removal still correct — 6s Soul Fire wastes the proc vs ~2s Incinerate. |

---

## QC Audit Phase 4 — Deep Logic Audit (Cata Talent Indices & Tank Spec)

Full-codebase sweep for subtle Cata/MoP/WoD logic remnants beyond spell names. Checked 45 expansion-specific patterns across all .cs files. Found 4 bugs: 3 wrong Cata talent tree indices and 1 wrong DK tank spec.

### Talent Index Verification Method

WoW `GetTalentInfo(tab, index)` enumerates talents by tier (row), then column (left→right) within each tier. Cata 4.0.1 reorganized ALL talent trees, so indices from Cata Singular code map to DIFFERENT talents in WotLK. Cross-verified against known-correct `(2, 6) = Improved Ghost Wolf` in MountManager.cs.

### MUST-FIX

| File | Issue | Fix |
|------|-------|-----|
| Helpers/Group.cs L30 | `MeIsTank` listed `FrostDeathKnight` as tank spec — wrong for WotLK (Blood = tank, Frost = DPS) | `FrostDeathKnight` → `BloodDeathKnight` |

### SHOULD-FIX (Talent Index Corrections)

| File | Issue | Fix |
|------|-------|-----|
| Druid/Balance.cs (3 locations) | `GetCount(1, 1)` for Nature's Grace — Index 1 = Starlight Wrath in WotLK (Cata moved NG to tier 1) | `GetCount(1, 1)` → `GetCount(1, 7)` (Nature's Grace = Balance Tier 3) |
| Rogue/Assassination.cs (2 locations) | `GetCount(1, 14)` for Overkill — Index 14 = Improved Kidney Shot in WotLK (Cata moved Overkill to tier 5) | `GetCount(1, 14)` → `GetCount(1, 19)` (Overkill = Assassination Tier 6) |
| Shaman/Totems.cs L403 | `GetCount(2, 7)` for "Totemic Reach" — talent doesn't exist in WotLK. Index 7 = Improved Shields (Enhancement Tier 2). Phantom +15%/+30% totem range multiplier. | Removed talent factor calculation, set `talentFactor = 1f` |

### Full Sweep Results (45 patterns checked — all clean except above)

Verified clean: Mastery, Holy Power, Vengeance (confirmed = Seal of Vengeance), Colossus Smash, Dark Intent, Wild Mushroom, Redirect, Smoke Bomb, Necrotic Strike, Dark Simulacrum, Outbreak, Spiritwalker's Grace, Unleash Elements, Healing Rain, Chakra, Mind Spike, Leap of Faith, PW:Barrier, Inner Will, Camouflage, Cobra Shot, Focus Fire, Ring of Frost, Flame Orb, Time Warp, Demon Soul, Hand of Gul'dan, Fel Flame, Bane of (Cata naming), Inquisition, Guardian of Ancient Kings, Word of Glory, Templar's Verdict, Zealotry, Inner Rage, Rallying Cry, Heroic Leap, MoP talents (Skull Banner/Avatar/Storm Bolt/Dragon Roar), WoWSpec/GetSpecialization, TalentManager.IsSelected, Symbiosis, Ascendance, Stampede, Active Mitigation.

Also verified: Hunter uses ManaPercent (not Focus), DK rune system correct, Rogue energy thresholds ≤100, Paladin seals all WotLK names, Dispelling.cs correct per-class dispel capabilities, all `IsBoss()` usage reasonable, all interrupt spell names correct WotLK, all aura stack counts match WotLK mechanics.

---

## QC Audit Phase 5 — Singular Infrastructure Audit

Full audit of all non-rotation infrastructure files: SingularRoutine.cs, all Settings, Helpers, Managers, Utilities, Lists, Dynamics, GUI. Found 0 MUST-FIX, 4 SHOULD-FIX issues.

### SHOULD-FIX (Critical — Combat Log)

| File | Issue | Fix |
|------|-------|-----|
| Utilities/CombatLog.cs | **All Args[] indices wrong for WotLK** — Code ported from Cata Singular used Cata 4.2+ CLEU format with `hideCaster` (4.1) + `sourceRaidFlags`/`destRaidFlags` (4.2) = 11 base params. WotLK has only 8 base params. All indices were off by 3, breaking evade detection, immunity tracking, and all combat log event handling. | Removed `HideCaster` property. Shifted ALL indices: SourceGuid 3→2, SourceName 4→3, SourceFlags 5→4, DestGuid 7→5, DestName 8→6, DestFlags 9→7, SpellId 11→8, SpellName 12→9, SpellSchool 13→10, SuffixParams loop 11→8. Also fixed `args.Add(args[i])` → `args.Add(Args[i])` bug (lowercase `args` was the local List, not the inherited `Args` array — would have caused IndexOutOfRangeException). |

### SHOULD-FIX (Comments & Logic)

| File | Issue | Fix |
|------|-------|-----|
| SingularRoutine.cs L155 | Stale comment "Group.MeIsTank always returns false in WotLK" — factually wrong since MeIsTank has spec-based + aura-based fallbacks (Bear Form, Defensive Stance, Frost Presence, Righteous Fury) | Corrected comment to reflect actual behavior |
| Managers/HealerManager.cs L168 | `GetMainTankGuids()` only checked `WoWPartyMember.GroupRole.Tank` (LFD role). In manually formed groups (no Dungeon Finder), returns empty — healers wouldn't prioritize tanks | Added fallback: when LFD roles return no tanks, uses `Group.Tanks` which has aura-based detection |
| Settings/WarriorSettings.cs | `UseWarriorT12` and `UseWarriorSMF` dead settings visible in UI PropertyGrid (confuses users — settings for Cata abilities that don't exist) | Added `[Browsable(false)]` to hide from settings UI while preserving serialization compat |

### Verified Clean (no changes needed)

All other infrastructure files verified clean: BossList.cs (only Classic/TBC/WotLK bosses), ConfigurationWindow.cs, all other Settings files, TalentManager.cs, TankManager.cs, PetManager.cs, SpellImmunityManager.cs, MountManager.cs, Party.cs, Spell.cs, Rest.cs, Unit.cs, Extensions.cs, Dynamics/CompositeBuilder.cs, Lists/.

---

## QC Audit Phase 3 — Meta-Audit of Phase 1+2 Changes

Re-audit of all 23 files modified in session. Verified every rotation, ID, spell name, and percentage for WotLK 3.3.5a compatibility.

### MUST-FIX (Functional Bugs Found)

| File | Issue | Fix | Verified |
|------|-------|-----|----------|
| Warlock/Demonology.cs | "Felstorm" — does NOT exist in WotLK (Cata 4.0.1 replaced Cleave) | "Felstorm" → "Cleave" in Pet.CreateCastPetAction (2 locations) | ✅ wowhead.com/wotlk/npc=17252/felguard |
| Rogue/Assassination.cs | PvP section still had Murderous Intent (Backstab sub-35%) — missed in Phase 2 (only Normal+Instance were fixed) | Removed Backstab/Mutilate conditional pair → single Mutilate | ✅ |
| Warrior/Fury.cs | `HasAura("Incite", 1)` dead code in 6 locations — Incite is passive +crit in WotLK, no proc aura | Removed `HasAura("Incite", 1) \|\|` from Cleave+HS in Normal/PvP/Instance (6 locations) | ✅ |
| Warrior/Arms.cs | Execute only checked `HealthPercent < 20` — missed Sudden Death procs above 20% | Added `\|\| StyxWoW.Me.HasAura("Sudden Death")` to Execute condition (Normal+PvP) | ✅ |
| Warrior/Arms.cs | PvP Overpower used `Spell.Buff("Overpower")` — wrong API, Buff checks for aura not spell cast | Changed to `Spell.Cast("Overpower")` | ✅ |
| Warrior/Arms.cs | Instance post-Slaughter rotation was messy (contradicting Execute conditions) | Collapsed to clean: Rend → Mortal Strike → Execute with Sudden Death | ✅ |

### SHOULD-FIX (Dead Code & Comments)

| File | Issue | Fix |
|------|-------|-----|
| Warrior/Arms.cs | "recklessness gets to be used in any stance soon" — factually wrong for 3.3.5 (Recklessness became stance-free in 3.1.0) | Comment → "WotLK 3.1+: Recklessness usable in any stance" (3 locations) |
| Warrior/Arms.cs | French comment about Blood and Thunder | Translated to English |
| Warrior/Arms.cs | "Rotatiom" typo | Fixed to "Rotation" (3 locations) |
| Warrior/Protection.cs | Duplicate Battle Shout in all 3 CombatBuffs — identical condition appears twice in PrioritySelector | Removed second occurrence (3 methods: Normal/PvP/Instance) |
| Warrior/Protection.cs | Retaliation in CombatBuffs — requires Battle Stance but Prot always starts with Defensive Stance | Commented out with explanation (3 methods: Normal/PvP/Instance) |

### Files Verified CLEAN (No Issues)

- Shaman: Enhancement.cs, Elemental.cs, Restoration.cs — `Name.StartsWith()` enchant pattern ✅
- Priest/Holy.cs — HymnofHopeMana setting ✅
- Helpers/Item.cs — Flask of the North 47499 ✅
- Paladin/Common.cs — Wisdom enum + encoding fix ✅
- Paladin/Holy.cs — Crusader Strike removal ✅
- Mage/Common.cs — Mind Mastery comment ✅
- SingularSettings.cs — Flask description ✅
- TalentManager.cs — Glyph `[2]` index ✅
- Helpers/Common.cs — Gag Order `(3, 10)` ✅
- DK/Unholy.cs — Duplicate Lichborne removal ✅
- Hunter/Lowbie.cs — `GotAlivePet` null check ✅
- PriestSettings.cs — Cata settings commented out ✅
- Warlock/Destruction.cs — Curse of the Elements typo ✅
- WarriorSettings.cs — T12/SMF description updates ✅

---

## QC Audit Phase 2 — Reference-Verified Fixes

All changes below verified against wowhead.com/wotlk, wotlk.evowow.com, and wowpedia.fandom.com.

### HIGH Priority (applied first)

| File | Fix | Verified |
|------|-----|----------|
| TalentManager.cs | Glyph socket index `[3]` → `[2]` for WotLK 6-glyph system | ✅ |
| Helpers/Common.cs | Gag Order talent `GetCount(3, 7)` → `GetCount(3, 10)` for WotLK position | ✅ |
| DK/Unholy.cs | Removed duplicate Lichborne with wrong method signature | ✅ |
| Hunter/Lowbie.cs | Added `GotAlivePet` null check before pet commands | ✅ |
| PriestSettings.cs | Commented out Mind Spike, MindBlastOrbs, MindBlastTimer (Cata-only) | ✅ |
| Warlock/Demonology.cs | "Curse of Elements" → "Curse of the Elements" | ✅ |
| Warlock/Destruction.cs | Same typo + added Conflagrate to Instance rotation | ✅ |
| Rogue/Assassination.cs | Removed Backstab sub-35% "Murderous Intent" Cata mechanic (3 locations) | ✅ |

### MEDIUM Priority

#### Helpers/Group.cs — Full Rewrite
- Replaced Cata-only `WoWPartyMember.GroupRole` direct checks with WotLK-compatible heuristics:
  1. LFD/raid role assignment via `LocalPlayer.Role` (uses `GetRaidRosterInfo()`)
  2. `TalentManager.CurrentSpec` fallback (ProtWarrior, ProtPala, FrostDK, FeralDruid+Bear, HolyPriest, Disc, RestoDruid, HolyPala, RestoShaman)
  3. Aura-based last resort (Bear Form, Dire Bear Form, Defensive Stance, Frost Presence, Righteous Fury)

#### Warrior/Arms.cs
- **Blood and Thunder** (spell does NOT exist in WotLK — Cata 4.0.1; position (3,3) = Trauma): Removed `TalentManager.GetCount(3, 3) > 0` gate from Rend in AOE (3 locations). Rend now cast unconditionally in AOE
- **Incite** (spell 50685, EXISTS in WotLK as Prot T1 passive +crit): Removed `HasAura("Incite", 1)` check (6 locations) — Incite has NO proc aura in WotLK (proc added in Cata 4.0.1), falls back to `CanUseRageDump()` 
- **Slaughter** (spell 84584, Cata-only): Removed `HasAura("Slaughter", 3)` check (1 location, Instance section)
- **UseWarriorT12** (Firelands Cata 4.2): Removed from ALL 10 shout conditions. 7 Battle Shout `|| UseWarriorT12` removed, 3 T12-only Commanding Shout lines removed entirely

#### Warrior/Protection.cs
- **UseWarriorT12**: Removed from ALL 9 Battle Shout conditions (3 Pull + 6 CombatBuffs methods across Normal/PvP/Instance)

#### Warrior/Fury.cs
- **UseWarriorT12**: Removed from ALL 9 shout conditions (6 Battle Shout + 3 Commanding Shout across Normal/PvP/Instance)
- **UseWarriorSMF** (Single-Minded Fury, Cata 4.0.1): Collapsed dual SMF/TG rotation branches into single Titan's Grip rotation (BT > WW > Slam on Bloodsurge proc) in all 3 Combat methods

#### Warrior/WarriorSettings.cs
- Updated `UseWarriorT12` and `UseWarriorSMF` descriptions to note they're no longer consumed by rotation code

#### Shaman Enchant IDs (Enhancement.cs, Elemental.cs, Restoration.cs)
- **CRITICAL FIX**: Replaced hardcoded Rank 1 enchant IDs (Windfury=283, Flametongue=5, Earthliving=3345) with `Name.StartsWith()` checks
- Old behavior: checked `TemporaryEnchantment.Id != 283` — at level 80 with max rank, enchant ID is 3787, causing infinite re-imbue loop every tick
- New behavior: `!TemporaryEnchantment.Name.StartsWith("Windfury")` matches all ranks via DBC enchant name prefix
- Also removed redundant `MainHand.TemporaryEnchantment != null` check (TemporaryEnchantment never returns null)

#### Priest/Holy.cs
- Replaced hardcoded `ManaPercent <= 15` with `SingularSettings.Instance.Priest.HymnofHopeMana` (setting exists at PriestSettings.cs)

#### Helpers/Item.cs + Settings/SingularSettings.cs
- **Flask of Enhancement** (item 58149): Removed — Cata-only (wowhead: "Item #58149 doesn't exist" on WotLK)
- **Flask of the North** (item 47499): Kept — valid WotLK alchemy flask (Alchemy 400)
- Updated description from "Flask of the North or Flask of Enhancement" to "Flask of the North (WotLK alchemy)"

### LOW Priority

#### Paladin/Common.cs
- Fixed encoding artifact (U+FFFD → em dash `—`) in comment
- Added `Wisdom` to `PaladinBlessings` enum (WotLK has separate Blessing of Wisdom, merged into Might in Cata)
- Added TODO for Blessing of Wisdom buff logic (feature addition, not implemented)

#### Paladin/Holy.cs
- Removed `Crusader Strike` from 2 solo-DPS fallback sections — Crusader Strike is Ret talent only in WotLK (became baseline in Cata 4.0.1)

#### Mage/Common.cs
- Fixed comment "mastery works off of how much mana we have" → "Arcane damage scales with mana pool via Mind Mastery talent" (Mastery doesn't exist in WotLK)

---

## Mage

### Common.cs (PreCombatBuffs)
- **Arcane Intellect fallback**: Added `Arcane Intellect` (level 1) as fallback when `Arcane Brilliance` (level 56) is not available
- **Frost Armor / Ice Armor fallback**: Added `Ice Armor` and `Frost Armor` for low-level mages who don't yet have Molten/Mage Armor
- **Mage Ward -> Fire Ward**: `Mage Ward` does not exist in WotLK (added in Cata). Replaced with `Fire Ward`
- **MageFoodIds**: Replaced Cata IDs (65xxx) with all WotLK ranks (Conjured Bread through Conjured Mana Strudel)
- **Mana Gem IDs**: Replaced Cata ID (36799) with WotLK IDs (Mana Agate/Jade/Citrine/Ruby/Emerald)
- **Conjure Food fallback**: Added `Conjure Food` for mages below level 74 (before Conjure Refreshment)

### Fire.cs
- **Mage Ward -> Fire Ward**: 3 occurrences (Normal, PvP, Instance)
- **Critical Mass -> Improved Scorch**: The debuff is called `Improved Scorch` in WotLK, not `Critical Mass`
- **Impact**: In WotLK, Impact is a passive talent (stun chance on Fire spells), not a player buff. Fire Blast is no longer conditioned on Impact proc  used as finisher or while moving
- **Blast Wave**: In WotLK this is a PBAoE (centered on caster), not a ground-targeted spell. Changed `CastOnGround` to `Cast`

### Frost.cs
- **Mage Ward -> Fire Ward**: 3 occurrences (Normal, PvP, Instance)
- **Arcane Missiles! proc removed**: This proc does not exist in WotLK. Arcane Missiles is always castable but useless in Frost rotation. Removed from all 3 contexts

### Arcane.cs
- **Arcane Missiles! proc check removed**: In WotLK, Arcane Missiles has no proc system. Kept the cast conditioned on 3 stacks of Arcane Blast (mana conservation). 3 contexts fixed

### Lowbie.cs
- OK  rotation correct for WotLK (Fireball -> Frostbolt, Fire Blast as finisher)

### QC  Quality Control (verified via wowhead.com/wotlk)
- **RefreshmentTableIds**: Removed 207386 and 207387 (Cata-only). Only 186812 is valid in WotLK
- **Conjured food item names corrected** in comments:
  - 43523 = Conjured Mana Strudel (req level 80)
  - 43518 = Conjured Mana Pie (req level 74)
  - 34062 = Conjured Mana Biscuit (req level 65)
  - 22895 = Conjured Cinnamon Roll (req level 55)
  - 22019 = Conjured Croissant (req level 65)
- **All spells verified**: Frost Armor (L1), Ice Armor (L30), Fire Ward (L20), Frost Ward (L22), Conjure Refreshment (L75), Hot Streak, Improved Scorch, Living Bomb, Deep Freeze, Fingers of Frost, Brain Freeze  all valid WotLK
- **Mana Gem IDs verified**: 22044 (Emerald), 8008 (Ruby), 8007 (Citrine), 5513 (Jade), 5514 (Agate)  all correct
- **Note**: Focus Magic in Arcane Instance uses `HasRole(GroupRole.Damage)`  works with WotLK LFD (patch 3.3)

### Spells used by item ID
- MageFoodIds: 43523, 43518, 34062, 22895, 22019 (all verified WotLK)
- ManaGemIds: 22044, 8008, 8007, 5513, 5514 (all verified WotLK)
- RefreshmentTableIds: 186812 only (207386, 207387 removed  Cata-only)

---

## Warrior

### Common.cs
- OK  only contains `ChargeTimer` helper, no WotLK issues

### Lowbie.cs
- OK  rotation correct for WotLK (Victory Rush, Rend, Thunder Clap, Heroic Strike, Charge, Throw/Shoot). `RagePercent` helper already present

### Arms.cs
- **"Commanding Shut" -> "Commanding Shout"**: Typo fixed (2 occurrences  Normal PreCombat, PvP PreCombat). Spell was not working at all with the wrong name
- **Throwdown removed**: `Throwdown` does not exist in WotLK (added in Cata 4.0.1). Commented out in 3 contexts (Normal, PvP, Instance). WotLK has no direct equivalent  interrupts go through `CreateInterruptSpellCast` (Pummel)
- **Harmless code left in place**:
  - `Roar of Courage` in buff checks: Cata Hunter pet buff, does not exist in WotLK but the silent check breaks nothing
  - `UseWarriorT12`: Cata tier 12 setting, inactive when false (default)
  - `Incite` aura check: In WotLK this is a passive talent, not a player buff  `HasAura` returns false  falls back to `CanUseRageDump()` which works
  - `Slaughter` aura check: Dead Cata code, evaluates to true (buff does not exist)  condition simplified automatically

### Fury.cs
- **RagePercent helper added**: `StyxWoW.Me.RagePercent` does not exist in WotLK API. Added `private static double RagePercent` computed from `CurrentRage / MaxRage * 100`, matching the pattern in other Warrior files. 13 references replaced
- **Whirlwind added to single-target rotation**: In WotLK, the core Fury cycle is BT > WW (Bloodthirst > Whirlwind). WW was only present in AOE sections. Added to all 6 single-target blocks (3 contexts x 2 SMF toggles)
- **Raging Blow**: Already commented out in prior port with "doesn't exist in WotLK (added in Cata 4.0.1)" note  no action required
- Same harmless code notes as Arms (Roar of Courage, UseWarriorT12, Incite)

### Protection.cs
- **Blood and Thunder**: Comments updated at lines 226 and 586. "Blood and Thunder will refresh Rend" replaced with note explaining B&T is Cata-only. Thunder Clap still used for threat + slow
- **RagePercent helper**: Already present
- **Rotation OK**: All spells verified for WotLK  Shield Slam, Revenge, Devastate, Concussion Blow, Shockwave, Shield Block, Shield Wall, Spell Reflection, Demoralizing Shout, Challenging Shout, Taunt, Disarm
- Same harmless code notes for Roar of Courage, UseWarriorT12

### QC  Quality Control (verified via wowhead.com/wotlk)
- **All spells verified**:
  - Arms: Mortal Strike, Overpower, Rend, Execute, Bladestorm, Sweeping Strikes, Shattering Throw, Retaliation
  - Fury: Bloodthirst, Whirlwind, Slam (Bloodsurge proc), Death Wish, Heroic Fury, Berserker Rage, Enraged Regeneration, Recklessness
  - Protection: Shield Slam, Revenge, Devastate, Concussion Blow, Shockwave, Shield Block, Shield Wall, Spell Reflection
  - Common: Charge, Intercept (separate from Charge in WotLK), Heroic Throw, Battle Shout, Commanding Shout, Intimidating Shout, Piercing Howl, Thunder Clap, Cleave, Heroic Strike, Execute, Victory Rush
- **Stances**: Battle Stance / Defensive Stance / Berserker Stance  stance dancing code correct for WotLK
- **Throwdown (Cata-only)**: Verified  does not appear on wowhead.com/wotlk/warrior, confirmed added in Cata 4.0.1
- **TalentManager.GetCount(3, 3)** for Blood and Thunder: Returns 0 in WotLK (talent does not exist at this position)  Rend AOE not applied via this condition. Rend applied in single-target rotation via `Spell.Buff("Rend")`

### No spells used by hard-coded ID in Warrior files

---

## Lowbie (all classes)  Cross-class audit

### Verified OK
- **Druid**: Wrath, Moonfire, Entangling Roots, Cat Form, Rejuvenation, Rake, Ferocious Bite, Claw  all valid WotLK
- **Hunter**: Raptor Strike, Hunter's Mark, Mend Pet, Concussive Shot, Arcane Shot, Steady Shot  all valid WotLK
- **Mage**: Previously verified (Fireball, Frostbolt, Fire Blast)
- **Priest**: Power Word: Shield, Flash Heal, Shadow Word: Pain, Mind Blast, Smite, CreateUseWand  all valid WotLK
- **Rogue**: Sinister Strike, Eviscerate, Stealth, Blood Fury, Berserking, Lifeblood (racials)  all valid WotLK
- **Shaman**: Lightning Shield, Lightning Bolt, Earth Shock, Healing Wave  all valid WotLK. Primal Strike note correct (Cata-only)
- **Warlock**: Shadow Bolt, Corruption, Immolate, Life Tap, Drain Life, Imp summon  all valid WotLK
- **Warrior**: Previously verified (Victory Rush, Rend, Thunder Clap, Heroic Strike, Charge)

### Fixed
- **Paladin Lowbie.cs**: `Judgement` -> `Judgement of Light`. In WotLK, the generic "Judgement" spell does not exist  it was consolidated in Cata. WotLK has Judgement of Light (L4), Judgement of Wisdom (L14), Judgement of Justice (L20). Lowbie uses Judgement of Light (available at level 4)

### Excluded
- **Monk Lowbie.cs**: Already fully commented out (MoP class, does not exist in WotLK)

### No spells used by hard-coded ID in any Lowbie file

---

## Paladin

### Lowbie.cs
- **Judgement -> Judgement of Light**: `Judgement` does not exist in WotLK (consolidated in Cata). Replaced with `Judgement of Light` (available at level 4). 2 occurrences (Pull, Combat)
- **Word of Glory**: Already commented out (Cata-only Holy Power spell)
- Seal of Righteousness, Devotion Aura, Holy Light  all valid WotLK

### Protection.cs
- **Judgement -> Judgement of Wisdom**: 3 occurrences (multi-target L81, single-target L90, pull L111). Prot uses Judgement of Wisdom for mana sustain
- **Crusader Strike -> Shield of Righteousness**: Crusader Strike is a deep Ret talent in WotLK, not available to Prot builds. Replaced with Shield of Righteousness (baseline at L75, 6s CD). 2 occurrences
- **Holy Shield added**: Core Prot ability missing from rotation. Added as `Spell.BuffSelf("Holy Shield")`  8s CD, increases block chance by 30%, 8 charges. Placed after autoattack, before seal logic
- **Ardent Defender commented out**: In WotLK, Ardent Defender is a passive talent (reduces damage taken below 35% HP), not an active cooldown. `Spell.BuffSelf("Ardent Defender")` would fail silently
- Seal logic (Vengeance/Corruption/Wisdom/Righteousness) already correct for WotLK
- Hand of Reckoning, Lay on Hands, Divine Protection  all valid WotLK

### Retribution.cs
- **Judgement -> Judgement of Light**: 3 active occurrences (Normal, PvP, Instance). Ret uses Judgement of Light for healing proc
- **Hammer of Wrath AW condition removed**: In WotLK, Sanctified Wrath does NOT unlock Hammer of Wrath above 20% HP (that mechanic was added in Cata). Removed `|| StyxWoW.Me.ActiveAuras.ContainsKey("Avenging Wrath")` from all 3 contexts. HoW now only fires at target < 20% HP
- **Normal rotation  Seal of Vengeance/Corruption added**: The Normal context was missing a single-target seal (only had Seal of Righteousness for AoE). Added Seal of Vengeance (Alliance) / Seal of Corruption (Horde) for < 4 targets, matching the Instance rotation pattern
- **Guardian of Ancient Kings & Zealotry**: Already commented out (Cata-only)
- **Templar's Verdict & Inquisition**: Already removed (Cata Holy Power abilities)
- Art of War Exorcism proc, Crusader Strike (Ret talent), Divine Storm  all valid WotLK
- RET_T13_ITEM_SET_ID (Cata tier set)  dead code, harmless (never referenced in active rotation)


## Hunter

### Common.cs
- OK — Trap IDs verified WotLK (49056 Immolation, 14311 Freezing, 49067 Explosive, 13809 Frost, 34600 Snake)
- Trap Launcher removed (Cata-only) — already done in prior port
- Misdirection correct for WotLK

### BeastMastery.cs
- OK — Kill Command, Bestial Wrath, Intimidation, Focus Fire — all valid WotLK
- Mana system (not Focus) — correct for WotLK

### Marksmanship.cs
- OK — Aimed Shot, Chimera Shot, Silencing Shot, Readiness — all valid WotLK

### Survival.cs
- OK — Explosive Shot, Black Arrow, Lock and Load proc — all valid WotLK

### Lowbie.cs
- OK — Raptor Strike, Hunter's Mark, Arcane Shot, Steady Shot — all valid

### No changes required — all 5 files clean

---

## Warlock

### Common.cs
- **Demon Soul (6x)**: Commented out in all 6 contexts (Normal, PvP, Instance × Succubus check + cast). Demon Soul does not exist in WotLK (added in Cata 4.0.1)
- **Axe Toss -> Intercept (2x)**: Felguard interrupt spell renamed. In WotLK the Felguard uses `Intercept` (stun), not `Axe Toss` (Cata). Fixed in PvP and Instance contexts
- Demonic Empowerment, Soul Link, Life Tap, Fel Armor — all valid WotLK

### Affliction.cs
- OK — Unstable Affliction, Haunt, Corruption, Curse of Agony, Drain Soul, Shadow Bolt — all valid WotLK

### Demonology.cs
- OK — Metamorphosis, Immolation Aura, Soul Fire (Decimation proc), Felguard — all valid WotLK

### Destruction.cs
- OK — Conflagrate, Chaos Bolt, Incinerate, Immolate, Shadowfury — all valid WotLK

### Lowbie.cs
- OK — Shadow Bolt, Corruption, Immolate, Life Tap, Drain Life — all valid WotLK

---

## Rogue

### Combat.cs
- **Revealing Strike (1x)**: Commented out in PvP context. Revealing Strike does not exist in WotLK (added in Cata 4.0.1)
- Sinister Strike, Eviscerate, Slice and Dice, Adrenaline Rush, Killing Spree, Blade Flurry — all valid WotLK

### Assassination.cs
- OK — Mutilate, Envenom, Hunger for Blood, Cold Blood, Rupture — all valid WotLK

### Subtlety.cs
- OK — Hemorrhage, Shadow Dance, Premeditation, Preparation — all valid WotLK

### Common.cs
- OK — Stealth, Kick, Evasion, Cloak of Shadows, Vanish, Tricks of the Trade — all valid WotLK

### Lowbie.cs
- OK — Sinister Strike, Eviscerate, Stealth — all valid WotLK

---

## Shaman

### Common.cs
- **Unleash Elements (2x)**: Commented out in Normal and PvP contexts. Unleash Elements does not exist in WotLK (added in Cata 4.0.1)
- **Earthquake (1x)**: Commented out in AoE context. Earthquake does not exist in WotLK (added in Cata 4.0.1)
- **Time Warp / Ancient Hysteria**: Removed from buff logic. These are Mage/Hunter Cata abilities, not Shaman. WotLK Shaman uses Heroism/Bloodlust only

### Restoration.cs
- **Greater Healing Wave -> Healing Wave (CRITICAL)**: In WotLK, the strong single-target heal is called `Healing Wave`, not `Greater Healing Wave`. Cata renamed the old Healing Wave to Greater Healing Wave and introduced a new fast "Healing Wave". This was causing the heal to fail silently. Fixed in all contexts
- Riptide, Chain Heal, Earth Shield, Lesser Healing Wave — all valid WotLK

### Enhancement.cs
- OK — Stormstrike, Lava Lash, Maelstrom Weapon, Shamanistic Rage, Feral Spirit — all valid WotLK

### Elemental.cs
- OK — Lava Burst, Lightning Bolt, Chain Lightning, Flame Shock, Thunderstorm — all valid WotLK

### Lowbie.cs
- OK — Lightning Bolt, Earth Shock, Lightning Shield, Healing Wave — all valid WotLK

---

## Druid

### Common.cs
- **Stampeding Roar (77764)**: Commented out. Stampeding Roar does not exist in WotLK (added in Cata 4.0.1, spell ID 77764). Dash remains as the movement speed ability
- **Fury of Stormrage (2x)**: Commented out Wrath mana refund in Resto PreCombat and combat contexts. Fury of Stormrage is a Cata talent that procs free Wraths — does not exist in WotLK

### Feral.cs
- **Tree of Life unconditional**: Removed `HasAura("Tree of Life")` condition check. In WotLK, Tree of Life is a permanent shapeshift form (not a 30s cooldown like Cata). The aura check was failing and preventing healing in tree form
- **Cata T11/T13 tier set stubs removed**: Removed `FERAL_T11_ITEM_SET_ID` (was 928), `FERAL_T13_ITEM_SET_ID` (was 1058), `NumTier11Pieces`, `NumTier13Pieces` properties. Replaced `Has4PieceTier11Bonus`, `Has2PieceTier13Bonus`, `Has4PieceTier13Bonus` with simple `return false` stubs to avoid compilation errors
- **Finisherhealth level 81-84 branches removed**: WotLK max level is 80. Removed dead branches for levels 81, 82-84 that could never trigger. Simplified `Level >= 80 && Level <= 81` to `Level == 80`
- **Tiger's Fury #3 (T13 4pc) removed**: Comment out in both PvP and Instance rotations — Was conditioned on `Has4PieceTier13Bonus` which is always false in WotLK
- **Blood in the Water #9/#10 (T13 2pc) fixed**: Replaced `(Has2PieceTier13Bonus ? 60 : 25)` with fixed `25` threshold in both PvP and Instance rotations — Cata T13 2pc extended threshold to 60%, WotLK uses standard 25%
- **Dragon Soul zone 5892 taunt swap removed**: Removed Cata Dragon Soul auto-taunt logic from `CreateFeralBearInstanceCombat` (Morchok 55265, Ultraxion, Warmaster Blackhorn 56427, Madness of Deathwing 56471)
- **ExtraActionButton1 removed**: Removed from `CreateFeralCatInstanceCombat` — ExtraActionButton doesn't exist in WotLK (added in Cata 4.3 for Dragon Soul)
- **Dragon Soul helper methods removed**: Removed `ClickExtraActionButton1()`, `TauntNeed()`, `Ultra()` (Hour of Twilight check), `UltraFl()` (Fading Light check), `Dw()` (Shrapnel check) — all Dragon Soul (Cata) encounter-specific
- **DruidBossExts boss IDs emptied**: Removed all Dragon Soul boss IDs from `_shred` (56846, 56167, 56168, 57962, 56471) and `_charge` (55294, 56846, 56167, 56168, 57962, 56471, 54191, 57281, 57795, 56249, 56252, 56251, 56250) HashSets. Lists now empty — add WotLK boss IDs as needed
- **Enrage comments fixed**: WotLK Enrage increases damage taken by 10% (unlike Cata where penalty was removed). Updated 2 comments in bear rotation to reflect WotLK behavior
- **Glyph of Bloodletting comments updated**: In WotLK this was called "Glyph of Shred" — noted in comments

### Balance.cs
- OK — Starfall, Typhoon, Moonkin Form, Wrath, Starfire, Moonfire, Insect Swarm — all valid WotLK

### Restoration.cs
- OK after Tree of Life fix — Rejuvenation, Lifebloom, Wild Growth, Swiftmend, Nourish — all valid WotLK

### Lowbie.cs
- OK — Wrath, Moonfire, Rejuvenation, Cat Form, Rake, Claw, Ferocious Bite — all valid WotLK

---

## Death Knight

### Blood.cs
- OK — Heart Strike, Death Strike, Rune Strike, Vampiric Blood, Icebound Fortitude, Dancing Rune Weapon — all valid WotLK
- Mark of Blood, Hysteria — valid WotLK Blood spells

### Frost.cs
- OK — Already adapted with WotLK-specific spells:
  - Frost Strike, Howling Blast, Obliterate, Rime proc
  - **Unbreakable Armor**: Already present (WotLK Frost cooldown, removed in Cata)
  - Deathchill: Already present (WotLK talent)

### Unholy.cs
- OK — Already adapted with WotLK-specific spells:
  - Scourge Strike, Festering Strike area replaced with Blood Strike (WotLK)
  - **Ghoul Frenzy**: Already present (WotLK talent, removed in Cata)
  - Bone Shield, Summon Gargoyle, Anti-Magic Shell — all valid WotLK

### Common.cs
- OK — Death Grip, Mind Freeze, Strangulate, Horn of Winter, Raise Dead, Death Coil — all valid WotLK
- Presence management (Blood/Frost/Unholy) — correct for WotLK

### Lowbie.cs
- OK — Death Coil, Icy Touch, Plague Strike, Blood Strike — all valid WotLK

### All 5 files clean — no changes required

---

## Priest

### Common.cs
- **Inner Will**: Commented out. Inner Will does not exist in WotLK (added in Cata 4.0.1). WotLK Priest only has Inner Fire

### Discipline.cs
- **"Heal" -> "Greater Heal" (CRITICAL)**: In WotLK, the strong single-target heal is `Greater Heal`. Cata introduced a new baseline `Heal` spell. The Disc rotation was silently failing to cast its main heal. Fixed
- **Pain Suppression typo (CRITICAL)**: `"Pain Supression"` (one 's') → `"Pain Suppression"` (two 's'). This was preventing Pain Suppression from ever being cast on the target
- Power Word: Shield, Penance, Prayer of Mending, Flash Heal — all valid WotLK

### Holy.cs
- **"Heal" -> "Greater Heal" (CRITICAL)**: Same issue as Disc — the main heal was silently failing. Fixed
- Circle of Healing, Prayer of Healing, Guardian Spirit, Renew — all valid WotLK

### Shadow.cs
- OK — Mind Blast, Shadow Word: Pain, Vampiric Touch, Devouring Plague, Mind Flay, Shadow Word: Death, Dispersion — all valid WotLK
- Shadowform management correct for WotLK

### Lowbie.cs
- OK — Smite, Shadow Word: Pain, Mind Blast, Power Word: Shield, Flash Heal — all valid WotLK

---

## Settings & Infrastructure (Phase 2)

### Settings\DeathKnightSettings.cs
- **UsePillarOfFrost**: Commented out — Pillar of Frost does not exist in WotLK (added in Cata 4.0.1, replaced Unbreakable Armor)
- **UseNecroticStrike**: Commented out — Necrotic Strike does not exist in WotLK (added in Cata 4.0.1)

### Settings\DruidSettings.cs
- **Stampeding Roar description**: Fixed "Dash/Stampeding Roar" → "Dash" — Stampeding Roar does not exist in WotLK
- **CatRaidStampeding**: Setting property commented out — references Cata-only ability

### Settings\PaladinSettings.cs
- **WordOfGloryHealth**: Commented out — Word of Glory is a Cata Holy Power ability
- **LightOfDawnHealth + LightOfDawnCount**: Commented out — Light of Dawn is a Cata Holy Power ability
- **DivineLightHealth**: Commented out — Divine Light does not exist in WotLK (Cata replacement for Greater Heal)
- **GoAKHealth (Prot)**: Commented out — Guardian of Ancient Kings is Cata-only
- **ArdentDefenderHealth**: Commented out — Ardent Defender is a passive talent in WotLK, not an active cooldown
- **RetGoatK (Zealotry + GoAK)**: Commented out — both abilities are Cata-only

### Settings\PriestSettings.cs
- **MindBlastTimer description**: Removed "shadow orbs" reference — Shadow Orbs do not exist in WotLK
- **MindBlastOrbs description**: Updated to note Shadow Orbs don't exist in WotLK
- **AlwaysArchangel5**: Commented out — Archangel/Dark Evangelism is a Cata mechanic
- **UseInnerFire description**: Removed "otherwise uses Inner Will" — Inner Will does not exist in WotLK
- **ArchangelMana**: Commented out — Archangel is Cata-only

### Settings\WarriorSettings.cs
- **UseWarriorT12**: Commented out — Tier 12 is Cata Firelands tier set
- **UseWarriorCloser description**: Removed Heroic Leap from gap closer list — Heroic Leap does not exist in WotLK (added in Cata)
- **UseWarriorThrowdown**: Commented out — Throwdown does not exist in WotLK (added in Cata)

### Helpers\Common.cs
- **Quaking Palm**: Commented out — Pandaren racial ability, MoP-only (Pandaren race does not exist in WotLK)

### Lists\BossList.cs
- **Training dummy 46647**: Commented out — Cata-level training dummy (level 81-85)
- **Cata Heroic Deadmines (6 entries)**: Commented out — 47162, 47296, 43778, 47626, 47739, 49541
- **Cata Heroic SFK (3 entries)**: Commented out — 46962, 46963, 46964
- **Cata Dungeons (40+ entries)**: Commented out — Blackrock Caverns, Throne of Tides, Stonecore, Vortex Pinnacle, Grim Batol, Halls of Origination, Lost City of Tol'vir
- **Cata Raids (15+ entries)**: Commented out — Baradin Hold, BWD, Throne of Four Winds, Bastion of Twilight
- **Cata Zul'Gurub (6 entries)**: Commented out — 52155, 52151, 52271, 52059, 52053, 52148
- **Firelands (7 entries)**: Commented out — 53691, 52558, 52498, 52530, 53494, 52571, 52409
- **Dragon Soul (11 entries)**: Commented out — 55265, 57773, 55308, 55312, 55689, 55294, 56427, 56846, 56167, 56168, 57962

### Lists\CataHeroicDpsList.cs
- **Entire file commented out**: Contains only Cata heroic dungeon add creature entries (Grim Batol, Stonecore, BRC, Lost City, HOO). None of these dungeons exist in WotLK



### Verified clean (no changes needed)
- Settings: HunterSettings, MageSettings, RogueSettings, ShamanSettings, WarlockSettings, SingularSettings
- GUI: ConfigurationWindow.cs
- Helpers: Rest.cs
- Managers: TalentManager.cs, MountManager.cs
- Core: SingularRoutine.cs

---

## Spells used by hard-coded ID (all classes)

| File | Spell ID | Spell Name | Rank | Valid WotLK |
|------|----------|------------|------|-------------|
| Hunter/Common.cs | 49056 | Immolation Trap | Rank 8 | Yes (req L78) |
| Hunter/Common.cs | 14311 | Freezing Trap | Rank 3 | Yes (req L60) |
| Hunter/Common.cs | 49067 | Explosive Trap | Rank 6 | Yes (req L77) |
| Hunter/Common.cs | 13809 | Frost Trap | (no ranks) | Yes (req L28) |
| Hunter/Common.cs | 34600 | Snake Trap | (no ranks) | Yes (req L68) |
| Druid/Common.cs | 77764 | Stampeding Roar | N/A | **NO  Cata-only** |
| Shaman/Totems.cs | dynamic | GetTotemSpellId() | dynamic | OK (resolved at runtime) |

---

