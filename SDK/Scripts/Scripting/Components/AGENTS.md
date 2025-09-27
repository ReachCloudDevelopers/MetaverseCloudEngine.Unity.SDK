# MetaverseScript Authoring and Integration Guide

This guide equips both automated agents and human developers with the knowledge required to write, wire, and debug JavaScript that runs inside the MetaverseScript component powered by the Jint engine in Unity.

---

## Table of Contents

1. MetaverseScript Architecture
2. Runtime Environment
3. Lifecycle and Execution Flow
4. API Surface Summary
5. Coding Conventions and Constraints
6. Cross-Script Communication Patterns
7. Variables and Data Binding
8. Unity Integration and YAML References
9. Error Handling and Diagnostics
10. Advanced Topics
11. Worked Examples
12. Git Workflow Safeguards
13. Pre-Deployment Checklist
14. Appendix A - Frequently Used Snippets
15. Appendix B - Blacklist and Whitelist Reference

---

## 1. MetaverseScript Architecture

MetaverseScript is implemented by the class `MetaverseCloudEngine.Unity.Scripting.Components.MetaverseScript` located in the MVCE package cache (`SDK/Scripts/Scripting/Components/MetaverseScript.cs`). The component embeds a Jint JavaScript runtime within a Unity `MonoBehaviour`.

### 1.1 Key Characteristics

- Inherits from `NetworkObjectBehaviour`, so it respects Unity Netcode authority and lifecycle events.
- Loads a primary JavaScript `TextAsset` plus optional include files, then caches function handles for repeated invocations.
- Provides helper methods (`ExecuteVoid`, `Execute`) that enable other scripts or UnityEvents to call into the hosted JavaScript functions.
- Supports bindings to Unity Visual Scripting variables for scene and object level data exchange.
- Injects a `console` object that mirrors browser-style logging functions (`log`, `warn`, `error`, `info`).

### 1.2 Serialized Fields

| Field | Type | Description |
|-------|------|-------------|
| `javascriptFile` | `TextAsset` | Primary `.js` asset executed by the component. |
| `includes` | `TextAsset[]` | Optional dependency scripts loaded before `javascriptFile`. |
| `globalTypeImports` | enum flags | Enables curated .NET namespace imports (for example MetaQuest). Defaults to `None`. |
| `variables` | `Unity.VisualScripting.Variables` | Reference to a Visual Scripting Variables component that exposes bindings to the script. |
| `networkObject` | `NetworkObjectBehaviour` | Explicit network object reference. Usually left empty when `autoAssignNetworkObject` is true. |
| `autoAssignNetworkObject` | `bool` | Automatically binds to the nearest `NetworkObjectBehaviour` when true. |

---

## 2. Runtime Environment

### 2.1 Jint Sandbox

- Scripts run inside a sandboxed Jint interpreter. They can access approved .NET assemblies but cannot call into restricted namespaces like `System.IO` or `System.Reflection`.
- Unity namespaces are imported using `importNamespace`. Example:

```javascript
const UnityEngine = importNamespace("UnityEngine");
const Mathf = UnityEngine.Mathf;
const Scripting = importNamespace("Unity.VisualScripting");
```

- The helper `importType` does **not** exist. Always use namespace imports.
- Scripts execute on the Unity main thread. Avoid long blocking operations and prefer cached references to limit interpreter overhead.

### 2.2 Logging Support

MetaverseScript injects a `console` object with familiar methods:

- `console.log(message)` - informational trace messages.
- `console.warn(message)` - recoverable warnings.
- `console.error(message)` - serious issues that require attention.
- `console.info(message)` - supplementary context for debugging.

Logs appear in the Unity console with the script name as a prefix, making it easy to trace output back to the owning script.

### 2.3 Assembly Whitelist

Allowed assemblies include Unity core modules (CoreModule, PhysicsModule, UI, AudioModule), Cinemachine, TextMeshPro, Unity Visual Scripting, MVCE assemblies, UniTask, and XR / AR packages when enabled. Refer to `MetaverseScript.GetAssemblies()` for the complete list.

