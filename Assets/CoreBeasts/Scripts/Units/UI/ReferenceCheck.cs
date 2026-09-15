using System.Collections.Generic;
using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// SerializeFieldの未設定を、どのフィールドかが分かる形で報告します。
    /// 不足が複数ある場合はすべて出力します。
    /// </summary>
    public static class ReferenceCheck
    {
        /// <summary>1件ぶんの検査対象。</summary>
        public readonly struct Field
        {
            public readonly string Name;
            public readonly object Value;

            public Field(string name, object value)
            {
                Name = name;
                Value = value;
            }
        }

        public static Field Of(string name, object value)
        {
            return new Field(name, value);
        }

        /// <summary>
        /// すべて設定済みなら true。不足があれば、フィールド名ごとにエラーを出します。
        /// 例: [RosterGridView] RosterScroll: content is not assigned.
        /// </summary>
        public static bool Validate(Component owner, string label, params Field[] fields)
        {
            if (fields == null || fields.Length == 0)
            {
                return true;
            }

            List<string> missing = null;

            for (int i = 0; i < fields.Length; i++)
            {
                if (IsMissing(fields[i].Value))
                {
                    missing ??= new List<string>();
                    missing.Add(fields[i].Name);
                }
            }

            if (missing == null)
            {
                return true;
            }

            string ownerName = owner != null ? owner.name : "<destroyed>";

            for (int i = 0; i < missing.Count; i++)
            {
                Debug.LogError(
                    $"[{label}] {ownerName}: {missing[i]} is not assigned.",
                    owner
                );
            }

            return false;
        }

        /// <summary>UnityのObjectは破棄済みでも参照が残るため、専用に判定します。</summary>
        private static bool IsMissing(object value)
        {
            if (value is Object unityObject)
            {
                return unityObject == null;
            }

            return value == null;
        }
    }
}
