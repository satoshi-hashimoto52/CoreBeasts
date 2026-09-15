using System.Collections.Generic;
using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 所持個体の一覧。
    /// テスト用データと正式データはアセットを分けて運用します
    /// （テスト用は Assets/CoreBeasts/Data/Testing 配下）。
    /// </summary>
    [CreateAssetMenu(
        fileName = "Roster",
        menuName = "CoreBeasts/Core Beast Roster"
    )]
    public sealed class CoreBeastRoster : ScriptableObject
    {
        [SerializeField]
        [Tooltip("ONのときInspectorに警告色の注記を出す、テスト専用データ印")]
        private bool isTestData;

        [SerializeField]
        private List<OwnedCoreBeast> owned = new List<OwnedCoreBeast>();

        public bool IsTestData => isTestData;

        public IReadOnlyList<OwnedCoreBeast> Owned => owned;

        /// <summary>内部IDから所持個体を引きます。</summary>
        public OwnedCoreBeast Find(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return null;
            }

            for (int i = 0; i < owned.Count; i++)
            {
                OwnedCoreBeast candidate = owned[i];

                if (candidate != null &&
                    candidate.IsValid &&
                    candidate.InstanceId == instanceId)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
