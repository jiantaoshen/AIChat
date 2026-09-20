// This file converts semantic emotion/gesture choices into restrained deterministic motion so a small model cannot overanimate the avatar.
using AiAvatar.Backend.Models;

namespace AiAvatar.Backend.Services;

public static class AvatarMotionPolicy
{
    public static AvatarDecision Apply(AvatarDecision decision)
    {
        var gesture = decision.Gesture;
        var gestureIntensity = decision.GestureIntensity;

        switch (decision.Emotion)
        {
            case "neutral":
                if (gesture is "jump" or "shake")
                {
                    gesture = "none";
                    gestureIntensity = 0;
                }
                else if (gesture == "nod")
                {
                    gestureIntensity = Math.Min(gestureIntensity, 0.32);
                }
                break;

            case "happy":
                if (gesture == "shake")
                {
                    gesture = "nod";
                    gestureIntensity = Math.Clamp(gestureIntensity, 0.18, 0.48);
                }
                else if (gesture == "none" && decision.EmotionIntensity >= 0.82)
                {
                    gesture = "jump";
                    gestureIntensity = 0.38;
                }
                else if (gesture == "none" && decision.EmotionIntensity >= 0.48)
                {
                    gesture = "nod";
                    gestureIntensity = 0.22;
                }

                if (gesture == "jump")
                {
                    gestureIntensity = Math.Min(gestureIntensity, 0.58);
                }
                break;

            case "sad":
                if (gesture is "jump" or "shake")
                {
                    gesture = "none";
                    gestureIntensity = 0;
                }
                else if (gesture == "nod")
                {
                    gestureIntensity = Math.Min(gestureIntensity, 0.24);
                }
                break;

            case "angry":
                if (gesture == "jump")
                {
                    gesture = "shake";
                    gestureIntensity = Math.Clamp(gestureIntensity, 0.28, 0.62);
                }
                else if (gesture == "none" && decision.EmotionIntensity >= 0.72)
                {
                    gesture = "shake";
                    gestureIntensity = 0.32;
                }

                if (gesture == "shake")
                {
                    gestureIntensity = Math.Min(gestureIntensity, 0.62);
                }
                break;

            case "surprised":
                if (gesture == "shake")
                {
                    gesture = "jump";
                    gestureIntensity = Math.Clamp(gestureIntensity, 0.26, 0.58);
                }
                else if (gesture == "none" && decision.EmotionIntensity >= 0.58)
                {
                    gesture = "jump";
                    gestureIntensity = 0.34;
                }

                if (gesture == "jump")
                {
                    gestureIntensity = Math.Min(gestureIntensity, 0.58);
                }
                break;

            case "confused":
                if (gesture == "jump")
                {
                    gesture = "shake";
                    gestureIntensity = 0.2;
                }
                else if (gesture == "none" && decision.EmotionIntensity >= 0.65)
                {
                    gesture = "shake";
                    gestureIntensity = 0.16;
                }

                if (gesture == "shake")
                {
                    gestureIntensity = Math.Min(gestureIntensity, 0.3);
                }
                break;
        }

        return decision with
        {
            Gesture = gesture,
            GestureIntensity = Math.Clamp(gestureIntensity, 0, 1)
        };
    }
}
