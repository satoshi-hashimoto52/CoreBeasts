using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    public sealed class DragGhostPresenterTests
    {
        private GameObject host;
        private GameObject prefabSource;
        private AttributePalette palette;
        private CoreBeastRoster roster;

        [SetUp]
        public void SetUp()
        {
            palette = TestPaletteFactory.Create();
            roster = TestRosterFactory.Create(1);

            prefabSource = new GameObject("GhostPrefab", typeof(RectTransform));
            prefabSource.AddComponent<CanvasGroup>();
            prefabSource.AddComponent<DragGhostView>();
            prefabSource.SetActive(false);

            host = new GameObject("GhostLayer", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            if (host != null) { Object.DestroyImmediate(host); host = null; }
            if (prefabSource != null)
            {
                Object.DestroyImmediate(prefabSource);
                prefabSource = null;
            }

            TestRosterFactory.Destroy(roster);
            roster = null;

            if (palette != null) { Object.DestroyImmediate(palette); palette = null; }
        }

        [Test]
        public void Ghost_IsRemovedAfterHide()
        {
            RectTransform root = (RectTransform)host.transform;
            DragGhostPresenter presenter = host.AddComponent<DragGhostPresenter>();

            SetField(presenter, "ghostRoot", root);
            SetField(presenter, "ghostPrefab",
                     prefabSource.GetComponent<DragGhostView>());

            Assert.That(presenter.IsShowing, Is.False);

            presenter.Show(roster.Owned[0], palette, null, null);

            Assert.That(presenter.IsShowing, Is.True);
            Assert.That(root.childCount, Is.EqualTo(1));

            presenter.Hide();

            Assert.That(presenter.IsShowing, Is.False);
            Assert.That(
                root.childCount,
                Is.EqualTo(0),
                "操作終了後にドラッグ表示が残ってはいけません。"
            );

            Assert.DoesNotThrow(() => presenter.Hide());
        }

        [Test]
        public void Show_ReplacesPreviousGhost()
        {
            RectTransform root = (RectTransform)host.transform;
            DragGhostPresenter presenter = host.AddComponent<DragGhostPresenter>();

            SetField(presenter, "ghostRoot", root);
            SetField(presenter, "ghostPrefab",
                     prefabSource.GetComponent<DragGhostView>());

            presenter.Show(roster.Owned[0], palette, null, null);
            presenter.Show(roster.Owned[0], palette, null, null);

            Assert.That(root.childCount, Is.EqualTo(1), "表示が増殖してはいけません。");

            presenter.Hide();
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            Assert.That(field, Is.Not.Null, name + " が見つかりません。");
            field.SetValue(target, value);
        }
    }
}
