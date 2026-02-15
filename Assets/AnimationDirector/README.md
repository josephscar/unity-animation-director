# Animation Director

Frame-accurate, data-driven animation action sequencer for Unity. Replaces fragile Animation Events with ScriptableObject timelines that trigger prefabs, sounds, and object state changes in sync with AnimationClips.

Current version: 1.0.1

## Features

- **Frame-Based Keyframes**: Integer frame numbers for precise timing
- **Multiple Action Types**: Spawn prefabs, play sounds, enable/disable objects, destroy spawned instances
- **Multiple Sequences**: Support for multiple animations in a single prefab
- **Editor Window**: Visual authoring tool with frame scrubber
- **Deterministic Runtime**: No string-based events, no reflection
- **Parenting Control**: Choose whether spawned objects follow the character or stay in world space

## Installation

### Option 1: Unity Package Manager (Git URL)
1. Open Unity Package Manager (Window -> Package Manager)
2. Click the `+` button -> "Add package from git URL"
3. Enter: `https://github.com/josephscar/unity-animation-director.git?path=/Assets/AnimationDirector#v1.0.1`

Note: If you're developing inside the package repo, don't add the Git URL to the same project. Use the local copy instead to avoid GUID conflicts.

### Option 2: Unity Package (.unitypackage)
1. Export the `Assets/AnimationDirector` folder as a `.unitypackage`
2. Import into your project via Assets -> Import Package -> Custom Package

### Option 3: Local Package
1. Copy the `AnimationDirector` folder to your project's `Assets` folder
2. The tool will be available immediately

## Quick Start

1. Create an AnimationActionSequence asset:
   - Right-click in Project -> Create -> Animation Director -> Action Sequence
   - Assign the target AnimationClip

2. Open the Animation Director window:
   - Window -> Animation Director

3. Author keyframes:
   - Select your sequence asset
   - Scrub to desired frame
   - Click "Add Keyframe at Current Frame"
   - Configure action type and parameters

4. Attach to your character:
   - Add `ActionSequencePlayer` component to your character GameObject
   - Assign the Animator reference
   - Add your sequence(s) to the Sequences list

5. Test in Play mode:
   - Trigger your animation
   - Keyframes will fire automatically at the correct frames

## Multiple Animations

To support multiple animations:

1. Create one `AnimationActionSequence` per animation clip
2. Add all sequences to the `ActionSequencePlayer`'s Sequences list
3. The player automatically selects the correct sequence based on the currently playing animation

## Action Types

- **SpawnPrefab**: Instantiate a prefab at a specific frame
  - Optional: Parent to anchor, set lifetime
- **PlaySound**: Play an AudioClip
- **EnableObject**: Activate a GameObject (supports detaching from parent)
- **DisableObject**: Deactivate a GameObject
- **DestroySpawned**: Destroy all prefabs spawned by this player

## Requirements

- Developed with Unity 6000.3.8f1 (Unity 6). Earlier versions are unverified.
- Animator component on your character

## Limitations (V1)

- No drag-and-drop timeline UI
- No SceneView live preview
- Manual prefab management

## License

MIT License - See [LICENSE.txt](LICENSE.txt) for details.

Copyright (c) 2026 Joseph Scarnecchia

## Support

For issues, questions, or contributions, contact: josephdscar@proton.me

