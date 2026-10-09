// React adapter around the deterministic AvatarSpeechController.
"use client";

import { useCallback, useEffect, useState } from "react";
import {
  AvatarSpeechController,
  type AvatarSpeechRequest,
  type AudioLike,
  type SpeechPlaybackResult,
  type SpeechPlaybackState,
} from "@/hooks/avatarSpeechController";
import { synthesizeSpeech } from "@/lib/api";

export type { SpeechPlaybackResult, SpeechPlaybackState };

interface UseAvatarSpeechOptions {
  onError?: (message: string) => void;
}

export function useAvatarSpeech({ onError }: UseAvatarSpeechOptions = {}) {
  const [speechState, setSpeechState] = useState<SpeechPlaybackState>("idle");
  const [hasReplay, setHasReplay] = useState(false);
  const [controller] = useState(
    () =>
      new AvatarSpeechController({
        synthesizeSpeech,
        createAudio: (url) => new Audio(url) as AudioLike,
        createObjectUrl: (blob) => URL.createObjectURL(blob),
        revokeObjectUrl: (url) => URL.revokeObjectURL(url),
        onStateChanged: setSpeechState,
        onReplayAvailabilityChanged: setHasReplay,
        onError,
      }),
  );

  useEffect(() => {
    controller.setErrorHandler(onError);
  }, [controller, onError]);

  const speak = useCallback(
    async (request: AvatarSpeechRequest): Promise<SpeechPlaybackResult> =>
      await controller.speak(request),
    [controller],
  );

  const replay = useCallback(
    async (): Promise<SpeechPlaybackResult> => await controller.replay(),
    [controller],
  );

  const stopSpeech = useCallback(() => {
    controller.stop();
  }, [controller]);

  const clearUnsupportedState = useCallback(() => {
    controller.clearUnsupportedState();
  }, [controller]);

  const clearSpeech = useCallback(() => {
    controller.clear();
  }, [controller]);

  useEffect(() => {
    return () => controller.dispose();
  }, [controller]);

  return {
    speechState,
    hasReplay,
    speak,
    replay,
    stopSpeech,
    clearSpeech,
    clearUnsupportedState,
  };
}
