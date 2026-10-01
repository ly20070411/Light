# Unity MCP Client Configuration Guide

This guide provides configuration patterns for connecting the Unity MCP Server to any MCP-compatible AI client.

## Generic Configuration Shape

Most MCP clients accept a JSON file (often called `mcp.json` or similar) with this structure:

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

**Key points**:
- Replace the `args` path with the absolute path to your cloned `UnityMCP/Server/build/index.js`.
- The `env` block is optional but recommended for controlling port and logging.
- After changing the config file, restart your AI client completely.

## Common Client Patterns (Examples Only)

Exact file locations and UI flows vary by client and version. Always check the client's own documentation or settings UI.

- Many desktop / CLI clients look for `mcp.json` in their config directory (`~/.config/<client>/` or Application Support folders).
- VS Code-based extensions often expose an "MCP Servers" section in settings or use a `globalStorage` path.
- Some clients support adding servers directly through a visual settings panel instead of editing JSON.

When in doubt, search the client's settings for "MCP" or "Model Context Protocol".

## Environment Variables

These can be set inside the `env` block of the server config or in your shell:

| Variable | Description | Default |
|----------|-------------|---------|
| `UNITY_PORT` | Port the Unity Editor HTTP server listens on | `8090` |
| `REQUEST_TIMEOUT` | Tool execution timeout in seconds | `10` |
| `LOG_LEVEL` | Logging verbosity (`debug`, `info`, `warn`, `error`) | `info` |

## Troubleshooting

- **Server not appearing**: Restart the AI client after any config change. Verify the Node server builds without errors (`cd Server && npm run build`).
- **Connection refused**: Confirm the Unity MCP Server window inside Unity shows "Running" at the expected port.
- **Tool not found**: Ensure the Unity Editor window is open and the server is started before the AI client connects.

For the most up-to-date client-specific instructions, refer to the documentation of your chosen MCP client.
