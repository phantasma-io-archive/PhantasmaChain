using System;
using System.Collections.Generic;
using System.Linq;

using Phantasma.Storage.Context;
using Phantasma.Cryptography;
using Phantasma.Storage;
using Phantasma.Numerics;
using Phantasma.Blockchain.Tokens;
using Phantasma.Core.Types;

namespace Phantasma.Blockchain.Contracts.Native
{
    #region RULES
    public static class Rules
    {
        public static bool ValidateAddressName(string name)
        {
            if (name == null) return false;

            if (name.Length < 4 || name.Length > 15) return false;

            if (name.Equals(Constants.ANONYMOUS_NAME, StringComparison.OrdinalIgnoreCase) || name.Equals(Constants.ACADEMY_NAME, StringComparison.OrdinalIgnoreCase)) return false;

            int index = 0;
            while (index < name.Length)
            {
                var c = name[index];
                index++;

                if (c >= 97 && c <= 122) continue; // lowercase allowed
                if (c == 95) continue; // underscore allowed
                if (c >= 48 && c <= 57) continue; // numbers allowed

                return false;
            }

            return true;
        }

        public static bool IsModeWithMatchMaker(BattleMode mode)
        {
            return (mode == BattleMode.Unranked || mode == BattleMode.Ranked);
        }

        public static bool IsModeWithBets(BattleMode mode)
        {
            return (mode == BattleMode.Versus || mode == BattleMode.Ranked);
        }

        public static bool IsNegativeStatus(BattleStatus status)
        {
            switch (status)
            {
                case BattleStatus.None: return false;
                case BattleStatus.Bright: return false;
                default: return true;
            }
        }

        public static bool IsChoiceItem(ItemKind kind)
        {
            if (kind == ItemKind.Body_Armor) return true;
            if (kind == ItemKind.Killer_Gloves) return true;
            return false;
        }

        public static bool IsFocusMove(WrestlingMove move)
        {
            if (move == WrestlingMove.Armlock) return true;
            if (move == WrestlingMove.Refresh) return true;
            return false;
        }

        public static bool IsChargeMove(WrestlingMove move)
        {
            if (move == WrestlingMove.Rhino_Charge) return true;
            if (move == WrestlingMove.Mantra) return true;
            if (move == WrestlingMove.Chilli_Dance) return true;
            return false;
        }

        public static bool IsDamageMove(WrestlingMove move)
        {
            switch (move)
            {
                case WrestlingMove.Chop:
                case WrestlingMove.Smash:
                case WrestlingMove.Boomerang:
                case WrestlingMove.Bash:
                case WrestlingMove.Armlock:
                case WrestlingMove.Avalanche:
                case WrestlingMove.Corkscrew:
                case WrestlingMove.Butterfly_Kick:
                case WrestlingMove.Gutbuster:
                case WrestlingMove.Rhino_Rush:
                case WrestlingMove.Razor_Jab:
                case WrestlingMove.Hammerhead:
                case WrestlingMove.Knee_Bomb:
                case WrestlingMove.Chicken_Wing:
                case WrestlingMove.Headspin:
                case WrestlingMove.Side_Hook:
                case WrestlingMove.Wolf_Claw:
                case WrestlingMove.Monkey_Slap:
                case WrestlingMove.Leg_Twister:
                case WrestlingMove.Heart_Punch:
                case WrestlingMove.Smile_Crush:
                case WrestlingMove.Spinning_Crane:
                case WrestlingMove.Octopus_Arm:
                case WrestlingMove.Rage_Punch:
                case WrestlingMove.Flying_Kick:
                case WrestlingMove.Torpedo:
                case WrestlingMove.Catapult:
                case WrestlingMove.Iron_Fist:
                case WrestlingMove.Mega_Chop:
                case WrestlingMove.Fire_Breath:
                case WrestlingMove.Tart_Throw:
                case WrestlingMove.Bizarre_Ball:
                case WrestlingMove.Pain_Release:
                case WrestlingMove.Chomp:
                case WrestlingMove.Takedown:
                case WrestlingMove.Burning_Tart:
                case WrestlingMove.Sick_Sock:
                case WrestlingMove.Palm_Strike:
                case WrestlingMove.Slingshot:
                case WrestlingMove.Fart:
                case WrestlingMove.Hurricane:
                case WrestlingMove.Moth_Drill:
                case WrestlingMove.Knock_Off:
                case WrestlingMove.Back_Slap:
                case WrestlingMove.Rehab:
                case WrestlingMove.Hyper_Slam:
                case WrestlingMove.Flame_Fang:
                case WrestlingMove.Needle_Sting:
                case WrestlingMove.Psy_Strike:
                case WrestlingMove.Tart_Splash:
                case WrestlingMove.Ultra_Punch:
                    return true;

                default: return false;
            }
        }

        public static Rarity GetItemRarity(ItemKind kind)
        {
            switch (kind)
            {
                case ItemKind.Bling:
                case ItemKind.Meat_Snack:
                case ItemKind.Golden_Bullet:
                case ItemKind.Pincers:
                case ItemKind.Ancient_Trinket:
                case ItemKind.Virus_Chip:
                case ItemKind.Gyroscope:
                case ItemKind.Dojo_Belt:
                case ItemKind.Nullifier:
                case ItemKind.Yo_Yo:
                case ItemKind.Trap_Card:
                case ItemKind.Dev_Badge:
                case ItemKind.Make_Up_Legendary:
                    return Rarity.Legendary;

                case ItemKind.Gym_Card:
                case ItemKind.Creepy_Toy:
                case ItemKind.Loser_Mask:
                case ItemKind.Ego_Mask:
                case ItemKind.Trainer_Speaker:
                case ItemKind.Vampire_Teeth:
                case ItemKind.Magnifying_Glass:
                case ItemKind.Body_Armor:
                case ItemKind.Dumbell:
                case ItemKind.Lucky_Charm:
                case ItemKind.Spinner:
                case ItemKind.Wood_Chair:
                case ItemKind.Spy_Specs:
                case ItemKind.Focus_Banana:
                case ItemKind.Yellow_Card:
                case ItemKind.Magic_Mirror:
                case ItemKind.Stance_Block:
                case ItemKind.Wrist_Weights:
                case ItemKind.Tequilla:
                case ItemKind.Sombrero:
                case ItemKind.Poncho:
                case ItemKind.Make_Up_Epic:
                    return Rarity.Epic;

                case ItemKind.Battle_Helmet:
                case ItemKind.Steel_Boots:
                case ItemKind.Envy_Mask:
                case ItemKind.Giant_Pillow:
                case ItemKind.Killer_Gloves:
                case ItemKind.Rare_Taco:
                case ItemKind.XP_Perfume:
                case ItemKind.Nails:
                case ItemKind.Clown_Nose:
                case ItemKind.Hand_Bandages:
                case ItemKind.Love_Lotion:
                case ItemKind.Nanobots:
                case ItemKind.Make_Up_Rare:
                    return Rarity.Rare;

                case ItemKind.Muscle_Overclocker:
                case ItemKind.Spike_Vest:
                case ItemKind.Dice:
                case ItemKind.Lemon_Shake:
                case ItemKind.Gas_Mask:
                case ItemKind.Ignition_Chip:
                case ItemKind.Bomb:
                case ItemKind.Stamina_Pill:
                case ItemKind.Attack_Pill:
                case ItemKind.Defense_Pill:
                case ItemKind.Rubber_Suit:
                case ItemKind.Mummy_Bandages:
                case ItemKind.Purple_Cog:
                case ItemKind.Red_Spring:
                case ItemKind.Blue_Gear:
                case ItemKind.Make_Up_Uncommon:
                    return Rarity.Uncommon;

                case ItemKind.Expert_Gloves:
                case ItemKind.Vigour_Juice:
                case ItemKind.Power_Juice:
                case ItemKind.Claws:
                case ItemKind.Shell:
                case ItemKind.Chilli_Bottle:
                case ItemKind.Fork:
                case ItemKind.Gnome_Cap:
                case ItemKind.Wood_Potato:
                //case ItemKind.Avatar:
                case ItemKind.Make_Up_Common:
                    return Rarity.Common;

                default: return Rarity.Common;
            }
        }

        public static MoveCategory GetMoveCategory(WrestlingMove move)
        {
            switch (move)
            {
                case WrestlingMove.Psy_Strike:
                case WrestlingMove.Mind_Slash:
                case WrestlingMove.Zen_Point:
                case WrestlingMove.Pain_Release:
                    return MoveCategory.Mind;

                case WrestlingMove.Knee_Bomb:
                case WrestlingMove.Leg_Twister:
                case WrestlingMove.Flying_Kick:
                case WrestlingMove.Headspin:
                case WrestlingMove.Boomerang:
                case WrestlingMove.Butterfly_Kick:
                case WrestlingMove.Sick_Sock:
                    return MoveCategory.Legs;

                case WrestlingMove.Chop:
                case WrestlingMove.Gutbuster:
                case WrestlingMove.Chicken_Wing:
                case WrestlingMove.Armlock:
                case WrestlingMove.Corkscrew:
                case WrestlingMove.Side_Hook:
                case WrestlingMove.Wolf_Claw:
                case WrestlingMove.Monkey_Slap:
                case WrestlingMove.Heart_Punch:
                case WrestlingMove.Smile_Crush:
                case WrestlingMove.Slingshot:
                case WrestlingMove.Beggar_Bag:
                case WrestlingMove.Trick:
                case WrestlingMove.Recycle:
                case WrestlingMove.Spinning_Crane:
                case WrestlingMove.Octopus_Arm:
                case WrestlingMove.Rage_Punch:
                case WrestlingMove.Palm_Strike:
                case WrestlingMove.Torniquet:
                case WrestlingMove.Wood_Work:
                case WrestlingMove.Iron_Fist:
                case WrestlingMove.Mega_Chop:
                    return MoveCategory.Hands;

                case WrestlingMove.Rhino_Rush:
                case WrestlingMove.Hammerhead:
                case WrestlingMove.Moth_Drill:
                    return MoveCategory.Head;

                case WrestlingMove.Smash:
                case WrestlingMove.Bash:
                case WrestlingMove.Takedown:
                case WrestlingMove.Block:
                case WrestlingMove.Avalanche:
                case WrestlingMove.Sweatshake:
                    return MoveCategory.Body;

                case WrestlingMove.Taunt:
                case WrestlingMove.Scream:
                case WrestlingMove.Lion_Roar:
                case WrestlingMove.Gobble:
                case WrestlingMove.Fire_Breath:
                    return MoveCategory.Mouth;

                default: return MoveCategory.Other;
            }
        }

        public static bool IsSucessful(int chance, int percentage)
        {
            return chance > (100 - percentage);
        }

        public static bool CanBeInMoveset(WrestlingMove move)
        {
            if (move <= WrestlingMove.Unknown || move >= WrestlingMove.Flinch)
            {
                return false;
            }

            switch (move)
            {
                case WrestlingMove.Rhino_Rush:
                case WrestlingMove.Iron_Fist:
                case WrestlingMove.Flinch:
                    return false;

                default:
                    return true;
            }
        }

        public static readonly WrestlingMove[] PRIMARY_MOVES = new WrestlingMove[]
        {
            WrestlingMove.Chop,
            WrestlingMove.Butterfly_Kick,
            WrestlingMove.Monkey_Slap,
            WrestlingMove.Moth_Drill,
        };

        public static readonly WrestlingMove[] SECONDARY_MOVES = new WrestlingMove[]
        {
            WrestlingMove.Block,
            WrestlingMove.Hammerhead,
            WrestlingMove.Knee_Bomb,
            WrestlingMove.Side_Hook,
            WrestlingMove.Grip,
            WrestlingMove.Leg_Twister,
            WrestlingMove.Heart_Punch,
            WrestlingMove.Smile_Crush,
            WrestlingMove.Slingshot,
            WrestlingMove.Octopus_Arm,
            WrestlingMove.Palm_Strike,
            WrestlingMove.Rage_Punch,
            WrestlingMove.Bash,
            WrestlingMove.Armlock,
            WrestlingMove.Gutbuster,
            WrestlingMove.Takedown,
            WrestlingMove.Boomerang,
            WrestlingMove.Back_Slap,
            WrestlingMove.Rehab,
            WrestlingMove.Hyper_Slam,
            WrestlingMove.Refresh,
            WrestlingMove.Clown_Makeup,
            WrestlingMove.Torniquet,
            WrestlingMove.Pray,
            WrestlingMove.Bottle_Sip,
            WrestlingMove.Dynamite_Arm,
            WrestlingMove.Gorilla_Cannon,
        };

        public static readonly WrestlingMove[] TERTIARY_MOVES = new WrestlingMove[]
        {
            WrestlingMove.Razor_Jab,
            WrestlingMove.Avalanche,
            WrestlingMove.Corkscrew,
            WrestlingMove.Flying_Kick,
            WrestlingMove.Taunt,
            WrestlingMove.Bulk,
            WrestlingMove.Scream,
            WrestlingMove.Chicken_Wing,
            WrestlingMove.Copycat,
            WrestlingMove.Beggar_Bag,
            WrestlingMove.Torpedo,
            WrestlingMove.Trick,
            WrestlingMove.Lion_Roar,
            WrestlingMove.Flame_Fang,
            WrestlingMove.Fire_Breath,
            WrestlingMove.Wolf_Claw,
            WrestlingMove.Fart,
            WrestlingMove.Needle_Sting,
            WrestlingMove.Knock_Off,
            WrestlingMove.Psy_Strike,
            WrestlingMove.Sick_Sock,
            WrestlingMove.Mind_Slash,
            WrestlingMove.Star_Gaze,
            WrestlingMove.Flailing_Arms,
        };

        public static readonly WrestlingMove[] STANCE_MOVES = new WrestlingMove[]
        {
            WrestlingMove.Headspin,
            WrestlingMove.Dodge,
            WrestlingMove.Rhino_Charge,
            WrestlingMove.Mantra,
            WrestlingMove.Gobble,
            WrestlingMove.Recycle,
            WrestlingMove.Chilli_Dance,
            WrestlingMove.Sweatshake,
            WrestlingMove.Wood_Work,
            WrestlingMove.Spinning_Crane,
            WrestlingMove.Voodoo,
            WrestlingMove.Zen_Point,
            WrestlingMove.Poison_Ivy,
            WrestlingMove.Astral_Tango,
        };

        public static readonly WrestlingMove[] TAG_MOVES = new WrestlingMove[]
        {
            WrestlingMove.Catapult,
            WrestlingMove.Smuggle,
            WrestlingMove.Hurricane,
            WrestlingMove.Barrel_Roll,
        };

        public static WrestlingMove GetPrimaryMoveFromGenes(byte[] genes)
        {
            var n = genes[Constants.GENE_MOVE] + genes[Constants.GENE_COUNTRY] + genes[Constants.GENE_RANDOM];
            n = n % 8;
            if (n >= PRIMARY_MOVES.Length) return WrestlingMove.Unknown;
            return PRIMARY_MOVES[n];
        }

        public static WrestlingMove GetSecondaryMoveFromGenes(byte[] genes)
        {
            var n = genes[Constants.GENE_MOVE] + genes[Constants.GENE_SKIN] + genes[Constants.GENE_RANDOM];
            n = n % 32;
            if (n >= SECONDARY_MOVES.Length) return WrestlingMove.Unknown;
            return SECONDARY_MOVES[n];
        }

        public static WrestlingMove GetTertiaryMoveFromGenes(byte[] genes)
        {
            var n = genes[Constants.GENE_MOVE] + genes[Constants.GENE_RARITY] + genes[Constants.GENE_RANDOM];
            n = n % 32;
            if (n >= TERTIARY_MOVES.Length) return WrestlingMove.Unknown;
            return TERTIARY_MOVES[n];
        }

        public static WrestlingMove GetStanceMoveFromGenes(byte[] genes)
        {
            var n = genes[Constants.GENE_MOVE] + genes[Constants.GENE_STANCE] + genes[Constants.GENE_RANDOM];
            n = n % 16;
            if (n >= STANCE_MOVES.Length) return WrestlingMove.Unknown;
            return STANCE_MOVES[n];
        }

        public static WrestlingMove GetTagMoveFromGenes(byte[] genes)
        {
            var n = genes[Constants.GENE_MOVE] + genes[Constants.GENE_RANDOM];
            n = n % 16;
            if (n >= TAG_MOVES.Length) return WrestlingMove.Unknown;
            return TAG_MOVES[n];
        }

        public static WrestlingMove GetMoveFromMoveset(byte[] genes, BigInteger slot, BattleStance stance)
        {
            var slotIndex = (int) slot;

            switch (slotIndex)
            {
                case 0: return WrestlingMove.Idle;

                case 1:
                    switch (stance)
                    {
                        case BattleStance.Main:
                            return GetPrimaryMoveFromGenes(genes);

                        case BattleStance.Alternative:
                            return GetTertiaryMoveFromGenes(genes);

                        case BattleStance.Bizarre:
                            return WrestlingMove.Ritual;

                        case BattleStance.Clown:
                            return WrestlingMove.Trick;

                        case BattleStance.Zombie:
                            return WrestlingMove.Chomp;

                        default:
                            return WrestlingMove.Unknown;
                    }

                case 2:
                    switch (stance)
                    {
                        case BattleStance.Main:
                            return WrestlingMove.Smash;

                        case BattleStance.Alternative:
                            return GetSecondaryMoveFromGenes(genes);

                        case BattleStance.Bizarre:
                            return WrestlingMove.Bizarre_Ball;

                        case BattleStance.Clown:
                            return WrestlingMove.Tart_Throw;

                        case BattleStance.Zombie:
                            return WrestlingMove.Scream;

                        default:
                            return WrestlingMove.Unknown;
                    }


                case 3:
                    switch (stance)
                    {
                        case BattleStance.Bizarre:
                            return WrestlingMove.Anti_Counter;

                        default:
                            return WrestlingMove.Counter;
                    }

                case 4:
                    switch (stance)
                    {
                        case BattleStance.Zombie:
                            return WrestlingMove.Fart;

                        case BattleStance.Clown:
                            return WrestlingMove.Joker;

                        default:
                            return GetStanceMoveFromGenes(genes);
                    }

                case 5: return GetTagMoveFromGenes(genes);

                case 6: return WrestlingMove.Forfeit;

                default: return WrestlingMove.Unknown;
            }
        }

