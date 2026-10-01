using System.Runtime.CompilerServices;

// CPUの選出が「プレイヤーより先に確定している」ことは、テストからしか確かめられません。
// ただし、その個体IDを画面へ出すと Reveal 前に次の敵が分かってしまいます。
// そこで internal のまま、テストアセンブリにだけ見せます。
// CoreBeasts.Battle.UI からは見えないので、UI が誤って使うことはできません。
[assembly: InternalsVisibleTo("CoreBeasts.Battle.EditModeTests")]
