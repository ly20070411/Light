import type { Tool } from '../types/index.js';
import type { UnityClient } from '../unity-client.js';

export function createProjectTools(unityClient: UnityClient): Tool[] {
  return [
    {
      name: 'project.analyze',
      description: 'Analyze Unity project structure and return comprehensive information about the project, including scenes, assets, packages, and settings.',
      inputSchema: {
        type: 'object',
        properties: {
          includeAssets: {
            type: 'boolean',
            description: 'Whether to include detailed asset information',
            default: false
          },
          includePackages: {
            type: 'boolean', 
            description: 'Whether to include package information',
            default: true
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const { includeAssets = false, includePackages = true } = args;
        
        const result = await unityClient.sendRequest('project.analyze', {
          includeAssets,
          includePackages
        });
        
        return result;
      }
    },

    {
      name: 'project.getInfo',
      description: 'Get basic information about the Unity project including name, version, platform, and loaded scenes.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false
      },
      async execute() {
        const result = await unityClient.sendRequest('project.getInfo');
        return result;
      }
    },

    {
      name: 'project.setSettings',
      description: 'Modify Unity project settings such as company name, product name, version, or other player settings.',
      inputSchema: {
        type: 'object',
        properties: {
          companyName: {
            type: 'string',
            description: 'Company name in player settings'
          },
          productName: {
            type: 'string',
            description: 'Product name in player settings'
          },
          version: {
            type: 'string',
            description: 'Application version'
          },
          bundleVersion: {
            type: 'string',
            description: 'Bundle version for mobile platforms'
          },
          settings: {
            type: 'object',
            description: 'Additional player settings as key-value pairs',
            additionalProperties: true
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('project.setSettings', args);
        return result;
      }
    },

    {
      name: 'project.listScenes',
      description: 'List all scenes in the project, including their build settings and current load status.',
      inputSchema: {
        type: 'object',
        properties: {
          includeDisabled: {
            type: 'boolean',
            description: 'Whether to include scenes not in build settings',
            default: false
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const { includeDisabled = false } = args;
        
        const result = await unityClient.sendRequest('project.listScenes', {
          includeDisabled
        });
        
        return result;
      }
    },

    {
      name: 'project.getBuildSettings',
      description: 'Get current build settings including target platform, scenes in build, and player settings.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false
      },
      async execute() {
        const result = await unityClient.sendRequest('project.getBuildSettings');
        return result;
      }
    },

    {
      name: 'project.setBuildTarget',
      description: 'Change the active build target platform for the project.',
      inputSchema: {
        type: 'object',
        properties: {
          target: {
            type: 'string',
            enum: ['StandaloneWindows64', 'StandaloneOSX', 'StandaloneLinux64', 'iOS', 'Android', 'WebGL', 'WSAPlayer'],
            description: 'Target platform for builds'
          }
        },
        required: ['target'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const { target } = args;
        
        const result = await unityClient.sendRequest('project.setBuildTarget', {
          target
        });
        
        return result;
      }
    },

    {
      name: 'project.refreshAssets',
      description: 'Refresh the Unity Asset Database to detect changes in project files.',
      inputSchema: {
        type: 'object',
        properties: {
          forceReimport: {
            type: 'boolean',
            description: 'Whether to force reimport all assets',
            default: false
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const { forceReimport = false } = args;
        
        const result = await unityClient.sendRequest('project.refreshAssets', {
          forceReimport
        });
        
        return result;
      }
    }
  ];
} 