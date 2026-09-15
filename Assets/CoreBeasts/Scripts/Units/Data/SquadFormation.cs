using System;

namespace CoreBeasts.Units
{
    /// <summary>
    /// 自部隊7枠の編成データ。UnityのUIには一切依存しません。
    /// 表示側は<see cref="Changed"/>を購読して描き直します。
    /// </summary>
    public sealed class SquadFormation
    {
        /// <summary>編成枠の数。</summary>
        public const int SlotCount = 7;

        private readonly OwnedCoreBeast[] slots = new OwnedCoreBeast[SlotCount];

        /// <summary>いずれかの枠が変化したときに発火します。</summary>
        public event Action Changed;

        /// <summary>枠番号が有効範囲内か。</summary>
        public static bool IsValidIndex(int index)
        {
            return index >= 0 && index < SlotCount;
        }

        /// <summary>指定枠の個体。空なら null。</summary>
        public OwnedCoreBeast GetAt(int index)
        {
            return IsValidIndex(index) ? slots[index] : null;
        }

        /// <summary>
        /// 指定個体が入っている枠番号。未配置なら -1。
        ///
        /// 判定は所持個体の内部ID(<see cref="OwnedCoreBeast.InstanceId"/>)で行います。
        /// セーブ・ロードやロースター再構築でオブジェクト参照が作り直されても、
        /// 同じ個体なら一致します。
        /// 同じ<see cref="CoreBeastDefinition"/>を共有する別個体は、
        /// IDが異なるため別物として扱われます。
        /// </summary>
        public int IndexOf(OwnedCoreBeast beast)
        {
            if (beast == null)
            {
                return -1;
            }

            if (!string.IsNullOrEmpty(beast.InstanceId))
            {
                return IndexOfInstance(beast.InstanceId);
            }

            // 内部IDが無い個体は比較材料が無いため、参照一致だけで判断します。
            for (int i = 0; i < SlotCount; i++)
            {
                if (ReferenceEquals(slots[i], beast))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>内部IDで枠番号を探します。未配置なら -1。</summary>
        public int IndexOfInstance(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
            {
                return -1;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                OwnedCoreBeast slot = slots[i];

                if (slot != null && slot.InstanceId == instanceId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>配置済みの個体数。0〜<see cref="SlotCount"/>。</summary>
        public int OccupiedCount
        {
            get
            {
                int count = 0;

                for (int i = 0; i < SlotCount; i++)
                {
                    if (slots[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>
        /// 指定枠へ配置します。
        /// その個体が既に別の枠にいる場合は、2つの枠を入れ替えます。
        /// </summary>
        public void Assign(int index, OwnedCoreBeast beast)
        {
            if (!IsValidIndex(index))
            {
                return;
            }

            if (beast == null)
            {
                ClearAt(index);
                return;
            }

            int currentIndex = IndexOf(beast);

            if (currentIndex == index)
            {
                return;
            }

            OwnedCoreBeast displaced = slots[index];

            if (currentIndex >= 0)
            {
                // 既に編成済みの個体 → 枠同士を入れ替える
                slots[currentIndex] = displaced;
            }

            slots[index] = beast;

            Changed?.Invoke();
        }

        /// <summary>指定枠を空にします。</summary>
        public void ClearAt(int index)
        {
            if (!IsValidIndex(index) || slots[index] == null)
            {
                return;
            }

            slots[index] = null;

            Changed?.Invoke();
        }

        /// <summary>保存用のスナップショットを作ります。</summary>
        public SquadSnapshot CreateSnapshot()
        {
            string[] ids = new string[SlotCount];

            for (int i = 0; i < SlotCount; i++)
            {
                ids[i] = slots[i] != null ? slots[i].InstanceId : string.Empty;
            }

            return new SquadSnapshot(ids);
        }

        /// <summary>スナップショットから復元します。</summary>
        public void Restore(SquadSnapshot snapshot, CoreBeastRoster roster)
        {
            if (snapshot == null || roster == null)
            {
                return;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                slots[i] = roster.Find(snapshot.GetInstanceId(i));
            }

            Changed?.Invoke();
        }
    }
}
