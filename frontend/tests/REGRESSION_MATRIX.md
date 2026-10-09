# Async / State Regression Matrix

This matrix maps the highest-risk invariants to automated tests. It is intentionally scenario-oriented rather than counting tests.

| Risk scenario | Expected invariant | Automated coverage |
| --- | --- | --- |
| Double-send single-flight | A second text execute cannot preempt or append while an LLM turn owns the transport | `frontend/tests/chatTurnTransportController.test.mts` — `a second text execution cannot preempt an active LLM turn` |
| Reset while LLM | Reset aborts transport; a late response is ignored; reducer returns to clean idle state | `chatTurnTransportController.test.mts` — stale completion test; `chatSessionState.test.mts` — reset while thinking |
| Reset while TTS | Abort synthesis/playback, clear replay, and ignore late synthesis completion | `frontend/tests/avatarSpeechController.test.mts` — `reset while TTS synthesis...` |
| Unsupported language | No TTS request/audio; visible `unsupported` state has an explicit exit to `idle` after lifecycle hold | `avatarSpeechController.test.mts` — unsupported test; `operationalState.test.mts` — forced hold/non-busy policy |
| New send interrupts speech | Active audio stops immediately; old speak resolves as interrupted | `avatarSpeechController.test.mts` — new send interruption test |
| Stale audio callback | Old `onended/onerror/play` callbacks cannot mutate a newer playback generation | `avatarSpeechController.test.mts` — stale audio callback test |
| Context truncation | Context contains only complete persisted turns; never starts with orphan assistant | `tests/AiAvatar.Backend.Tests/ConversationHistoryReaderTests.cs` |
| Same-conversation concurrent turns | Second turn waits until first commits, then reads history including first turn | `tests/AiAvatar.Backend.Tests/ChatTurnServiceConcurrencyTests.cs` and `ConversationTurnGateTests.cs` |
| Speech messageId authority | Browser request contains only `messageId`; TTS text/emotion come from persisted assistant message | `tests/AiAvatar.Backend.Tests/SpeechSynthesisServiceTests.cs` and `SpeechRepositoryTests.cs` |

## Review rule

A future refactor is not considered safe merely because the total test count increases. The scenarios above are architecture invariants and must remain covered by deterministic regression tests.
