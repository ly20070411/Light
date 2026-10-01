# Unity MCP Setup Guide

This guide helps you set up Unity MCP for use with any MCP-compatible AI client.

**Free and open source** under the MIT License. No purchase required.

## Prerequisites

- Unity Editor 2022.3 LTS or later
- Node.js 18+ and npm
- An MCP-compatible AI client
- Git

## Installation Steps

1. Clone this repository and build the server:

   ```bash
   git clone https://github.com/isekream/Unity_MCP.git
   cd Unity_MCP/Server
   npm install
   npm run build
   cd ..
   ```

2. Install the Unity package in your project via Package Manager → "Add package from git URL" using:

   `https://github.com/isekream/Unity_MCP.git?path=/Unity`

3. In Unity: **Tools > Unity MCP > Server Window**, then click **Start Server**. Confirm "MCP Server started on port 8090".

4. Add the server to your AI client's MCP configuration (see CLIENT_CONFIGS.md).

5. Restart your AI client.

For the fastest path, see [QUICK_SETUP.md](../QUICK_SETUP.md).
