import assert from "node:assert/strict";
import test from "node:test";

import {
  isOperationalStateBusy,
  shouldForceSpeechStatusHold,
} from "../lib/operationalState.ts";

test("unsupported is visible status but not an operational busy state", () => {
  assert.equal(isOperationalStateBusy("unsupported"), false);
  assert.equal(isOperationalStateBusy("idle"), false);
  assert.equal(isOperationalStateBusy("error"), false);
});

test("only active work states are operationally busy", () => {
  assert.equal(isOperationalStateBusy("thinking"), true);
  assert.equal(isOperationalStateBusy("synthesizing"), true);
  assert.equal(isOperationalStateBusy("speaking"), true);
});

test("unsupported forces a hold timer even for a neutral decision", () => {
  assert.equal(shouldForceSpeechStatusHold("unsupported"), true);
  assert.equal(shouldForceSpeechStatusHold("completed"), false);
  assert.equal(shouldForceSpeechStatusHold("interrupted"), false);
});
