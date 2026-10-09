// Deterministic TTS orchestration used by useAvatarSpeech.
// Browser Audio/object-URL/replay ownership lives in AudioPlaybackController so
// this controller can stay focused on synthesis, interruption, and speech state.

import { AudioPlaybackController } from "./audioPlaybackController.ts";

export type SpeechPlaybackState =
  | "idle"
  | "synthesizing"
  | "speaking"
  | "unsupported";

export type SpeechPlaybackResult =
  | "completed"
  | "unsupported"
  | "interrupted";

export interface AvatarSpeechRequest {
  messageId: string;
  supported: boolean;
}

export interface SpeechTransportRequest {
  messageId: string;
}

export interface AvatarSpeechControllerDependencies {
  synthesizeSpeech: (
    request: SpeechTransportRequest,
    signal: AbortSignal,
  ) => Promise<Blob>;
  playback: AudioPlaybackController;
  onStateChanged: (state: SpeechPlaybackState) => void;
  onError?: (message: string) => void;
}

export class AvatarSpeechController {
  private readonly dependencies: AvatarSpeechControllerDependencies;
  private onError: ((message: string) => void) | undefined;
  private abortController: AbortController | null = null;
  private generation = 0;
  private state: SpeechPlaybackState = "idle";

  constructor(dependencies: AvatarSpeechControllerDependencies) {
    this.dependencies = dependencies;
    this.onError = dependencies.onError;
  }

  setErrorHandler(onError: ((message: string) => void) | undefined) {
    this.onError = onError;
  }

  get speechState() {
    return this.state;
  }

  get hasReplay() {
    return this.dependencies.playback.hasReplay;
  }

  async speak(request: AvatarSpeechRequest): Promise<SpeechPlaybackResult> {
    this.stop();

    // `supported` is a backend-owned capability returned by the Chat API.
    // The frontend intentionally does not maintain a language policy.
    if (!request.supported) {
      this.dependencies.playback.clear();
      this.setState("unsupported");
      return "unsupported";
    }

    const generation = this.generation;
    const controller = new AbortController();
    this.abortController = controller;
    this.setState("synthesizing");

    try {
      const blob = await this.dependencies.synthesizeSpeech(
        { messageId: request.messageId },
        controller.signal,
      );

      if (generation !== this.generation) {
        return "interrupted";
      }

      this.abortController = null;
      this.dependencies.playback.replace(blob);

      const playbackResult = await this.dependencies.playback.play(() => {
        if (generation === this.generation) {
          this.setState("speaking");
        }
      });

      if (
        generation !== this.generation ||
        playbackResult === "interrupted"
      ) {
        return "interrupted";
      }

      this.setState("idle");
      return "completed";
    } catch (caught) {
      if (controller.signal.aborted || generation !== this.generation) {
        return "interrupted";
      }

      this.setState("idle");
      const message =
        caught instanceof Error ? caught.message : "Unknown local TTS error.";
      this.onError?.(message);
      throw caught;
    }
  }

  async replay(): Promise<SpeechPlaybackResult> {
    this.stop();
    const generation = this.generation;

    try {
      const playbackResult = await this.dependencies.playback.play(() => {
        if (generation === this.generation) {
          this.setState("speaking");
        }
      });

      if (
        generation !== this.generation ||
        playbackResult === "interrupted"
      ) {
        return "interrupted";
      }

      this.setState("idle");
      return "completed";
    } catch (caught) {
      if (generation !== this.generation) {
        return "interrupted";
      }

      this.setState("idle");
      const message =
        caught instanceof Error
          ? caught.message
          : "Could not replay avatar speech.";
      this.onError?.(message);
      throw caught;
    }
  }

  stop() {
    this.generation += 1;
    this.abortController?.abort();
    this.abortController = null;
    this.dependencies.playback.stop();
    this.setState("idle");
  }

  clearUnsupportedState() {
    if (this.state === "unsupported") {
      this.setState("idle");
    }
  }

  clear() {
    this.stop();
    this.dependencies.playback.clear();
  }

  dispose() {
    this.generation += 1;
    this.abortController?.abort();
    this.abortController = null;
    this.dependencies.playback.dispose();

    // React may be unmounting; update only internal state here and do not emit
    // state callbacks into a disposed adapter.
    this.state = "idle";
  }

  private setState(state: SpeechPlaybackState) {
    if (this.state === state) {
      return;
    }

    this.state = state;
    this.dependencies.onStateChanged(state);
  }
}
