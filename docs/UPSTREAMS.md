# Upstreams and License Strategy

下記プロジェクトは設計と公開APIの調査に利用しました。現在の実装へ上流ソースコードはコピーしていません。依存パッケージは`THIRD-PARTY-NOTICES.md`と`licenses/`に固定バージョンとライセンスを記録しています。

## Primary references

| Project | Planned use | License status | Policy |
| --- | --- | --- | --- |
| [InuInu2022/YMM4VoiSonaPlugin](https://github.com/InuInu2022/YMM4VoiSonaPlugin) | YMM4音声プラグイン構成、外部エディタ自動操作の参考 | MIT | 必要な小さい単位だけ移植し、元著作権表示とコミットSHAを記録する |
| [InuInu2022/KuchiPaku](https://github.com/InuInu2022/KuchiPaku) | 母音口形、`.lab`、YMM4口パク処理の参考 | MIT | Coreロジックの再利用候補。変更点を明記する |
| [routersys/YMM4-MIDI](https://github.com/routersys/YMM4-MIDI) | MIDI、歌詞・音素、YMM4音声プラグインの調査 | MIT | 必要性とビルド再現性を確認してから採用判断する |
| [YukkuriMovieMaker4PluginSamples](https://github.com/manju-summoner/YukkuriMovieMaker4PluginSamples) | 現行YMM4プラグインAPIと.NET 10構成の参照 | リポジトリのライセンスを実装前に再確認 | ライセンスが不明確なコードはコピーしない |
| Windows UI Automation | VOCALOID6の公開UI操作 | Windows公開API | 外部UIライブラリを配布せずAutomation IDと標準ダイアログを利用する |

## Runtime dependencies

| Package | Version | License | Use |
| --- | --- | --- | --- |
| Lucene.Net.Analysis.Kuromoji | 4.8.0-beta00017 | Apache-2.0 | 日本語の形態素解析と読み |
| Lucene.Net / Analysis.Common | 4.8.0-beta00017 | Apache-2.0 | Kuromojiランタイム |
| J2N | 2.1.0 | BSD-3-Clause | Lucene.NET依存 |
| Microsoft.Extensions.Configuration.Abstractions / Primitives | 8.0.0 | MIT | Lucene.NET依存 |

## Import procedure

上流コードを取り込むPull Requestには次を含めます。

1. 上流URLと固定コミットSHA
2. 対象ファイルと採用理由
3. 上流ライセンスと著作権者
4. 原文からの変更点
5. `THIRD-PARTY-NOTICES`またはファイルヘッダーの更新
6. 上流更新を追跡する必要性の判断

依存パッケージを追加する場合は、ライセンス、保守状況、配布サイズ、Windows/.NET 10対応を記録します。

## Clean boundary

VOCALOID6 Editor、ボイスバンク、YMM4本体、キャラクター素材をソースツリー、テストフィクスチャ、GitHub Releasesへ含めません。テストでは自作の短いMIDI、無音WAV、抽象化された偽ドライバーを使用します。
