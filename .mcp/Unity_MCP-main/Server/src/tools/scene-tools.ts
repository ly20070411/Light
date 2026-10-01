import type { Tool } from '../types/index.js';
import type { UnityClient } from '../unity-client.js';

export function createSceneTools(unityClient: UnityClient): Tool[] {
  return [
    {
      name: 'scene.createGameObject',
      description: 'Create a new GameObject in the current scene with specified name, position, rotation, and components.',
      inputSchema: {
        type: 'object',
        properties: {
          name: {
            type: 'string',
            description: 'Name of the GameObject'
          },
          parent: {
            type: 'string',
            description: 'Name or instance ID of parent GameObject'
          },
          position: {
            type: 'object',
            properties: {
              x: { type: 'number' },
              y: { type: 'number' },
              z: { type: 'number' }
            },
            description: 'World position of the GameObject'
          },
          rotation: {
            type: 'object',
            properties: {
              x: { type: 'number' },
              y: { type: 'number' },
              z: { type: 'number' }
            },
            description: 'Euler rotation of the GameObject'
          },
          scale: {
            type: 'object',
            properties: {
              x: { type: 'number', default: 1 },
              y: { type: 'number', default: 1 },
              z: { type: 'number', default: 1 }
            },
            description: 'Scale of the GameObject'
          },
          components: {
            type: 'array',
            items: { type: 'string' },
            description: 'List of component types to add'
          },
          tag: {
            type: 'string',
            description: 'Tag to assign to the GameObject'
          },
          layer: {
            type: 'number',
            description: 'Layer to assign to the GameObject'
          }
        },
        required: ['name'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('scene.createGameObject', args);
        return result;
      }
    },

    {
      name: 'scene.modifyComponent',
      description: 'Add, remove, or modify components on GameObjects in the scene.',
      inputSchema: {
        type: 'object',
        properties: {
          gameObjectName: {
            type: 'string',
            description: 'Name of the GameObject to modify'
          },
          instanceId: {
            type: 'number',
            description: 'Instance ID of the GameObject (alternative to name)'
          },
          componentType: {
            type: 'string',
            description: 'Type of component to add/modify (e.g., "Transform", "Rigidbody")'
          },
          action: {
            type: 'string',
            enum: ['add', 'remove', 'modify'],
            description: 'Action to perform on the component'
          },
          properties: {
            type: 'object',
            description: 'Properties to set on the component (for add/modify actions)',
            additionalProperties: true
          }
        },
        required: ['componentType', 'action'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('scene.modifyComponent', args);
        return result;
      }
    },

    {
      name: 'scene.query',
      description: 'Query the current scene hierarchy and get information about GameObjects and their components.',
      inputSchema: {
        type: 'object',
        properties: {
          filter: {
            type: 'string',
            description: 'Filter GameObjects by name pattern'
          },
          includeInactive: {
            type: 'boolean',
            description: 'Whether to include inactive GameObjects',
            default: false
          },
          maxDepth: {
            type: 'number',
            description: 'Maximum hierarchy depth to query',
            default: -1
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('scene.query', args);
        return result;
      }
    },

    {
      name: 'scene.selectObjects',
      description: 'Select GameObjects in the Unity Editor scene view.',
      inputSchema: {
        type: 'object',
        properties: {
          objectNames: {
            type: 'array',
            items: { type: 'string' },
            description: 'Names of GameObjects to select'
          },
          instanceIds: {
            type: 'array',
            items: { type: 'number' },
            description: 'Instance IDs of GameObjects to select'
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('scene.selectObjects', args);
        return result;
      }
    },

    {
      name: 'scene.deleteGameObject',
      description: 'Delete GameObjects from the current scene.',
      inputSchema: {
        type: 'object',
        properties: {
          gameObjectName: {
            type: 'string',
            description: 'Name of the GameObject to delete'
          },
          gameObjectId: {
            type: 'number',
            description: 'Instance ID of the GameObject to delete'
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('scene.deleteGameObject', args);
        return result;
      }
    },

    {
      name: 'scene.moveGameObject',
      description: 'Move a GameObject to a new position, rotation, or scale.',
      inputSchema: {
        type: 'object',
        properties: {
          gameObjectName: {
            type: 'string',
            description: 'Name of the GameObject to move'
          },
          gameObjectId: {
            type: 'number',
            description: 'Instance ID of the GameObject to move'
          },
          position: {
            type: 'object',
            properties: {
              x: { type: 'number' },
              y: { type: 'number' },
              z: { type: 'number' }
            },
            description: 'New world position'
          },
          rotation: {
            type: 'object',
            properties: {
              x: { type: 'number' },
              y: { type: 'number' },
              z: { type: 'number' }
            },
            description: 'New Euler rotation'
          },
          scale: {
            type: 'object',
            properties: {
              x: { type: 'number' },
              y: { type: 'number' },
              z: { type: 'number' }
            },
            description: 'New scale'
          },
          relative: {
            type: 'boolean',
            description: 'Whether the values are relative to current transform',
            default: false
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('scene.moveGameObject', args);
        return result;
      }
    },

    {
      name: 'scene.save',
      description: 'Save the current scene to disk.',
      inputSchema: {
        type: 'object',
        properties: {
          path: {
            type: 'string',
            description: 'Path to save the scene (relative to Assets folder)'
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('scene.save', args);
        return result;
      }
    },

    {
      name: 'scene.load',
      description: 'Load a scene in the Unity Editor.',
      inputSchema: {
        type: 'object',
        properties: {
          scenePath: {
            type: 'string',
            description: 'Path to the scene file'
          },
          additive: {
            type: 'boolean',
            description: 'Whether to load additively or replace current scene',
            default: false
          }
        },
        required: ['scenePath'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('scene.load', args);
        return result;
      }
    }
  ];
} 