### 2.4 Blacklisted APIs

| Category | Examples |
|----------|----------|
| Global search | `FindObjectOfType`, `FindObjectsOfType`, `FindAnyObjectByType`, `FindSceneObjectsOfType` |
| Messaging | `SendMessage`, `BroadcastMessage`, `SendMessageUpwards` |
| Persistence | `PlayerPrefs`, `Resources`, `AssetBundle` |
| System namespaces | `System.IO`, `System.Reflection`, `System.Web`, `Microsoft.Win32`, `Microsoft.SafeHandles` |

---

## 3. Lifecycle and Execution Flow

### 3.1 Common Hooks

| Hook | Purpose |
|------|---------|
| `Awake` / `OnEnable` / `Start` | Cache components and initialise state. |
| `OnNetworkSpawn` / `OnNetworkDespawn` | Respond to Netcode spawn and despawn events. |
| `OnNetworkReady` | Fired once the script and networking are ready; ideal for setup logic. |
| `Update` / `LateUpdate` | Per-frame logic. Guard with `GetIsInputAuthority()` or `GetIsStateAuthority()`. |
| `OnDisable` / `OnDestroy` | Cleanup and unsubscribing. |

### 3.2 Authority Gating

```javascript
function Update() {
    if (!GetIsInputAuthority()) {
        return;
    }
    // authoritative logic goes here
}
```

### 3.3 Initialization Template

```javascript
let cachedTransform = null;
let sceneVariables = null;

function OnNetworkReady() {
    cachedTransform = transform;

    try {
        if (Scripting !== null && typeof Scripting !== "undefined") {
            sceneVariables = Scripting.Variables.Scene(gameObject.scene);
        }
    } catch (error) {
        sceneVariables = null;
    }

    CacheWaypoints();
    ResolveProjectilePrefab();
}
```

---

## 4. API Surface Summary

### 4.1 Invocation Helpers

| Method | Signature | Usage |
|--------|-----------|-------|
| `ExecuteVoid` | `ExecuteVoid(string fn)` | Invoke a no-argument JavaScript function. |
| `ExecuteVoid` | `ExecuteVoid(string fn, object[] args)` | Invoke a JavaScript function with arguments. |
| `Execute` | `Execute(string fn)` | Invoke a function and receive a `JsValue` result. |
| `Execute` | `Execute(string fn, object[] args)` | Invoke with arguments and receive a `JsValue` result. |
| `ExecuteFunction` | `[Obsolete] ExecuteFunction(string fn)` | Legacy helper that forwards to `ExecuteVoid`. |

### 4.2 Null Semantics

Because Unity overrides equality operators, use `NULL(obj)` in addition to `=== null` checks:

```javascript
if (obj === null || typeof obj === "undefined" || NULL(obj)) {
    return;
}
```

### 4.3 Visual Scripting Variables

- Scene level: `Scripting.Variables.Scene(gameObject.scene)`
- Object level: `Scripting.Variables.Object(gameObject)`
- Methods: `Get(name)`, `Set(name, value)`, `declarations.IsDefined(name)`

---

## 5. Coding Conventions and Constraints

### 5.1 Object Construction

- Keyed object literals (`{ key: value }`) are forbidden.
- Use `{}` followed by property assignments.
- Arrays (`[]`) and constructor calls such as `new UnityEngine.Vector3(...)` are permitted.

### 5.2 Enumeration Pattern

```javascript
const InteractionState = {};
InteractionState.Idle = "Idle";
InteractionState.Targeting = "Targeting";
Object.freeze(InteractionState);
```

### 5.3 Defensive Coding

- Guard component access with null checks.
- Verify optional method existence using `typeof target.method === "function"`.
- Wrap fragile operations in `try/catch` blocks with `console.warn` for diagnostics.

### 5.4 Performance Practices

