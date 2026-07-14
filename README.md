# Miku Robot Speech / YMM4VocaloidBridge

ユーザーが正規に所有・インストールしている初音ミク V6で、日本語のロボ声WAVを作る非公式オープンソースツールです。単体EXEで利用でき、同じ音声生成コアをYMM4の音声プラグインからも呼び出せます。

> [!IMPORTANT]
> VOCALOID6と初音ミク V6は利用者自身が正規に所有・インストールする必要があります。本ツールはボイスバンク、キャラクター画像、認証情報を同梱しません。VOCALOID6は歌声合成エンジンのため、専用TTSと同じ自然会話ではなく、固定音程の機械的なロボ声を目的とします。

## 目標

- 任意の日本語から、初音ミク V6を利用した固定音程のロボ声WAVを生成する
- 単体EXEとYMM4プラグインの両方から同じ音声生成コアを利用する
- ボイスバンク、キャラクター画像、認証情報を配布・複製しない
- 外部コントリビューターがIssueとPull Requestで参加できる開発基盤にする

## 実装済み

- 日本語の漢字かな読み変換とモーラ分割
- 基準音程64の固定音程、約150ms/モーラ、短い音節間隔を持つロボ声シーケンス
- 促音、語境界、読点、文末の休止と、50%から200%の話速変更
- VOCALOID:AI Take 1から10の選択とTake 10の既定値
- UTF-8歌詞イベント付きStandard MIDI生成
- ダブルクリックで台詞を入力できる自己完結型Windows EXE
- VOCALOID6の補助手動モードと、日本語UI向け自動操作
- 自動操作失敗時の補助手動モードへの復帰
- WAV構造検証、安定待ち、再生成キャッシュ
- 無音または信号床未満のWAVをドライバ段階で失敗させる音声活動検査
- YMM4公開APIによる「あ・い・う・え・お・閉じ」の口パク
- 完成WAVの有音区間解析、無音出力の拒否、口パクタイムライン補正と先行量設定
- `.lab`サイドカー、診断CLI、デスクトップ診断画面
- `.ymme`パッケージ、Windows CI、依存ライセンス検査

## 最短利用（YMM4なし）

1. `MikuRobotSpeech.v.<version>.win-x64.zip`を展開する
2. `MikuRobotSpeech.exe`を起動する
3. `台詞>`へ日本語を入力する
4. デスクトップに作成されたWAVを使う

コマンド操作、話速変更、YMM4からの利用方法は[初音ミク ロボ声の使い方](docs/ROBOT_SPEECH.md)を参照してください。

## YMM4で使う

1. [インストールと初回実行](docs/INSTALLATION.md)に従って`.ymme`を導入する
2. YMM4の声質で`VOCALOID6 Bridge / 初音ミク V6 ORIGINAL`を選ぶ
3. 既定の`Automatic`、話速`100%`、基準音程`64`、Take`10`で生成する

自動モードでは専用のVOCALOID6プロジェクトを使ってください。名前付きの非Bridgeトラックがあるプロジェクトは変更せず、補助手動モードへ戻ります。

## 想定ワークフロー

1. YMM4でキャラクターのボイスとして本プラグインを選ぶ
2. セリフを入力する
3. プラグインが読み・モーラ・固定音程のロボ声シーケンスを生成し、歌詞付きMIDIへ変換する
4. ローカルのVOCALOID6 Editorで、所有済みボイスバンクを使ってWAVを書き出す
5. 完成WAVの有音区間へ口パクタイミングを補正し、YMM4へ返す

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

機械的なビルド、自動操作、無音、口パク検査に加え、実機で単体`speak`からPCM WAVまで生成します。詳細は[E2E検証証跡](docs/E2E_EVIDENCE.md)を参照してください。

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
