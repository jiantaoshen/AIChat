import assert from "node:assert/strict";
import test from "node:test";

import {
  chatSessionReducer,
  createInitialChatSessionState,
} from "../hooks/chatSessionState.ts";

const userMessage = { role: "user" as const, content: "hello" };

const fallbackDecision = {
  speech: "",
  language: "en",
  emotion: "confused" as const,
  emotionIntensity: 0.5,
  gesture: "none" as const,
  gestureIntensity: 0,
};

test("reset while LLM is thinking removes the optimistic user message and returns to idle", () => {
  const initial = createInitialChatSessionState();
  const thinking = chatSessionReducer(initial, {
    type: "sendStarted",
    userMessage,
  });

  assert.equal(thinking.chatState, "thinking");
  assert.equal(thinking.messages.at(-1)?.content, "hello");

  const reset = chatSessionReducer(thinking, { type: "reset" });

  assert.equal(reset.chatState, "idle");
  assert.equal(reset.conversationId, null);
  assert.equal(reset.input, "");
  assert.equal(reset.error, null);
  assert.equal(reset.messages.length, initial.messages.length);
  assert.deepEqual(reset.messages, initial.messages);
});

test("single-flight send failure removes exactly its optimistic user message and restores retry input", () => {
  const initial = createInitialChatSessionState();
  const thinking = chatSessionReducer(initial, {
    type: "sendStarted",
    userMessage,
  });

  const failed = chatSessionReducer(thinking, {
    type: "sendFailed",
    message: "temporary failure",
    decision: fallbackDecision,
    retryMessage: "hello",
  });

  assert.equal(failed.chatState, "error");
  assert.equal(failed.input, "hello");
  assert.equal(failed.error, "temporary failure");
  assert.deepEqual(failed.messages, initial.messages);
});
