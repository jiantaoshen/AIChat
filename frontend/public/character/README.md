# Character sprite assets

This file documents the binary PNG files used by the frontend visual actuator because binary image files cannot contain source-code purpose comments.

All sprites use the same original yellow-and-white court-fantasy character design without glasses.

| File | Runtime purpose |
| --- | --- |
| `neutral.png` | Base idle expression |
| `thinking.png` | Local operational state shown immediately while inference is running |
| `confused.png` | Confused / uncertain semantic emotion |
| `happy.png` | Positive / warm semantic emotion |
| `sad.png` | Sad / sympathetic semantic emotion |
| `angry.png` | Angry / irritated semantic emotion |
| `surprised.png` | Surprised semantic emotion |

The LLM never directly chooses a filename. It outputs a supported semantic emotion, and `AvatarStage.tsx` maps that emotion to a sprite.
