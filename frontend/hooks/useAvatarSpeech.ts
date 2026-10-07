// This file synthesizes supported assistant replies with local CosyVoice, plays the returned WAV in the browser, exposes speaking/unsupported state, and keeps a replayable copy of the latest utterance.
"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { synthesizeSpeech } from "@/lib/api";
import type { SpeechSynthesisRequest } from "@/types/chat";

export type SpeechPlaybackState =
  | "idle"
  | "synthesizing"
  | "speaking"
  | "unsupported";

export type SpeechPlaybackResult =
  | "completed"
  | "unsupported"
  | "interrupted";

interface AvatarSpeechRequest extends SpeechSynthesisRequest {
  language: string;
}

interface UseAvatarSpeechOptions {
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

export function useAvatarSpeech({ onError }: UseAvatarSpeechOptions = {}) {
  const [speechState, setSpeechState] = useState<SpeechPlaybackState>("idle");
  const [hasReplay, setHasReplay] = useState(false);
  const audioRef = useRef<HTMLAudioElement | null>(null);
  const audioUrlRef = useRef<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);
  const completionRef = useRef<{
    resolve: () => void;
    reject: (error: Error) => void;
  } | null>(null);
  const generationRef = useRef(0);

  const finishPlayback = useCallback((error?: Error) => {
    audioRef.current = null;
    setSpeechState("idle");

    const completion = completionRef.current;
    completionRef.current = null;

    if (!completion) {
      return;
    }

    if (error) {
      completion.reject(error);
    } else {
      completion.resolve();
    }
  }, []);

  const stopSpeech = useCallback(() => {
    generationRef.current += 1;
    abortRef.current?.abort();
    abortRef.current = null;

    if (audioRef.current) {
      audioRef.current.pause();
      audioRef.current.currentTime = 0;
    }

    finishPlayback();
  }, [finishPlayback]);

  const replaceAudioUrl = useCallback((blob: Blob) => {
    if (audioUrlRef.current) {
      URL.revokeObjectURL(audioUrlRef.current);
    }

    audioUrlRef.current = URL.createObjectURL(blob);
    setHasReplay(true);
  }, []);

  const playCurrentUrl = useCallback(async (): Promise<void> => {
    const url = audioUrlRef.current;
    if (!url) {
      throw new Error("No synthesized avatar voice is available to replay yet.");
    }

    if (audioRef.current) {
      audioRef.current.pause();
    }

    const audio = new Audio(url);
    audio.preload = "auto";
    audioRef.current = audio;

    return await new Promise<void>((resolve, reject) => {
      completionRef.current = { resolve, reject };

      audio.onended = () => {
        finishPlayback();
      };

      audio.onerror = () => {
        const error = new Error("The browser could not play the synthesized WAV audio.");
        finishPlayback(error);
      };

      void audio.play().then(
        () => {
          setSpeechState("speaking");
        },
        (caught) => {
          finishPlayback(
            caught instanceof Error
              ? caught
              : new Error("Browser audio playback was blocked."),
          );
        },
      );
    });
  }, [finishPlayback]);

  const speak = useCallback(
    async (request: AvatarSpeechRequest): Promise<SpeechPlaybackResult> => {
      stopSpeech();

      const language = normalizeLanguageCode(request.language);
      if (!COSYVOICE_SUPPORTED_LANGUAGES.has(language)) {
        setSpeechState("unsupported");
        return "unsupported";
      }

      const generation = generationRef.current;
      const controller = new AbortController();
      abortRef.current = controller;
      setSpeechState("synthesizing");

      try {
        const speechRequest: SpeechSynthesisRequest = {
          messageId: request.messageId,
          text: request.text,
          emotion: request.emotion,
          emotionIntensity: request.emotionIntensity,
        };
        const blob = await synthesizeSpeech(speechRequest, controller.signal);

        if (generation !== generationRef.current) {
          return "interrupted";
        }

        abortRef.current = null;
        replaceAudioUrl(blob);
        await playCurrentUrl();
        return "completed";
      } catch (caught) {
        if (controller.signal.aborted) {
          return "interrupted";
        }

        setSpeechState("idle");
        const message =
          caught instanceof Error ? caught.message : "Unknown local TTS error.";
        onError?.(message);
        throw caught;
      }
    },
    [onError, playCurrentUrl, replaceAudioUrl, stopSpeech],
  );

  const replay = useCallback(async (): Promise<void> => {
    stopSpeech();

    try {
      await playCurrentUrl();
    } catch (caught) {
      const message =
        caught instanceof Error ? caught.message : "Could not replay avatar speech.";
      onError?.(message);
      throw caught;
    }
  }, [onError, playCurrentUrl, stopSpeech]);

  const clearSpeech = useCallback(() => {
    stopSpeech();

    if (audioUrlRef.current) {
      URL.revokeObjectURL(audioUrlRef.current);
      audioUrlRef.current = null;
    }

    setHasReplay(false);
  }, [stopSpeech]);

  useEffect(() => {
    return () => {
      abortRef.current?.abort();
      audioRef.current?.pause();

      if (audioUrlRef.current) {
        URL.revokeObjectURL(audioUrlRef.current);
      }
    };
  }, []);

  return {
    speechState,
    hasReplay,
    speak,
    replay,
    stopSpeech,
    clearSpeech,
  };
}

function normalizeLanguageCode(language: string): string {
  return language.trim().toLowerCase().replace("_", "-").split("-", 1)[0];
}
