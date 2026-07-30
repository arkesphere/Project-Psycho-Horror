# Pickup, Hold & Inventory — Setup

Drop all `.cs` files into `Assets/Scripts/Interaction/`. Everything lives in the
`SurvivalHorror` namespace. No package dependencies beyond TextMeshPro.

---

## 1. Hook your character controller

`PlayerControlGate` is a reference-counted lock, so an inventory menu and the
examine view can both hold it without one closing releasing the other. For a
simple controller, one line at the top of `Update()` is enough:

```csharp
if (PlayerControlGate.Locked) return;
```

**If your controller samples input in `Update()` and applies it in
`FixedUpdate()`** — as most Rigidbody controllers do — a bare `return` leaves the
last sampled input frozen and the player keeps sliding. Clear the buffered input
first. See `INTEGRATION_FirstPersonController.md` for a worked example.

If your controller manages the cursor itself, set
`PlayerControlGate.ManageCursor = false;` once at startup.

Call `PlayerControlGate.ForceClear()` on scene load so a stale lock can never
softlock the player.

---

## 2. Player hierarchy

```
Player (Rigidbody, FirstPersonController)
├── Inventory              ← Capacity 12 (or whatever the case holds)
├── PlayerInteractor       ← assign Camera, Inventory, ItemExaminer, PromptUI
└── CameraJoint            ← head bob target
    └── PlayerCamera (Camera)
        └── ItemExaminer   ← assign Backdrop, Volume, Lights, Info UI

HUD_Canvas                 ← SEPARATE ROOT OBJECT, not a child of Player
├── InteractionPromptUI
└── ExamineInfoBinding
```

`ItemExaminer` builds its own `ExamineRig` at the scene root on Awake and moves
the backdrop and examine lights onto it, so their authored parents don't matter.

Keeping the HUD canvas out of the Player hierarchy matters if your controller
uses `GetComponentInChildren<Image>()` or `GetComponentInChildren<CanvasGroup>()`
to find its crosshair and sprint bar — those calls will happily grab this
system's UI instead.

`PlayerInteractor` settings worth tuning:

- **Interact Distance** — 2.5 m is close to RE7's reach. This is the *look-based*
  range that shows the E prompt.
- **Cast Radius** — 0.12 gives forgiving aim on small props. Set to 0 for a
  pure raycast if you want precision.
- **Interaction Mask** — make an `Interactable` layer, put pickups on it, and
  select only that. Leaving it as Everything means walls will block correctly
  but you'll pay for extra hits.
- **Proximity Radius** — the RE-style "something's here" range, default 4 m.
  Unlike Interact Distance, this is checked in every direction, not just where
  you're looking, and drives the arrow (Far) state of the box — no key hint
  yet. Keep it larger than Interact Distance so there's a beat between
  "I notice it" and "I can take it."
- **Proximity Check Interval** — how often that omnidirectional check runs,
  default every 0.15 s. It's not looked at every frame on purpose; nothing
  about "what's nearby" needs single-frame precision, and checking less often
  means checking cheaper.

The two ranges feed one box, not two separate cues. Walk near an item without
looking at it and the box fades in showing the arrow; turn your crosshair onto
it while still in range and the arrow cross-fades into the E hint. Walk away
without ever focusing it and the whole box just fades back out — no prompt was
ever shown. If multiple items are in proximity at once, the box always tracks
the *closest* one until you focus something, at which point focus always wins
regardless of distance.

---

## 3. Making an item

1. `Assets > Create > Survival Horror > Item Data`. Fill in display name,
   description, icon. Tick **Stackable** with a max stack for ammo and herbs.
2. Build a world prefab: mesh + a Collider on the `Interactable` layer + a
   `WorldItem` component. Assign the ItemData and quantity.
3. Optionally assign a higher-poly **Examine Prefab** on the ItemData — the
   thing the player holds up can afford far more detail than the one lying on a
   shelf in a dark room.
4. **Examine On Pickup** (on the ItemData) is what gives you the RE moment where
   the item rises into frame the instant you take it.

