// This file renders the Hybrid Fluid visual-novel stage: a cropped upper-body avatar, local thinking/gesture motion, state badge, and the latest assistant reply layered over the image using the shared website theme.
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
  const sprite = isThinking ? thinkingSprite : spriteByEmotion[emotion];
  const visibleState = isThinking ? "thinking" : emotion;
  const activeGesture = isThinking ? "none" : gesture;
  const motionClass = isThinking
    ? "avatar-thinking"
    : gestureClassByGesture[activeGesture];

  const safeGestureIntensity = clamp01(isThinking ? 0 : gestureIntensity);
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
      className="avatar-stage relative min-h-[clamp(32rem,66dvh,54rem)] min-w-0 overflow-hidden rounded-3xl border border-border bg-background shadow-sm desktop:h-full desktop:min-h-0"
      aria-label={`Avatar state: ${visibleState}`}
    >
      <div className="avatar-stage-sky absolute inset-0" aria-hidden="true" />
      <div
        className="avatar-stage-ground absolute inset-x-0 bottom-0 h-[40%]"
        aria-hidden="true"
      />
      <div
        className="avatar-stage-building avatar-stage-building-left hidden"
        aria-hidden="true"
      />
      <div
        className="avatar-stage-building avatar-stage-building-right hidden"
        aria-hidden="true"
      />
      <div className="avatar-stage-horizon hidden" aria-hidden="true" />

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
              sizes="(max-width: 768px) 94vw, (max-width: 1440px) 70vw, 76rem"
              className="object-contain object-top"
            />
          </div>
        </div>
      </div>

      <Badge
        variant="secondary"
        className="absolute z-20 gap-2 border border-border bg-background/90 px-3 py-1.5 text-xs capitalize text-foreground shadow-sm backdrop-blur-md inset-bs-[clamp(0.75rem,1vw,1.25rem)] inset-s-[clamp(0.75rem,1vw,1.25rem)]"
      >
        <span
          className="size-2 rounded-full bg-primary shadow-[0_0_0.75rem_rgb(113_150_20_/55%)]"
          aria-hidden="true"
        />
        {visibleState}
      </Badge>

      <Card className="avatar-dialog-card absolute left-1/2 z-30 w-[min(calc(100%-var(--avatar-dialog-inset)-var(--avatar-dialog-inset)),var(--avatar-dialog-max-width))] -translate-x-1/2 gap-0 overflow-hidden py-0 backdrop-blur-md bottom-[clamp(1rem,2.2vw,2rem)]">
        <CardContent className="px-(--avatar-dialog-padding-x) py-(--avatar-dialog-padding-y)">
          <div className="mb-2 flex items-center gap-3">
            <Badge className="text-[0.68rem] font-bold tracking-[0.12em]">
              AVATAR
            </Badge>
            <span className="text-[0.68rem] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
              local dialogue
            </span>
          </div>
          <p
            className={`min-h-12 whitespace-pre-wrap text-(length:--avatar-dialog-text) leading-[1.75] text-foreground${isThinking ? " animate-pulse" : ""}`}
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
