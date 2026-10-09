// Browser-audio resource owner used by AvatarSpeechController.
// It deliberately knows nothing about TTS languages, message IDs, HTTP transport,
// or React state. Its only job is deterministic playback/replay resource lifecycle.

export type AudioPlaybackResult = "completed" | "interrupted";

export interface AudioLike {
  preload: string;
  currentTime: number;
  onended: (() => void) | null;
  onerror: (() => void) | null;
  play(): Promise<void>;
  pause(): void;
}

interface PlaybackCompletion {
  resolve: (result: AudioPlaybackResult) => void;
  reject: (error: Error) => void;
}

export interface AudioPlaybackControllerDependencies {
  createAudio: (url: string) => AudioLike;
  createObjectUrl: (blob: Blob) => string;
  revokeObjectUrl: (url: string) => void;
  onReplayAvailabilityChanged: (hasReplay: boolean) => void;
}

export class AudioPlaybackController {
  private readonly dependencies: AudioPlaybackControllerDependencies;
  private audio: AudioLike | null = null;
  private audioUrl: string | null = null;
  private completion: PlaybackCompletion | null = null;
  private generation = 0;
  private replayAvailable = false;

  constructor(dependencies: AudioPlaybackControllerDependencies) {
    this.dependencies = dependencies;
  }

  get hasReplay() {
    return this.replayAvailable;
  }

  replace(blob: Blob) {
    this.stop();

    if (this.audioUrl) {
      this.dependencies.revokeObjectUrl(this.audioUrl);
    }

    this.audioUrl = this.dependencies.createObjectUrl(blob);
    this.setReplayAvailable(true);
  }

  async play(onStarted?: () => void): Promise<AudioPlaybackResult> {
    const url = this.audioUrl;
    if (!url) {
      throw new Error("No synthesized avatar voice is available to replay yet.");
    }

    this.stop();

    const generation = this.generation;
    const audio = this.dependencies.createAudio(url);
    audio.preload = "auto";
    this.audio = audio;

    return await new Promise<AudioPlaybackResult>((resolve, reject) => {
      this.completion = { resolve, reject };

      const isCurrentPlayback = () =>
        generation === this.generation && this.audio === audio;

      audio.onended = () => {
        if (!isCurrentPlayback()) {
          return;
        }

        this.finishCurrentPlayback("completed");
      };

      audio.onerror = () => {
        if (!isCurrentPlayback()) {
          return;
        }

        this.finishCurrentPlayback(
          "completed",
          new Error("The browser could not play the synthesized WAV audio."),
        );
      };

      void audio.play().then(
        () => {
          if (isCurrentPlayback()) {
            onStarted?.();
          } else {
            audio.pause();
          }
        },
        (caught) => {
          if (!isCurrentPlayback()) {
            return;
          }

          this.finishCurrentPlayback(
            "completed",
            caught instanceof Error
              ? caught
              : new Error("Browser audio playback was blocked."),
          );
        },
      );
    });
  }

  stop() {
    this.generation += 1;

    if (this.audio) {
      this.audio.pause();
      this.audio.currentTime = 0;
    }

    this.finishCurrentPlayback("interrupted");
  }

  clear() {
    this.stop();

    if (this.audioUrl) {
      this.dependencies.revokeObjectUrl(this.audioUrl);
      this.audioUrl = null;
    }

    this.setReplayAvailable(false);
  }

  dispose() {
    this.generation += 1;
    this.audio?.pause();
    this.audio = null;
    this.completion?.resolve("interrupted");
    this.completion = null;

    if (this.audioUrl) {
      this.dependencies.revokeObjectUrl(this.audioUrl);
      this.audioUrl = null;
    }

    // The React adapter may already be unmounting. Avoid emitting callbacks from
    // disposal and only update internal ownership state.
    this.replayAvailable = false;
  }

  private finishCurrentPlayback(
    result: AudioPlaybackResult,
    error?: Error,
  ) {
    this.audio = null;

    const completion = this.completion;
    this.completion = null;
    if (!completion) {
      return;
    }

    if (error) {
      completion.reject(error);
    } else {
      completion.resolve(result);
    }
  }

  private setReplayAvailable(hasReplay: boolean) {
    if (this.replayAvailable === hasReplay) {
      return;
    }

    this.replayAvailable = hasReplay;
    this.dependencies.onReplayAvailabilityChanged(hasReplay);
  }
}