If the item spawns facing the wrong way in the examine view, adjust
**Examine Rotation Offset** on the ItemData rather than rotating the prefab.

---

## 4. The backdrop (important)

The examined item is a real 3D object ~0.45 m from the camera, so a Screen Space
Overlay canvas **cannot** dim the room behind it — the canvas would draw over the
item too. Use a 3D quad instead:

1. Create a Quad, scale it to about `(4, 3, 1)`. Position and parent don't matter:
   `ItemExaminer` reparents it onto its own rig at startup and places it at
   **Backdrop Distance** (default 0.9 m) in front of the camera.
2. Material: **HDRP/Unlit**, Surface Type **Transparent**, colour black, alpha ~0.85.
3. Disable the object; assign it to `ItemExaminer > Backdrop`.

Backdrop Distance must stay larger than the maximum **Zoom Range**, or zooming
out will push the item behind the backdrop.

For extra polish assign a `GameObject` holding a **Volume** with Depth of Field
and Vignette overrides to `Volume To Enable`.

### Why the rig sits at the scene root

`ItemExaminer` creates an `ExamineRig` object at the root of the scene and copies
the camera's position and rotation into it every `LateUpdate`, rather than
parenting to the camera. This is deliberate. Controllers that crouch by squashing
the player's `localScale` (the common `transform.localScale.y = crouchHeight`
approach) push a non-uniform scale down the hierarchy; a rotated child under a
non-uniform parent scale gets sheared, and the held item visibly warps as you
look around while crouched. Keeping the rig detached sidesteps it entirely, and
it means `ItemExaminer` can live on any GameObject you like.

---

## 5. HDRP specifics

**Near clip plane.** The item sits 0.45 m out. Camera near clip must be below
that — 0.1 is a good value. Don't go to 0.01; you'll wreck depth precision across
the whole scene for no benefit.

**Lighting the held item.** In a pitch-dark room the item will be pitch dark.
Fix it with rendering layers rather than a second camera. This takes three
separate steps in three different places — miss the Frame Settings one and the
feature silently does nothing.

**1. Enable the feature on the HDRP Asset.**
Select your HDRP Asset in the Project window (often `Assets/Settings/HDRenderPipelineAsset`
or `DefaultHDRPAsset`). In the Inspector go to the **Lighting** section and tick
**Light Layers**.

Not sure which asset is yours? `Edit > Project Settings > Graphics` shows the
active one at the top.

**2. Enable it in the default Frame Settings, so cameras actually process it.**
The path here moved between versions:

- **Unity 6 / HDRP 17+** — `Edit > Project Settings > Graphics > Pipeline Specific Settings > HDRP`,
  then **Frame Settings (Default Values)** → **Lighting** → tick **Light Layers**.
- **HDRP 12–16** — `Edit > Project Settings > Graphics > HDRP Global Settings`,
  same **Frame Settings (Default Values)** → **Lighting** → **Light Layers**.

**3. Name the layer.** This one is *not* on the HDRP Asset in newer versions:

- **Unity 6 / HDRP 17+** — `Edit > Project Settings > Graphics`. Scroll to the
  **Rendering Layers** foldout near the bottom of the page (it's a Graphics-wide
  setting now, not HDRP-specific). You'll see a list: `Default`, `Layer 1`,
  `Layer 2`… Click the text next to index 1 and rename it `Examine`.
- **HDRP 12–16** — select the HDRP Asset, go to **Lighting**, and open the
  **Light Layer Names** foldout. Rename `Light Layer 1` to `Examine`.

**Naming is purely cosmetic.** It only changes the label in the dropdowns so you
don't have to remember what "Layer 1" meant. If you can't find the list at all,
skip this step entirely — everything still works, the dropdown just says
`Layer 1` instead of `Examine`. What matters is that `ItemExaminer > Examine
Rendering Layer` and the Light's mask both point at **the same index**.

Then wire it up:

- Set `ItemExaminer > Examine Rendering Layer` to `1`. The script applies that
  mask to every renderer on the spawned model at runtime, so you don't touch the
  prefabs.
