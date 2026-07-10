# ADR 0001: Project Scope and Upstream Strategy

- Status: Accepted for planning
- Date: 2026-07-10

## Context

YMM4、VOCALOID6、初音ミク V6、口パクを一体化した既存の公開リポジトリは確認できませんでした。一方で、YMM4VoiSonaPlugin、KuchiPaku、YMM4-MIDIには再利用可能な設計またはMITコードがあります。

## Decision

GitHubのネットワークForkを1つ選ぶのではなく、独立した公開リポジトリを作成します。理由は、プロジェクトの対象がVoiSonaでもKuchiPaku単体でもなく、複数上流の小さな責務を組み合わせるためです。

上流コードは一括コピーせず、Phaseごとに必要な範囲だけ、ライセンスと固定SHAを記録したPull Requestで取り込みます。新規コードはYMM4、VOCALOID6、口パクの境界を分離します。

## Consequences

- 上流の履歴と著作権表示を明示的に管理する必要がある
- 不要な機能と古いターゲットフレームワークを持ち込まずに済む
- 複数のコントリビューターが独立した領域へ参加しやすい
- 実装開始前にPhase 0の互換性・ライセンス検証が必要になる
