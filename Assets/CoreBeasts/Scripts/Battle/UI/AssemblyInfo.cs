using System.Runtime.CompilerServices;

// BattleScreenController の CPU 差し替え口（TestCpuRandomSeed / TestCpuSelectorFactory）は、
// 乱数次第で失敗しない PlayMode テストのためだけにあります。
// internal のまま、テストアセンブリにだけ見せます（製品のコードからは設定できません）。
[assembly: InternalsVisibleTo("CoreBeasts.Battle.PlayModeTests")]
[assembly: InternalsVisibleTo("CoreBeasts.Battle.UI.EditModeTests")]