- Cache `transform`, animators, rigidbodies, and frequently accessed arrays in setup functions.
- Reuse temporary vectors or store computations when possible to reduce allocations inside `Update`.
- Avoid heavy loops per frame; batch work or defer via asynchronous patterns when available.

---

## 6. Cross-Script Communication Patterns

### 6.1 Resolving Another Script

```javascript
function ResolveAnimatorScript(instance) {
    if (instance === null || typeof instance === "undefined" || NULL(instance)) {
        return null;
    }
    if (typeof instance.GetMetaverseScript !== "function") {
        return null;
    }
    try {
        return instance.GetMetaverseScript("ProjectileAnimator");
    } catch (error) {
        return null;
    }
}
```

### 6.2 Executing Cross-Script Functions

```javascript
const animatorScript = ResolveAnimatorScript(instance);
if (animatorScript !== null && !NULL(animatorScript)) {
    try {
        animatorScript.ExecuteVoid("BeginArc", [
            spawnPosition,
            targetPosition,
            solution.arcOffset,
            solution.flightTime,
            0
        ]);
    } catch (error) {
        console.warn("Animator script failed to start arc.", error);
    }
}
```

Always wrap cross-script calls to handle missing functions or scripts that are not yet initialised.

---

## 7. Variables and Data Binding

### 7.1 Reading Scene Variables

```javascript
function GetSceneFloat(name, defaultValue) {
    if (sceneVariables === null) {
        return defaultValue;
    }
    try {
        const value = sceneVariables.Get(name);
        if (typeof value === "number" && !isNaN(value)) {
            return value;
        }
    } catch (error) {
        // ignore and use fallback
    }
    return defaultValue;
}
```

### 7.2 Writing Object Variables

```javascript
function PersistProjectile(instance, startPosition, targetPosition, solution) {
    if (Scripting === null || typeof Scripting === "undefined") {
        return;
    }
    let objectVars = null;
    try {
        objectVars = Scripting.Variables.Object(instance);
    } catch (error) {
        objectVars = null;
    }
    if (objectVars === null) {
        return;
    }

    const payload = {};
    payload.startPosition = startPosition;
    payload.targetPosition = targetPosition;
    payload.arcOffset = solution.arcOffset;
    payload.duration = solution.flightTime;
    payload.elapsed = 0;

    objectVars.Set("ProjectilePayload", payload);
}
```

---

## 8. Unity Integration and YAML References

### 8.1 Component Template

```yaml
--- !u!114 &1406217474624389408
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_GameObject: {fileID: 7160965207638636577}
  m_Enabled: 1
  m_Script: {fileID: 11500000, guid: b468b2cc2f491644d9477c42f276126d, type: 3}
  javascriptFile: {fileID: -8253061345870894857, guid: 538ca881a08add44e9305e5c23259b92, type: 3}
  includes: []
  globalTypeImports: 0
  variables: {fileID: 1782663359040408844}
  autoAssignNetworkObject: 1
```

### 8.2 Variables Component

```yaml
--- !u!114 &1782663359040408844
MonoBehaviour:
  m_Script: {fileID: 11500000, guid: e741851cba3ad425c91ecf922cc6b379, type: 3}
  _data:
    _json: "{""declarations"":{""Kind"":""Object"",""collection"":{""$content"":[]}}"
    _objectReferences: []
```

### 8.3 UnityEvent Configuration

```yaml
m_PersistentCalls:
  m_Calls:
  - m_Target: {fileID: 1406217474624389408}
    m_MethodName: ExecuteVoid
    m_Mode: 1
    m_Arguments:
      m_StringArgument: "OnButtonPressed"
```

---

## 9. Error Handling and Diagnostics

- Guard component lookups and cross-script references with null checks.
- Wrap `ExecuteVoid` and `Execute` calls in `try/catch` blocks to report missing functions without crashing.
- Use `console.warn` for recoverable issues and `console.error` for critical failures.
- Delay cross-script calls until `OnNetworkReady` or after confirming that the target script is ready.

---

## 10. Advanced Topics

### 10.1 Asynchronous Patterns

