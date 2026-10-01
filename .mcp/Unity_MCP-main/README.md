# 🎮 Unity MCP Server

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A powerful, **free and open source** Model Context Protocol (MCP) server that enables seamless Unity Editor integration with any MCP-compatible AI client or coding assistant (Claude Code, Cursor, Windsurf, and more). Control Unity projects through natural language commands powered by AI — no subscription or paid license required.

This is a community-driven alternative to the official Unity AI Assistant's MCP integration (which requires a Unity AI subscription for Personal users beyond a short trial).

## ✨ Features

## ✨ Features

- 🎯 **43 Unity Tools** - Comprehensive project, scene, asset, code, and build management
- 👁️ **Viewport Capture** - AI can "see" the Unity scene, enabling visual feedback loops
- ⏱️ **Runtime Observation** - AI records property changes over time during Play Mode — the temporal debugger that closes the build→test→fix loop
- 🎮 **Input Simulation** - AI can play the game by sending keyboard/mouse input during Play Mode
- 🔄 **Real-time Integration** - Direct Unity Editor communication via HTTP API
- 🤖 **AI-Powered** - Natural language commands for complex Unity operations
- 🛠️ **Extensible** - Easy to add custom tools for specific project needs
- 📱 **Cross-Platform** - Works on Windows, macOS, and Linux
- 🚀 **Zero Config** - Auto-discovery and setup for new Unity projects

## 🏗️ Architecture

```
MCP Client ←→ Node.js MCP Server ←→ Unity Editor C# Plugin
                    │                                 │                        │
                AI Commands                    Protocol Bridge            Unity API

Complete AI Development Loop:
  Build ──→ Run ──→ Observe ──→ Evaluate ──→ Fix ──→ Build
   │         │        │           │            │
  code.*   play.*  observe    AI reasons    code.*
  scene.*  enter   (time-     on temporal   scene.*
  asset.*          series)    data + stats
```

### Components

1. **Node.js MCP Server** (`Server/`) - Handles MCP client communication and tool routing
2. **Unity C# Plugin** (`Unity/`) - Executes commands directly in Unity Editor
3. **Python Client** (`unity_mcp_client.py`) - Test client for development

### Supported Clients

Any MCP-compatible client or AI coding assistant that supports the Model Context Protocol (stdio or HTTP transport). Common examples include various AI-powered IDEs and extensions. See CLIENT_CONFIGS.md for configuration patterns.

## 🚀 Quick Start

### Prerequisites

- Unity 2022.3+
- Node.js 18+
- An MCP-compatible AI client or coding assistant

### Installation

1. **Clone the repository**:
   ```bash
   git clone https://github.com/isekream/Unity_MCP.git
   cd Unity_MCP
   ```

2. **Install Node.js dependencies**:
   ```bash
   cd Server
   npm install
   npm run build
   cd ..
   ```

3. **Configure Your AI IDE**:

   Configuration varies by IDE. See [CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md) for detailed examples.

   **Quick Example**:
   Add a server entry to your MCP client's configuration file (exact location depends on the client; see [CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md) for common patterns):
   ```json
   {
     "mcpServers": {
       "unity-mcp": {
         "command": "node",
         "args": ["/absolute/path/to/UnityMCP/Server/build/index.js"],
         "env": {
           "NODE_ENV": "production",
           "UNITY_PORT": "8090"
         }
       }
     }
   }
   ```

   See [CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md) for configuration details for popular clients.

4. **Install Unity Plugin**:
   - Open Unity Editor with your project
   - Go to `Window > Package Manager`
   - Click `+` → `Add package from git URL...`
   - Enter: `https://github.com/isekream/Unity_MCP.git?path=/Unity`

5. **Start the Unity MCP Server**:
   - In Unity: go to **Tools > Unity MCP > Server Window** (opens the control window)
   - Click the **Start Server** button
   - Look for the log message "MCP Server started on port 8090"
   - The window should show status "Running" at http://localhost:8090

6. **Restart Your AI IDE** to load the MCP server

## 🎯 Available Tools (43 Total)

### Project Management (7 tools)
- `project.analyze` - Comprehensive project analysis and structure overview
- `project.getInfo` - Get basic project information and settings
- `project.setSettings` - Modify project configuration and player settings
- `project.listScenes` - List all scenes in build settings with status
- `project.getBuildSettings` - Get current build configuration
- `project.setBuildTarget` - Change active build target platform
- `project.refreshAssets` - Force refresh of project assets database

