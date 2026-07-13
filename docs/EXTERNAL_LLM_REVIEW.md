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
| GitHub Copilot CLI 1.0.70 | Completed chunked diff reviews. Initial P1/P2 findings were fixed; final focused reviews found no remaining P0/P1 in audio/cache or release provenance. Follow-up automation and boundary findings were also addressed locally and revalidated. |
| Claude Code 2.1.139 / Sonnet | **BLOCKED**: authenticated, but both the repository review (5 minutes) and tools-disabled diff review (4 minutes) timed out without a verdict. |
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

This review evidence does not replace the remaining real YMM4 standing-image preview and ORIGINAL soak gates. Until those run, the product-level result remains **BLOCKED**, not PASS.