- When UniTask or dispatcher utilities are available, offload long operations to asynchronous flows. Ensure Unity objects are manipulated on the main thread.

### 10.2 Global Type Imports

- Enable `globalTypeImports` flags only when additional namespaces are required. Unnecessary imports increase the surface area of the scripting environment.

### 10.3 Script Includes

- Use the `includes` array to load shared helper scripts before executing the main script file. Includes are processed in order.

### 10.4 Networking Considerations
### 10.5 Network RPC Workflow

MetaverseScript exposes a thin wrapper around the MVCE networking layer so scripts can register and invoke remote procedure calls (RPCs) entirely from JavaScript. The runtime injects the following helpers:

| Helper | Signature | Target |
|--------|-----------|--------|
| `RegisterRPC` | `RegisterRPC(short rpcId, RpcEventDelegate handler)` | Registers a handler so the script responds when the specified RPC ID is received. |
| `UnregisterRPC` | `UnregisterRPC(short rpcId, RpcEventDelegate handler)` | Removes a handler; always call this during cleanup. |
| `ServerRPC` | `ServerRPC(short rpcId, object payload)` | Sends an RPC to the server / state authority. |
| `ClientRPC` | `ClientRPC(short rpcId, object payload)` | Broadcasts to all clients (including the sender). |
| `ClientRPCOthers` | `ClientRPCOthers(short rpcId, object payload)` | Sends to every client except the sender. |
| `PlayerRPC` | `PlayerRPC(short rpcId, int playerId, object payload)` | Sends to a specific player ID. |

**Arguments**

- `rpcId` (`short`): Unique identifier for the RPC; define constants so IDs remain stable.
- `payload` (`object | string | array`): Serializable data you supply to the call; ensure it follows the serialization guidance above.
- `playerId` (`int`, `PlayerRPC` only): Netcode player ID to deliver the message to.

> **Handler signature:** `RpcEventDelegate` resolves to `function handler(rpcId, senderId, payload)`. The handler executes on whichever peer registered it.

#### 10.5.1 Registering RPCs

MetaverseScript automatically invokes optional user-defined functions `RegisterNetworkRPCs()` and `UnRegisterNetworkRPCs()` when the engine is ready. Implement these in your script to wire RPC handlers.

```javascript
const RPC = {};
RPC.LaunchProjectile = 1001; // short (Int16) IDs; keep them stable across builds
Object.freeze(RPC);

function RegisterNetworkRPCs() {
    RegisterRPC(RPC.LaunchProjectile, OnLaunchProjectileRPC);
}

function UnRegisterNetworkRPCs() {
    UnregisterRPC(RPC.LaunchProjectile, OnLaunchProjectileRPC);
}

function OnLaunchProjectileRPC(rpcId, senderId, payload) {
    console.log("RPC", rpcId, "from", senderId, payload);
    LaunchProjectile(null, payload?.targetPosition ?? null);
}
```

Best practices:
- Payloads must use blittable types or encoded strings. Avoid ExpandoObject or other dynamic types; prefer primitives, arrays, or JSON via `JSON.stringify` before sending.

  Wrong (plain object becomes ExpandoObject):
  ```javascript
  const payload = {};
  payload.score = 5;
  ClientRPC(RPC.ScoreUpdate, payload); // plain objects are promoted to ExpandoObject
  ```

  Correct (string-encoded):
  ```javascript
  const payload = {};
  payload.score = 5;
  ClientRPC(RPC.ScoreUpdate, JSON.stringify(payload));
  ```

  Correct (blittable array):
  ```javascript
  const payload = [5, 3]; // [score, rallyCount]
  ClientRPC(RPC.ScoreUpdate, payload);
  ```

  Receiving JSON payloads:
  ```javascript
  function OnScoreUpdateRPC(rpcId, senderId, payload) {
      if (typeof payload !== "string") {
          console.warn('Expected JSON string for RPC.ScoreUpdate');
          return;
      }

      const decoded = JSON.parse(payload);
      const score = decoded.score;
      const rallyCount = decoded.rallyCount;
      // ... use decoded values ...
  }
  ```

  Receiving array payloads:
  ```javascript
  function OnScoreUpdateRPC(rpcId, senderId, payload) {
      if (!Array.isArray(payload)) {
          console.warn('Expected array for RPC.ScoreUpdate');
          return;
      }

      const score = payload[0];
      const rallyCount = payload[1];
      // ... use decoded values ...
  }
  ```

