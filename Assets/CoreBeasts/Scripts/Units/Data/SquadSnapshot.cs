using System;
using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 編成の保存形式。所持個体の内部IDだけを持つため、
    /// そのままJSONやセーブデータへ載せられます。
    /// </summary>
    [Serializable]
    public sealed class SquadSnapshot
    {
        [SerializeField]
        private string[] instanceIds;

        public SquadSnapshot(string[] ids)
        {
            instanceIds = new string[SquadFormation.SlotCount];

            if (ids == null)
            {
                return;
            }

            int count = Mathf.Min(ids.Length, instanceIds.Length);

            for (int i = 0; i < count; i++)
            {
                instanceIds[i] = ids[i];
            }
        }

        /// <summary>指定枠の内部ID。空枠なら空文字。</summary>
        public string GetInstanceId(int index)
        {
            if (instanceIds == null ||
                index < 0 ||
                index >= instanceIds.Length)
            {
                return string.Empty;
            }

            return instanceIds[index] ?? string.Empty;
        }
    }
}
