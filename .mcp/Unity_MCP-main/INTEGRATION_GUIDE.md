# 🎮 Unity MCP Integration Guide

**Complete reference for Unity MCP tools integration with any MCP-compatible AI client.**

## 🎯 Available Unity MCP Tools (40 Total)

### Quick Tool List

When users ask for Unity operations, these tools are available through the `unity-mcp` server:

#### **Project Management (7 tools)**
| Tool | Command | Description |
|------|---------|-------------|
| Project Analysis | `project.analyze` | Full project structure analysis |
| Project Info | `project.getInfo` | Basic project information |
| Project Settings | `project.setSettings` | Modify project configuration |
| Scene List | `project.listScenes` | List all project scenes |
| Build Settings | `project.getBuildSettings` | Get build configuration |
| Build Target | `project.setBuildTarget` | Change build platform |
| Refresh Assets | `project.refreshAssets` | Refresh asset database |

#### **Scene Operations (8 tools)**
| Tool | Command | Description |
|------|---------|-------------|
| Create Objects | `scene.createGameObject` | Create GameObjects with components |
| Modify Components | `scene.modifyComponent` | Add/edit/remove components |
| Query Scene | `scene.query` | Find objects in scene |
| Select Objects | `scene.selectObjects` | Select objects in Unity |
| Delete Objects | `scene.deleteGameObject` | Remove GameObjects |
| Move Objects | `scene.moveGameObject` | Transform objects |
| Save Scene | `scene.save` | Save current scene |
| Load Scene | `scene.load` | Open different scene |

#### **Asset Management (8 tools)**
| Tool | Command | Description |
|------|---------|-------------|
| Import Assets | `assets.import` | Import files into project |
| Create Material | `assets.createMaterial` | Generate materials |
| Manage Prefabs | `assets.managePrefabs` | Create/modify prefabs |
| Organize Assets | `assets.organize` | Folder organization |
| Search Assets | `assets.search` | Find assets by criteria |
| Create Texture | `assets.createTexture` | Generate textures |
| Package Manager | `packages.manage` | Install/update packages |
| Asset Info | `assets.getInfo` | Get asset details |

#### **Code Generation (8 tools)**
| Tool | Command | Description |
|------|---------|-------------|
| Create Script | `code.createScript` | Generate C# scripts |
| Analyze Scripts | `code.analyzeScripts` | Parse existing code |
| Attach Scripts | `code.attachScripts` | Attach scripts to objects |
| Find References | `code.findReferences` | Find code usage |
| Refactor Code | `code.refactor` | Rename/move code |
| Generate Docs | `code.generateDocumentation` | Auto-generate docs |
| Validate Code | `code.validate` | Check code quality |
| Format Code | `code.format` | Format code style |

#### **Build & Deploy (9 tools)**
| Tool | Command | Description |
|------|---------|-------------|
| Configure Build | `build.configure` | Setup build settings |
| Execute Build | `build.execute` | Run builds |
| Run Tests | `build.runTests` | Execute tests |
| Build Report | `build.getReport` | Get build statistics |
| Clean Build | `build.clean` | Clean cache/temp files |
| Addressables | `build.addressables` | Manage addressables |
| Optimize Build | `build.optimize` | Optimize performance |
| Console Logs | `build.getConsoleLogs` | Get Unity console |
| Profile Build | `build.profile` | Performance profiling |

## 🤖 Natural Language Mapping

### Common User Requests → Tool Usage

**Project Analysis:**
- "Analyze my Unity project" → `project.analyze`
- "What's in this project?" → `project.getInfo`
- "Show project structure" → `project.analyze` with full parameters

**Scene Building:**
- "Create a cube" → `scene.createGameObject` with cube primitive
- "Add physics to player" → `scene.modifyComponent` with Rigidbody
- "Delete all enemies" → `scene.query` + `scene.deleteGameObject`

**Asset Creation:**
- "Make a red material" → `assets.createMaterial` with red color
- "Import these textures" → `assets.import` with file paths
- "Organize my assets" → `assets.organize` with folder operations

