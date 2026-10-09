// Deterministic speech synthesis/playback state machine used by useAvatarSpeech.
// Keeping the generation token and audio callbacks here makes the race-prone
// lifecycle testable without React or a browser DOM.

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
  language: string;
}

export interface SpeechTransportRequest {
  messageId: string;
}

export interface AudioLike {
  preload: string;
  currentTime: number;
  onended: (() => void) | null;
  onerror: (() => void) | null;
  play(): Promise<void>;
  pause(): void;
}

interface PlaybackCompletion {
  resolve: () => void;
  reject: (error: Error) => void;
}

export interface AvatarSpeechControllerDependencies {
  synthesizeSpeech: (
    request: SpeechTransportRequest,
    signal: AbortSignal,
  ) => Promise<Blob>;
  createAudio: (url: string) => AudioLike;
  createObjectUrl: (blob: Blob) => string;
  revokeObjectUrl: (url: string) => void;
  onStateChanged: (state: SpeechPlaybackState) => void;
  onReplayAvailabilityChanged: (hasReplay: boolean) => void;
  onError?: (message: string) => void;
}

const COSYVOICE_SUPPORTED_LANGUAGES = new Set([
  "zh",
  "en",
  "ja",
  "ko",
  "de",
  "es",
  "fr",
  "it",
  "ru",
]);

export class AvatarSpeechController {
  private readonly dependencies: AvatarSpeechControllerDependencies;
  private onError: ((message: string) => void) | undefined;
  private audio: AudioLike | null = null;
  private audioUrl: string | null = null;
  private abortController: AbortController | null = null;
  private completion: PlaybackCompletion | null = null;
  private generation = 0;
  private state: SpeechPlaybackState = "idle";
  private replayAvailable = false;

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
    return this.replayAvailable;
  }

  async speak(request: AvatarSpeechRequest): Promise<SpeechPlaybackResult> {
    this.stop();

    const language = normalizeLanguageCode(request.language);
    if (!COSYVOICE_SUPPORTED_LANGUAGES.has(language)) {
      this.clearReplayAudio();
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
      this.replaceAudioUrl(blob);
      await this.playCurrentUrl();

      return generation === this.generation ? "completed" : "interrupted";
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
      await this.playCurrentUrl();
      return generation === this.generation ? "completed" : "interrupted";
    } catch (caught) {
      if (generation !== this.generation) {
        return "interrupted";
      }

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

    if (this.audio) {
      this.audio.pause();
      this.audio.currentTime = 0;
    }

    this.finishCurrentPlayback();
  }

  clearUnsupportedState() {
    if (this.state === "unsupported") {
      this.setState("idle");
    }
  }

  clear() {
    this.stop();
    this.clearReplayAudio();
  }

  dispose() {
    this.generation += 1;
    this.abortController?.abort();
    this.abortController = null;
    this.audio?.pause();
    this.audio = null;
    this.completion?.resolve();
    this.completion = null;

    if (this.audioUrl) {
      this.dependencies.revokeObjectUrl(this.audioUrl);
      this.audioUrl = null;
    }

    // React may be unmounting; update only internal state here and do not emit
    // state callbacks into a disposed adapter.
    this.replayAvailable = false;
    this.state = "idle";
  }

  private async playCurrentUrl(): Promise<void> {
    const url = this.audioUrl;
    if (!url) {
      throw new Error("No synthesized avatar voice is available to replay yet.");
    }

    this.audio?.pause();

    const generation = this.generation;
    const audio = this.dependencies.createAudio(url);
    audio.preload = "auto";
    this.audio = audio;

    return await new Promise<void>((resolve, reject) => {
      this.completion = { resolve, reject };

      const isCurrentPlayback = () =>
        generation === this.generation && this.audio === audio;

      audio.onended = () => {
        if (!isCurrentPlayback()) {
          return;
        }
        this.finishCurrentPlayback();
      };

      audio.onerror = () => {
        if (!isCurrentPlayback()) {
          return;
        }

        this.finishCurrentPlayback(
          new Error("The browser could not play the synthesized WAV audio."),
        );
      };

      void audio.play().then(
        () => {
          if (isCurrentPlayback()) {
            this.setState("speaking");
          } else {
            audio.pause();
          }
        },
        (caught) => {
          if (!isCurrentPlayback()) {
            return;
          }

          this.finishCurrentPlayback(
            caught instanceof Error
              ? caught
              : new Error("Browser audio playback was blocked."),
          );
        },
      );
    });
  }

  private finishCurrentPlayback(error?: Error) {
    this.audio = null;
    this.setState("idle");

    const completion = this.completion;
    this.completion = null;
    if (!completion) {
      return;
    }

    if (error) {
      completion.reject(error);
    } else {
      completion.resolve();
    }
  }

  private replaceAudioUrl(blob: Blob) {
    if (this.audioUrl) {
      this.dependencies.revokeObjectUrl(this.audioUrl);
    }

    this.audioUrl = this.dependencies.createObjectUrl(blob);
    this.setReplayAvailable(true);
  }

  private clearReplayAudio() {
    if (this.audioUrl) {
      this.dependencies.revokeObjectUrl(this.audioUrl);
      this.audioUrl = null;
    }

    this.setReplayAvailable(false);
  }

  private setState(state: SpeechPlaybackState) {
    if (this.state === state) {
      return;
    }

    this.state = state;
    this.dependencies.onStateChanged(state);
  }

  private setReplayAvailable(hasReplay: boolean) {
    if (this.replayAvailable === hasReplay) {
      return;
    }

    this.replayAvailable = hasReplay;
    this.dependencies.onReplayAvailabilityChanged(hasReplay);
  }
}

function normalizeLanguageCode(language: string): string {
  return language.trim().toLowerCase().replace("_", "-").split("-", 1)[0];
}
