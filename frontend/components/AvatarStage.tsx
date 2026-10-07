// This component renders the avatar scene, emotion sprite, restrained gesture animation, operational-state badge, and current assistant reply.
"use client";

import Image from "next/image";
import { useEffect } from "react";
import type { CSSProperties } from "react";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import type { Emotion, Gesture, OperationalState } from "@/types/chat";

interface AvatarStageProps {
  emotion: Emotion;
  emotionIntensity: number;
  gesture: Gesture;
  gestureIntensity: number;
  operationalState: OperationalState;
  animationKey: number;
  reply: string;
}

const spriteByEmotion: Record<Emotion, string> = {
  neutral: "/character/neutral.png",
  happy: "/character/happy.png",
  sad: "/character/sad.png",
  angry: "/character/angry.png",
  surprised: "/character/surprised.png",
  confused: "/character/confused.png",
};

const gestureClassByGesture: Record<Gesture, string> = {
  none: "",
  nod: "avatar-nod",
  shake: "avatar-shake",
  jump: "avatar-jump",
};

const thinkingSprite = "/character/thinking.png";
const allSprites = [...Object.values(spriteByEmotion), thinkingSprite];

export function AvatarStage({
  emotion,
  emotionIntensity,
  gesture,
  gestureIntensity,
  operationalState,
  animationKey,
  reply,
}: AvatarStageProps) {
  useEffect(() => {
    for (const source of allSprites) {
      const image = new window.Image();
      image.src = source;
    }
  }, []);

  const isThinking = operationalState === "thinking";
  const isSpeaking = operationalState === "speaking";
  const hasOperationalOverride = [
    "thinking",
    "synthesizing",
    "speaking",
    "unsupported",
  ].includes(operationalState);
  const sprite = isThinking ? thinkingSprite : spriteByEmotion[emotion];
  const visibleState =
    operationalState === "unsupported"
      ? "unsupported language"
      : hasOperationalOverride
        ? operationalState
        : emotion;
  const activeGesture =
    operationalState === "idle" ||
    operationalState === "unsupported" ||
    isSpeaking
      ? gesture
      : "none";
  const motionClass = isThinking
    ? "avatar-thinking"
    : gestureClassByGesture[activeGesture];

  const safeGestureIntensity = clamp01(
    activeGesture === "none" ? 0 : gestureIntensity,
  );
  const safeEmotionIntensity = clamp01(isThinking ? 0.35 : emotionIntensity);
  const nodDistance = Math.round(3 + safeGestureIntensity * 7);
  const shakeDistance = Math.round(3 + safeGestureIntensity * 9);
  const jumpDistance = Math.round(10 + safeGestureIntensity * 18);

  const motionStyle = {
    "--avatar-nod-distance": `${nodDistance}px`,
    "--avatar-shake-distance": `${shakeDistance}px`,
    "--avatar-shake-negative-distance": `${-shakeDistance}px`,
    "--avatar-shake-return-distance": `${Math.round(-shakeDistance * 0.55)}px`,
    "--avatar-jump-negative-distance": `${-jumpDistance}px`,
    "--avatar-character-opacity": `${0.97 + safeEmotionIntensity * 0.03}`,
    "--avatar-character-glow-alpha": `${0.05 + safeEmotionIntensity * 0.12}`,
  } as CSSProperties;

  return (
    <section
      className="avatar-stage relative min-h-[32rem] min-w-0 overflow-hidden rounded-3xl border bg-background shadow-sm xl:min-h-0"
      aria-label={`Avatar state: ${visibleState}`}
    >
      <div className="avatar-stage-sky absolute inset-0" aria-hidden="true" />
      <div className="avatar-stage-ground absolute inset-x-0 bottom-0 h-2/5" aria-hidden="true" />

      <div className="absolute inset-0 overflow-hidden">
        <div className="avatar-character-frame absolute left-1/2 -translate-x-1/2">
          <div
            key={`${animationKey}-${sprite}-${activeGesture}`}
            className={`avatar-character-motion relative size-full origin-[50%_30%] will-change-transform ${motionClass}`}
            style={motionStyle}
          >
            <Image
              src={sprite}
              alt={`Anime avatar showing ${visibleState}`}
              fill
              priority
              sizes="(max-width: 1279px) 100vw, 1280px"
              className="object-contain object-top"
            />
          </div>
        </div>
      </div>

      <Badge
        variant="secondary"
        className="absolute top-4 left-4 z-20 gap-2 border bg-background/90 px-3 py-1.5 text-xs capitalize shadow-sm backdrop-blur-md"
      >
        <span
          className="size-2 rounded-full bg-primary shadow-[0_0_0.75rem_rgb(113_150_201_/_55%)]"
          aria-hidden="true"
        />
        {visibleState}
      </Badge>

      <Card className="avatar-dialog-card absolute right-4 bottom-4 left-4 z-30 mx-auto max-w-5xl gap-0 overflow-hidden rounded-3xl py-0 backdrop-blur-md">
        <CardContent className="px-5 py-4">
          <div className="mb-2 flex items-center gap-3">
            <Badge className="rounded-full px-3 py-1 text-xs font-bold tracking-wider">
              AVATAR
            </Badge>
            <span className="text-xs font-semibold uppercase tracking-widest text-muted-foreground">
              local dialogue
            </span>
          </div>
          <p
            className={`min-h-12 whitespace-pre-wrap text-base leading-7 xl:text-lg${isThinking ? " animate-pulse" : ""}`}
            aria-live="polite"
          >
            {reply}
          </p>
        </CardContent>
      </Card>
    </section>
  );
}

function clamp01(value: number): number {
  if (!Number.isFinite(value)) {
    return 0;
  }

  return Math.min(1, Math.max(0, value));
}
