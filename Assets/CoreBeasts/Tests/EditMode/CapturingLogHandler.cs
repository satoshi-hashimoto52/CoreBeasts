using System.Collections.Generic;

using UnityEngine;

namespace CoreBeasts.Units.Tests
{
    /// <summary>
    /// テスト中の意図的なログを捕まえて、Unity Consoleへは一切流さないログ受け。
    ///
    /// <see cref="UnityEngine.TestTools.LogAssert.Expect"/>は「このログは想定内」と
    /// 印を付けるだけで、メッセージ自体はConsoleへ残ります。全テスト実行後の
    /// Consoleに赤いエラーが残るのはそのためです。
    /// こちらは<see cref="Debug.unityLogger"/>のログ受けそのものを差し替え、
    /// 元のログ受けへ転送しないため、Consoleには何も出ません。
    ///
    /// 使い方（差し替えはログが出る操作より前に行い、必ず元へ戻します）:
    /// <code>
    /// ILogHandler original = Debug.unityLogger.logHandler;
    /// CapturingLogHandler capture = new CapturingLogHandler();
    /// Debug.unityLogger.logHandler = capture;
    ///
    /// try { /* ログが出る操作 */ }
    /// finally { Debug.unityLogger.logHandler = original; }
    /// </code>
    /// </summary>
    internal sealed class CapturingLogHandler : ILogHandler
    {
        private readonly List<Entry> entries = new List<Entry>();

        /// <summary>捕まえた1件ぶん。</summary>
        internal readonly struct Entry
        {
            internal Entry(LogType type, string message)
            {
                Type = type;
                Message = message;
            }

            internal LogType Type { get; }

            internal string Message { get; }
        }

        /// <summary>捕まえたログ。発生順に並びます。</summary>
        internal IReadOnlyList<Entry> Entries => entries;

        /// <summary>指定の重要度で捕まえた件数。</summary>
        internal int CountOf(LogType type)
        {
            int count = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>指定の重要度で最初に捕まえたメッセージ。無ければ空文字。</summary>
        internal string FirstMessageOf(LogType type)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Type == type)
                {
                    return entries[i].Message;
                }
            }

            return string.Empty;
        }

        /// <summary>捕まえた内容を捨てます。</summary>
        internal void Clear()
        {
            entries.Clear();
        }

        /// <summary>
        /// ログを控えるだけで、元のログ受けへは転送しません。
        /// ここで転送すると、意図的なエラーがConsoleへ残ってしまいます。
        /// </summary>
        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            entries.Add(new Entry(logType, SafeFormat(format, args)));
        }

        /// <summary>例外も同じく控えるだけです。</summary>
        public void LogException(System.Exception exception, Object context)
        {
            entries.Add(new Entry(
                LogType.Exception,
                exception != null ? exception.ToString() : string.Empty));
        }

        /// <summary>書式が壊れていても例外を出さずに素の値を返します。</summary>
        private static string SafeFormat(string format, object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return string.Empty;
            }

            if (args == null || args.Length == 0)
            {
                return format;
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
