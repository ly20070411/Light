# Basic Usage Examples

Examples of using Unity MCP with any MCP-compatible AI client for common Unity tasks.

## Project Analysis

"Ask your AI client: Analyze my Unity project and show the structure, scenes, and packages."

## Scene Creation

"Create a player character with a CharacterController and a simple movement script."

The server will use a combination of `scene.createGameObject`, `code.createScript`, and `code.attachScripts`.

## Play Mode Observation

"Enter play mode, simulate WASD input for 3 seconds, then report the average movement speed from the time-series data."

This exercises `playmode.enter`, `playmode.simulateInput`, and especially `playmode.observe`.

See TOOLS_REFERENCE.md for the full list of available tools and parameters.
