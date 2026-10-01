# Quick Unity MCP Setup for New Projects

**Free and open source** under the MIT License. No paid license or credentials required.

## ✅ Global MCP is Now Configured!

Your Unity MCP is globally configured and will work with **ALL Unity projects**.

## 🚀 For Any New Unity Project (2-Minute Setup):

### 1. Open Unity Editor with your project

### 2. Install the Unity MCP Package
- Go to `Window > Package Manager`
- Click the `+` button → `Add package from git URL...`
- Paste this URL:
  ```
  https://github.com/isekream/Unity_MCP.git?path=/Unity
  (if the repo requires auth for any reason, use a GitHub token or public access)
  ```
- Click `Add`

### 3. Start the Unity MCP Server
- In Unity go to **Tools > Unity MCP > Server Window**
- In the window that opens, click the **Start Server** button
- Look for log: "MCP Server started on port 8090"
- It should show status "Running" at http://localhost:8090

### 4. Restart Your AI IDE
- Fully restart your AI client after configuration changes
- The Unity MCP tools will now be available

## 🎉 That's It!

You can now use your AI IDE with natural language commands like:
- "List all scenes in the project"
- "Create a new cube in the current scene"
- "Build the project for Windows"
- "Show me the project structure"
- "Create a player controller script"

## 🔧 Global Configuration Details

**MCP Server Location**: `<PROJECT_FOLDER>/Server/build/index.js`
**Configuration Files**: See [CLIENT_CONFIGS.md](./CLIENT_CONFIGS.md) for your specific IDE

## 💡 Pro Tips

1. **Keep this folder**: Don't move or delete the `Unity_MCP` folder - the global config points to it
2. **Auto-start Unity server**: Enable "Auto Start Server" in the Unity MCP Server Window for convenience
3. **Check Unity Console**: If something doesn't work, check Unity Console for error messages
4. **Port conflicts**: If port 8090 is busy, change it in the Unity Server Window

## 🐛 Troubleshooting

If the Unity MCP tools do not appear in your client:
1. Verify Unity MCP server is running (green status)
2. Fully restart your AI client / IDE
3. Check your client's MCP config file (location varies) exists and contains the correct absolute path to the built server
4. Run diagnostic: `node <PROJECT_FOLDER>/test-connection.js` 