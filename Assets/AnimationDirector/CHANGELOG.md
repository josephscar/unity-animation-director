# Changelog

All notable changes to Animation Director will be documented in this file.

## [1.0.2] - 2026-02-16
- Fix clip matching to use the actual Animator clip (better blend tree/retimed state support)
- Allow multiple keyframes on the same frame to fire correctly
- Catch up keyframes when animation time jumps forward (no missed early frames)

## [1.0.1] - 2026-02-15

### Fixed
- Non-looping animations now execute action keyframes reliably (no early bailout after completion).
- Replays of the same state now reset keyframe firing even if the state hash doesn't change.

## [1.0.0] - 2026-02-13

### Added
- Initial release
- Frame-based action keyframes
- AnimationDirectorWindow editor tool
- ActionSequencePlayer runtime component
- Support for multiple sequences per prefab
- Action types: SpawnPrefab, PlaySound, EnableObject, DisableObject, DestroySpawned
- Parenting control for spawned objects
- State change detection to prevent cross-animation contamination
- Looping vs non-looping clip support

### Features
- Frame scrubber in editor window
- Delayed frame editing (updates on Enter/click away)
- Automatic sequence matching based on playing animation
- Target binding system for Enable/Disable actions
