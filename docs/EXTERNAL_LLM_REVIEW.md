# External LLM Review Evidence

Date: 2026-07-13

## Scope

The merged `v0.1.0-beta.1` implementation at commit `533703382d78684b7118986653b28b4830da1dd5` and the subsequent atomic-output hardening were reviewed independently. Review prompts prohibited file edits, GUI launches, and GitHub changes, and requested only concrete P0, P1, or P2 findings with file and line references.

## Reviewer results

| Reviewer | Initial result | Final result |
| --- | --- | --- |
| Claude Sonnet | Found a P1 `.wav` atomic-publish bypass and a P2 cleanup exception-masking risk in patch-only review | **PASS**, no P0/P1 findings |
| GitHub Copilot CLI | Found P1 non-atomic cache/output copies and a later P1 cache read/write sharing race | **PASS**, no P0/P1 findings |
| Gemini CLI | **BLOCKED**: installed client rejected authentication with `UNSUPPORTED_CLIENT` | Not counted as passing evidence |

Claude's initial repository-agent runs timed out and were not counted. Claude was then given the complete changed files directly with tools disabled. Copilot was given the same completed code bundle for the final verification.

## Findings addressed

- Cache restore no longer streams directly into the final destination.
- `.wav` and YMM4 temporary-extension requests always render to a separate work file.
- Completed destination files remain untouched until a same-directory temporary copy is complete.
- Cancellation and copy failures remove temporary files without replacing the original exception.
- Cache readers allow delete-sharing so a concurrent atomic store can replace the cache entry.
- Transient Windows sharing and access-denied errors during final replacement are retried for up to two seconds.

## Final local evidence

- Release build: 0 warnings, 0 errors.
- Tests: 21 passed.
- Formatting: `dotnet format --verify-no-changes` passed.
- `git diff --check` passed.

The final Claude and Copilot verdicts are both PASS. Gemini remains explicitly blocked rather than being reported as reviewed or passing.

## Completion-gate patch review (2026-07-13)

The staged beta.3 completion-gate patch was reviewed again after the earlier release was found to lack real YMM4 standing-image evidence, rendered-WAVE lip-sync alignment, and reproducible package provenance.

| Reviewer | Result |
| --- | --- |
| GitHub Copilot | Reviewed the complete pull-request change set. Its package-scanner memory finding was fixed and revalidated. A later `TimeSpan` compile comment was disproved by the language operator and passing Windows build. |
| Claude Code 2.1.139 / Sonnet | Completed a full branch review, found two medium and two low UI-automation risks, and confirmed after commit `153e30d` that all four were addressed with no new actionable regression. |
| Gemini CLI 0.43.0 | **BLOCKED**: `UNSUPPORTED_CLIENT` / `IneligibleTierError`; no review verdict was produced. |

Copilot findings addressed in this patch include:

- preserve punctuation through YMM4's custom-reading round trip;
- invalidate pre-change audio caches after prosody and alignment changes;
- prevent silent or unsupported WAVE data from being cached as successful speech;
- support PCM/float `WAVE_FORMAT_EXTENSIBLE` and consistent first-data-chunk parsing;
- keep every viseme transition at a unique timestamp and within the audio duration;
- verify ORIGINAL through selection actions or observable assigned UI values without treating unrelated combo boxes as failures;
- retain the prior `voicebank-selected` event while adding the verified event;
- isolate tag-release write permission in a separate job;
- scan packaged binaries for narrow and UTF-16LE secrets/absolute user paths;
- require a clean worktree, matching RID clean, embedded source revision, and CI revision checks for plugin DLLs and the CLI executable.

The Claude findings fixed the imported-track selector timeout, restricted update-prompt dismissal to identified update UI, restored explicit combo collapse, and guarded cursor restoration. The final package then passed an ORIGINAL automatic render with no assisted fallback, and the real YMM4 executable restored 15 native lip-sync frames from cache. See [End-to-End Evidence](E2E_EVIDENCE.md).

## beta.4 quality review (2026-07-14)

The beta.3 completion claim was withdrawn after intelligibility was found to be outside the acceptance gate. Claude Sonnet reviewed the complete beta.4 diff after the quality rework. The review covered bridge-project ownership, clean-session restart, session-recovery handling, exact style/take selection, silent-WAVE rejection, cache invalidation, sokuon timing, lip sync, and CLI dialogue redaction.

Claude reported no high- or medium-severity actionable issue. Its observations were checked against the source: the Japanese recovery labels are valid UTF-8, `WaveAudioAnalyzer.Analyze` rejects unusable audio by exception, and the CLI parser does not support `--text=value`, so the adjacent-value redaction covers accepted syntax. Gemini remains blocked by `UNSUPPORTED_CLIENT` and is not counted as passing evidence. A GitHub Copilot review is required again after the beta.4 commit is pushed.

## beta.5 robot-speech review (2026-07-14)

The standalone robot-speech and shared YMM4-core diff was reviewed by the local
Qwen 3.6 35B model. The review covered timing validation, fixed-pitch sequence
generation, interactive CLI behavior, standalone packaging, and the release
workflow.

Three defensive changes were adopted: explicitly clamp the minimum note gap to
zero, add milliseconds to interactive output names, and require exactly one
standalone ZIP during package verification. Two reported P0 findings were
disproved against the full source: `$revision` is defined in the workflow step
before use, and `$cliPublish` is defined before the publish/copy block.

The 35B post-fix rerun timed out without a verdict and is not counted. A focused
post-fix review by the local Qwen 3.5 9.7B model returned `PASS: no P0/P1/P2
findings`. GitHub Copilot was unavailable because the active `gh` credential is
invalid, so it is explicitly blocked and not counted as passing evidence.
