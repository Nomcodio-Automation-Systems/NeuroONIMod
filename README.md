# NeuroMod - Oxygen Not Included Mod for Neuro-sama

A mod for Oxygen Not Included that allows [Neuro-sama](https://www.twitch.tv/vedal987) to control a duplicate and interact with the game.

## 🚀 Quick Start

### Prerequisites

- Oxygen Not Included (game)
- Visual Studio 2022 (for building the mod)
- Node.js 16+ (for Randy test server)
- NeuroSDK access

### Dependencies

**Build Dependencies:**
- Visual Studio 2022 or later
- .NET Framework 4.7.2 SDK
- MSBuild (included with Visual Studio)

**Runtime Dependencies (from ONI installation):**
- Unity Engine 2021.3.33
- Assembly-CSharp.dll (ONI game code)
- UnityEngine.CoreModule.dll
- UnityEngine.UI.dll
- UnityEngine.TextRenderingModule.dll
- 0Harmony.dll (for patching)

**NuGet Packages (auto-restored):**
- Newtonsoft.Json 13.0.4
- VedalAI.NeuroSdk.Unity 1.2.1
- UniTask 2.5.10
- System.Runtime.CompilerServices.Unsafe 6.1.2


### Building the Mod

```batch
Build-And-Deploy-Release.bat
```

This will compile the mod and copy it to your ONI mods folder.

### Starting Randy (Test Server)

```batch
start-randy.bat
```

Randy acts as a mock Neuro connection for testing actions without the actual Neuro API.

### Running Tests

```batch
test-priority-execute.bat    # Test priority management
test-errand-assign.bat        # Test errand assignment
test-schedule-execute.bat     # Test schedule system
```

## 📁 Project Structure

```
NeuroMod/
├── NeuroMod/                 # Main mod source code
│   ├── Actions/              # Action handlers (old structure)
│   ├── Integration/          # Integration layer with Neuro
│   │   ├── Actions/          # Action implementations
│   │   │   ├── PriorityActions.cs   # ChoreGroup priority management
│   │   │   ├── ErrandActions.cs     # Errand listing and assignment
│   │   │   └── ScheduleActions.cs   # Schedule management
│   │   └── NeuroIntegrationManager.cs
│   ├── BioData/              # Duplicate bio data tracking
│   ├── Schedule/             # Schedule block system
│   ├── Websocket/            # WebSocket communication
│   └── Configuration/        # Mod configuration
│
├── Randy/                    # Test server (mock Neuro connection)
│   ├── index.ts              # Main Randy server
│   ├── test-*.js             # Test scripts
│   └── README.md             # Randy documentation
│
├── NeuroMod.Tests/           # C# unit tests
│
├── docs/                     # Documentation
│   ├── SPECIFICATION.md      # Full project specification
│   ├── USAGE.md              # User guide
│   ├── QUICK_TEST_GUIDE.md   # Testing guide
│   ├── ASSIGN_ERRAND_SIMPLIFIED.md   # Errand assignment docs
│   ├── PRIORITIES_VS_ERRANDS_DESIGN.md
│   └── [Other design docs]
│
└── [Build scripts]           # Batch files for building/deploying
```

## 🎮 Core Features

### Priority Management

Control which types of work Neuro will prioritize:

- **17 ChoreGroups** (Dig, Build, Cook, Research, etc.)
- **Priority levels 0-5** (Disabled to Critical)
- Actions: `list_priorities`, `set_priority`

### Status & Control

Monitor Neuro's status and control current work:

- Health, Stress, Calories, Stamina tracking
- Current task/errand information
- **Location data**: Grid coordinates, screen position, and room/area
- Stop current work: `clear_current_errand`
- Query status: `get_status`

### Errand Assignment

Boost specific work priorities by finding available errands:

- Find nearest errand of a type (Mop, Dig, Build, etc.)
- Automatically boost ChoreGroup priority to 5 (critical)
- Actions: `list_errands`, `get_current_errand`, `assign_errand`

### Schedule System

Manage Neuro's daily schedule with custom blocks:

- Define work/sleep/break periods
- Set priorities per schedule block
- Actions: `get_schedule`, `set_schedule`, `get_current_block`

## 🔧 Build Scripts

| Script | Description |
|--------|-------------|
| `Build-Release.bat` | Build mod in Release mode |
| `Build-Debug.bat` | Build mod with debug symbols |
| `Build-And-Deploy-Release.bat` | Build and copy to ONI mods folder |
| `Deploy-Mod.bat` | Copy compiled DLL to ONI |
| `Clean-VS-Cache.bat` | Clean Visual Studio cache |

## 🧪 Testing

### Test Scripts

| Script | Description |
|--------|-------------|
| `test-priority-execute.bat` | Test priority management actions |
| `test-errand-assign.bat` | Test errand assignment system |
| `test-schedule-execute.bat` | Test schedule system |

### Randy Test Server

Randy provides a mock Neuro connection for testing:

```batch
# Start Randy
start-randy.bat

# Run tests from another terminal
cd Randy
node test-quick.js           # Quick connection test
node test-comprehensive.js   # Full test suite
```

See `Randy/README.md` for more details.

## 📖 Documentation

- **[SPECIFICATION.md](docs/SPECIFICATION.md)** - Complete project specification
- **[USAGE.md](docs/USAGE.md)** - User guide and API reference
- **[QUICK_TEST_GUIDE.md](docs/QUICK_TEST_GUIDE.md)** - Testing guide
- **[PRIORITIES_VS_ERRANDS_DESIGN.md](docs/PRIORITIES_VS_ERRANDS_DESIGN.md)** - Priority system architecture
- **[ASSIGN_ERRAND_SIMPLIFIED.md](docs/ASSIGN_ERRAND_SIMPLIFIED.md)** - Errand assignment guide

## 🎯 Action Reference

### Priority Actions (ChoreGroup Management)

Manage Neuro's willingness to perform different work categories.

```javascript
// List all 17 ChoreGroup priorities (0-5)
list_priorities({})

// Set priority for a specific ChoreGroup
set_priority({
  chore_group: "Digging",
  priority: 5  // 0=Disabled, 5=Critical
})
```

### Status & Control Actions

Monitor duplicate status and control current work.

```javascript
// Get current status (health, stress, calories, stamina, current task)
get_status({
  query_type: "basic",  // "minimal", "basic", or "detailed"
  include_environment: false,
  include_skills: false
})

// Example response:
// Status Report for Neuro:
// Health: 100.0% (Perfect)
// Stress: 5.2%
// Calories: 85.3%
// Stamina: 92.1%
// Location: Grid (45, 23), Screen (1024, 768), Room: Barracks
// Current Task: Digging

// Stop current errand immediately
clear_current_errand({
  force_stop: false,
  reason: "Taking a break"
})
```

### Errand Actions (Chore Management)

```javascript
// List available errands nearby
list_errands({
  filter_type: "nearby",
  max_distance: 50,
  chore_type: "Mop"
})

// Get what Neuro is currently doing
get_current_errand({})

// Boost priority by finding an errand
assign_errand({
  errand_type: "Mop",
  max_distance: 50,
  target_x: 100,  // Optional
  target_y: 50    // Optional
})
```

### Schedule Actions

```javascript
// Get current schedule
get_schedule({})

// Set schedule blocks
set_schedule({
  blocks: [
    {
      name: "Morning Work",
      start_time: 6.0,
      duration: 6.0,
      priorities: { Digging: 5, Building: 4 }
    },
    // ... more blocks
  ]
})

// Get current active block
get_current_block({})
```

## 🛠️ Development

### Development Setup

- Visual Studio 2022
- .NET Framework 4.7.2
- ONI installed
- Unity references from ONI installation

### Building

1. Open `Put Neuro Into a Dupe.sln` in Visual Studio
2. Build in Release mode
3. DLL is output to `NeuroMod/bin/Release/`
4. Run `Deploy-Mod.bat` to copy to ONI mods folder

### Testing Changes

1. Build the mod
2. Start Randy: `start-randy.bat`
3. Launch ONI with the mod
4. Run tests: `test-priority-execute.bat`
5. Check logs in `%AppData%\..\LocalLow\Klei\OxygenNotIncluded\Player.log`

## 📝 Coding Standards

See `.github/copilot-instructions.md` for:

- Naming conventions (PascalCase, camelCase)
- Error handling patterns
- Documentation requirements
- Testing guidelines

## 🐛 Troubleshooting

### Mod Not Loading

- Check `Player.log` for errors
- Verify DLL is in ONI mods folder
- Ensure all dependencies are present

### Actions Not Working

- Check Randy is running (`start-randy.bat`)
- Verify WebSocket connection in logs
- Test with `Randy/test-connection.js`

### "Unknown action" Errors

- Rebuild and deploy the mod
- Restart ONI to load new DLL
- Check action names match exactly

## 📜 License

This project is licensed under the [MIT License](LICENSE).

## 🙏 Credits

- **Vedal987** - Neuro-sama creator
- **Klei Entertainment** - Oxygen Not Included
- **VedalAI** - NeuroSDK
- **LinearBotSystems**
---

**Note**: This mod is for entertainment and educational purposes. Use responsibly!
