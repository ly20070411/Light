#!/bin/bash

# Unity MCP Installation Script
# Open source under MIT License. See LICENSE file.

echo "🚀 Installing Unity MCP..."
echo ""

# Check requirements
echo "📋 Checking requirements..."

# Check Node.js
if ! command -v node &> /dev/null; then
    echo "❌ Node.js not found. Please install Node.js 18+ first."
    echo "   Download from: https://nodejs.org/"
    exit 1
fi

NODE_VERSION=$(node -v | cut -d'v' -f2 | cut -d'.' -f1)
if [ "$NODE_VERSION" -lt "18" ]; then
    echo "❌ Node.js version 18+ required. Current: $(node -v)"
    echo "   Please update Node.js from: https://nodejs.org/"
    exit 1
fi

echo "✅ Node.js $(node -v) found"

# Check npm
if ! command -v npm &> /dev/null; then
    echo "❌ npm not found. Please install npm."
    exit 1
fi

echo "✅ npm $(npm -v) found"

# Build and install the MCP server
echo ""
echo "📦 Building MCP server..."
cd Server
npm install
npm run build

echo ""
echo "🌍 Installing globally..."
npm link

cd ..

# Verify installation
echo ""
echo "🔍 Verifying installation..."
if command -v unity-mcp &> /dev/null; then
    echo "✅ unity-mcp command available at: $(which unity-mcp)"
else
    echo "⚠️  Global command not found in PATH"
    echo "   You may need to add npm global bin to your PATH:"
    echo "   export PATH=\"$(npm config get prefix)/bin:\$PATH\""
fi

# Note: MCP client configuration is client-specific.
# After installation, add the unity-mcp server entry to your MCP client's configuration file.
# See README.md and CLIENT_CONFIGS.md for examples.
echo ""
echo "📝 MCP client configuration is not modified automatically."
echo "   You will need to add the Unity MCP server to your AI client's MCP settings manually."

echo ""
echo "🎉 Installation complete!"
echo ""
echo "📋 Next steps:"
echo "1. Open Unity Editor with any project"
echo "2. Install Unity package via Package Manager:"
echo "   Window > Package Manager > + > Add package from git URL"
echo "   https://github.com/isekream/Unity_MCP.git?path=/Unity"
echo "3. Start Unity MCP server: Tools > Unity MCP > Server Window"
echo "4. Configure your MCP client to use the Unity MCP server (see README.md)"
echo "5. Restart your AI client / IDE"
echo ""
echo "📖 For detailed instructions, see:"
echo "   - README.md for complete setup guide"
echo "   - QUICK_UNITY_SETUP.md for per-project setup"
echo "   - SECURITY_AND_PRIVACY.md for security guidelines"
echo ""
echo "🌍 The MCP will now work with ALL your Unity projects!" 