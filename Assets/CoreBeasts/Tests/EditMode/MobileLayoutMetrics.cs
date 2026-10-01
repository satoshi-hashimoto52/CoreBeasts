using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// モバイルUIの寸法をテストから確かめるための換算。
    ///
    /// 画面は iPhone 16 Pro（論理 402 x 874 pt / 実 1206 x 2622 px, @3x）を基準にします。
    /// CanvasScaler が Scale With Screen Size のため、Canvas の1単位が何ptになるかは
    /// 参照解像度と Match の値から決まります。ここを定数で埋め込まず毎回計算するので、
    /// 参照解像度を変えてもテストの意味が変わりません。
    /// </summary>
    internal static class MobileLayoutMetrics
    {
        /// <summary>iPhone 16 Pro の実ピクセル幅。</summary>
        internal const float ScreenWidthPixels = 1206f;

        /// <summary>iPhone 16 Pro の実ピクセル高さ。</summary>
        internal const float ScreenHeightPixels = 2622f;

        /// <summary>@3x。</summary>
        internal const float DeviceScale = 3f;

        /// <summary>Dynamic Island 側の安全余白（pt）。</summary>
        internal const float TopInsetPoints = 62f;

        /// <summary>ホームインジケーター側の安全余白（pt）。</summary>
        internal const float BottomInsetPoints = 34f;

        /// <summary>タップ領域の下限（pt）。</summary>
        internal const float MinimumTapPoints = 44f;

        /// <summary>Canvas 1単位が何ピクセルになるか。</summary>
        internal static float PixelsPerUnit(CanvasScaler scaler)
        {
            float logWidth = Mathf.Log(
                ScreenWidthPixels / scaler.referenceResolution.x, 2f);

            float logHeight = Mathf.Log(
                ScreenHeightPixels / scaler.referenceResolution.y, 2f);

            return Mathf.Pow(
                2f, Mathf.Lerp(logWidth, logHeight, scaler.matchWidthOrHeight));
        }

        /// <summary>Canvas 1単位が何ptになるか。</summary>
        internal static float PointsPerUnit(CanvasScaler scaler)
        {
            return PixelsPerUnit(scaler) / DeviceScale;
        }

        /// <summary>Safe Area の高さ（Canvas単位）。</summary>
        internal static float SafeAreaHeightUnits(CanvasScaler scaler)
        {
            float insetPixels = (TopInsetPoints + BottomInsetPoints) * DeviceScale;

            return (ScreenHeightPixels - insetPixels) / PixelsPerUnit(scaler);
        }

        /// <summary>Safe Area の幅（Canvas単位）。左右の安全余白は縦画面では0です。</summary>
        internal static float SafeAreaWidthUnits(CanvasScaler scaler)
        {
            return ScreenWidthPixels / PixelsPerUnit(scaler);
        }

        /// <summary>
        /// 親の高さが <paramref name="parentHeight"/> のときに、この矩形が占める
        /// 縦の範囲を「親の下端からの距離」で返します（x=下端, y=上端）。
        /// </summary>
        internal static Vector2 VerticalSpan(RectTransform rt, float parentHeight)
        {
            float anchorBottom = rt.anchorMin.y * parentHeight;
            float anchorTop = rt.anchorMax.y * parentHeight;

            float height = (anchorTop - anchorBottom) + rt.sizeDelta.y;

            float anchorPoint = Mathf.Lerp(anchorBottom, anchorTop, rt.pivot.y);
            float pivotY = anchorPoint + rt.anchoredPosition.y;
            float bottom = pivotY - rt.pivot.y * height;

            return new Vector2(bottom, bottom + height);
        }

        /// <summary>この矩形の実寸（Canvas単位）。親の大きさに依らない指定のときに使います。</summary>
        internal static Vector2 FixedSize(RectTransform rt)
        {
            Assert.That(
                rt.anchorMin,
                Is.EqualTo(rt.anchorMax),
                rt.name + " は伸縮指定のため、実寸を一意に決められません。");

            return rt.sizeDelta;
        }

        /// <summary>タップ領域が 44pt 四方を満たすか確かめます。</summary>
        internal static void AssertTapTarget(
            RectTransform rt,
            CanvasScaler scaler,
            float minimumPoints = MinimumTapPoints)
        {
            Vector2 size = FixedSize(rt);
            float points = PointsPerUnit(scaler);

            Assert.That(
                size.x * points,
                Is.GreaterThanOrEqualTo(minimumPoints),
                rt.name + " の横のタップ領域が " + minimumPoints + "pt 未満です。");

            Assert.That(
                size.y * points,
                Is.GreaterThanOrEqualTo(minimumPoints),
                rt.name + " の縦のタップ領域が " + minimumPoints + "pt 未満です。");
        }
    }
}