- Define descriptive constants for RPC IDs and freeze the container object so values are immutable.
- Always unregister handlers to prevent duplicate registrations after respawn.
- Keep handlers idempotent and resilient; they may fire on multiple peers depending on network role.

#### 10.5.2 Sending RPCs

When sending, supply a payload that MVCE can serialise (primitives, arrays, or plain objects constructed via `{}` followed by assignments). For complex data, serialise to JSON strings.

```javascript
function NotifyHostOfHit(targetPosition) {
    const payload = {};
    payload.targetPosition = targetPosition;
    payload.hitTime = UnityEngine.Time.time;
    ServerRPC(RPC.LaunchProjectile, payload);
}
```

Choose the helper that matches your intent:

- **State-authoritative actions:** `ServerRPC`.
- **Broadcast to everyone:** `ClientRPC`.
- **Broadcast to everyone else:** `ClientRPCOthers`.
- **Target a specific player:** `PlayerRPC` with the player ID.

#### 10.5.3 Authority Considerations

Because MetaverseScript inherits `NetworkObjectBehaviour`, scripts can branch on `GetIsInputAuthority()` and `GetIsStateAuthority()` to decide when to send or process RPCs.

- Register handlers on every peer that should respond to the message.
- Only send RPCs from peers that are authorised to announce the event (often the state authority or owner).
- Inside handlers, branch on `senderId` or authority status to tailor behaviour.

#### 10.5.4 Cleanup and Diagnostics

- Unregister handlers in `UnRegisterNetworkRPCs()` to avoid duplicates after respawn or scene unload.
- Wrap registration and send calls in `try/catch` if networking services might be unavailable (offline testing).
- Log failures with `console.warn` or `console.error` to highlight missing network bindings.

Combined with the existing networking helpers (`GetNetworkObject`, `GetHostID`, `IsInputAuthority`, etc.), these RPC APIs allow MetaverseScript code to participate fully in MVCE network messaging from JavaScript.

- Use `GetIsOwner`, `GetIsInputAuthority`, and `GetIsStateAuthority` to divide responsibilities between owner, authority, and observers.
- Perform spawn-time setup in `OnNetworkSpawn` to ensure network-specific references are initialised.

---

## 11. Worked Examples

### 11.1 State-Driven Waypoint Navigation

```javascript
const UnityEngine = importNamespace("UnityEngine");
const Mathf = UnityEngine.Mathf;

let waypoints = [];
let currentTarget = null;
let cachedTransform = null;
const moveSpeed = 1.5;
const arriveThreshold = 0.05;

const State = {};
State.Idle = "Idle";
State.Moving = "Moving";
Object.freeze(State);

let state = State.Idle;

function OnNetworkReady() {
    cachedTransform = transform;
    CacheWaypoints();
    PickNextTarget();
    state = State.Moving;
}

function CacheWaypoints() {
    waypoints = [];
    const parent = cachedTransform.parent;
    if (NULL(parent)) {
        return;
    }
    const root = parent.Find("Waypoints");
    if (NULL(root)) {
        return;
    }
    const count = root.childCount;
    for (let i = 0; i < count; i++) {
        waypoints.push(root.GetChild(i));
    }
}

function Update() {
    if (!GetIsInputAuthority()) {
        return;
    }
    if (state !== State.Moving || currentTarget === null || NULL(currentTarget)) {
        return;
    }
    const targetPosition = currentTarget.position;
    const currentPosition = cachedTransform.position;
    const nextPosition = UnityEngine.Vector3.MoveTowards(
        currentPosition,
        targetPosition,
        moveSpeed * UnityEngine.Time.deltaTime
    );
    cachedTransform.position = nextPosition;

    if (UnityEngine.Vector3.Distance(nextPosition, targetPosition) <= arriveThreshold) {
        PickNextTarget();
    }
}

function PickNextTarget() {
    if (waypoints.length === 0) {
        state = State.Idle;
        currentTarget = null;
        return;
    }
    currentTarget = waypoints[UnityEngine.Random.Range(0, waypoints.length)];
}
```

