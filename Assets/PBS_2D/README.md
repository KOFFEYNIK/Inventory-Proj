# PBS 2D - Physics-Based Shooter Framework

## Introduction
Thank you for supporting my work! Please keep in mind that this is an early version, so there may be missing features and some scripts could be improved.

## Requirements
- Unity 6000.4.0f1 or later
- Universal Render Pipeline (URP)
- Input System

## Overview
PBS 2D (physics-based shooter 2D) is a side-view 2D shooter built around physics, inverse kinematics, and procedural animations.

### Files
Everything is in the PBS_2D folder:
- **Data** - ScriptableObject configurations
- **Materials** - Materials and physics materials
- **Prefabs** - Ready-to-use prefabs organized by category
- **Scenes** - Demo scene
- **Scripts** - All C# source code
- **Sounds** - Audio clips (.wav)
- **Sprites** - Sprite assets
- **Tiles** - Tileset assets

### Characters
A character is a chain of Rigidbody2D body parts connected by HingeJoint2D joints. All characters are built from the **Human** base prefab, which contains the body structure and components.

#### Character Components
- **Character** - Central coordinator, holds references to every body part, joint, and IK solver
- **CharacterHealth** - Manages health and death
- **CharacterMovement** - Handles horizontal movement and jumping (input movement)
- **LegsController** - Procedural leg animation controller
- **TorsoHeightController** - Keeps the torso (and with that, the whole character) at a set height above the ground
- **BodyPhysicsController** - Manages the physics of the body parts of a character
- **WeaponManager** - Manages the weapon of the character (equip, drop, shoot, etc.)
- **InteractionHandler** - Detects nearby interactable objects (weapons, items)
- **CharacterSkin** - Applies a SkinConfig to swap character sprites
- **CharacterRotation** - Rotates the character to face the aim direction 

The **Character** script checks the `IsPlayer` flag at runtime to decide what to add:

- If `IsPlayer` is **true**: adds a **PlayerInputHandler** (keyboard, gamepad, and touch input manager that allows these inputs to control the character) and an **InteractionHandler** (allows detecting and interacting with nearby items).
- If `IsPlayer` is **false**: adds an **AIBrain** that drives the character from an **AIBehavior** ScriptableObject (toggles like chase/attack and tunable parameters such as keep distance, reaction time, burst timing, and hit chance). Keep in mind that this is a temporary AI component that is only for demonstration purposes and is very bare-bones.

#### Body Part Components
- **Balance** - Applies rotational spring forces to try and keep the body part at a specific angle
- **BodyPart** - Handles taking damage (the gun impact uses this script to apply damage)

### Weapons
Weapons are items the player can equip and attack with. Currently, only guns are available, with support for additional weapon types (such as melee) planned for future updates.

#### Gun Prefab Structure
Each gun prefab follows this structure:
- **Points** - Container for hand and mechanical reference points
  - **Shooting Point** - Where the bullets come from (also has a smoke particle system to simplify the after-shot smoke)
  - **Front Hand Point** - Where the front hand grips the gun
  - **Back Hand Point** - Where the back hand grips the gun
  - **Reload Hand Point** - Where the hand ends the reload
  - **Cycle Hand Point** - Where the hand starts the cycle
  - **Ejection Point** - Where casings are ejected from
- **Bolt** (or Slide) - The moving part that cycles when firing
- **Black** - Simple black sprite renderer to cover the background when the bolt moves
- **Forend** - Only on pump-action guns, the pump the hand grabs

#### Gun Components
- **Gun** - Handles shooting, recoil, and ammo. Uses per-frame raycasting for bullet travel with penetration support
- **Outline** - Handles the outline settings for when the gun is highlighted (because it is interactable)
- **Reload** - Static class (not in the gun object) that runs the reload animation (hand moves to body, grabs magazine, inserts into gun)
- **Cycle** - Static class (not in the gun object) that runs the cycling animation (bolt-action, pump-action, or automatic)

Each gun is configured through ScriptableObjects:
- **GunStats** - Stats that change how the gun behaves and impacts the world (damage, fire rate, recoil, ammo capacity, cycle type, reload type, etc.)
- **GunAudioConfig** - Sound clips for the gun parts (shooting, reloading, mechanical, etc.)
- **GunEffectConfig** - Visual effects when using the gun (smoke, muzzle flash, camera shake, etc.)
- **GunImpactConfig** - Visual effects for bullet hits (impact smoke, blood, etc.)

## Input
The framework supports multiple input devices out of the box through the Unity Input System:
- Keyboard and mouse
- Gamepad
- Touchscreen

You can change the keybindings for each device in the GameControls (Scripts/Input/GameControls).

## Usage
PBS 2D includes the basics for creating this type of physics-based game. You can also start from scratch and copy or reference individual components as needed, which is the approach I recommend.

## License
Everything in this framework can be used for both commercial and non-commercial projects. You have full permission to use the scripts, sprites, sounds, and any other files for your own projects. Even if you find some of these assets being sold elsewhere, by purchasing this framework you are licensed to use them.

The following is not permitted:
- Reselling this framework or any of its scripts as a standalone asset
- Using the scripts to train AI models
- Sharing the scripts publicly online (uploading, redistributing, etc.)

Sharing small code snippets when asking questions on forums or similar platforms is perfectly fine.