**Code Development:**
- "Create a player controller" → `code.createScript` with MonoBehaviour
- "Find all PlayerController usage" → `code.findReferences`
- "Generate enemy AI script" → `code.createScript` with AI template

**Building:**
- "Build for Android" → `project.setBuildTarget` + `build.execute`
- "Run all tests" → `build.runTests`
- "Optimize my build" → `build.optimize`

## 📋 Tool Parameter Examples

### Essential Parameter Patterns

#### Creating GameObjects:
```json
{
  "name": "Player",
  "primitive": "Capsule",
  "position": {"x": 0, "y": 1, "z": 0},
  "components": ["Rigidbody", "CapsuleCollider"]
}
```

#### Creating Materials:
```json
{
  "materialName": "PlayerMaterial",
  "properties": {
    "albedo": {"r": 0.8, "g": 0.2, "b": 0.2, "a": 1.0},
    "metallic": 0.5,
    "smoothness": 0.8
  }
}
```

#### Creating Scripts:
```json
{
  "scriptName": "PlayerController",
  "scriptType": "MonoBehaviour",
  "methods": ["Start", "Update", "FixedUpdate"],
  "namespace": "Game.Player"
}
```

#### Project Analysis:
```json
{
  "includeAssets": true,
  "includePackages": true,
  "includeScenes": true,
  "includeSettings": true
}
```

#### Build Configuration:
```json
{
  "platform": "Android",
  "developmentBuild": false,
  "scriptingBackend": "IL2CPP",
  "targetArchitecture": "ARM64"
}
```

#### Scene Query:
```json
{
  "criteria": {
    "tag": "Enemy",
    "layer": "Default",
    "componentType": "EnemyAI"
  },
  "includeInactive": false
}
```

## 🔧 Implementation Notes

### Prerequisites
Before using Unity tools, verify:
1. ✅ Unity Editor is running
2. ✅ Unity MCP Server window is open (Window → MCP Server)
3. ✅ Server shows "Running" status
4. ✅ Port 8090 is accessible (or your configured port)

### Error Handling
Common error patterns and solutions:

| Error | Meaning | Solution |
|-------|---------|----------|
| `"Connection refused"` | Unity not running | Start Unity Editor and MCP Server |
| `"Tool execution timed out"` | Unity busy/frozen | Wait or restart Unity |
| `"Tool 'X' not found"` | Wrong tool name format | Check tool name spelling |
| `"Invalid parameters"` | Incorrect JSON structure | Validate against tool schema |
| `"Unity Editor not responding"` | Editor crashed | Restart Unity Editor |

### Response Format
All tools return a standardized response:
```json
{
  "Id": "request-id-uuid",
  "Type": "response",
  "Result": {
    "success": true,
    "data": {},
    "message": "Operation completed"
  },
  "Error": null
}
```

On error:
```json
{
  "Id": "request-id-uuid",
  "Type": "response",
  "Result": null,
  "Error": {
    "code": "ERROR_CODE",
    "message": "Error description"
  }
}
```

### Tool Categories for Context
- **Project**: Settings, configuration, analysis
- **Scene**: GameObjects, components, hierarchy
- **Assets**: Materials, textures, prefabs, import
- **Code**: Scripts, analysis, documentation
- **Build**: Compilation, testing, deployment

## 🎯 AI Assistant Guidelines

### Suggested Response Patterns

**For "Analyze project":**
> "I'll analyze your Unity project structure using the project.analyze tool. This will give us information about scenes, assets, packages, and settings."

**For "Create a player character":**
> "I'll create a player character by:
> 1. Creating a capsule GameObject with scene.createGameObject
> 2. Adding physics components with scene.modifyComponent
> 3. Generating a PlayerController script with code.createScript
> 4. Attaching the script with code.attachScripts"

**For "Build for mobile":**
> "I'll configure your project for mobile deployment:
> 1. Setting Android build target with project.setBuildTarget
> 2. Configuring build settings with build.configure
> 3. Executing the build with build.execute"

