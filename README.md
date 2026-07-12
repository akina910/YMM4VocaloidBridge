# YMM4VocaloidBridge

YMM4から、ユーザーが正規に所有・インストールしているVOCALOID6ボイスバンクを利用するための、非公式オープンソース連携プラグインです。最初の検証対象は初音ミク V6です。

> [!IMPORTANT]
> 現在は公開ベータ実装中です。VOCALOID6と初音ミク V6は利用者自身が正規に所有・インストールする必要があります。

## 目標

- YMM4のセリフから、VOCALOID6を利用した会話調のWAVを生成する
- 生成音声に合わせて、ユーザー所有の「動く立ち絵」を口パクさせる
- ボイスバンク、キャラクター画像、認証情報を配布・複製しない
- 外部コントリビューターがIssueとPull Requestで参加できる開発基盤にする

## 実装済み

- 日本語の漢字かな読み変換、モーラ分割、会話向けの決定的な音程生成
- UTF-8歌詞イベント付きStandard MIDI生成
- VOCALOID6の補助手動モードと、日本語UI向け自動操作
- 自動操作失敗時の補助手動モードへの復帰
- WAV構造検証、安定待ち、再生成キャッシュ
- YMM4公開APIによる「あ・い・う・え・お・閉じ」の口パク
- `.lab`サイドカー、診断CLI、デスクトップ診断画面
- `.ymme`パッケージ、Windows CI、依存ライセンス検査

## 最短導入

1. [インストールと初回実行](docs/INSTALLATION.md)に従って`.ymme`を導入する
2. 同梱CLIの`doctor --ui`でYMM4、VOCALOID6、初音ミク V6を確認する
3. YMM4の声質で`VOCALOID6 Bridge / 初音ミク V6 ORIGINAL`を選ぶ
4. 初期値の`Automatic`で生成する。自動操作に失敗した場合だけ`Assisted`へ切り替える

自動モードでは専用のVOCALOID6プロジェクトを使ってください。名前付きの非Bridgeトラックがあるプロジェクトは変更せず、補助手動モードへ戻ります。

## 想定ワークフロー

1. YMM4でキャラクターのボイスとして本プラグインを選ぶ
2. セリフを入力する
3. プラグインが読み・モーラ・会話用ピッチを生成し、歌詞付きMIDIへ変換する
4. ローカルのVOCALOID6 Editorで、所有済みボイスバンクを使ってWAVを書き出す
5. WAVと口パク用タイミングをYMM4へ返す

VOCALOID6に公開された無人レンダリングAPIが確認できないため、公開UIと標準ファイル形式を利用します。補助手動モードを常に残し、その上にWindows UI Automationを追加しています。

## スコープ

初期リリースでは、Windows 11、現行YMM4、VOCALOID6 6.12.0以降、日本語UI、初音ミク V6、日本語の短いセリフを対象にします。

次のものは配布しません。

- VOCALOID6 Editor、ボイスバンク、認証データ
- 初音ミクを含むキャラクター画像や立ち絵素材
- Yamaha、クリプトン・フューチャー・メディア、YMM4のバイナリ
- ライセンス認証を回避または改変する機能

## 計画

- [実装計画](docs/IMPLEMENTATION_PLAN.md)
- [アーキテクチャ](docs/ARCHITECTURE.md)
- [上流プロジェクトとライセンス](docs/UPSTREAMS.md)
- [商標・所有素材の扱い](docs/LEGAL_AND_ASSETS.md)
- [コントリビューションガイド](CONTRIBUTING.md)
- [インストールと初回実行](docs/INSTALLATION.md)
- [M0実機検証証跡](docs/M0_FEASIBILITY_EVIDENCE.md)
- [E2E検証証跡](docs/E2E_EVIDENCE.md)
- [変更履歴](CHANGELOG.md)

Phase 0ゲートは実機検証で通過済みです。現在はM1からM5の公開ベータ実装と検証を進めています。

## 開発

```powershell
dotnet test tests\YMM4VocaloidBridge.Tests\YMM4VocaloidBridge.Tests.csproj -c Release
dotnet build src\YMM4VocaloidBridge.Plugin\YMM4VocaloidBridge.Plugin.csproj -c Release -p:YMM4DirPath="C:\path\to\YMM4"
$env:YMM4_DIR = "C:\path\to\YMM4"
.\tools\build-package.ps1
```

プラグインAPI参照のため、ローカルビルドでは正規に取得したYMM4ディレクトリが必要です。YMM4 DLLはリポジトリやパッケージへコピーしません。

## 参加方法

不具合、提案、技術調査、ドキュメント改善を歓迎します。大きな実装を始める前にIssueで設計を合意してください。Pull Requestの提出方法は[CONTRIBUTING.md](CONTRIBUTING.md)を参照してください。

## 非公式プロジェクトについて

本プロジェクトは、Yamaha Corporation、クリプトン・フューチャー・メディア株式会社、饅頭遣い（YMM4）その他の権利者による公式プロジェクトではなく、提携・承認を示すものでもありません。製品名・キャラクター名・商標は各権利者に帰属します。

## License

本リポジトリで新規作成するコードとドキュメントは、特記がない限り[MIT License](LICENSE)です。外部プロジェクトから取り込むコードには、各上流ライセンスと著作権表示が適用されます。
