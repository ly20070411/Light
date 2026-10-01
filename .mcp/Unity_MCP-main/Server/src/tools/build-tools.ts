import type { Tool } from '../types/index.js';
import type { UnityClient } from '../unity-client.js';

export function createBuildTools(unityClient: UnityClient): Tool[] {
  return [
    {
      name: 'build.configure',
      description: 'Configure build settings for Unity project including platform, scenes, and player settings.',
      inputSchema: {
        type: 'object',
        properties: {
          target: {
            type: 'string',
            enum: ['StandaloneWindows64', 'StandaloneOSX', 'StandaloneLinux64', 'iOS', 'Android', 'WebGL', 'WSAPlayer'],
            description: 'Target build platform'
          },
          scenes: {
            type: 'array',
            items: { type: 'string' },
            description: 'List of scene paths to include in build'
          },
          buildPath: {
            type: 'string',
            description: 'Output path for the build'
          },
          development: {
            type: 'boolean',
            description: 'Whether to create a development build',
            default: false
          },
          scriptDebugging: {
            type: 'boolean',
            description: 'Enable script debugging in development builds',
            default: false
          },
          compressionType: {
            type: 'string',
            enum: ['None', 'Lz4', 'Lz4HC'],
            description: 'Asset bundle compression type',
            default: 'Lz4'
          },
          playerSettings: {
            type: 'object',
            properties: {
              companyName: { type: 'string' },
              productName: { type: 'string' },
              version: { type: 'string' },
              bundleVersion: { type: 'string' },
              applicationIdentifier: { type: 'string' }
            },
            description: 'Player settings to apply before build',
            additionalProperties: true
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('build.configure', args);
        return result;
      }
    },

    {
      name: 'build.execute',
      description: 'Execute a build with current settings or specified configuration.',
      inputSchema: {
        type: 'object',
        properties: {
          target: {
            type: 'string',
            description: 'Override build target platform'
          },
          outputPath: {
            type: 'string',
            description: 'Override output path for this build'
          },
          clean: {
            type: 'boolean',
            description: 'Clean build cache before building',
            default: false
          },
          strictMode: {
            type: 'boolean',
            description: 'Enable strict build mode (treat warnings as errors)',
            default: false
          },
          showBuiltPlayer: {
            type: 'boolean',
            description: 'Show built player after successful build',
            default: true
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('build.execute', args);
        return result;
      }
    },

    {
      name: 'build.runTests',
      description: 'Run Unity tests (EditMode and/or PlayMode) and return results.',
      inputSchema: {
        type: 'object',
        properties: {
          mode: {
            type: 'string',
            enum: ['EditMode', 'PlayMode', 'Both'],
            description: 'Test mode to run',
            default: 'Both'
          },
          filter: {
            type: 'object',
            properties: {
              testNames: {
                type: 'array',
                items: { type: 'string' },
                description: 'Specific test names to run'
              },
              categories: {
                type: 'array',
                items: { type: 'string' },
                description: 'Test categories to include'
              },
              assemblies: {
                type: 'array',
                items: { type: 'string' },
                description: 'Test assemblies to run'
              }
            },
            description: 'Filters for test execution'
          },
          buildTarget: {
            type: 'string',
            description: 'Target platform for PlayMode tests'
          },
          timeout: {
            type: 'number',
            description: 'Timeout for test execution in seconds',
            default: 300
          },
          generateReport: {
            type: 'boolean',
            description: 'Whether to generate a test report',
            default: true
          },
          reportPath: {
            type: 'string',
            description: 'Path to save the test report'
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('build.runTests', args);
        return result;
      }
    },

    {
      name: 'build.getReport',
      description: 'Get detailed information about the last build including size, warnings, and errors.',
      inputSchema: {
        type: 'object',
        properties: {
          includeAssets: {
            type: 'boolean',
            description: 'Whether to include asset information in the report',
            default: true
          },
          includeSteps: {
            type: 'boolean',
            description: 'Whether to include build step information',
            default: true
          },
          includeFiles: {
            type: 'boolean',
            description: 'Whether to include file list',
            default: false
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('build.getReport', args);
        return result;
      }
    },

    {
      name: 'build.clean',
      description: 'Clean build artifacts and temporary files.',
      inputSchema: {
        type: 'object',
        properties: {
          cleanType: {
            type: 'string',
            enum: ['all', 'library', 'temp', 'builds', 'logs'],
            description: 'Type of cleanup to perform',
            default: 'all'
          },
          confirmClean: {
            type: 'boolean',
            description: 'Confirmation that user wants to clean (safety check)',
            default: false
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('build.clean', args);
        return result;
      }
    },

    {
      name: 'build.addressables',
      description: 'Build Addressable assets for the project.',
      inputSchema: {
        type: 'object',
        properties: {
          buildTarget: {
            type: 'string',
            description: 'Target platform for Addressables build'
          },
          buildPath: {
            type: 'string',
            description: 'Path for Addressables build output'
          },
          clearCache: {
            type: 'boolean',
            description: 'Whether to clear the cache before building',
            default: false
          },
          buildPlayerContent: {
            type: 'boolean',
            description: 'Whether to build player content',
            default: true
          },
          buildRemoteContent: {
            type: 'boolean',
            description: 'Whether to build remote content',
            default: true
          },
          profile: {
            type: 'string',
            description: 'Addressables profile to use for the build'
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('build.addressables', args);
        return result;
      }
    },

    {
      name: 'build.optimize',
      description: 'Analyze and optimize build settings for size and performance.',
      inputSchema: {
        type: 'object',
        properties: {
          target: {
            type: 'string',
            description: 'Target platform to optimize for'
          },
          optimizationType: {
            type: 'string',
            enum: ['size', 'performance', 'balanced'],
            description: 'Type of optimization to prioritize',
            default: 'balanced'
          },
          analyzeAssets: {
            type: 'boolean',
            description: 'Whether to analyze asset usage and sizes',
            default: true
          },
          suggestChanges: {
            type: 'boolean',
            description: 'Whether to suggest optimization changes',
            default: true
          },
          applyChanges: {
            type: 'boolean',
            description: 'Whether to automatically apply safe optimizations',
            default: false
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('build.optimize', args);
        return result;
      }
    },

    {
      name: 'build.getConsoleLogs',
      description: 'Retrieve console logs from Unity Editor including errors, warnings, and debug messages.',
      inputSchema: {
        type: 'object',
        properties: {
          logTypes: {
            type: 'array',
            items: {
              type: 'string',
              enum: ['Log', 'Warning', 'Error', 'Assert', 'Exception']
            },
            description: 'Types of logs to retrieve',
            default: ['Log', 'Warning', 'Error']
          },
          limit: {
            type: 'number',
            description: 'Maximum number of log entries to return',
            default: 100
          },
          since: {
            type: 'string',
            description: 'ISO timestamp to get logs since (optional)'
          },
          clearAfterRetrieve: {
            type: 'boolean',
            description: 'Whether to clear logs after retrieving them',
            default: false
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('build.getConsoleLogs', args);
        return result;
      }
    },

    {
      name: 'build.profile',
      description: 'Profile build performance and identify bottlenecks.',
      inputSchema: {
        type: 'object',
        properties: {
          target: {
            type: 'string',
            description: 'Target platform to profile'
          },
          profileSteps: {
            type: 'array',
            items: {
              type: 'string',
              enum: ['preprocessing', 'compilation', 'assetprocessing', 'linking', 'postprocessing']
            },
            description: 'Build steps to profile',
            default: ['compilation', 'assetprocessing']
          },
          generateReport: {
            type: 'boolean',
            description: 'Whether to generate a detailed profiling report',
            default: true
          },
          reportPath: {
            type: 'string',
            description: 'Path to save the profiling report'
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('build.profile', args);
        return result;
      }
    }
  ];
} 