### 11.2 Projectile Launch and Animation

```javascript
function LaunchProjectile(targetOverride = null, explicitTargetPosition = null) {
    if (cachedTransform === null || NULL(cachedTransform)) {
        return false;
    }

    const spawnPosition = cachedTransform.position + cachedTransform.TransformVector(spawnOffset);

    let targetTransform = targetOverride !== null && typeof targetOverride !== "undefined" ? targetOverride : null;
    let targetPosition = ExtractWorldPosition(explicitTargetPosition);

    if (targetPosition === null && targetTransform !== null && !NULL(targetTransform)) {
        const pos = targetTransform.position;
        targetPosition = new UnityEngine.Vector3(pos.x, pos.y, pos.z);
    }

    if (targetPosition === null) {
        targetTransform = AcquireFallbackTarget();
        if (targetTransform === null || NULL(targetTransform)) {
            return false;
        }
        const fallbackPos = targetTransform.position;
        targetPosition = new UnityEngine.Vector3(fallbackPos.x, fallbackPos.y, fallbackPos.z);
    }

    const prefab = GetProjectilePrefab();
    if (prefab === null || NULL(prefab)) {
        return false;
    }

    let aimDirection = targetPosition - spawnPosition;
    if (aimDirection.sqrMagnitude <= 0.0001) {
        aimDirection = cachedTransform.forward;
    }

    const rotation = UnityEngine.Quaternion.LookRotation(aimDirection.normalized, UnityEngine.Vector3.up);
    const solution = SolveLaunch(spawnPosition, targetPosition);
    if (solution === null) {
        return false;
    }

    SpawnNetworkPrefab(prefab, spawnPosition, rotation, null, instance => {
        if (instance === null || NULL(instance)) {
            return;
        }

        const projectileTransform = instance.transform;
        if (!NULL(projectileTransform)) {
            projectileTransform.position = spawnPosition;
        }

        const rigidbody = instance.GetComponent(UnityEngine.Rigidbody);
        if (!NULL(rigidbody)) {
            rigidbody.isKinematic = true;
            rigidbody.velocity = UnityEngine.Vector3.zero;
            rigidbody.angularVelocity = UnityEngine.Vector3.zero;
        }

        CacheProjectilePayload(instance, spawnPosition, targetPosition, solution);

        const animatorScript = ResolveAnimatorScript(instance);
        if (animatorScript !== null && !NULL(animatorScript)) {
            try {
                animatorScript.ExecuteVoid("BeginArc", [
                    spawnPosition,
                    targetPosition,
                    solution.arcOffset,
                    solution.flightTime,
                    0
                ]);
            } catch (error) {
                console.warn("Failed to play projectile animation.", error);
            }
        }
    });

    return true;
}
```

---

## 12. Git Workflow Safeguards

### 12.1 Day-to-day discipline
- Run `git status` before and after every batch of edits so you always know what will be committed.
- Stage selectively (`git add -p` or per-file in the UI) to keep unrelated changes out of a single commit.
- Commit early and often with descriptive summaries (for example `git commit -m "feat: update pickleball opponent flow"`).
- Push work-in-progress branches instead of reusing `master` when experimenting, so recovery is as simple as resetting the branch.

### 12.2 Protecting user content
- Never use destructive commands like `git checkout -- <path>` or `git reset --hard` without taking a backup or confirming with the owner of the changes.
- If you need a clean copy of a scene or prefab, duplicate the asset or branch first; do not overwrite the only copy.
- When you are unsure whether changes are yours, stash (`git stash`) or commit them before attempting risky operations.
- Unity scenes and prefabs are large YAML files; overwrites are rarely diff-friendly. Always prefer merging by hand over blind replacement.

