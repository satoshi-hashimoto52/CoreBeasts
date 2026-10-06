# Noto Sans JP（ATTRIBUTE LINK・ユニークスキル表示用サブセット）

ATTRIBUTE LINK（Phase 4）とユニークスキル（Phase 5）の日本語表示（属性リンク・同じ属性で・POWER勝利・スキル発動・妨害、スキルの説明文 など）だけに使う、必要文字だけのサブセットです。

- 元フォント: Noto Sans JP（可変フォント `NotoSansJP[wght].ttf`）
- 入手元: https://github.com/google/fonts/tree/main/ofl/notosansjp
- 元ファイルの SHA-256: `c2f3b4d463500a2ddcd3849cded1fceeb9fd6d1c32e6cbecd568453ba50fc68f`
- ライセンス: SIL Open Font License 1.1（同じフォルダの `OFL.txt`）
  - Reserved Font Name は 'Source' のみで、このサブセットの名前（Noto Sans JP）には使っていません。
- 太さ: wght=700（Bold）の静的インスタンス

## 収録文字（96字）

```
 !()+-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ、。いかがしじたてでなにのられウキクスドラリルン体個出利前動勝北同場妨害対属性手敗時流発登直相連鎖！（）＋／
```

スキル名（英大文字）を同じラベルで描くため、A〜Z をすべて含めています。

## 作り方（fontTools 4.51.0）

```
python3 -m fontTools.varLib.instancer "NotoSansJP[wght].ttf" wght=700 --update-name-table -o NotoSansJP-Bold-full.ttf
python3 -m fontTools.subset NotoSansJP-Bold-full.ttf --text-file=chars.txt \
  --output-file=NotoSansJP-Bold-CoreBeastsLink.ttf --layout-features='*' --name-IDs='*' --name-legacy --name-languages='*'
```

`NotoSansJP-Bold-CoreBeastsLink SDF.asset` はこの TTF から作った静的な TMP Font Asset（48pt・余白5・1024×512）で、
`LiberationSans SDF` のフォールバックに登録しています。決着理由・スキルの説明文など LiberationSans のラベルでは、
日本語だけがこのフォールバックから描かれます。LINK・スキル専用のラベル（段の LINK / スキル表示、演出の文字）は、
実行中に TMP の SubMesh を作らないよう、このアセットを主フォントにしています（英大文字・数字も収録済み）。
文字を増やすときは、TTF を作り直してから Font Asset の文字も追加してください。
Font Asset を作り直す場合は、シーンやフォールバックからの参照が切れないよう、既存アセットの GUID と
アトラス・Material のローカルIDを保ってください。行の高さ（LiberationSans と同じ比率）と擬似ボールドの弱め（boldStyle 0.2 / boldSpacing 2）も同じに揃えます。
