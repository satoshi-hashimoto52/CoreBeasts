using System.Text;

using CoreBeasts.Progression;
using CoreBeasts.Units;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CoreBeasts.Home
{
    /// <summary>
    /// Homeシーン上へ、ホーム・報酬・ガチャ・獲得・コレクションの全画面UIを組み立てます。
    /// シーンを増やさず、遷移の待ち時間も発生させません。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HomeHubView : MonoBehaviour
    {
        private static readonly Color Background = new Color(0.035f, 0.055f, 0.10f, 0.96f);
        private static readonly Color Panel = new Color(0.08f, 0.12f, 0.20f, 0.98f);
        private static readonly Color Accent = new Color(0.20f, 0.58f, 0.96f, 1f);
        private static readonly Color Gold = new Color(1f, 0.78f, 0.24f, 1f);

        private TMP_FontAsset font;
        private GameObject homePage;
        private GameObject rewardPage;
        private GameObject gachaPage;
        private GameObject acquisitionPage;
        private GameObject collectionPage;

        private TMP_Text coinLabel;
        private TMP_Text recordLabel;
        private TMP_Text rewardLabel;
        private TMP_Text gachaCoinLabel;
        private TMP_Text gachaMessageLabel;
        private Button pullButton;
        private TMP_Text acquisitionTitle;
        private TMP_Text acquisitionName;
        private TMP_Text acquisitionDetail;
        private Image acquisitionImage;
        private TMP_Text collectionLabel;

        private UnityAction battleAction;
        private UnityAction unitSetAction;
        private UnityAction pullAction;

        public void Build(
            RectTransform host,
            TMP_FontAsset textFont,
            UnityAction onBattle,
            UnityAction onUnitSet,
            UnityAction onPull)
        {
            font = textFont;
            battleAction = onBattle;
            unitSetAction = onUnitSet;
            pullAction = onPull;

            RectTransform root = CreateRect("PlayableFlow", host);
            Stretch(root);
            Image rootImage = root.gameObject.AddComponent<Image>();
            rootImage.color = Background;
            rootImage.raycastTarget = true;
            root.SetAsLastSibling();

            BuildHome(root);
            BuildReward(root);
            BuildGacha(root);
            BuildAcquisition(root);
            BuildCollection(root);
        }

        public void ShowHome(PlayerProfile profile)
        {
            ShowOnly(homePage);
            RefreshHeader(profile);
        }

        public void ShowReward(int reward, int battles, PlayerProfile profile)
        {
            ShowOnly(rewardPage);
            RefreshHeader(profile);

            rewardLabel.text = battles > 1
                ? $"{battles} BATTLES REWARD\n+{reward} CORE COIN"
                : $"BATTLE REWARD\n+{reward} CORE COIN";
        }

        public void ShowGacha(PlayerProfile profile, string message = "")
        {
            ShowOnly(gachaPage);
            RefreshHeader(profile);

            gachaCoinLabel.text =
                $"COINS {profile.Coins}  /  COST {GameEconomy.GachaCost}";
            gachaMessageLabel.text = message ?? string.Empty;
            pullButton.interactable = profile.Coins >= GameEconomy.GachaCost;
        }

        public void ShowAcquisition(GachaResult result)
        {
            ShowOnly(acquisitionPage);

            if (result == null || result.Beast == null || result.Beast.Definition == null)
            {
                acquisitionTitle.text = "ACQUISITION ERROR";
                acquisitionName.text = string.Empty;
                acquisitionDetail.text = string.Empty;
                acquisitionImage.sprite = null;
                return;
            }

            CoreBeastDefinition definition = result.Beast.Definition;

            acquisitionTitle.text = result.IsNew ? "NEW CORE BEAST!" : "CORE DUPLICATED";
            acquisitionName.text = definition.DisplayName;
            acquisitionDetail.text =
                $"Lv.{result.Beast.Level}  /  {BuildAttributes(definition)}\n" +
                $"COPIES x{result.Copies}    {result.RemainingCoins} COINS LEFT";
            acquisitionImage.sprite = definition.Thumbnail;
            acquisitionImage.enabled = definition.Thumbnail != null;
        }

        public void ShowCollection(PlayerProfile profile, CoreBeastRoster catalog)
        {
            ShowOnly(collectionPage);
            RefreshHeader(profile);

            StringBuilder builder = new StringBuilder();
            int unique = 0;

            for (int i = 0; i < catalog.Owned.Count; i++)
            {
                OwnedCoreBeast beast = catalog.Owned[i];

                if (beast == null || !beast.IsValid)
                {
                    continue;
                }

                int copies = profile.CopiesOf(beast.InstanceId);

                if (copies > 0)
                {
                    unique++;
                    builder.Append("◆ ")
                        .Append(beast.Definition.DisplayName)
                        .Append("  Lv.").Append(beast.Level)
                        .Append("  ").Append(BuildAttributes(beast.Definition))
                        .Append("  x").Append(copies);
                }
                else
                {
                    builder.Append("◇ LOCKED CORE  ?????");
                }

                builder.AppendLine();
            }

            collectionLabel.text =
                $"COLLECTION  {unique}/{catalog.Owned.Count}\n\n{builder}";
        }

        private void BuildHome(RectTransform root)
        {
            homePage = CreatePage("HomePage", root);
            CreateText(homePage.transform, "CORE BEASTS", 52, new Vector2(0, 730), new Vector2(900, 90), Gold);
            coinLabel = CreateText(homePage.transform, string.Empty, 30, new Vector2(0, 635), new Vector2(900, 55), Color.white);
            recordLabel = CreateText(homePage.transform, string.Empty, 24, new Vector2(0, 575), new Vector2(900, 50), new Color(0.72f, 0.82f, 0.94f));

            CreateButton(homePage.transform, "BATTLE", new Vector2(-190, 250), battleAction);
            CreateButton(homePage.transform, "UNIT SET", new Vector2(190, 250), unitSetAction);
            CreateButton(homePage.transform, "GACHA", new Vector2(-190, 80), () => ShowGacha(PlayerProfileProvider.Get(FindCatalog())));
            CreateButton(homePage.transform, "COLLECTION", new Vector2(190, 80), () =>
            {
                CoreBeastRoster catalog = FindCatalog();
                ShowCollection(PlayerProfileProvider.Get(catalog), catalog);
            });

            CreateText(
                homePage.transform,
                "BATTLE  >  REWARD  >  GACHA  >  UNIT SET",
                25,
                new Vector2(0, -190),
                new Vector2(900, 80),
                new Color(0.58f, 0.72f, 0.90f));
        }

        private void BuildReward(RectTransform root)
        {
            rewardPage = CreatePage("RewardPage", root);
            CreateText(rewardPage.transform, "MISSION COMPLETE", 42, new Vector2(0, 570), new Vector2(900, 80), Gold);
            rewardLabel = CreateText(rewardPage.transform, string.Empty, 52, new Vector2(0, 230), new Vector2(900, 210), Color.white);
            CreateButton(rewardPage.transform, "TO GACHA", new Vector2(0, -120), () => ShowGacha(PlayerProfileProvider.Get(FindCatalog())));
            CreateButton(rewardPage.transform, "HOME", new Vector2(0, -300), () => ShowHome(PlayerProfileProvider.Get(FindCatalog())), false);
        }

        private void BuildGacha(RectTransform root)
        {
            gachaPage = CreatePage("GachaPage", root);
            CreateText(gachaPage.transform, "CORE CAPSULE", 48, new Vector2(0, 610), new Vector2(900, 90), Gold);
            CreateText(gachaPage.transform, "ANALYZE A MECHANICAL LIFE CORE SIGNAL", 27, new Vector2(0, 500), new Vector2(900, 70), Color.white);
            gachaCoinLabel = CreateText(gachaPage.transform, string.Empty, 27, new Vector2(0, 350), new Vector2(900, 70), Color.white);
            pullButton = CreateButton(gachaPage.transform, "ACTIVATE  100", new Vector2(0, 70), pullAction);
            gachaMessageLabel = CreateText(gachaPage.transform, string.Empty, 25, new Vector2(0, -90), new Vector2(900, 100), new Color(1f, 0.48f, 0.40f));
            CreateButton(gachaPage.transform, "BACK", new Vector2(0, -330), () => ShowHome(PlayerProfileProvider.Get(FindCatalog())), false);
        }

        private void BuildAcquisition(RectTransform root)
        {
            acquisitionPage = CreatePage("AcquisitionPage", root);
            acquisitionTitle = CreateText(acquisitionPage.transform, string.Empty, 45, new Vector2(0, 650), new Vector2(900, 90), Gold);

            RectTransform frame = CreateRect("Portrait", acquisitionPage.transform);
            frame.sizeDelta = new Vector2(420, 420);
            frame.anchoredPosition = new Vector2(0, 260);
            Image frameImage = frame.gameObject.AddComponent<Image>();
            frameImage.color = new Color(0.08f, 0.15f, 0.25f, 1f);
            acquisitionImage = CreateRect("Beast", frame).gameObject.AddComponent<Image>();
            Stretch(acquisitionImage.rectTransform, 26f);
            acquisitionImage.preserveAspect = true;

            acquisitionName = CreateText(acquisitionPage.transform, string.Empty, 48, new Vector2(0, -30), new Vector2(900, 80), Color.white);
            acquisitionDetail = CreateText(acquisitionPage.transform, string.Empty, 27, new Vector2(0, -145), new Vector2(900, 130), new Color(0.75f, 0.86f, 1f));
            CreateButton(acquisitionPage.transform, "TO COLLECTION", new Vector2(0, -350), () =>
            {
                CoreBeastRoster catalog = FindCatalog();
                ShowCollection(PlayerProfileProvider.Get(catalog), catalog);
            });
        }

        private void BuildCollection(RectTransform root)
        {
            collectionPage = CreatePage("CollectionPage", root);
            collectionLabel = CreateText(collectionPage.transform, string.Empty, 27, new Vector2(0, 170), new Vector2(920, 1050), Color.white);
            collectionLabel.alignment = TextAlignmentOptions.TopLeft;
            CreateButton(collectionPage.transform, "TO UNIT SET", new Vector2(0, -410), unitSetAction);
            CreateButton(collectionPage.transform, "BACK", new Vector2(0, -570), () => ShowHome(PlayerProfileProvider.Get(FindCatalog())), false);
        }

        private CoreBeastRoster FindCatalog()
        {
            HomeController controller = GetComponent<HomeController>();
            return controller != null ? controller.Roster : null;
        }

        private void RefreshHeader(PlayerProfile profile)
        {
            if (profile == null)
            {
                return;
            }

            if (coinLabel != null)
            {
                coinLabel.text = $"CORE COIN  {profile.Coins}";
            }

            if (recordLabel != null)
            {
                recordLabel.text =
                    $"BATTLE {profile.Battles}  /  W {profile.Wins}  D {profile.Draws}  L {profile.Losses}";
            }
        }

        private void ShowOnly(GameObject page)
        {
            homePage.SetActive(page == homePage);
            rewardPage.SetActive(page == rewardPage);
            gachaPage.SetActive(page == gachaPage);
            acquisitionPage.SetActive(page == acquisitionPage);
            collectionPage.SetActive(page == collectionPage);
        }

        private GameObject CreatePage(string name, RectTransform root)
        {
            RectTransform page = CreateRect(name, root);
            Stretch(page);
            Image image = page.gameObject.AddComponent<Image>();
            image.color = Panel;
            image.raycastTarget = true;
            return page.gameObject;
        }

        private Button CreateButton(
            Transform parent,
            string label,
            Vector2 position,
            UnityAction action,
            bool accent = true)
        {
            RectTransform rect = CreateRect(label.Replace(" ", string.Empty) + "Button", parent);
            rect.sizeDelta = new Vector2(350, 125);
            rect.anchoredPosition = position;

            Image image = rect.gameObject.AddComponent<Image>();
            image.color = accent ? Accent : new Color(0.16f, 0.22f, 0.32f, 1f);

            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            if (action != null)
            {
                button.onClick.AddListener(action);
            }

            TMP_Text text = CreateText(rect, label, 29, Vector2.zero, rect.sizeDelta, Color.white);
            text.fontStyle = FontStyles.Bold;

            return button;
        }

        private TMP_Text CreateText(
            Transform parent,
            string value,
            float size,
            Vector2 position,
            Vector2 dimensions,
            Color color)
        {
            RectTransform rect = CreateRect("Label", parent);
            rect.sizeDelta = dimensions;
            rect.anchoredPosition = position;

            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = size;
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            label.text = value ?? string.Empty;
            return label;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject created = new GameObject(name, typeof(RectTransform));
            created.layer = 5;
            RectTransform rect = created.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static void Stretch(RectTransform rect, float inset = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static string BuildAttributes(CoreBeastDefinition definition)
        {
            if (definition == null)
            {
                return string.Empty;
            }

            string primary = definition.PrimaryAttribute.ToString().ToUpperInvariant();
            return definition.HasSecondaryAttribute
                ? primary + "/" + definition.SecondaryAttribute.ToString().ToUpperInvariant()
                : primary;
        }
    }
}
