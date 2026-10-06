using UnityEngine;

namespace CoreBeasts.Battle.UI
{
    /// <summary>
    /// バトル画面の表示文字列をまとめたアセット。
    ///
    /// 既存の<see cref="CoreBeasts.Units.UiTextCatalog"/>は編成画面のものです。
    /// そちらを書き換えず、バトル固有の文言だけをここへ分けています。
    /// 言語を増やすときはこのアセットを複製して差し替えます。
    ///
    /// 文言は短い英語を基本とし、説明文へ依存しません。
    /// 状況は色・数値・状態変化で読み取れるようにします。
    /// </summary>
    [CreateAssetMenu(
        fileName = "BattleTextCatalog_EN",
        menuName = "CoreBeasts/Battle Text Catalog"
    )]
    public sealed class BattleTextCatalog : ScriptableObject, IBattleTextSource
    {
        [Header("Screen")]
        [SerializeField] private string battle = "BATTLE";
        [SerializeField] private string deploy = "DEPLOY";
        [SerializeField] private string versus = "VS";
        [SerializeField] private string player = "PLAYER";
        [SerializeField] private string cpu = "CPU";

        [Header("Hidden opponent")]
        [Tooltip("CPUが選出済みであることだけを示します。個体は特定できません。")]
        [SerializeField] private string ready = "READY";
        [SerializeField] private string hidden = "?";
        [SerializeField] private string used = "USED";
        [SerializeField] private string remainingFormat = "LEFT {0} / {1}";

        [Header("Score")]
        [SerializeField] private string roundFormat = "ROUND {0} / {1}";
        [SerializeField] private string scoreFormat = "PLAYER {0}  -  {1} CPU";

        [Header("Round result")]
        [SerializeField] private string attributeWin = "ATTRIBUTE WIN";
        [SerializeField] private string powerWin = "POWER WIN";
        [SerializeField] private string roundDraw = "DRAW";
        [SerializeField] private string playerWin = "PLAYER WIN";
        [SerializeField] private string cpuWin = "CPU WIN";
        [SerializeField] private string matchDraw = "DRAW";
        [SerializeField] private string unitSummaryFormat = "{0}  {1}";

        [Header("Attribute link")]
        [Tooltip("戦闘中・選択前予告の LINK ボーナス（{0}=ボーナス）。")]
        [SerializeField] private string linkBonusFormat = "同じ属性で POWER +{0}";

        [Tooltip("LINK 演出の文字（{0}=実チェーン数, {1}=ボーナス）。2行で出します。")]
        [SerializeField] [TextArea(2, 3)] private string linkCueFormat = "属性リンク {0}連鎖！\nPOWER +{1}";

        [Tooltip("LINK 加算を受けた側の比較値（{0}=基礎値, {1}=加算値）。加算値はその数値のすぐ後ろに、少し小さく付けます（両側に付いても1行に収まるように）。")]
        [SerializeField] private string linkedPowerFormat = "{0}<size=70%>（リンク+{1}）</size>";

        [Tooltip("LINK 加算の無い側の比較値（{0}=値）。")]
        [SerializeField] private string unlinkedPowerFormat = "{0} ";

        [Tooltip("LINK が絡んだ POWER 決着の理由（{0}=PLAYER の比較値, {1}=CPU の比較値）。")]
        [SerializeField] private string linkPowerDecisionFormat = "POWER勝利  {0}対 {1}";

        [Header("Unique skill")]
        [Tooltip("自分の POWER が上がるスキル（{0}=加算）。選択前予告と戦闘中表示。")]
        [SerializeField] private string skillSelfBonusFormat = "スキル発動 POWER +{0}";

        [Tooltip("相手の POWER を下げるスキル（{0}=減算）。選択前予告と戦闘中表示。")]
        [SerializeField] private string skillOpponentPenaltyFormat = "スキル発動 相手の POWER -{0}";

        [Tooltip("スキル発動の演出（{0}=スキル名, {1}=2行目）。2行で出します。")]
        [SerializeField] [TextArea(2, 3)] private string skillCueFormat = "{0} 発動\n{1}";

        [Tooltip("CRIMSON BITE の2行目（{0}=加算）。")]
        [SerializeField] private string crimsonBiteCueFormat = "同じ属性の流れで POWER +{0}";

        [Tooltip("TIDAL HOWL の2行目（{0}=減算）。")]
        [SerializeField] private string tidalHowlCueFormat = "相手の POWER -{0}";

        [Tooltip("VERDANT FANG の2行目（{0}=加算）。")]
        [SerializeField] private string verdantFangCueFormat = "直前の敗北で POWER +{0}";

        [Tooltip("STORM BITE の2行目（{0}=加算）。")]
        [SerializeField] private string stormBiteCueFormat = "相手がREDかBLUEで POWER +{0}";

        [Tooltip("内訳つきの比較値（{0}=基礎値, {1}=内訳）。内訳はその数値のすぐ後ろに、少し小さく付けます。")]
        [SerializeField] private string powerBreakdownFormat = "{0}<size=70%>（{1}）</size>";

        [Tooltip("内訳: LINK の加算（{0}=加算）。")]
        [SerializeField] private string linkPartFormat = "リンク+{0}";

        [Tooltip("内訳: 自分のスキルの加算（{0}=加算）。")]
        [SerializeField] private string skillPartFormat = "スキル+{0}";

        [Tooltip("内訳: 相手のスキルから受けた減算（{0}=減算）。")]
        [SerializeField] private string penaltyPartFormat = "妨害-{0}";

