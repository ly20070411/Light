# Quick Setup Guide

**Free and open source** under the MIT License. No paid license required.

Get Unity MCP running with any MCP-compatible client in under 10 minutes.

## 1. Install the Server (Node.js)

```bash
cd UnityMCP/Server
npm install
npm run build
```

## 2. Configure Your MCP Client

Add a server entry to your client's MCP configuration file. The exact file location is client-specific (common patterns are in CLIENT_CONFIGS.md).

Minimal example:

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

After editing, **fully restart** your AI client.

## 3. Install the Unity Package

1. Open your Unity project (2022.3+)
2. Window → Package Manager
3. Click + → "Add package from git URL"
4. Paste: `https://github.com/isekream/Unity_MCP.git?path=/Unity`

## 4. Start the Unity Server

1. In Unity: **Tools → Unity MCP → Server Window**
2. Click **Start Server**
3. Confirm the log shows "MCP Server started on port 8090" and status "Running" at http://localhost:8090

## 5. Verify

Ask your AI client something like:
> "List the Unity MCP tools that are available"

You should see 40+ tools related to project, scene, assets, code, build, and playmode.

## Next Steps

- Read the full [README.md](README.md)
- See [TOOLS_REFERENCE.md](TOOLS_REFERENCE.md) for all available tools
- See [INTEGRATION_GUIDE.md](INTEGRATION_GUIDE.md) for advanced usage
