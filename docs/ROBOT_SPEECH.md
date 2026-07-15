# 初音ミク ロボ声の使い方

## 必要なもの

- Windows 11
- VOCALOID6 Editor 6.12.0以降
- 正規にインストール、認証したHATSUNE MIKU V6

VOCALOID6、ボイスバンク、認証情報はこのツールに含まれません。YMM4は任意です。

## EXEを直接使う

1. `MikuRobotSpeech.v.<version>.win-x64.zip`を展開する。
2. `MikuRobotSpeech.exe`を起動する。
3. `台詞>`へ日本語を入力してEnterを押す。
4. VOCALOID6の自動処理が終わると、デスクトップへ`miku-robot-speech-日時.wav`が作成される。

既定値は基準音程64の固定音程、話速125%（約120ms/モーラ）、短い音節間隔、促音と句読点の休止です。

## コマンドから使う

```powershell
.\MikuRobotSpeech.exe doctor
.\MikuRobotSpeech.exe speak --text "こんにちは、初音ミクです。" --output ".\miku.wav"
```

話速は`--rate 50-200`、基準音程は`--base-note 48-72`、VOCALOID:AI Takeは`--take 1-10`で変更できます。

```powershell
.\MikuRobotSpeech.exe speak --text "少し速く話します。" --output ".\fast.wav" --rate 120
```

`speak`は自動操作だけを使い、失敗時に手動操作へ切り替わりません。既存のVOCALOID6プロジェクトに名前付きの非Bridgeトラックがある場合は保護のため停止します。

## YMM4から使う

`.ymme`版を導入すると、YMM4の`VOCALOID6 Bridge / 初音ミク V6 ORIGINAL`から同じロボ声エンジンを利用できます。YMM4では話速、基準音程、Takeをキャラクター設定から変更できます。

## 作業データ

既定の作業データは`%LOCALAPPDATA%\YMM4VocaloidBridge`へ保存されます。保存先を変える場合は、起動前に`YMM4_VOCALOID_BRIDGE_DATA_DIR`を設定します。