- Create a small Point or Spot light, disable it, and add it to the
  `Examine Lights` array. In its Inspector, enable **additional properties** on
  the **General** section (the ⋮ menu in the header) to expose **Rendering Layer
  Mask**, then set it to `Examine` only.

Result: scene lights can't reach the held item and the examine light can't leak
onto the room. The item stays readable in a black corridor.

One side effect to know about: HDRP links shadow layers to light layers by
default, so a mesh only casts shadows for lights on its own rendering layer. It
doesn't matter for a held item, but if you use this trick elsewhere, the Light's
**Shadows > Custom Shadow Layers** checkbox is how you decouple them.

**Alternative — render texture.** If you want the examine view fully isolated
(its own exposure, its own volume, zero interaction with scene fog), render a
second camera to a RenderTexture and display it on a full-screen RawImage. It's
more setup and costs an extra render, but it's the bulletproof version. HDRP has
no URP-style camera stacking, so this is the route rather than overlay cameras.

---

## 6. UI

Build this as **one canvas at the scene root** — not a child of Player. (See §2:
your controller's `GetComponentInChildren<Image>()` and
`GetComponentInChildren<CanvasGroup>()` will grab these instead of its own
crosshair and sprint bar.)

`GameObject > UI > Canvas`, name it `HUD_Canvas`. On the Canvas Scaler set
**UI Scale Mode → Scale With Screen Size**, reference resolution `1920 x 1080`.

### The floating prompt box: arrow from a distance, cross-fades to E up close

One box, two contents. From proximity range it shows a small arrow icon; once
you're close enough and looking at it, the arrow cross-fades into the "E" key
hint. It's a **World Space Canvas**, not Screen Space Overlay — that's what
lets it sit on the object and scale with distance instead of pinning to a
fixed spot on screen.

```
WorldPrompt                ← World Space Canvas, WorldSpacePrompt component here
├── CanvasGroup             (on the WorldPrompt object itself — overall fade)
├── Box                    ← Image, the frame (prompt_box.png)
├── ArrowIcon               ← Image (prompt_arrow.png) + its own CanvasGroup
│   └── centred inside Box
└── KeyLabel                TextMeshPro - Text (UI) + its own CanvasGroup
    └── also centred inside Box, "E"
```

Build steps:

1. `GameObject > UI > Canvas`, name it `WorldPrompt`. It does **not** go under
   HUD_Canvas or Player — leave it at the scene root; its position is driven
   entirely by script every frame.
2. On the Canvas component, set **Render Mode → World Space**.
3. Set the RectTransform **Width/Height** to something small, e.g. `80 x 80`,
   then set the Canvas's own **localScale** to about `0.003` on all three
   axes. Tune until the box reads as roughly the size of the RE prompt at
   arm's reach.
4. Add a **CanvasGroup** directly on `WorldPrompt` — this is the *overall*
   fade (whole box in/out), separate from the two below.
5. `Box` — an Image child filling the canvas. Assign `prompt_box.png`, **Image
   Type → Simple**, **Preserve Aspect** on.
6. `ArrowIcon` — an Image child, centred, smaller than Box (maybe 40% of its
   size). Assign `prompt_arrow.png`. Add its **own CanvasGroup** — this is
   what fades independently of KeyLabel.
7. `KeyLabel` — a TMP text child, also centred inside Box, at the same
   position as ArrowIcon (they occupy the same spot; only one is visible at a
   time). Just the letter, no "Take X" text. Add its **own CanvasGroup**.