### Scene Operations (9 tools)
- `scene.createGameObject` - Create GameObjects with components and hierarchy
- `scene.modifyComponent` - Add, remove, or update component properties
- `scene.query` - Query scene hierarchy and find objects by criteria
- `scene.selectObjects` - Select and focus objects in Scene/Hierarchy view
- `scene.deleteGameObject` - Remove GameObjects and children safely
- `scene.moveGameObject` - Transform position, rotation, scale, and parenting
- `scene.save` - Save current scene or all open scenes
- `scene.load` - Open and switch between project scenes
- `scene.capture` - Capture viewport screenshot for AI visual inspection

### Asset Management (8 tools)
- `assets.import` - Import files with custom import settings
- `assets.createMaterial` - Generate materials with shader properties
- `assets.managePrefabs` - Create, modify, and instantiate prefab assets
- `assets.organize` - Folder operations, moving, and organizing project assets
- `assets.search` - Find assets by name, type, or properties
- `assets.createTexture` - Generate procedural textures and import images
- `packages.manage` - Install, update, and remove Unity packages
- `assets.getInfo` - Get detailed information about selected assets

### Code Generation (8 tools)
- `code.createScript` - Generate C# scripts from templates or descriptions
- `code.analyzeScripts` - Parse existing scripts and extract information
- `code.attachScripts` - Attach MonoBehaviour scripts to GameObjects
- `code.findReferences` - Find script and component usage across project
- `code.refactor` - Rename, move, and restructure code safely
- `code.generateDocumentation` - Auto-generate XML documentation
- `code.validate` - Check scripts for common issues and best practices
- `code.format` - Format and style code according to conventions

### Build & Deploy (9 tools)
- `build.configure` - Set build settings, scenes, and platform options
- `build.execute` - Trigger builds for target platforms
- `build.runTests` - Execute Unity Test Runner and get results
- `build.getReport` - Get detailed build reports and statistics
- `build.clean` - Clean build cache and temporary files
- `build.addressables` - Manage Addressable Asset System configuration
- `build.optimize` - Analyze and optimize build size and performance
- `build.getConsoleLogs` - Retrieve Unity Console messages and errors
- `build.profile` - Performance profiling and optimization suggestions

### Play Mode & Runtime (13 tools)
- `playmode.getState` - Check if game is playing, paused, or stopped
- `playmode.enter` - Enter Play Mode to run the game
- `playmode.exit` - Exit Play Mode and return to Edit Mode
- `playmode.pause` - Pause the running game
- `playmode.resume` - Resume a paused game
- `playmode.step` - Advance by N frames for frame-by-frame debugging
- `playmode.inspectGameObject` - Inspect live runtime component values
- `playmode.setProperty` - Modify component properties at runtime
- `playmode.invokeMethod` - Call public methods on components during Play Mode
- `playmode.getConsoleLogs` - Retrieve runtime console logs and errors
- `playmode.getRuntimeInfo` - Get FPS, memory, physics stats, and time info
- `playmode.setTimeScale` - Change game speed (slow-motion, fast-forward)
- **`playmode.observe`** - **Record property values over time during Play Mode** — the AI's temporal debugger. Tracks transforms, velocities, custom fields across multiple frames and returns structured time-series with summary stats (min/max/mean/delta). Optionally simulates input to test behavior end-to-end.
- **`playmode.simulateInput`** - **Send keyboard/mouse input to the running game**. Supports press, release, and tap actions. Works with both legacy Input and new Input System.

### Persistent Project Intelligence (Memory Layer)
- `memory.status` - Inspect the current state of the project's long-term memory
- `memory.rebuild` - Deeply index scripts, design docs, and intent into a queryable model
- `memory.query` - Ask natural-language questions against accumulated project knowledge and past insights
- `memory.recordInsight` - Explicitly store design decisions, measured results, and learnings from observations
- `memory.getDesignDoc` / `memory.updateDesignDoc` - Read and maintain a living design document that captures the "soul" and current truth of the game

This layer gives the AI persistent, project-specific memory and taste that compounds across sessions.

### The Lab: Autonomous Experimentation & Optimization

