# Simplified Errand Assignment System

## What Changed

The errand assignment system was **simplified** to remove unnecessary complexity:

### ❌ Removed

- `ErrandAssignmentTracker` component (deleted)
- Auto-restore of priority when errand completes
- State tracking and monitoring
- Timeout logic
- Completion detection

### ✅ Kept

- `assign_errand` action - find and boost priority
- Finds nearest available errand of specified type
- Boosts ChoreGroup priority to 5 (maximum)
- Simple, predictable behavior

## How It Works Now

### The Action

```javascript
assign_errand({
  errand_type: "Mop",        // Required: Mop, Dig, Build, etc.
  max_distance: 50,          // Optional: default 50 tiles
  target_x: 100,             // Optional: search near this location
  target_y: 50               // Optional: search near this location
})
```

### What It Does

1. Finds the specified errand type (e.g., "Mop")
2. Searches for nearest available errand within distance
3. Gets the ChoreGroup containing that errand type
4. **Sets ChoreGroup priority to 5 (critical)**
5. Returns success message with errand location

### What It DOESN'T Do

- ❌ Track the errand state
- ❌ Monitor completion
- ❌ Auto-restore priority
- ❌ Cancel previous assignments
- ❌ Timeout after X minutes

## Why This Design?

### The Philosophy

**ONI's priority system handles the queue naturally.**

- Multiple ChoreGroups can be at priority 5 simultaneously
- Neuro picks errands based on:
  - Distance (closer is better)
  - Availability (not assigned to others)
  - Skills (can they do it?)
  - Current state (already heading somewhere?)
- Priority stays at 5 until **you manually change it**

### Simple Workflow

```javascript
// Boost mopping to maximum
assign_errand({ errand_type: "Mop" })
// → Basekeeping priority = 5

// Boost digging too
assign_errand({ errand_type: "Dig" })
// → Digging priority = 5
// → Basekeeping still = 5

// Manually lower mopping when done
set_priority({ chore_group: "Basekeeping", priority: 3 })
// → Basekeeping priority = 3
// → Digging still = 5
```

### Advantages

✅ **Simple** - No complex state tracking
✅ **Predictable** - Priority stays where you set it
✅ **Flexible** - Multiple priorities can be high
✅ **Transparent** - Easy to see what's happening
✅ **Safe** - Uses ONI's natural priority system

## Usage Examples

### Example 1: Emergency Mopping

```javascript
// Base is flooding!
assign_errand({ errand_type: "Mop", max_distance: 100 })
// → Boosts Basekeeping to priority 5
// → Neuro will prioritize mopping nearby liquid
```

### Example 2: Urgent Building

```javascript
// Need to finish this build NOW
assign_errand({
  errand_type: "Build",
  target_x: 150,
  target_y: 75,
  max_distance: 20
})
// → Boosts Construction to priority 5
// → Finds nearest build errand near (150, 75)
```

### Example 3: Reset Priorities

```javascript
// Lower all priorities back to normal
set_priority({ chore_group: "Basekeeping", priority: 3 })
set_priority({ chore_group: "Construction", priority: 3 })
set_priority({ chore_group: "Digging", priority: 3 })
// → All back to normal levels
```

## Testing

### Run the Test Suite

```batch
cd e:\VSProjects\NeuroMod
Build-And-Deploy-Release.bat
REM Restart ONI
cd Randy
node test-errand-assign.js
```

### What the Test Does

1. Lists current priorities
2. Assigns 6 different errand types (Mop, Dig, Build, etc.)
3. Tests location-based assignment
4. Verifies available errands
5. Checks priority changes

### What to Check in Player.log

```
[AssignErrandAction] Errand type: Mop, Max distance: 50
[AssignErrandAction] Found nearest chore: Mop at distance 12.4
[AssignErrandAction] Boosted Basekeeping priority from 3 to 5
```

## API Reference

### assign_errand

**Parameters:**

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `errand_type` | string | ✅ Yes | - | Type of errand (Mop, Dig, Build, etc.) |
| `max_distance` | integer | ❌ No | 50 | Search radius in tiles |
| `target_x` | integer | ❌ No | Neuro's X | Target X coordinate |
| `target_y` | integer | ❌ No | Neuro's Y | Target Y coordinate |

**Returns:**

```json
{
  "success": true,
  "message": "Boosted Basekeeping priority from 3 to 5. Nearest errand: Mop at (42, 28) - 12.4 tiles away"
}
```

**Error Cases:**

- Errand type not found → `"Chore type 'XYZ' not found"`
- No errands available → `"No available Mop errands found within 50 tiles"`
- Missing Neuro → `"Neuro duplicate not found"`

### Related Actions

- `list_priorities` - See all ChoreGroup priorities (0-5)
- `set_priority` - Manually adjust a ChoreGroup priority
- `list_errands` - See available errands in the world
- `get_current_errand` - See what Neuro is doing now

## Migration Notes

If you have old code using the previous design:

### Before (Complex)

```javascript
// Old: Auto-restored after completion
assign_errand({ errand_type: "Mop" })
// Wait for completion...
// Priority automatically restored
```

### After (Simple)

```javascript
// New: Manual control
assign_errand({ errand_type: "Mop" })
// Priority stays at 5 until you change it
set_priority({ chore_group: "Basekeeping", priority: 3 })
```

## Conclusion

The simplified system is **easier to understand, maintain, and use**. It leverages ONI's natural priority queue instead of fighting against it with complex tracking logic.

**Key Takeaway:** Just boost what you want, ONI handles the rest.