        public static bool IsPrimaryMove(WrestlingMove move)
        {
            foreach (var entry in PRIMARY_MOVES)
            {
                if (entry == move)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsSecondaryMove(WrestlingMove move)
        {
            foreach (var entry in SECONDARY_MOVES)
            {
                if (entry == move)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsTertiaryMove(WrestlingMove move)
        {
            foreach (var entry in TERTIARY_MOVES)
            {
                if (entry == move)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsStanceMove(WrestlingMove move)
        {
            foreach (var entry in STANCE_MOVES)
            {
                if (entry == move)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsTagMove(WrestlingMove move)
        {
            foreach (var entry in TAG_MOVES)
            {
                if (entry == move)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsCounter(WrestlingMove atkMove, ItemKind attackerItem, WrestlingMove defMove, ItemKind defenderItem)
        {
            if (attackerItem == ItemKind.Nullifier)
            {
                defenderItem = ItemKind.None;
            }

            return defMove == WrestlingMove.Counter && (atkMove == WrestlingMove.Smash || defenderItem == ItemKind.Gyroscope)
                || defMove == WrestlingMove.Anti_Counter && (atkMove != WrestlingMove.Smash || defenderItem == ItemKind.Gyroscope);
        }

        public static bool IsReleasedItem(ItemKind item)
        {
            switch (item)
            {
                case ItemKind.Gym_Card: return true;
                case ItemKind.Creepy_Toy: return true;
                case ItemKind.Meat_Snack: return true;
                case ItemKind.Golden_Bullet: return true;
                case ItemKind.Pincers: return true;
                case ItemKind.Virus_Chip: return true;
                case ItemKind.Envy_Mask: return true;
                case ItemKind.Vampire_Teeth: return true;

                case ItemKind.Magnifying_Glass: return true;
                case ItemKind.Body_Armor: return true;

                case ItemKind.Dumbell: return true;
                case ItemKind.Gyroscope: return true;
                case ItemKind.Dojo_Belt: return true;
                case ItemKind.Battle_Helmet: return true;
                case ItemKind.Steel_Boots: return true;

                case ItemKind.Lucky_Charm: return true;

                case ItemKind.Spike_Vest: return true;
                case ItemKind.Trap_Card: return true;
                case ItemKind.Nullifier: return true;

                case ItemKind.Wrist_Weights: return true;
                case ItemKind.Yo_Yo: return true;
                case ItemKind.Dice: return true;
                case ItemKind.Giant_Pillow: return true;
                case ItemKind.Killer_Gloves: return true;

                case ItemKind.Rare_Taco: return true;
                case ItemKind.XP_Perfume: return true;
                case ItemKind.Lemon_Shake: return true;
                case ItemKind.Nanobots: return true;
                case ItemKind.Nails: return true;
                case ItemKind.Ignition_Chip: return true;
                case ItemKind.Wood_Chair: return true;
                case ItemKind.Hand_Bandages: return true;
                case ItemKind.Gas_Mask: return true;

                case ItemKind.Love_Lotion: return true;
                case ItemKind.Vigour_Juice: return true;
                case ItemKind.Power_Juice: return true;

                case ItemKind.Bomb: return true;
                case ItemKind.Claws: return true;
                case ItemKind.Shell: return true;
                case ItemKind.Spy_Specs: return true;
                case ItemKind.Stamina_Pill: return true;
                case ItemKind.Attack_Pill: return true;
                case ItemKind.Defense_Pill: return true;
                case ItemKind.Chilli_Bottle: return true;
                case ItemKind.Focus_Banana: return true;
                case ItemKind.Fork: return true;
                case ItemKind.Gnome_Cap: return true;
                case ItemKind.Yellow_Card: return true;

                case ItemKind.Magic_Mirror: return true;
                case ItemKind.Clown_Nose: return true;
                case ItemKind.Spinner: return true;

                case ItemKind.Cooking_Hat: return true;
                case ItemKind.Shock_Chip: return true;

                case ItemKind.Soul_Poster: return true;
                case ItemKind.Trophy_Case: return true;

                case ItemKind.Tequilla: return true;
                case ItemKind.Sombrero: return true;
                case ItemKind.Poncho: return true;
                case ItemKind.Spoon: return true;

                case ItemKind.Serious_Mask: return true;
                case ItemKind.Mummy_Bandages: return true;

                case ItemKind.Blue_Gear: return true;
                case ItemKind.Purple_Cog: return true;
                case ItemKind.Red_Spring: return true;

                case ItemKind.Stance_Block: return true;

                case ItemKind.Make_Up_Common: return true;
                case ItemKind.Make_Up_Uncommon: return true;
                case ItemKind.Make_Up_Rare: return true;
                case ItemKind.Make_Up_Epic: return true;
                case ItemKind.Make_Up_Legendary: return true;
                //case ItemKind.Avatar: return true;

                // those should never be released
                case ItemKind.Dev_Badge: return false;

                default:
                    return false; // item <= ItemKind.Mummy_Bandages;
            }
        }

        public static ItemCategory GetItemCategory(ItemKind itemKind)
        {
            switch (itemKind)
            {
                //case ItemKind.Avatar:
                //    return ItemCategory.Avatar;

                case ItemKind.Dumbell:
                case ItemKind.Gym_Card:
                    return ItemCategory.Gym;

                case ItemKind.Trophy_Case:
                case ItemKind.Soul_Poster:
                    return ItemCategory.Decoration;

                case ItemKind.Chilli_Bottle:
                case ItemKind.Rare_Taco:
                case ItemKind.Stamina_Pill:
                case ItemKind.Attack_Pill:
                case ItemKind.Defense_Pill:
                case ItemKind.Love_Lotion:
                case ItemKind.XP_Perfume:
                case ItemKind.Dev_Badge:
                case ItemKind.Make_Up_Common:
                case ItemKind.Make_Up_Uncommon:
                case ItemKind.Make_Up_Rare:
                case ItemKind.Make_Up_Epic:
                case ItemKind.Make_Up_Legendary:
                    return ItemCategory.Consumable;

                default:
                    return ItemCategory.Battle;
            }
        }
    }

    #endregion

        #region FORMULAS
    public static class Formulas
    {
        public const int MaxBaseStat = 120;
        public const int BaseStatSplit = MaxBaseStat / 3;
        public const int MaxTrainStat = 256; // this is the total for all 3 stats together!

        public static int CalculateWrestlerStat(int level, int baseStat, int trainStat)
        {
            level *= 6; // 16 * 6.25 = 100
            var result = (baseStat + 100) * 2;
            result = result + (trainStat / 4);
            result = result * (int)level;
            result = result / 100;
            result = result + 1;
            return result;
        }

        public static int CalculateStamina(int currentStamina, int fullStamina)
        {
            // TODO usar isto qd for para calcular a percentagem da stamina
            int finalPercent = (int)Math.Round(currentStamina / (float)fullStamina * 100);
            if (finalPercent <= 0 && currentStamina > 0)
            {
                finalPercent = 1;
            }

            return finalPercent;
        }

        public static int CalculateWrestlerLevel(int XP)
        {
            return 1 + (Sqrt(XP) / 61);
        }

        public static int CalculateXpPercentage(int currentXp, int currentLevel)
        {
            if (currentLevel == Constants.MAX_LEVEL) return 100;

            var requiredXp = Constants.EXPERIENCE_MAP[currentLevel + 1] - Constants.EXPERIENCE_MAP[currentLevel];
            var levelXp = currentXp - Constants.EXPERIENCE_MAP[currentLevel];
            return (int)((levelXp * 100) / requiredXp);
        }

        public static int CalculateDamage(int level, int atk, int def, int rnd, int power)
        {
            var result = (int)((2 * level) / 5) + 2;
            result = (result * (power * atk) / def);
            result *= Constants.DAMAGE_FACTOR_NUMERATOR;
            result = (int)(result / Constants.DAMAGE_FACTOR_DENOMINATOR);
            result += 2;

            // between 0 and 100
            var mod = (rnd % 16);
            mod = mod + 85;

            result = result * mod;
            result = result / 100;

            result += 2; // make sure damage never goes below 2, to be divisible by 2 etc

            return result;
        }

        // calculates integer approximation of Sqrt(n)
        public static int Sqrt(int n)
        {
            return (int)Math.Sqrt(n);
            /*int root = n / 2;

            while (n < root * root)
            {
                root += n / root;
                root /= 2;
            }

            return root;*/
        }

        //public static ItemKind GetItemKind(BigInteger itemID)
        //{
        //    if (itemID <= 0)
        //    {
        //        return ItemKind.None;
        //    }

        //    var bytes = CryptoExtensions.Sha256(itemID.ToByteArray());
        //    var num = 1 + bytes[0] + bytes[1] * 256;
        //    return (ItemKind)num;
        //}

        public static int GetAvatarID(BigInteger itemID)
        {
            if (itemID <= 0)
            {
                return 0;
            }

            var bytes = CryptoExtensions.Sha256(itemID.ToByteArray());
            var num = 1 + bytes[2];
            return num;
        }

        public static int CalculateBaseStat(byte[] genes, StatKind stat)
        {
            return Formulas.BaseStatSplit + genes[(byte)stat] % Formulas.BaseStatSplit + GetProfileStat(genes, stat);
        }

        public static LuchadorHoroscope GetHoroscopeSign(byte[] genes)
        {
            return (LuchadorHoroscope)((genes[Constants.GENE_RANDOM] + genes[Constants.GENE_PROFILE]) % 19);
        }

        public static int GetProfileStat(byte[] genes, StatKind stat)
        {
            var seed = genes[Constants.GENE_ATK] + genes[Constants.GENE_DEF] + genes[Constants.GENE_STAMINA];
            seed *= genes[Constants.GENE_PROFILE];
            var points = seed % Formulas.BaseStatSplit;

            var sign = GetHoroscopeSign(genes);

            var statGrid = Constants.horoscopeStats[sign];
            var statIndex = (int)stat - 1;
            byte multiplier = statGrid[statIndex];
            if (multiplier > 4) multiplier = 4;
            multiplier = (byte)(4 - multiplier);
            if (multiplier == 0)
            {
                return 0;
            }
            return points / multiplier;
        }

        public static int FindItemTotal()
        {
            var names = Enum.GetNames(typeof(ItemKind)).Where(x => x != "None").ToArray();
            return names.Length;
        }

        public static int CountNumberOfStatus(BattleStatus flags)
        {
            int result = 0;
            for (int i = 0; i < 16; i++)
            {
                var flag = (BattleStatus)(1 << i);
                if (flags.HasFlag(flag))
                {
                    result++;
                }
            }
            return result;
        }

    }
    #endregion

    #region CONSTANTS
    // TODO clean up stuff that is not related to backend
    public static class Constants
    {
        public const string ANONYMOUS_NAME = "Anonymous";
        public const string ACADEMY_NAME = "Academy";
        
        public const string SOUL_SYMBOL = "SOUL";
        public const string NACHO_SYMBOL = "NACHO";
        public const string WRESTLER_SYMBOL = "LUCHA";
        public const string ITEM_SYMBOL = "ITEMS"; // TODO later combine both items and wrestlers into a single token

        public const int NACHO_TOKEN_DECIMALS = 10;

        public const int LOOTBOX_SALE_RANKED_POT_FEE = 10; // 10%

        public const uint NEO_TICKER = 1376;
        public const uint SOUL_TICKER = 2827;

        public const int DEFAULT_AVATARS = 10;

        public const int RANKED_BATTLE_ENTRY_COST       = 5;
        public const int RANKED_BATTLE_WINNER_PRIZE     = 10;
        public const int RANKED_BATTLE_DRAW_PRIZE       = 6;
        public const int RANKED_BATTLE_LOSER_PRIZE      = 2;

        public const int UNRANKED_BATTLE_WINNER_PRIZE   = 5;
        public const int UNRANKED_BATTLE_DRAW_PRIZE     = 3;
        public const int UNRANKED_BATTLE_LOSER_PRIZE    = 1;

        public const decimal DOLLAR_NACHOS_RATE = 100; // 1 USD = 100 NACHOS //TODO set this with the in-apps conversion rate

        public const string PHANTASMA_DEV_ADDRESS = "PGUHKgY6o72fTQCBHstFcBNwqfaFKMFAEGDr2pfxWg5bV";

        public const string NEODepositAddress = "AXAZLXZHuUWAzFkyEZKSPLyVmZA4dNjepx";
        public const decimal NEOPurchaseFee = 0.15m;

        // minimum XP to reach each level. where the level = index of array
        public static readonly int[] EXPERIENCE_MAP = new int[] { 0, 0, 3721, 14884, 33489, 59536, 93025, 133956, 182329, 238144, 301401, 372100, 450241, 535824, 628849, 729316, 837225, 952576, 1075369, 1205604, 1343281 };

        // minimum vip points to reach each vip level. where the level = index of array
        public static readonly int[] VIP_LEVEL_POINTS = new int[] { 0, 250, 500, 1000, 2500, 5000, 10000, 15000, 25000, 35000, 50000 };

        public static readonly Dictionary<Rarity, List<ItemKind>> ITEMS_RARITY = 
            new Dictionary<Rarity, List<ItemKind>>
            {
                {
                    Rarity.Common,
                    new List<ItemKind>()
                    {
                        ItemKind.Expert_Gloves, ItemKind.Vigour_Juice, ItemKind.Power_Juice, ItemKind.Claws, ItemKind.Shell,
                        ItemKind.Chilli_Bottle, ItemKind.Fork, ItemKind.Gnome_Cap, ItemKind.Wood_Potato, ItemKind.Make_Up_Common
                    }
                },
                {
                    Rarity.Uncommon,
                    new List<ItemKind>()
                    {
                        ItemKind.Muscle_Overclocker, ItemKind.Spike_Vest, ItemKind.Dice, ItemKind.Lemon_Shake, ItemKind.Gas_Mask, ItemKind.Ignition_Chip, ItemKind.Bomb, ItemKind.Stamina_Pill,
                        ItemKind.Attack_Pill, ItemKind.Defense_Pill, ItemKind.Rubber_Suit, ItemKind.Mummy_Bandages, ItemKind.Purple_Cog, ItemKind.Red_Spring, ItemKind.Blue_Gear, ItemKind.Make_Up_Uncommon
                    }
                },
                {
                    Rarity.Rare,
                    new List<ItemKind>()
                    {
                        ItemKind.Battle_Helmet, ItemKind.Steel_Boots, ItemKind.Envy_Mask, ItemKind.Giant_Pillow, ItemKind.Killer_Gloves, ItemKind.Rare_Taco, ItemKind.XP_Perfume,
                        ItemKind.Nails, ItemKind.Clown_Nose, ItemKind.Hand_Bandages, ItemKind.Love_Lotion, ItemKind.Nanobots, ItemKind.Make_Up_Rare
                    }
                },
                {
                    Rarity.Epic,
                    new List<ItemKind>()
                    {
                        ItemKind.Gym_Card, ItemKind.Creepy_Toy, ItemKind.Loser_Mask, ItemKind.Ego_Mask, ItemKind.Trainer_Speaker, ItemKind.Vampire_Teeth, ItemKind.Magnifying_Glass,
                        ItemKind.Body_Armor, ItemKind.Dumbell, ItemKind.Lucky_Charm, ItemKind.Spinner, ItemKind.Wood_Chair, ItemKind.Spy_Specs, ItemKind.Focus_Banana, ItemKind.Yellow_Card,
                        ItemKind.Magic_Mirror, ItemKind.Stance_Block, ItemKind.Wrist_Weights, ItemKind.Tequilla, ItemKind.Sombrero, ItemKind.Poncho, ItemKind.Make_Up_Epic
                    }
                },
                {
                    Rarity.Legendary,
                    new List<ItemKind>()
                    {
                        ItemKind.Bling, ItemKind.Meat_Snack, ItemKind.Golden_Bullet, ItemKind.Pincers, ItemKind.Ancient_Trinket, ItemKind.Virus_Chip, ItemKind.Gyroscope, ItemKind.Dojo_Belt,
                        ItemKind.Nullifier, ItemKind.Yo_Yo, ItemKind.Trap_Card, ItemKind.Dev_Badge, ItemKind.Make_Up_Legendary
                    }
                }
            };

        public static readonly Dictionary<int, DailyRewards> VIP_DAILY_LOOT_BOX_REWARDS = new Dictionary<int, DailyRewards>()
        {
            { 0,    new DailyRewards {vipWrestlerReward = 0, vipItemReward = 0, vipMakeUpReward = 0} },
            { 1,    new DailyRewards {vipWrestlerReward = 0, vipItemReward = 0, vipMakeUpReward = 1} },
            { 2,    new DailyRewards {vipWrestlerReward = 0, vipItemReward = 1, vipMakeUpReward = 1} },
            { 3,    new DailyRewards {vipWrestlerReward = 1, vipItemReward = 1, vipMakeUpReward = 1} },
            { 4,    new DailyRewards {vipWrestlerReward = 1, vipItemReward = 1, vipMakeUpReward = 3} },
            { 5,    new DailyRewards {vipWrestlerReward = 2, vipItemReward = 2, vipMakeUpReward = 3} },
            { 6,    new DailyRewards {vipWrestlerReward = 3, vipItemReward = 3, vipMakeUpReward = 3} },
            { 7,    new DailyRewards {vipWrestlerReward = 3, vipItemReward = 3, vipMakeUpReward = 4} },
            { 8,    new DailyRewards {vipWrestlerReward = 3, vipItemReward = 3, vipMakeUpReward = 5} },
            { 9,    new DailyRewards {vipWrestlerReward = 4, vipItemReward = 4, vipMakeUpReward = 5} },
            { 10,   new DailyRewards {vipWrestlerReward = 5, vipItemReward = 5, vipMakeUpReward = 5} },
        };

        public static readonly Dictionary<ELOOT_BOX_TYPE, int> LOOT_BOX_NACHOS_PRICE = new Dictionary<ELOOT_BOX_TYPE, int>()
        {
            { ELOOT_BOX_TYPE.WRESTLER,  50 },
            { ELOOT_BOX_TYPE.ITEM,      25 },
            { ELOOT_BOX_TYPE.MAKE_UP,   10 },
            //{ ELOOT_BOX_TYPE.AVATAR,    10 },
        };

        public static readonly Dictionary<int, int> IN_APPS_DOLLAR_PRICE = new Dictionary<int, int>()
        {
            { 0,    1 },
            { 1,    2 },
            { 2,    5 },
            { 3,    10 },
            { 4,    20 },
            { 5,    50 },
            { 6,    100 },
            { 7,    150 }
        };

        public static readonly Dictionary<int, int> IN_APPS_NACHOS = new Dictionary<int, int>()
        {
            { 0,    100 },
            { 1,    250 },
            { 2,    600 },
            { 3,    1300 },
            { 4,    2750 },
            { 5,    7500 },
            { 6,    17500 },
            { 7,    30000 }
        };

        public const int CHANGE_FACTION_COST = 500;

        public const int UPDATE_MARKET_CONVERSIONS_INTERVAL = 5; // minutes

        public const int MIN_LEVEL = 1;
        // max level than a luchador can reach
        public const int MAX_LEVEL = 16;

        // average expected XP per battle (taking account all battles from lv 1 to max level)
        public const int AVERAGE_XP_GAIN = 4372;

        public const int BOT_EXPERIENCE_PERCENT = 80;

        public static int WRESTLER_MAX_XP => EXPERIENCE_MAP[MAX_LEVEL];

        // minimum XP that a luchador can receive per battle
        public const int MINIMUM_XP_PER_BATTLE = 20;

        public const int QUEUE_FORCE_BOT_SECONDS = 30; //force a bot fight with people who are queued for this long

        // default max mojo of a luchador
        public const int DEFAULT_MOJO = 3;

        public const int MAX_MOJO = 6;

        // minimum time in minutes to refresh mojo of a luchador 
        public const uint MOJO_REFRESH_MINUTES = 15;

        public static int MAX_PRATICE_LEVEL = 8;

        public const uint BASE_LUCHADOR_ID = 1; // Wrestler Ids = [1, ...[   &&   Bot Ids = [-1, -99]
        public const uint LUCHADOR_GENERATION_SIZE = 1000;

        public const int MINIMUM_SOUL_TRANSFER_AMOUNT = 1;

        public static readonly BigInteger MINIMUM_AUCTION_PRICE = UnitConversion.ToBigInteger(0.01m, Nexus.StakingTokenDecimals);
        public static readonly BigInteger MAXIMUM_AUCTION_PRICE = UnitConversion.ToBigInteger(100000.0m, Nexus.StakingTokenDecimals);

        public static readonly int MINIMUM_AUCTION_DURATION = 86400; // in seconds 
        public static readonly int MAXIMUM_AUCTION_DAYS_DURATION = 30;
        public static readonly int MAXIMUM_AUCTION_SECONDS_DURATION = 86400 * MAXIMUM_AUCTION_DAYS_DURATION; // in seconds 

        public const int MINIMUM_BET = 100;
        public const int MINIMUM_POT_SIZE = 100;

        public const uint MINIMUM_MINUTES_FOR_CANCEL = 2;
        public const uint MINIMUM_MINUTES_FOR_IDLE = 1;

        public const uint GENE_RARITY = 0;
        public const uint GENE_STAMINA = 1;
        public const uint GENE_ATK = 2;
        public const uint GENE_DEF = 3;
        public const uint GENE_COUNTRY = 4;
        public const uint GENE_MOVE = 5;
        public const uint GENE_RANDOM = 6;
        public const uint GENE_SKIN = 7;
        public const uint GENE_STANCE = 8;
        public const uint GENE_PROFILE = 9;

        public const uint MOVE_OVERRIDE_COUNT = 5;

        // those two constants express a fraction, when is damage calculated it is multiplied by this fraction
        public static int DAMAGE_FACTOR_NUMERATOR = 20;
        public static int DAMAGE_FACTOR_DENOMINATOR = 43;

        // damage values for different moves
        // NOTE: some of those values might seem higher when compared to others, usually higher values means the move has some special condition
        public const int DAMAGE_CHOP_POWER = 13;
        public const int DAMAGE_MEGA_CHOP_POWER = 17;
        public const int DAMAGE_SMASH_POWER = 25;
        public const int DAMAGE_GUTBUSTER_POWER = 40;
        public const int DAMAGE_RHINO_CHARGE_POWER = 40;
        public const int DAMAGE_GRIP_POWER = 10;
        public const int DAMAGE_RAZOR_JAB_POWER = 8;
        public const int DAMAGE_SPINNING_CRANE_POWER = 6;
        public const int DAMAGE_CHICKEN_WING_POWER = 8;
        public const int DAMAGE_SIDE_HOOK_POWER = 35;
        public const int DAMAGE_HAMMERHEAD_POWER = 30;
        public const int DAMAGE_LEG_TWISTER_POWER = 14;
        public const int DAMAGE_WOLF_CLAW_POWER = 12;
        public const int DAMAGE_FART_POWER = 12;
        public const int DAMAGE_OCTOPUS_ARM_POWER = 8;
        public const int DAMAGE_IRON_FIST_POWER = 25;
        public const int DAMAGE_CHOMP_POWER = 9;
        public const int DAMAGE_TART_THROW_POWER = 20;
        public const int DAMAGE_FIRE_BREATH_POWER = 8;
        public const int DAMAGE_FIRE_FANG_POWER = 12;
        public const int DAMAGE_NEEDLE_STING_POWER = 8;
        public const int DAMAGE_HEAD_SPIN_POWER = 14;
        public const int DAMAGE_KNEE_BOMB_POWER = 30;
        public const int DAMAGE_SMILE_CRUSH_POWER = 35;
        public const int DAMAGE_RAGE_PUNCH_POWER = 14;
        public const int DAMAGE_FLYING_KICK_POWER = 12;
        public const int DAMAGE_PALM_STRIKE_POWER = 16;
        public const int DAMAGE_BIZARRE_BALL_POWER = 2;
        public const int DAMAGE_HEPER_SLAM_POWER = 35;
        public const int DAMAGE_SICK_SOCK_POWER = 12;
        public const int DAMAGE_HEADSPIN_POWER = 13;
        public const int DAMAGE_KNOCK_OFF_POWER = 8;
        public const int DAMAGE_PSY_STRIKE_POWER = 7;
        public const int DAMAGE_MIND_SLASH = 20;
        public const int DAMAGE_HYPER_SLAM = 35;
        public const int DAMAGE_DRUKEN_FIST_POWER = 22;
        public const int DAMAGE_FLAILING_ARMS_POWER_WEAK = 15;
        public const int DAMAGE_FLAILING_ARMS_POWER_STRONG = 23;
        public const int DAMAGE_GORILLA_CANNON_POWER = 14;

        public const int DAMAGE_GOOD_CHOP_POWER = 15;
        public const int DAMAGE_BAD_CHOP_POWER = 11;

        public const int DAMAGE_SLINGSHOT_MAX = 27;
        public const int DAMAGE_SLINGSHOT_HIGH = 23;
        public const int DAMAGE_SLINGSHOT_LOW = 20;
        public const int DAMAGE_SLINGSHOT_MIN = 15;

        public const int DAMAGE_BUTTERFLY_KICK_ODD_TURN_POWER = DAMAGE_CHOP_POWER + 2;
        public const int DAMAGE_BUTTERFLY_KICK_EVEN_TURN_POWER = DAMAGE_CHOP_POWER - 2;

        public const int DAMAGE_MONKEY_SLAP_SAME_STANCE_POWER = DAMAGE_CHOP_POWER + 2;
        public const int DAMAGE_MONKEY_SLAP_DIFFERENT_STANCE_POWER = DAMAGE_CHOP_POWER - 2;

        public const int DAMAGE_MOTH_DRILL_ODD_TURN_POWER = DAMAGE_CHOP_POWER - 2;
        public const int DAMAGE_MOTH_DRILL_EVEN_TURN_POWER = DAMAGE_CHOP_POWER + 2;

        public const int DAMAGE_HEART_PUNCH_NORMAL_POWER = 14;
        public const int DAMAGE_HEART_PUNCH_MAX_POWER = 20;

        public const int DAMAGE_BACK_SLAP_NORMAL_POWER = 14;
        public const int DAMAGE_BACK_SLAP_MAX_POWER = 20;

        public const int DAMAGE_REHAB_NORMAL_POWER = 14;
        public const int DAMAGE_REHAB_MAX_POWER = 20;

        public const int DAMAGE_RAGE_PUNCH_NORMAL_POWER = 12;
        public const int DAMAGE_RAGE_PUNCH_MAX_POWER = 24;

        public const int DAMAGE_BASH_NORMAL_POWER = 13;
        public const int DAMAGE_BASH_MAX_POWER = 26;

        public const int DAMAGE_ARMLOCK_NORMAL_POWER = 14;
        public const int DAMAGE_ARMLOCK_MAX_POWER = 28;

        public const int DAMAGE_BOOMERANG_NORMAL_POWER = 13;
        public const int DAMAGE_BOOMERANG_MAX_POWER = 28;

        public const int DAMAGE_TORPEDO_MIN_POWER = 6;
        public const int DAMAGE_TORPEDO_MAX_POWER = 20;

        public const int DAMAGE_TAKEDOWN_POWER = 20;
        public const int DAMAGE_TAKEDOWN_RECOIL_PERCENT = 20;

        public const int DAMAGE_AVALANCHE_POWER = 12;
        public const int DAMAGE_AVALANCHE_INCREASE_ATTACK_PERCENTAGE = 10;

        public const int DAMAGE_CORKSCREW_POWER = 12;
        public const int DAMAGE_CORKSCREW_INCREASE_DEFENSE_PERCENTAGE = 10;

        public const int DAMAGE_MUSCLE_OVERCLOCKER_PERCENTAGE = 10;
        public const int DAMAGE_CHAIR_PERCENTAGE = 125;

        public const int ZEN_CHARGE_PERCENTAGE = 20;
        public const int ZEN_RELEASE_PERCENTAGE = 175;

        public const int TART_THROW_ACCURACY = 50;
        public const int KNEE_BOMB_ACCURACY = 50;
        public const int SMALL_SIDE_EFFECT_ACCURACY = 33;
        public const int BIG_SIDE_EFFECT_ACCURACY = 66;
        public const int CONFUSION_ACCURACY = 80; // this is inverted actuall it is the accuracy of confusion not working in a turn

        public const int LOW_STAMINA_ITEM_PERCENT = 33;

        public const int MARACAS_BOOST_PERCENTAGE = 25;
        public const int BONGOS_BOOST_PERCENTAGE = 25;

        public const int BLEEDING_DAMAGE_PERCENT = 10;
        public const int BURNING_DAMAGE_PERCENT = 10;
        public const int POISON_DAMAGE_PERCENT = 10;
        public const int BOMB_DAMAGE_PERCENT = 25;
        public const int FORK_DAMAGE_PERCENT = 10;
        public const int SHOCK_CHIP_DAMAGE_PERCENT = 10;
        public const int TRAINER_SPEAKER_RECOVER_PERCENT = 33;
        public const int LEMON_SHAKE_RECOVER_PERCENT = 25;
        public const int TEQUILLA_RECOVER_PERCENT = 5;
        public const int BOTTLE_SIP_RECOVER_PERCENT = 15;
        public const int GOBBLE_RECOVER_PERCENT = 15;
        public const int REFRESH_RECOVER_PERCENT = 10;
        public const int MEAT_SNACK_RECOVER_PERCENT = 15;
        public const int MANTRA_RECOVER_PERCENT = 10;
        public const int WOOD_POTATO_RECOVER_PERCENT = 10;
        public const int VAMPIRE_TEETH_RECOVER_PERCENT = 10;
        public const int ZOMBIE_RECOVER_PERCENT = 25;
        public const int GIANT_PILLOW_PERCENT = 15;
        public const int IRON_FIST_DEFENSE_REDUCTION = 10;
        public const int SICK_SOCK_DAMAGE_PERCENT = 20;

        public const int NAILS_DAMAGE_PERCENT = 10;
        public const int DYNAMITE_RIG_PERCENT = 20;

        public const int SPOON_BOOST_PERCENT = 20;

        public const int BATTLE_HELMET_DAMAGE_BOOST = 25;
        public const int BATTLE_STEEL_BOOTS_DAMAGE_BOOST = 25;
        public const int BATTLE_GNOME_CAP_DAMAGE_BOOST = 50;
        public const int KILLER_GLOVES_BOOST_PERCENT = 50;
        public const int DRUNK_BOOST_PERCENT = 10;

        // percentages used for stat boosts during battle
        public const int BIG_BOOST_CHANGE = 30;
        public const int SMALL_BOOST_CHANGE = 15;

        public const int GYM_CARD_DAYS = 5;
        public const int DUMBELL_BOOST_PERCENTAGE = 50;

        public const int ACCOUNT_COUNTER_WINS_UNRANKED = 0;
        public const int ACCOUNT_COUNTER_LOSSES_UNRANKED = 1;
        public const int ACCOUNT_COUNTER_DRAWS_UNRANKED = 2;
        public const int ACCOUNT_COUNTER_WINS_RANKED = 3;
        public const int ACCOUNT_COUNTER_LOSSES_RANKED = 4;
        public const int ACCOUNT_COUNTER_DRAWS_RANKED = 5;
        public const int ACCOUNT_COUNTER_CURRENT_STREAK = 6;
        public const int ACCOUNT_COUNTER_LONGEST_STREAK = 7;
        public const int ACCOUNT_COUNTER_POT_COUNT = 8;
        public const int ACCOUNT_COUNTER_MARKET_SALES = 9;
        public const int ACCOUNT_COUNTER_CURRENT_GYM_COUNT = 10;
        public const int ACCOUNT_COUNTER_GYM_UPGRADES = 11;
        public const int ACCOUNT_COUNTER_MAX = 12;

        public const int BATTLE_COUNTER_START_TIME = 0;
        public const int BATTLE_COUNTER_CHARGE_A = 1;
        public const int BATTLE_COUNTER_CHARGE_B = 2;
        public const int BATTLE_COUNTER_ACCUM_A = 3;
        public const int BATTLE_COUNTER_ACCUM_B = 4;
        public const int BATTLE_COUNTER_DRINK_A = 5;
        public const int BATTLE_COUNTER_DRINK_B = 6;
        public const int BATTLE_COUNTER_GORILLA_A = 7;
        public const int BATTLE_COUNTER_GORILLA_B = 8;
        public const int BATTLE_COUNTER_POISON_A = 9;
        public const int BATTLE_COUNTER_POISON_B = 10;
        public const int BATTLE_COUNTER_MAX = 11;

        public const int DRINKING_LIMIT = 2;

        // default ELO rating of a player
        public const int DEFAULT_SCORE = 1000;

        public static readonly int SECONDS_PER_DAY = 86400;
        public static readonly int SECONDS_PER_HOUR = 3600;

        public const int MAX_GYM_BOOST = 255;
        public const int PILL_GYM_BOOST = 16;

        public const int GYM_LIMIT_PER_UPGRADE = 8;
        public const int MAX_GYM_UPGRADES = 0;

        public const int XP_PERFUME_DURATION_IN_HOURS = 12;

        public static int MAX_COMMENT_LENGTH = 140;

        public const int MIN_NAME_CHAR_LENGTH = 4;
        public const int MAX_NAME_CHAR_LENGTH = 15;

        public static int LUCHADOR_COMMENT_INTRO = 0;
        public static int LUCHADOR_COMMENT_EASY_WIN = 1;
        public static int LUCHADOR_COMMENT_HARD_WIN = 2;
        public static int LUCHADOR_COMMENT_DRAW = 3;
        public static int LUCHADOR_COMMENT_FAST_LOSE = 4;
        public static int LUCHADOR_COMMENT_SLOW_LOSE = 5;
        public static int LUCHADOR_COMMENT_MINDREAD = 6;
        public static int LUCHADOR_COMMENT_ITEM = 7;
        public static int LUCHADOR_COMMENT_REVERSAL = 8;
        public static int LUCHADOR_COMMENT_MAX = 9;

        public static int LOOT_BOX_POT_NACHOS_PERCENTAGE                = 5;
        public static int LOOT_BOX_FACTION_REWARD_NACHOS_PERCENTAGE     = 20;
        public static int NACHOS_IN_APPS_SOUL_REWARD_SOUL_PERCENTAGE    = 25;

        public static int REFERRAL_STAKE_AMOUNT = 100;
        public static int REFERRAL_MINIMUM_DAYS = 30;

        public const int MATCHMAKER_UPDATE_SECONDS = 5;
        public const int MATCHMAKER_REMOVAL_SECONDS = 600;

        public const int DEFAULT_ELO = 1200;

        // percentage distributions for pot in ranked mode
        public static readonly int[] POT_PERCENTAGES = new int[]
        {
            50,
            20,
            10,
            5,
            5,
            2,
            2,
            2,
            2,
            2
        };

        public static readonly Dictionary<LuchadorHoroscope, byte[]> horoscopeStats = new Dictionary<LuchadorHoroscope, byte[]>()
        {
            { LuchadorHoroscope.House, new byte[]{ 2, 2, 2 } },
            { LuchadorHoroscope.Crocodile, new byte[]{ 2, 4, 1 } },
            { LuchadorHoroscope.Wind, new byte[]{ 0, 3, 2} },
            { LuchadorHoroscope.Lizard, new byte[]{ 0, 2, 4} },
            { LuchadorHoroscope.Serpent, new byte[]{ 1, 4, 0} },
            { LuchadorHoroscope.Death, new byte[]{ 2, 4, 0} },
            { LuchadorHoroscope.Rabbit, new byte[]{ 1, 0, 3} },
            { LuchadorHoroscope.Water, new byte[]{ 1, 0, 4} },
            { LuchadorHoroscope.Dog, new byte[]{ 2, 3, 2} },
            { LuchadorHoroscope.Monkey, new byte[]{ 1, 3, 2 } },
            { LuchadorHoroscope.Grass, new byte[]{ 1, 1, 4} },
            { LuchadorHoroscope.Reed, new byte[]{ 3, 2, 2 } },
            { LuchadorHoroscope.Jaguar, new byte[]{ 2, 3, 1 } },
            { LuchadorHoroscope.Eagle, new byte[]{ 3, 3, 1} },
            { LuchadorHoroscope.Vulture, new byte[]{1, 2, 3 } },
            { LuchadorHoroscope.Earth, new byte[]{ 4, 0, 2} },
            { LuchadorHoroscope.Knife, new byte[]{ 0, 4, 2} },
            { LuchadorHoroscope.Flower, new byte[]{ 2, 0, 3} },
            { LuchadorHoroscope.Storm, new byte[]{ 3, 4, 0 } },
        };

    }
    #endregion

    #region ENUMS
    public enum AuctionKind
    {
        None = 0,
        Luchador = 1,
        Equipment = 2,
    }

    public enum Faction
    {
        None,
        Latinos,
        Gringos
    }

    public enum Gender
    {
        Male,
        Female
    }

    [Flags]
    public enum ItemFlags
    {
        None = 0,
        Locked = 1, // cant be transfered or sold
        Wrapped = 2, // wrapped as gift
    }

    public enum ItemCategory
    {
        Consumable,
        Gym,
        Battle,
        Decoration,
        //Avatar,
        Other
    }

    public enum ELOOT_BOX_TYPE
    {
        WRESTLER,
        ITEM,
        MAKE_UP,
        //AVATAR
    }

    public enum AuctionCurrency
    {
        NACHO,
        //USD,
        //SOUL
    }

    public enum TrophyFlag
    {
        None = 0,
        Dummy = 1,
        Pot = 2,
        Referral = 4,
        Safe = 8,
        Drink = 16,
        Clown = 32,
        Academy = 64,
        One_Hit = 128,
    }

    public enum LuchadorHoroscope
    {
        House,
        Crocodile,
        Wind,
        Lizard,
        Serpent,
        Death,
        Rabbit,
        Water,
        Dog,
        Monkey,
        Grass,
        Reed,
        Jaguar,
        Eagle,
        Vulture,
        Earth,
        Knife,
        Flower,
        Storm,
    }

    public enum ItemKind
    {
        None = 0,
        Claws = 1,
        Shell = 2,
        Rubber_Suit = 3,
        Spike_Vest = 4,
        Vigour_Juice = 5,
        Power_Juice = 6,
        Ego_Mask = 7,
        Envy_Mask = 8,
        Trainer_Speaker = 9,
        Trap_Card = 10,
        Wrist_Weights = 11,
        Vampire_Teeth = 12,
        Nullifier = 13,
        Yo_Yo = 14,
        Dice = 15,
        Magnifying_Glass = 16,
        Muscle_Overclocker = 17,
        Giant_Pillow = 18,
        Killer_Gloves = 19,
        Focus_Banana = 20,
        Spy_Specs = 21,
        Chilli_Bottle = 22,
        Rare_Taco = 23,
        Stamina_Pill = 24,
        Attack_Pill = 25,
        Defense_Pill = 26,
        Body_Armor = 27,
        Lemon_Shake = 28,
        Gym_Card = 29,
        Dumbell = 30,
        Nanobots = 31,
        Bling = 32,
        Creepy_Toy = 33,
        Fork = 34,
        Meat_Snack = 35,
        Nails = 36,
        Lucky_Charm = 37,
        Yellow_Card = 38,
        Pincers = 39,
        Love_Lotion = 40,
        XP_Perfume = 41,
        Bomb = 42,
        Gyroscope = 43,
        Ignition_Chip = 44,
        Wood_Chair = 45,
        Dojo_Belt = 46,
        Hand_Bandages = 47,
        Steel_Boots = 48,
        Gas_Mask = 49,
        Battle_Helmet = 50,
        Ancient_Trinket = 51,
        Golden_Bullet = 52,
        Expert_Gloves = 53,
        Virus_Chip = 54,
        Gnome_Cap = 55,
        Clown_Nose = 56,
        Stance_Block = 57,
        Wood_Potato = 58,
        Mummy_Bandages = 59,
        Serious_Mask = 60,
        Magic_Mirror = 61,
        Loser_Mask = 62,
        Sombrero = 63,
        Poncho = 64,
        Tequilla = 65,
        Spinner = 66, // no confusion
        Black_Card = 67, // no stance change allowed for opponent, once??
        Red_Card = 68, // one hit 
        Cooking_Hat = 69,
        Shock_Chip = 70,
        Dev_Badge = 71,
        Scary_Mask = 72,
        Spoon = 73,
        Maracas = 74,
        Blue_Gear = 75,
        Red_Spring = 76,
        Purple_Cog = 77,
        Soul_Poster = 78,
        Trophy_Case = 79,
        Bongos = 80,
        Astral_Trinket = 81,
        Toxin = 82,
        Speed_Chip = 83, // replaces last move with Smash if no damage done 
        Echo_Box = 84,
        Make_Up_Common = 85,
        Make_Up_Uncommon = 86,
        Make_Up_Rare = 87,
        Make_Up_Epic = 88,
        Make_Up_Legendary = 89,
        //Avatar = 90 // TODO avatars
    }

    public enum Rarity
    {
        Bot = 0,
        Common = 1,     // normal
        Uncommon = 2,   // more "cool" normals
        Rare = 3,       // animals
        Epic = 4,       // memes
        Legendary = 5   // real persons
    }

    public enum BodyPart
    {
        Feet,
        Hands,
        Arms,
        Legs,
        Hips,
        Body,
        Head,
    }

    public enum LeagueRank
    {
        None,
        Bronze,
        Silver,
        Gold,
        Platinum
    }

    public enum PraticeLevel
    {
        None,
        Wood,
        Iron,
        Steel,
        Silver,
        Gold,
        Ruby,
        Emerald,
        Diamond,
    }

    public enum AcademyTutorial
    {
        Novice,
        Beginner,
        Junior,
        Intermediate,
        Skilled,
        Advanced,
        Senior,
        Expert,
        Move1,
        Move2,
        Move3,
        Move4,
        Move5,
        Move6,
        Move7,
        Move8,
        Move9,
        Move10,
        Move11,
        Move12,
        Move13,
        Move14,
        Move15,
        Move16,
        Move17,
        Move18,
        Move19,
        Move20,
        Move21,
        Move22,
        Move23,
        Move24,
        Move25,
    }

    public enum AcademyLevelStatus
    {
        Undefeated,
        Defeated
    }

    public enum StatKind
    {
        None = 0,
        Stamina = 1,
        Attack = 2,
        Defense = 3,
    }

    public enum WrestlerLocation
    {
        None,
        Battle,
        Gym,
        Market,
        Room,
    }

    public enum ItemLocation
    {
        Unknown,
        Wrestler,
        Market,
        Room,
        None,
    }

    public enum MoveCategory
    {
        Other,
        Hands,
        Legs,
        Head,
        Body,
        Mouth,
        Mind,
    }

    public enum WrestlingMove
    {
        Idle,
        Smash,
        Counter,
        Forfeit,
        Unknown,
        Chop,
        Block,
        Refresh,
        Boomerang,
        Bash,
        Taunt,
        Armlock,
        Bulk,
        Scream,
        Avalanche,
        Corkscrew,
        Butterfly_Kick,
        Gutbuster,
        Takedown,
        Rhino_Charge,
        Razor_Jab,
        Hammerhead,
        Knee_Bomb,
        Chicken_Wing,
        Headspin,
        Dodge,
        Pray,
        Side_Hook,
        Wolf_Claw,
        Monkey_Slap,
        Grip,
        Mantra,
        Leg_Twister,
        Heart_Punch,
        Smile_Crush,
        Slingshot,
        Copycat,
        Beggar_Bag,
        Trick,
        Lion_Roar,
        Recycle,
        Gobble,
        Chilli_Dance,
        Sweatshake,
        Spinning_Crane,
        Octopus_Arm,
        Rage_Punch,
        Flying_Kick,
        Torpedo,
        Palm_Strike,
        Torniquet,
        Fart,
        Voodoo,
        Zen_Point,
        Clown_Makeup,
        Wood_Work,
        Catapult,
        Smuggle,
        Barrel_Roll,
        Hurricane,
        Moth_Drill,
        Knock_Off,
        Back_Slap,
        Rehab,
        Hyper_Slam,
        Flame_Fang,
        Needle_Sting,
        Poison_Ivy,
        Sick_Sock,
        Psy_Strike,
        Mind_Slash,
        Star_Gaze,
        Flailing_Arms,
        Bottle_Sip, // recovers stamina + increases drinking counter
        Dynamite_Arm, // riggs the last opponent move
        Astral_Tango,
        // Special moves
        Flinch,
        Iron_Fist,
        Rhino_Rush,
        Mega_Chop,
        Fire_Breath,
        Tart_Throw,
        Tart_Splash,
        Ritual,
        Bizarre_Ball,
        Pain_Release,
        Anti_Counter,
        Chomp,
        Joker,
        Burning_Tart,
        Ultra_Punch,
        Vomit,
        Drunken_Fist, // requires drunk status 
        Gorilla_Cannon, // charges whenever user does not attack
    }

    public enum BattleStance
    {
        Main = 0,
        Alternative = 1,
        Bizarre = 2,
        Clown = 3,
        Zombie = 4
    }

    public enum BattleMode
    {
        None = 0,
        Pratice = 1,
        Unranked = 2,
        Ranked = 3,
        Versus = 4,
        Academy = 5
    }

    public enum BattleState
    {
        Active = 0,
        WinA = 1,
        WinB = 2,
        ForfeitA = 3,
        ForfeitB = 4,
        Draw = 5,
        Cancelled = 6
    }

    [Flags]
    public enum BattleStatus
    {
        None = 0,
        Bleeding = 1,
        Poisoned = 2,
        Taunted = 4, // can only use attack moves
        Confused = 8,
        Cursed = 16, // cant use same move twice in a row
        Diseased = 32, // zombie flag
        Flinched = 64, // cant attack next turn
        Smiling = 128, // 
        Burned = 256, // 
        Bright = 512, // double damage, positive brother of Flinch
        Drunk = 1024,
    }

    [Flags]
    public enum AccountFlags
    {
        None = 0,
        Admin = 1,
        Premium = 2,
        Banned = 4,
    }

    [Flags]
    public enum WrestlerFlags
    {
        None = 0,
        Shine = 1,
        Locked = 2,
        Wrapped = 4, // wrapped as gift
    }

    #endregion

    #region STRUCTS

    public struct LuchadorBattleState
    {
        public BigInteger wrestlerID;
        public BigInteger currentStamina;
        public BigInteger boostAtk;
        public BigInteger boostDef;
        public BattleStatus status;
        public ItemKind itemKind;
        public WrestlingMove learnedMove;
        public WrestlingMove lastMove;
        public WrestlingMove disabledMove;
        public WrestlingMove riggedMove;
        public BattleStance stance;
        public BattleStance lastStance;
    }

    public struct BattleSide
    {
        public LuchadorBattleState[] wrestlers;
        public Address address;
        public BigInteger current; // index of current wrestlers
        public BigInteger previousDirectDamage;
        public BigInteger previousIndirectDamage;
        public BigInteger previousRecover;
        public WrestlingMove move;
        public BigInteger turn;
        public bool auto; // if true, uses random move without requiring user action
    }

    public struct NachoBattle
    {
        public BattleSide[] sides;
        public BigInteger version;
        public BattleMode mode;
        public BigInteger bet;
        public BigInteger turn;
        public Hash lastTurnHash;
        public BattleState state;
        public Timestamp time;
        public BigInteger[] counters;
    }

    public struct NachoWrestler
    {
        public byte[] genes;
        public BigInteger currentMojo;
        public BigInteger maxMojo;
        public BigInteger experience;
        public BigInteger score;
        public string nickname;
        public BigInteger battleCount;
        public PraticeLevel praticeLevel;
        public Timestamp mojoTime;
        public Timestamp gymTime;
        public Timestamp perfumeTime;
        public WrestlerLocation location;
        public StatKind trainingStat;
        public byte gymBoostStamina;
        public byte gymBoostAtk;
        public byte gymBoostDef;
        public byte maskOverrideRarity;
        public byte maskOverrideID;
        public byte maskOverrideCheck;
        public BigInteger itemID;
        public Timestamp roomTime;
        public string[] comments;
        public byte[] moveOverrides;
        public WrestlerFlags flags;
        public BigInteger stakeAmount;
    }

    public struct DailyRewards
    {
        public BigInteger factionReward;
        public BigInteger championshipReward;
        public BigInteger vipWrestlerReward;
        public BigInteger vipItemReward;
        public BigInteger vipMakeUpReward;
    }

    //public struct NachoAuction
    //{
    //    public uint startTime;
    //    public uint endTime;
    //    public BigInteger contentID;
    //    public BigInteger startPrice;
    //    public BigInteger endPrice;
    //    public Address creator;
    //    public AuctionKind kind;
    //    public string comment;
    //    public AuctionCurrency currency;
    //}

    public struct NachoSale
    {
        public Timestamp time;
        public BigInteger auctionID;
        public BigInteger price;
        public Address buyer;
    }

    public struct NachoAccount
    {
        public Timestamp creationTime;
        public BigInteger battleID;
        public AccountFlags flags;
        public BigInteger[] counters;
        public string comment;
        public Address referral;
        public Timestamp lastTime;
        public TrophyFlag trophies;
        public BigInteger ELO;

        public BattleMode queueMode;
        public BigInteger queueBet;
        public Timestamp queueJoinTime;
        public Timestamp queueUpdateTime;
        public Address queueVersus;
        public PraticeLevel queueLevel;
        public BigInteger[] queueWrestlerIDs;
        public Address lastOpponent;

        public BigInteger vipPoints;
        public Faction faction;

        //public BigInteger avatarID;
    }

    public struct NachoReferral
    {
        public Address address;
        public Timestamp referalTime;
        public BigInteger bonusAmount;
        public Timestamp stakeTime;
        public BigInteger stakeAmount;
        public BigInteger bonusPercentage;
    }

    public struct NachoPotEntry
    {
        public Timestamp timestamp;
        public Address address;
        public BigInteger wins;
    }

    public struct NachoPot
    {
        public BigInteger currentBalance;
        public BigInteger lastBalance;
        public Address[] lastWinners;
        public Timestamp timestamp;
        public bool claimed;
        public NachoPotEntry[] entries;
    }

    public struct NachoItem
    {
        public BigInteger wrestlerID;
        public ItemKind kind;
        public ItemLocation location;
        public ItemFlags flags;
    }

    public struct NachoConfig
    {
        public Timestamp    time;
        public bool         suspendedTransfers;
    }

    public struct NachoVersusInfo
    {
        public Address challenger;
        public Timestamp time;
        public BigInteger bet;
        public byte[] levels;
    }

    public struct InAppData
    {
        public string name;
        public BigInteger contentID;
        public BigInteger price;
        public BigInteger nachos;
    }

    public struct Friend
    {
        public string name;
        public Address address;
        //public int avatarID;
        public BigInteger ELO;
    }

    #endregion
    
    public enum NachoEvent
    {
        Purchase = 0,
        ItemAdded = 1,
        ItemRemoved = 2,
        ItemSpent = 3,
        ItemActivated = 4,
        ItemUnwrapped = 5,
        Stance = 6,
        StatusAdded = 7,
        StatusRemoved = 8,
        Buff = 9,
        Debuff = 10,
        Experience = 11,
        Unlock = 12,
        Rename = 13,
        Auto = 14,
        PotPrize = 15,
        Referral = 16,
        Trophy = 17,
        Confusion = 18,
        MoveMiss = 19,
        SelectAvatar = 20,
        CollectFactionReward = 21,
        CollectChampionshipReward = 22,
        CollectVipWrestlerReward = 23,
        CollectVipItemReward = 24,
        CollectVipMakeUpReward = 25,
        PlayerJoinFaction = 26,
        MysteryStake = 27,
        MysteryUnstake = 28,
    }

    public struct WrestlerTurnInfo
    {
        public Address address;
        public WrestlingMove move;
        public WrestlingMove lastMove;
        public BigInteger currentAtk;
        public BigInteger currentDef;
        public BigInteger currentStamina;
        public BigInteger initialAtk;
        public BigInteger initialDef;
        public BigInteger maxStamina;
        public BigInteger level;
        public BigInteger seed;
        public BigInteger power;
        public ItemKind item;
        public BattleStatus status;
        public BattleStance stance;
        public BattleStance lastStance;
        public BigInteger lastDamage;
        public BigInteger chance;
        public bool itemActivated;
    }

    public class NachoContract : SmartContract
    {
        public override string Name => "nacho";

        public static readonly int CurrentBattleVersion = 10;

        public static readonly string ACCOUNT_WRESTLERS = "team";
        public static readonly string ACCOUNT_ITEMS = "items";
        public static readonly string ACCOUNT_HISTORY = "history";
        //public static readonly string ACCOUNT_CHALLENGES = "versus"; //todo check

        public static readonly string ACTIVE_AUCTIONS_LIST = "active";
        public static readonly string GLOBAL_AUCTIONS_LIST = "auctions";
        public static readonly string GLOBAL_SALES_LIST = "sales";
        public static readonly string GLOBAL_BATTLE_LIST = "battles";
        //  public static readonly string GLOBAL_TOURNEY_LIST = "tourneys";
        //public static readonly string GLOBAL_WRESTLERS_LIST = "wrestlers";
        public static readonly string GLOBAL_ITEM_LIST = "objs";
        //public static readonly string GLOBAL_MATCHMAKER_LIST = "matcher";

        public static readonly string QUEUE_MAP = "queues";
        public static readonly string NAME_MAP = "names";
        public static readonly string ADDRESS_MAP = "addresses";
        //public static readonly string TOKEN_BALANCE_MAP = "tokens";
        public static readonly string ACCOUNT_MAP = "accounts";
        public static readonly string ITEM_MAP = "items";
        public static readonly string PURCHASE_MAP = "purchases";
        public static readonly string NEO_TO_PHANTASMA_MAP = "neo2pha";
        public static readonly string PHANTASMA_TO_NEO_MAP = "pha2neo";

        //public static readonly string ROOM_COUNTER_KEY = "_roomctr_";
        //public static readonly string ROOM_SEQUENCE_KEY = "_roomseq_";
        public static readonly string MOTD_KEY = "_MOTD_";

        public bool SuspendTransfers = false;

        public Action<string> systemEvent = null;
        public Action<Address, string> singleEvent = null;
        public Action<Address, Address, string> pairEvent = null;

        internal StorageList _globalMatchmakerList;

        internal StorageMap _playerVersusChallengesList; // _accountWrestlers, _accountItems;
        internal StorageMap _battles; //, _wrestlers, _items;
        //internal StorageMap _globalVersusChallengesList;

        // temporary hack
        public Address DevelopersAddress => Runtime.Nexus.GenesisAddress;

        // storage
        private StorageMap _referrals;  // <Address, StorageList<NachoReferal>>
        private StorageMap _accounts; // <Address, NachoAccount>;
        private NachoPot _pot;

        internal BigInteger _roomCounter;
        internal BigInteger _roomSequence;

        public NachoContract() : base()
        {
        }

        public void BuyInApp()
        {
            // TODO
        }

        #region ACCOUNT API

        public void SetPlayerFaction(Address address, Faction faction)
        {
            // TODO finish implementation

            Runtime.Expect(faction != Faction.None, "Faction not valid");

            var account = GetAccount(address);

            Runtime.Expect(account.faction != faction, "Player is already in this faction");

            if (account.faction == Faction.None)
            {
                account.faction = faction;

                Runtime.Notify(NachoEvent.PlayerJoinFaction, address, faction);
            }
            else
            {
                // charge change faction cost

                var changeFactionCost = Constants.CHANGE_FACTION_COST;

                // todo

                Runtime.Notify(NachoEvent.PlayerJoinFaction, address, faction);
            }
        }

        public void OnSendWrestlerTrigger(BigInteger tokenID, Address sender)
        {
            TransferWrestler(tokenID);
        }

        public void TransferWrestler(/*Address from, Address to, */BigInteger wrestlerID)
        {
            //Runtime.Expect(IsWitness(from), "invalid witness");
            //Runtime.Expect(to != from, "same address");

            //var wrestlers = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_WRESTLERS, from);
            //var wrestlers = _accountWrestlers.Get<Address, StorageList>(from);
            //Runtime.Expect(wrestlers.Contains(wrestlerID), "wrestler invalid");

            var wrestler = GetWrestler(wrestlerID);

            Runtime.Expect(!wrestler.flags.HasFlag(WrestlerFlags.Locked), "locked wrestler");
            Runtime.Expect(wrestler.location == WrestlerLocation.None, "location invalid");
            Runtime.Expect(wrestler.itemID == 0, "can't have equipped item");

            // change owner
            //wrestler.owner = to;
            //SetWrestler(wrestlerID, wrestler);

            // remove it from old team
            //wrestlers.Remove(wrestlerID);

            // add to new team
            //var otherWrestlers = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_WRESTLERS, to);
            //var otherWrestlers = _accountWrestlers.Get<Address, StorageList>(to);
            //otherWrestlers.Add(wrestlerID);

            //Runtime.Notify(EventKind.TokenSend, from, wrestlerID);
            //Runtime.Notify(EventKind.TokenReceive, to, wrestlerID);
        }

        public void OnSendItemTrigger(BigInteger tokenID, Address sender)
        {
            TransferItem(tokenID);
        }
        
        public void TransferItem(/*Address from, Address to, */BigInteger itemID)
        {
            //Runtime.Expect(IsWitness(from), "invalid witness");

            //Runtime.Expect(to != from, "same address");

            //var playerItems = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_ITEMS, from);
            //var playerItems = _accountItems.Get<Address, StorageList>(from);
            //Runtime.Expect(playerItems.Contains(itemID), "item invalid");

            var item = GetItem(itemID);

            //if (item.owner == Address.Null)
            //{
            //    item.owner = from;
            //}

            Runtime.Expect(!item.flags.HasFlag(ItemFlags.Locked), "locked item");
            //Runtime.Expect(item.owner == from, "invalid owner");

            //item.owner = to;
            //SetItem(itemID, item);

            // remove it from old team
            //playerItems.Remove(itemID);

            // add to new team
            //var otherItems = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_ITEMS, to);
            //var otherItems = _accountItems.Get<Address, StorageList>(to);
            //otherItems.Add(itemID);

            //Runtime.Notify(EventKind.TokenSend, from, itemID);
            //Runtime.Notify(EventKind.TokenReceive, to, itemID);
        }

        private bool SpendFromAccountBalance<T>(Address address, BigInteger amount)
        {
            return SpendFromAccountBalance<byte[]>(address, amount, null);
        }

        private bool SpendFromAccountBalance<T>(Address address, BigInteger amount, T reference)
        {
            if (amount == 0)
            {
                return true;
            }

            var tokenSymbol = Nexus.StakingTokenSymbol;
            return Runtime.Nexus.TransferTokens(Runtime, tokenSymbol, address, DevelopersAddress, amount);
        }

        public NachoAccount GetAccount(Address address)
        {
            NachoAccount account;

            if (_accounts.ContainsKey(address))
            {
                account = _accounts.Get<Address, NachoAccount>(address);

                if (account.battleID != 0)
                {
                    var battle = GetBattle(account.battleID);
                    if (battle.state != BattleState.Active)
                    {
                        account.battleID = 0;
                    }
                }

                if (account.comment == null)
                {
                    account.comment = "";
                }

                if (account.ELO == 0)
                {
                    account.ELO = Constants.DEFAULT_ELO;
                }
            }
            else
            {
                account = new NachoAccount()
                {
                    battleID = 0,
                    queueBet = 0,
                    queueWrestlerIDs = new BigInteger[0],
                    creationTime = 0,
                    flags = AccountFlags.None,
                    counters = new BigInteger[Constants.ACCOUNT_COUNTER_MAX],
                    comment = "",
                    referral = Address.Null,
                    ELO = Constants.DEFAULT_ELO, // TODO o elo assim nunca é actualizado
                    //avatarID = 0  // TODO Avatar no inicio, antes do jogador mudar de avatar,pode ficar com o 0 mas dps tem de devolver o
                };

                //SetAccount(address, account);
                //_accounts.Set<Address, NachoAccount>(address, account);
            }

            return account;
        }

        private void SetAccount(Address address, NachoAccount account)
        {
            if (address == DevelopersAddress)
            {
                account.flags |= AccountFlags.Admin;
            }

            if (account.referral == Address.Null)
            {
                account.referral = Address.Null;
            }

            if (account.creationTime == 0)
            {
                account.creationTime = GetCurrentTime();
            }

            // this allows us to add new counters later while keeping binary compatibility
            if (account.counters == null)
            {
                account.counters = new BigInteger[Constants.ACCOUNT_COUNTER_MAX];
            }
            else
            if (account.counters.Length < Constants.ACCOUNT_COUNTER_MAX)
            {
                var temp = new BigInteger[Constants.ACCOUNT_COUNTER_MAX];
                for (int i = 0; i < account.counters.Length; i++)
                {
                    temp[i] = account.counters[i];
                }
                account.counters = temp;
            }

            _accounts.Set<Address, NachoAccount>(address, account);
        }

        public void InitAccount(Address address)
        {
            var account         = GetAccount(address);
            account.lastTime    = Runtime.Time;

            SetAccount(address, account);
        }

        public NachoReferral[] GetAccountReferrals(Address address)
        {
            var referrals = _referrals.Get<Address, StorageList>(address);
            return referrals.All<NachoReferral>();
        }

    public void RegisterReferral(Address from, Address target)
    {
        Runtime.Expect(IsWitness(from), "witness failed");

        var fromAccount = GetAccount(from);
        Runtime.Expect(fromAccount.creationTime > 0, "invalid account");
        Runtime.Expect(fromAccount.referral == Address.Null, "already has referal");

        var targetAccount = GetAccount(target);

        Runtime.Expect(targetAccount.creationTime > 0, "invalid account");

        Runtime.Expect(fromAccount.creationTime > targetAccount.creationTime, "newer account failed");

        var referrals = _referrals.Get<Address, StorageList>(target);

        var referral = new NachoReferral();
        int referralIndex = -1;
        var count = referrals.Count(); 
        for (int i=0; i<count; i++) // no breaks here, we need to check every referal to make sure we're not registering the same guy twice
        { 
            referral = referrals.Get<NachoReferral>(i);
            Runtime.Expect(referral.address != from, "already referral");

            if (referral.address == Address.Null && referral.stakeAmount> 0)
            {
                referralIndex = i;
            }
        }

        Runtime.Expect(referralIndex >= 0, "no refreral slots available");

        referral.address = from;
        referral.referalTime = Runtime.Time.Value;
        referrals.Replace(referralIndex, referral);

        fromAccount.referral = target;
        SetAccount(from, fromAccount);

        var rnd = new Random();

        /* TODO fix 
        byte[] genes = Luchador.MineGenes(rnd, (x) =>
        {
            if (x.Rarity != Rarity.Common)
            {
                return false;
            }

            return WrestlerValidation.IsValidWrestler(x);
        });

        // TODO wrapped false ou true?
        var wrestlerID = CreateWrestler(from, genes, 0, false);

        var wrestler = GetWrestler(wrestlerID);
        wrestler.flags |= WrestlerFlags.Locked;
        SetWrestler(wrestlerID, wrestler);

        //Runtime.Notify(EventKind.Referal, from, target); // todo fix
        */
    }

        private int FindReferalIndex(Address from, StorageList referals)
        {
            var count = referals.Count();
            for (int i = 0; i < count; i++)
            {
                var referal = referals.Get<NachoReferral>(i);
                if (referal.address == from)
                {
                    return i;
                }
            }

            return -1;
        }

        private BigInteger RegisterReferalPurchase(Address from, BigInteger totalAmount, BigInteger auctionID)
        {
            var fromAccount = GetAccount(from);
            if (fromAccount.referral == Address.Null)
            {
                return 0;
            }

            var target = fromAccount.referral;

            var referals = _referrals.Get<Address, StorageList>(target);
            var referalIndex = FindReferalIndex(from, referals);
            Runtime.Expect(referalIndex >= 0, "invalid referal");

            var referal = referals.Get<NachoReferral>(referalIndex);
            if (referal.stakeAmount <= 0)
            {
                return 0;
            }

            var bonusAmount = (totalAmount * referal.bonusPercentage) / 100;
            Runtime.Expect(bonusAmount < totalAmount, "invalid referal percent");
            if (bonusAmount <= 0)
            {
                return 0;
            }

            referal.bonusAmount += bonusAmount;
            referals.Replace(referalIndex, referal);

            return bonusAmount;
        }

        public void StakeReferral(Address from, BigInteger referralIndex)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");
            Runtime.Expect(referralIndex >= 0, "invalid referral");

            var referrals = _referrals.Get<Address, StorageList>(from);
            var count = referrals.Count();

            NachoReferral referral;

            if (referralIndex == count) // new slot
            {
                referral = new NachoReferral()
                {
                    address = Address.Null,
                    stakeAmount = 0,
                    stakeTime = 0,
                };
            }
            else
            {
                Runtime.Expect(referralIndex < count, "invalid referral");
                referral = referrals.Get<NachoReferral>(referralIndex);
                Runtime.Expect(referral.stakeAmount == 0, "already staked");
            }

            var stakeAmount = UnitConversion.ToBigInteger(Constants.REFERRAL_STAKE_AMOUNT, Nexus.StakingTokenDecimals);

            // update account balance
            if (!SpendFromAccountBalance(from, stakeAmount, referralIndex))
            {
                throw new ContractException("balance failed");
            }

            int currentReferralPercent;
            // TODO update this values
            if (Runtime.Time.Value <= 1541030400) // 1 nov
            {
                currentReferralPercent = 40;
            }
            else
            if (Runtime.Time.Value <= 1541635200) //  8 nov
            {
                currentReferralPercent = 30;
            }
            else
            if (Runtime.Time.Value <= 1542240000) // 15 nov
            {
                currentReferralPercent = 25;
            }
            else
            if (Runtime.Time.Value <= 1542844800) //  22 nov
            {
                currentReferralPercent = 20;
            }
            else
            if (Runtime.Time.Value <= 1543449600) // 29 nov
            {
                currentReferralPercent = 15;
            }
            else
            if (Runtime.Time.Value <= 1544054400) // 6 dec
            {
                currentReferralPercent = 10;
            }
            else
            {
                currentReferralPercent = 5;
            }

            referral.stakeTime = Runtime.Time.Value;
            referral.stakeAmount = stakeAmount;
            referral.bonusPercentage = currentReferralPercent;

            if (referralIndex == count)
            {
                referrals.Add(referral);
            }
            else
            {
                referrals.Replace(referralIndex, referral);
            }

            Runtime.Notify(EventKind.TokenStake, from, referralIndex);
        }

        public void UnstakeReferral(Address from, BigInteger referralIndex)
        {
            Runtime.Expect(IsWitness(from), "invalid witness");

            var referrals = _referrals.Get<Address, StorageList>(from);
            var count = referrals.Count();
            Runtime.Expect(referralIndex >= 0, "invalid referral");
            Runtime.Expect(referralIndex < count, "invalid referral");

            var referral = referrals.Get<NachoReferral>(referralIndex);
            var outputAmount = referral.stakeAmount + referral.bonusAmount;

            Runtime.Expect(outputAmount > 0, "already unstaked");

            if (referral.stakeAmount > 0)
            {
                var diff = Runtime.Time.Value - referral.stakeTime;
                //diff = diff / Constants.SECONDS_PER_DAY; // convert to days // TODO fix
                Runtime.Expect(diff >= Constants.REFERRAL_MINIMUM_DAYS, "too soon");
            }
            
            //Runtime.Expect(UpdateAccountBalance(from, outputAmount), "deposit failed");
            Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Constants.SOUL_SYMBOL, Runtime.Chain.Address, from, outputAmount), "deposit failed");

            referral.stakeAmount = 0;
            referral.bonusAmount = 0;
            referrals.Replace(referralIndex, referral);

            AddTrophy(from, TrophyFlag.Referral);

            Runtime.Notify(EventKind.TokenUnstake, from, outputAmount);
        }

        /*
    public void DeleteWrestler(Address from, BigInteger wrestlerID)
    {
        Runtime.Expect(IsWitness(DevelopersAddress), "dev only");

        var wrestlers = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_WRESTLERS, from);
        Runtime.Expect(wrestlers.Contains(wrestlerID), "not found");

        var wrestler = GetWrestler(wrestlerID);
        Runtime.Expect(wrestler.location != WrestlerLocation.Market, "in auction");

        wrestler.location = WrestlerLocation.None;
        wrestler.owner = Address.Null;

        wrestlers.Remove(wrestlerID);
    }

        // get how many wrestlers in an account
        //public BigInteger[] GetAccountWrestlers(Address address)
        //{
        //    Runtime.Expect(Runtime.Nexus.TokenExists(Constants.WRESTLER_SYMBOL), Constants.WRESTLER_SYMBOL + " token not found");

        //    var ownerships = new OwnershipSheet(Constants.WRESTLER_SYMBOL);
        //    var ownerIDs = ownerships.Get(this.Storage, address);
        //    return ownerIDs.ToArray();
        //}

        //public BigInteger[] GetAccountItems(Address address)
        //{
        //    Runtime.Expect(Runtime.Nexus.TokenExists(Constants.ITEM_SYMBOL), Constants.ITEM_SYMBOL + " token not found");

        //    var ownerships = new OwnershipSheet(Constants.ITEM_SYMBOL);

        //    var ownerIDs = ownerships.Get(this.Storage, address);
        //    return ownerIDs.ToArray();
        //    /*
        //    var items = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_ITEMS, address);
        //    var result = items.All();

        //    var wrestlers = GetAccountWrestlers(address);
        //    var auctions = GetActiveAuctions();

        //    for (int i = 0; i < result.Length; i++)
        //    {
        //        var itemID = result[i];
        //        var item = GetItem(itemID);

        //        bool changed = false;

        //        if (item.owner == Address.Null)
        //        {
        //            item.owner = address;
        //            changed = true;
        //        }

        //        if (item.location == ItemLocation.Unknown)
        //        {
        //            foreach (var ID in wrestlers)
        //            {
        //                var wrestler = GetWrestler(ID);
        //                if (wrestler.itemID == itemID)
        //                {
        //                    item.location = ItemLocation.Wrestler;
        //                    item.locationID = ID;
        //                    changed = true;
        //                    break;
        //                }
        //            }
        //        }

        //        if (item.location == ItemLocation.Unknown)
        //        {
        //            foreach (var ID in auctions)
        //            {
        //                var auction = GetAuction(ID);
        //                if (auction.kind == AuctionKind.Equipment && auction.creator == address && auction.contentID == itemID)
        //                {
        //                    item.location = ItemLocation.Market;
        //                    item.locationID = ID;
        //                    changed = true;
        //                    break;
        //                }
        //            }
        //        }

        //        if (item.location == ItemLocation.Unknown)
        //        {
        //            item.location = ItemLocation.None;
        //            item.locationID = 0;
        //            changed = true;
        //        }

        //        if (changed)
        //        {
        //            SetItem(itemID, item);
        //        }
        //    }

        //    return result;*/
        //}

        #endregion

        #region ITEM API
        //public NachoItem[] GetItems(BigInteger[] IDs)
        //{
        //    var items = new NachoItem[IDs.Length];
        //    for (int i = 0; i < items.Length; i++)
        //    {
        //        items[i] = GetItem(IDs[i]);
        //    }
        //    return items;
        //}

        // TODO error handling when item not exist
        public NachoItem GetItem(BigInteger ID)
        {
            var nft = Runtime.Nexus.GetNFT(Constants.ITEM_SYMBOL, ID);

            var item = Serialization.Unserialize<NachoItem>(nft.RAM);

            if (item.location == ItemLocation.Wrestler)
            {
                if (item.wrestlerID != 0) // TODO confirmar se o operador != dos BigInteger já foi corrigido. Alternativa => ID > 0
                {
                    var wrestler = GetWrestler(item.wrestlerID);
                    if (wrestler.itemID != ID)
                    {
                        item.location = ItemLocation.None;
                    }
                }
            }

            return item;
        }

        public void SetItem(BigInteger ID, NachoItem item)
        {
            var bytes = Serialization.Serialize(item);
            Runtime.Nexus.EditNFTContent(Constants.ITEM_SYMBOL, ID, bytes);
        }

        public bool HasItem(Address address, BigInteger itemID)
        {
            var ownerships = new OwnershipSheet(Constants.ITEM_SYMBOL);
            return ownerships.GetOwner(this.Storage, itemID) == address;
        }

        public void DeleteItem(Address from, BigInteger itemID)
        {
            Runtime.Expect(IsWitness(DevelopersAddress), "dev only");
            
            Runtime.Expect(HasItem(from, itemID), "invalid owner");

            var item = GetItem(itemID);
            Runtime.Expect(item.location != ItemLocation.Market, "in auction");

            item.location = ItemLocation.None;
            //item.owner = Address.Null;

            var ownerships = new OwnershipSheet(Constants.ITEM_SYMBOL);
            ownerships.Remove(this.Storage, from, itemID);
            //token.Burn(balances, from,)
            Runtime.Expect(Runtime.Nexus.BurnToken(Runtime, Constants.ITEM_SYMBOL, from, itemID), "burn failed");

            Runtime.Notify(EventKind.TokenBurn, from, itemID);
        }

        public void UnwrapItem(Address from, BigInteger itemID)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(HasItem(from, itemID), "invalid item");

            var item = GetItem(itemID);

            //if (item.owner == Address.Null)
            //{
            //    item.owner = from;
            //}

            Runtime.Expect(item.location == ItemLocation.None, "invalid location");

            Runtime.Expect(item.flags.HasFlag(ItemFlags.Wrapped), "unwrapped item");
            item.flags ^= ItemFlags.Wrapped;

            var nft = Runtime.Nexus.GetNFT(Constants.ITEM_SYMBOL, itemID);
            Runtime.Expect(nft.CurrentOwner == from, "invalid owner");
            
            SetItem(itemID, item);
            Runtime.Notify(NachoEvent.ItemUnwrapped, from, itemID);
        }


        public void UseItem(Address from, BigInteger wrestlerID, BigInteger itemID, ItemKind itemKind)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(from != DevelopersAddress, "no items for developers");

            //var itemKind = Formulas.GetItemKind(itemID);
            var category = Rules.GetItemCategory(itemKind);
            Runtime.Expect(category == ItemCategory.Consumable, "not consumable");

            Runtime.Expect(HasWrestler(from, wrestlerID), "invalid wrestler");
            Runtime.Expect(HasItem(from, itemID), "invalid item");

            var wrestler = GetWrestler(wrestlerID);

            var item = GetItem(itemID);

            //if (item.owner == Address.Null)
            //{
            //    item.owner = from;
            //}

            Runtime.Expect(item.location == ItemLocation.None, "invalid location");
            Runtime.Expect(!item.flags.HasFlag(ItemFlags.Wrapped), "wrapped item");

            var nft = Runtime.Nexus.GetNFT(Constants.ITEM_SYMBOL, itemID);
            Runtime.Expect(nft.CurrentOwner == from, "invalid owner");

            switch (itemKind)
            {
                case ItemKind.Dev_Badge:
                    {
                        wrestler.gymBoostAtk = Constants.MAX_GYM_BOOST - 1;
                        wrestler.gymBoostStamina = Constants.MAX_GYM_BOOST - 1;
                        wrestler.gymBoostDef = 0;
                        break;
                    }

                case ItemKind.Chilli_Bottle:
                    {
                        Runtime.Expect(wrestler.currentMojo < wrestler.maxMojo, "mojo full");
                        wrestler.currentMojo = wrestler.maxMojo;
                        break;
                    }

                case ItemKind.Love_Lotion:
                    {
                        Runtime.Expect(wrestler.maxMojo < Constants.MAX_MOJO, "mojo max");
                        wrestler.maxMojo++;
                        wrestler.currentMojo = wrestler.maxMojo;
                        break;
                    }

                case ItemKind.XP_Perfume:
                    {
                        var curTime = GetCurrentTime();
                        var diff = curTime - wrestler.perfumeTime;
                        diff /= 3600;
                        Runtime.Expect(diff > Constants.XP_PERFUME_DURATION_IN_HOURS, "already perfumed");

                        wrestler.perfumeTime = curTime;
                        break;
                    }

                case ItemKind.Rare_Taco:
                    {
                        var level = Formulas.CalculateWrestlerLevel((int)wrestler.experience);
                        Runtime.Expect(level < Constants.MAX_LEVEL, "reached maximum level");

                        wrestler.experience = Constants.EXPERIENCE_MAP[level + 1];
                        break;
                    }

                case ItemKind.Stamina_Pill:
                    {
                        var new_boost = wrestler.gymBoostStamina + Constants.PILL_GYM_BOOST;
                        Runtime.Expect(new_boost <= Constants.MAX_GYM_BOOST, "reached maximum boost");
                        wrestler.gymBoostStamina = (byte)new_boost;
                        break;
                    }

                case ItemKind.Attack_Pill:
                    {
                        var new_boost = wrestler.gymBoostAtk + Constants.PILL_GYM_BOOST;
                        Runtime.Expect(new_boost <= Constants.MAX_GYM_BOOST, "reached maximum boost");
                        wrestler.gymBoostAtk = (byte)new_boost;
                        break;
                    }

                case ItemKind.Defense_Pill:
                    {
                        var new_boost = wrestler.gymBoostDef + Constants.PILL_GYM_BOOST;
                        Runtime.Expect(new_boost <= Constants.MAX_GYM_BOOST, "reached maximum boost");
                        wrestler.gymBoostDef = (byte)new_boost;
                        break;
                    }

                default:
                    {
                        Runtime.Expect(false, "not usable");
                        break;
                    }
            }

            SetWrestler(wrestlerID, wrestler);

            Runtime.Notify(NachoEvent.ItemSpent, from, itemID);

            // TransferItem(from, DevelopersAddress, itemID); TODO LATER
        }
        
        /* TODO LATER*/
        //public BigInteger GenerateItem(Address to, BigInteger itemID, ItemKind itemKind, bool wrapped)
        //{
        //    Runtime.Expect(IsWitness(DevelopersAddress), "witness failed");
        //    return CreateItem(to, itemID, itemKind, wrapped);
        //}

        private BigInteger CreateItem(Address to, ItemKind itemKind, bool wrapped)
        {
            var itemToken = Runtime.Nexus.GetTokenInfo(Constants.ITEM_SYMBOL);
            Runtime.Expect(Runtime.Nexus.TokenExists(Constants.ITEM_SYMBOL), "Can't find the token symbol");

            var item = new NachoItem()
            {
                wrestlerID = BigInteger.Zero,
                kind        = itemKind,
                flags       = ItemFlags.None,
                location    = ItemLocation.None,
            };

            if (wrapped)
            {
                item.flags |= ItemFlags.Wrapped;
            }
          
            var itemBytes = item.Serialize();

            var tokenROM = new byte[0]; //itemBytes;
            var tokenRAM = itemBytes;   //new byte[0];

            var tokenID = this.Runtime.Nexus.CreateNFT(Constants.ITEM_SYMBOL, Runtime.Chain.Address, tokenROM, tokenRAM, 0);
            Runtime.Expect(tokenID > 0, "invalid tokenID");

            //var temp = Storage.FindMapForContract<BigInteger, bool>(ITEM_MAP);
            //Runtime.Expect(!temp.ContainsKey(itemID), "duplicated ID");
            //var hasItem = _items.Get<BigInteger, bool>(tokenID);
            //Runtime.Expect(!hasItem, "duplicated ID");

            //temp.Set(itemID, true);
            //_items.Set(tokenID, true);

            //var player_items = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_ITEMS, to);
            //var playerItems = _accountItems.Get<Address, StorageList>(to);
            var ownership   = new OwnershipSheet(Constants.ITEM_SYMBOL);
            var playerItems = ownership.Get(this.Storage, to);
            //playerItems.Add(tokenID);
            ownership.Add(this.Storage, to, tokenID);
            
            Runtime.Expect(Runtime.Nexus.MintToken(Runtime, Constants.ITEM_SYMBOL, to, tokenID), "minting failed");
            //Runtime.Notify(EventKind.ItemReceived, to, itemID);
            Runtime.Notify(EventKind.TokenReceive, to, new TokenEventData() { chainAddress = Runtime.Chain.Address, value = tokenID, symbol = Constants.ITEM_SYMBOL });

            return tokenID;
        }

        /*
        private BigInteger CreateItem(Address to, BigInteger itemID, bool wrapped)
        {
            var itemToken = Runtime.Nexus.GetTokenInfo(Constants.ITEM_SYMBOL);
            Runtime.Expect(Runtime.Nexus.TokenExists(Constants.ITEM_SYMBOL), "Can't find the token symbol");

            //var temp = Storage.FindMapForContract<BigInteger, bool>(ITEM_MAP);
            //Runtime.Expect(!temp.ContainsKey(itemID), "duplicated ID");
            var hasItem = _globalItemList.Get<BigInteger, bool>(itemID);
            Runtime.Expect(!hasItem, "duplicated ID");

            //temp.Set(itemID, true);
            _globalItemList.Set(itemID, true);

            //var player_items = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_ITEMS, to);
            var player_items = _playerItemsList.Get<Address, StorageList>(to);
            player_items.Add(itemID);

            var item = new NachoItem()
            {
                //owner = to,
                //locationID = 0,
                wrestlerID = BigInteger.Zero,
                //kind = ,// TODO
                flags = ItemFlags.None,
                location = ItemLocation.None,
            };

            if (wrapped)
            {
                item.flags |= ItemFlags.Wrapped;
            }

            SetItem(itemID, item);

            Runtime.Notify(EventKind.ItemReceived, to, itemID);

            return itemID;
        }
        */

        private ItemKind GetRandomItemKind(Rarity rarity)
        {
            var items = Constants.ITEMS_RARITY[rarity];

            var rnd = new Random();

            return items[rnd.Next(0, items.Count)];
        }

        private BigInteger MineItemRarity(Rarity rarity, ref BigInteger lastID)
        {
            return MineItem((x) => (Rules.GetItemRarity(x) == rarity), ref lastID);
        }

        private BigInteger MineItem(Func<ItemKind, bool> filter, ref BigInteger lastID)
        {
            //var rnd = new Random();
            do
            {
                lastID++;

                var item = new NachoItem(); // TODO Equipment.FromID(lastID);

                if (item.kind.ToString() != ((int)item.kind).ToString())
                {
                    if (filter(item.kind))
                    {
                        return lastID;
                    }
                }


            } while (true);
        }


        #endregion

        #region AVATAR API
        // TODO when player sells an equipped avatar, the avatar must revert back to default avatar!
        //public void UseAvatar(Address from, BigInteger itemID)
        //{
        //    var kind = Formulas.GetItemKind(itemID);
        //    var category = Rules.GetItemCategory(kind);
        //    Runtime.Expect(category == ItemCategory.Avatar, "not avatar");

        //    Runtime.Expect(HasItem(from, itemID), "invalid item");

        //    var item = GetItem(itemID);

        //    if (item.owner == Address.Null)
        //    {
        //        item.owner = from;
        //    }

        //    Runtime.Expect(item.location == ItemLocation.None, "invalid location");
        //    Runtime.Expect(item.owner == from, "invalid owner");
        //    Runtime.Expect(!item.flags.HasFlag(ItemFlags.Wrapped), "wrapped item");

        //    var account = GetAccount(from);
        //    account.avatarID = new BigInteger(Formulas.GetAvatarID(itemID));
        //    SetAccount(from, account);

        //    Runtime.Notify(EventKind.SelectAvatar, from, account.avatarID);
        //}

        // todo confirmar se pode ficar com bigint. Com int dava erro a converter 
        //public void UseDefaultAvatar(Address from, BigInteger avatarID)
        //{
        //    Runtime.Expect(avatarID <= Constants.DEFAULT_AVATARS, "invalid avatar ID");

        //    var account = GetAccount(from);
        //    account.avatarID = avatarID;
        //    SetAccount(from, account);

        //    Runtime.Notify(EventKind.SelectAvatar, from, avatarID);
        //}
        #endregion

        #region WRESTLER API
        // TODO UnwrapWrestler

        public bool HasWrestler(Address address, BigInteger wrestlerID)
        {
            var ownerships = new OwnershipSheet(Constants.WRESTLER_SYMBOL);
            return ownerships.GetOwner(this.Storage, wrestlerID) == address;
        }

        /* TODO LATER
        public BigInteger GenerateWrestler(Address to, byte[] genes, uint experience, bool wrapped)
        {
            Runtime.Expect(IsWitness(DevelopersAddress), "witness failed");
            return CreateWrestler(to, genes, experience, wrapped);
        }

        private BigInteger CreateWrestler(Address to, byte[] genes, uint experience, bool wrapped)
        {
            var wrestlers = Storage.FindMapForContract<BigInteger, NachoWrestler>(GLOBAL_WRESTLERS_LIST);

            BigInteger wrestlerID = wrestlers.Count() + Constants.BASE_LUCHADOR_ID;

            var wrestler = new NachoWrestler()
            {
                owner = to,
                genes = genes,
                itemID = 0,
                nickname = "",
                score = Constants.DEFAULT_SCORE,
                maxMojo = Constants.DEFAULT_MOJO,
                currentMojo = Constants.DEFAULT_MOJO,
                experience = experience,
                mojoTime = GetCurrentTime(),
            };

            if (wrapped)
            {
                wrestler.flags |= WrestlerFlags.Wrapped;
            }

            // save wrestler
            wrestlers.Set(wrestlerID, wrestler);

            var team = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_WRESTLERS, to);
            team.Add(wrestlerID);

            Runtime.Notify(to, NachoEvent.WrestlerReceived, wrestlerID);
            return wrestlerID;
        }*/

        public void RenameWrestler(Address from, BigInteger wrestlerID, string name)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            /* TODO LATER
            var nameResult = NameGenerator.ValidateName(name);
            Runtime.Expect(nameResult == EWRESTLER_NAME_ERRORS.NONE, nameResult.ToString().ToLower().Replace("_", " "));
            */

            Runtime.Expect(HasWrestler(from, wrestlerID), "wrestler invalid");

            var wrestler = GetWrestler(wrestlerID);
            Runtime.Expect(wrestler.location == WrestlerLocation.None, "location invalid");

            wrestler.nickname = name;
            SetWrestler(wrestlerID, wrestler);

            Runtime.Notify(NachoEvent.Rename, from, wrestlerID);
        }

        public void SetWrestlerComment(Address from, BigInteger wrestlerID, int commentIndex, string comment)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(comment != null, "null comment");
            Runtime.Expect(comment.Length < Constants.MAX_COMMENT_LENGTH, "invalid length");

            Runtime.Expect(commentIndex >= 0, "invalid index");
            Runtime.Expect(commentIndex < Constants.LUCHADOR_COMMENT_MAX, "invalid index");

            //var team = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_WRESTLERS, from);
            var ownership = new OwnershipSheet(Constants.WRESTLER_SYMBOL);
            var team = ownership.Get(this.Storage, from);
            Runtime.Expect(team.Contains(wrestlerID), "invalid wrestler");

            var wrestler = GetWrestler(wrestlerID);
            //Runtime.Expect(wrestler.location == WrestlerLocation.None, "location invalid");

            if (wrestler.comments == null)
            {
                wrestler.comments = new string[Constants.LUCHADOR_COMMENT_MAX];
            }
            else
            if (wrestler.comments.Length != Constants.LUCHADOR_COMMENT_MAX)
            {
                var temp = wrestler.comments;
                wrestler.comments = new string[Constants.LUCHADOR_COMMENT_MAX];
                for (int i = 0; i < temp.Length; i++)
                {
                    wrestler.comments[i] = temp[i];
                }
            }

            wrestler.comments[commentIndex] = comment;
            SetWrestler(wrestlerID, wrestler);

            //Runtime.Notify(from, NachoEvent.Rename);
            Runtime.Notify(NachoEvent.Rename, from, comment);
        }

        //public NachoWrestler[] GetWrestlers(BigInteger[] IDs)
        //{
        //    var wrestlers = new NachoWrestler[IDs.Length];
        //    for (int i = 0; i < wrestlers.Length; i++)
        //    {
        //        wrestlers[i] = GetWrestler(IDs[i]);
        //    }
        //    return wrestlers;
        //}

        /// <summary>
        /// Bot ids = [-100 ; -1]
        /// </summary>
        /// <param name="botID"></param>
        /// <returns></returns>
        public NachoWrestler GetBot(int botID)
        {
            byte[] genes;
            int level;
            BigInteger botItemID;
            string introText = "";

            var botLevel = (PraticeLevel)(botID);
            switch (botLevel)
            {
                case PraticeLevel.Wood - (int)PraticeLevel.Wood * 2: // PraticeLevel.Wood = -1
                    level = 1; botItemID = 0; genes = new byte[] { 120, 46, 40, 40, 131, 93, 80, 221, 68, 155, };
                    introText = "Beep boop... amigo, entrena conmigo!";
                    break;

                case PraticeLevel.Iron - (int)PraticeLevel.Iron * 2: // PraticeLevel.Iron = -2
                    level = 4; botItemID = 0; genes = new byte[] { 222, 50, 52, 48, 131, 88, 144, 8, 51, 104, };
                    introText = "I'm made from iron and because of that, I'm stronger than my wood brother!";
                    break;

                case PraticeLevel.Steel - (int)PraticeLevel.Steel * 2: // PraticeLevel.Steel = -3
                    level = 6; botItemID = 0; genes = new byte[] { 114, 50, 53, 59, 131, 123, 122, 223, 181, 184, };
                    introText = "Get ready.. because I'm faster and stronger than my iron brother!";
                    break;

                case PraticeLevel.Silver - (int)PraticeLevel.Silver * 2: // PraticeLevel.Silver = -4
                    level = 8; botItemID = 0; genes = new byte[] { 72, 59, 61, 64, 131, 115, 18, 108, 11, 195, };
                    introText = "Counters are for plebs!";
                    break;

                case PraticeLevel.Gold - (int)PraticeLevel.Gold * 2: // PraticeLevel.Gold = -5
                    level = 10; botItemID = 0; genes = new byte[] { 138, 66, 65, 61, 131, 51, 148, 143, 99, 55, };
                    introText = "Luchador... My congratulations for getting so far!";
                    break;

                case PraticeLevel.Ruby - (int)PraticeLevel.Ruby * 2: // PraticeLevel.Ruby = -6
                    level = 13; botItemID = 0; genes = new byte[] { 12, 65, 68, 65, 131, 110, 146, 11, 100, 111 };
                    introText = "Amigo... I'm too strong to fail!";
                    break;

                case PraticeLevel.Emerald - (int)PraticeLevel.Emerald * 2: // PraticeLevel.Emerald = -7
                    level = 16; botItemID = 329390; genes = new byte[] { 240, 76, 73, 79, 131, 68, 218, 145, 232, 20 };
                    introText = "Beep...Beep...My hobby is wasting time in asian basket weaving foruns...";
                    break;

                case PraticeLevel.Diamond - (int)PraticeLevel.Diamond * 2: // PraticeLevel.Diamond = -8
                    level = 20; botItemID = 35808; genes = new byte[] { 144, 76, 77, 76, 131, 46, 168, 202, 141, 188, };
                    introText = "Beep... boop... I am become Death, the destroyer of worlds!";
                    break;

                default:
                    switch (botID)
                    {
                        case -9: level = 1; botItemID = 0; genes = new byte[] { 169, 149, 19, 125, 210, 41, 238, 87, 66, 103, }; break;
                        case -10: level = 1; botItemID = 0; genes = new byte[] { 229, 67, 21, 113, 126, 40, 125, 193, 141, 185, }; break;
                        case -11: level = 1; botItemID = 0; introText = "you should give me your coins if you lose..."; genes = new byte[] { 157, 46, 74, 54, 216, 55, 81, 190, 42, 81, }; break;
                        case -12: level = 2; botItemID = 0; genes = new byte[] { 253, 187, 122, 153, 122, 254, 115, 83, 50, 56, }; break;
                        case -13: level = 2; botItemID = 0; introText = "To hold or no?"; genes = new byte[] { 139, 255, 58, 213, 143, 24, 97, 217, 108, 210, }; break;
                        case -14: level = 3; botItemID = 0; genes = new byte[] { 169, 249, 77, 77, 75, 64, 166, 137, 85, 165, }; break;
                        case -15: level = 3; botItemID = 96178; genes = new byte[] { 187, 61, 210, 174, 9, 149, 2, 180, 127, 46, }; break;
                        case -16: level = 3; botItemID = 0; introText = "I like potatoes with burgers"; genes = new byte[] { 145, 219, 94, 119, 72, 246, 162, 232, 47, 182, }; break;
                        case -17: level = 3; botItemID = 0; genes = new byte[] { 86, 57, 97, 203, 29, 225, 123, 174, 239, 104, }; break;
                        case -18: level = 4; botItemID = 0; genes = new byte[] { 139, 16, 224, 44, 177, 157, 131, 245, 82, 179, }; break;
                        case -19: level = 4; botItemID = 0; introText = "Im all in neo since antshares lol"; genes = new byte[] { 31, 235, 54, 221, 2, 248, 247, 165, 216, 148, }; break;
                        case -20: level = 4; botItemID = 0; genes = new byte[] { 68, 40, 37, 184, 149, 169, 67, 163, 104, 242, }; break;
                        case -21: level = 5; botItemID = 0; introText = "Derp derp derp.."; genes = new byte[] { 115, 24, 16, 61, 155, 239, 232, 59, 116, 109, }; break;
                        case -22: level = 5; botItemID = 0; genes = new byte[] { 73, 79, 227, 227, 138, 103, 98, 1, 255, 106, }; break;
                        case -23: level = 5; botItemID = 0; genes = new byte[] { 134, 103, 6, 7, 106, 172, 149, 135, 18, 36, }; break;
                        case -24: level = 6; botItemID = 30173; genes = new byte[] { 31, 85, 236, 135, 191, 87, 212, 70, 139, 202, }; break;
                        case -25: level = 6; botItemID = 0; introText = "Fugg you mann"; genes = new byte[] { 79, 171, 219, 185, 190, 234, 170, 161, 223, 103, }; break;
                        case -26: level = 6; botItemID = 0; genes = new byte[] { 32, 85, 113, 69, 127, 170, 193, 248, 233, 245, }; break;
                        case -27: level = 7; botItemID = 84882; introText = "Self proclaimed bitcoin maximalist"; genes = new byte[] { 115, 43, 166, 208, 198, 146, 2, 130, 231, 31, }; break;
                        case -28: level = 7; botItemID = 138905; genes = new byte[] { 169, 0, 145, 179, 144, 214, 165, 83, 22, 218, }; break;
                        case -29: level = 7; botItemID = 0; genes = new byte[] { 67, 33, 45, 42, 168, 35, 94, 3, 34, 237, }; break;
                        case -30: level = 7; botItemID = 32478; genes = new byte[] { 169, 172, 84, 63, 74, 69, 60, 65, 15, 20, }; break;
                        case -31: level = 8; botItemID = 0; introText = "SOUL goes 100x if I win"; genes = new byte[] { 235, 14, 247, 227, 158, 106, 178, 5, 25, 240, }; break;
                        case -32: level = 8; botItemID = 0; genes = new byte[] { 73, 204, 196, 177, 33, 2, 87, 242, 33, 219, }; break;
                        case -33: level = 9; botItemID = 329390; introText = "Bantasma fan number one!!"; genes = new byte[] { 25, 188, 160, 127, 57, 106, 143, 248, 79, 84, }; break;
                        case -34: level = 9; botItemID = 0; genes = new byte[] { 121, 215, 5, 48, 178, 2, 231, 109, 183, 226, }; break;
                        case -35: level = 9; botItemID = 63217; genes = new byte[] { 7, 156, 157, 29, 234, 28, 226, 214, 29, 191, }; break;
                        case -36: level = 10; botItemID = 0; introText = "How is babby formed?"; genes = new byte[] { 49, 251, 234, 105, 253, 80, 196, 238, 220, 153, }; break;
                        case -37: level = 10; botItemID = 0; genes = new byte[] { 229, 130, 158, 161, 191, 170, 82, 147, 21, 163, }; break;
                        case -38: level = 11; botItemID = 56842; introText = "Show bobs pls"; genes = new byte[] { 205, 45, 173, 101, 40, 78, 165, 195, 56, 37, }; break;
                        case -39: level = 11; botItemID = 0; genes = new byte[] { 224, 238, 2, 27, 102, 10, 250, 125, 225, 252, }; break;
                        case -40: level = 12; botItemID = 110988; genes = new byte[] { 205, 45, 173, 101, 40, 78, 165, 195, 56, 37, }; break;
                        case -41: level = 12; botItemID = 0; genes = new byte[] { 145, 129, 73, 79, 223, 110, 69, 225, 50, 177 }; break;
                        case -42: level = 12; botItemID = 0; genes = new byte[] { 75, 189, 32, 0, 161, 182, 202, 214, 66, 70, }; break;
                        case -43: level = 13; botItemID = 0; introText = "Hey hey hey"; genes = new byte[] { 145, 203, 122, 65, 201, 98, 29, 100, 247, 240 }; break;
                        case -44: level = 13; botItemID = 0; genes = new byte[] { 135, 51, 219, 37, 241, 111, 81, 148, 183, 245, }; break;
                        case -45: level = 13; botItemID = 0; genes = new byte[] { 21, 27, 0, 194, 231, 32, 19, 240, 72, 250, }; break;
                        case -46: level = 14; botItemID = 0; genes = new byte[] { 55, 246, 253, 29, 244, 91, 52, 229, 33, 242, }; break;
                        case -47: level = 14; botItemID = 0; introText = "My wife still doest not believe me"; genes = new byte[] { 235, 125, 252, 144, 205, 158, 37, 109, 95, 0, }; break;
                        case -48: level = 14; botItemID = 0; genes = new byte[] { 14, 14, 153, 133, 202, 193, 247, 77, 226, 24, }; break;
                        case -49: level = 15; botItemID = 0; introText = "Wasasasa wasa wasa"; genes = new byte[] { 97, 186, 117, 13, 47, 141, 188, 190, 231, 98, }; break;
                        case -50: level = 15; botItemID = 0; genes = new byte[] { 187, 85, 182, 157, 197, 58, 43, 171, 14, 148, }; break;
                        case -51: level = 15; botItemID = 0; genes = new byte[] { 61, 214, 97, 16, 173, 52, 55, 218, 218, 23, }; break;
                        case -52: level = 15; botItemID = 0; introText = "PM me for nachos"; genes = new byte[] { 21, 43, 3, 20, 205, 239, 157, 121, 148, 200, }; break;
                        case -53: level = 16; botItemID = 0; genes = new byte[] { 122, 126, 4, 86, 138, 161, 173, 188, 217, 9, }; break;
                        case -54: level = 16; botItemID = 0; genes = new byte[] { 31, 178, 25, 47, 197, 24, 91, 18, 36, 165, }; break;
                        case -55: level = 16; botItemID = 0; introText = "Cold nachos or hot nachos?"; genes = new byte[] { 236, 166, 41, 184, 74, 99, 53, 178, 237, 145, }; break;
                        case -56: level = 16; botItemID = 0; genes = new byte[] { 181, 62, 101, 177, 50, 199, 105, 21, 5, 215 }; break;
                        case -57: level = 16; botItemID = 0; introText = "Just get rekt man"; genes = new byte[] { 218, 98, 58, 113, 15, 35, 6, 184, 0, 52, }; break;
                        case -58: level = 16; botItemID = 0; genes = new byte[] { 218, 224, 182, 214, 13, 108, 167, 3, 114, 109, }; break;
                        case -59: level = 16; botItemID = 0; genes = new byte[] { 226, 50, 168, 123, 194, 11, 117, 193, 18, 5, }; break;
                        case -60: level = 16; botItemID = 0; genes = new byte[] { 25, 119, 165, 120, 137, 252, 108, 184, 63, 154, }; break;
                        case -61: level = 16; botItemID = 0; genes = new byte[] { 235, 82, 164, 247, 121, 136, 242, 77, 222, 251, }; break;
                        case -62: level = 16; botItemID = 0; genes = new byte[] { 163, 32, 214, 236, 118, 198, 228, 182, 98, 125 }; break;

                        default:
                            // todo remove this hack. implement for bot id = [-63,-99] ?
                            if (botID < 100)
                            {
                                level = 16; botItemID = 0; genes = new byte[] { 163, 32, 214, 236, 118, 198, 228, 182, 98, 125 }; break;
                            }
                            else
                            {
                                throw new ContractException("invalid bot");
                            }
                    }
                    break;
            }

            var bot = new NachoWrestler()
            {
                genes = genes,
                experience = Constants.EXPERIENCE_MAP[level],
                nickname = "",
                score = Constants.DEFAULT_SCORE,
                location = WrestlerLocation.None,
                itemID = botItemID,
                comments = new string[Constants.LUCHADOR_COMMENT_MAX],
                moveOverrides = new byte[Constants.MOVE_OVERRIDE_COUNT],
            };

            bot.comments[Constants.LUCHADOR_COMMENT_INTRO] = introText;

            return bot;
        }

        public void SetWrestlerFlags(Address from, BigInteger wrestlerID, WrestlerFlags flag)
        {
            Runtime.Expect(from == DevelopersAddress, "invalid permissions");
            var wrestler = GetWrestler(wrestlerID);

            wrestler.flags = flag;
            SetWrestler(wrestlerID, wrestler);
        }

        public NachoWrestler GetWrestler(BigInteger wrestlerID)
        {
            Runtime.Expect(wrestlerID > 0, "null or negative id");

            if (wrestlerID < Constants.BASE_LUCHADOR_ID)
            {
                return GetBot((int)wrestlerID);
            }

            var nft = Runtime.Nexus.GetNFT(Constants.WRESTLER_SYMBOL, wrestlerID);

            var wrestler = Serialization.Unserialize<NachoWrestler>(nft.RAM);
            if (wrestler.moveOverrides == null || wrestler.moveOverrides.Length < Constants.MOVE_OVERRIDE_COUNT)
            {
                var temp = wrestler.moveOverrides;
                wrestler.moveOverrides = new byte[Constants.MOVE_OVERRIDE_COUNT];

                if (temp != null)
                {
                    for (int i = 0; i < temp.Length; i++)
                    {
                        wrestler.moveOverrides[i] = temp[i];
                    }
                }
            }

            if (nft.CurrentOwner == nft.CurrentChain)
            {
                wrestler.location = WrestlerLocation.Market;
            }

            // TODO fix -> por alguma razão o itemID não está inicializado mas quando se cria um novo lutador no server, o itemID é inicializado com 0
            //if (wrestler.itemID != 0) // TODO podemos por este if outra vez dps dos operadores do big int estarem corrigidos
            if (wrestler.itemID > 0)
            {
                //var itemKind = Formulas.GetItemKind(wrestler.itemID);
                //var itemKind = GetItem(wrestler.itemID).kind;
                // todo confirmar apagar este código. este tryparse já não sentido acho eu
                //int n;
                //if (int.TryParse(itemKind.ToString(), out n))
                //{
                //    wrestler.itemID = 0;
                //}
            }

            if (!IsValidMaskOverride(wrestler))
            {
                wrestler.maskOverrideID = 0;
                wrestler.maskOverrideRarity = 0;
                wrestler.maskOverrideCheck = 0;
            }

            return wrestler;
        }

        private bool IsValidMaskOverride(NachoWrestler wrestler)
        {
            var checksum = (byte)((wrestler.maskOverrideID * 11 * wrestler.maskOverrideRarity * 7) % 256);
            return checksum == wrestler.maskOverrideCheck;
        }

        private bool HasValidGenes(NachoWrestler wrestler)
        {
            if (wrestler.genes == null || wrestler.genes.Length < 10)
            {
                return false;
            }

            for (int i = 0; i < wrestler.genes.Length; i++)
            {
                if (wrestler.genes[i] != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetWrestler(BigInteger wrestlerID, NachoWrestler wrestler)
        {
            Runtime.Expect(HasValidGenes(wrestler), "invalid wrestler genes"); // if this gets triggered, imediately warn Sergio about it

            /* TODO LATER
        var luchador = Luchador.FromGenes(wrestlerID, wrestler.genes);
        Runtime.Expect(luchador.Rarity != Rarity.Bot, "invalid dummy");
        */

            var bytes = Serialization.Serialize(wrestler);
            Runtime.Nexus.EditNFTContent(Constants.WRESTLER_SYMBOL, wrestlerID, bytes);
        }

        #endregion

        #region AUCTION API

        /*
        public BigInteger GetAuctionCurrentPrice(BigInteger auctionID)
        {
            var auctions = Storage.FindMapForContract<BigInteger, NachoAuction>(GLOBAL_AUCTIONS_LIST);
            var auction = auctions.Get(auctionID);

            var activeList = Storage.FindCollectionForContract<BigInteger>(ACTIVE_AUCTIONS_LIST);
            Runtime.Expect(activeList.Contains(auctionID), "auction finished");

            var totalDays = (auction.endTime - auction.startTime) / Constants.SECONDS_PER_DAY;
            if (totalDays < 1) totalDays = 1;

            var currentDay = (Runtime.Time.Value - auction.startTime) / Constants.SECONDS_PER_DAY;

            var incrementPerDay = (auction.endPrice - auction.startPrice) / totalDays;

            var currentPrice = auction.startPrice + incrementPerDay * currentDay;

            return currentPrice;
        }

        public BigInteger SellWrestler(Address from, BigInteger wrestlerID)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            var nft = Runtime.Nexus.GetNFT(Constants.WRESTLER_SYMBOL, wrestlerID);


            var wrestler = GetWrestler(wrestlerID);

            Runtime.Expect(wrestler.location == WrestlerLocation.None, "location invalid");
            Runtime.Expect(!wrestler.flags.HasFlag(WrestlerFlags.Locked), "locked wrestler");

            // store info for the auction
            var currentTime = GetCurrentTime();
            var auction = new NachoAuction()
            {
                startTime = currentTime,
                endTime = currentTime + duration,
                startPrice = startPrice,
                endPrice = endPrice,
                currency = currency,
                contentID = wrestlerID,
                kind = AuctionKind.Luchador,
                creator = from,
                comment = comment,
            };

            var auctions = Storage.FindMapForContract<BigInteger, NachoAuction>(GLOBAL_AUCTIONS_LIST);
            var auctionID = auctions.Count() + 1;
            auctions.Set(auctionID, auction);

            var activeList = Storage.FindCollectionForContract<BigInteger>(ACTIVE_AUCTIONS_LIST);
            activeList.Add(auctionID);

            wrestler.auctionID = auctionID;
            wrestler.location = WrestlerLocation.Market;
            SetWrestler(wrestlerID, wrestler);

            Runtime.Notify(from, NachoEvent.Auction);
            return auctionID;
        }

        private void ProcessAuctionSale(Address to, BigInteger auctionID, ref NachoAuction auction)
        {
            var currentPrice = GetAuctionCurrentPrice(auctionID);

            BigInteger receivedAmount = currentPrice;
            Runtime.Expect(receivedAmount > 0, "invalid sale amount");

            if (auction.creator == DevelopersAddress)
            {
                var referalBonus = RegisterReferalPurchase(to, receivedAmount, auctionID);
                if (referalBonus > 0)
                {
                    receivedAmount -= referalBonus;
                }
            }

            // update account balance
            if (!SpendFromAccountBalance(to, currentPrice, auctionID))
            {
                throw new ContractException("balance failed");
            }

            Runtime.Expect(UpdateAccountBalance(auction.creator, receivedAmount), "seller credit failed");

            // update sale counter
            var account = GetAccount(auction.creator);
            account.counters[Constants.ACCOUNT_COUNTER_MARKET_SALES]++;
            SetAccount(auction.creator, account);

            // register sale
            var sale = new NachoSale()
            {
                auctionID = auctionID,
                buyer = to,
                price = receivedAmount,
                time = Runtime.Time.Value
            };

            var sales = Storage.FindCollectionForContract<NachoSale>(GLOBAL_SALES_LIST);
            sales.Add(sale);
        }

        // note: This can also be used to remove from auction previously owned wrestler, at zero GAS cost
        public void BuyWrestler(Address to, BigInteger auctionID)
        {
            // TODO loot boxes backend criar novo auction da mesma raridade do que foi comprado para manter as % das loot boxes
            Runtime.Expect(IsWitness(to), "witness failed");

            var auction = GetAuction(auctionID);

            Runtime.Expect(auction.kind == AuctionKind.Luchador, "invalid type");

            var activeList = Storage.FindCollectionForContract<BigInteger>(ACTIVE_AUCTIONS_LIST);
            Runtime.Expect(activeList.Contains(auctionID), "auction finished");

            var wrestlerID = auction.contentID;
            var wrestler = GetWrestler(wrestlerID);

            if (wrestler.owner != to)
            {
                ProcessAuctionSale(to, auctionID, ref auction);

                var oldTeam = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_WRESTLERS, auction.creator);
                oldTeam.Remove(wrestlerID);

                var newTeam = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_WRESTLERS, to);
                newTeam.Add(wrestlerID);

                // update wrestler owner
                wrestler.owner = to;
            }

            wrestler.location = WrestlerLocation.None;
            wrestler.auctionID = 0;
            SetWrestler(wrestlerID, wrestler);

            // delete this auction from active list
            activeList.Remove(auctionID);

            Runtime.Notify(to, NachoEvent.Purchase);
        }

        public BigInteger SellItem(Address from, BigInteger itemID, BigInteger startPrice, BigInteger endPrice, AuctionCurrency currency, uint duration, string comment)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(startPrice >= Constants.MINIMUM_AUCTION_PRICE, "start price failed");
            Runtime.Expect(endPrice >= Constants.MINIMUM_AUCTION_PRICE, "end price failed");
            Runtime.Expect(duration >= Constants.MINIMUM_AUCTION_DURATION, "duration failed");
            Runtime.Expect(comment != null, "invalid comment");
            Runtime.Expect(comment.Length < Constants.MAX_COMMENT_LENGTH, "comment too large");

            var items = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_ITEMS, from);
            Runtime.Expect(items.Contains(itemID), "item invalid");

            var itemKind = Formulas.GetItemKind(itemID);
            Runtime.Expect(itemKind != ItemKind.Dev_Badge, "unsoldable item");

            var item = GetItem(itemID);

            if (item.owner == Address.Null)
            {
                item.owner = from;
            }

            Runtime.Expect(item.location == ItemLocation.None, "invalid location");
            Runtime.Expect(item.owner == from, "invalid owner");
            Runtime.Expect(!item.flags.HasFlag(ItemFlags.Locked), "locked item");

            if (from != DevelopersAddress)
            {
                Runtime.Expect(!item.flags.HasFlag(ItemFlags.Wrapped), "wrapped item");
            }

            // store info for the auction
            var currentTime = GetCurrentTime();
            var auction = new NachoAuction()
            {
                startTime = currentTime,
                endTime = currentTime + duration,
                startPrice = startPrice,
                endPrice = endPrice,
                currency = currency,
                contentID = itemID,
                kind = AuctionKind.Equipment,
                creator = from,
                comment = comment,
            };

            var auctions = Storage.FindMapForContract<BigInteger, NachoAuction>(GLOBAL_AUCTIONS_LIST);
            BigInteger auctionID = auctions.Count() + 1;
            auctions.Set(auctionID, auction);

            var activeList = Storage.FindCollectionForContract<BigInteger>(ACTIVE_AUCTIONS_LIST);
            activeList.Add(auctionID);

            // update item
            item.location = ItemLocation.Market;
            item.locationID = auctionID;
            SetItem(itemID, item);

            Runtime.Notify(from, NachoEvent.Auction);
            return auctionID;
        }

        // note: This can also be used to remove from auction previously owned item, at zero GAS cost
        public void BuyItem(Address to, BigInteger auctionID)
        {
            // TODO loot boxes backend criar novo auction da mesma raridade do que foi comprado para manter as % das loot boxes

            Runtime.Expect(IsWitness(to), "witness failed");

            var auction = GetAuction(auctionID);

            Runtime.Expect(auction.kind == AuctionKind.Equipment, "invalid type");

            var activeList = Storage.FindCollectionForContract<BigInteger>(ACTIVE_AUCTIONS_LIST);
            Runtime.Expect(activeList.Contains(auctionID), "auction finished");

            var itemID = auction.contentID;

            var from_items = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_ITEMS, auction.creator);
            //Runtime.Expect(from_items.Contains(itemID), "invalid item index");

            var item = GetItem(itemID);

            if (item.owner == Address.Null)
            {
                item.owner = auction.creator;
            }

             //those should be required later 
             //Runtime.Expect(item.location == ItemLocation.Market, "invalid location");
            ///Runtime.Expect(item.locationID == auctionID, "invalid auction");

            item.location = ItemLocation.None;
            item.owner = to;
            SetItem(itemID, item);

            if (auction.creator != to)
            {
                ProcessAuctionSale(to, auctionID, ref auction);

                from_items.Remove(itemID);

                var to_items = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_ITEMS, to);
                to_items.Add(itemID);
            }
            else
            {
                if (!from_items.Contains(itemID))
                {
                    from_items.Add(itemID);
                }
            }

            // delete this auction from active list
            activeList.Remove(auctionID);

            Runtime.Notify(to, NachoEvent.Purchase);
        }

        public bool UpdateAuction(Address from, BigInteger auctionID, BigInteger contentID, BigInteger startPrice, BigInteger endPrice, uint duration, string comment)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(startPrice >= Constants.MINIMUM_AUCTION_PRICE, "start price failed");
            Runtime.Expect(endPrice >= Constants.MINIMUM_AUCTION_PRICE, "end price failed");
            Runtime.Expect(duration >= Constants.MINIMUM_AUCTION_DURATION, "duration failed");
            Runtime.Expect(comment != null, "invalid comment");
            Runtime.Expect(comment.Length < Constants.MAX_COMMENT_LENGTH, "comment too large");

            var auctions = Storage.FindMapForContract<BigInteger, NachoAuction>(GLOBAL_AUCTIONS_LIST);
            var auction = auctions.Get(auctionID);

            Runtime.Expect(auction.creator == from, "invalid owner");
            Runtime.Expect(auction.contentID == contentID, "invalid element");

            var activeList = Storage.FindCollectionForContract<BigInteger>(ACTIVE_AUCTIONS_LIST);
            Runtime.Expect(activeList.Contains(auctionID), "auction finished");

            var currentTime = GetCurrentTime();
            auction.startPrice = startPrice;
            auction.endPrice = endPrice;
            auction.startTime = currentTime;
            auction.endTime = currentTime + duration;
            auction.comment = comment;

            auctions.Set(auctionID, auction);
            Runtime.Notify(from, NachoEvent.Auction);
            //Runtime.Runtime.Notify("price ok");
            return true;
        }

        public const int auctionsPerRequest = 64;

        public BigInteger[] GetActiveAuctions()
        {
            var activeList = Storage.FindCollectionForContract<BigInteger>(ACTIVE_AUCTIONS_LIST);
            return activeList.All();
        }

        public NachoAuction[] GetAuctions(BigInteger[] IDs)
        {
            var auctions = Storage.FindMapForContract<BigInteger, NachoAuction>(GLOBAL_AUCTIONS_LIST);
            return auctions.All(IDs);
        }

        public NachoAuction GetAuction(BigInteger auctionID)
        {
            var auctions = Storage.FindMapForContract<BigInteger, NachoAuction>(GLOBAL_AUCTIONS_LIST);
            var auction = auctions.Get(auctionID);
            if (auction.comment == null)
            {
                auction.comment = "";
            }

            return auction;
        }

        private void SetAuction(BigInteger auctionID, NachoAuction auction)
        {
            var auctions = Storage.FindMapForContract<BigInteger, NachoAuction>(GLOBAL_AUCTIONS_LIST);
            auctions.Set(auctionID, auction);
        }

        public int GetAuctionCount()
        {
            var auctions = Storage.FindMapForContract<BigInteger, NachoAuction>(GLOBAL_AUCTIONS_LIST);
            return (int)auctions.Count();
        }

        public NachoSale GetSale(BigInteger saleID)
        {
            var sales = Storage.FindMapForContract<BigInteger, NachoSale>(GLOBAL_SALES_LIST);
            var sale = sales.Get(saleID);
            return sale;
        }

        public NachoSale[] GetSales(BigInteger[] IDs)
        {
            var sales = Storage.FindMapForContract<BigInteger, NachoSale>(GLOBAL_SALES_LIST);
            return sales.All(IDs);
        }

        public int GetSaleCount()
        {
            var sales = Storage.FindMapForContract<BigInteger, NachoSale>(GLOBAL_SALES_LIST);
            return (int)sales.Count();
        }*/

        #endregion

        #region ITEM API

        public void EquipItem(Address from, BigInteger wrestlerID, BigInteger itemID)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(HasWrestler(from, wrestlerID), "invalid wrestler");
            Runtime.Expect(HasItem(from, itemID), "invalid item");

            var item = GetItem(itemID);

            //if (item.owner == Address.Null)
            //{
            //    item.owner = from;
            //}

            Runtime.Expect(item.location == ItemLocation.None, "invalid location");
            Runtime.Expect(!item.flags.HasFlag(ItemFlags.Wrapped), "wrapped item");

            var nft = Runtime.Nexus.GetNFT(Constants.ITEM_SYMBOL, itemID);
            Runtime.Expect(nft.CurrentOwner == from, "invalid owner");

            var wrestler = GetWrestler(wrestlerID);
            wrestler.itemID = itemID;
            SetWrestler(wrestlerID, wrestler);

            item.location = ItemLocation.Wrestler;
            item.wrestlerID = wrestlerID;
            SetItem(itemID, item);

            Runtime.Notify(NachoEvent.ItemAdded, from, itemID);
        }

        public void UnequipItem(Address from, BigInteger wrestlerID)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(HasWrestler(from, wrestlerID), "invalid wrestler");

            var wrestler = GetWrestler(wrestlerID);
            Runtime.Expect(wrestler.itemID > 0, "item failed");

            var itemID = wrestler.itemID;

            var item = GetItem(itemID);

            //if (item.owner == Address.Null)
            //{
            //    item.owner = from;
            //}

            Runtime.Expect(item.location == ItemLocation.Wrestler, "invalid location");
            //Runtime.Expect(item.locationID == wrestlerID, "invalid wrestler"); // TODO fix

            var nft = Runtime.Nexus.GetNFT(Constants.ITEM_SYMBOL, itemID);
            Runtime.Expect(nft.CurrentOwner == from, "invalid owner");

            wrestler.itemID = 0;
            SetWrestler(wrestlerID, wrestler);

            item.location = ItemLocation.None;
            item.wrestlerID = 0;
            SetItem(itemID, item);

            Runtime.Notify(NachoEvent.ItemRemoved, from, itemID);
        }
        #endregion

        #region ROOM API
        public void DecorateMysteryRoom(Address from, BigInteger itemID)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(HasItem(from, itemID), "invalid item");

            var item = GetItem(itemID);

            //if (item.owner == Address.Null)
            //{
            //    item.owner = from;
            //}

            Runtime.Expect(item.location == ItemLocation.None, "invalid location");
            Runtime.Expect(!item.flags.HasFlag(ItemFlags.Wrapped), "wrapped item");

            var nft = Runtime.Nexus.GetNFT(Constants.ITEM_SYMBOL, itemID);
            Runtime.Expect(nft.CurrentOwner == from, "invalid owner");

            item.location = ItemLocation.Room;
            item.wrestlerID = 0;
            SetItem(itemID, item);

            Runtime.Notify(NachoEvent.ItemAdded, from, itemID);
        }

        public void JoinMysteryRoom(Address from, BigInteger wrestlerID, BigInteger stakeAmount)
        {
            Runtime.Expect(IsWitness(from), "witness failed");
            Runtime.Expect(stakeAmount >= 0, "invalid stake amount");

            Runtime.Expect(HasWrestler(from, wrestlerID), "invalid wrestler");

            var wrestler = GetWrestler(wrestlerID);
            Runtime.Expect(wrestler.location == WrestlerLocation.None, "location failed");

            if (stakeAmount > 0)
            {
                Runtime.Expect(SpendFromAccountBalance(from, stakeAmount, wrestlerID), "not enough funds");
            }

            Timestamp last_time = wrestler.roomTime;
            var current_time = GetCurrentTime();

            var diff = current_time - last_time;
            Runtime.Expect(diff >= Constants.SECONDS_PER_DAY, "time failed");

            wrestler.roomTime = current_time;
            wrestler.location = WrestlerLocation.Room;
            wrestler.stakeAmount = stakeAmount;
            SetWrestler(wrestlerID, wrestler);
        }

        private static int Fibonacci(int n)
        {
            int a = 0;
            int b = 1;
            // In N steps compute Fibonacci sequence iteratively.
            for (int i = 0; i < n; i++)
            {
                int temp = a;
                a = b;
                b = temp + b;
            }
            return a;
        }

        // note - requires that wrestler spent at least one hour training
        public void LeaveMysteryRoom(Address from, BigInteger wrestlerID)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            //var wrestlers = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_WRESTLERS, from);
            //var wrestlers = _accountWrestlers.Get<Address, StorageList>(from);
            var ownership = new OwnershipSheet(Constants.WRESTLER_SYMBOL);
            var wrestlers = ownership.Get(this.Storage, from);
            Runtime.Expect(wrestlers.Contains(wrestlerID), "invalid wrestler");

            var wrestler = GetWrestler(wrestlerID);
            Runtime.Expect(wrestler.location == WrestlerLocation.Room, "location failed");

            var stakedAmount = wrestler.stakeAmount;
            if (wrestler.stakeAmount > 0)
            {
                //Runtime.Expect(UpdateAccountBalance(from, wrestler.stakeAmount), "unstake failed");
                Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Constants.SOUL_SYMBOL, from, Runtime.Chain.Address, wrestler.stakeAmount), "unstake failed");
                //Runtime.Notify(from, NachoEvent.Withdraw, wrestler.stakeAmount);
                Runtime.Notify(EventKind.TokenUnstake, from, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = Constants.SOUL_SYMBOL, value = wrestler.stakeAmount });
                wrestler.stakeAmount = 0;
            }