Built on top of memory + observation, the Lab lets the AI run rigorous, data-driven experiments at scale:

- Define hypotheses and parameter spaces tied to your living design goals
- Automatically execute dozens of instrumented play sessions using the existing runtime observation and input simulation systems
- Measure results quantitatively and visually
- Evolve mechanics toward what actually feels good for *this game*
- Apply winning variants with full audit trail back into memory

This is the closest thing that currently exists to giving an AI a real game design research lab inside Unity.

## 💡 Usage Examples

### Natural Language Commands

Ask your AI IDE:

```
"Analyze my Unity project and show me the structure"
→ Executes: project.analyze

"Create a player character with movement controls in the scene"
→ Executes: scene.createGameObject, code.createScript, code.attachScripts

"Build my project for Android with development settings"
→ Executes: build.configure, build.setBuildTarget, build.execute

"Find all scripts that use the PlayerController component"
→ Executes: code.findReferences

"Create a fire particle system with appropriate materials"
→ Executes: scene.createGameObject, assets.createMaterial, assets.managePrefabs
```

### Autonomous Build→Test→Fix Loop + Accumulated Intelligence

The AI can now build features, play-test them, and self-correct without human intervention. More importantly, it can *remember* what worked, what didn't, and why — across many sessions.

After a successful tuning pass using `playmode.observe`, the agent should call `memory.recordInsight` with the measured results and rationale. Over time this creates a living project model that makes every subsequent interaction dramatically more grounded and effective.

```
"Create a WASD player controller, enter play mode, verify the player moves at 5 units/sec"
→ AI creates script → attaches to Player → enters Play Mode
→ Executes: playmode.observe with simulateInput key="w"
→ AI reads time-series: position delta over 2s = (0, 0, 10.1) → speed ≈ 5.05 u/s ✓

"The jump feels too floaty — make it snappier"
→ AI increases gravity scale → enters Play Mode
→ Executes: playmode.observe on Rigidbody.velocity with simulateInput key="space"
→ AI reads velocity curve: apex reached in 0.3s, descent in 0.4s → adjusts until symmetric

"Test that enemies patrol between waypoints correctly"
→ Executes: playmode.observe on Enemy/Transform.position for 5s at 10Hz
→ AI reads position time-series: enemy oscillates between (0,0,0) and (10,0,0) ✓
```

### The Memory Flywheel (Long-term Intelligence)

This is the highest-leverage capability:

1. Agent explores and tunes using `playmode.observe` + `simulateInput`
2. After every meaningful session, it calls `memory.recordInsight` with the actual measured results
3. Later (next day, next week, new team member), the agent calls `memory.query` or `memory.suggest`
4. The suggestions are now grounded in the real history and design intent of *this specific game*

Example flow an advanced agent will follow:
```
memory.status
memory.rebuild
memory.query "What have we learned about movement feel so far?"
memory.suggest "Make the coyote time feel more generous without breaking precision"
... implement ...
playmode.observe + simulateInput
memory.recordInsight "Coyote time of 0.18s produced the best player feedback in testing"
memory.updateDesignDoc (with new known-good values)
```

Over time this creates a project that genuinely gets better at being worked on by AI.

### Direct Tool Testing

You can test tools directly using the Python client:

```bash
# Analyze project structure
python3 unity_mcp_client.py project.analyze

# Get project info
python3 unity_mcp_client.py project.getInfo

# Create a cube in the scene
python3 unity_mcp_client.py scene.createGameObject '{"name": "TestCube", "primitive": "Cube"}'

# List all scenes
python3 unity_mcp_client.py project.listScenes

# Get build settings
python3 unity_mcp_client.py project.getBuildSettings
```

## 🔧 Configuration

### Unity Configuration

1. **Server Settings**: 
   - Port: 8090 (default, configurable)
   - Timeout: 10 seconds (configurable)
   - Auto-start: Optional for convenience

2. **Tool Categories**:
   - All tools are auto-registered on Unity Editor startup
   - Custom tools can be added by extending `McpToolBase`

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `UNITY_PORT` | Unity Editor HTTP server port | `8090` |
| `NODE_ENV` | Node.js environment mode | `production` |
| `REQUEST_TIMEOUT` | Tool execution timeout (seconds) | `10` |
| `LOG_LEVEL` | Logging verbosity | `info` |

