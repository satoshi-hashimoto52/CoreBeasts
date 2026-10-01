using System.Collections.Generic;
using System.Reflection;

using CoreBeasts.Units;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// 表示コンポーネントを組み立てるテスト用ヘルパー。
    /// シーンを開かずに配線済みのViewを作り、破棄まで面倒を見ます。
    /// 本番コードへテスト専用のセッターを足さないよう、参照は直接入れます。
    /// </summary>
    internal sealed class TestBattleViews
    {
        private const BindingFlags FieldFlags =
            BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly List<GameObject> created = new List<GameObject>();

        /// <summary>空のUIオブジェクトを作ります。</summary>
        internal GameObject CreateObject(string name, Transform parent = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));

            if (parent != null)
            {
                go.transform.SetParent(parent, false);
            }
            else
            {
                created.Add(go);
            }

            return go;
        }

        internal Image CreateImage(string name, Transform parent)
        {
            return CreateObject(name, parent).AddComponent<Image>();
        }

        internal TMP_Text CreateLabel(string name, Transform parent)
        {
            return CreateObject(name, parent).AddComponent<TextMeshProUGUI>();
        }

        /// <summary>既存の縮小立ち絵コンポーネントを、3層そろえて作ります。</summary>
        internal BeastThumbnailView CreateThumbnail(string name, Transform parent)
        {
            GameObject root = CreateObject(name, parent);

            BeastThumbnailView view = root.AddComponent<BeastThumbnailView>();

            SetField(view, "baseLayer", CreateObject("Base", root.transform).AddComponent<RawImage>());
            SetField(view, "primaryLayer", CreateObject("Primary", root.transform).AddComponent<RawImage>());
            SetField(view, "secondaryLayer", CreateObject("Secondary", root.transform).AddComponent<RawImage>());

            return view;
        }

        /// <summary>配線済みのトレイ枠を作ります。</summary>
        internal BattleTraySlotView CreateTraySlot(string name = "TraySlot")
        {
            return CreateTraySlot(out TraySlotParts _, name);
        }

        /// <summary>
        /// 配線済みのトレイ枠を、内部パーツごと作ります。
        /// プレハブと同じく、勝敗バッジは見た目の根（Lift）の最後の子にします。
        /// </summary>
        internal BattleTraySlotView CreateTraySlot(
            out TraySlotParts parts,
            string name = "TraySlot")
        {
            GameObject root = CreateObject(name);
            BattleTraySlotView slot = root.AddComponent<BattleTraySlotView>();

            GameObject lift = CreateObject("Lift", root.transform);
            GameObject badge = CreateObject("OutcomeBadge", lift.transform);

            parts = new TraySlotParts
            {
                LiftRoot = lift.GetComponent<RectTransform>(),
                CardGroup = root.AddComponent<CanvasGroup>(),
                Background = CreateImage("Background", lift.transform),
                Frame = CreateImage("Frame", lift.transform),
                AttributeChip = CreateImage("Chip", lift.transform),
                OrderLabel = CreateLabel("Order", lift.transform),
                AttributeLabel = CreateLabel("Attribute", lift.transform),
                UsedLabel = CreateLabel("Used", lift.transform),
                BadgeRoot = badge,
                BadgeGroup = badge.AddComponent<CanvasGroup>(),
                BadgeImage = badge.AddComponent<Image>(),
                BadgeLabel = CreateLabel("OutcomeLabel", badge.transform),
            };

            SetField(slot, "liftRoot", parts.LiftRoot);
            SetField(slot, "canvasGroup", parts.CardGroup);
            SetField(slot, "background", parts.Background);
            SetField(slot, "frame", parts.Frame);
            SetField(slot, "thumbnail", CreateThumbnail("Thumb", lift.transform));
            SetField(slot, "attributeChip", parts.AttributeChip);
            SetField(slot, "orderLabel", parts.OrderLabel);
            SetField(slot, "attributeLabel", parts.AttributeLabel);
            SetField(slot, "usedLabel", parts.UsedLabel);
            SetField(slot, "outcomeBadgeRoot", parts.BadgeRoot);
            SetField(slot, "outcomeBadgeGroup", parts.BadgeGroup);
            SetField(slot, "outcomeBadgeImage", parts.BadgeImage);
            SetField(slot, "outcomeBadgeLabel", parts.BadgeLabel);

            return slot;
        }

        /// <summary>配線済みのトレイを作ります。枠のプレハブ代わりにシーン上の枠を使います。</summary>
        internal BattleSquadTrayView CreateTray(out BattleTraySlotView prefab)
        {
            GameObject root = CreateObject("PlayerTray");
            BattleSquadTrayView tray = root.AddComponent<BattleSquadTrayView>();

            prefab = CreateTraySlot("TraySlotPrefab");

            SetField(tray, "content", CreateObject("Content", root.transform).GetComponent<RectTransform>());
            SetField(tray, "slotPrefab", prefab);

            return tray;
        }

        /// <summary>配線済みのCPU側枠を作ります。</summary>
        internal EnemyMarkerView CreateEnemyMarker(string name = "EnemyMarker")
        {
            GameObject root = CreateObject(name);
            EnemyMarkerView marker = root.AddComponent<EnemyMarkerView>();

            SetField(marker, "background", CreateImage("Background", root.transform));
            SetField(marker, "frame", CreateImage("Frame", root.transform));
            SetField(marker, "markLabel", CreateLabel("Mark", root.transform));

            return marker;
        }

        /// <summary>配線済みのCPU側7枠表示を作ります。</summary>
        internal EnemySquadStatusView CreateEnemyStatus(out TMP_Text remainingLabel)
        {
            GameObject root = CreateObject("EnemyStatus");
            EnemySquadStatusView status = root.AddComponent<EnemySquadStatusView>();

            remainingLabel = CreateLabel("Remaining", root.transform);

            SetField(status, "content", CreateObject("Content", root.transform).GetComponent<RectTransform>());
            SetField(status, "markerPrefab", CreateEnemyMarker("EnemyMarkerPrefab"));
            SetField(status, "remainingLabel", remainingLabel);

            return status;
        }

        /// <summary>配線済みのスコア表示を作ります。</summary>
        internal BattleScoreView CreateScoreView(
            out TMP_Text roundLabel,
            out TMP_Text scoreLabel)
        {
            GameObject root = CreateObject("ScoreView");
            BattleScoreView view = root.AddComponent<BattleScoreView>();

            roundLabel = CreateLabel("Round", root.transform);
            scoreLabel = CreateLabel("Score", root.transform);

            SetField(view, "roundLabel", roundLabel);
            SetField(view, "scoreLabel", scoreLabel);

            return view;
        }

        /// <summary>配線済みの結果表示を作ります。</summary>
        internal BattleResultView CreateResultView(out ResultLabels labels)
        {
            GameObject root = CreateObject("ResultView");
            BattleResultView view = root.AddComponent<BattleResultView>();

            GameObject banner = CreateObject("Banner", root.transform);
            GameObject final = CreateObject("Final", root.transform);

            labels = new ResultLabels
            {
                BannerRoot = banner,
                FinalRoot = final,
                Decision = CreateLabel("Decision", banner.transform),
                Winner = CreateLabel("Winner", banner.transform),
                Matchup = CreateLabel("Matchup", banner.transform),
                BannerScore = CreateLabel("BannerScore", banner.transform),
                FinalTitle = CreateLabel("FinalTitle", final.transform),
                FinalScore = CreateLabel("FinalScore", final.transform),
            };

            SetField(view, "bannerRoot", banner);
            SetField(view, "bannerGroup", banner.AddComponent<CanvasGroup>());
            SetField(view, "decisionLabel", labels.Decision);
            SetField(view, "winnerLabel", labels.Winner);
            SetField(view, "matchupLabel", labels.Matchup);
            SetField(view, "bannerScoreLabel", labels.BannerScore);
            SetField(view, "finalRoot", final);
            SetField(view, "finalTitleLabel", labels.FinalTitle);
            SetField(view, "finalScoreLabel", labels.FinalScore);

            return view;
        }

        /// <summary>配線済みの出場中表示を作ります。</summary>
        internal BattleCombatantView CreateCombatant(out CombatantParts parts)
        {
            GameObject root = CreateObject("Combatant");
            BattleCombatantView view = root.AddComponent<BattleCombatantView>();

            GameObject portrait = CreateObject("Portrait", root.transform);
            GameObject hidden = CreateObject("Hidden", root.transform);
            GameObject info = CreateObject("Info", root.transform);

            parts = new CombatantParts
            {
                PortraitRoot = portrait.GetComponent<RectTransform>(),
                HiddenRoot = hidden,
                InfoRoot = info,
                HiddenLabel = CreateLabel("HiddenLabel", hidden.transform),
                Name = CreateLabel("Name", info.transform),
                Level = CreateLabel("Level", info.transform),
                Attribute = CreateLabel("Attribute", info.transform),
                Power = CreateLabel("Power", info.transform),
                Core = CreateLabel("Core", info.transform),
                SkillName = CreateLabel("SkillName", info.transform),
                SkillDescription = CreateLabel("SkillDescription", info.transform),
                Chip = CreateImage("Chip", info.transform),
            };

            SetField(view, "portraitRoot", parts.PortraitRoot);
            SetField(view, "portraitGroup", portrait.AddComponent<CanvasGroup>());
            SetField(view, "thumbnail", CreateThumbnail("Thumb", portrait.transform));
            SetField(view, "hiddenRoot", hidden);
            SetField(view, "hiddenLabel", parts.HiddenLabel);
            SetField(view, "infoRoot", info);
            SetField(view, "nameLabel", parts.Name);
            SetField(view, "levelLabel", parts.Level);
            SetField(view, "attributeLabel", parts.Attribute);
            SetField(view, "powerLabel", parts.Power);
            SetField(view, "coreLabel", parts.Core);
            SetField(view, "skillNameLabel", parts.SkillName);
            SetField(view, "skillDescriptionLabel", parts.SkillDescription);
            SetField(view, "attributeChip", parts.Chip);

            return view;
        }

        /// <summary>
        /// 配線済みの設定パネルを作ります。歯車だけがパネルの外に出ます。
        /// パネルは実機と同じく、最初から閉じた状態で組み立てます。
        /// </summary>
        internal BattleSettingsView CreateSettingsView(out SettingsParts parts)
        {
            GameObject root = CreateObject("SettingsRoot");
            BattleSettingsView view = root.AddComponent<BattleSettingsView>();

            GameObject panel = CreateObject("Panel", root.transform);
            panel.SetActive(false);

            // 実物と同じ順番で作ります。板が先、本体が後（＝本体が手前）です。
            GameObject backdrop = CreateObject("Backdrop", panel.transform);
            GameObject body = CreateObject("Body", panel.transform);

            parts = new SettingsParts
            {
                PanelRoot = panel,
                Body = body.GetComponent<RectTransform>(),
                SettingsButton =
                    CreateObject("SettingsButton", root.transform).AddComponent<Button>(),
                BackdropButton = backdrop.AddComponent<Button>(),
                FxButton = CreateObject("FxButton", body.transform).AddComponent<Button>(),
                HomeButton =
                    CreateObject("HomeButton", body.transform).AddComponent<Button>(),
                CloseButton =
                    CreateObject("CloseButton", body.transform).AddComponent<Button>(),
                TitleLabel = CreateLabel("TitleLabel", body.transform),
                FxCaptionLabel = CreateLabel("FxCaptionLabel", body.transform),
                FxValueLabel = CreateLabel("FxValueLabel", body.transform),
                HomeLabel = CreateLabel("HomeLabel", body.transform),
                CloseLabel = CreateLabel("CloseLabel", body.transform),
            };

            SetField(view, "panelRoot", parts.PanelRoot);
            SetField(view, "panelBody", parts.Body);
            SetField(view, "settingsButton", parts.SettingsButton);
            SetField(view, "backdropButton", parts.BackdropButton);
            SetField(view, "fxButton", parts.FxButton);
            SetField(view, "homeButton", parts.HomeButton);
            SetField(view, "closeButton", parts.CloseButton);
            SetField(view, "titleLabel", parts.TitleLabel);
            SetField(view, "fxCaptionLabel", parts.FxCaptionLabel);
            SetField(view, "fxValueLabel", parts.FxValueLabel);
            SetField(view, "homeLabel", parts.HomeLabel);
            SetField(view, "closeLabel", parts.CloseLabel);

            return view;
        }

        /// <summary>配線済みの演出プレイヤーを作ります。</summary>
        internal BattleFxPlayer CreateFxPlayer(out FxParts parts)
        {
            GameObject root = CreateObject("FxPlayer");
            BattleFxPlayer player = root.AddComponent<BattleFxPlayer>();

            GameObject playerPortrait = CreateObject("PlayerPortrait", root.transform);
            GameObject cpuPortrait = CreateObject("CpuPortrait", root.transform);
            GameObject flash = CreateObject("Flash", root.transform);
            GameObject impact = CreateObject("ImpactBurst", root.transform);

            parts = new FxParts
            {
                PlayerPortrait = playerPortrait.GetComponent<RectTransform>(),
                CpuPortrait = cpuPortrait.GetComponent<RectTransform>(),
                PlayerGroup = playerPortrait.AddComponent<CanvasGroup>(),
                CpuGroup = cpuPortrait.AddComponent<CanvasGroup>(),
                FlashGroup = flash.AddComponent<CanvasGroup>(),
                ImpactBurst = impact.AddComponent<ImpactBurstGraphic>(),
            };

            SetField(player, "playerPortrait", parts.PlayerPortrait);
            SetField(player, "cpuPortrait", parts.CpuPortrait);
            SetField(player, "playerGroup", parts.PlayerGroup);
            SetField(player, "cpuGroup", parts.CpuGroup);
            SetField(player, "flashImage", flash.AddComponent<Image>());
            SetField(player, "flashGroup", parts.FlashGroup);
            SetField(player, "impactBurst", parts.ImpactBurst);

            return player;
        }

        /// <summary>作成したオブジェクトをすべて破棄します。</summary>
        internal void Cleanup()
        {
            for (int i = 0; i < created.Count; i++)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
        }

        internal static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, FieldFlags);

            if (field == null)
            {
                throw new System.MissingFieldException(target.GetType().Name, name);
            }

            field.SetValue(target, value);
        }

        /// <summary>トレイ枠の内部パーツ。</summary>
        internal struct TraySlotParts
        {
            internal RectTransform LiftRoot;
            internal CanvasGroup CardGroup;
            internal Image Background;
            internal Image Frame;
            internal Image AttributeChip;
            internal TMP_Text OrderLabel;
            internal TMP_Text AttributeLabel;
            internal TMP_Text UsedLabel;
            internal GameObject BadgeRoot;
            internal CanvasGroup BadgeGroup;
            internal Image BadgeImage;
            internal TMP_Text BadgeLabel;
        }

        /// <summary>結果表示の内部ラベル。</summary>
        internal struct ResultLabels
        {
            internal GameObject BannerRoot;
            internal GameObject FinalRoot;
            internal TMP_Text Decision;
            internal TMP_Text Winner;
            internal TMP_Text Matchup;
            internal TMP_Text BannerScore;
            internal TMP_Text FinalTitle;
            internal TMP_Text FinalScore;
        }

        /// <summary>出場中表示の内部パーツ。</summary>
        internal struct CombatantParts
        {
            internal RectTransform PortraitRoot;
            internal GameObject HiddenRoot;
            internal GameObject InfoRoot;
            internal TMP_Text HiddenLabel;
            internal TMP_Text Name;
            internal TMP_Text Level;
            internal TMP_Text Attribute;
            internal TMP_Text Power;
            internal TMP_Text Core;
            internal TMP_Text SkillName;
            internal TMP_Text SkillDescription;
            internal Image Chip;
        }

        /// <summary>設定パネルの内部パーツ。</summary>
        internal struct SettingsParts
        {
            internal GameObject PanelRoot;
            internal RectTransform Body;
            internal Button SettingsButton;
            internal Button BackdropButton;
            internal Button FxButton;
            internal Button HomeButton;
            internal Button CloseButton;
            internal TMP_Text TitleLabel;
            internal TMP_Text FxCaptionLabel;
            internal TMP_Text FxValueLabel;
            internal TMP_Text HomeLabel;
            internal TMP_Text CloseLabel;
        }

        /// <summary>演出が動かすパーツ。</summary>
        internal struct FxParts
        {
            internal RectTransform PlayerPortrait;
            internal RectTransform CpuPortrait;
            internal CanvasGroup PlayerGroup;
            internal CanvasGroup CpuGroup;
            internal CanvasGroup FlashGroup;
            internal ImpactBurstGraphic ImpactBurst;
        }
    }
}
