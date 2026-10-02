# Noto Sans JP（ATTRIBUTE LINK 表示用サブセット）

ATTRIBUTE LINK の日本語表示（属性リンク・同じ属性で・POWER勝利 など）だけに使う、必要文字だけのサブセットです。

- 元フォント: Noto Sans JP（可変フォント `NotoSansJP[wght].ttf`）
- 入手元: https://github.com/google/fonts/tree/main/ofl/notosansjp
- 元ファイルの SHA-256: `c2f3b4d463500a2ddcd3849cded1fceeb9fd6d1c32e6cbecd568453ba50fc68f`
- ライセンス: SIL Open Font License 1.1（同じフォルダの `OFL.txt`）
  - Reserved Font Name は 'Source' のみで、このサブセットの名前（Noto Sans JP）には使っていません。
- 太さ: wght=700（Bold）の静的インスタンス

## 収録文字（37字）

```
 !()+0123456789EOPRWじでクリン利勝同対属性連鎖！（）＋
```

## 作り方（fontTools 4.51.0）

```
python3 -m fontTools.varLib.instancer "NotoSansJP[wght].ttf" wght=700 --update-name-table -o NotoSansJP-Bold-full.ttf
python3 -m fontTools.subset NotoSansJP-Bold-full.ttf --text-file=chars.txt \
  --output-file=NotoSansJP-Bold-CoreBeastsLink.ttf --layout-features='*' --name-IDs='*' --name-legacy --name-languages='*'
```

`NotoSansJP-Bold-CoreBeastsLink SDF.asset` はこの TTF から作った静的な TMP Font Asset で、
`LiberationSans SDF` のフォールバックに登録しています（英数字は従来どおり LiberationSans で描かれます）。
文字を増やすときは、TTF を作り直してから Font Asset の文字も追加してください。
