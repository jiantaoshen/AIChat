import assert from "node:assert/strict";
import test from "node:test";

import { AvatarSpeechController } from "../hooks/avatarSpeechController.ts";
import { completeSpeechHold } from "../hooks/chatSpeechLifecyclePolicy.ts";

function createUnsupportedController() {
  return new AvatarSpeechController({
    synthesizeSpeech: async () => new Blob(["unused"]),
    createAudio: () => {
      throw new Error("unsupported language must not create audio");
    },
    createObjectUrl: () => "blob:unused",
    revokeObjectUrl: () => {},
    onStateChanged: () => {},
    onReplayAvailabilityChanged: () => {},
  });
}

test("unsupported speech hold expiry returns the current turn to idle and neutral", async () => {
  const controller = createUnsupportedController();
  let neutralResets = 0;
  const identity = { sessionId: 7, turnGeneration: 3 };

  assert.equal(
    await controller.speak({ messageId: "assistant-sv", supported: false }),
    "unsupported",
  );
  assert.equal(controller.speechState, "unsupported");

  const applied = completeSpeechHold({
    identity,
    isCurrentTurn: (candidate) =>
      candidate.sessionId === identity.sessionId &&
      candidate.turnGeneration === identity.turnGeneration,
    clearUnsupportedState: () => controller.clearUnsupportedState(),
    onNeutralReset: () => {
      neutralResets += 1;
    },
  });

  assert.equal(applied, true);
  assert.equal(controller.speechState, "idle");
  assert.equal(neutralResets, 1);
});

test("a stale speech hold callback cannot clear or neutralize the current turn", async () => {
  const controller = createUnsupportedController();
  let neutralResets = 0;

  await controller.speak({ messageId: "assistant-sv", supported: false });

  const applied = completeSpeechHold({
    identity: { sessionId: 1, turnGeneration: 1 },
    isCurrentTurn: () => false,
    clearUnsupportedState: () => controller.clearUnsupportedState(),
    onNeutralReset: () => {
      neutralResets += 1;
    },
  });

  assert.equal(applied, false);
  assert.equal(controller.speechState, "unsupported");
  assert.equal(neutralResets, 0);
});
