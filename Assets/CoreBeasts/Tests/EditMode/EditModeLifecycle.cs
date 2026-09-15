using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// EditModeではUnityがAwake / OnEnable / OnDisable / Update を配送しません
    /// （[ExecuteAlways]を付けたコンポーネントを除く）。
    /// 実行時と同じ順序を再現するため、ハンドラを直接呼び出します。
    /// 本番コードにテスト専用の分岐は入れず、呼び出し側だけで解決します。
    /// </summary>
    internal static class EditModeLifecycle
    {
        internal static void Invoke(Component target, string methodName)
        {
            Assert.That(target, Is.Not.Null, "対象コンポーネントがありません。");

            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.That(
                method,
                Is.Not.Null,
                target.GetType().Name + "." + methodName + " が見つかりません。");

            method.Invoke(target, null);
        }

        internal static void Awake(Component target) => Invoke(target, "Awake");

        internal static void Enable(Component target) => Invoke(target, "OnEnable");

        internal static void Disable(Component target) => Invoke(target, "OnDisable");

        internal static void Update(Component target) => Invoke(target, "Update");
    }
}
