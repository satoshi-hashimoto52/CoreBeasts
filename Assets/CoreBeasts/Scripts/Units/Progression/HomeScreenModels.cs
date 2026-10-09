using System.Collections.Generic;
using System.Globalization;
using System.Text;

using CoreBeasts.Units;

namespace CoreBeasts.Progression
{
    /// <summary>
    /// Home・報酬・ガチャ・獲得画面の文言（Phase 7）。画面はここを通して文字を作り、コードの各所へ直書きしません。
    /// 英字・数字と、LiberationSans SDF に収録済みの記号（/ > + -）だけを使います。
    /// </summary>
    public static class HomeText
    {
        public const string Title = "CORE BEASTS";
        public const string Subtitle = "MECHANICAL LIFE COMMAND";
        public const string CoinCaption = "CORE COIN";
        public const string SquadCaption = "SQUAD";
        public const string OwnedCaption = "OWNED";
        public const string RecordCaption = "RECORD";
        public const string Battle = "BATTLE";
        public const string UnitSet = "UNIT SET";
        public const string Gacha = "GACHA";
        public const string Collection = "COLLECTION";
        public const string Settings = "SETTINGS";
        public const string Close = "CLOSE";
        public const string FxOn = "FX  ON";
        public const string FxOff = "FX  OFF";
        public const string Home = "HOME";
        public const string Back = "BACK";
        public const string Continue = "CONTINUE";
        public const string ToGacha = "TO GACHA";
        public const string ToCollection = "TO COLLECTION";
        public const string ToUnitSet = "TO UNIT SET";
        public const string MissionComplete = "MISSION COMPLETE";
        public const string Win = "WIN";
        public const string Draw = "DRAW";
        public const string Loss = "LOSS";
        public const string Summon = "CORE SUMMON";
        public const string SummonSubtitle = "ANALYZE A MECHANICAL LIFE CORE SIGNAL";
        public const string NewGuaranteed = "NEW GUARANTEED";
        public const string DuplicatesAvailable = "DUPLICATES AVAILABLE";
        public const string Summoning = "SUMMONING";
        public const string NewCoreBeast = "NEW CORE BEAST";
        public const string Duplicate = "DUPLICATE";
        public const string Level = "LV";
        public const string Power = "POWER";
        public const string Core = "CORE";
        public const string Copies = "COPIES";

        public static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>例: OWNED 8 / 8</summary>
        public static string Owned(int owned, int total)
        {
            return OwnedCaption + " " + Number(owned) + " / " + Number(total);
        }

        /// <summary>例: W 3  D 1  L 2</summary>
        public static string Record(int wins, int draws, int losses)
        {
            return "W " + Number(wins) + "  D " + Number(draws) + "  L " + Number(losses);
        }

        /// <summary>例: SET 1</summary>
        public static string SquadSet(string setId)
        {
            return "SET " + (string.IsNullOrEmpty(setId) ? "1" : setId);
        }

        /// <summary>例: ACTIVATE 100</summary>
        public static string Activate(int cost)
        {
            return "ACTIVATE  " + Number(cost);
        }

        /// <summary>例: COST 100</summary>
        public static string Cost(int cost)
        {
            return "COST " + Number(cost);
        }

        /// <summary>例: NEED 40 MORE CORE COIN</summary>
        public static string Shortfall(int amount)
        {
            return "NEED " + Number(amount) + " MORE " + CoinCaption;
        }

        /// <summary>例: +30 CORE COIN</summary>
        public static string Earned(int amount)
        {
            return "+" + Number(amount) + " " + CoinCaption;
        }

        /// <summary>例: 3 BATTLES</summary>
        public static string Battles(int count)
        {
            return Number(count) + (count == 1 ? " BATTLE" : " BATTLES");
        }

        /// <summary>例: POOL 8  /  UNOWNED 1</summary>
        public static string Pool(int total, int unowned)
        {
            return "POOL " + Number(total) + "  /  UNOWNED " + Number(unowned);
        }
    }

    /// <summary>Home 画面の表示モデル（Phase 7）。プロフィールと編成から毎回作り直します。</summary>
    public sealed class HomeSummary
    {
        public HomeSummary(int coins, int battles, int wins, int draws, int losses, string squadSetId, int owned, int catalogCount)
        {
            Coins = coins;
            Battles = battles;
            Wins = wins;
            Draws = draws;
            Losses = losses;
            SquadSetId = string.IsNullOrEmpty(squadSetId) ? "1" : squadSetId;
            OwnedCount = owned;
            CatalogCount = catalogCount;
        }

