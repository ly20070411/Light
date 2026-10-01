import type { Tool } from '../types/index.js';
import type { UnityClient } from '../unity-client.js';

export function createAssetTools(unityClient: UnityClient): Tool[] {
  return [
    {
      name: 'assets.import',
      description: 'Import assets into the Unity project from file paths or URLs.',
      inputSchema: {
        type: 'object',
        properties: {
          sourcePath: {
            type: 'string',
            description: 'Source path or URL of the asset to import'
          },
          targetPath: {
            type: 'string',
            description: 'Target path within the Assets folder'
          },
          importSettings: {
            type: 'object',
            properties: {
              textureType: { type: 'string', description: 'Texture import type' },
              wrapMode: { type: 'string', description: 'Texture wrap mode' },
              filterMode: { type: 'string', description: 'Texture filter mode' },
              maxSize: { type: 'number', description: 'Maximum texture size' },
              compression: { type: 'string', description: 'Compression format' }
            },
            description: 'Import settings for the asset',
            additionalProperties: true
          }
        },
        required: ['sourcePath'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('assets.import', args);
        return result;
      }
    },

    {
      name: 'assets.createMaterial',
      description: 'Create a new material with specified properties and shader.',
      inputSchema: {
        type: 'object',
        properties: {
          name: {
            type: 'string',
            description: 'Name of the material'
          },
          path: {
            type: 'string',
            description: 'Path to save the material (relative to Assets folder)'
          },
          shader: {
            type: 'string',
            description: 'Shader name (e.g., "Standard", "Unlit/Color")',
            default: 'Standard'
          },
          properties: {
            type: 'object',
            description: 'Material properties to set',
            properties: {
              color: {
                type: 'object',
                properties: {
                  r: { type: 'number', minimum: 0, maximum: 1 },
                  g: { type: 'number', minimum: 0, maximum: 1 },
                  b: { type: 'number', minimum: 0, maximum: 1 },
                  a: { type: 'number', minimum: 0, maximum: 1 }
                }
              },
              metallic: { type: 'number', minimum: 0, maximum: 1 },
              smoothness: { type: 'number', minimum: 0, maximum: 1 },
              emission: {
                type: 'object',
                properties: {
                  r: { type: 'number', minimum: 0 },
                  g: { type: 'number', minimum: 0 },
                  b: { type: 'number', minimum: 0 }
                }
              }
            }
          }
        },
        required: ['name'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('assets.createMaterial', args);
        return result;
      }
    },

    {
      name: 'assets.managePrefabs',
      description: 'Create, modify, or instantiate prefabs in the project.',
      inputSchema: {
        type: 'object',
        properties: {
          action: {
            type: 'string',
            enum: ['create', 'modify', 'instantiate', 'update'],
            description: 'Action to perform on prefabs'
          },
          prefabPath: {
            type: 'string',
            description: 'Path to the prefab asset'
          },
          gameObjectName: {
            type: 'string',
            description: 'Name of GameObject to create prefab from (for create action)'
          },
          instantiatePosition: {
            type: 'object',
            properties: {
              x: { type: 'number' },
              y: { type: 'number' },
              z: { type: 'number' }
            },
            description: 'Position to instantiate prefab at'
          },
          modifications: {
            type: 'object',
            description: 'Property modifications for the prefab',
            additionalProperties: true
          }
        },
        required: ['action'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('assets.managePrefabs', args);
        return result;
      }
    },

    {
      name: 'assets.organize',
      description: 'Organize and restructure assets in the project folders.',
      inputSchema: {
        type: 'object',
        properties: {
          action: {
            type: 'string',
            enum: ['move', 'copy', 'rename', 'delete', 'createFolder'],
            description: 'Organization action to perform'
          },
          sourcePath: {
            type: 'string',
            description: 'Source asset path'
          },
          targetPath: {
            type: 'string',
            description: 'Target path for move/copy operations'
          },
          newName: {
            type: 'string',
            description: 'New name for rename operations'
          },
          recursive: {
            type: 'boolean',
            description: 'Whether to perform action recursively',
            default: false
          }
        },
        required: ['action'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('assets.organize', args);
        return result;
      }
    },

    {
      name: 'assets.search',
      description: 'Search for assets in the project based on various criteria.',
      inputSchema: {
        type: 'object',
        properties: {
          query: {
            type: 'string',
            description: 'Search query for asset names'
          },
          type: {
            type: 'string',
            description: 'Asset type filter (e.g., "Texture2D", "Material", "Prefab")'
          },
          path: {
            type: 'string',
            description: 'Path filter to search within specific folders'
          },
          label: {
            type: 'string',
            description: 'Asset label filter'
          },
          maxResults: {
            type: 'number',
            description: 'Maximum number of results to return',
            default: 100
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('assets.search', args);
        return result;
      }
    },

    {
      name: 'assets.createTexture',
      description: 'Create a new texture asset with specified properties.',
      inputSchema: {
        type: 'object',
        properties: {
          name: {
            type: 'string',
            description: 'Name of the texture'
          },
          width: {
            type: 'number',
            description: 'Texture width in pixels'
          },
          height: {
            type: 'number',
            description: 'Texture height in pixels'
          },
          format: {
            type: 'string',
            enum: ['RGBA32', 'RGB24', 'ARGB32', 'Alpha8', 'R16', 'RGBAFloat'],
            description: 'Texture format',
            default: 'RGBA32'
          },
          color: {
            type: 'object',
            properties: {
              r: { type: 'number', minimum: 0, maximum: 1 },
              g: { type: 'number', minimum: 0, maximum: 1 },
              b: { type: 'number', minimum: 0, maximum: 1 },
              a: { type: 'number', minimum: 0, maximum: 1 }
            },
            description: 'Fill color for the texture',
            default: { r: 1, g: 1, b: 1, a: 1 }
          },
          savePath: {
            type: 'string',
            description: 'Path to save the texture (relative to Assets folder)'
          }
        },
        required: ['name', 'width', 'height'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('assets.createTexture', args);
        return result;
      }
    },

    {
      name: 'packages.manage',
      description: 'Manage Unity packages - install, update, or remove packages.',
      inputSchema: {
        type: 'object',
        properties: {
          action: {
            type: 'string',
            enum: ['install', 'update', 'remove', 'list'],
            description: 'Package management action'
          },
          packageName: {
            type: 'string',
            description: 'Name of the package (for install/update/remove)'
          },
          version: {
            type: 'string',
            description: 'Specific version to install (optional)'
          },
          source: {
            type: 'string',
            enum: ['registry', 'git', 'local'],
            description: 'Package source type',
            default: 'registry'
          },
          url: {
            type: 'string',
            description: 'Package URL for git or local sources'
          }
        },
        required: ['action'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('packages.manage', args);
        return result;
      }
    },

    {
      name: 'assets.getInfo',
      description: 'Get detailed information about a specific asset.',
      inputSchema: {
        type: 'object',
        properties: {
          assetPath: {
            type: 'string',
            description: 'Path to the asset (relative to Assets folder)'
          },
          guid: {
            type: 'string',
            description: 'GUID of the asset'
          },
          includeMetadata: {
            type: 'boolean',
            description: 'Whether to include asset metadata',
            default: true
          },
          includeDependencies: {
            type: 'boolean',
            description: 'Whether to include asset dependencies',
            default: false
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('assets.getInfo', args);
        return result;
      }
    }
  ];
} 