        [Tooltip("内訳どうしの区切り。")]
        [SerializeField] private string breakdownSeparator = "／";

        [Header("Match end")]
        [SerializeField] private string rematch = "REMATCH";

        [Header("Squad required")]
        [SerializeField] private string squadRequired = "SQUAD REQUIRED";
        [SerializeField] private string squadRequiredHint = "SET 7 CORE BEASTS";

        [Header("Effects")]
        [SerializeField] private string fxOn = "FX ON";
        [SerializeField] private string fxOff = "FX OFF";

        [Header("Settings panel")]
        [Tooltip("右上の歯車から開く設定パネルの文言です。")]
        [SerializeField] private string settings = "SETTINGS";
        [SerializeField] private string fx = "FX";
        [Tooltip("ON / OFF の値表示。YAMLの予約語を避けるためフィールド名を分けています。")]
        [SerializeField] private string valueOn = "ON";
        [SerializeField] private string valueOff = "OFF";
        [SerializeField] private string close = "CLOSE";
        [SerializeField] private string home = "HOME";

        public string Battle => battle;

        public string Deploy => deploy;

        public string Ready => ready;

        public string Hidden => hidden;

        public string Versus => versus;

        public string FxOn => fxOn;

        public string FxOff => fxOff;

        public string Settings => settings;

        public string Fx => fx;

        public string On => valueOn;

        public string Off => valueOff;

        public string Close => close;

        public string Home => home;

        public string Player => player;

        public string Cpu => cpu;

        public string Rematch => rematch;

        public string SquadRequired => squadRequired;

        public string SquadRequiredHint => squadRequiredHint;

        public string Used => used;

        public string AttributeWin => attributeWin;

        public string PowerWin => powerWin;

        public string RoundDraw => roundDraw;

        public string PlayerWin => playerWin;

        public string CpuWin => cpuWin;

        public string MatchDraw => matchDraw;

        public string FormatRound(int round, int maxRounds)
        {
            return SafeFormat(roundFormat, round, maxRounds);
        }

        public string FormatScore(int playerWins, int cpuWins)
        {
            return SafeFormat(scoreFormat, playerWins, cpuWins);
        }

        public string FormatRemaining(int remaining, int total)
        {
            return SafeFormat(remainingFormat, remaining, total);
        }

        public string FormatUnitSummary(string attributeSymbol, string powerLine)
        {
            return SafeFormat(unitSummaryFormat, attributeSymbol, powerLine);
        }

        public string FormatLinkBonus(int bonusPower)
        {
            return SafeFormat(linkBonusFormat, bonusPower);
        }

        public string FormatLinkCue(int chainCount, int bonusPower)
        {
            return SafeFormat(linkCueFormat, chainCount, bonusPower);
        }

        public string FormatLinkedPower(int basePower, int bonusPower)
        {
            return SafeFormat(linkedPowerFormat, basePower, bonusPower);
        }

        public string FormatUnlinkedPower(int power)
        {
            return SafeFormat(unlinkedPowerFormat, power);
        }

        public string FormatLinkPowerDecision(string playerPower, string cpuPower)
        {
            return SafeFormat(linkPowerDecisionFormat, playerPower, cpuPower).TrimEnd();
        }

        public string FormatSkillSelfBonus(int bonusPower)
        {
            return SafeFormat(skillSelfBonusFormat, bonusPower);
        }

        public string FormatSkillOpponentPenalty(int penaltyPower)
        {
            return SafeFormat(skillOpponentPenaltyFormat, penaltyPower);
        }

        public string FormatSkillCue(string skillName, CoreBeasts.Units.UniqueSkillKind kind, int value)
        {
            string line;

            switch (kind)
            {
                case CoreBeasts.Units.UniqueSkillKind.CrimsonBite:
                    line = SafeFormat(crimsonBiteCueFormat, value);
                    break;
                case CoreBeasts.Units.UniqueSkillKind.TidalHowl:
                    line = SafeFormat(tidalHowlCueFormat, value);
                    break;
                case CoreBeasts.Units.UniqueSkillKind.VerdantFang:
                    line = SafeFormat(verdantFangCueFormat, value);
                    break;
                case CoreBeasts.Units.UniqueSkillKind.StormBite:
                    line = SafeFormat(stormBiteCueFormat, value);
                    break;
                default:
                    return string.Empty;
            }

            return SafeFormat(skillCueFormat, skillName ?? string.Empty, line);
        }

        public string FormatPowerBreakdown(int basePower, string parts)
        {
            return SafeFormat(powerBreakdownFormat, basePower, parts ?? string.Empty);
        }

        public string FormatLinkPart(int bonusPower)
        {
            return SafeFormat(linkPartFormat, bonusPower);
        }

        public string FormatSkillPart(int bonusPower)
        {
            return SafeFormat(skillPartFormat, bonusPower);
        }

        public string FormatPenaltyPart(int penaltyPower)
        {
            return SafeFormat(penaltyPartFormat, penaltyPower);
        }

        public string BreakdownSeparator => breakdownSeparator;

        /// <summary>書式が壊れていても例外を出さずに素の値を返します。</summary>
        private static string SafeFormat(string format, params object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return args != null && args.Length > 0 && args[0] != null
                    ? args[0].ToString()
                    : string.Empty;
            }

            try
            {
                return string.Format(format, args);
            }
            catch (System.FormatException)
            {
                return format;
            }
        }
    }
}
