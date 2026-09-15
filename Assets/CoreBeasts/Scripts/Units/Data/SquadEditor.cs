namespace CoreBeasts.Units
{
    /// <summary>
    /// 編成画面の操作ルール。UnityのUIに依存しないため単体テストできます。
    /// ・一覧の短いタップ … 選択のみ。編成は変えない
    /// ・枠へのドロップ   … 配置または入れ替え
    /// ・配置済み枠のタップ … 除外
    /// </summary>
    public sealed class SquadEditor
    {
        private readonly SquadFormation formation;

        public SquadEditor(SquadFormation formation)
        {
            this.formation = formation;
        }

        /// <summary>編成データ本体。</summary>
        public SquadFormation Formation => formation;

        /// <summary>一覧で選択中の個体。未選択なら null。</summary>
        public OwnedCoreBeast Selected { get; private set; }

        /// <summary>
        /// 一覧の短いタップ。詳細表示用に選択するだけで、編成は変更しません。
        /// </summary>
        public void Select(OwnedCoreBeast beast)
        {
            Selected = beast != null && beast.IsValid ? beast : null;
        }

        /// <summary>
        /// 一覧から編成枠へのドロップ。
        /// 空き枠なら配置、使用中の枠なら入れ替え、
        /// 既に別枠にいる個体なら複製せず2枠を入れ替えます。
        /// 変更があったときだけ true を返します。
        /// </summary>
        public bool DropOnSlot(int slotIndex, OwnedCoreBeast beast)
        {
            if (beast == null || !beast.IsValid)
            {
                return false;
            }

            if (!SquadFormation.IsValidIndex(slotIndex))
            {
                return false;
            }

            if (formation.IndexOf(beast) == slotIndex)
            {
                return false;
            }

            formation.Assign(slotIndex, beast);

            return true;
        }

        /// <summary>
        /// 配置済み枠の短いタップ。部隊から除外します。
        /// 空き枠なら何もしません。所持一覧からは削除しません。
        /// </summary>
        public bool RemoveAt(int slotIndex)
        {
            if (!SquadFormation.IsValidIndex(slotIndex))
            {
                return false;
            }

            if (formation.GetAt(slotIndex) == null)
            {
                return false;
            }

            formation.ClearAt(slotIndex);

            return true;
        }
    }
}
