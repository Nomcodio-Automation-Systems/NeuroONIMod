# Priorities vs Errands System Design

**Date:** October 18, 2025  
**Purpose:** Separate ChoreGroup priorities from actual errand management  
**Status:** 📋 Design Phase

## Problem Statement

The current "task" actions conflate two different ONI concepts:

1. **Priorities (ChoreGroups)**: The 17 categories in the Jobs/Priorities screen (Basekeeping, Dig, Cook, etc.)
   - These set the duplicate's **willingness** to perform certain types of work
   - Priority values: 0-5 (disabled → critical)
   - Shown in: Jobs/Priorities screen (Ctrl+2)

2. **Errands (Chores)**: Actual specific tasks available in the world
   - Individual actionable items like "Mop tile at (25, 10)", "Build ladder at (30, 15)"
   - Managed by the game's ChoreManager
   - Shown in: Duplicate's TODO list, errand list

**Current Implementation** only handles Priorities (ChoreGroups).  
**User Request**: Also need Errands (actual chores) management.

## ONI System Architecture

### ChoreGroup (Priority) System

```
Db.Get().ChoreGroups.resources  (17 groups)
  ├── Basekeeping   (Mop, Disinfect, EmptyStorage)
  ├── Build         (Build, Dig, Deconstruct)
  ├── Cook          (Cook, CookingFetch)
  ├── Dig           (Dig, Uproot)
  ├── Farming       (Harvest, FarmTend)
  ├── Ranching      (Wrangle, Ranch, RanchingFetch)
  ├── Research      (Research, AnalyzeArtifact)
  ├── MedicalAid    (Doctor, Assist)
  ├── Art           (Art, Decor)
  ├── MachineOperating (Operate, GeneratePower, Capture)
  ├── Hauling       (FetchCritical, Fetch, Food Delivery)
  ├── Toggle        (Toggle building states)
  ├── LifeSupport   (Cooking, CompostWorkable)
  ├── Storage       (Store, Relocate)
  ├── Recreation    (JoyReaction, Relax)
  ├── Combat        (Attack, Flee)
  └── Rocketry      (RocketControl, LoadRocket)
```

**Each Duplicate has:**
```csharp
ChoreConsumer.GetPersonalPriority(ChoreGroup) → int (0-5)
ChoreConsumer.SetPersonalPriority(ChoreGroup, int)
```

### Chore (Errand) System

```
GlobalChoreProvider.Instance.choreWorldMap  (spatial index of all chores)
  ├── Chore instances (100s-1000s in a typical base)
  │   ├── choreType  (e.g., ChoreTypes.Mop, ChoreTypes.Build)
  │   ├── target     (GameObject being worked on)
  │   ├── location   (Vector3 position)
  │   ├── priorityMod (building priority 1-9)
  │   └── driver     (ChoreDriver if assigned)
```

**Each Duplicate's ChoreConsumer:**
```csharp
choreConsumer.FindNextChore()  // AI picks best errand
choreConsumer.choreDriver.GetCurrentChore()  // Currently executing
choreDriver.StopChore()  // Cancel current
choreDriver.SetChore(Chore)  // Force assign (risky!)
```

## Proposed Action Split

### Current Actions (Priority Management) ✅

**Rename for clarity:**
- `list_tasks` → `list_priorities` or keep as `list_tasks`
- `set_task` → `set_priority` or keep as `set_task`
- `clear_tasks` → `clear_current_task` (keep name, but add better docs)

**What they do:**
- Get/set ChoreGroup priority values (0-5)
- Shows duplicate's preferences for work types
- Maps to Jobs/Priorities screen functionality

### New Actions Needed (Errand Management) 🆕

#### 1. `list_errands` / `get_available_errands`

**Purpose**: Get all errands (chores) currently available for the duplicate

**Parameters**:
```json
{
  "filter_type": "all",  // all, assigned, nearby, priority
  "max_distance": 100,   // optional: only within X tiles
  "chore_types": [],     // optional: filter by ChoreType names
  "max_results": 50      // limit results
}
```

**Returns**:
```json
{
  "errands": [
    {
      "id": "chore_12345",  // internal ID
      "type": "Mop",
      "group": "Basekeeping",
      "description": "Mop floor",
      "location": {"x": 25, "y": 10},
      "distance": 15.3,
      "priority": 5,  // building priority
      "assigned_to": null,  // or duplicate name
      "can_perform": true   // can THIS duplicate do it?
    },
    {
      "id": "chore_12346",
      "type": "Build",
      "group": "Build",
      "description": "Build Ladder",
      "location": {"x": 30, "y": 15},
      "distance": 20.1,
      "priority": 7,
      "assigned_to": "Meep",
      "can_perform": false  // maybe no construction skill
    }
  ],
  "total_available": 127,
  "showing": 50
}
```

**Implementation considerations:**
- Use `GlobalChoreProvider.Instance.choreWorldMap` to find chores
- Filter by `choreConsumer.IsPermittedByUser(chore)` to check if duplicate can do it
- Calculate distance from duplicate's position
- Sort by priority/distance
- **Performance**: Limit results, don't iterate ALL chores

#### 2. `assign_errand` / `force_errand`

**Purpose**: Force duplicate to perform a specific errand

**Parameters**:
```json
{
  "errand_id": "chore_12345",  // from list_errands
  // OR
  "errand_type": "Mop",
  "location": {"x": 25, "y": 10}
}
```

**Returns**:
```json
{
  "success": true,
  "message": "Neuro assigned to Mop floor at (25, 10)",
  "errand_id": "chore_12345"
}
```

