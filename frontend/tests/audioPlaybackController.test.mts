import assert from "node:assert/strict";
import test from "node:test";

import {
  AudioPlaybackController,
  type AudioLike,
} from "../hooks/audioPlaybackController.ts";

class FakeAudio implements AudioLike {
  preload = "";
  currentTime = 0;
  onended: (() => void) | null = null;
  onerror: (() => void) | null = null;
  paused = false;

  async play() {}

  pause() {
    this.paused = true;
  }
}

function createHarness() {
  const audios: FakeAudio[] = [];
  const revoked: string[] = [];
  const replayAvailability: boolean[] = [];
  let objectUrlCounter = 0;

  const controller = new AudioPlaybackController({
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

  return { controller, audios, revoked, replayAvailability };
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

test("stop owns Audio pause/reset and resolves active playback as interrupted", async () => {
  const { controller, audios } = createHarness();
  controller.replace(new Blob(["wav"]));

  const playback = controller.play();
  await waitFor(() => audios.length === 1);

  const audio = audios[0]!;
  controller.stop();

  assert.equal(audio.paused, true);
  assert.equal(audio.currentTime, 0);
  assert.equal(await playback, "interrupted");
});

test("stale onended callback cannot complete a newer playback generation", async () => {
  const { controller, audios } = createHarness();
  controller.replace(new Blob(["wav"]));

  const first = controller.play();
  await waitFor(() => audios.length === 1);
  const staleAudio = audios[0]!;

  controller.stop();
  assert.equal(await first, "interrupted");

  const second = controller.play();
  await waitFor(() => audios.length === 2);
  const currentAudio = audios[1]!;
  let secondSettled = false;
  void second.finally(() => {
    secondSettled = true;
  });

  staleAudio.onended?.();
  await new Promise<void>((resolve) => setImmediate(resolve));
  assert.equal(secondSettled, false);

  currentAudio.onended?.();
  assert.equal(await second, "completed");
});

test("object URL and replay cache lifecycle are fully owned by playback controller", () => {
  const { controller, revoked, replayAvailability } = createHarness();

  controller.replace(new Blob(["first"]));
  assert.equal(controller.hasReplay, true);
  assert.deepEqual(replayAvailability, [true]);

  controller.replace(new Blob(["second"]));
  assert.deepEqual(revoked, ["blob:test-1"]);
  assert.equal(controller.hasReplay, true);
  assert.deepEqual(replayAvailability, [true]);

  controller.clear();
  assert.deepEqual(revoked, ["blob:test-1", "blob:test-2"]);
  assert.equal(controller.hasReplay, false);
  assert.deepEqual(replayAvailability, [true, false]);
});