            wrestler.location = WrestlerLocation.None;
            SetWrestler(wrestlerID, wrestler);

            //var roomCounter = Storage.Get(ROOM_COUNTER_KEY).AsBigInteger();
            //var roomSequence = Storage.Get(ROOM_SEQUENCE_KEY).AsBigInteger();
            _roomCounter++;

            if (_roomSequence < 1)
            {
                _roomSequence = 1;
            }

            var nextNumber = Fibonacci((int)_roomSequence);

            if (_roomCounter >= nextNumber && stakedAmount >= nextNumber)
            {
                //BigInteger itemID;
                //BigInteger lastID = Runtime.Time.Value;

                Rarity rarity;
                if (nextNumber >= 1000)
                {
                    rarity = Rarity.Epic;
                }
                else
                if (nextNumber >= 100)
                {
                    rarity = Rarity.Rare;
                }
                else
                if (nextNumber >= 20)
                {
                    rarity = Rarity.Uncommon;
                }
                else
                {
                    rarity = Rarity.Common;
                }

                var itemKind = ItemKind.None;

                //var temp = Storage.FindMapForContract<BigInteger, bool>(ITEM_MAP);
                do
                {
                    //itemID = MineItemRarity(rarity, ref lastID); //Equipment.MineItemRarity(rarity, ref lastID);

                    //var itemKind = Formulas.GetItemKind(itemID);
                    itemKind = GetRandomItemKind(rarity);

                    //var hasItem = _globalItemList.Get<BigInteger, bool>(itemID);
                    //if (Rules.IsReleasedItem(itemKind) && !temp.ContainsKey(itemID))
                    if (Rules.IsReleasedItem(itemKind)/* && !hasItem*/)
                    {
                        break;
                    }

                    //lastID++;
                } while (true);

                //CreateItem(from, itemID, itemKind, false);
                CreateItem(from, itemKind, false);
                AddTrophy(from, TrophyFlag.Safe);

                if (nextNumber >= 10000)
                {
                    _roomCounter = 0;
                    _roomSequence = 1;
                }
                else
                {
                    _roomSequence++;
                }

                //Storage.Put(ROOM_SEQUENCE_KEY, _roomSequence);
            }

