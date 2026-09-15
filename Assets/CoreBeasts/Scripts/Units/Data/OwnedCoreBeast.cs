using System;
using UnityEngine;

namespace CoreBeasts.Units
{
    /// <summary>
    /// プレイヤーが所持している1個体。
    /// 同じ<see cref="CoreBeastDefinition"/>でもレベル違いで複数所持できるため、
    /// 枠への配置は<see cref="InstanceId"/>で識別します。
    /// </summary>
    [Serializable]
    public sealed class OwnedCoreBeast
    {
        [SerializeField]
        [Tooltip("所持個体の内部ID。ロースター内で一意にしてください。")]
        private string instanceId = string.Empty;

        [SerializeField]
        private CoreBeastDefinition definition;

        [SerializeField]
        [Min(1)]
        private int level = 1;

        public string InstanceId => instanceId;

        public CoreBeastDefinition Definition => definition;

        public int Level => Mathf.Max(1, level);

        /// <summary>定義とIDが揃っていて、UIに出せる状態か。</summary>
        public bool IsValid =>
            definition != null && !string.IsNullOrEmpty(instanceId);
    }
}