**⚠️ WARNING**: This is **RISKY**!
- Forcing a chore can break ONI's AI
- Duplicate might get stuck if prerequisites aren't met
- Should validate chore is still valid and reachable

**Safer alternative**: Just increase ChoreGroup priority and let AI pick

#### 3. `get_current_errand` / `get_duplicate_status`

**Purpose**: Get detailed info about what duplicate is currently doing

**Parameters**: None (uses Neuro duplicate)

**Returns**:
```json
{
  "has_errand": true,
  "errand": {
    "type": "Mop",
    "group": "Basekeeping",
    "description": "Mop floor",
    "location": {"x": 25, "y": 10},
    "progress": 0.45,  // 45% complete
    "assigned_by": "ai"  // or "forced"
  },
  "next_errands": [
    // Queue of upcoming tasks (if any)
  ]
}
```

**Implementation**:
```csharp
Chore currentChore = choreConsumer.choreDriver.GetCurrentChore();
// Get progress, type, location, etc.
```

## Recommended Implementation Plan

### Phase 1: Documentation & Naming (Current) ✅

1. Document the difference between Priorities and Errands
2. Update existing action names/docs to clarify they handle Priorities
3. Fix clear_tasks graphics error (already done)

### Phase 2: Read-Only Errand Actions 🆕

**Implement:**
- `list_errands` - Show available chores
- `get_current_errand` - Show what duplicate is doing

**Benefits:**
- Safe (read-only, no AI interference)
- Useful for context/decision-making
- Foundation for future write operations

**Code location**: Create `ErrандActions.cs` (new file)

### Phase 3: Experimental Errand Assignment (Optional) ⚠️

**Implement with caution:**
- `assign_errand` - Force specific chore

**Requirements:**
- Extensive validation (chore exists, reachable, duplicate can do it)
- Fallback to AI if chore becomes invalid
- Clear warnings in documentation
- Debug logging to track issues

### Phase 4: Integration & Testing

- Test all actions together
- Document workflows (e.g., "find closest Mop errand → set Basekeeping priority high")
- Create test suite like `test-errand-execute.js`

## API Design Examples

### Example 1: Find and Prioritize Work

```javascript
// List available errands
const errands = await sendAction('list_errands', {
  filter_type: 'nearby',
  max_distance: 50,
  max_results: 20
});

// Find urgent tasks
const urgent = errands.errands.filter(e => e.priority >= 7);

// If lots of Mop tasks, boost Basekeeping priority
const mopTasks = urgent.filter(e => e.type === 'Mop');
if (mopTasks.length > 5) {
  await sendAction('set_task', {
    task_type: 'Basekeeping',
    priority: 'high'
  });
}
```

### Example 2: Monitor Current Work

```javascript
// Check what Neuro is doing
const status = await sendAction('get_current_errand');

if (status.has_errand && status.errand.group === 'Dig') {
  console.log(`Neuro is digging at (${status.errand.location.x}, ${status.errand.location.y})`);
  console.log(`Progress: ${status.errand.progress * 100}%`);
}
```

### Example 3: List All Farming Errands

```javascript
// Find all farming tasks
const farmWork = await sendAction('list_errands', {
  chore_types: ['Harvest', 'FarmTend', 'Uproot'],
  max_results: 100
});

// Group by location
const byLocation = farmWork.errands.reduce((acc, errand) => {
  const key = `${errand.location.x},${errand.location.y}`;
  acc[key] = acc[key] || [];
  acc[key].push(errand);
  return acc;
}, {});

console.log('Farm areas needing attention:', Object.keys(byLocation).length);
```

## Safety Considerations

### What's Safe ✅

1. **Reading Priority values** (current list_tasks/set_task)
2. **Setting Priority values** (adjusting willingness to work)
3. **Reading available errands** (just looking, not touching)
4. **Reading current errand** (monitoring status)

### What's Risky ⚠️

1. **Forcing errand assignment** (can break AI pathfinding)
2. **Canceling errands repeatedly** (duplicate becomes indecisive)
3. **Setting invalid chore data** (game crashes)

### Best Practices

1. **Prefer Priority adjustments over forced assignments**
   - Let ONI's AI pick the best errand from the list
   - Just adjust priorities to influence choices

2. **Use errand listing for context, not control**
   - See what work is available
   - Make decisions based on that
   - But let duplicate choose specifics

3. **Only force errands in exceptional cases**
   - Emergency situations
   - Specific requests from user
   - With extensive validation

## File Structure

```
ONIMod/Integration/Actions/
  ├── TaskActions.cs          (current - Priority management)
  │   ├── list_tasks
  │   ├── set_task
  │   └── clear_tasks
  │
  └── ErrandActions.cs        (new - Errand management)
      ├── list_errands
      ├── get_current_errand
      └── assign_errand (optional, risky)
```

## Questions for User

1. **Priority**: Do you want read-only errand actions first (safer), or full assignment (riskier)?

2. **Use case**: What's the main goal?
   - Just see what work is available?
   - Actually force specific tasks?
   - Something else?

3. **Naming**: Prefer "errands" or "chores" in action names?
   - `list_errands` vs `list_chores`
   - `get_current_errand` vs `get_current_chore`

4. **Integration**: Keep existing action names or rename for clarity?
   - Current: `list_tasks`, `set_task`, `clear_tasks`
   - Clearer: `list_priorities`, `set_priority`, `clear_current_task`

## Next Steps

Let me know:
1. Should we proceed with implementing `list_errands` and `get_current_errand`?
2. Do you want errand assignment (`assign_errand`), or just reading?
3. Any specific use cases you have in mind?

Then I'll create the `ErrandActions.cs` file with the actions you need.