            //Storage.Put(ROOM_COUNTER_KEY, _roomCounter);
        }
        #endregion

        #region GYM API
        public void RecoverMojo(Address from, BigInteger wrestlerID)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(HasWrestler(from, wrestlerID), "invalid wrestler");

            var wrestler = GetWrestler(wrestlerID);
            Runtime.Expect(wrestler.currentMojo < wrestler.maxMojo, "max mojo already");

            var diff = GetCurrentTime() - wrestler.mojoTime;
            var minutesDiff = diff / 60; // convert to minutes
            Runtime.Expect(minutesDiff >= Constants.MOJO_REFRESH_MINUTES, "too soon");

            int increase = (int)(minutesDiff / Constants.MOJO_REFRESH_MINUTES);
            if (increase < 1)
            {
                increase = 1;
            }

            wrestler.currentMojo = wrestler.currentMojo + increase;
            wrestler.mojoTime = GetCurrentTime();

            SetWrestler(wrestlerID, wrestler);
        }

        public void StartTrainingWrestler(Address from, BigInteger wrestlerID, StatKind mode)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            //Runtime.Expect(mode >= 1 && mode <= 3, "mode failed");
            Runtime.Expect((int)mode >= 1 && (int)mode <= 3, "mode failed");

            Runtime.Expect(HasWrestler(from, wrestlerID), "invalid wrestler");

            var wrestler = GetWrestler(wrestlerID);
            Runtime.Expect(wrestler.location == WrestlerLocation.None, "location failed");

            Timestamp last_time = wrestler.gymTime;
            var current_time = GetCurrentTime();

            var diff = current_time - last_time;
            Runtime.Expect(diff >= Constants.SECONDS_PER_DAY, "time failed");

            var account = GetAccount(from);
            Runtime.Expect(account.battleID == 0, "in battle");
            Runtime.Expect(account.queueMode == BattleMode.None, "in queue");

            wrestler.gymTime = current_time;
            wrestler.location = WrestlerLocation.Gym;
            wrestler.trainingStat = mode;
            SetWrestler(wrestlerID, wrestler);
        }

        private int GetMaxGymXPForWrestler(NachoWrestler wrestler, ItemKind itemKind)
        {
            var maxXPPerSession = Constants.SECONDS_PER_DAY;

            if (itemKind == ItemKind.Gym_Card)
            {
                maxXPPerSession *= Constants.GYM_CARD_DAYS;
            }

            return maxXPPerSession;
        }

        private int GetObtainedGymXP(NachoWrestler wrestler, int maxXPPerSession, ItemKind itemKind)
        {
            var start_time = wrestler.gymTime;
            var current_time = GetCurrentTime();

            int diff = (int)(current_time.Value - start_time.Value);
            if (diff < Constants.SECONDS_PER_HOUR)
            {
                return 0;
            }

            var obtainedXP = diff;

            if (itemKind == ItemKind.Dumbell)
            {
                var extra = (obtainedXP * Constants.DUMBELL_BOOST_PERCENTAGE) / 100;
                obtainedXP += extra;
            }

            if (obtainedXP > maxXPPerSession)
            {
                obtainedXP = maxXPPerSession;
            }

            if (wrestler.experience + obtainedXP > Constants.WRESTLER_MAX_XP)
            {
                obtainedXP = Constants.WRESTLER_MAX_XP - (int)wrestler.experience;
            }

            return obtainedXP;
        }

        public bool GetTrainingStatus(BigInteger wrestlerID)
        {
            var wrestler = GetWrestler(wrestlerID);
            Runtime.Expect(wrestler.location == WrestlerLocation.Gym, "location failed");

            //var itemKind = Formulas.GetItemKind(wrestler.itemID);
            var itemKind = wrestler.itemID > 0 ? GetItem(wrestler.itemID).kind : ItemKind.None;

            var maxXP = GetMaxGymXPForWrestler(wrestler, itemKind);
            var obtainedXP = GetObtainedGymXP(wrestler, maxXP, itemKind);
            return obtainedXP < maxXP;
        }

        // note - requires that wrestler spent at least one hour training
        public void EndTrainingWrestler(Address from, BigInteger wrestlerID)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(HasWrestler(from, wrestlerID), "invalid wrestler");

            var wrestler = GetWrestler(wrestlerID);
            Runtime.Expect(wrestler.location == WrestlerLocation.Gym, "location failed");

            //var itemKind = Formulas.GetItemKind(wrestler.itemID);
            var itemKind = wrestler.itemID > 0 ? GetItem(wrestler.itemID).kind : ItemKind.None;

            var maxXPPerSession = GetMaxGymXPForWrestler(wrestler, itemKind);

            // one XP point is earned per second inside gym
            var obtainedXP = GetObtainedGymXP(wrestler, maxXPPerSession, itemKind);
            Runtime.Expect(obtainedXP > 0, "xp failed");

            var current_xp = wrestler.experience + obtainedXP;

            var obtainedEV = (int)(obtainedXP / Constants.SECONDS_PER_HOUR);
            IncreaseWrestlerEV(ref wrestler, wrestler.trainingStat, obtainedEV);

            wrestler.experience = current_xp;
            wrestler.location = WrestlerLocation.None;
            wrestler.trainingStat = 0;
            SetWrestler(wrestlerID, wrestler);

            //Runtime.Runtime.Notify("gym finished");
        }

        private void IncreaseWrestlerEV(ref NachoWrestler wrestler, StatKind statKind, int obtainedEV)
        {
            var totalEV = wrestler.gymBoostStamina + wrestler.gymBoostAtk + wrestler.gymBoostDef;

            if (totalEV + obtainedEV > Formulas.MaxTrainStat)
            {
                obtainedEV = Formulas.MaxTrainStat - totalEV;
            }

            if (obtainedEV > 0)
            {
                if (statKind == StatKind.Stamina)
                {
                    var newStaminaBoost = wrestler.gymBoostStamina + obtainedEV;
                    if (newStaminaBoost > Constants.MAX_GYM_BOOST)
                    {
                        newStaminaBoost = Constants.MAX_GYM_BOOST;
                    }
                    wrestler.gymBoostStamina = (byte)newStaminaBoost;
                }
                else
                if (statKind == StatKind.Attack)
                {
                    var newAttackBoost = wrestler.gymBoostAtk + obtainedEV;
                    if (newAttackBoost > Constants.MAX_GYM_BOOST)
                    {
                        newAttackBoost = Constants.MAX_GYM_BOOST;
                    }
                    wrestler.gymBoostAtk = (byte)newAttackBoost;
                }
                else
                if (statKind == StatKind.Defense)
                {
                    var newDefenseBoost = wrestler.gymBoostDef + obtainedEV;
                    if (newDefenseBoost > Constants.MAX_GYM_BOOST)
                    {
                        newDefenseBoost = Constants.MAX_GYM_BOOST;
                    }
                    wrestler.gymBoostDef = (byte)newDefenseBoost;
                }
            }
        }
        #endregion

        #region MATCHMAKING
        
        private void DeleteChallengers(Address address)
        {
            //var list = Storage.FindCollectionForAddress<NachoVersusInfo>(ACCOUNT_CHALLENGES, address);
            var list = _playerVersusChallengesList.Get<Address, StorageList>(address);

            list.Clear();
        }
        
        public NachoVersusInfo[] GetVersusChallengers(Address from)
        {
            //var list = Storage.FindCollectionForAddress<NachoVersusInfo>(ACCOUNT_CHALLENGES, from);
            var list = _playerVersusChallengesList.Get<Address, StorageList>(from);

            //int count = list.Count();
            var count = list.Count();
            int i = 0;
            while (i < count)
            {
                // TODO fix às vezes quando entra aqui o list count = 1, fazemos o get do primeiro elemento get(0) e no get dá out of range pq diz que o count = 0
                //var entry = list.Get(i);
                var entry = list.Get<NachoVersusInfo>(i);
                bool discard = false;
                var otherAccount = GetAccount(entry.challenger);

                if (otherAccount.battleID != 0 || otherAccount.queueMode != BattleMode.Versus || otherAccount.queueVersus != from)
                {
                    discard = true;
                }
                else
                {
                    var diff = GetCurrentTime() - entry.time;
                    if (diff > 60 * 5)
                    {
                        discard = true;
                    }
                }

                if (discard)
                {
                    //list.RemoveAt(i);
                    list.RemoveAt<NachoVersusInfo>(i);
                }
                else
                {
                    i++;
                }
            }

            //return list.All();
            return list.All<NachoVersusInfo>();
        }
        
        public int GetMatchMakerCount(BattleMode mode)
        {
            //var list = Storage.FindCollectionForContract<Address>(GLOBAL_MATCHMAKER_LIST);
            
            int total = 0;
            //int count = list.Count();
            var count = _globalMatchmakerList.Count();
            for (int i = 0; i < count; i++)
            {
                //var address = list.Get(i);
                var address = _globalMatchmakerList.Get<Address>(i);
                var account = GetAccount(address);
                if (account.queueMode == mode)
                {
                    total++;
                }
            }
            return total;
        }
        
        public bool IsAddressInMatchMaker(Address address)
        {
            //var list = Storage.FindCollectionForContract<Address>(GLOBAL_MATCHMAKER_LIST);
            //return list.Contains(address);

            return _globalMatchmakerList.Contains(address);
        }

        private void InsertIntoMatchMaker(Address address)
        {
            //var list = Storage.FindCollectionForContract<Address>(GLOBAL_MATCHMAKER_LIST);
            
            //if (list.Contains(address))
            if (_globalMatchmakerList.Contains(address))
            {
                return;
            }

            //list.Add(address);
            _globalMatchmakerList.Add(address);
        }

        private void RemoveFromMatchMaker(Address address)
        {
            //var list = Storage.FindCollectionForContract<Address>(GLOBAL_MATCHMAKER_LIST);

            //if (list.Contains(address))
            if (_globalMatchmakerList.Contains(address))
            {
                //list.Remove(address);
                _globalMatchmakerList.Remove(address);
            }
        }

        private int CalculateScore(Address addressA, Address addressB)
        {
            if (addressA == addressB)
            {
                return -1;
            }

            var accountA = GetAccount(addressA);
            var accountB = GetAccount(addressB);

            if (accountA.queueMode != accountB.queueMode)
            {
                return -1;
            }

            var wrestlerA = GetWrestler(accountA.queueWrestlerIDs[0]);
            var levelA = Formulas.CalculateWrestlerLevel((int)wrestlerA.experience);

            var wrestlerB = GetWrestler(accountB.queueWrestlerIDs[0]);
            var levelB = Formulas.CalculateWrestlerLevel((int)wrestlerB.experience);

            var levelDiff = Math.Abs(levelA - levelB);

            int score = 0;

            switch (levelDiff)
            {
                case 0: score += 3; break;
                case 1: score += 2; break;
                case 2: score += 1; break;
                default: return -1;
            }

            //var eloDiff = Math.Abs(accountA.ELO - accountB.ELO) / 32;
            var eloDiff = Math.Abs((decimal) (accountA.ELO - accountB.ELO)) / 32;

            switch (eloDiff)
            {
                case 0: score += 1; break;
                case 1: score += 0; break;
                case 2: score -= 1; break;
                default: score -= 2; break;
            }

            // add a slight bias against playing with the same opponent twice in a row
            if (score > 1 && (accountA.lastOpponent == addressB || accountB.lastOpponent == addressA))
            {
                score--;
            }

            return score;
        }

        private bool MatchMakerFindMatch(Address targetAddress)
        {
            //var list = Storage.FindCollectionForContract<Address>(GLOBAL_MATCHMAKER_LIST);

            bool foundOwn = false;

            //var count = list.Count();
            var count = _globalMatchmakerList.Count();

            Address bestAddress = Address.Null;
            int bestScore = -1;
            uint bestTime = 0;

            var removalList = new System.Collections.Generic.List<Address>();

            var currentTime = GetCurrentTime();

            var targetAccount = GetAccount(targetAddress);

            for (int i = 0; i < count; i++)
            {
                //var otherAddress = list.Get(i);
                var otherAddress = _globalMatchmakerList.Get<Address>(i);

                if (otherAddress == targetAddress)
                {
                    foundOwn = true;
                    continue;
                }

                var otherAccount = GetAccount(otherAddress);
                if (otherAccount.queueMode == BattleMode.None || otherAccount.battleID != 0)
                {
                    removalList.Add(otherAddress);
                    continue;
                }

                var updateDiff = (uint)Math.Abs(currentTime - otherAccount.queueJoinTime);
                if (updateDiff >= Constants.MATCHMAKER_REMOVAL_SECONDS)
                {
                    removalList.Add(otherAddress);
                    continue;
                }

                var score = CalculateScore(targetAddress, otherAddress);

                if (score < 0)
                {
                    continue;
                }

                var joinDiff = (uint)Math.Abs(targetAccount.queueJoinTime - otherAccount.queueJoinTime);

                if (score == bestScore && joinDiff > bestTime)
                {
                    score++;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestAddress = otherAddress;
                    bestTime = joinDiff;
                }
            }

            foreach (var otherAddress in removalList)
            {
                RemoveAccountFromQueue(otherAddress);
            }

            Runtime.Expect(foundOwn, "not in matcher");

            if (bestScore > 0)
            {
                PrepareMatch(targetAddress, bestAddress);
                return true;
            }

            return false;
        }

        private int CalculateELO(int currentELO, int opponentELO, int side, BattleState result)
        {
            Runtime.Expect(result != BattleState.Active, "still active");

            float score;

            if (result == BattleState.Draw)
            {
                score = 0.5f;
            }
            else
            if (side == 0 && (result == BattleState.WinA || result == BattleState.ForfeitB))
            {
                score = 1;
            }
            else
            if (side == 1 && (result == BattleState.WinB || result == BattleState.ForfeitA))
            {
                score = 1;
            }
            else
            {
                score = 0;
            }

            var R1 = (float)Math.Pow(10.0f, currentELO / 400.0f);
            var R2 = (float)Math.Pow(10.0f, opponentELO / 400.0f);

            var E1 = R1 / (R1 + R2);

            float K = 32;
            var change = K * (score - E1);

            return (int)(currentELO + change);
        }
        #endregion

        #region QUEUE API
       
        private void SpendBet(Address from, BigInteger bet, BattleMode mode, Address address)
        {
            Runtime.Expect(bet > 0, "invalid bet");

            var account = GetAccount(address);

            if (mode == BattleMode.Ranked)
            {
                Runtime.Expect(bet == GetRankedBet(), "invalid bet");
                Runtime.Expect(account.queueBet == bet, "bets differ");

                var fee = new BigInteger(5); // (bet * Constants.POT_FEE_PERCENTAGE) / 100; TODO fix
                var split = bet - fee;

                Runtime.Expect(SpendFromAccountBalance(from, split, 0), "balance failed for bet");
                Runtime.Expect(SpendFromAccountBalance(from, fee, 0), "balance failed for fee");
            }
            else
            {
                Runtime.Expect(mode == BattleMode.Versus, "versus expected");

                // pick the minimum value
                if (account.queueBet < bet)
                {
                    bet = account.queueBet;
                }

                Runtime.Expect(SpendFromAccountBalance(from, bet, 0), "balance failed");
            }
        }

        private BigInteger GetRankedBet()
        {
            return new BigInteger(2); // GetConfig().rankedFee; TODO fix
        }

        public void JoinPraticeQueue(Address from, BigInteger wrestlerID, PraticeLevel level)
        {
            Runtime.Expect(IsWitness(from), "witness failed");
            JoinQueue(from, new BigInteger[] { wrestlerID }, 0, BattleMode.Pratice, Address.Null, level);
        }

        public void JoinSingleUnrankedQueue(Address from, BigInteger wrestlerID)
        {
            Runtime.Expect(IsWitness(from), "witness failed");
            JoinQueue(from, new BigInteger[] { wrestlerID }, 0, BattleMode.Unranked, Address.Null, PraticeLevel.None);
        }

        public void JoinSingleRankedQueue(Address from, BigInteger wrestlerID)
        {
            Runtime.Expect(IsWitness(from), "witness failed");
            var bet = GetRankedBet();
            JoinQueue(from, new BigInteger[] { wrestlerID }, bet, BattleMode.Ranked, Address.Null, PraticeLevel.None);
        }

        public void JoinSingleVersusQueue(Address from, BigInteger wrestlerID, Address other, BigInteger bet)
        {
            Runtime.Expect(IsWitness(from), "witness failed");
            Runtime.Expect(from != other, "same address");
            Runtime.Expect(DevelopersAddress != other, "invalid target");
            JoinQueue(from, new BigInteger[] { wrestlerID }, bet, BattleMode.Versus, other, PraticeLevel.None);
        }

        public void JoinDoubleUnrankedQueue(Address from, BigInteger[] wrestlerIDs)
        {
            Runtime.Expect(IsWitness(from), "witness failed");
            Runtime.Expect(wrestlerIDs.Length == 2, "double failed");
            JoinQueue(from, wrestlerIDs, 0, BattleMode.Unranked, Address.Null, PraticeLevel.None);
        }

        public void JoinDoubleRankedQueue(Address from, BigInteger[] wrestlerIDs)
        {
            Runtime.Expect(IsWitness(from), "witness failed");
            Runtime.Expect(wrestlerIDs.Length == 2, "double failed");
            var bet = GetRankedBet();
            JoinQueue(from, wrestlerIDs, bet, BattleMode.Ranked, Address.Null, PraticeLevel.None);
        }

        /*
        public void JoinDoubleVersusQueue(Address from, BigInteger[] wrestlerIDs, Address other, BigInteger bet)
        {
            Runtime.Expect(IsWitness(from), "witness failed");
            Runtime.Expect(from != other, "same address");
            Runtime.Expect(this.Address != other, "invalid target");
            Runtime.Expect(wrestlerIDs.Length == 2, "double failed");
            JoinQueue(from, wrestlerIDs, bet, BattleMode.Versus, other, PraticeLevel.None);
        }
        */
        
        private void SetAccountBattle(Address address, BigInteger battleID, NachoBattle battle, int sideIndex)
        {
            var account = GetAccount(address);

            Runtime.Expect(account.queueMode == BattleMode.None, "queued not clear");

            if (battle.state == BattleState.Active)
            {
                account.battleID = battleID;

                // Start battle -> set wrestlers location to Battle
                foreach (var luchadorBattleState in battle.sides[0].wrestlers)
                {
                    var wrestler = GetWrestler(luchadorBattleState.wrestlerID);
                    wrestler.location = WrestlerLocation.Battle;
                }

                foreach (var luchadorBattleState in battle.sides[1].wrestlers)
                {
                    var wrestler = GetWrestler(luchadorBattleState.wrestlerID);
                    wrestler.location = WrestlerLocation.Battle;
                }
            }
            else
            {
                // End battle -> set wrestlers location to None

                foreach (var luchadorBattleState in battle.sides[0].wrestlers)
                {
                    var wrestler = GetWrestler(luchadorBattleState.wrestlerID);
                    wrestler.location = WrestlerLocation.None;
                }

                foreach (var luchadorBattleState in battle.sides[1].wrestlers)
                {
                    var wrestler = GetWrestler(luchadorBattleState.wrestlerID);
                    wrestler.location = WrestlerLocation.None;
                }

                bool victory = false;
                bool loss = false;

                if (sideIndex == 0)
                {
                    if (battle.state == BattleState.WinA || battle.state == BattleState.ForfeitB)
                    {
                        victory = true;
                    }
                    else
                    if (battle.state == BattleState.WinB || battle.state == BattleState.ForfeitA)
                    {
                        loss = true;
                    }
                }
                else
                if (sideIndex == 1)
                {
                    if (battle.state == BattleState.WinA || battle.state == BattleState.ForfeitB)
                    {
                        loss = true;
                    }
                    else
                    if (battle.state == BattleState.WinB || battle.state == BattleState.ForfeitA)
                    {
                        victory = true;
                    }
                }

                int baseIndex;

                if (battle.mode == BattleMode.Ranked)
                {
                    baseIndex = 3;
                }
                else
                if (battle.mode == BattleMode.Unranked)
                {
                    baseIndex = 0;
                }
                else
                {
                    baseIndex = -1;
                }

                if (baseIndex != -1)
                {
                    var winIndex = 0 + baseIndex;
                    var lossIndex = 1 + baseIndex;
                    var drawIndex = 2 + baseIndex;

                    if (victory)
                    {
                        account.counters[winIndex]++;
                        victory = true;
                    }
                    else
                    if (loss)
                    {
                        account.counters[lossIndex]++;
                    }
                    else
                    {
                        account.counters[drawIndex]++;
                    }
                }

                if (battle.mode == BattleMode.Unranked && address != DevelopersAddress)
                {
                    if (victory)
                    {
                        account.counters[Constants.ACCOUNT_COUNTER_CURRENT_STREAK]++;

                        if (account.counters[Constants.ACCOUNT_COUNTER_CURRENT_STREAK] > account.counters[Constants.ACCOUNT_COUNTER_LONGEST_STREAK])
                        {
                            account.counters[Constants.ACCOUNT_COUNTER_LONGEST_STREAK] = account.counters[Constants.ACCOUNT_COUNTER_CURRENT_STREAK];
                        }
                    }
                    else
                    {
                        account.counters[Constants.ACCOUNT_COUNTER_CURRENT_STREAK] = 0;
                    }
                }

                /* TODO mint nachos
                if (battle.mode == BattleMode.Pratice || battle.mode == BattleMode.Unranked)
                {
                    account.balanceNACHOS++;
                }*/

                account.battleID = 0;
            }

            SetAccount(address, account);
        }

        private void StartBotMatch(Address from, BigInteger botID)
        {
            var botAddress = Runtime.Chain.Address;

            var botAccount = GetAccount(botAddress);
            botAccount.queueJoinTime = GetCurrentTime();
            botAccount.queueUpdateTime = GetCurrentTime();
            botAccount.queueBet = 0;
            botAccount.queueWrestlerIDs = new BigInteger[] {botID};
            botAccount.queueVersus = Address.Null;
            botAccount.queueMode = BattleMode.Pratice;
            SetAccount(botAddress, botAccount);

            PrepareMatch(botAddress, from);
        }

        // NOTE - bet parameter is hijacked for JoinPratice, who passes level of bot inside bet arg
        private void JoinQueue(Address from, BigInteger[] wrestlerIDs, BigInteger bet, BattleMode mode, Address versus, PraticeLevel praticeLevel)
        {
            Runtime.Expect(mode != BattleMode.None, "invalid queue mode");

            /*            if (mode == BattleMode.Versus)
                        {
                            // TODO support tag battles
                            var wrestlerA = GetWrestler(queue.wrestlerIDs[0]);
                            var wrestlerB = GetWrestler(wrestlerIDs[0]);

                            var levelA = Formulas.CalculateWrestlerLevel(wrestlerA.experience);
                            var levelB = Formulas.CalculateWrestlerLevel(wrestlerB.experience);

                            Runtime.Expect(levelA == levelB, "luchadores must be same level");

                            if (bet > 0)
                            {
                                SpendBet(from, bet, mode, ref queueB);
                            }
                        }*/

            if (mode == BattleMode.Versus)
            {
                Runtime.Expect(bet >= 0, "invalid bet");
                Runtime.Expect(versus != Address.Null, "invalid versus address");
            }
            else
            {
                if (mode == BattleMode.Ranked)
                {
                    Runtime.Expect(bet > 0, "invalid bet");
                }
                else
                {
                    Runtime.Expect(bet == 0, "invalid bet");
                }

                Runtime.Expect(versus == Address.Null, "unexpected versus address");
            }

            if (mode != BattleMode.Pratice)
            {
                Runtime.Expect(praticeLevel == PraticeLevel.None, "unexpected pratice level");
            }

            //var team = Storage.FindCollectionForAddress<BigInteger>(ACCOUNT_WRESTLERS, from);

            var wrestlers = new NachoWrestler[wrestlerIDs.Length];
            var levels = new byte[wrestlerIDs.Length];
            for (int i = 0; i < wrestlerIDs.Length; i++)
            {
                var ID = wrestlerIDs[i];

                Runtime.Expect(HasWrestler(from, ID), "invalid wrestler");

                var wrestler = GetWrestler(ID);
                Runtime.Expect(wrestler.location == WrestlerLocation.None, "invalid location");
                //Runtime.Expect(wrestler.currentMojo > 0, "not enough mojo"); // TODO fix

                var nft = Runtime.Nexus.GetNFT(Constants.WRESTLER_SYMBOL, ID);
                Runtime.Expect(nft.CurrentOwner == from, "invalid owner");
                
                var level = Formulas.CalculateWrestlerLevel((int)wrestler.experience);

                if (mode == BattleMode.Ranked)
                {
                    Runtime.Expect(level == Constants.MAX_LEVEL, "luchador must be max level");
                }
                else
                if (mode == BattleMode.Unranked)
                {
                    //key = $"{key}/{level}";
                }

                levels[i] = (byte)level;
                wrestlers[i] = wrestler;
            }

            var account = GetAccount(from);
            Runtime.Expect(account.queueMode == BattleMode.None, "already queued");

            account.queueJoinTime = GetCurrentTime();
            account.queueUpdateTime = GetCurrentTime();
            account.queueBet = bet;
            account.queueWrestlerIDs = wrestlerIDs;
            account.queueVersus = versus;
            account.queueMode = mode;
            SetAccount(from, account);

            if (bet > 0)
            {
                SpendBet(from, bet, mode, from);
            }

            switch (mode)
            {
                case BattleMode.Versus:
                    {                       
                        var otherAccount = GetAccount(versus);
                        if (otherAccount.queueMode == BattleMode.Versus && otherAccount.queueVersus == from)
                        {
                            PrepareMatch(versus, from);
                            return;
                        }
                        else
                        if (otherAccount.queueMode == BattleMode.None && otherAccount.battleID == 0)
                        {
                            //var challengerList = Storage.FindCollectionForAddress<NachoVersusInfo>(ACCOUNT_CHALLENGES, versus);
                            var challengerList = _playerVersusChallengesList.Get<Address, StorageList>(versus);
                            var entry = new NachoVersusInfo()
                            {
                                bet = bet,
                                challenger = from,
                                time = GetCurrentTime(),
                                levels = levels,
                            };

                            challengerList.Add(entry);
                        }
                        else
                        {
                            // cancel this match because the opponent is already busy
                            RemoveAccountFromQueue(from);
                            return;
                        }
                        
                        break;
                    }

                case BattleMode.Pratice:
                    {
                        // against bots the battle can start instantly...

                        Runtime.Expect(praticeLevel < PraticeLevel.None, "invalid bot ID");

                        if (praticeLevel >= PraticeLevel.Diamond)
                        {
                            var maxPraticeLevelAllowed = (PraticeLevel)(1 + (int)wrestlers[0].praticeLevel);
                            if (maxPraticeLevelAllowed < PraticeLevel.Diamond)
                            {
                                maxPraticeLevelAllowed = PraticeLevel.Diamond;
                            }

                            Runtime.Expect(praticeLevel >= maxPraticeLevelAllowed, "locked bot");
                        }

                        StartBotMatch(from, (int)praticeLevel);

                        return;
                    }

                default:
                    {
                        if (Rules.IsModeWithMatchMaker(mode))
                        {
                            InsertIntoMatchMaker(from);

                            if (MatchMakerFindMatch(from))
                            {
                                return;
                            }
                            else
                            {
                                if (mode == BattleMode.Ranked && GetMatchMakerCount(BattleMode.Ranked) == 1)
                                {
                                    string secondPhrase;
                                    switch (Environment.TickCount % 6)
                                    {
                                        case 1:
                                            secondPhrase = "Is someone up to the challenge?";
                                            break;

                                        case 2:
                                            secondPhrase = "Does someone dare to beat him up?";
                                            break;

                                        case 3:
                                            secondPhrase = "Anyone crazy enough to enter the ring?";
                                            break;

                                        case 4:
                                            secondPhrase = "Amigos, amigos, join in!";
                                            break;

                                        default:
                                            secondPhrase = "Anyone wants to join?";
                                            break;
                                    }

                                    singleEvent?.Invoke(from, "{0} joined the Ranked queue. " + secondPhrase);
                                }
                            }
                        }
                        
                        break;
                    }
            }
        }

        private void PrepareMatch(Address addressA, Address addressB)
        {
            // cant battle against itself
            Runtime.Expect(addressA != addressB, "same address failed");

            var accountA = GetAccount(addressA);
            var accountB = GetAccount(addressB);

            Runtime.Expect(accountA.queueMode == accountB.queueMode, "modes not match");

            var mode = accountA.queueMode;

            if (Rules.IsModeWithMatchMaker(mode))
            {
                RemoveFromMatchMaker(addressA);
                RemoveFromMatchMaker(addressB);
            }

            DeleteChallengers(addressA);
            DeleteChallengers(addressB);

            // get last battle ID, increment and update
            //var battles = Storage.FindMapForContract<BigInteger, NachoBattle>(GLOBAL_BATTLE_LIST);

            //var battle_id = battles.Count() + 1;
            var battle_id = _battles.Count() + 1;

            if (mode == BattleMode.Ranked)
            {
                pairEvent?.Invoke(addressA, addressB, "A new ranked match started: {0} vs {1}.\nTo spectate insert battle ID " + battle_id + " inside Nacho Men battle menu.");
            }

            var stateA = new LuchadorBattleState[accountA.queueWrestlerIDs.Length];
            var stateB = new LuchadorBattleState[accountB.queueWrestlerIDs.Length];

            var sides = new BattleSide[2];

            for (var i = 0; i < stateA.Length; i++)
            {
                var wrestler = GetWrestler(accountA.queueWrestlerIDs[i]);

                //var itemKind = Formulas.GetItemKind(wrestler.itemID);
                var itemKind = wrestler.itemID > 0 ? GetItem(wrestler.itemID).kind : ItemKind.None;

                var level = Formulas.CalculateWrestlerLevel((int)wrestler.experience);
                var genes = wrestler.genes;
                var base_stamina = Formulas.CalculateBaseStat(genes, StatKind.Stamina);

                stateA[i] = new LuchadorBattleState()
                {
                    wrestlerID = accountA.queueWrestlerIDs[i],
                    boostAtk = 100,
                    boostDef = 100,
                    status = BattleStatus.None,
                    itemKind = itemKind,
                    lastMove = WrestlingMove.Idle,
                    disabledMove = WrestlingMove.Unknown,
                    riggedMove = WrestlingMove.Unknown,
                    learnedMove = WrestlingMove.Unknown,
                    stance = (itemKind == ItemKind.Ignition_Chip ? BattleStance.Alternative : BattleStance.Main),
                    currentStamina = Formulas.CalculateWrestlerStat(level, base_stamina, wrestler.gymBoostStamina)
                };
            }

            for (var i = 0; i < stateB.Length; i++)
            {
                var wrestler = GetWrestler(accountB.queueWrestlerIDs[i]);

                //var itemKind = Formulas.GetItemKind(wrestler.itemID);
                var itemKind = wrestler.itemID > 0 ? GetItem(wrestler.itemID).kind : ItemKind.None;

                var level = Formulas.CalculateWrestlerLevel((int)wrestler.experience);
                var genes = wrestler.genes;
                var base_stamina = Formulas.CalculateBaseStat(genes, StatKind.Stamina);

                stateB[i] = new LuchadorBattleState()
                {
                    wrestlerID = accountB.queueWrestlerIDs[i],
                    boostAtk = 100,
                    boostDef = 100,
                    status = BattleStatus.None,
                    itemKind = itemKind,
                    lastMove = WrestlingMove.Idle,
                    disabledMove = WrestlingMove.Unknown,
                    riggedMove = WrestlingMove.Unknown,
                    learnedMove = WrestlingMove.Unknown,
                    stance = (itemKind == ItemKind.Ignition_Chip ? BattleStance.Alternative : BattleStance.Main),
                    currentStamina = Formulas.CalculateWrestlerStat(level, base_stamina, wrestler.gymBoostStamina)
                };
            }

            sides[0] = new BattleSide()
            {
                address = addressA,
                wrestlers = stateA,
                move = WrestlingMove.Idle,
                auto = (mode == BattleMode.Pratice),
            };

            sides[1] = new BattleSide()
            {
                address = addressB,
                wrestlers = stateB,
                move = WrestlingMove.Idle,
                auto = false,
            };

            if (sides[0].wrestlers[0].itemKind == ItemKind.Yo_Yo || sides[1].wrestlers[0].itemKind == ItemKind.Yo_Yo)
            {
                int index = (sides[0].wrestlers[0].itemKind == ItemKind.Yo_Yo) ? 0 : 1;
                var other = 1 - index;

                if (sides[other].wrestlers[0].itemKind != ItemKind.Nullifier)
                {
                    var temp = sides[0].wrestlers[0].itemKind;
                    sides[0].wrestlers[0].itemKind = sides[1].wrestlers[0].itemKind;
                    sides[1].wrestlers[0].itemKind = temp;

                    for (int i = 0; i < 2; i++)
                    {
                        if (sides[i].wrestlers[0].itemKind == ItemKind.Yo_Yo)
                        {
                            Runtime.Notify(NachoEvent.ItemActivated, sides[i].address, ItemKind.Yo_Yo);
                        }
                    }
                }
            }

            // TODO this works only in 1 vs 1 matches
            for (int i = 0; i < 2; i++)
            {
                var other = 1 - i;
                if (sides[i].wrestlers[0].itemKind == ItemKind.Ignition_Chip && sides[other].wrestlers[0].itemKind == ItemKind.Nullifier)
                {
                    sides[i].wrestlers[0].stance = BattleStance.Main;
                }
            }

            accountA.lastOpponent = addressB;
            accountB.lastOpponent = addressA;
            accountA.queueMode = BattleMode.None;
            accountB.queueMode = BattleMode.None;

            SetAccount(addressA, accountA);
            SetAccount(addressB, accountB);

            // equalize bets
            BigInteger bet;

            if (accountA.queueBet != accountB.queueBet)
            {
                Address refundAddress;
                BigInteger refundAmount;    //refund amount should be negative!

                if (accountA.queueBet > accountB.queueBet)
                {
                    refundAddress = addressA;
                    refundAmount = accountA.queueBet - accountB.queueBet;
                    bet = accountB.queueBet;
                }
                else
                {
                    refundAddress = addressB;
                    refundAmount = accountB.queueBet - accountA.queueBet;
                    bet = accountA.queueBet;
                }

                //Runtime.Expect(UpdateAccountBalance(refundAddress, refundAmount), "refund failed");
                Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Constants.NACHO_SYMBOL, Runtime.Chain.Address, refundAddress, refundAmount), "refund failed");
            }
            else
            {
                bet = accountA.queueBet;
            }

            // store info for the battle
            var battle = new NachoBattle()
            {
                sides = sides,
                version = CurrentBattleVersion,
                mode = mode,
                turn = 1,
                bet = bet,
                lastTurnHash = Hash.Null,
                state = BattleState.Active,
                time = GetCurrentTime(),
                counters = new BigInteger[Constants.BATTLE_COUNTER_MAX]
            };

            //battle.counters[Constants.BATTLE_COUNTER_START_TIME] = (int)GetCurrentTime();
            battle.counters[Constants.BATTLE_COUNTER_START_TIME] = (int)GetCurrentTime().Value;

            // save battle info
            //battles.Set(battle_id, battle);
            _battles.Set(battle_id, battle);

            SetAccountBattle(addressA, battle_id, battle, 0);
            SetAccountBattle(addressB, battle_id, battle, 1);
        }

            
        public void UpdateQueue(Address from)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            var account = GetAccount(from);

            Runtime.Expect(account.queueMode != BattleMode.None, "not in queue");

            Runtime.Expect(account.queueWrestlerIDs.Length == 1, "invalid wrester count"); // TODO tag team support

            var updateDiff = Runtime.Time.Value - account.queueUpdateTime;
            Runtime.Expect(updateDiff >= Constants.MATCHMAKER_UPDATE_SECONDS, "too soon");

            if (Rules.IsModeWithMatchMaker(account.queueMode) && MatchMakerFindMatch(from))
            {
                return;
            }

            account.queueUpdateTime = GetCurrentTime();
            SetAccount(from, account);

            var joinDiff = Runtime.Time.Value - account.queueJoinTime;
            if (joinDiff >= Constants.QUEUE_FORCE_BOT_SECONDS && account.queueMode == BattleMode.Unranked)
            {
                int minID, maxID;

                var wrestlerID = account.queueWrestlerIDs[0];
                var wrestler = GetWrestler(wrestlerID);
                var level = Formulas.CalculateWrestlerLevel((int)wrestler.experience);

                switch (level)
                {
                    case 1: minID = 9; maxID = 12; break;
                    case 2: minID = 11; maxID = 14; break;
                    case 3: minID = 12; maxID = 18; break;
                    case 4: minID = 17; maxID = 21; break;
                    case 5: minID = 20; maxID = 24; break;
                    case 6: minID = 23; maxID = 27; break;
                    case 7: minID = 26; maxID = 32; break;
                    case 8: minID = 29; maxID = 33; break;
                    case 9: minID = 31; maxID = 36; break;
                    case 10: minID = 35; maxID = 38; break;
                    case 11: minID = 36; maxID = 40; break;
                    case 12: minID = 39; maxID = 43; break;
                    case 13: minID = 42; maxID = 46; break;
                    case 14: minID = 45; maxID = 49; break;
                    case 15: minID = 48; maxID = 53; break;
                    default: minID = 53; maxID = 62; break;
                }

                account.queueMode = BattleMode.Pratice;
                SetAccount(from, account);

                int botID = (int)(minID + Runtime.Time.Value % (1 + maxID - minID));
                StartBotMatch(from, -botID);
                return;
            }
        }

        public void CancelQueue(Address from)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            var account = GetAccount(from);
            Runtime.Expect(account.queueMode != BattleMode.None, "not in queue");

            RemoveAccountFromQueue(from);
        }
        
        private void RemoveAccountFromQueue(Address from)
        {
            var account = GetAccount(from);

            if (account.queueMode != BattleMode.None)
            {
                var mode = account.queueMode;
                var bet = account.queueBet;

                account.queueMode = BattleMode.None;
                account.queueBet = 0;
                SetAccount(from, account);

                if (bet > 0)
                {
                    //Runtime.Expect(UpdateAccountBalance(from, bet), "refund failed");
                    Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Constants.NACHO_SYMBOL, Runtime.Chain.Address, from, bet), "refund failed");
                }
            }

            RemoveFromMatchMaker(from);
        }
        
        #endregion

        #region BATTLE API
        
        public NachoBattle GetBattle(BigInteger battleID)
        {
            //var battles = Storage.FindMapForContract<BigInteger, NachoBattle>(GLOBAL_BATTLE_LIST);

            //var battle = battles.Get(battleID);
            var battle = _battles.Get<BigInteger, NachoBattle>(battleID);

            // this allows us to add new counters later while keeping binary compatibility
            if (battle.counters == null)
            {
                battle.counters = new BigInteger[Constants.BATTLE_COUNTER_MAX];
            }
            else
            if (battle.counters.Length < Constants.BATTLE_COUNTER_MAX)
            {
                var temp = new BigInteger[Constants.BATTLE_COUNTER_MAX];
                for (int i = 0; i < battle.counters.Length; i++)
                {
                    temp[i] = battle.counters[i];
                }

                battle.counters = temp;
            }

            if (IsBattleBroken(battle))
            {
                battle.state = BattleState.Cancelled;
                SetBattle(battleID, battle);
            }

            return battle;
        }

        private void SetBattle(BigInteger battleID, NachoBattle battle)
        {
            //var battles = Storage.FindMapForContract<BigInteger, NachoBattle>(GLOBAL_BATTLE_LIST);
            //battles.Set(battleID, battle);

            _battles.Set(battleID, battle);
        }

        // this is the calculate damage if the move hits, ignoring the move of the opponent, which is taken into account in CalculateMoveResult()
        private int CalculateMoveDamage(NachoBattle battle, WrestlerTurnInfo attacker, WrestlerTurnInfo defender)
        {
            int rnd = attacker.item == ItemKind.Expert_Gloves ? 15 : (int)(attacker.seed % 16);

            switch (attacker.move)
            {
                case WrestlingMove.Smash:
                    {
                        var power = Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_SMASH_POWER);
                        if (attacker.item == ItemKind.Wood_Chair && defender.item != ItemKind.Wood_Potato)
                        {
                            power = (power * Constants.DAMAGE_CHAIR_PERCENTAGE) / 100;
                        }
                        return power;
                    }

                case WrestlingMove.Chop:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_CHOP_POWER);

                case WrestlingMove.Avalanche:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_AVALANCHE_POWER);

                case WrestlingMove.Corkscrew:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_CORKSCREW_POWER);

                case WrestlingMove.Mega_Chop:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_MEGA_CHOP_POWER);

                case WrestlingMove.Knock_Off:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_KNOCK_OFF_POWER);

                case WrestlingMove.Armlock:
                    {
                        int power;
                        if (defender.move == WrestlingMove.Idle || defender.move == WrestlingMove.Counter || Rules.IsTertiaryMove(defender.move))
                        {
                            power = Constants.DAMAGE_ARMLOCK_MAX_POWER;
                        }
                        else
                        {
                            power = Constants.DAMAGE_ARMLOCK_NORMAL_POWER;
                        }

                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, power);
                    }

                case WrestlingMove.Spinning_Crane:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_SPINNING_CRANE_POWER);

                case WrestlingMove.Butterfly_Kick:
                    {
                        int baseDamage;
                        if (battle.turn % 2 == 0)
                        {
                            baseDamage = Constants.DAMAGE_BAD_CHOP_POWER;
                        }
                        else
                        {
                            baseDamage = Constants.DAMAGE_GOOD_CHOP_POWER;
                        }

                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, baseDamage);
                    }

                case WrestlingMove.Moth_Drill:
                    {
                        int baseDamage;
                        if (battle.turn % 2 == 0)
                        {
                            baseDamage = Constants.DAMAGE_GOOD_CHOP_POWER;
                        }
                        else
                        {
                            baseDamage = Constants.DAMAGE_BAD_CHOP_POWER;
                        }

                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, baseDamage);
                    }

                case WrestlingMove.Gutbuster:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_GUTBUSTER_POWER);

                case WrestlingMove.Takedown:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_TAKEDOWN_POWER);

                case WrestlingMove.Fart:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_FART_POWER);

                case WrestlingMove.Needle_Sting:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_NEEDLE_STING_POWER);

                case WrestlingMove.Leg_Twister:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_LEG_TWISTER_POWER);

                case WrestlingMove.Wolf_Claw:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_WOLF_CLAW_POWER);

                case WrestlingMove.Flying_Kick:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_FLYING_KICK_POWER);

                case WrestlingMove.Octopus_Arm:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_OCTOPUS_ARM_POWER);

                case WrestlingMove.Iron_Fist:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_IRON_FIST_POWER);

                case WrestlingMove.Chomp:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_CHOMP_POWER);

                case WrestlingMove.Burning_Tart:
                case WrestlingMove.Tart_Throw:
                case WrestlingMove.Tart_Splash:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_TART_THROW_POWER);

                case WrestlingMove.Fire_Breath:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_FIRE_BREATH_POWER);

                case WrestlingMove.Flame_Fang:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_FIRE_FANG_POWER);

                case WrestlingMove.Headspin:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_HEAD_SPIN_POWER);

                case WrestlingMove.Palm_Strike:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.initialDef, rnd, Constants.DAMAGE_PALM_STRIKE_POWER);

                case WrestlingMove.Sick_Sock:
                    {
                        var damage = Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_SICK_SOCK_POWER);
                        var extra = Formulas.CountNumberOfStatus(attacker.status);

                        damage += (damage * Constants.SICK_SOCK_DAMAGE_PERCENT * extra) / 100;
                        return damage;
                    }

                case WrestlingMove.Psy_Strike:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.initialDef, rnd, Constants.DAMAGE_PSY_STRIKE_POWER);

                case WrestlingMove.Bizarre_Ball:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, Formulas.BaseStatSplit, rnd, Constants.DAMAGE_BIZARRE_BALL_POWER);

                case WrestlingMove.Torpedo:
                    {
                        int add = (int)attacker.chance / 4;

                        if (attacker.status.HasFlag(BattleStatus.Drunk))
                        {
                            add += 10;
                        }

                        if (add > 25)
                        {
                            add = 25;
                        }

                        var basePower = 7 + add;
                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, basePower);
                    }

                case WrestlingMove.Pain_Release:
                    {
                        // calculate the original damage without reduction
                        var power = (int)attacker.lastDamage + (((int)attacker.lastDamage * Constants.ZEN_CHARGE_PERCENTAGE) / 100);

                        // augmentate the damage
                        power = (power * Constants.ZEN_RELEASE_PERCENTAGE) / 100;
                        return power;
                    }

                case WrestlingMove.Rage_Punch:
                    {
                        var power = Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_RAGE_PUNCH_POWER);
                        return power;
                    }

                case WrestlingMove.Ultra_Punch:
                    {
                        var power = Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_RAGE_PUNCH_POWER);
                        return power * 2;
                    }

                case WrestlingMove.Boomerang:
                    //Deadly damage when near KO. Weak otherwise.
                    {
                        var percent = (attacker.currentStamina * 100) / attacker.maxStamina;
                        percent = 100 - percent;

                        var diff = Constants.DAMAGE_BOOMERANG_MAX_POWER - Constants.DAMAGE_BOOMERANG_NORMAL_POWER;
                        var power = Constants.DAMAGE_BOOMERANG_NORMAL_POWER;
                        power += (int)(diff * percent) / 100;

                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, power);
                    }

                case WrestlingMove.Bash:
                    // Stronger damage when the users stamina is higher.
                    {
                        var percent = (attacker.currentStamina * 100) / attacker.maxStamina;

                        var diff = Constants.DAMAGE_BASH_MAX_POWER - Constants.DAMAGE_BASH_NORMAL_POWER;
                        var power = Constants.DAMAGE_BASH_NORMAL_POWER;
                        power += (int)(diff * percent) / 100;

                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, power);
                    }

                case WrestlingMove.Flailing_Arms:
                    {
                        int power;

                        if (attacker.status.HasFlag(BattleStatus.Confused))
                        {
                            power = Constants.DAMAGE_FLAILING_ARMS_POWER_STRONG;
                        }
                        else
                        {
                            power = Constants.DAMAGE_FLAILING_ARMS_POWER_WEAK;
                        }

                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, power);
                    }

                case WrestlingMove.Rhino_Rush:
                    {
                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_RHINO_CHARGE_POWER);
                    }

                case WrestlingMove.Hyper_Slam:
                    {
                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_HYPER_SLAM);
                    }

                case WrestlingMove.Gorilla_Cannon:
                    {
                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_GORILLA_CANNON_POWER);
                    }

                case WrestlingMove.Mind_Slash:
                    {
                        if (attacker.status != BattleStatus.None)
                        {
                            return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_MIND_SLASH);
                        }
                        else
                        {
                            return 0;
                        }
                    }

                case WrestlingMove.Drunken_Fist:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_DRUKEN_FIST_POWER);

                case WrestlingMove.Razor_Jab:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_RAZOR_JAB_POWER);

                case WrestlingMove.Hammerhead:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_HAMMERHEAD_POWER);

                case WrestlingMove.Knee_Bomb:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_KNEE_BOMB_POWER);

                case WrestlingMove.Chicken_Wing:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_CHICKEN_WING_POWER);

                case WrestlingMove.Smile_Crush:
                    {
                        var atkPercentage = (attacker.currentStamina * 100) / attacker.maxStamina;
                        var defPercentage = (defender.currentStamina * 100) / defender.maxStamina;

                        if (atkPercentage < defPercentage)
                        {
                            var diff = defPercentage - atkPercentage;
                            var power = (int)(Constants.DAMAGE_SMILE_CRUSH_POWER * diff) / 100;
                            if (power < 2)
                            {
                                power = 2;
                            }

                            if (defender.status.HasFlag(BattleStatus.Smiling))
                            {
                                power *= 2;
                            }

                            return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, power);
                        }
                        else
                            return 0;
                    }

                case WrestlingMove.Heart_Punch:
                    {
                        var power = Constants.DAMAGE_HEART_PUNCH_NORMAL_POWER;

                        if (defender.status.HasFlag(BattleStatus.Bleeding))
                        {
                            power = Constants.DAMAGE_HEART_PUNCH_MAX_POWER;
                        }

                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, power);
                    }

                case WrestlingMove.Back_Slap:
                    {
                        var power = Constants.DAMAGE_BACK_SLAP_NORMAL_POWER;

                        if (defender.status.HasFlag(BattleStatus.Burned))
                        {
                            power = Constants.DAMAGE_BACK_SLAP_MAX_POWER;
                        }

                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, power);
                    }

                case WrestlingMove.Rehab:
                    {
                        var power = Constants.DAMAGE_REHAB_NORMAL_POWER;

                        if (defender.status.HasFlag(BattleStatus.Poisoned))
                        {
                            power = Constants.DAMAGE_REHAB_MAX_POWER;
                        }

                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, power);
                    }

                case WrestlingMove.Monkey_Slap:
                    {
                        int power;

                        if (defender.stance != attacker.stance)
                        {
                            power = Constants.DAMAGE_BAD_CHOP_POWER;
                        }
                        else
                        {
                            power = Constants.DAMAGE_GOOD_CHOP_POWER;
                        }

                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, power);
                    }

                case WrestlingMove.Side_Hook:
                    return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, Constants.DAMAGE_SIDE_HOOK_POWER);

                case WrestlingMove.Slingshot:
                    if (attacker.item != ItemKind.None)
                    {
                        int baseDamage;

                        switch (attacker.item)
                        {
                            case ItemKind.Bomb: baseDamage = Constants.DAMAGE_SLINGSHOT_MAX; break;

                            case ItemKind.Wrist_Weights:
                            case ItemKind.Dumbell:
                            case ItemKind.Bling:
                                baseDamage = Constants.DAMAGE_SLINGSHOT_HIGH; break;

                            case ItemKind.XP_Perfume:
                            case ItemKind.Love_Lotion:
                            case ItemKind.Nails:
                            case ItemKind.Muscle_Overclocker:
                            case ItemKind.Fork:
                            case ItemKind.Chilli_Bottle:
                            case ItemKind.Tequilla:
                            case ItemKind.Spy_Specs:
                            case ItemKind.Magnifying_Glass:
                            case ItemKind.Vampire_Teeth:
                            case ItemKind.Claws:
                            case ItemKind.Gyroscope:
                                baseDamage = Constants.DAMAGE_SLINGSHOT_LOW; break;

                            default: baseDamage = Constants.DAMAGE_SLINGSHOT_MIN; break;
                        }

                        return Formulas.CalculateDamage((int)attacker.level, (int)attacker.currentAtk, (int)defender.currentDef, rnd, baseDamage);
                    }
                    else
                        return 0;

                default: return 0;
            }
        }

        private int CalculateMoveResult(WrestlerTurnInfo attacker, WrestlerTurnInfo defender, BigInteger seed)
        {
            int damage;

            if (Rules.IsCounter(defender.move, defender.item, attacker.move, attacker.item))
            {
                var percentage = defender.item == ItemKind.Gyroscope ? 100 : 150;
                damage = (int)(defender.power * percentage) / 100;
            }
            else
            if (Rules.IsCounter(attacker.move, attacker.item, defender.move, defender.item))
            {
                damage = 0;
            }
            else
                switch (attacker.move)
                {
                    case WrestlingMove.Block:
                        if (defender.power > 0)
                        {
                            var power = (int)defender.power / 2;
                            if (power < 1)
                            {
                                power = 1;
                            }

                            damage = power;
                        }
                        else
                        {
                            damage = 0;
                        }
                        break;

                    case WrestlingMove.Smash:
                    case WrestlingMove.Chop:
                    case WrestlingMove.Boomerang:
                    case WrestlingMove.Avalanche:
                    case WrestlingMove.Corkscrew:
                    case WrestlingMove.Butterfly_Kick:
                    case WrestlingMove.Moth_Drill:
                    case WrestlingMove.Fart:
                    case WrestlingMove.Needle_Sting:
                    case WrestlingMove.Bash:
                    case WrestlingMove.Rhino_Rush:
                    case WrestlingMove.Hyper_Slam:
                    case WrestlingMove.Razor_Jab:
                    case WrestlingMove.Drunken_Fist:
                    case WrestlingMove.Hammerhead:
                    case WrestlingMove.Gorilla_Cannon:
                    case WrestlingMove.Knock_Off:
                    case WrestlingMove.Chicken_Wing:
                    case WrestlingMove.Headspin:
                    case WrestlingMove.Wolf_Claw:
                    case WrestlingMove.Monkey_Slap:
                    case WrestlingMove.Iron_Fist:
                    case WrestlingMove.Leg_Twister:
                    case WrestlingMove.Heart_Punch:
                    case WrestlingMove.Back_Slap:
                    case WrestlingMove.Rehab:
                    case WrestlingMove.Smile_Crush:
                    case WrestlingMove.Slingshot:
                    case WrestlingMove.Spinning_Crane:
                    case WrestlingMove.Octopus_Arm:
                    case WrestlingMove.Rage_Punch:
                    case WrestlingMove.Ultra_Punch:
                    case WrestlingMove.Flying_Kick:
                    case WrestlingMove.Tart_Throw:
                    case WrestlingMove.Torpedo:
                    case WrestlingMove.Palm_Strike:
                    case WrestlingMove.Mega_Chop:
                    case WrestlingMove.Fire_Breath:
                    case WrestlingMove.Flame_Fang:
                    case WrestlingMove.Bizarre_Ball:
                    case WrestlingMove.Flailing_Arms:
                    case WrestlingMove.Pain_Release:
                    case WrestlingMove.Takedown:
                    case WrestlingMove.Knee_Bomb:
                    case WrestlingMove.Chomp:
                    case WrestlingMove.Burning_Tart:
                    case WrestlingMove.Armlock:
                    case WrestlingMove.Sick_Sock:
                    case WrestlingMove.Psy_Strike:
                    case WrestlingMove.Mind_Slash:
                        damage = (int)attacker.power;
                        break;

                    case WrestlingMove.Gutbuster:
                        damage = (defender.move == defender.lastMove) ? (int)attacker.power : 0;
                        break;

                    case WrestlingMove.Side_Hook:
                        damage = (Rules.IsStanceMove(defender.move)) ? (int)attacker.power : 0;
                        break;

                    default:
                        damage = 0;
                        break;
                }

            switch (defender.move)
            {
                case WrestlingMove.Zen_Point:
                    if (damage > 1)
                    {
                        var dmgReduction = (damage * Constants.ZEN_CHARGE_PERCENTAGE) / 100;
                        if (dmgReduction < 1) dmgReduction = 1;
                        damage -= dmgReduction;
                    }
                    break;

                case WrestlingMove.Block:
                    {
                        if (damage > 0)
                        {
                            damage = damage / 2;
                            if (damage < 1)
                            {
                                damage = 1;
                            }
                        }

                        break;
                    }

                case WrestlingMove.Dodge:
                    {
                        if (attacker.move != WrestlingMove.Chop && attacker.move != WrestlingMove.Mega_Chop)
                        {
                            damage = 0;
                        }
                        break;
                    }
            }

            return damage;
        }

        private WrestlerTurnInfo CalculateTurnInfo(BattleSide side, NachoWrestler wrestler, BigInteger wrestlerID, WrestlingMove move, WrestlingMove lastMove, LuchadorBattleState state, BigInteger seed)
        {
            var level = Formulas.CalculateWrestlerLevel((int)wrestler.experience);

            var genes = wrestler.genes;

            var base_atk = Formulas.CalculateBaseStat(genes, StatKind.Attack);

            var initialAtk = Formulas.CalculateWrestlerStat(level, base_atk, wrestler.gymBoostAtk);
            var currentAtk = (initialAtk * state.boostAtk) / 100;

            if (currentAtk < 1)
            {
                currentAtk = 1;
            }

            var base_def = Formulas.CalculateBaseStat(genes, StatKind.Defense);

            var initialDef = Formulas.CalculateWrestlerStat(level, base_def, wrestler.gymBoostDef);
            var currentDef = (initialDef * state.boostDef) / 100;

            if (currentDef < 1)
            {
                currentDef = 1;
            }

            int chance = (int)(seed % 100);

            if (state.itemKind == ItemKind.Lucky_Charm)
            {
                chance += 25;
            }

            var base_stamina = Formulas.CalculateBaseStat(genes, StatKind.Stamina);
            var maxStamina = Formulas.CalculateWrestlerStat(level, base_stamina, wrestler.gymBoostStamina);

            var nft = Runtime.Nexus.GetNFT(Constants.WRESTLER_SYMBOL, wrestlerID);

            var info = new WrestlerTurnInfo()
            {
                address = nft.CurrentOwner,
                level = level,
                seed = seed,
                initialAtk = initialAtk,
                initialDef = initialDef,
                maxStamina = maxStamina,
                currentStamina = state.currentStamina,
                currentAtk = currentAtk,
                currentDef = currentDef,
                move = move,
                lastMove = lastMove,
                item = state.itemKind,
                status = state.status,
                stance = state.stance,
                lastStance = state.lastStance,
                lastDamage = side.previousDirectDamage,
                chance = chance,
                itemActivated = false
            };
            return info;
        }

        public bool CancelMatch(Address from, BigInteger battleID)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            var battle = GetBattle(battleID);

            Runtime.Expect(battle.state == BattleState.Active, "battle failed");

            var timeDiff = (GetCurrentTime() - battle.time) / 60;
            bool timeOut = timeDiff > Constants.MINIMUM_MINUTES_FOR_CANCEL;

            int localIndex = -1;

            for (int i = 0; i < 2; i++)
            {
                if (battle.sides[i].address == from)
                {
                    localIndex = i;
                    break;
                }
            }

            Runtime.Expect(localIndex != -1, "participant failed");

            int opponentIndex = 1 - localIndex;

            if (battle.sides[localIndex].turn >= battle.sides[opponentIndex].turn || !timeOut)
            {
                throw new ContractException("not time yet");
            }

            TerminateMatchWithResult(battleID, battle, BattleState.Cancelled);
            return true;
        }
        
        private void InitPot()
        {
            //Runtime.Expect(IsWitness(DevelopersAddress), "developer only");
            Runtime.Expect(!Storage.Has("pot"), "pot already created");

            //Runtime.Expect(DepositNEP5(DevelopersAddress, amount), "deposit failed");

            _pot = new NachoPot()
            {
                currentBalance = 0,
                claimed = false,
                lastBalance = 0,
                entries = new NachoPotEntry[0],
                lastWinners = new Address[0],
                timestamp = GetCurrentTime(),
            };
        }

        public NachoPot GetPot()
        {
            if (_pot.timestamp == 0)
            {
                InitPot();
            }

            return _pot;
        }

        private void ClosePot()
        {
            var diff = GetCurrentTime() - _pot.timestamp;
            if (diff < Constants.SECONDS_PER_DAY)
            {
                return;
            }

            if (_pot.claimed)
            {
                _pot.lastBalance = 0;
            }

            var winnerCount = _pot.entries.Length;

            if (winnerCount < 3) // not enough daily participants
            {
                winnerCount = 0;
            }
            else
            if (winnerCount > Constants.POT_PERCENTAGES.Length)
            {
                winnerCount = Constants.POT_PERCENTAGES.Length;
            }

            var winners = new Address[winnerCount];
            for (int i = 0; i < winners.Length; i++)
            {
                winners[i] = _pot.entries[i].address;
            }

            var minimumPotSize = UnitConversion.ToBigInteger(Constants.MINIMUM_POT_SIZE, Nexus.StakingTokenDecimals);

            // only close pot if enough winners and minimum amount of SOUL reached, otherwise accumulate to next day
            if (winners.Length > 0 && _pot.currentBalance >= minimumPotSize)
            {
                _pot.lastBalance += _pot.currentBalance;
                _pot.currentBalance = 0;
                _pot.entries = new NachoPotEntry[0];
                _pot.lastWinners = winners;

                AddTrophy(winners[0], TrophyFlag.Pot);
            }

            _pot.claimed = false;
            _pot.timestamp = GetCurrentTime();
        }

        private void AddToPot(Address address, BigInteger amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (_pot.timestamp == 0)
            {
                InitPot();
            }

            ClosePot();

            _pot.currentBalance += amount;

            if (address != Address.Null)
            {
                var list = _pot.entries.ToList();

                int index = -1;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i].address == address)
                    {
                        index = i;
                        break;
                    }
                }

                if (index == -1)
                {
                    list.Add(new NachoPotEntry() { address = address, wins = 1, timestamp = GetCurrentTime() });
                }
                else
                {
                    var temp = list[index];
                    temp.wins++;
                    temp.timestamp = GetCurrentTime();
                    list[index] = temp;
                }

                _pot.entries = list.OrderByDescending(x => x.wins).ThenBy(x => x.timestamp).ToArray();
            }
        }

        public void DistributePot(Address from)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            if (_pot.timestamp == 0)
            {
                InitPot();
            }

            Runtime.Expect(_pot.lastBalance > 0, "already claimed");

            ClosePot();

            Runtime.Expect(!_pot.claimed, "already claimed");

            BigInteger distributedTotal = 0;

            for (int i = 0; i < _pot.lastWinners.Length; i++)
            {
                var target = _pot.lastWinners[i];
                var amount = (_pot.lastBalance * Constants.POT_PERCENTAGES[i]) / 100;
                distributedTotal += amount;

                var account = GetAccount(target);
                account.counters[Constants.ACCOUNT_COUNTER_POT_COUNT]++;
                SetAccount(target, account);

                //Runtime.Expect(UpdateAccountBalance(target, amount), "deposit failed");
                Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Constants.NACHO_SYMBOL, Runtime.Chain.Address, target, amount), "deposit failed");
                //Runtime.Notify(NachoEvent.Deposit, target, amount);
                Runtime.Notify(EventKind.TokenReceive, target, amount);
            }

            var leftovers = _pot.lastBalance - distributedTotal;
            Runtime.Expect(leftovers >= 0, "leftovers failed");
            _pot.currentBalance += leftovers;

            _pot.claimed = true;

            //Runtime.Notify(EventKind.Pot, from, ???);
        }

        private void TerminateMatchWithResult(BigInteger battleID, NachoBattle battle, BattleState result)
        {
            var state_A = battle.sides[0].wrestlers[(uint)battle.sides[0].current];
            var wrestler_A = GetWrestler(state_A.wrestlerID);

            var state_B = battle.sides[1].wrestlers[(uint)battle.sides[1].current];
            var wrestler_B = GetWrestler(state_B.wrestlerID);

            var level_A = Formulas.CalculateWrestlerLevel((int)wrestler_A.experience);
            var level_B = Formulas.CalculateWrestlerLevel((int)wrestler_B.experience);

            // TODO ver código repetido

            var avgLevel = (level_A + level_B) / 2;

            battle.state = result;
            TerminateMatch(battleID, battle, state_A, state_B, avgLevel);
        }

        private void TerminateMatch(BigInteger battleID, NachoBattle battle, LuchadorBattleState state_A, LuchadorBattleState state_B, int avgLevel)
        {
            if (battle.bet > 0)
            {
                BigInteger winnerAmount = 0;
                BigInteger loserAmount  = 0;
                BigInteger drawAmount   = 0;

                switch (battle.mode)
                {
                    case BattleMode.Academy:
                    case BattleMode.Pratice:
                        // Battles against bots do not give nacho prizes
                        break;
                    case BattleMode.Unranked:
                        winnerAmount    = Constants.UNRANKED_BATTLE_WINNER_PRIZE;
                        loserAmount     = Constants.UNRANKED_BATTLE_LOSER_PRIZE;
                        drawAmount      = Constants.UNRANKED_BATTLE_DRAW_PRIZE;
                        break;
                    case BattleMode.Ranked:
                        winnerAmount    = Constants.RANKED_BATTLE_WINNER_PRIZE;
                        loserAmount     = Constants.RANKED_BATTLE_LOSER_PRIZE;
                        drawAmount      = Constants.RANKED_BATTLE_DRAW_PRIZE;
                        break;
                    case BattleMode.Versus:
                        winnerAmount    = battle.bet * 2;
                        loserAmount     = 0;
                        drawAmount      = battle.bet;
                        break;
                }

                // TODO -> Ranked battle fees do not go to the pot anymore ?
                BigInteger potAmount;
                if (battle.mode == BattleMode.Ranked)
                {
                    potAmount = winnerAmount;// TODO fix (winnerAmount * Constants.POT_FEE_PERCENTAGE) / 100;
                    winnerAmount -= potAmount;
                }
                else
                {
                    potAmount = 0;
                }

                int winnerSide;
                int loserSide;

                switch (battle.state)
                {
                    case BattleState.WinA:
                    case BattleState.ForfeitB:
                        winnerSide  = 0;
                        loserSide   = 1;
                        break;

                    case BattleState.WinB:
                    case BattleState.ForfeitA:
                        winnerSide  = 1;
                        loserSide   = 0;
                        break;

                    default:
                        winnerSide  = -1;
                        loserSide   = -1;
                        break;
                }

                if (winnerSide != -1)
                {
                    // TODO both players now receive prizes. Check the bet spent transfers and notifications and both prizes
                    if (potAmount > 0)
                    {
                        AddToPot(battle.sides[winnerSide].address, potAmount);
                    }

                    //Runtime.Expect(UpdateAccountBalance(battle.sides[winnerSide].address, winnerAmount), "refund failed");
                    Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Constants.NACHO_SYMBOL, Runtime.Chain.Address, battle.sides[winnerSide].address, winnerAmount), "refund failed");
                    //Runtime.Expect(UpdateAccountBalance(battle.sides[loserSide].address, loserAmount), "refund failed");
                    Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Constants.NACHO_SYMBOL, Runtime.Chain.Address, battle.sides[loserSide].address, loserAmount), "refund failed");

                    var other = 1 - winnerSide;

                    //Runtime.Notify(battle.sides[winnerSide].address, NachoEvent.Deposit, winnerAmount);
                    Runtime.Notify(EventKind.TokenReceive, battle.sides[winnerSide].address, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = Constants.NACHO_SYMBOL, value = winnerAmount });
                    Runtime.Notify(EventKind.TokenReceive, battle.sides[other].address, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = Constants.NACHO_SYMBOL, value = loserAmount });

                    // Old spend bet
                    //Runtime.Notify(battle.sides[other].address, NachoEvent.Withdraw, battle.bet);
                    Runtime.Notify(EventKind.TokenSend, battle.sides[other].address, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = Constants.NACHO_SYMBOL, value = battle.bet });
                }
                else
                {
                    // refund both
                    var refundAmount = drawAmount;

                    if (potAmount > 0)
                    {
                        AddToPot(Address.Null, potAmount);
                    }
                    
                    for (var i = 0; i < 2; i++)
                    {
                        //Runtime.Expect(UpdateAccountBalance(battle.sides[i].address, refundAmount), "refund failed");
                        Runtime.Expect(Runtime.Nexus.TransferTokens(Runtime, Constants.NACHO_SYMBOL, Runtime.Chain.Address, battle.sides[i].address, refundAmount), "refund failed");

                        if (potAmount > 0)
                        {
                            //Runtime.Notify(battle.sides[i].address, NachoEvent.Withdraw, potAmount / 2);
                            Runtime.Notify(EventKind.TokenSend, battle.sides[i].address, new TokenEventData() { chainAddress = Runtime.Chain.Address, symbol = Constants.NACHO_SYMBOL, value = potAmount / 2 });
                        }
                    }
                }
            }

            if (battle.mode != BattleMode.Versus)
            {
                int factor = (100 * avgLevel) / 4; // apply a curve
                int receivedXP = Constants.AVERAGE_XP_GAIN * factor;
                receivedXP = receivedXP / 100;
                if (receivedXP < Constants.MINIMUM_XP_PER_BATTLE) receivedXP = Constants.MINIMUM_XP_PER_BATTLE;

                var XP = new int[2];
                var mojo_change = new int[2];

                switch (battle.state)
                {
                    case BattleState.Draw:
                        receivedXP = receivedXP / 2;
                        XP[0] = receivedXP;
                        XP[1] = receivedXP;
                        mojo_change[0] = -1;
                        mojo_change[1] = -1;
                        break;

                    case BattleState.WinA:
                    case BattleState.ForfeitB:
                        XP[0] = receivedXP;
                        XP[1] = receivedXP / 10;
                        mojo_change[0] = 1;
                        mojo_change[1] = -1;
                        break;

                    default:
                        XP[0] = receivedXP / 10;
                        XP[1] = receivedXP;
                        mojo_change[0] = -1;
                        mojo_change[1] = 1;
                        break;
                }

                var nextPraticeLevel = -1;
                bool isDummyBattle = false;

                // NOTE: In pratice mode it is assumed the human player is always B, and the dummy is A
                if (battle.mode == BattleMode.Pratice && state_A.wrestlerID <= Constants.MAX_PRATICE_LEVEL)
                {
                    isDummyBattle = true;
                    XP[0] = 0;
                    mojo_change[0] = 0;

                    if (battle.state == BattleState.WinB)
                    {
                        if (state_A.wrestlerID < Constants.MAX_PRATICE_LEVEL)
                        {
                            nextPraticeLevel = (int)state_A.wrestlerID;
                        }
                        else
                        if (state_A.wrestlerID == Constants.MAX_PRATICE_LEVEL)
                        {
                            AddTrophy(battle.sides[1].address, TrophyFlag.Dummy);
                        }
                    }
                }

                if (Rules.IsModeWithMatchMaker(battle.mode))
                {
                    int winningSide;

                    switch (battle.state)
                    {
                        case BattleState.WinA:
                            winningSide = 0;
                            break;

                        case BattleState.WinB:
                            winningSide = 1;
                            break;

                        default:
                            winningSide = -1;
                            break;
                    }

                    if (winningSide != -1)
                    {
                        var side = battle.sides[winningSide];

                        if (side.wrestlers[(int)side.current].stance == BattleStance.Clown)
                        {
                            AddTrophy(battle.sides[1].address, TrophyFlag.Clown);
                        }

                    }
                }

                var wrestlers = new NachoWrestler[2];
                var currentELOs = new BigInteger[2];

                // TODO FIXME does not support tag team yet
                for (int i = 0; i < 2; i++)
                {
                    var state = battle.sides[i].wrestlers[0];

                    var wrestler = GetWrestler(state.wrestlerID);
                    wrestlers[i] = wrestler;

                    var account = GetAccount(battle.sides[i].address);
                    currentELOs[i] = account.ELO;
                }

                for (int sideIndex = 0; sideIndex < 2; sideIndex++)
                {
                    int maxIndex = battle.sides[sideIndex].wrestlers.Length;

                    for (int wrestlerIndex = 0; wrestlerIndex < 1; wrestlerIndex++)
                    {
                        var state = battle.sides[sideIndex].wrestlers[wrestlerIndex];
                        var wrestler = wrestlers[sideIndex];

                        wrestler.mojoTime = GetCurrentTime();
                        wrestler.location = WrestlerLocation.None;

                        var addXP = XP[sideIndex];

                        if (sideIndex == 1 && battle.mode == BattleMode.Pratice)
                        {
                            // Players earn less XP against bots
                            if (addXP > 0)
                            {
                                addXP = (addXP * Constants.BOT_EXPERIENCE_PERCENT) / 100;
                            }

                            if (addXP < 1)
                            {
                                addXP = 1;
                            }
                        }

                        var other = 1 - sideIndex;

                        if (battle.mode != BattleMode.Versus && (sideIndex == 1 || battle.mode != BattleMode.Pratice))
                        {
                            StatKind boostedStat;
                            byte obtainedEV = 1;

                            if (state.itemKind == ItemKind.Blue_Gear)
                            {
                                boostedStat = StatKind.Stamina;
                            }
                            else
                            if (state.itemKind == ItemKind.Red_Spring)
                            {
                                boostedStat = StatKind.Attack;
                            }
                            else
                            if (state.itemKind == ItemKind.Purple_Cog)
                            {
                                boostedStat = StatKind.Defense;
                            }
                            else
                            {
                                var opponentSign = Formulas.GetHoroscopeSign(wrestlers[other].genes);
                                var statGrid = Constants.horoscopeStats[opponentSign];
                                var statRound = (int)(battleID % 3);

                                switch (statRound)
                                {
                                    case 1:
                                        boostedStat = StatKind.Stamina;
                                        obtainedEV = statGrid[0];
                                        break;

                                    case 2:
                                        boostedStat = StatKind.Attack;
                                        obtainedEV = statGrid[1];
                                        break;

                                    default:
                                        boostedStat = StatKind.Defense;
                                        obtainedEV = statGrid[2];
                                        break;
                                }
                            }

                            IncreaseWrestlerEV(ref wrestler, boostedStat, obtainedEV);
                        }

                        // check if Perfume item is active, if yes, double the gained XP
                        {
                            var diff = GetCurrentTime() - wrestler.perfumeTime;
                            diff /= 3600;
                            if (diff < Constants.XP_PERFUME_DURATION_IN_HOURS)
                            {
                                addXP *= 2;
                            }
                        }

                        if (addXP > 0)
                        {
                            var oldXP = wrestler.experience;

                            if (state.itemKind == ItemKind.Wrist_Weights)
                            {
                                Runtime.Notify(NachoEvent.ItemActivated, battle.sides[sideIndex].address, state.itemKind);
                                addXP *= 2;
                            }

                            wrestler.experience += addXP;

                            if (wrestler.experience > Constants.WRESTLER_MAX_XP)
                            {
                                wrestler.experience = Constants.WRESTLER_MAX_XP;
                            }

                            var diff = (uint)(wrestler.experience - oldXP);
                            if (diff > 0)
                            {
                                Runtime.Notify(NachoEvent.Experience, battle.sides[sideIndex].address, diff);
                            }
                        }

                        wrestler.currentMojo += mojo_change[sideIndex];
                        if (wrestler.currentMojo > wrestler.maxMojo)
                        {
                            wrestler.currentMojo = wrestler.maxMojo;
                        }

                        if (sideIndex == 1 && nextPraticeLevel > 0 && nextPraticeLevel > (int)wrestler.praticeLevel)
                        {
                            wrestler.praticeLevel = (PraticeLevel)nextPraticeLevel;
                            Runtime.Notify(NachoEvent.Unlock, battle.sides[sideIndex].address, wrestler.praticeLevel);
                        }

                        if (Rules.IsModeWithMatchMaker(battle.mode))
                        //if (sideIndex == 1 || !isDummyBattle)
                        {
                            var sideAddress = battle.sides[sideIndex].address;
                            var account = GetAccount(sideAddress);
                            account.ELO = CalculateELO((int)currentELOs[sideIndex], (int)currentELOs[other], sideIndex, battle.state);
                            if (account.ELO != currentELOs[sideIndex])
                            {
                                SetAccount(sideAddress, account);
                            }
                        }

                        if (sideIndex == 1 || !isDummyBattle)
                        {
                            SetWrestler(state.wrestlerID, wrestler);
                        }
                    }
                }
            }
            else
            {
                for (int sideIndex = 0; sideIndex < 2; sideIndex++)
                {
                    int maxIndex = battle.sides[sideIndex].wrestlers.Length;

                    for (int wrestlerIndex = 0; wrestlerIndex < 1; wrestlerIndex++)
                    {
                        var state = battle.sides[sideIndex].wrestlers[wrestlerIndex];
                        var wrestler = GetWrestler(state.wrestlerID);
                        //wrestler.mojoTime = GetCurrentTime();
                        wrestler.location = WrestlerLocation.None;

                        SetWrestler(state.wrestlerID, wrestler);
                    }
                }
            }

            if (battle.mode == BattleMode.Ranked && battle.state != BattleState.Active)
            {
                string verb;

                switch (battle.state)
                {
                    case BattleState.WinA:
                    case BattleState.ForfeitB:
                        verb = "won";
                        break;

                    case BattleState.WinB:
                    case BattleState.ForfeitA:
                        verb = "lost";
                        break;

                    case BattleState.Draw:
                        verb = "draw";
                        break;

                    default: verb = null; break;
                }

                if (verb != null)
                {
                    pairEvent?.Invoke(battle.sides[0].address, battle.sides[1].address, "{0} " + verb + " against {1}!");
                }
            }

            SetBattle(battleID, battle);

            for (int i = 0; i < 2; i++)
            {
                SetAccountBattle(battle.sides[i].address, battleID, battle, i);
            }
        }

        private void AddTrophy(Address address, TrophyFlag trophy)
        {
            if (trophy == TrophyFlag.None)
            {
                return;
            }

            var account = GetAccount(address);
            if (!account.trophies.HasFlag(trophy))
            {
                account.trophies |= trophy;
                SetAccount(address, account);

                Runtime.Notify(NachoEvent.Trophy, address, trophy);
            }
        }

        // changes luchador mode into Auto mode
        public void AutoTurn(Address from, BigInteger battleID)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            var battle = GetBattle(battleID);

            Runtime.Expect(battle.state == BattleState.Active, "battle failed");

            int localIndex = -1;
            for (int i = 0; i < 2; i++)
                if (battle.sides[i].address == from)
                {
                    localIndex = i;
                    break;
                }

            Runtime.Expect(localIndex != -1, "not participant");

            var opponentIndex = 1 - localIndex;
            Runtime.Expect(!battle.sides[opponentIndex].auto, "opponent already auto");

            battle.sides[localIndex].auto = true;
            SetBattle(battleID, battle);

            //Runtime.Notify(EventKind.Auto, from, battleID);
            Runtime.Notify(NachoEvent.Auto, from, battleID);
        }

        private bool IsBattleBroken(NachoBattle battle)
        {
            if (battle.state != BattleState.Active)
            {
                return false;
            }

            var diff = GetCurrentTime() - battle.time;
            if (diff < 0 || diff >= Constants.SECONDS_PER_HOUR)
            {
                return true;
            }

            if (battle.version != CurrentBattleVersion)
            {
                return true;
            }

            return false;
        }

        private int GetBattleCounter(ref NachoBattle battle, int index)
        {
            if (index < battle.counters.Length)
            {
                return (int)battle.counters[index];
            }

            return 0;
        }

        private void SetBattleCounter(ref NachoBattle battle, int index, int val)
        {
            Runtime.Expect(index < battle.counters.Length, "invalid counters");
            battle.counters[index] = val;
        }

        private void IncreaseBattleCounter(ref NachoBattle battle, int index)
        {
            Runtime.Expect(index < battle.counters.Length, "invalid counters");
            battle.counters[index]++;
        }

        private void IncreaseDrinkingCounter(ref NachoBattle battle, int side, ref LuchadorBattleState[] states, ref WrestlerTurnInfo[] info)
        {
            var index = Constants.BATTLE_COUNTER_DRINK_A + side;

            IncreaseBattleCounter(ref battle, index);

            var wrestlerID = states[side].wrestlerID;
            if (wrestlerID <= (int)PraticeLevel.Diamond)
            {
                return; // bots cant get drunk
            }

            var val = GetBattleCounter(ref battle, index);

            if (val <= Constants.DRINKING_LIMIT)
            {
                return;
            }

            if (states[side].status.HasFlag(BattleStatus.Drunk))
            {
                return;
            }

            ApplyStatusEffect(ref battle, ref states, ref info, side, BattleStatus.Drunk, true);
        }

        private int GetBattleAccum(NachoBattle battle, int side)
        {
            int index = Constants.BATTLE_COUNTER_ACCUM_A + side;
            return GetBattleCounter(ref battle, index);
        }

        private int GetBattleCharge(NachoBattle battle, int side)
        {
            int index = Constants.BATTLE_COUNTER_CHARGE_A + side;
            return GetBattleCounter(ref battle, index);
        }

        private void SetBattleCharge(NachoBattle battle, int side, int val)
        {
            int index = Constants.BATTLE_COUNTER_CHARGE_A + side;
            SetBattleCounter(ref battle, index, val);
        }

        public bool PlayTurn(Address from, BigInteger battleID, BigInteger turn, BigInteger slot)
        {
            Runtime.Expect(IsWitness(from), "witness failed");

            Runtime.Expect(slot >= 0 && slot <= 6, "slot failed");

            var battle = GetBattle(battleID);
            Runtime.Expect(battle.state == BattleState.Active, "inactive battle");

            Runtime.Expect(turn == battle.turn, "turn failed");

            var timeDiff = (GetCurrentTime() - battle.time) / 60;
            bool timeOut = timeDiff > Constants.MINIMUM_MINUTES_FOR_IDLE;

            BigInteger seed = Runtime.Randomize(battleID.ToByteArray());

            int localIndex = -1;

            for (int i = 0; i < 2; i++)
            {
                if (battle.sides[i].address == from)
                {
                    localIndex = i;
                    break;
                }
            }

            Runtime.Expect(localIndex != -1, "participant failed");

            int opponentIndex = 1 - localIndex;

            var localSide = battle.sides[localIndex];
            var localWrestler = GetWrestler(localSide.wrestlers[(int)localSide.current].wrestlerID);
            var move = Rules.GetMoveFromMoveset(localWrestler.genes, slot, localSide.wrestlers[(int)localSide.current].stance);

            if (battle.sides[localIndex].turn == turn && battle.sides[localIndex].move != move)
            {
                throw new ContractException("cant change move already");
            }

            battle.sides[localIndex].turn = turn;
            battle.sides[localIndex].move = move;

            if (battle.sides[opponentIndex].auto)
            {
                // Auto play mode opponents

                battle.sides[opponentIndex].turn = turn;
                var aiSlot = /*(byte)*/(1 + (seed % 4));

                var opponentState = battle.sides[1].wrestlers[0];
                var aiState = battle.sides[0].wrestlers[0];

                var aiWrestlerID = aiState.wrestlerID;
                var aiStance = battle.sides[0].wrestlers[0].stance;
                var aiWrestler = GetWrestler(aiWrestlerID);
                var aiMove = Rules.GetMoveFromMoveset(aiWrestler.genes, aiSlot, aiStance);

                seed = Runtime.NextRandom();

                if (battle.mode == BattleMode.Pratice)
                {
                    // BOTs AI

                    var aiLevel = Formulas.CalculateWrestlerLevel((int)aiWrestler.experience);
                    int smartness = (int)((aiLevel * 100) / Constants.MAX_LEVEL);
                    if (smartness > 100)
                    {
                        smartness = 100;
                    }

                    var chance = (int)(seed % 100);
                    seed = Runtime.NextRandom();

                    smartness -= chance;

                    if (smartness > 50)
                    {
                        switch (move)
                        {
                            case WrestlingMove.Smash:
                                {
                                    aiMove = WrestlingMove.Counter;
                                    break;
                                } 

                            case WrestlingMove.Block:
                                {
                                    if (aiStance == BattleStance.Main)
                                        aiMove = Rules.GetMoveFromMoveset(aiWrestler.genes, 4, aiStance);
                                    else
                                        aiMove = Rules.GetMoveFromMoveset(aiWrestler.genes, 0, aiStance);
                                    break;
                                }

                            case WrestlingMove.Counter:
                                {
                                    if (aiMove == WrestlingMove.Smash)
                                    {
                                        aiMove = Rules.GetMoveFromMoveset(aiWrestler.genes, 0, aiStance);
                                    }
                                    break;
                                }
                        }

                        bool avoidMove = false;

                        switch (aiMove)
                        {
                            case WrestlingMove.Slingshot:
                                {
                                    if (aiState.itemKind == ItemKind.None)
                                    {
                                        avoidMove = true;
                                    }
                                    break;
                                }

                            case WrestlingMove.Recycle:
                                {
                                    if (aiState.itemKind != ItemKind.None)
                                    {
                                        avoidMove = true;
                                    }
                                    break;
                                }

                            case WrestlingMove.Taunt:
                                {
                                    if (opponentState.status.HasFlag(BattleStatus.Taunted))
                                    {
                                        avoidMove = true;
                                    }
                                    break;
                                }

                            case WrestlingMove.Clown_Makeup:
                                {
                                    if (opponentState.status.HasFlag(BattleStatus.Smiling))
                                    {
                                        avoidMove = true;
                                    }
                                    break;
                                }

                            case WrestlingMove.Dodge:
                                {
                                    if (aiState.lastMove == WrestlingMove.Dodge)
                                    {
                                        avoidMove = true;
                                    }
                                    break;
                                }

                            case WrestlingMove.Counter:
                                {
                                    avoidMove = true;
                                    break;
                                }

                            default:
                                {
                                    if (aiState.lastMove == aiMove && aiState.status.HasFlag(BattleStatus.Cursed))
                                    {
                                        avoidMove = true;
                                    }
                                    break;
                                }
                        }

                        if (avoidMove)
                        {
                            var botSlot = /*(byte)*/(chance % 2 == 0 ? 4 : 0);
                            aiMove = Rules.GetMoveFromMoveset(aiWrestler.genes, botSlot, aiStance);
                        }
                    }
                    else if (smartness < 0)
                    {
                        switch (move)
                        {
                            case WrestlingMove.Smash:
                                {
                                    if (aiMove == WrestlingMove.Counter)
                                    {
                                        aiMove = WrestlingMove.Chop;
                                    }
                                    break;
                                }

                            case WrestlingMove.Counter:
                                {
                                    aiMove = WrestlingMove.Smash;
                                    break;
                                }
                        }
                    }
                }

                battle.sides[opponentIndex].move = aiMove;
            }
            else
            if (battle.sides[opponentIndex].turn < battle.sides[localIndex].turn && timeOut)
            {
                battle.sides[opponentIndex].turn = turn;
                battle.sides[opponentIndex].move = WrestlingMove.Idle;
            }

            // from here forward we no longer care about who is A or B

            if (battle.sides[0].turn != battle.sides[1].turn)
            {
                SetBattle(battleID, battle);
                //Runtime.Runtime.Notify("turn played");
                return true;
            }

            var states = new LuchadorBattleState[2];
            var wrestlers = new NachoWrestler[2];
            var info = new WrestlerTurnInfo[2];
            var originalItems = new ItemKind[2];

            for (var i = 0; i < 2; i++)
            {
                var other = 1 - i;

                var current = (int)battle.sides[i].current;
                states[i] = battle.sides[i].wrestlers[current];
                originalItems[i] = states[i].itemKind;

                var wrestler = GetWrestler(states[i].wrestlerID);
                wrestlers[i] = wrestler;
            }

            // HACK for item activations
            for (var i = 0; i < 2; i++)
            {
                var other = 1 - i;

                info[i].address = battle.sides[i].address;
            }

            if (battle.sides[0].move == WrestlingMove.Forfeit && battle.sides[1].move == WrestlingMove.Forfeit)
            {
                battle.state = BattleState.Draw;
            }
            else
            if (battle.sides[0].move == WrestlingMove.Forfeit && battle.sides[1].move != WrestlingMove.Forfeit)
            {
                battle.state = BattleState.ForfeitA;
            }
            else
            if (battle.sides[0].move != WrestlingMove.Forfeit && battle.sides[1].move == WrestlingMove.Forfeit)
            {
                battle.state = BattleState.ForfeitB;
            }
            else
            {
                // pre turn move checks
                for (var i = 0; i < 2; i++)
                {
                    var other = 1 - i;

                    // WARNING: The order of following if conditions matters, and defines the priority of different effects/items

                    if (states[i].lastMove == WrestlingMove.Hyper_Slam)
                    {
                        battle.sides[i].move = WrestlingMove.Flinch;
                    }

                    if (states[i].lastMove == WrestlingMove.Rage_Punch)
                    {
                        battle.sides[i].move = WrestlingMove.Ultra_Punch;
                    }

                    if (battle.sides[i].move == WrestlingMove.Wood_Work && states[i].itemKind == ItemKind.Wood_Chair)
                    {
                        battle.sides[i].move = WrestlingMove.Smash;
                    }

                    var charge = GetBattleCharge(battle, i);

                    if (charge > 0)
                    {
                        switch (battle.sides[i].move)
                        {
                            case WrestlingMove.Rhino_Charge:
                                battle.sides[i].move = WrestlingMove.Rhino_Rush;
                                break;

                            case WrestlingMove.Mantra:
                                battle.sides[i].move = WrestlingMove.Iron_Fist;
                                break;

                            case WrestlingMove.Zen_Point:
                                battle.sides[i].move = WrestlingMove.Pain_Release;
                                break;
                        }
                    }

                    if (states[i].status.HasFlag(BattleStatus.Drunk))
                    {
                        var genes = GetWrestler(states[i].wrestlerID).genes;
                        var vomitSlot = /*(byte)*/(1 + battle.turn % 4);
                        var vomitMove = Rules.GetMoveFromMoveset(genes, vomitSlot, states[i].stance);

                        if (battle.sides[i].move == vomitMove)
                        {
                            battle.sides[i].move = WrestlingMove.Vomit;
                        }
                    }

                    if (battle.sides[i].move == WrestlingMove.Bottle_Sip && states[i].status.HasFlag(BattleStatus.Drunk))
                    {
                        battle.sides[i].move = WrestlingMove.Drunken_Fist;
                    }

                    if (battle.sides[i].move == WrestlingMove.Copycat)
                    {
                        if (states[i].learnedMove != WrestlingMove.Unknown)
                        {
                            battle.sides[i].move = states[i].learnedMove;
                        }
                    }

                    if (Rules.IsChoiceItem(states[i].itemKind) && states[other].itemKind != ItemKind.Nullifier && turn > 1)
                    {
                        if (states[i].lastMove != battle.sides[i].move)
                        {
                            battle.sides[i].move = states[i].lastMove;
                        }
                    }

                    if (battle.sides[i].move == WrestlingMove.Tart_Throw && states[i].itemKind == ItemKind.Cooking_Hat && ActivateItem(ref states, ref info, i, true, false))
                    {
                        battle.sides[i].move = WrestlingMove.Burning_Tart;
                    }

                    if (battle.sides[i].move == WrestlingMove.Chop && states[i].itemKind == ItemKind.Dojo_Belt && ActivateItem(ref states, ref info, i, true, false))
                    {
                        battle.sides[i].move = WrestlingMove.Mega_Chop;
                    }

                    if (states[i].status.HasFlag(BattleStatus.Taunted))
                    {
                        if (!Rules.IsDamageMove(battle.sides[i].move))
                        {
                            battle.sides[i].move = WrestlingMove.Chop;
                        }
                    }

                    if (states[i].status.HasFlag(BattleStatus.Cursed))
                    {
                        if (battle.sides[i].move == states[i].lastMove)
                        {
                            battle.sides[i].move = WrestlingMove.Flinch;
                        }
                    }

                    if (states[other].itemKind == ItemKind.Trap_Card && (Rules.IsSecondaryMove(battle.sides[i].move) || Rules.IsTertiaryMove(battle.sides[i].move)) && ActivateItem(ref states, ref info, other, true, true))
                    {
                        battle.sides[i].move = WrestlingMove.Flinch;
                    }

                    if (battle.sides[i].move == states[i].lastMove)
                    {
                        if (battle.sides[i].previousDirectDamage == 0 && states[i].itemKind == ItemKind.Speed_Chip)
                        {
                            battle.sides[i].move = WrestlingMove.Smash;
                        }

                        switch (battle.sides[i].move)
                        {
                            case WrestlingMove.Dodge:
                            case WrestlingMove.Block:
                            case WrestlingMove.Refresh:
                                battle.sides[i].move = WrestlingMove.Flinch;
                                break;
                        }
                    }

                    if (states[i].itemKind == ItemKind.None && battle.sides[i].move == WrestlingMove.Slingshot)
                    {
                        battle.sides[i].move = WrestlingMove.Flinch;
                    }

                    if (battle.sides[i].move == states[i].disabledMove)
                    {
                        battle.sides[i].move = WrestlingMove.Flinch;
                    }

                    if (states[i].itemKind == ItemKind.Golden_Bullet && ActivateItem(ref states, ref info, i, false, false))
                    {
                        if (states[i].stance != BattleStance.Bizarre && battle.sides[i].move != WrestlingMove.Counter && battle.sides[other].move == WrestlingMove.Smash
                            || states[i].stance == BattleStance.Bizarre && battle.sides[i].move != WrestlingMove.Anti_Counter && battle.sides[other].move != WrestlingMove.Smash)
                        {
                            var targetMove = states[i].stance == BattleStance.Bizarre ? WrestlingMove.Anti_Counter : WrestlingMove.Counter;

                            if (states[i].disabledMove != targetMove)
                            {
                                battle.sides[i].move = targetMove;
                                states[i].itemKind = ItemKind.None;
                                Runtime.Notify(NachoEvent.ItemActivated, from, ItemKind.Golden_Bullet);
                            }
                        }
                    }

                    if (states[i].status.HasFlag(BattleStatus.Flinched))
                    {
                        states[i].status &= ~BattleStatus.Flinched; // clear flag 
                        battle.sides[i].move = WrestlingMove.Flinch;
                    }
                }

                for (var i = 0; i < 2; i++)
                {
                    var other = 1 - i;
                    if (battle.sides[i].move == WrestlingMove.Copycat && states[i].learnedMove == WrestlingMove.Unknown)
                    {
                        states[i].learnedMove = battle.sides[other].move;
                        battle.sides[i].move = battle.sides[other].move; // uses learned move in same turn
                    }
                }

                var confusion = new bool[2];
                var misses = new bool[2];

                for (var i = 0; i < 2; i++)
                {
                    var other = 1 - i;
                    info[i] = CalculateTurnInfo(battle.sides[i], wrestlers[i], battle.sides[i].wrestlers[0].wrestlerID, battle.sides[i].move, states[i].lastMove, states[i], seed);

                    seed = Runtime.NextRandom();

                    if (info[i].move == WrestlingMove.Tart_Throw && info[i].item != ItemKind.Cooking_Hat && !Rules.IsSucessful((int)info[i].chance, Constants.TART_THROW_ACCURACY))
                    {
                        info[i].move = WrestlingMove.Tart_Splash;
                        battle.sides[i].move = info[i].move;
                    }

                    // miss setup
                    misses[i] = false;

                    // confusion setup
                    if (states[i].status.HasFlag(BattleStatus.Confused))
                    {
                        confusion[i] = !Rules.IsSucessful((int)info[i].chance, Constants.CONFUSION_ACCURACY);
                    }
                    else
                    {
                        confusion[i] = false;
                    }

                    if (battle.sides[i].move == WrestlingMove.Flailing_Arms)
                    {
                        confusion[i] = false;
                    }
                }

                for (var i = 0; i < 2; i++)
                {
                    var other = 1 - i;
                    var multiplier = 1;
                    var buff = false;

                    if (states[i].itemKind == ItemKind.Magnifying_Glass && states[other].itemKind != ItemKind.Nullifier)
                    {
                        multiplier = 2;
                    }

                    switch (info[i].move)
                    {
                        case WrestlingMove.Scream:
                            if (!ApplyStatBoost(ref info, ref states, i, StatKind.Attack, Constants.BIG_BOOST_CHANGE * multiplier))
                            {
                                Runtime.Notify(NachoEvent.MoveMiss, battle.sides[i].address, battle.sides[i].move);
                            }
                            buff = true;
                            break;

                        case WrestlingMove.Bulk:
                            if (!ApplyStatBoost(ref info, ref states, i, StatKind.Defense, Constants.BIG_BOOST_CHANGE * multiplier))
                            {
                                Runtime.Notify(NachoEvent.MoveMiss, battle.sides[i].address, battle.sides[i].move);
                            }
                            buff = true;
                            break;

                        case WrestlingMove.Lion_Roar:
                            ApplyStatBoost(ref info, ref states, i, StatKind.Attack, Constants.SMALL_BOOST_CHANGE * multiplier);
                            ApplyStatBoost(ref info, ref states, i, StatKind.Defense, Constants.SMALL_BOOST_CHANGE * multiplier);
                            buff = true;
                            break;

                        case WrestlingMove.Avalanche:
                            ApplyStatBoost(ref info, ref states, i, StatKind.Attack, Constants.SMALL_BOOST_CHANGE * multiplier);
                            buff = true;
                            break;

                        case WrestlingMove.Corkscrew:
                            ApplyStatBoost(ref info, ref states, i, StatKind.Defense, Constants.SMALL_BOOST_CHANGE * multiplier);
                            buff = true;
                            break;

                        case WrestlingMove.Refresh:
                        case WrestlingMove.Sweatshake:
                            states[i].status = 0;
                            break;

                        case WrestlingMove.Chicken_Wing:
                            ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Confused, false);
                            break;
                    }

                    if (multiplier > 1 && buff)
                    {
                        Runtime.Notify(NachoEvent.ItemActivated, info[i].address, states[i].itemKind);
                    }

                    // first turn item-activations
                    switch (states[i].itemKind)
                    {
                        // TODO more boost variations with less power, and make those 2 have more power
                        case ItemKind.Claws:
                            if (info[i].currentAtk < info[other].currentAtk && ActivateItem(ref states, ref info, i, true, false))
                            {
                                ApplyStatBoost(ref info, ref states, i, StatKind.Attack, Constants.SMALL_BOOST_CHANGE);
                            }
                            break;

                        case ItemKind.Shell:
                            if (info[i].currentDef < info[other].currentDef && ActivateItem(ref states, ref info, i, true, false))
                            {
                                ApplyStatBoost(ref info, ref states, i, StatKind.Defense, Constants.SMALL_BOOST_CHANGE);
                            }
                            break;

                        case ItemKind.Spy_Specs:
                            {
                                // do nothing besides "activation"
                                ActivateItem(ref states, ref info, i, false, false);
                                Runtime.Notify(NachoEvent.ItemActivated, info[i].address, states[i].itemKind);
                                break;
                            }

                        case ItemKind.Dice:
                            {
                                if (ActivateItem(ref states, ref info, i, true, true))
                                {
                                    seed = Runtime.NextRandom();
                                    var roll = seed % 6;

                                    int target = roll < 4 ? i : 1 - i;
                                    int boost = roll < 2 ? Constants.BIG_BOOST_CHANGE : Constants.SMALL_BOOST_CHANGE;

                                    var stat = (roll % 2 == 0) ? StatKind.Attack : StatKind.Defense;
                                    ApplyStatBoost(ref info, ref states, i, stat, boost);

                                }
                                break;
                            }

                        case ItemKind.Pincers:
                            {
                                if (ActivateItem(ref states, ref info, i, true, true))
                                {
                                    states[other].disabledMove = battle.sides[other].move;
                                }
                                break;
                            }
                    }

                    // pre turn item-activation
                    switch (states[i].itemKind)
                    {
                        case ItemKind.Creepy_Toy:
                            if (info[other].lastMove == info[other].move && ActivateItem(ref states, ref info, i, true, false))
                            {
                                states[other].disabledMove = info[other].move;
                            }
                            break;
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    var other = 1 - i;
                    var power = CalculateMoveDamage(battle, info[i], info[other]);

                    // clamp the damage, this makes possible to survive a counter in some situations
                    if (power > info[other].currentStamina)
                    {
                        power = (int)info[other].currentStamina;
                    }

                    if (power > 0)
                    {
                        var category = Rules.GetMoveCategory(info[i].move);

                        if (category == MoveCategory.Legs && states[i].itemKind == ItemKind.Steel_Boots && ActivateItem(ref states, ref info, i, true, false))
                        {
                            var dmgBoost = (power * Constants.BATTLE_STEEL_BOOTS_DAMAGE_BOOST) / 100;
                            if (dmgBoost < 1) dmgBoost = 1;

                            power += dmgBoost;
                        }

                        if (category == MoveCategory.Head && states[i].itemKind == ItemKind.Battle_Helmet && ActivateItem(ref states, ref info, i, true, false))
                        {
                            var dmgBoost = (power * Constants.BATTLE_HELMET_DAMAGE_BOOST) / 100;
                            if (dmgBoost < 1) dmgBoost = 1;

                            power += dmgBoost;
                        }

                        if (states[i].status.HasFlag(BattleStatus.Cursed) && states[i].itemKind == ItemKind.Gnome_Cap && ActivateItem(ref states, ref info, i, true, false))
                        {
                            var dmgBoost = (power * Constants.BATTLE_GNOME_CAP_DAMAGE_BOOST) / 100;
                            if (dmgBoost < 1) dmgBoost = 1;

                            power += dmgBoost;
                        }
                    }

                    info[i].power = power;
                }

                // mid turn move checks
                for (var i = 0; i < 2; i++)
                {
                    var other = 1 - i;

                    switch (battle.sides[i].move)
                    {
                        case WrestlingMove.Smash:

                            if (states[i].itemKind == ItemKind.Wood_Chair && states[other].itemKind != ItemKind.Nullifier)
                            {
                                if (states[other].itemKind != ItemKind.Wood_Potato)
                                {
                                    Runtime.Notify(NachoEvent.ItemSpent, info[i].address, states[i].itemKind);
                                }

                                states[i].itemKind = ItemKind.None;
                            }

                            if (states[i].stance == BattleStance.Alternative)
                            {
                                ChangeStance(ref states, ref info, i, false);
                            }

                            break;

                        case WrestlingMove.Counter:
                        case WrestlingMove.Anti_Counter:
                            if (states[i].itemKind == ItemKind.Gyroscope && ActivateItem(ref states, ref info, i, true, false))
                            {
                                states[i].itemKind = ItemKind.None;
                            }
                            break;

                        case WrestlingMove.Hammerhead:
                            ApplyStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Confused, true);
                            break;

                        case WrestlingMove.Recycle:
                            if (states[i].itemKind == ItemKind.None && wrestlers[i].itemID != 0)
                            {
                                //states[i].itemKind = Formulas.GetItemKind(wrestlers[i].itemID);
                                states[i].itemKind = GetItem(wrestlers[i].itemID).kind;
                                Runtime.Notify(NachoEvent.ItemAdded, battle.sides[i].address, states[i].itemKind);
                            }

                            ChangeStance(ref states, ref info, i, false);
                            SetBattleCharge(battle, i, 1);
                            break;


                        case WrestlingMove.Iron_Fist:

                            if (states[other].boostDef > 2 * Constants.IRON_FIST_DEFENSE_REDUCTION)
                            {
                                ApplyStatBoost(ref info, ref states, other, StatKind.Defense, -Constants.IRON_FIST_DEFENSE_REDUCTION, true);
                            }

                            ChangeStance(ref states, ref info, i, false);
                            SetBattleCharge(battle, i, 0);
                            break;

                        case WrestlingMove.Knee_Bomb:
                            if (states[i].boostAtk > 2)
                            {
                                states[i].boostAtk /= 2;
                            }
                            break;

                        case WrestlingMove.Taunt:
                            ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Taunted, false);
                            break;

                        case WrestlingMove.Chilli_Dance:
                            if (Rules.IsSucessful((int)info[i].chance, Constants.BIG_SIDE_EFFECT_ACCURACY))
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Burned, false);
                            }

                            ChangeStance(ref states, ref info, i, false);
                            break;

                        case WrestlingMove.Poison_Ivy:
                            if (Rules.IsSucessful((int)info[i].chance, Constants.BIG_SIDE_EFFECT_ACCURACY))
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Poisoned, false);
                            }

                            ChangeStance(ref states, ref info, i, false);
                            break;

                        // change stance, no charge
                        case WrestlingMove.Dodge:
                        case WrestlingMove.Headspin:
                        case WrestlingMove.Gobble:
                        case WrestlingMove.Sweatshake:
                        case WrestlingMove.Astral_Tango:
                            ChangeStance(ref states, ref info, i, false);
                            break;

                        // change stance, with charge
                        case WrestlingMove.Zen_Point:
                        case WrestlingMove.Rhino_Charge:
                        case WrestlingMove.Mantra:
                            ChangeStance(ref states, ref info, i, false);
                            SetBattleCharge(battle, i, 1);
                            break;

                        // change stance, with discharge
                        case WrestlingMove.Pain_Release:
                        case WrestlingMove.Rhino_Rush:
                            //case WrestlingMove.Iron_Fist: // this one has a separate case
                            ChangeStance(ref states, ref info, i, false);
                            SetBattleCharge(battle, i, 0);
                            break;

                        case WrestlingMove.Spinning_Crane:

                            if (Rules.IsSucessful((int)info[i].chance, Constants.SMALL_SIDE_EFFECT_ACCURACY))
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Bleeding, false);
                            }

                            ChangeStance(ref states, ref info, i, false);
                            break;

                        case WrestlingMove.Fart:
                            if (Rules.IsSucessful((int)info[i].chance, Constants.SMALL_SIDE_EFFECT_ACCURACY))
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Poisoned, false);
                            }
                            break;

                        case WrestlingMove.Vomit:

                            int victimIndex;
                            BattleStatus victimStatus;

                            switch ((int)info[i].chance % 4)
                            {
                                case 0:
                                    victimIndex = i;
                                    victimStatus = BattleStatus.Confused;
                                    break;

                                case 1:
                                    victimIndex = other;
                                    victimStatus = BattleStatus.Confused;
                                    break;

                                case 2:
                                    victimIndex = i;
                                    victimStatus = BattleStatus.Poisoned;
                                    break;

                                case 3:
                                    victimIndex = other;
                                    victimStatus = BattleStatus.Poisoned;
                                    break;

                                case 4:
                                    victimIndex = i;
                                    victimStatus = BattleStatus.Taunted;
                                    break;

                                case 5:
                                    victimIndex = other;
                                    victimStatus = BattleStatus.Taunted;
                                    break;

                                default:
                                    victimIndex = i;
                                    victimStatus = BattleStatus.Flinched;
                                    break;
                            }

                            ApplyStatusEffect(ref battle, ref states, ref info, victimIndex, victimStatus, victimIndex == i);
                            break;

                        case WrestlingMove.Needle_Sting:
                            ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Poisoned, false);
                            break;

                        case WrestlingMove.Clown_Makeup:
                            // here we check if the stance can be changed, otherwise the luchador might be locked into a stance already

                            if (states[other].stance != BattleStance.Zombie && states[other].stance != BattleStance.Clown && states[other].stance != BattleStance.Bizarre)
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Smiling, false);
                            }
                            break;

                        case WrestlingMove.Mind_Slash:
                            if (states[i].status != BattleStatus.None)
                            {
                                var targetFlag = BattleStatus.None;
                                for (int f = 0; f < 16; f++)
                                {
                                    var flag = (BattleStatus)(1 << f);
                                    if (states[i].status.HasFlag(flag))
                                    {
                                        targetFlag = flag;
                                        break;
                                    }
                                }

                                if (targetFlag != BattleStatus.None)
                                {
                                    states[i].status &= ~targetFlag; // clear flag 
                                }
                            }
                            break;

                        case WrestlingMove.Wood_Work:
                            ChangeStance(ref states, ref info, i, false);
                            SetBattleCharge(battle, i, 1);

                            if (states[i].itemKind == ItemKind.None && states[other].itemKind != ItemKind.Nullifier)
                            {
                                states[i].itemKind = ItemKind.Wood_Chair;
                                Runtime.Notify(NachoEvent.ItemAdded, info[i].address, states[i].itemKind);
                            }
                            break;

                        case WrestlingMove.Octopus_Arm:
                            states[other].disabledMove = Rules.GetMoveFromMoveset(wrestlers[other].genes, 4, 0);
                            break;

                        case WrestlingMove.Flying_Kick:
                            if (Rules.IsSucessful((int)info[i].chance, Constants.SMALL_SIDE_EFFECT_ACCURACY))
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Flinched, false);
                            }
                            break;

                        case WrestlingMove.Wolf_Claw:
                            if (Rules.IsSucessful((int)info[i].chance, Constants.SMALL_SIDE_EFFECT_ACCURACY))
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Bleeding, false);
                            }
                            break;

                        case WrestlingMove.Razor_Jab:
                            if (Rules.IsSucessful((int)info[i].chance, Constants.BIG_SIDE_EFFECT_ACCURACY))
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Bleeding, false);
                            }
                            break;

                        case WrestlingMove.Fire_Breath:
                            ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Burned, false);
                            break;

                        case WrestlingMove.Flame_Fang:
                            if (Rules.IsSucessful((int)info[i].chance, Constants.SMALL_SIDE_EFFECT_ACCURACY))
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Burned, false);
                            }

                            break;

                        case WrestlingMove.Burning_Tart:
                            ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Burned, false);
                            break;

                        case WrestlingMove.Slingshot:
                            if (states[i].itemKind != ItemKind.None)
                            {
                                switch (states[i].itemKind)
                                {
                                    case ItemKind.Nails:
                                    case ItemKind.Tequilla:
                                        {
                                            ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Bleeding, false);
                                            break;
                                        }

                                    case ItemKind.Chilli_Bottle:
                                    case ItemKind.Bomb:
                                        {
                                            ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Burned, false);
                                            break;
                                        }

                                    case ItemKind.Virus_Chip:
                                        {
                                            RemoveStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Diseased);
                                            ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Diseased, false);
                                            break;
                                        }

                                    case ItemKind.Creepy_Toy:
                                        {
                                            ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Cursed, false);
                                            break;
                                        }

                                    case ItemKind.Clown_Nose:
                                        {
                                            RemoveStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Smiling);
                                            ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Smiling, false);
                                            break;
                                        }

                                    case ItemKind.Fork:
                                        {
                                            if (states[other].itemKind == ItemKind.None)
                                            {
                                                states[other].itemKind = states[i].itemKind;
                                                Runtime.Notify(NachoEvent.ItemAdded, info[other].address, states[i].itemKind);
                                            }
                                            break;
                                        }
                                }

                                // we notify here even if we lose the item to show the opponent what was throw
                                // this does not count as an "item activation"                            
                                Runtime.Notify(NachoEvent.ItemSpent, info[i].address, states[i].itemKind);
                                states[i].itemKind = ItemKind.None;
                            }
                            break;

                        case WrestlingMove.Trick:
                            {
                                var temp = states[i].itemKind;
                                states[i].itemKind = states[other].itemKind;
                                states[other].itemKind = temp;

                                if (states[other].itemKind == ItemKind.Clown_Nose && states[i].itemKind != ItemKind.Clown_Nose)
                                {
                                    RemoveStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Smiling);
                                    ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Smiling, false);
                                }
                                else
                                if (states[other].itemKind != ItemKind.Clown_Nose && states[i].itemKind == ItemKind.Clown_Nose)
                                {
                                    RemoveStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Smiling);
                                    ApplyStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Smiling, false);
                                }

                                // does not count as "item activation"
                                Runtime.Notify(NachoEvent.ItemAdded, info[i].address, states[i].itemKind);
                                Runtime.Notify(NachoEvent.ItemAdded, info[other].address, states[other].itemKind);
                            }
                            break;

                        case WrestlingMove.Joker:
                            if (Rules.IsSucessful((int)info[i].chance, 50))
                            {
                                states[i].stance = BattleStance.Main;
                                Runtime.Notify(NachoEvent.Stance, info[i].address, states[i].stance);
                            }
                            else
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Flinched, true);
                            }
                            break;

                        case WrestlingMove.Beggar_Bag:
                            {
                                ItemKind summon;
                                switch ((int)(seed % 10))
                                {
                                    case 0: summon = ItemKind.Bomb; break;
                                    case 1: summon = ItemKind.Pincers; break;
                                    case 2: summon = ItemKind.Bling; break;
                                    case 3: summon = ItemKind.Focus_Banana; break;
                                    case 4: summon = ItemKind.Vampire_Teeth; break;
                                    case 5: summon = ItemKind.Vigour_Juice; break;
                                    case 6: summon = ItemKind.Sombrero; break;
                                    case 7: summon = ItemKind.Poncho; break;
                                    case 8: summon = ItemKind.Power_Juice; break;

                                    default: summon = ItemKind.Wrist_Weights; break;
                                }

                                states[i].itemKind = summon;
                                // does not count as "item activation"
                                Runtime.Notify(NachoEvent.ItemAdded, info[i].address, states[i].itemKind);
                                break;
                            }

                        case WrestlingMove.Leg_Twister:
                            if (states[i].itemKind == ItemKind.Serious_Mask && ActivateItem(ref states, ref info, i, true, false))
                            {
                                // do nothing
                            }
                            else
                            {
                                ChangeStance(ref states, ref info, other, true); // force switching stance
                            }

                            break;
                    }

                }

                // post turn item-activation
                for (int i = 0; i < 2; i++)
                {
                    var other = 1 - i;

                    switch (states[i].itemKind)
                    {
                        case ItemKind.Clown_Nose:
                            ApplyStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Smiling, true);
                            break;

                        case ItemKind.Mummy_Bandages:
                            if (states[i].status.HasFlag(BattleStatus.Bleeding) && ActivateItem(ref states, ref info, i, true, false))
                            {
                                RemoveStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Bleeding);
                            }
                            break;
                    }
                }

                var direct_damage = new int[2];
                var indirect_damage = new int[2];
                var recover_damage = new int[2];

                for (int i = 0; i < 2; i++)
                {
                    direct_damage[i] = 0;
                    indirect_damage[i] = 0;
                    recover_damage[i] = 0;
                }

                for (int i = 0; i < 2; i++)
                {
                    var other = 1 - i;

                    var dmg = CalculateMoveResult(info[other], info[i], seed);
                    seed = Runtime.NextRandom();

                    if (dmg > 0)
                    {
                        // make first battles against wood dummy bot easier for the players while they are below level 2
                        if (battle.mode == BattleMode.Pratice && i == 1 && wrestlers[i].experience < Constants.EXPERIENCE_MAP[2] && battle.sides[0].wrestlers[0].wrestlerID == 1)
                        {
                            dmg /= 2;
                            if (dmg < 1) dmg = 1;
                        }

                        switch (states[i].itemKind)
                        {
                            // this is choice item
                            case ItemKind.Body_Armor:

                                // no notification for gameplay balancing purposes
                                if (battle.sides[other].move != WrestlingMove.Palm_Strike && ActivateItem(ref states, ref info, i, false, false))
                                {
                                    dmg /= 2;
                                    if (dmg < 1) dmg = 1;
                                }

                                break;

                            case ItemKind.Bling:
                                {
                                    if (ActivateItem(ref states, ref info, i, true, false))
                                    {
                                        ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Taunted, false);
                                    }
                                    break;
                                }

                            case ItemKind.Giant_Pillow:
                                {
                                    if (battle.sides[other].move != WrestlingMove.Palm_Strike && ActivateItem(ref states, ref info, i, true, false))
                                    {
                                        var dmgReduction = (dmg * Constants.GIANT_PILLOW_PERCENT) / 100;
                                        if (dmgReduction < 1) dmgReduction = 1;

                                        dmg -= dmgReduction;
                                    }
                                    break;
                                }

                        }
                    }

                    if (states[other].status.HasFlag(BattleStatus.Flinched))
                    {
                        dmg = 0;
                    }

                    if (misses[i] && dmg > 0)
                    {
                        Runtime.Notify(NachoEvent.MoveMiss, battle.sides[i].address, battle.sides[i].move);
                        dmg = 0;
                    }

                    if (dmg > 0)
                    {
                        // this is choice item
                        // no notification for gameplay balancing purposes
                        if (states[other].itemKind == ItemKind.Killer_Gloves && ActivateItem(ref states, ref info, other, false, false))
                        {
                            var dmgBoost = (dmg * Constants.KILLER_GLOVES_BOOST_PERCENT) / 100;
                            if (dmgBoost < 1) dmgBoost = 1;

                            dmg += dmgBoost;
                        }

                        if (states[other].itemKind == ItemKind.Muscle_Overclocker && ActivateItem(ref states, ref info, other, true, false))
                        {
                            var extra = (dmg * Constants.DAMAGE_MUSCLE_OVERCLOCKER_PERCENTAGE) / 100;
                            if (extra < 1) extra = 1;
                            dmg += extra;
                            indirect_damage[other] += extra;
                        }

                        if (battle.sides[other].move == WrestlingMove.Takedown)
                        {
                            var recoil = (dmg * Constants.DAMAGE_TAKEDOWN_RECOIL_PERCENT) / 100;

                            if (states[other].itemKind == ItemKind.Hand_Bandages && ActivateItem(ref states, ref info, other, true, false))
                            {
                                recoil /= 2;
                            }

                            if (recoil < 1) recoil = 1;

                            indirect_damage[other] += recoil;
                        }
                    }

                    if (battle.sides[0].move == WrestlingMove.Ritual || battle.sides[1].move == WrestlingMove.Ritual)
                    {
                        var sum = states[0].currentStamina + states[1].currentStamina;
                        var isOdd = (sum % 2 == 1); // check if sum is odd number
                        sum /= 2;

                        if (states[i].currentStamina > sum)
                        {
                            var diff = states[i].currentStamina - sum;
                            dmg += (int)diff;
                        }
                        else
                        if (states[i].currentStamina < sum)
                        {
                            var diff = sum - states[i].currentStamina;
                            if (isOdd)
                            {
                                diff++;
                            }
                            recover_damage[i] += (int)diff;
                        }
                    }

                    // check missable moves
                    if (dmg == 0)
                    {
                        switch (info[i].move)
                        {
                            case WrestlingMove.Gutbuster:
                            case WrestlingMove.Side_Hook:
                                Runtime.Notify(NachoEvent.MoveMiss, battle.sides[i].address, info[i].move);
                                break;
                        }
                    }

                    // check if direct or indirect damage
                    switch (info[i].move)
                    {
                        case WrestlingMove.Psy_Strike:
                        case WrestlingMove.Mind_Slash:
                            if (states[i].itemKind == ItemKind.Spoon && ActivateItem(ref states, ref info, i, true, false))
                            {
                                dmg += (dmg * Constants.SPOON_BOOST_PERCENT) / 100;
                            }

                            indirect_damage[i] += dmg;
                            break;

                        case WrestlingMove.Gorilla_Cannon:
                            {
                                int charges = GetBattleCounter(ref battle, Constants.BATTLE_COUNTER_GORILLA_A + i);

                                float multiplier;
                                switch (charges)
                                {
                                    case 0: multiplier = 0; break;
                                    case 1: multiplier = 2; break;
                                    case 2: multiplier = 3.5f; break;
                                    case 3: multiplier = 4.75f; break;
                                    default: multiplier = 6; break;
                                }

                                direct_damage[i] += (int)(dmg * multiplier);
                                break;
                            }

                        default:
                            direct_damage[i] += dmg;
                            break;
                    }
                }

                // recover damage
                for (int i = 0; i < 2; i++)
                {
                    var recover = 0;
                    var other = 1 - i;

                    if (info[i].move == WrestlingMove.Pray)
                    {
                        var amount = (info[i].maxStamina / 3);
                        if (info[i].currentStamina + amount > info[i].maxStamina)
                        {
                            amount = info[i].maxStamina - info[i].currentStamina;
                        }
                        recover += (int)amount;
                    }

                    if (info[other].move == WrestlingMove.Tart_Splash)
                    {
                        var amount = info[other].power;

                        if (info[i].currentStamina + amount > info[i].maxStamina)
                        {
                            amount = info[i].maxStamina - info[i].currentStamina;
                        }
                        recover += (int)amount;
                    }

                    if (states[i].stance != states[i].lastStance && states[i].itemKind == ItemKind.Meat_Snack &&
                        (info[i].currentStamina < info[i].maxStamina || direct_damage[i] > 0 || indirect_damage[i] > 0) && ActivateItem(ref states, ref info, i, true, false))
                    {
                        recover += (int)(info[i].maxStamina * Constants.MEAT_SNACK_RECOVER_PERCENT) / 100;
                    }

                    var needsRecover = (states[i].currentStamina < info[i].maxStamina || direct_damage[i] > 0 || indirect_damage[i] > 0);

                    switch (battle.sides[i].move)
                    {
                        case WrestlingMove.Torniquet:
                            {
                                if (direct_damage[i] == 0)
                                {
                                    var amount = (info[i].maxStamina / 2);
                                    if (amount < 1) amount = 1;

                                    if (info[i].currentStamina + amount > info[i].maxStamina)
                                    {
                                        amount = info[i].maxStamina - info[i].currentStamina;
                                    }

                                    recover += (int)amount;
                                }
                                else
                                {
                                    Runtime.Notify(NachoEvent.MoveMiss, battle.sides[i].address, battle.sides[i].move);
                                }
                                break;
                            }

                        case WrestlingMove.Refresh:
                            {
                                if (needsRecover)
                                {
                                    var amount = (info[i].maxStamina * Constants.REFRESH_RECOVER_PERCENT) / 100;
                                    if (amount < 1) amount = 1;
                                    recover += (int)amount;
                                }
                                else
                                {
                                    Runtime.Notify(NachoEvent.MoveMiss, battle.sides[i].address, battle.sides[i].move);
                                }

                                break;
                            }

                        case WrestlingMove.Chomp:
                            {
                                if (direct_damage[other] > 0)
                                {
                                    var amount = (direct_damage[other] / 2);
                                    if (amount < 1) amount = 1;
                                    recover += amount;
                                }
                                break;
                            }

                        case WrestlingMove.Bottle_Sip:

                            if (needsRecover)
                            {
                                IncreaseDrinkingCounter(ref battle, i, ref states, ref info);
                                recover += (int)(info[i].maxStamina * Constants.BOTTLE_SIP_RECOVER_PERCENT) / 100;
                            }
                            else
                            {
                                Runtime.Notify(NachoEvent.MoveMiss, battle.sides[i].address, battle.sides[i].move);
                            }
                            break;

                        case WrestlingMove.Gobble:

                            if (needsRecover)
                            {
                                recover += (int)(info[i].maxStamina * Constants.GOBBLE_RECOVER_PERCENT) / 100;
                            }
                            break;

                        case WrestlingMove.Mantra:
                            if (needsRecover)
                            {
                                recover += (int)(info[i].maxStamina * Constants.MANTRA_RECOVER_PERCENT) / 100;
                            }

                            break;
                    }

                    // only works if user is poisoned
                    if (states[i].itemKind == ItemKind.Nanobots && states[i].status.HasFlag(BattleStatus.Poisoned) && needsRecover && ActivateItem(ref states, ref info, i, true, false))
                    {
                        recover += (int)(info[i].maxStamina * (Constants.POISON_DAMAGE_PERCENT / 2)) / 100;
                    }

                    if (states[i].itemKind == ItemKind.Tequilla && needsRecover && ActivateItem(ref states, ref info, i, true, false))
                    {
                        recover += (int)(info[i].maxStamina * Constants.TEQUILLA_RECOVER_PERCENT) / 100;
                        IncreaseDrinkingCounter(ref battle, i, ref states, ref info);
                    }

                    // NOTE Wood Potato can recover stamina in 2 different cases
                    if (battle.sides[i].move == WrestlingMove.Wood_Work && states[i].itemKind == ItemKind.Wood_Potato && ActivateItem(ref states, ref info, i, true, false))
                    {
                        recover += (int)(info[i].maxStamina * Constants.WOOD_POTATO_RECOVER_PERCENT) / 100;
                    }

                    if (battle.sides[other].move == WrestlingMove.Smash && states[other].itemKind == ItemKind.Wood_Chair
                        && states[i].itemKind == ItemKind.Wood_Potato && ActivateItem(ref states, ref info, i, true, false))
                    {
                        recover += (int)(info[i].maxStamina * Constants.WOOD_POTATO_RECOVER_PERCENT) / 100;
                    }

                    var isGoingToDie = (direct_damage[i] + indirect_damage[i]) >= states[i].currentStamina;

                    if (isGoingToDie && (states[i].itemKind != ItemKind.Focus_Banana || !ActivateItem(ref states, ref info, i, false, false)))
                    {
                        recover = 0;
                    }

                    recover_damage[i] += recover;
                }

                // more indirect damage
                for (int i = 0; i < 2; i++)
                {
                    var extra = 0;
                    var other = 1 - i;

                    if (states[i].riggedMove == battle.sides[i].move)
                    {
                        extra += (int)(info[i].maxStamina * Constants.DYNAMITE_RIG_PERCENT) / 100;
                    }

                    if (states[i].itemKind == ItemKind.Nails && ActivateItem(ref states, ref info, i, true, false))
                    {
                        extra += (int)(info[i].maxStamina * Constants.NAILS_DAMAGE_PERCENT) / 100;
                    }

                    if (states[i].status.HasFlag(BattleStatus.Bleeding))
                    {
                        extra += (int)(info[i].maxStamina * Constants.BLEEDING_DAMAGE_PERCENT) / 100;
                    }

                    if (states[i].status.HasFlag(BattleStatus.Burned))
                    {
                        extra += (int)(info[i].maxStamina * Constants.BURNING_DAMAGE_PERCENT) / 100;
                    }

                    if (states[i].status.HasFlag(BattleStatus.Poisoned))
                    {
                        if (states[i].itemKind == ItemKind.Nanobots && ActivateItem(ref states, ref info, i, true, false))
                        {

                        }
                        else
                        {
                            var poisonDamagePercent = Constants.POISON_DAMAGE_PERCENT;

                            var poisonCounter = GetBattleCounter(ref battle, Constants.BATTLE_COUNTER_POISON_A + i);
                            if (poisonCounter > 0)
                            {
                                var extraPoison = poisonCounter * 2;
                                poisonDamagePercent += extraPoison;

                                if (extraPoison < Constants.POISON_DAMAGE_PERCENT)
                                {
                                    IncreaseBattleCounter(ref battle, Constants.BATTLE_COUNTER_POISON_A + i);
                                }
                            }

                            extra += (int)(info[i].maxStamina * poisonDamagePercent) / 100;
                        }
                    }

                    if (originalItems[i] != ItemKind.Bomb && states[i].itemKind == ItemKind.Bomb && ActivateItem(ref states, ref info, i, true, true))
                    {
                        extra += (int)(info[i].maxStamina * Constants.BOMB_DAMAGE_PERCENT) / 100;
                    }

                    if (states[i].itemKind == ItemKind.Fork && states[i].stance != states[i].lastStance && ActivateItem(ref states, ref info, i, true, false))
                    {
                        extra += (int)(info[i].maxStamina * Constants.FORK_DAMAGE_PERCENT) / 100;
                    }

                    indirect_damage[i] += extra;
                }

                // bright and drunk status check
                for (int i = 0; i < 2; i++)
                {
                    int other = 1 - i;

                    if (direct_damage[other] <= 0)
                    {
                        continue;
                    }

                    if (states[i].status.HasFlag(BattleStatus.Drunk))
                    {
                        direct_damage[other] += (direct_damage[other] * Constants.DRUNK_BOOST_PERCENT) / 100;
                    }

                    if (states[i].status.HasFlag(BattleStatus.Bright))
                    {
                        states[i].status &= ~BattleStatus.Bright; // clear flag 
                        direct_damage[other] *= 2;
                    }
                    else
                    if (battle.sides[i].move == WrestlingMove.Star_Gaze)
                    {
                        ApplyStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Bright, true);
                    }
                }

                // check damage items activation 
                for (int i = 0; i < 2; i++)
                {
                    var other = 1 - i;

                    switch (states[i].itemKind)
                    {
                        case ItemKind.Rubber_Suit:
                            if (direct_damage[i] > 0 && info[i].maxStamina < info[other].maxStamina)
                            {
                                if (battle.sides[other].move != WrestlingMove.Palm_Strike && ActivateItem(ref states, ref info, i, true, false))
                                {
                                    var half = direct_damage[i] / 2;
                                    if (half < 1) half = 1;
                                    direct_damage[i] = half;
                                }
                            }
                            break;

                        case ItemKind.Spike_Vest:
                            {
                                if (direct_damage[i] > 0 && ActivateItem(ref states, ref info, i, true, false))
                                {
                                    var recoil = (direct_damage[i] / 10);

                                    if (states[other].itemKind == ItemKind.Hand_Bandages)
                                    {
                                        recoil /= 2;
                                    }

                                    if (recoil < 1) recoil = 1;
                                    indirect_damage[other] += recoil;
                                }
                                break;
                            }

                        case ItemKind.Sombrero:
                            {
                                if (direct_damage[other] > 0 && ActivateItem(ref states, ref info, i, true, true))
                                {
                                    direct_damage[other] *= 2;
                                }
                                break;
                            }

                        case ItemKind.Poncho:
                            {
                                if (direct_damage[i] > 0 && ActivateItem(ref states, ref info, i, true, true))
                                {
                                    direct_damage[i] /= 2;
                                }
                                break;
                            }

                        case ItemKind.Yellow_Card:
                            if (direct_damage[i] >= (info[i].maxStamina / 2) && ActivateItem(ref states, ref info, i, true, false))
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Flinched, false);
                            }
                            break;
                    }

                    if (states[i].itemKind == ItemKind.Vampire_Teeth && direct_damage[other] > 0 && info[i].currentStamina < info[i].maxStamina && ActivateItem(ref states, ref info, i, true, false))
                    {
                        var extra = (direct_damage[other] * Constants.VAMPIRE_TEETH_RECOVER_PERCENT) / 100;
                        if (extra < 1) extra = 1;
                        recover_damage[i] += extra;
                    }

                }

                // confusion damage test
                for (int i = 0; i < 2; i++)
                {
                    var other = 1 - i;

                    if (battle.sides[i].move != WrestlingMove.Idle && confusion[i])
                    {
                        indirect_damage[i] += direct_damage[other];
                        direct_damage[other] = 0;
                        //Runtime.Notify(info[i].address, NachoEvent.Confusion);
                        Runtime.Notify(NachoEvent.Confusion, info[i].address, 0); // todo check this event
                    }
                }

                for (int i = 0; i < 2; i++)
                {
                    if (battle.sides[i].move == WrestlingMove.Bizarre_Ball)
                    {
                        var temp = direct_damage[0];
                        direct_damage[0] = direct_damage[1];
                        direct_damage[1] = temp;
                    }
                }

                for (var i = 0; i < 2; i++)
                {
                    var other = 1 - i;
                    if (states[i].itemKind == ItemKind.Shock_Chip && info[other].itemActivated)
                    {
                        indirect_damage[other] += (int)(Constants.SHOCK_CHIP_DAMAGE_PERCENT * info[other].maxStamina) / 100;
                    }
                }

                bool hasMaracas = states[0].itemKind == ItemKind.Maracas || states[1].itemKind == ItemKind.Maracas;
                bool hasBongos = states[0].itemKind == ItemKind.Bongos || states[1].itemKind == ItemKind.Bongos;

                for (int i = 0; i < 2; i++)
                {
                    var totalDamage = direct_damage[i] + indirect_damage[i];

                    if (hasMaracas && !hasBongos && totalDamage > 0)
                    {
                        totalDamage += (totalDamage * Constants.MARACAS_BOOST_PERCENTAGE) / 100;
                    }
                    else
                    if (!hasMaracas && hasBongos && totalDamage > 0)
                    {
                        totalDamage -= (totalDamage * Constants.BONGOS_BOOST_PERCENTAGE) / 100;
                        if (totalDamage < 1)
                        {
                            totalDamage = 1;
                        }
                    }

                    if (direct_damage[i] == 0)
                    {
                        IncreaseBattleCounter(ref battle, Constants.BATTLE_COUNTER_GORILLA_A + i);
                    }

                    var totalChange = totalDamage - recover_damage[i];

                    var target_stamina = states[i].currentStamina - totalChange;

                    if (target_stamina <= 0)
                    {
                        target_stamina = 0;
                        // clamp the direct damage
                        direct_damage[i] = (int)states[i].currentStamina - indirect_damage[i];

                        if (states[i].itemKind == ItemKind.Focus_Banana && ActivateItem(ref states, ref info, i, true, true))
                        {
                            target_stamina = 1;
                            states[i].status = BattleStatus.None;
                        }
                    }

                    if (target_stamina > info[i].maxStamina)
                    {
                        // clamp recover amount
                        var diff = target_stamina - info[i].maxStamina;
                        recover_damage[i] -= (int)diff;
                        if (recover_damage[i] < 0)
                        {
                            recover_damage[i] = 0;
                        }

                        target_stamina = info[i].maxStamina;
                    }


                    states[i].currentStamina = target_stamina;
                    var stamina_percent = (states[i].currentStamina * 100) / info[i].maxStamina;
                    // TODO should use the method to round the stamina value -> Formulas.CalculateStamina()

                    if (stamina_percent <= Constants.LOW_STAMINA_ITEM_PERCENT)
                    {
                        switch (states[i].itemKind)
                        {
                            case ItemKind.Power_Juice:
                                if (ActivateItem(ref states, ref info, i, true, true))
                                {
                                    ApplyStatBoost(ref info, ref states, i, StatKind.Attack, Constants.BIG_BOOST_CHANGE);
                                    IncreaseDrinkingCounter(ref battle, i, ref states, ref info);
                                }
                                break;

                            case ItemKind.Vigour_Juice:
                                if (ActivateItem(ref states, ref info, i, true, true))
                                {
                                    ApplyStatBoost(ref info, ref states, i, StatKind.Defense, Constants.BIG_BOOST_CHANGE);
                                    IncreaseDrinkingCounter(ref battle, i, ref states, ref info);
                                }
                                break;

                            case ItemKind.Trainer_Speaker:
                                {
                                    if (ActivateItem(ref states, ref info, i, true, true))
                                    {
                                        var recoverAmount = (info[i].maxStamina * Constants.TRAINER_SPEAKER_RECOVER_PERCENT) / 100;
                                        if (recoverAmount < 1) recoverAmount = 1;

                                        recover_damage[i] += (int)recoverAmount;
                                    }
                                    break;
                                }

                            case ItemKind.Lemon_Shake:
                                {
                                    if (ActivateItem(ref states, ref info, i, true, true))
                                    {
                                        var recoverAmount = (info[i].maxStamina * Constants.LEMON_SHAKE_RECOVER_PERCENT) / 100;
                                        states[i].currentStamina += recoverAmount;

                                        recover_damage[i] += (int)recoverAmount;

                                        ApplyStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Poisoned, true);

                                        IncreaseDrinkingCounter(ref battle, i, ref states, ref info);

                                        if (states[i].currentStamina > info[i].maxStamina) states[i].currentStamina = info[i].maxStamina;
                                    }
                                    break;
                                }
                        }
                    }

                    battle.sides[i].previousDirectDamage = direct_damage[i];
                    battle.sides[i].previousIndirectDamage = indirect_damage[i];
                    battle.sides[i].previousRecover = recover_damage[i];
                }

                // post turn move checks
                for (var i = 0; i < 2; i++)
                {
                    var other = 1 - i;

                    switch (battle.sides[i].move)
                    {
                        case WrestlingMove.Astral_Tango:
                            if (battle.sides[i].previousDirectDamage == 0 && battle.sides[i].previousIndirectDamage == 0)
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Bright, true);
                            }
                            break;

                        case WrestlingMove.Voodoo:
                            if (ChangeStance(ref states, ref info, i, false))
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Cursed, false);
                            }
                            break;

                        case WrestlingMove.Gorilla_Cannon:
                            if (GetBattleCounter(ref battle, Constants.BATTLE_COUNTER_GORILLA_A + i) > 0)
                            {
                                SetBattleCounter(ref battle, Constants.BATTLE_COUNTER_GORILLA_A + i, 0);
                            }
                            else
                            {
                                Runtime.Notify(NachoEvent.MoveMiss, battle.sides[i].address, battle.sides[i].move);
                            }
                            break;

                        case WrestlingMove.Dynamite_Arm:
                            if (states[other].riggedMove == WrestlingMove.Unknown)
                            {
                                states[other].riggedMove = battle.sides[other].move;
                            }
                            break;

                        case WrestlingMove.Knock_Off:
                            if (states[other].itemKind != ItemKind.None)
                            {
                                states[other].itemKind = ItemKind.None;
                                //Runtime.Notify(info[other].address, NachoEvent.ItemRemoved);
                                Runtime.Notify(NachoEvent.ItemRemoved, info[other].address, 0); // todo check this event arg = 0
                            }
                            break;

                        case WrestlingMove.Grip:
                            if (direct_damage[i] == 0)
                            {
                                ApplyStatusEffect(ref battle, ref states, ref info, other, BattleStatus.Flinched, false);
                            }
                            else
                            {
                                Runtime.Notify(NachoEvent.MoveMiss, battle.sides[i].address, battle.sides[i].move);
                            }
                            break;
                    }
                }

                for (var i = 0; i < 2; i++)
                {
                    var other = 1 - i;

                    if (states[i].status.HasFlag(BattleStatus.Smiling) && direct_damage[other] > 0 && battle.sides[other].move != WrestlingMove.Clown_Makeup)
                    {
                        if (states[i].stance != BattleStance.Clown && states[i].stance != BattleStance.Zombie)
                        {
                            RemoveStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Smiling);
                            states[i].stance = BattleStance.Clown;
                            Runtime.Notify(NachoEvent.Stance, info[i].address, states[i].stance);
                        }
                    }

                    // check for death activation items
                    if (states[i].currentStamina <= 0)
                    {
                        switch (states[i].itemKind)
                        {
                            case ItemKind.Virus_Chip:
                                if (ActivateItem(ref states, ref info, i, true, true))
                                {
                                    if (ApplyStatusEffect(ref battle, ref states, ref info, i, BattleStatus.Diseased, true))
                                    {
                                        states[i].status = BattleStatus.Diseased; // clear all other flags
                                        states[i].currentStamina = (info[i].maxStamina * Constants.ZOMBIE_RECOVER_PERCENT) / 100;
                                    }
                                }
                                break;

                            case ItemKind.Loser_Mask:
                                if (ActivateItem(ref states, ref info, i, true, true))
                                {
                                    states[0].currentStamina = info[0].maxStamina;
                                    states[1].currentStamina = info[1].maxStamina;
                                }
                                break;
                        }
                    }
                }
            }

            // this loop must be separate from all others and be the last one!!!
            for (int i = 0; i < 2; i++)
            {
                states[i].lastMove = battle.sides[i].move;
                states[i].lastStance = states[i].stance;

                var current = (int)battle.sides[i].current;
                battle.sides[i].wrestlers[current] = states[i];
            }

            if (battle.state == BattleState.Active)
            {
                // check for end of battle conditions
                if (states[0].currentStamina <= 0 && states[1].currentStamina <= 0)
                {
                    battle.state = BattleState.Draw;
                }
                else if (states[0].currentStamina <= 0 && states[1].currentStamina > 0)
                {
                    battle.state = BattleState.WinB;
                }
                else if (states[0].currentStamina > 0 && states[1].currentStamina <= 0)
                {
                    battle.state = BattleState.WinA;
                }
            }

            battle.turn++;
            battle.time = GetCurrentTime();
            battle.lastTurnHash = Runtime.Transaction != null ? Runtime.Transaction.Hash : Hash.Null; 

            if (battle.state != BattleState.Active)
            {
                // check for one hit victory
                if (Rules.IsModeWithMatchMaker(battle.mode))
                {
                    for (int i = 0; i < 2; i++)
                    {
                        int other = 1 - i;

                        if (info[i].currentStamina == info[i].maxStamina && states[i].currentStamina == 0)
                        {
                            AddTrophy(battle.sides[other].address, TrophyFlag.One_Hit);
                        }
                    }
                }

                // calculate average levels and terminate match
                var avgLevel = (info[0].level + info[1].level) / 2;
                TerminateMatch(battleID, battle, states[0], states[1], (int)avgLevel);
            }
            else
            {
                SetBattle(battleID, battle);
            }

            return true;
        }

        private bool ChangeStance(ref LuchadorBattleState[] states, ref WrestlerTurnInfo[] info, int target, bool forced)
        {
            if (states[target].status.HasFlag(BattleStatus.Diseased))
            {
                states[target].stance = BattleStance.Zombie;
                Runtime.Notify(NachoEvent.Stance, info[target].address, states[target].stance);
                return true;
            }

            var other = 1 - target;
            if (states[target].itemKind == ItemKind.Stance_Block || states[other].itemKind == ItemKind.Stance_Block)
            {
                return false;
            }

            BattleStance next;
            switch (states[target].stance)
            {
                case BattleStance.Main:
                    next = BattleStance.Alternative;
                    break;

                case BattleStance.Alternative:
                    if (states[target].itemKind == ItemKind.Ancient_Trinket && ActivateItem(ref states, ref info, target, true, false))
                        next = BattleStance.Bizarre;
                    else
                        next = BattleStance.Main;
                    break;

                case BattleStance.Clown:
                    return false;

                case BattleStance.Zombie:
                    return false;

                default:
                    next = BattleStance.Main;
                    break;
            }

            states[target].stance = next;
            Runtime.Notify(NachoEvent.Stance, info[target].address, states[target].stance);
            return true;
        }

        private bool ActivateItem(ref LuchadorBattleState[] states, ref WrestlerTurnInfo[] info, int target, bool shouldNotify, bool shouldConsume)
        {
            var other = 1 - target;

            if (states[other].itemKind == ItemKind.Nullifier)
            {
                Runtime.Notify(NachoEvent.ItemActivated, info[other].address, states[other].itemKind);
                return false;
            }

            if (shouldNotify)
            {
                Runtime.Notify(shouldConsume ? NachoEvent.ItemSpent : NachoEvent.ItemActivated, info[target].address, states[target].itemKind);
            }

            if (shouldConsume)
            {
                states[target].itemKind = ItemKind.None;
            }

            info[target].itemActivated = true;
            return true;
        }

        private void RemoveStatusEffect(ref NachoBattle battle, ref LuchadorBattleState[] states, ref WrestlerTurnInfo[] info, int target, BattleStatus status)
        {
            if (states[target].status.HasFlag(status))
            {
                states[target].status &= ~status; // clear flag 
                Runtime.Notify(NachoEvent.StatusRemoved, info[target].address, status);
            }
        }

        private bool ApplyStatusEffect(ref NachoBattle battle, ref LuchadorBattleState[] states, ref WrestlerTurnInfo[] info, int target, BattleStatus status, bool selfInflicted)
        {
            var source = selfInflicted ? target : 1 - target;

            if (states[target].status.HasFlag(status))
            {
                return false;
            }

            if (status == BattleStatus.Confused && states[target].itemKind == ItemKind.Spinner && ActivateItem(ref states, ref info, target, true, false))
            {
                return false;
            }

            if (status == BattleStatus.Poisoned && states[target].itemKind == ItemKind.Gas_Mask && ActivateItem(ref states, ref info, target, true, false))
            {
                return false;
            }

            /*if (states[target].itemKind == ItemKind.Ego_Mask && ActivateItem(ref states, ref info, target, true, false))
            {
                return false;
            }*/

            if (status == BattleStatus.Smiling || status == BattleStatus.Diseased)
            {
                if (states[target].itemKind == ItemKind.Serious_Mask && ActivateItem(ref states, ref info, target, true, false))
                {
                    return false;
                }

                if (status == BattleStatus.Diseased)
                {
                    states[target].stance = BattleStance.Zombie;
                    Runtime.Notify(NachoEvent.Stance, info[target].address, states[target].stance);
                }
            }

            if (!selfInflicted && states[target].itemKind == ItemKind.Magic_Mirror && !states[target].status.HasFlag(status)
                && ActivateItem(ref states, ref info, target, true, true))
            {
                states[source].status |= status;
                return false;
            }

            if (!selfInflicted && states[target].itemKind == ItemKind.Echo_Box && !states[target].status.HasFlag(status)
                && ActivateItem(ref states, ref info, target, false, false))
            {
                states[source].status |= status;
            }

            if (status == BattleStatus.Poisoned && states[source].itemKind == ItemKind.Toxin && ActivateItem(ref states, ref info, source, true, false))
            {
                SetBattleCounter(ref battle, Constants.BATTLE_COUNTER_POISON_A + target, 1);
            }

            if (Rules.IsNegativeStatus(status) && states[target].itemKind == ItemKind.Astral_Trinket && !states[target].status.HasFlag(BattleStatus.Bright) && ActivateItem(ref states, ref info, target, true, false))
            {
                states[target].status |= BattleStatus.Bright;
            }

            states[target].status |= status;
            Runtime.Notify(NachoEvent.Stance, info[target].address, status);
            return true;
        }

        // TODO put me in the proper code region
        private bool ApplyStatBoost(ref WrestlerTurnInfo[] info, ref LuchadorBattleState[] states, int target, StatKind stat, int boost, bool triggerItems = true)
        {
            if (triggerItems && boost < 0 && states[target].itemKind == ItemKind.Ego_Mask && ActivateItem(ref states, ref info, target, true, false))
            {
                return false;
            }

            switch (stat)
            {
                case StatKind.Attack:
                    if (states[target].boostAtk >= 300)
                    {
                        return false;
                    }

                    states[target].boostAtk += boost;
                    break;

                case StatKind.Defense:
                    if (states[target].boostDef >= 300)
                    {
                        return false;
                    }

                    states[target].boostDef += boost;
                    break;

                default: throw new ContractException("invalid stat");
            }

            int other = 1 - target;
            if (triggerItems && boost > 0 && states[other].itemKind == ItemKind.Envy_Mask && ActivateItem(ref states, ref info, other, true, false))
            {
                ApplyStatBoost(ref info, ref states, other, stat, boost, false);
            }

            Runtime.Notify(boost > 0 ? NachoEvent.Buff : NachoEvent.Debuff, info[target].address, stat);

            return true;
        }

        #endregion

        #region tourney API
        // returns number of tourneys
        public BigInteger GettourneyCount()
        {
            throw new System.NotImplementedException();
        }

        // returns number of tourneys
        public string GettourneyName(BigInteger tourID)
        {
            throw new System.NotImplementedException();
        }
        #endregion

        #region Daily Rewards

        public DailyRewards GetDailyRewards(Address address)
        {
            // TODO finish implementation

            var account = GetAccount(address);

            var vipPoints = account.vipPoints;

            var vipLevel = 0;

            for (int i = Constants.VIP_LEVEL_POINTS.Length - 1; i >= 0; i--)
            {
                if (vipPoints >= Constants.VIP_LEVEL_POINTS[i])
                {
                    vipLevel = i;
                    break;
                }
            }

            var rewards = Constants.VIP_DAILY_LOOT_BOX_REWARDS[vipLevel];

            var nachosReward    = 10; // Player Nb of Battles / Total Nb of Battles of All Players ) * Amount of Nachos to distribute
            var soulReward      = 10; // Player Nb of Battles / Total Nb of Battles of All Players ) * Amount of Nachos to distribute
            
            rewards.factionReward       = nachosReward;
            rewards.championshipReward  = soulReward;

            return rewards;
        }

        public void CollectFactionReward(Address address)
        {
            // TODO finish implementation

            var account = GetAccount(address);

            var rewards = GetDailyRewards(address);

            Runtime.Expect(rewards.factionReward > 0, "no faction reward");

            rewards.factionReward = 0;

            // save current rewards left somewhere. Add NachoRewards to NachoAccount?

            Runtime.Notify(NachoEvent.CollectFactionReward, address, 1);
        }

        private void CollectChampionshipReward(Address address)
        {
            // TODO finish implementation

            var account = GetAccount(address);

            var rewards = GetDailyRewards(address);

            Runtime.Expect(rewards.championshipReward > 0, "no championship reward");

            rewards.championshipReward = 0;

            // save current rewards left somewhere. Add NachoRewards to NachoAccount?

            Runtime.Notify(NachoEvent.CollectChampionshipReward, address, 1);
        }

        private void CollectVipWrestlerReward(Address address)
        {
            // TODO finish implementation

            var account = GetAccount(address);

            var rewards = GetDailyRewards(address);

            Runtime.Expect(rewards.vipWrestlerReward > 0, "no vip wrestler reward");

            rewards.vipWrestlerReward--;

            // save current rewards left somewhere. Add NachoRewards to NachoAccount?

            Runtime.Notify(NachoEvent.CollectVipWrestlerReward, address, 1);
        }

        private void CollectVipItemReward(Address address)
        {
            // TODO finish implementation

            var account = GetAccount(address);

            var rewards = GetDailyRewards(address);

            Runtime.Expect(rewards.vipItemReward > 0, "no vip item reward");

            rewards.vipItemReward--;

            // save current rewards left somewhere. Add NachoRewards to NachoAccount?

            Runtime.Notify(NachoEvent.CollectVipItemReward, address, 1);
        }

        private void CollectVipMakeUpReward(Address address)
        {
            // TODO finish implementation

            var account = GetAccount(address);

            var rewards = GetDailyRewards(address);

            Runtime.Expect(rewards.vipMakeUpReward > 0, "no vip make up reward");

            rewards.vipMakeUpReward--;

            // save current rewards left somewhere. Add NachoRewards to NachoAccount?

            Runtime.Notify(NachoEvent.CollectVipMakeUpReward, address, 1);
        }

        #endregion

        #region SOUL API
        /*
                private bool DepositNEP5(Address address, BigInteger amount)
                {
                    return Transfer(address, this.Address, amount);
                }

                private bool WithdrawNEP5(Address address, BigInteger amount)
                {
                    return Transfer(this.Address, address, amount);
                }

                // function that is always called when someone wants to transfer tokens.
                public bool Transfer(Address from, Address to, BigInteger value)
                {
                    Runtime.Expect(value > 0, "negative value");
                    Runtime.Expect(from != to, "same address");

                    Runtime.Expect(IsWitness(from), "witness failed");

                    var balances = Storage.FindMapForContract<Address, BigInteger>(TOKEN_BALANCE_MAP);

                    BigInteger from_balance = balances.ContainsKey(from) ? balances.Get(from) : 0;
                    Runtime.Expect(from_balance >= value, "funds failed");

                    from_balance -= value;

                    if (from_balance > 0)
                    {
                        balances.Set(from, from_balance);
                    }
                    else
                    {
                        balances.Delete(from);
                    }

                    BigInteger to_balance = balances.ContainsKey(to) ? balances.Get(to) : 0;
                    to_balance += value;
                    balances.Set(to, to_balance);

                    Runtime.Notify(from, NachoEvent.Transfer, value);
                    return true;
                }

                // Get the account balance of another account with address
                public BigInteger BalanceOf(Address address)
                {
                    var balances = Storage.FindMapForContract<Address, BigInteger>(TOKEN_BALANCE_MAP);
                    if (balances.ContainsKey(address))
                    {
                        return balances.Get(address);
                    }

                    return 0;
                }

                // initialization parameters, only once
                public bool Deploy(Address to)
                {
                    // Only Team/Admmin/Owner can deploy
                    Runtime.Expect(IsWitness(DevelopersAddress), "developer only");

                    var balances = Storage.FindMapForContract<Address, BigInteger>(TOKEN_BALANCE_MAP);
                    Runtime.Expect(balances.Count() == 0, "already deployed");

                    var totalSupply = UnitConversion.ToBigInteger(91136374);
                    balances.Set(to, totalSupply);
                    Runtime.Notify(to, NachoEvent.Transfer, totalSupply);

                    return true;
                }
                */
        #endregion

        #region PROTOCOL
        private Timestamp GetCurrentTime()
        {
            return Runtime.Time;
        }
        #endregion

        /* TODO LATER
        public void ValidatePurchase(Address address, string hash)
        {
            Runtime.Expect(IsWitness(address), "invalid witness");

            var map = Storage.FindMapForContract<string, BigInteger>(PURCHASE_MAP);

            // check if already in map, to avoid credit the same TX hash more than once
            if (map.ContainsKey(hash))
            {
                var amount = map.Get(hash);
                Runtime.Notify(EventKind.Transfer, address, amount);
            }
        }*/

        public NachoConfig GetConfig()
        {
            var pot = GetPot();

            return new NachoConfig()
            {
                //rankedFee = UnitConversion.ToBigInteger(5, Nexus.StakingTokenDecimals),
                time = GetCurrentTime(),
                suspendedTransfers = SuspendTransfers
            };
        }

        public string GetMOTD()
        {
            //var motd = Storage.Get<string>(MOTD_KEY);
            //if (motd == null || motd.Length == 0)
            //{
            //    return "Welcome to the oficial public release of Nacho Men!";
            //}

            var motd = Storage.Has(MOTD_KEY) ? "Welcome to the oficial public release of Nacho Men!" : string.Empty	;

            return motd;
        }

        public void SetMOTD(string motd)
        {
            Runtime.Expect(IsWitness(DevelopersAddress), "developrs only");

            Storage.Put(MOTD_KEY, motd);
        }

        /// the result of this should be always MajorVersion * 256 + MinorVersion
        public BigInteger GetProtocolVersion()
        {
            return 1 * 256 + 9;
        }
    }
}
