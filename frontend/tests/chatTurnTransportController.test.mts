import assert from "node:assert/strict";
import test from "node:test";
import { ChatTurnTransportController } from "../hooks/chatTurnTransportController.ts";

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason?: unknown) => void;
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });

  return { promise, resolve, reject };
}

test("reset aborts the pending request and ignores a stale completion", async () => {
  const pending = deferred<string>();
  let requestSignal: AbortSignal | undefined;
  let started = 0;
  const controller = new ChatTurnTransportController<string>(
    (_request, signal) => {
      requestSignal = signal;
      return pending.promise;
    },
    () => "turn-1",
  );

  const execution = controller.execute({
    sessionId: 1,
    conversationId: null,
    message: "hello",
    onStart: () => {
      started += 1;
    },
  });

  assert.equal(started, 1);
  assert.equal(requestSignal?.aborted, false);

  controller.reset();
  assert.equal(requestSignal?.aborted, true);

  // Deliberately resolve anyway to simulate a response that crossed the abort/reset race.
  pending.resolve("stale response");

  assert.equal(await execution, null);
});

test("a retry of the same logical send reuses its turnId", async () => {
  const seenTurnIds: string[] = [];
  let attempt = 0;
  const generatedIds = ["turn-a", "turn-b"];
  const controller = new ChatTurnTransportController<string>(
    async (request) => {
      seenTurnIds.push(request.turnId);
      attempt += 1;
      if (attempt === 1) {
        throw new Error("temporary failure");
      }

      return "ok";
    },
    () => generatedIds.shift() ?? "unexpected",
  );
  const options = {
    sessionId: 4,
    conversationId: "conversation-1",
    message: "retry me",
    onStart: () => {},
  };

  await assert.rejects(controller.execute(options), /temporary failure/);
  const retry = await controller.execute(options);

  assert.deepEqual(seenTurnIds, ["turn-a", "turn-a"]);
  assert.equal(retry?.turnId, "turn-a");
  assert.equal(retry?.response, "ok");
});

test("reset clears retry identity so a new session receives a new turnId", async () => {
  const seenTurnIds: string[] = [];
  const generatedIds = ["turn-a", "turn-b"];
  let shouldFail = true;
  const controller = new ChatTurnTransportController<string>(
    async (request) => {
      seenTurnIds.push(request.turnId);
      if (shouldFail) {
        throw new Error("temporary failure");
      }

      return "ok";
    },
    () => generatedIds.shift() ?? "unexpected",
  );

  await assert.rejects(
    controller.execute({
      sessionId: 1,
      conversationId: null,
      message: "hello",
      onStart: () => {},
    }),
  );

  controller.reset();
  shouldFail = false;
  await controller.execute({
    sessionId: 2,
    conversationId: null,
    message: "hello",
    onStart: () => {},
  });

  assert.deepEqual(seenTurnIds, ["turn-a", "turn-b"]);
});
