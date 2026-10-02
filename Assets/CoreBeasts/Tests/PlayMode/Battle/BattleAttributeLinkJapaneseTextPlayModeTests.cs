using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using CoreBeasts.Units;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CoreBeasts.Battle.UI.Tests
{
    /// <summary>
    /// ATTRIBUTE LINK の日本語表示を、実際の Battle シーンと実際のフォントで確かめます。
    ///
    /// - LINK の文言に使う文字が、LiberationSans SDF とそのフォールバック（Noto Sans JP のサブセット）で
    ///   すべて描けること（□への置き換え・Missing Character 警告が無いこと）
    /// - iPhone 12 mini 相当の縦画面で、選択前予告・戦闘中表示・発動演出（FX ON / OFF）・結果理由が
    ///   切れず、PLAYER と CPU の演出文字が重ならないこと
    /// </summary>
    public sealed class BattleAttributeLinkJapaneseTextPlayModeTests
    {
        private const string SceneName = "Battle";
        private const string RosterPath = "Assets/CoreBeasts/Data/Testing/Roster_Test.asset";
        private const string SetId = "1";
        private const float RoundSecondsLimit = 15f;
        private const string JapaneseFontName = "NotoSansJP-Bold-CoreBeastsLink SDF";

        /// <summary>iPhone 12 mini（1080 x 2340 px, @3x、安全余白 上50pt / 下34pt）。</summary>
        private const float PhoneWidthPixels = 1080f;
        private const float PhoneHeightPixels = 2340f;
        private const float PhoneTopInsetPoints = 50f;
        private const float PhoneBottomInsetPoints = 34f;

        private static readonly Regex RichTag = new Regex("<[^>]+>");

        private ISquadRepository originalRepository;
        private BattleScreenController controller;
        private BattleAttributeLinkView linkView;
        private BattleCombatantView playerCombatant;
        private BattleCombatantView cpuCombatant;
        private BattleTextCatalog catalog;
        private readonly List<string> missingCharacterWarnings = new List<string>();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            missingCharacterWarnings.Clear();
            Application.logMessageReceived += CollectMissingCharacters;

            originalRepository = SquadRepositoryProvider.Shared;
            SquadRepositoryProvider.SetShared(new InMemorySquadRepository());

            SaveTestSquad();

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);

            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            controller = Object.FindAnyObjectByType<BattleScreenController>();

            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.enabled, Is.True, "BattleScreenController が参照不足で無効化されました。");

            linkView = controller.AttributeLinkView;
            playerCombatant = (BattleCombatantView)GetField(controller, "playerCombatant");
            cpuCombatant = (BattleCombatantView)GetField(controller, "cpuCombatant");
            catalog = (BattleTextCatalog)GetField(controller, "battleText");

            Assert.That(linkView, Is.Not.Null);
            Assert.That(catalog, Is.Not.Null);
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= CollectMissingCharacters;
            SquadRepositoryProvider.SetShared(originalRepository);
        }

        // ---------------- グリフ ----------------

        [UnityTest]
        public IEnumerator EveryLinkTextCharacterIsInTheFontOrItsJapaneseFallback()
        {
            yield return null;

            // LINK 専用の4つのラベルは、日本語サブセットを主フォントにします（英数字も収録済み）。
            // フォールバックを使わないため、TMP が実行中に SubMesh の GameObject を作りません。
            TMP_FontAsset japanese = linkView.PlayerLabel.font;

            Assert.That(japanese, Is.Not.Null);
            Assert.That(japanese.name, Is.EqualTo(JapaneseFontName));

            foreach (TMP_Text label in new[] { linkView.PlayerLabel, linkView.CpuLabel, playerCombatant.LinkLabel, cpuCombatant.LinkLabel })
            {
                Assert.That(label.font, Is.SameAs(japanese), label.name + " は日本語サブセットで描きます。");
                Assert.That(label.fontSharedMaterial, Is.SameAs(japanese.material), label.name + " の Material が Font Asset と一致しません。");
            }

            // 決着理由は英語の理由と共用のため LiberationSans のままで、日本語はフォールバックから描きます。
            TMP_Text decisionLabel = (TMP_Text)GetField(Object.FindAnyObjectByType<BattleResultView>(), "decisionLabel");

            Assert.That(decisionLabel.font.name, Is.EqualTo("LiberationSans SDF"), "LINK 以外の決着理由の見た目は変えません。");
            Assert.That(decisionLabel.font.fallbackFontAssetTable, Has.Member(japanese), "LiberationSans SDF のフォールバックに日本語サブセットがありません。");

            Assert.That(japanese.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static), "必要文字だけの静的アセットです。");
            Assert.That(japanese.atlasTextures.Length, Is.EqualTo(1));

            TMP_FontAsset liberation = decisionLabel.font;

            foreach (string text in AllLinkTexts())
            {
                foreach (char c in Visible(text))
                {
                    if (c == '\n')
                    {
                        continue;
                    }

                    bool found = liberation.HasCharacter(c, true, false);

                    Assert.That(found, Is.True, "'" + c + "'（U+" + ((int)c).ToString("X4") + "）が描けません: " + text);
                    Assert.That(japanese.HasCharacter(c, false, false), Is.True,
                        "'" + c + "'（U+" + ((int)c).ToString("X4") + "）が日本語サブセットにありません: " + text);

                    if (c > 0x7F)
                    {
                        Assert.That(japanese.HasCharacter(c, false, false), Is.True,
                            "'" + c + "' は日本語フォールバックから描きます。");
                    }
                }
            }

            Assert.That(missingCharacterWarnings, Is.Empty, "Missing Character 警告が出ています。");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator ShowingEveryLinkTextCreatesNoSubMeshOrGameObject()
        {
            yield return UsePortraitPhone();

            // 1ラウンド進めて、両陣営の段を公開します。
            controller.OnWheelDeployRequested(controller.Coordinator.Session.PlayerAvailableUnits[0].InstanceId);

            float startedAt = Time.realtimeSinceStartup;

            while (!(playerCombatant.IsRevealed && cpuCombatant.IsRevealed && controller.IsRoundInProgress))
            {
                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "両陣営が公開されません。");
            }

            int subMeshes = CountScene<TMP_SubMeshUI>();
            int objects = CountScene<Transform>();

            AttributeLinkResult link = new AttributeLinkResult(3, 6, AttributeLink.BitOf(UnitAttribute.Red) | AttributeLink.BitOf(UnitAttribute.Blue));

            playerCombatant.ShowLink(link);
            cpuCombatant.ShowLink(link);
            linkView.Play(BattleAttributeLinkPresentationPlan.Create(link, link, true));

            BattleResultView resultView = Object.FindAnyObjectByType<BattleResultView>();
            GameObject banner = (GameObject)GetField(resultView, "bannerRoot");
            TMP_Text decision = (TMP_Text)GetField(resultView, "decisionLabel");
            BattleUnit u = new BattleUnit("u", UnitAttribute.Red, 50);
            bool bannerWasActive = banner.activeSelf;

            banner.SetActive(true);
            decision.text = BattleResultText.BuildDecision(
                new RoundResult(1, u, u, RoundWinner.Player, RoundDecision.PowerComparison, UnitAttribute.Red, link, link, u, u, 56, 56), catalog);

            for (int i = 0; i < 3; i++)
            {
                yield return null;
            }

            foreach (TMP_Text label in new[] { playerCombatant.LinkLabel, cpuCombatant.LinkLabel, linkView.PlayerLabel, linkView.CpuLabel, decision })
            {
                label.ForceMeshUpdate();
            }

            Assert.That(CountScene<TMP_SubMeshUI>(), Is.EqualTo(subMeshes), "日本語の LINK 表示で TMP が SubMesh を作りました。");
            Assert.That(CountScene<Transform>(), Is.EqualTo(objects), "日本語の LINK 表示で GameObject が増えました。");

            banner.SetActive(bannerWasActive);
            linkView.ResetVisuals();

            yield return WaitRoundEnd();

            Assert.That(missingCharacterWarnings, Is.Empty, "Missing Character 警告が出ています。");
        }

        // ---------------- iPhone 12 mini 相当の実画面 ----------------

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator ThePreviewFitsBesideTheAttributeChipOnIPhone12Mini()
        {
            yield return UsePortraitPhone();

            // 1ラウンド目を終え、直前の個体と属性を共有する候補を中央へ持ってきます。
            string linking = null;

            for (int round = 0; round < 6 && linking == null; round++)
            {
                BattleSession session = controller.Coordinator.Session;

                controller.OnWheelDeployRequested(session.PlayerAvailableUnits[0].InstanceId);
                yield return WaitRoundEnd();

                if (controller.Coordinator.State != BattleUiState.Selecting)
                {
                    break;
                }

                foreach (BattleUnit unit in controller.Coordinator.Session.PlayerAvailableUnits)
                {
                    if (controller.Coordinator.Session.PreviewPlayerLink(unit.InstanceId).IsActive)
                    {
                        linking = unit.InstanceId;
                        break;
                    }
                }
            }

            Assert.That(linking, Is.Not.Null, "LINK になる候補を用意できませんでした。");

            controller.Ring.Focus(linking);
            typeof(BattleScreenController)
                .GetMethod("OnWheelFocusChanged", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(controller, new object[] { linking });

            yield return null;

            TMP_Text label = playerCombatant.LinkLabel;
            AttributeLinkResult preview = controller.Coordinator.Session.PreviewPlayerLink(linking);

            Assert.That(label.gameObject.activeInHierarchy, Is.True, "選択前予告が出ていません。");
            Assert.That(label.text, Is.EqualTo(catalog.FormatLinkBonus(preview.BonusPower)));

            AssertRendered(label, label.text);
            AssertStageLabelFits(playerCombatant, label);

            Assert.That(missingCharacterWarnings, Is.Empty, "Missing Character 警告が出ています。");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator BothStageLinkLabelsFitOnIPhone12Mini()
        {
            yield return UsePortraitPhone();

            controller.OnWheelDeployRequested(controller.Coordinator.Session.PlayerAvailableUnits[0].InstanceId);

            float startedAt = Time.realtimeSinceStartup;

            while (!(playerCombatant.IsRevealed && cpuCombatant.IsRevealed && controller.IsRoundInProgress))
            {
                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "両陣営が公開されません。");
            }

            // 文字数が最も多い表記（+6）で、実際の段のラベルに出します。
            AttributeLinkResult widest = new AttributeLinkResult(4, AttributeLink.MaxLinkBonus, AttributeLink.BitOf(UnitAttribute.Red));

            playerCombatant.ShowLink(widest);
            cpuCombatant.ShowLink(widest);

            yield return null;

            foreach (BattleCombatantView combatant in new[] { playerCombatant, cpuCombatant })
            {
                TMP_Text label = combatant.LinkLabel;

                Assert.That(label.gameObject.activeInHierarchy, Is.True, combatant.name + " の LINK 表示が出ていません。");
                Assert.That(label.text, Is.EqualTo("同じ属性で POWER +6"));

                AssertRendered(label, label.text);
                AssertStageLabelFits(combatant, label);
            }

            yield return WaitRoundEnd();

            Assert.That(missingCharacterWarnings, Is.Empty, "Missing Character 警告が出ています。");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator TheTwoLineCueFitsAndNeverOverlapsOnIPhone12Mini([Values(true, false)] bool fx)
        {
            yield return UsePortraitPhone();

            int[] chains = { 2, 3, 4 };

            foreach (int chain in chains)
            {
                AttributeLinkResult link = new AttributeLinkResult(
                    chain, AttributeLink.BonusFor(chain), AttributeLink.BitOf(UnitAttribute.Green) | AttributeLink.BitOf(UnitAttribute.Blue));

                linkView.Play(BattleAttributeLinkPresentationPlan.Create(link, link, fx));

                yield return null;
                yield return null;

                Assert.That(linkView.IsPlaying, Is.True);
                Assert.That(linkView.Graphic.IsShowing, Is.EqualTo(fx), "FX OFF では図形を出しません。");

                string expected = "属性リンク " + chain + "連鎖！\nPOWER +" + AttributeLink.BonusFor(chain);
                RectTransform area = (RectTransform)linkView.transform;
                Rect areaRect = WorldRect(area);
                Rect player = Rect.zero;
                Rect cpu = Rect.zero;

                foreach (TMP_Text label in new[] { linkView.PlayerLabel, linkView.CpuLabel })
                {
                    Assert.That(label.text, Is.EqualTo(expected));

                    AssertRendered(label, expected);

                    Assert.That(label.textInfo.lineCount, Is.EqualTo(2), label.name + " は2行で出します。");

                    Rect bounds = TextWorldRect(label);

                    Assert.That(Contains(WorldRect(label.rectTransform), bounds), Is.True, label.name + " の文字が枠からはみ出します。");
                    Assert.That(Contains(areaRect, bounds), Is.True, label.name + " の文字が戦闘表示領域（RectMask2D）で切れます。 text=" + bounds + " area=" + areaRect +
                        " portrait=" + WorldRect((RectTransform)GetField(linkView, label == linkView.PlayerLabel ? "playerPortrait" : "cpuPortrait")) +
                        " label=" + WorldRect(label.rectTransform) + " anchored=" + label.rectTransform.anchoredPosition +
                        " cpuStage=" + WorldRect((RectTransform)cpuCombatant.transform));

                    if (label == linkView.PlayerLabel)
                    {
                        player = bounds;
                    }
                    else
                    {
                        cpu = bounds;
                    }
                }

                Assert.That(player.Overlaps(cpu), Is.False, "PLAYER と CPU の演出文字が重なっています。");

                if (!fx)
                {
                    Assert.That(linkView.PlayerLabelAlpha, Is.EqualTo(1f), "FX OFF でも文字ははっきり読めます。");
                    Assert.That(linkView.CpuLabelAlpha, Is.EqualTo(1f));
                }

                linkView.ResetVisuals();
            }

            Assert.That(missingCharacterWarnings, Is.Empty, "Missing Character 警告が出ています。");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator TheLongestResultReasonFitsTheBannerOnIPhone12Mini()
        {
            yield return UsePortraitPhone();

            BattleResultView resultView = Object.FindAnyObjectByType<BattleResultView>();
            GameObject banner = (GameObject)GetField(resultView, "bannerRoot");
            TMP_Text decision = (TMP_Text)GetField(resultView, "decisionLabel");

            BattleUnit p = new BattleUnit("p", UnitAttribute.Red, 50);
            BattleUnit c = new BattleUnit("c", UnitAttribute.Red, 50);
            AttributeLinkResult three = new AttributeLinkResult(2, 3, AttributeLink.BitOf(UnitAttribute.Red));
            AttributeLinkResult six = new AttributeLinkResult(3, 6, AttributeLink.BitOf(UnitAttribute.Red));

            // 両陣営に加算があり、3桁の POWER の時が最も長い表記です。
            RoundResult[] results =
            {
                new RoundResult(1, p, c, RoundWinner.Player, RoundDecision.PowerComparison, UnitAttribute.Red, three, AttributeLinkResult.None, p, c, 57, 50),
                new RoundResult(1, p, c, RoundWinner.Cpu, RoundDecision.PowerComparison, UnitAttribute.Red, AttributeLinkResult.None, three, p, c, 50, 53),
                new RoundResult(1, p, c, RoundWinner.Cpu, RoundDecision.PowerComparison, UnitAttribute.Red, six, six, p, c, 105, 106),
            };

            bool wasActive = banner.activeSelf;
            banner.SetActive(true);

            foreach (RoundResult result in results)
            {
                string text = BattleResultText.BuildDecision(result, catalog);

                decision.text = text;
                decision.ForceMeshUpdate();

                yield return null;

                Assert.That(decision.isTextTruncated, Is.False,
                    "結果理由が「…」で切れます: " + Visible(text) + " preferred=" + decision.GetPreferredValues(text).x + " width=" + decision.rectTransform.rect.width + " fontSize=" + decision.fontSize);

                AssertRendered(decision, text);

                Assert.That(decision.isTextTruncated, Is.False, "結果理由が「…」で切れます: " + Visible(text));
                Assert.That(decision.textInfo.lineCount, Is.EqualTo(1));
                Assert.That(Contains(WorldRect(decision.rectTransform), TextWorldRect(decision)), Is.True, "結果理由が枠からはみ出します: " + Visible(text));
            }

            banner.SetActive(wasActive);

            Assert.That(missingCharacterWarnings, Is.Empty, "Missing Character 警告が出ています。");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator BothPortraitsAreDrawnInsideTheirOwnStagesOnIPhone12Mini()
        {
            // LINK 演出は立ち絵を中心に描くため、立ち絵が画面外にあると CPU 側の演出が切れます。
            // CPU の立ち絵は左右反転（scale.x = -1）なので、反転の軸（pivot）が段の内側の端にある必要があります。
            yield return UsePortraitPhone();

            controller.OnWheelDeployRequested(controller.Coordinator.Session.PlayerAvailableUnits[0].InstanceId);

            float startedAt = Time.realtimeSinceStartup;

            while (!(playerCombatant.IsRevealed && cpuCombatant.IsRevealed && controller.IsRoundInProgress))
            {
                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "両陣営が公開されません。");
            }

            foreach (BattleCombatantView combatant in new[] { playerCombatant, cpuCombatant })
            {
                Rect portrait = Normalized(WorldRect(combatant.PortraitTransform));
                Rect stage = WorldRect((RectTransform)combatant.transform);

                Assert.That(Contains(stage, portrait), Is.True, combatant.name + " の立ち絵が段の外に描かれます。 portrait=" + portrait + " stage=" + stage);
            }

            Assert.That(cpuCombatant.PortraitTransform.localScale.x, Is.LessThan(0f), "CPU は PLAYER の方を向くよう反転したままです。");
            Assert.That(playerCombatant.PortraitTransform.localScale.x, Is.GreaterThan(0f));

            yield return WaitRoundEnd();
        }

        // ---------------- 検査 ----------------

        /// <summary>
        /// 実際に作られたメッシュの文字が、元の文字列（タグを除く）と1文字ずつ一致し、
        /// 日本語は日本語フォールバックで描かれていることを確かめます（□へ置き換わっていない）。
        /// </summary>
        private static void AssertRendered(TMP_Text label, string source)
        {
            label.ForceMeshUpdate();

            TMP_TextInfo info = label.textInfo;
            StringBuilder rendered = new StringBuilder();

            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo ch = info.characterInfo[i];

                rendered.Append(ch.character);

                if (char.IsWhiteSpace(ch.character))
                {
                    continue;
                }

                Assert.That(ch.isVisible, Is.True, label.name + ": '" + ch.character + "' が見えません。");
                Assert.That(ch.textElement, Is.Not.Null, label.name + ": '" + ch.character + "' のグリフがありません。");

                if (ch.character > 0x7F)
                {
                    Assert.That(ch.fontAsset.name, Is.EqualTo(JapaneseFontName), label.name + ": '" + ch.character + "' を日本語フォントで描いていません。");
                }
            }

            Assert.That(rendered.ToString(), Is.EqualTo(Visible(source)), label.name + " の文字が置き換わっています。");
            Assert.That(label.isTextOverflowing, Is.False, label.name + " が溢れています。");
        }

        /// <summary>戦闘中の段の LINK 表示が、Info の中で属性チップ・名前・POWER に重ならないこと。</summary>
        private static void AssertStageLabelFits(BattleCombatantView combatant, TMP_Text label)
        {
            Rect text = TextWorldRect(label);
            RectTransform info = (RectTransform)label.transform.parent;

            Assert.That(Contains(WorldRect(label.rectTransform), text), Is.True, combatant.name + " の LINK 表示が枠からはみ出します。 text=" + text + " rect=" + WorldRect(label.rectTransform) + " fontSize=" + label.fontSize);
            Assert.That(Contains(WorldRect(info), text), Is.True, combatant.name + " の LINK 表示が Info からはみ出します。");
            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(label.fontSizeMin), combatant.name);

            foreach (string other in new[] { "attributeChip", "nameLabel", "levelLabel", "powerLabel", "coreLabel", "skillNameLabel" })
            {
                Component c = (Component)GetField(combatant, other);
                RectTransform r = (RectTransform)c.transform;

                Assert.That(text.Overlaps(WorldRect(r)), Is.False, combatant.name + " の LINK 表示が " + other + " に重なっています。");
            }
        }

        private IEnumerable<string> AllLinkTexts()
        {
            for (int chain = 2; chain <= BattleSession.MaxRounds; chain++)
            {
                int bonus = AttributeLink.BonusFor(chain);

                yield return catalog.FormatLinkBonus(bonus);
                yield return catalog.FormatLinkCue(chain, bonus);
            }

            for (int power = 0; power <= 9; power++)
            {
                yield return catalog.FormatLinkPowerDecision(catalog.FormatLinkedPower(power * 11, 3), catalog.FormatUnlinkedPower(power));
                yield return catalog.FormatLinkPowerDecision(catalog.FormatUnlinkedPower(power), catalog.FormatLinkedPower(power * 11, 6));
            }
        }

        private void CollectMissingCharacters(string condition, string stackTrace, LogType type)
        {
            if (condition != null && condition.Contains("was not found in the"))
            {
                missingCharacterWarnings.Add(condition);
            }
        }

        // ---------------- 画面 ----------------

        /// <summary>
        /// バッチ実行の Game View は横長のため、ルート Canvas を iPhone 12 mini の縦画面と同じ
        /// Canvas 単位の大きさへ置き換えます（倍率は参照解像度と Match から計算します）。
        /// </summary>
        private IEnumerator UsePortraitPhone()
        {
            Canvas canvas = Find("Canvas").GetComponent<Canvas>();
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();

            float logWidth = Mathf.Log(PhoneWidthPixels / scaler.referenceResolution.x, 2f);
            float logHeight = Mathf.Log(PhoneHeightPixels / scaler.referenceResolution.y, 2f);
            float pixelsPerUnit = Mathf.Pow(2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));

            scaler.enabled = false;
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = (RectTransform)canvas.transform;
            canvasRect.localScale = Vector3.one;
            canvasRect.sizeDelta = new Vector2(PhoneWidthPixels / pixelsPerUnit, PhoneHeightPixels / pixelsPerUnit);

            RectTransform safeArea = (RectTransform)Find("SafeArea");
            safeArea.anchorMin = new Vector2(0f, PhoneBottomInsetPoints * 3f / PhoneHeightPixels);
            safeArea.anchorMax = new Vector2(1f, 1f - PhoneTopInsetPoints * 3f / PhoneHeightPixels);

            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return null;

            Assert.That(WorldRect(safeArea).height, Is.GreaterThan(WorldRect(safeArea).width), "縦画面になっていません。");
        }

        private IEnumerator WaitRoundEnd()
        {
            float startedAt = Time.realtimeSinceStartup;

            while (controller.IsRoundInProgress)
            {
                yield return null;

                Assert.That(Time.realtimeSinceStartup - startedAt, Is.LessThan(RoundSecondsLimit), "ラウンドが終わりません。");
            }
        }

        // ---------------- 道具 ----------------

        private static string Visible(string text)
        {
            return RichTag.Replace(text, string.Empty);
        }

        private static int CountScene<T>() where T : Component
        {
            int count = 0;

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                count += root.GetComponentsInChildren<T>(true).Length;
            }

            return count;
        }

        private static Rect TextWorldRect(TMP_Text label)
        {
            Bounds b = label.textBounds;
            Vector3 min = label.transform.TransformPoint(b.min);
            Vector3 max = label.transform.TransformPoint(b.max);

            return Rect.MinMaxRect(Mathf.Min(min.x, max.x), Mathf.Min(min.y, max.y), Mathf.Max(min.x, max.x), Mathf.Max(min.y, max.y));
        }

        /// <summary>反転した RectTransform の角は左右が入れ替わるため、幅を正にそろえます。</summary>
        private static Rect Normalized(Rect r)
        {
            return Rect.MinMaxRect(Mathf.Min(r.xMin, r.xMax), Mathf.Min(r.yMin, r.yMax), Mathf.Max(r.xMin, r.xMax), Mathf.Max(r.yMin, r.yMax));
        }

        private static Rect WorldRect(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static bool Contains(Rect outer, Rect inner)
        {
            const float Epsilon = 0.5f;

            return inner.xMin >= outer.xMin - Epsilon && inner.xMax <= outer.xMax + Epsilon &&
                   inner.yMin >= outer.yMin - Epsilon && inner.yMax <= outer.yMax + Epsilon;
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name + " が見つかりません。");

            return field.GetValue(target);
        }

        private static Transform Find(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindDeep(root.transform, name);

                if (found != null)
                {
                    return found;
                }
            }

            Assert.Fail(name + " が見つかりません。");
            return null;
        }

        private static Transform FindDeep(Transform node, string name)
        {
            if (node.name == name)
            {
                return node;
            }

            for (int i = 0; i < node.childCount; i++)
            {
                Transform found = FindDeep(node.GetChild(i), name);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void SaveTestSquad()
        {
#if UNITY_EDITOR
            CoreBeastRoster roster = AssetDatabase.LoadAssetAtPath<CoreBeastRoster>(RosterPath);

            Assert.That(roster, Is.Not.Null, RosterPath + " を読めません。");

            string[] ids = new string[SquadFormation.SlotCount];

            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = roster.Owned[i].InstanceId;
            }

            SquadRepositoryProvider.Shared.Save(SetId, new SquadSnapshot(ids));
#else
            Assert.Fail("エディタ上でのみ実行します（テスト用ロスターをAssetDatabaseから読むため）。");
#endif
        }
    }
}
