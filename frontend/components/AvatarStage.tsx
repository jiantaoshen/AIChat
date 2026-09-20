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
      className="avatar-stage"
      aria-label={`Avatar state: ${visibleState}`}
    >
      <div className="avatar-stage-sky" aria-hidden="true" />
      <div className="avatar-stage-ground" aria-hidden="true" />
      <div
        className="avatar-stage-building avatar-stage-building-left"
        aria-hidden="true"
      />
      <div
        className="avatar-stage-building avatar-stage-building-right"
        aria-hidden="true"
      />
      <div className="avatar-stage-horizon" aria-hidden="true" />

      <div className="avatar-character-viewport">
        <div className="avatar-character-frame">
          <div
            key={`${animationKey}-${sprite}-${activeGesture}`}
            className={`avatar-character-motion ${motionClass}`}
            style={motionStyle}
          >
            <Image
              src={sprite}
              alt={`Anime avatar showing ${visibleState}`}
              fill
              priority
              sizes="(max-width: 768px) 94vw, (max-width: 1440px) 70vw, 76rem"
              className="avatar-character-image"
            />
          </div>
        </div>
      </div>

      <Badge variant="secondary" className="avatar-state-badge">
        <span className="avatar-state-dot" aria-hidden="true" />
        {visibleState}
      </Badge>

      <Card className="avatar-dialog-card">
        <CardContent className="avatar-dialog-content">
          <div className="avatar-dialog-heading">
            <Badge className="avatar-dialog-name">AVATAR</Badge>
            <span className="avatar-dialog-meta">local dialogue</span>
          </div>
          <p
            className={`avatar-dialog-text${isThinking ? " avatar-dialog-thinking" : ""}`}
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