        public int Coins { get; }
        public int Battles { get; }
        public int Wins { get; }
        public int Draws { get; }
        public int Losses { get; }
        public string SquadSetId { get; }
        public int OwnedCount { get; }
        public int CatalogCount { get; }

        public string CoinText => HomeText.Number(Coins);
        public string RecordText => HomeText.Record(Wins, Draws, Losses);
        public string SquadText => HomeText.SquadSet(SquadSetId);
        public string OwnedText => HomeText.Owned(OwnedCount, CatalogCount);

        public static HomeSummary Build(PlayerProfile profile, CoreBeastRoster catalog, string squadSetId)
        {
            if (profile == null)
            {
                return new HomeSummary(0, 0, 0, 0, 0, squadSetId, 0, CatalogCountOf(catalog));
            }

            return new HomeSummary(
                profile.Coins, profile.Battles, profile.Wins, profile.Draws, profile.Losses,
                squadSetId, OwnedCountOf(profile, catalog), CatalogCountOf(catalog));
        }

        internal static int CatalogCountOf(CoreBeastRoster catalog)
        {
            if (catalog == null)
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<OwnedCoreBeast> entries = catalog.Owned;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].IsValid)
                {
                    count++;
                }
            }

            return count;
        }

        internal static int OwnedCountOf(PlayerProfile profile, CoreBeastRoster catalog)
        {
            if (profile == null || catalog == null)
            {
                return 0;
            }

            int count = 0;
            IReadOnlyList<OwnedCoreBeast> entries = catalog.Owned;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].IsValid && profile.Owns(entries[i].InstanceId))
                {
                    count++;
                }
            }

            return count;
        }
    }

    /// <summary>
    /// 報酬画面の表示モデル（Phase 7）。報酬は試合決着時に保存済みで、ここは確認だけに使います。
    /// 未確認の試合が複数あれば合計と試合数を持ちます。
    /// </summary>
    public sealed class RewardSummary
    {
        public RewardSummary(int totalReward, int battles, int wins, int draws, int losses, int balanceAfter)
        {
            TotalReward = totalReward < 0 ? 0 : totalReward;
            Battles = battles < 0 ? 0 : battles;
            Wins = wins;
            Draws = draws;
            Losses = losses;
            BalanceAfter = balanceAfter < 0 ? 0 : balanceAfter;
        }

        public int TotalReward { get; }
        public int Battles { get; }
        public int Wins { get; }
        public int Draws { get; }
        public int Losses { get; }

        /// <summary>現在の残高（報酬を加算した後）。</summary>
        public int BalanceAfter { get; }

        /// <summary>報酬を加算する前の残高。</summary>
        public int BalanceBefore => BalanceAfter - TotalReward < 0 ? 0 : BalanceAfter - TotalReward;

        /// <summary>表示する未確認の報酬があるか。</summary>
        public bool HasReward => TotalReward > 0;

        /// <summary>ガチャを1回引けるか。</summary>
        public bool CanAffordGacha => BalanceAfter >= GameEconomy.GachaCost;

        /// <summary>ガチャに足りない額。</summary>
        public int GachaShortfall => CanAffordGacha ? 0 : GameEconomy.GachaCost - BalanceAfter;

        /// <summary>WIN / DRAW / LOSS。複数試合なら「3 BATTLES」。勝敗が分からなければ「1 BATTLE」。</summary>
        public string OutcomeText
        {
            get
            {
                if (Battles == 1)
                {
                    if (Wins == 1)
                    {
                        return HomeText.Win;
                    }

                    if (Draws == 1)
                    {
                        return HomeText.Draw;
                    }

                    if (Losses == 1)
                    {
                        return HomeText.Loss;
                    }
                }

                return HomeText.Battles(Battles);
            }
        }

        /// <summary>複数試合のときの内訳（例: W 1  D 1  L 1）。1試合なら空です。</summary>
        public string BreakdownText =>
            Battles > 1 && Wins + Draws + Losses == Battles ? HomeText.Record(Wins, Draws, Losses) : string.Empty;

        public string EarnedText => HomeText.Earned(TotalReward);

        public string GachaReasonText => CanAffordGacha ? string.Empty : HomeText.Shortfall(GachaShortfall);
    }

    /// <summary>ガチャ画面の表示モデル（Phase 7）。</summary>
    public sealed class GachaSummary
    {
        public GachaSummary(int balance, int catalogCount, int unownedCount)
        {
            Balance = balance < 0 ? 0 : balance;
            CatalogCount = catalogCount;
            UnownedCount = unownedCount;
        }

        public int Balance { get; }
        public int Cost => GameEconomy.GachaCost;
        public int CatalogCount { get; }
        public int UnownedCount { get; }

        /// <summary>未所持が残っている間は必ず新規個体が出ます。</summary>
        public bool IsNewGuaranteed => UnownedCount > 0;

        public bool CanActivate => Balance >= Cost && CatalogCount > 0;

        public int Shortfall => Balance >= Cost ? 0 : Cost - Balance;

        public string PoolStatusText => IsNewGuaranteed ? HomeText.NewGuaranteed : HomeText.DuplicatesAvailable;

        public string PoolText => HomeText.Pool(CatalogCount, UnownedCount);

        public string ActivateText => HomeText.Activate(Cost);

        public string CostText => HomeText.Cost(Cost);

        public string ReasonText => Shortfall > 0 ? HomeText.Shortfall(Shortfall) : string.Empty;

        public static GachaSummary Build(PlayerProfile profile, CoreBeastRoster catalog)
        {
            int total = HomeSummary.CatalogCountOf(catalog);
            int owned = HomeSummary.OwnedCountOf(profile, catalog);

            return new GachaSummary(profile != null ? profile.Coins : 0, total, total - owned);
        }
    }

    /// <summary>獲得画面の表示モデル（Phase 7）。ガチャの確定結果を写すだけで、能力値は変えません。</summary>
    public sealed class AcquisitionSummary
    {
        private AcquisitionSummary()
        {
        }

        public bool IsValid { get; private set; }
        public bool IsNew { get; private set; }
        public string InstanceId { get; private set; }
        public CoreBeastDefinition Definition { get; private set; }
        public string Name { get; private set; }
        public int Level { get; private set; }
        public string AttributeText { get; private set; }
        public string PowerText { get; private set; }
        public int Core { get; private set; }
        public string SkillName { get; private set; }
        public string SkillDescription { get; private set; }
        public int CopiesBefore { get; private set; }
        public int CopiesAfter { get; private set; }
        public int OwnedCount { get; private set; }
        public int CatalogCount { get; private set; }
        public int RemainingCoins { get; private set; }

        public string TitleText => IsNew ? HomeText.NewCoreBeast : HomeText.Duplicate;
        public string LevelText => HomeText.Level + " " + HomeText.Number(Level);
        public string CoreText => HomeText.Number(Core);
        public string OwnedText => HomeText.Owned(OwnedCount, CatalogCount);

        /// <summary>新しく所持した個体だけ、編成への導線を出します。</summary>
        public bool ShowsUnitSetLink => IsNew;

        public static AcquisitionSummary Invalid => new AcquisitionSummary { IsValid = false, Name = string.Empty };

        public static AcquisitionSummary Build(GachaResult result, PlayerProfile profile, CoreBeastRoster catalog)
        {
            if (result == null || result.Beast == null || result.Beast.Definition == null)
            {
                return Invalid;
            }

            CoreBeastDefinition definition = result.Beast.Definition;

            return new AcquisitionSummary
            {
                IsValid = true,
                IsNew = result.IsNew,
                InstanceId = result.Beast.InstanceId,
                Definition = definition,
                Name = definition.DisplayName,
                Level = result.Beast.Level,
                AttributeText = AttributesOf(definition),
                PowerText = PowersOf(definition),
                Core = definition.Core,
                SkillName = definition.SkillName,
                SkillDescription = definition.SkillDescription,
                CopiesAfter = result.Copies,
                CopiesBefore = result.Copies - 1 < 0 ? 0 : result.Copies - 1,
                OwnedCount = HomeSummary.OwnedCountOf(profile, catalog),
                CatalogCount = HomeSummary.CatalogCountOf(catalog),
                RemainingCoins = result.RemainingCoins,
            };
        }

        /// <summary>例: RED / BLUE</summary>
        public static string AttributesOf(CoreBeastDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            string primary = Upper(definition.PrimaryAttribute);

            return definition.HasSecondaryAttribute ? primary + " / " + Upper(definition.SecondaryAttribute) : primary;
        }

        /// <summary>例: 40 / 21（属性の並びと同じ順）</summary>
        public static string PowersOf(CoreBeastDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            IReadOnlyList<AttributePower> powers = definition.AttributePowers;

            for (int i = 0; i < powers.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(" / ");
                }

                builder.Append(HomeText.Number(powers[i].Power));
            }

            return builder.ToString();
        }

        private static string Upper(UnitAttribute attribute)
        {
            return attribute.ToString().ToUpperInvariant();
        }
    }
}
