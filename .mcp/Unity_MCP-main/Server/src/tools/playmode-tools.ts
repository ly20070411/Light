import type { Tool } from '../types/index.js';
import type { UnityClient } from '../unity-client.js';

export function createPlayModeTools(unityClient: UnityClient): Tool[] {
  return [
    {
      name: 'playmode.getState',
      description:
        'Get the current Unity Play Mode state (stopped, playing, paused). Use this to check whether the game is running before issuing play mode commands.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.getState', args);
        return result;
      },
    },

    {
      name: 'playmode.enter',
      description:
        'Enter Unity Play Mode to run the game. After entering, use scene.capture to visually verify what the running game looks like, and playmode.getConsoleLogs to check for runtime errors. This enables a full code→test→see→fix development loop.',
      inputSchema: {
        type: 'object',
        properties: {
          maximizeGameView: {
            type: 'boolean',
            description: 'Whether to maximize the Game view when entering Play Mode',
            default: false,
          },
        },
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.enter', args);
        return result;
      },
    },

    {
      name: 'playmode.exit',
      description:
        'Exit Unity Play Mode and return to Edit Mode. Always exit Play Mode before making code or scene changes.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.exit', args);
        return result;
      },
    },

    {
      name: 'playmode.pause',
      description:
        'Pause the running game. The game freezes in its current state, allowing inspection of runtime values and viewport capture of a specific moment.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.pause', args);
        return result;
      },
    },

    {
      name: 'playmode.resume',
      description: 'Resume a paused game.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.resume', args);
        return result;
      },
    },

    {
      name: 'playmode.step',
      description:
        'Advance the paused game by exactly one frame. Useful for debugging frame-by-frame behavior, physics issues, or animation transitions.',
      inputSchema: {
        type: 'object',
        properties: {
          frames: {
            type: 'number',
            description: 'Number of frames to step (default 1). The game pauses after stepping.',
            default: 1,
          },
        },
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.step', args);
        return result;
      },
    },

    {
      name: 'playmode.inspectGameObject',
      description:
        'Inspect a GameObject\'s runtime state during Play Mode. Returns live component values (transforms, physics velocities, custom script fields) — not just the serialized editor values. Essential for debugging runtime behavior.',
      inputSchema: {
        type: 'object',
        properties: {
          gameObjectName: {
            type: 'string',
            description: 'Name of the GameObject to inspect',
          },
          instanceId: {
            type: 'number',
            description: 'Instance ID of the GameObject (alternative to name)',
          },
          componentFilter: {
            type: 'array',
            items: { type: 'string' },
            description:
              'Only return these component types (e.g., ["Transform", "Rigidbody", "PlayerController"]). If empty, returns all components.',
          },
          includeChildren: {
            type: 'boolean',
            description: 'Whether to include child GameObjects in the inspection',
            default: false,
          },
        },
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.inspectGameObject', args);
        return result;
      },
    },

    {
      name: 'playmode.setProperty',
      description:
        'Modify a component property on a GameObject at runtime during Play Mode. Changes are temporary and reset when exiting Play Mode. Useful for live-tuning values like speed, health, or positions.',
      inputSchema: {
        type: 'object',
        properties: {
          gameObjectName: {
            type: 'string',
            description: 'Name of the GameObject',
          },
          instanceId: {
            type: 'number',
            description: 'Instance ID of the GameObject (alternative to name)',
          },
          componentType: {
            type: 'string',
            description: 'Type of the component to modify (e.g., "Transform", "Rigidbody", "PlayerController")',
          },
          propertyName: {
            type: 'string',
            description: 'Name of the property or field to set (e.g., "position", "velocity", "speed")',
          },
          value: {
            description:
              'New value for the property. Can be a number, string, boolean, or object (e.g., {"x": 0, "y": 5, "z": 0} for Vector3).',
          },
        },
        required: ['componentType', 'propertyName', 'value'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.setProperty', args);
        return result;
      },
    },

    {
      name: 'playmode.invokeMethod',
      description:
        'Call a public method on a component during Play Mode. Useful for triggering game actions like TakeDamage(), Respawn(), or LoadLevel() to test specific behaviors.',
      inputSchema: {
        type: 'object',
        properties: {
          gameObjectName: {
            type: 'string',
            description: 'Name of the GameObject',
          },
          instanceId: {
            type: 'number',
            description: 'Instance ID of the GameObject (alternative to name)',
          },
          componentType: {
            type: 'string',
            description: 'Component type containing the method',
          },
          methodName: {
            type: 'string',
            description: 'Name of the public method to invoke',
          },
          arguments: {
            type: 'array',
            description: 'Arguments to pass to the method (in order)',
            items: {},
          },
        },
        required: ['componentType', 'methodName'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.invokeMethod', args);
        return result;
      },
    },

    {
      name: 'playmode.getConsoleLogs',
      description:
        'Get Unity console logs captured during Play Mode, including runtime errors, exceptions, warnings, and Debug.Log messages. Essential for diagnosing runtime issues after entering Play Mode.',
      inputSchema: {
        type: 'object',
        properties: {
          logTypes: {
            type: 'array',
            items: {
              type: 'string',
              enum: ['Log', 'Warning', 'Error', 'Assert', 'Exception'],
            },
            description: 'Types of logs to retrieve',
            default: ['Log', 'Warning', 'Error', 'Exception'],
          },
          limit: {
            type: 'number',
            description: 'Maximum number of log entries to return',
            default: 50,
          },
          sinceLastCall: {
            type: 'boolean',
            description: 'Only return logs since the last call to this tool (useful for incremental monitoring)',
            default: false,
          },
          search: {
            type: 'string',
            description: 'Filter logs containing this text (case-insensitive)',
          },
        },
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.getConsoleLogs', args);
        return result;
      },
    },

    {
      name: 'playmode.getRuntimeInfo',
      description:
        'Get runtime performance and game state information during Play Mode: FPS, frame time, active object count, physics stats, memory usage, and time info (elapsed, timeScale, frame count).',
      inputSchema: {
        type: 'object',
        properties: {
          includePerformance: {
            type: 'boolean',
            description: 'Include FPS, frame time, and memory stats',
            default: true,
          },
          includePhysics: {
            type: 'boolean',
            description: 'Include physics simulation info (contacts, rigidbody count)',
            default: true,
          },
          includeTime: {
            type: 'boolean',
            description: 'Include time info (elapsed, timeScale, frame count)',
            default: true,
          },
        },
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.getRuntimeInfo', args);
        return result;
      },
    },

    {
      name: 'playmode.observe',
      description:
        'Record a time-series of runtime property values across multiple frames during Play Mode. This is the AI\'s "debugger watch window" — instead of a single snapshot, it captures how values change over time. Use this to verify that gameplay mechanics work correctly (e.g., player moves at expected speed, projectile follows correct arc, health decreases on hit). Optionally simulate input during observation to test input-driven behavior end-to-end.',
      inputSchema: {
        type: 'object',
        properties: {
          targets: {
            type: 'array',
            description:
              'Properties to observe. Each target specifies a GameObject and the component property to track.',
            items: {
              type: 'object',
              properties: {
                gameObjectName: {
                  type: 'string',
                  description: 'Name of the GameObject to observe',
                },
                instanceId: {
                  type: 'number',
                  description: 'Instance ID of the GameObject (alternative to name)',
                },
                componentType: {
                  type: 'string',
                  description:
                    'Component type containing the property (e.g., "Transform", "Rigidbody", "PlayerController")',
                },
                propertyName: {
                  type: 'string',
                  description:
                    'Property or field name to observe (e.g., "position", "velocity", "health"). For nested values like Vector3, the full vector is recorded.',
                },
              },
              required: ['componentType', 'propertyName'],
            },
            minItems: 1,
          },
          duration: {
            type: 'number',
            description:
              'How long to observe in seconds (default 2.0, max 8.0). Must be less than the server request timeout.',
            default: 2.0,
          },
          sampleRate: {
            type: 'number',
            description:
              'Samples per second (default 10, max 30). Higher rates capture faster changes but produce more data.',
            default: 10,
          },
          simulateInput: {
            type: 'object',
            description:
              'Optional: simulate player input during observation. The key is pressed when observation starts and released after holdDuration.',
            properties: {
              key: {
                type: 'string',
                description:
                  'Key to simulate (e.g., "w", "space", "left shift", "mouse 0"). Uses Unity KeyCode names.',
              },
              holdDuration: {
                type: 'number',
                description: 'How long to hold the key in seconds (default: same as observation duration)',
              },
            },
            required: ['key'],
          },
        },
        required: ['targets'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const duration = (args.duration as number) ?? 2.0;
        const timeoutMs = (duration + 5) * 1000;
        const result = await unityClient.sendRequest('playmode.observe', args, timeoutMs);
        return result;
      },
    },

    {
      name: 'playmode.simulateInput',
      description:
        'Send simulated input to the running game during Play Mode. Supports keyboard keys and mouse buttons. Use this to trigger player actions (jump, move, shoot) without a human at the keyboard. Works with both legacy Input and the new Input System.',
      inputSchema: {
        type: 'object',
        properties: {
          key: {
            type: 'string',
            description:
              'Key to simulate (e.g., "w", "a", "s", "d", "space", "left shift", "mouse 0", "mouse 1"). Uses Unity KeyCode names.',
          },
          action: {
            type: 'string',
            enum: ['press', 'release', 'tap'],
            description:
              'Input action: "press" holds the key down, "release" releases it, "tap" presses and releases after holdDuration (default).',
            default: 'tap',
          },
          holdDuration: {
            type: 'number',
            description:
              'For "tap" action: how long to hold the key in seconds before releasing (default 0.1).',
            default: 0.1,
          },
        },
        required: ['key'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const holdDuration = (args.holdDuration as number) ?? 0.1;
        const timeoutMs = (holdDuration + 5) * 1000;
        const result = await unityClient.sendRequest('playmode.simulateInput', args, timeoutMs);
        return result;
      },
    },

    {
      name: 'playmode.setTimeScale',
      description:
        'Set the game time scale during Play Mode. Use 0 to freeze time (different from pause — scripts still run), 0.5 for slow-motion, 1 for normal, 2+ for fast-forward. Great for testing animations or debugging timing issues.',
      inputSchema: {
        type: 'object',
        properties: {
          timeScale: {
            type: 'number',
            description: 'Time scale value (0 = frozen, 1 = normal, 2 = double speed)',
          },
        },
        required: ['timeScale'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('playmode.setTimeScale', args);
        return result;
      },
    },
  ];
}
