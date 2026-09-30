// This file renders the Hybrid Fluid visual-novel stage; only image composition/effects and motion keyframes live in globals.css, while layout stays in Tailwind class names.
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
  const hasOperationalOverride =
    operationalState === "thinking" ||
    operationalState === "synthesizing" ||
    operationalState === "speaking";

  const sprite = isThinking ? thinkingSprite : spriteByEmotion[emotion];
  const visibleState = hasOperationalOverride ? operationalState : emotion;
  const gestureAllowed = operationalState === "idle" || isSpeaking;
  const activeGesture = gestureAllowed ? gesture : "none";
  const motionClass = isThinking
    ? "avatar-thinking"
    : activeGesture !== "none"
      ? gestureClassByGesture[activeGesture]
      : "";

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
      className="avatar-stage relative min-w-0 overflow-hidden rounded-3xl border border-border bg-background shadow-sm [height:var(--avatar-stage-height)] [min-height:var(--avatar-stage-min-height)]"
      aria-label={`Avatar state: ${visibleState}`}
    >
      <div className="avatar-stage-sky absolute inset-0" aria-hidden="true" />
      <div
        className="avatar-stage-ground absolute inset-x-0 bottom-0 h-[40%]"
        aria-hidden="true"
      />

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
        className="absolute top-[var(--avatar-stage-status-inset)] left-[var(--avatar-stage-status-inset)] z-20 gap-2 border border-border bg-background/90 px-3 py-1.5 text-xs capitalize text-foreground shadow-sm backdrop-blur-md"
      >
        <span
          className="size-2 rounded-full bg-primary shadow-[0_0_0.75rem_rgb(113_150_201_/_55%)]"
          aria-hidden="true"
        />
        {visibleState}
      </Badge>

      <Card className="avatar-dialog-card absolute bottom-[var(--avatar-dialog-bottom)] left-1/2 z-30 w-[calc(100%_-_var(--avatar-dialog-inset)_-_var(--avatar-dialog-inset))] max-w-[var(--avatar-dialog-max-width)] -translate-x-1/2 gap-0 overflow-hidden rounded-3xl border-border py-0 backdrop-blur-md">
        <CardContent className="px-[var(--avatar-dialog-padding-x)] py-[var(--avatar-dialog-padding-y)]">
          <div className="mb-2 flex items-center gap-3">
            <Badge className="rounded-full bg-primary px-3 py-1 text-[0.68rem] font-bold tracking-[0.12em] text-primary-foreground">
              AVATAR
            </Badge>
            <span className="text-[0.68rem] font-semibold uppercase tracking-[0.16em] text-muted-foreground">
              local dialogue
            </span>
          </div>
          <p
            className={`min-h-12 whitespace-pre-wrap text-[var(--avatar-dialog-text)] leading-[1.75] text-foreground${isThinking ? " animate-pulse" : ""}`}
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
