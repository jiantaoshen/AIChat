import assert from "node:assert/strict";
import test from "node:test";

import {
  AvatarSpeechController,
  type AudioLike,
  type SpeechPlaybackState,
} from "../hooks/avatarSpeechController.ts";

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });

  return { promise, resolve, reject };
}

class FakeAudio implements AudioLike {
  preload = "";
  currentTime = 0;
  onended: (() => void) | null = null;
  onerror: (() => void) | null = null;
  paused = false;
  playCalls = 0;

  async play() {
    this.playCalls += 1;
  }

  pause() {
    this.paused = true;
  }
}

function createHarness(
  synthesize: (
    request: { messageId: string },
    signal: AbortSignal,
  ) => Promise<Blob> = async () => new Blob(["wav"]),
) {
  const states: SpeechPlaybackState[] = [];
  const replayAvailability: boolean[] = [];
  const audios: FakeAudio[] = [];
  let objectUrlCounter = 0;
  const revoked: string[] = [];

  const controller = new AvatarSpeechController({
    synthesizeSpeech: synthesize,
    createAudio: () => {
      const audio = new FakeAudio();
      audios.push(audio);
      return audio;
    },
    createObjectUrl: () => `blob:test-${++objectUrlCounter}`,
    revokeObjectUrl: (url) => revoked.push(url),
    onStateChanged: (state) => states.push(state),
    onReplayAvailabilityChanged: (available) =>
      replayAvailability.push(available),
  });

  return { controller, states, replayAvailability, audios, revoked };
}

async function waitFor(condition: () => boolean) {
  for (let attempt = 0; attempt < 20; attempt += 1) {
    if (condition()) {
      return;
    }
    await new Promise<void>((resolve) => setImmediate(resolve));
  }

  assert.fail("condition did not become true");
}

test("reset while TTS synthesis aborts the request and stale completion cannot revive speech", async () => {
  const pending = deferred<Blob>();
  const observed = { signal: null as AbortSignal | null };
  const { controller, audios } = createHarness((_request, requestSignal) => {
    observed.signal = requestSignal;
    return pending.promise;
  });

  const speaking = controller.speak({ messageId: "assistant-1", language: "en" });
  assert.equal(controller.speechState, "synthesizing");
  assert.equal(observed.signal?.aborted, false);

  // resetConversation() calls clearSpeech(); this is the speech-side reset barrier.
  controller.clear();
  assert.equal(observed.signal?.aborted, true);
  assert.equal(controller.speechState, "idle");
  assert.equal(controller.hasReplay, false);

  // Simulate a fetch that resolves after AbortController was already triggered.
  pending.resolve(new Blob(["late wav"]));
  assert.equal(await speaking, "interrupted");
  assert.equal(controller.speechState, "idle");
  assert.equal(controller.hasReplay, false);
  assert.equal(audios.length, 0);
});

test("a new send can interrupt active speech without waiting for audio onended", async () => {
  const { controller, audios } = createHarness();

  const speaking = controller.speak({ messageId: "assistant-1", language: "en" });
  await waitFor(() => audios.length === 1 && controller.speechState === "speaking");

  const audio = audios[0]!;
  // useChatSession calls stopSpeech() only after the new text turn owns the
  // single-flight transport slot.
  controller.stop();

  assert.equal(audio.paused, true);
  assert.equal(audio.currentTime, 0);
  assert.equal(controller.speechState, "idle");
  assert.equal(await speaking, "interrupted");
});

test("a stale audio callback from the previous generation cannot end the new playback", async () => {
  const { controller, audios } = createHarness();

  const first = controller.speak({ messageId: "assistant-1", language: "en" });
  await waitFor(() => audios.length === 1 && controller.speechState === "speaking");
  const staleAudio = audios[0]!;

  controller.stop();
  assert.equal(await first, "interrupted");

  const second = controller.speak({ messageId: "assistant-2", language: "en" });
  await waitFor(() => audios.length === 2 && controller.speechState === "speaking");
  const currentAudio = audios[1]!;

  // Browser events may arrive late even after pause/stop. The generation token
  // and audio identity must make this callback a no-op.
  staleAudio.onended?.();
  assert.equal(controller.speechState, "speaking");

  currentAudio.onended?.();
  assert.equal(await second, "completed");
  assert.equal(controller.speechState, "idle");
});

test("unsupported language is informational and returns to idle when the lifecycle hold expires", async () => {
  const { controller, audios } = createHarness();

  const result = await controller.speak({
    messageId: "assistant-sv",
    language: "sv-SE",
  });

  assert.equal(result, "unsupported");
  assert.equal(controller.speechState, "unsupported");
  assert.equal(controller.hasReplay, false);
  assert.equal(audios.length, 0);

  // useChatSpeechLifecycle invokes this when the forced unsupported-status hold
  // timer expires. The state must have an explicit exit independent of send().
  controller.clearUnsupportedState();
  assert.equal(controller.speechState, "idle");
});
