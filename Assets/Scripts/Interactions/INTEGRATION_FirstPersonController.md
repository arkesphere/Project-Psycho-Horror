# Integrating with FirstPersonController (Modular FPC v1.0.1)

Three edits to `FirstPersonController.cs`, plus one scene-layout rule that will
save you an afternoon of debugging.

---

## Edit 1 — namespace import

At the top, with the other `using` directives:

```csharp
using SurvivalHorror;
```

---

## Edit 2 — the guard in `Update()`

The plain `if (PlayerControlGate.Locked) return;` is **not** safe in this
controller, because it samples input in `Update()` and consumes it in
`FixedUpdate()`. Returning early leaves `movementInput` frozen at its last value
and the Rigidbody keeps driving in that direction — the player slides across the
room for the whole examine.

Insert this as the first thing in `Update()`, above the `movementInput = new Vector2(...)` line:

```csharp
private void Update()
{
    // Interaction / examine / menu lock.
    if (PlayerControlGate.Locked)
    {
        SuspendInput();
        return;
    }

    // Input is sampled once per rendered frame, then consumed by physics
    // in FixedUpdate. Reading input directly in FixedUpdate can feel uneven.
    movementInput = new Vector2(
    // ... rest of the method unchanged
```

---

## Edit 3 — the `SuspendInput()` method

Add it anywhere in the class (next to `HeadBob()` reads well):

```csharp
private void SuspendInput()
{
    // FixedUpdate consumes these every physics tick. Clearing them lets
    // ApplyMovement decelerate to a stop instead of freezing mid-stride,
    // which reads much better than a hard halt.
    movementInput = Vector2.zero;
    sprintInput   = false;
    jumpRequested = false;
    isWalking     = false;
    isSprinting   = false;

    // Hold-style keys never receive their KeyUp while Update is short-circuited.
    // Without this, releasing crouch during an examine leaves the player stuck
    // crouched — and because Crouch() multiplies walkSpeed by speedReduction on
    // every entry, the next crouch press halves the walk speed a second time.
    if (holdToZoom) isZoomed = false;
    if (holdToCrouch && isCrouched) Crouch();

    // Ease the FOV back from sprint/zoom and settle the head bob to neutral, so
    // the item isn't framed by a lurching 80-degree sprint camera.
    playerCamera.fieldOfView =
        Mathf.Lerp(playerCamera.fieldOfView, fov, zoomStepTime * Time.deltaTime);

    if (enableHeadBob) HeadBob();
}
```

`HeadBob()` already lerps the joint back toward `jointOriginalPos` whenever
`isWalking` is false, which is why it's called after the flag is cleared.

---

## Edit 4 — cursor ownership

In `Start()`, right after the existing `lockCursor` block:

```csharp
if(lockCursor)
{
    Cursor.lockState = CursorLockMode.Locked;
}

// Let the gate free the cursor during examines only if this controller
// intended to capture it in the first place.
PlayerControlGate.ManageCursor = lockCursor;
PlayerControlGate.ForceClear();
```

`ForceClear()` guards against a stale lock surviving a scene reload and
softlocking the player.

---

## The scene-layout trap

Two lines in this controller reach blindly into the hierarchy:

```csharp
crosshairObject = GetComponentInChildren<Image>();        // Awake()
sprintBarCG     = GetComponentInChildren<CanvasGroup>();  // Start()
```

Both return the **first** match in depth-first order. `InteractionPromptUI` and
`ExamineInfoBinding` use `CanvasGroup` and `Image` heavily. If you parent the
interaction HUD under the Player, whichever object happens to sort first wins —
and the symptoms are baffling: your interaction prompt fades in and out with the
sprint meter, or the crosshair setup overwrites a prompt panel's sprite.

**Recommended fix (no code change):** keep the interaction/examine canvas as its
own root GameObject in the scene, not a child of Player. The system doesn't need
to be parented to anything.

**If you want it under Player anyway,** replace those two lines with serialized
fields you assign explicitly:

```csharp
[SerializeField] private Image crosshairObject;
[SerializeField] private CanvasGroup sprintBarCG;
```

Note that this controller draws a fully custom inspector, so new serialized
fields won't appear until you add matching lines to `FirstPersonControllerEditor`
(or temporarily switch the inspector to Debug mode to assign them).

---

## Things that don't conflict

- **E** is unused by the controller — free for interact.
- **Scroll wheel** is unused — free for examine zoom.
- **Right mouse** is `zoomKey`, and it also closes the examine view. No clash,
  because `Update()` returns before the zoom block while the gate is locked.
- The examiner runs on `Time.unscaledDeltaTime`, so it keeps working if you later
  add a `Time.timeScale = 0` pause menu.

One ordering note: both `FirstPersonController.LateUpdate()` and
`ItemExaminer.LateUpdate()` run each frame, and Unity doesn't guarantee which
goes first. It's invisible today, because `pitch` and `yaw` are frozen while the
gate is locked, so there's nothing for the rig to lag behind. If you ever allow
looking around *during* an examine, set an explicit Script Execution Order with
`ItemExaminer` after `FirstPersonController`.