## 🏃‍♂️ Development

### Adding Custom Tools

1. **Create Unity Tool** (C#):
   ```csharp
   public class MyCustomTool : McpToolBase
   {
       public override string ToolName => "custom_mytool";
       public override string Description => "My custom Unity tool";
       public override string Category => "custom";
       
       public override object Execute(object parameters)
       {
           // Your Unity API calls here
           return CreateSuccessResponse(result);
       }
   }
   ```

2. **Register in Unity Server**:
   ```csharp
   // In McpUnityServer.InitializeTools()
   RegisterTool(new MyCustomTool());
   ```

3. **Add Node.js Tool** (TypeScript):
   ```typescript
   // In Server/src/tools/custom-tools.ts
   export const myCustomTool: Tool = {
     name: 'custom.mytool',
     description: 'My custom Unity tool',
     inputSchema: { /* JSON schema */ },
     handler: async (params) => {
       return await unityClient.sendRequest('custom.mytool', params);
     }
   };
   ```

### Building and Testing

```bash
# Build Node.js server
cd Server
npm run build

# Run tests
npm test

# Start development server with hot reload
npm run dev

# Test specific tool
python3 unity_mcp_client.py your.tool '{"param": "value"}'
```

## 🐛 Troubleshooting

### Common Issues

1. **"Connection refused"**:
   - Ensure Unity Editor is running
   - Check Unity MCP Server window shows "Running"
   - Verify port 8090 is not blocked

2. **"Tool execution timed out"**:
   - Unity Editor may be busy or not responding
   - Increase timeout in Unity MCP Server settings
   - Check Unity Console for errors

3. **"Tool not found"**:
   - Verify tool is registered in Unity C# server
   - Check Node.js server logs for tool registration
   - Ensure tool name format matches (category.action)

4. **MCP not appearing in your IDE**:
   - Restart your AI IDE after configuration changes
   - Check MCP configuration file syntax (see [CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md))
   - Verify Node.js server builds without errors

### Diagnostic Commands

```bash
# Test Unity connection
curl -X POST http://localhost:8090 -H "Content-Type: application/json" -d '{"id":"test","type":"request","method":"project.getInfo","params":{}}'

# Check Node.js server status
node Server/build/index.js --test

# Validate MCP configuration (location is client-specific; see CLIENT_CONFIGS.md)
```

## 📁 Project Structure

```
Unity_MCP/
├── Server/                    # Node.js MCP Server
│   ├── src/                   # TypeScript source code
│   │   ├── tools/            # Tool implementations
│   │   ├── unity-client.ts   # Unity communication client
│   │   └── index.ts          # Main server entry point
│   ├── build/                # Compiled JavaScript
│   └── package.json          # Node.js dependencies
├── Unity/                     # Unity C# Plugin
│   ├── Editor/               # Unity Editor scripts
│   │   ├── Tools/           # Individual MCP tools
│   │   └── McpUnityServer.cs # Main Unity HTTP server
│   └── package.json          # Unity package definition
├── unity_mcp_client.py       # Python test client
├── mcp.json                  # Local MCP configuration
└── README.md                 # This documentation
```

## 🤝 Contributing

Contributions are welcome from everyone!

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-tool`)
3. Add your tool following the established patterns
4. Write tests and documentation
5. Submit a pull request

### Tool Development Guidelines

- Follow Unity C# coding conventions
- Use TypeScript for Node.js components
- Include comprehensive error handling
- Add parameter validation and helpful error messages
- Document all public APIs and tool parameters
- Test tools with various Unity project configurations

## 📄 License

MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Unity Technologies for the comprehensive Editor API
- Anthropic for the Model Context Protocol (MCP) specification
- MCP client developers and the Model Context Protocol community for the integration standard
- Open source contributors, beta testers, and the Unity and AI development community

## 📞 Support

- 🐛 **Issues**: [GitHub Issues](https://github.com/isekream/Unity_MCP/issues)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/isekream/Unity_MCP/discussions)
- 📧 **Email**: support@unitymcp.com (or use GitHub issues for faster community support)
- 📖 **Docs**: [Wiki](https://github.com/isekream/Unity_MCP/wiki)

---

**Made with ❤️ for the Unity and AI development community** 