8. Add `WorldSpacePrompt` to the `WorldPrompt` object. Assign **Canvas**,
   the outer **Canvas Group**, **Arrow Group** (ArrowIcon's CanvasGroup),
   **Key Group** (KeyLabel's CanvasGroup), and **Key Label**.

Then on `PlayerInteractor`, assign `WorldPrompt` to **World Prompt**, and set
**Interact Key Hint** to `E` (or whatever your controller binds — this system
doesn't read the key from your controller, so keep them in sync by hand if
you rebind).

**How the three-state logic works:** `PlayerInteractor` tracks two independent
things every frame — the *closest* interactable within `Proximity Radius`
(any direction), and whichever interactable the look-based raycast is
currently on within `Interact Distance`. One method, `UpdatePromptState()`,
turns those into a single decision: focused-and-in-range always wins and
shows **Near** (E), otherwise the nearest thing in proximity shows **Far**
(arrow), otherwise the box is **Hidden**. That's the whole state machine —
everything else is just fading between the three.

`InteractionPromptUI` is used **only** for short screen messages like
"No more room." now — it doesn't draw a per-object prompt at all. Its canvas
can be tiny: a CanvasGroup + one TMP label, still on HUD_Canvas.

### Per-item anchor point (optional)

By default the box appears at the center of the item's renderer bounds plus a
small upward nudge (`Prompt Auto Offset` on `WorldItem`). For an item where
that lands somewhere awkward — a long object, an oddly shaped mesh — add an
empty child transform where you want the box and assign it to
`WorldItem > Prompt Anchor`.

### Examine info panel

```
HUD_Canvas
└── ExamineInfoPanel     ← ExamineInfoBinding  (leave ACTIVE in the editor)
    ├── NameLabel
    ├── DescriptionLabel
    └── HintLabel
```

- **ExamineInfoPanel** — anchor bottom-centre or bottom-left, whichever suits
  your layout. Add `ExamineInfoBinding`.
- Leave **Root** empty on the component; it defaults to its own GameObject and
  deactivates itself in `Awake()`. Only assign Root if you want a *parent* object
  toggled instead.
- Assign `Name Label`, `Description Label`, `Hint Label`. Edit **Hint Text** to
  match your actual bindings.

Drag the panel into `ItemExaminer > Info UI`.

Unlike the prompt, this panel is toggled with `SetActive`, so it's fine (and
expected) to leave it enabled in the editor while you position it.

### Inventory screen

Not included — the display is too design-dependent to guess at. `Inventory`
gives you everything needed:

```csharp
void OnEnable()  => inventory.OnChanged += Redraw;
void OnDisable() => inventory.OnChanged -= Redraw;

void Redraw()
{
    for (int i = 0; i < inventory.Slots.Count; i++)
    {
        var slot = inventory.Slots[i];
        cells[i].SetItem(slot.IsEmpty ? null : slot.item, slot.quantity);
    }
}
```

Call `PlayerControlGate.Push()` when the screen opens and `Pop()` when it closes,
and movement, look and interaction all suspend exactly as they do for the examine
view — the gate is reference-counted, so the two can even overlap.

---

## 7. Controls

| Action | Default |
|---|---|
| Interact / take | E |
| Rotate examined item | Mouse move (or drag, if *Require Hold To Rotate*) |
| Zoom | Scroll wheel |
| Close examine | Esc, right mouse, or E |

All input goes through `InputCompat`, which compiles against either input
backend. If you already have an InputActions asset, replace the property bodies
in that one file and the rest of the system follows.

---

## Where to extend

- **Grid inventory** — RE7/8 use a grid where items occupy multiple cells.
  Add `Vector2Int gridSize` to `ItemData` and swap `Inventory`'s flat slot list
  for a 2D occupancy map. Everything else (`WorldItem`, `PlayerInteractor`,
  `ItemExaminer`) talks to `Inventory` only through `TryAdd`/`TryRemove`, so it
  stays untouched.
- **Hidden interactions** — the RE staple where rotating a lock box reveals a
  catch. Put a small collider + `IInteractable` on the examine prefab, re-enable
  colliders for that prefab in `StripGameplayComponents`, and raycast from the
  camera while `ItemExaminer.IsExamining`.
- **Dropping items** — `Instantiate(itemData.worldPrefab, ...)` in front of the
  player and `Inventory.TryRemove`.
- **Doors, drawers, notes** — implement `IInteractable` on them. `PlayerInteractor`
  needs no changes; it only knows about the interface.
- **Saving** — persist `itemId` + quantity per slot. That's why `ItemData` has a
  stable string id rather than relying on asset references.