### 12.3 Collaboration etiquette
- Pull (`git pull --rebase`) frequently to minimise merge conflicts with other contributors.
- Review the diff (`git diff` or the editor's compare tool) with a teammate before committing significant content changes.
- Document any manual YAML edits inside the commit message so others understand why Unity may not have generated the change automatically.
- Treat every commit as a recovery point: if you cannot explain or rebuild the change log, you are committing too much at once.
## 13. Pre-Deployment Checklist

- [ ] Namespace imports defined using `importNamespace`.
- [ ] No keyed object literals; objects constructed via `{}` and assignments.
- [ ] Authority guards (`GetIsInputAuthority` or `GetIsStateAuthority`) protect state changes.
- [ ] Cached references validated before use.
- [ ] Visual Scripting variables accessed safely; payloads stored via assignment pattern.
- [ ] Cross-script communication uses `ExecuteVoid` or `Execute` with `try/catch` guards.
- [ ] Restricted Unity APIs avoided according to the blacklist.
- [ ] Logging statements provide actionable context.
- [ ] UnityEvents target the MetaverseScript component.
- [ ] Manual YAML edits mimic Unity serialization patterns.

---

## 14. Appendix A - Frequently Used Snippets

### Null Guard Helper

```javascript
function IsNullish(value) {
    return value === null || typeof value === "undefined" || NULL(value);
}
```

### Quaternion Look Rotation with Fallback

```javascript
function ComputeAimRotation(direction, fallbackForward) {
    let aim = direction;
    if (aim.sqrMagnitude <= 0.0001) {
        aim = fallbackForward;
    }
    return UnityEngine.Quaternion.LookRotation(aim.normalized, UnityEngine.Vector3.up);
}
```

### Random Delay Generator

```javascript
function GetRandomDelay(minSeconds, maxSeconds) {
    return UnityEngine.Random.Range(minSeconds, maxSeconds);
}
```

### Binding UnityEvent to Script Function

1. Set the UnityEvent target to the MetaverseScript component (`fileID`).
2. Choose `ExecuteVoid` as the method.
3. Provide the function name in `m_StringArgument` (for example `"OnUIButtonPressed"`).
4. Note that this means that the void method (specified in `m_StringArgument`) does not support input parameters, and must be a parameterless void method.

---

## 15. Appendix B - Blacklist and Whitelist Reference

### Blocked APIs (Partial)

| Category | Members |
|----------|---------|
| Search | `FindObjectOfType`, `FindObjectsOfType`, `FindAnyObjectByType`, `FindSceneObjectsOfType` |
| Messaging | `SendMessage`, `BroadcastMessage`, `SendMessageUpwards` |
| Persistence | `PlayerPrefs`, `Resources`, `AssetBundle` |
| System Namespaces | `System.IO`, `System.Reflection`, `System.Web`, `Microsoft.Win32`, `Microsoft.SafeHandles` |

### Allowed Assemblies (Selected)

- `UnityEngine.CoreModule`
- `UnityEngine.PhysicsModule`
- `UnityEngine.UI`
- `UnityEngine.AudioModule`
- `UnityEngine.AnimationModule`
- `UnityEngine.AIModule` (when included)
- `UnityEngine.XR` assemblies (when included)
- `TextMeshPro`
- `Cinemachine`
- `Unity.VisualScripting`
- `System.Threading.Tasks`
- `UniTask`
- `MetaverseCloudEngine` assemblies

No other assemblies should be used outside of the listed assemblies.
Refer to `MetaverseScript.GetAssemblies()` for the authoritative list used at runtime.

---

By following the patterns, safeguards, and integration steps outlined above, developers and automated agents can produce MetaverseScript code that is safe, performant, and easy to integrate into Unity scenes and prefabs.