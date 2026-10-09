import assert from "node:assert/strict";
import test from "node:test";

import {
  AudioPlaybackController,
  type AudioLike,
} from "../hooks/audioPlaybackController.ts";
import {
  AvatarSpeechController,
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

  const playback = new AudioPlaybackController({
    createAudio: () => {
      const audio = new FakeAudio();
      audios.push(audio);
      return audio;
    },
    createObjectUrl: () => `blob:test-${++objectUrlCounter}`,
    revokeObjectUrl: (url) => revoked.push(url),
    onReplayAvailabilityChanged: (available) =>
      replayAvailability.push(available),
  });

  const controller = new AvatarSpeechController({
    synthesizeSpeech: synthesize,
    playback,
    onStateChanged: (state) => states.push(state),
  });

  return {
    controller,
    playback,
    states,
    replayAvailability,
    audios,
    revoked,
  };
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

  const speaking = controller.speak({ messageId: "assistant-1", supported: true });
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

test("a new send can interrupt delegated audio playback without waiting for onended", async () => {
  const { controller, audios } = createHarness();

  const speaking = controller.speak({ messageId: "assistant-1", supported: true });
  await waitFor(() => audios.length === 1 && controller.speechState === "speaking");

  const audio = audios[0]!;
  controller.stop();

  assert.equal(audio.paused, true);
  assert.equal(audio.currentTime, 0);
  assert.equal(controller.speechState, "idle");
  assert.equal(await speaking, "interrupted");
});

test("a stale audio callback owned by AudioPlaybackController cannot end the new speech", async () => {
  const { controller, audios } = createHarness();

  const first = controller.speak({ messageId: "assistant-1", supported: true });
  await waitFor(() => audios.length === 1 && controller.speechState === "speaking");
  const staleAudio = audios[0]!;

  controller.stop();
  assert.equal(await first, "interrupted");

  const second = controller.speak({ messageId: "assistant-2", supported: true });
  await waitFor(() => audios.length === 2 && controller.speechState === "speaking");
  const currentAudio = audios[1]!;

  staleAudio.onended?.();
  assert.equal(controller.speechState, "speaking");

  currentAudio.onended?.();
  assert.equal(await second, "completed");
  assert.equal(controller.speechState, "idle");
});

test("backend-reported unsupported speech is informational and does not call TTS transport", async () => {
  let synthesizeCalls = 0;
  const { controller, audios } = createHarness(async () => {
    synthesizeCalls += 1;
    return new Blob(["should-not-run"]);
  });

  const result = await controller.speak({
    messageId: "assistant-sv",
    supported: false,
  });

  assert.equal(result, "unsupported");
  assert.equal(controller.speechState, "unsupported");
  assert.equal(controller.hasReplay, false);
  assert.equal(synthesizeCalls, 0);
  assert.equal(audios.length, 0);

  controller.clearUnsupportedState();
  assert.equal(controller.speechState, "idle");
});
