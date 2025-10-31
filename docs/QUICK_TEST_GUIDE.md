# Quick Start: Test Neuro Schedule System

## Installation & Testing (3 Steps)

### ✅ Step 1: Deploy Mod to ONI
```cmd
deploy-to-oni.bat
```
**Result:** Mod installed to `%USERPROFILE%\Documents\Klei\OxygenNotIncluded\mods\NeuroMod`

### ✅ Step 2: Start Randy (Separate Terminal)
```cmd
cd Randy
npm start
```
**Result:** Randy running on `ws://localhost:8000` and `http://localhost:1337`

### ✅ Step 3: Run Test
```cmd
run-schedule-test.bat
```
**Result:** Tests WebSocket connection and action registration

## What Was Created

### Deployment
- ✅ `deploy-to-oni.bat` - Installs mod to ONI
- ✅ Mod files copied: `ONIMod.dll`, `PLib.dll`, configs

### Testing
- ✅ `run-schedule-test.bat` - Test runner
- ✅ `Randy/test-schedule-system.js` - Schedule system test (7 tests)
- ✅ `docs/TESTING_SCHEDULE_SYSTEM.md` - Complete testing guide

### Test Coverage
1. ✓ Connect to Randy WebSocket
2. ✓ Register schedule actions (get_schedule, set_schedule, list_schedules)
3. ✓ Test get_schedule action
4. ✓ Test list_schedules action
5. ✓ Test set_schedule (work_focused)
6. ✓ Test set_schedule (rest_focused)
7. ✓ Verify schedule changes

## Expected Test Output

```
╔════════════════════════════════════════════════════════╗
║   Neuro Schedule System Integration Test (Randy)      ║
╚════════════════════════════════════════════════════════╝

[1/7] Connecting to Randy at ws://localhost:8000...
  ✓ Connected to Randy
✓ PASS Connect to Randy WebSocket

[2/7] Registering Neuro schedule actions...
  ✓ Registered 3 schedule actions
✓ PASS Register schedule actions

... (5 more tests) ...

╔════════════════════════════════════════════════════════╗
║          ✓ ALL TESTS PASSED! Schedule system OK       ║
╚════════════════════════════════════════════════════════╝
```

## Next: Test in ONI

### 1. Enable Mod
- Launch ONI
- Mods menu → Enable "NeuroMod"
- Restart ONI

### 2. Check Logs
**Location:** `%USERPROFILE%\AppData\LocalLow\Klei\Oxygen Not Included\Player.log`

**Look for:**
```
[NeuroScheduleManager] Creating new schedule: Neuro's Schedule
[NeuroScheduleManager] Successfully created schedule: Neuro's Schedule
[ONIMod] Neuro duplicant (Neuro) spawned!
[NeuroScheduleManager] Successfully assigned Neuro to Neuro's Schedule
```

### 3. Verify in Game
1. Open Schedule UI (clock icon)
2. Look for "Neuro's Schedule"
3. Verify only Neuro is assigned
4. Other duplicants on different schedules

### 4. Test Schedule Changes
Send via Randy or HTTP API:
```javascript
// Change schedule
{ "action": "set_schedule", "data": { "schedule_type": "work_focused" } }

// Get current schedule  
{ "action": "get_schedule", "data": {} }

// List available
{ "action": "list_schedules", "data": {} }
```

## Summary

✅ **Mod Deployed:** ONI mods folder  
✅ **Test Created:** Randy integration test (7 tests)  
✅ **Documentation:** Complete testing guide  
✅ **Ready:** Run `run-schedule-test.bat` to verify

**Full Documentation:** See `docs/TESTING_SCHEDULE_SYSTEM.md`