### Best Practices
1. **Always verify Unity connection** before tool usage
2. **Use descriptive parameter names** for clarity
3. **Chain related operations** for complex workflows
4. **Provide error context** when operations fail
5. **Suggest alternatives** if primary tools fail
6. **Validate parameters** before sending requests
7. **Show progress** for multi-step operations

### Tool Chaining Examples

**Complex Scene Setup:**
```
1. scene.createGameObject (create player)
2. scene.modifyComponent (add Rigidbody)
3. code.createScript (generate controller)
4. code.attachScripts (attach to player)
5. scene.save (save scene)
```

**Asset Pipeline:**
```
1. assets.import (import textures)
2. assets.createMaterial (create material)
3. assets.managePrefabs (create prefab)
4. assets.organize (organize in folders)
```

**Build Pipeline:**
```
1. project.setBuildTarget (set platform)
2. build.configure (configure settings)
3. build.runTests (run tests)
4. build.execute (build project)
5. build.getReport (get build stats)
```

## 📞 Technical Details

### Connection Information
- **Protocol**: HTTP POST with JSON payloads
- **Default Port**: 8090 (configurable via `UNITY_PORT`)
- **Base URL**: `http://localhost:8090`
- **Timeout**: 10 seconds default (configurable via `REQUEST_TIMEOUT`)
- **Retry Logic**: 3 attempts default (configurable via `RETRY_ATTEMPTS`)

### Environment Variables
| Variable | Default | Description |
|----------|---------|-------------|
| `UNITY_PORT` | `8090` | Unity Editor HTTP server port |
| `REQUEST_TIMEOUT` | `10` | Request timeout in seconds |
| `RETRY_ATTEMPTS` | `3` | Connection retry attempts |
| `RETRY_DELAY` | `1000` | Delay between retries (ms) |
| `LOG_LEVEL` | `info` | Logging level (debug/info/warn/error) |
| `NODE_ENV` | `production` | Node environment |

### Security Considerations
- Server only accepts connections from `localhost`
- No external network access required
- All communication is local HTTP
- No authentication required (local-only)
- See [SECURITY_AND_PRIVACY.md](./SECURITY_AND_PRIVACY.md) for details

## 🚀 Quick Start

### 1. Install Unity Plugin
```bash
# Copy Unity folder to your Unity project
cp -r Unity/Editor YourUnityProject/Assets/Editor/UnityMCP
```

### 2. Build Node.js Server
```bash
cd Server
npm install
npm run build
```

### 3. Configure Your IDE
See [CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md) for configuration examples for:
- Any MCP client with stdio or HTTP MCP support (see CLIENT_CONFIGS.md)

### 4. Start Unity & Test
1. Open Unity Editor
2. Open Window → MCP Server
3. Click "Start Server"
4. In your AI IDE, ask: "What Unity tools are available?"

## 📚 Additional Resources

- **[CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md)** - Configuration for all supported IDEs
- **[TOOLS_REFERENCE.md](./TOOLS_REFERENCE.md)** - Complete tool documentation
- **[QUICK_SETUP.md](./QUICK_SETUP.md)** - 5-minute setup guide
- **[SECURITY_AND_PRIVACY.md](./SECURITY_AND_PRIVACY.md)** - Security best practices
- **[QUICK_UNITY_SETUP.md](./QUICK_UNITY_SETUP.md)** - Unity plugin installation

## 🧪 Testing Your Integration

Once configured, test with these commands:

1. **List tools**:
   ```
   "What Unity MCP tools are available?"
   ```

2. **Get project info**:
   ```
   "Show me my Unity project information"
   ```

3. **Create a simple object**:
   ```
   "Create a cube named TestCube at position (0, 0, 0)"
   ```

4. **Analyze project**:
   ```
   "Analyze my Unity project and show me the structure"
   ```

If these work, your integration is successful! 🎉

---

**For AI Assistants**: Use these 40 tools to provide comprehensive Unity development assistance through natural language commands. All tools are production-ready and cover the complete Unity development workflow.
