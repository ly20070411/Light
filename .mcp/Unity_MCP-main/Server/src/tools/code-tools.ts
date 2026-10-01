import type { Tool } from '../types/index.js';
import type { UnityClient } from '../unity-client.js';

export function createCodeTools(unityClient: UnityClient): Tool[] {
  return [
    {
      name: 'code.createScript',
      description: 'Generate C# scripts with templates for common Unity patterns like MonoBehaviour, ScriptableObject, or custom classes.',
      inputSchema: {
        type: 'object',
        properties: {
          name: {
            type: 'string',
            description: 'Name of the script class'
          },
          template: {
            type: 'string',
            enum: ['MonoBehaviour', 'ScriptableObject', 'Interface', 'Enum', 'Class', 'Singleton', 'StateMachine'],
            description: 'Template type to use',
            default: 'MonoBehaviour'
          },
          namespace: {
            type: 'string',
            description: 'Namespace for the script'
          },
          savePath: {
            type: 'string',
            description: 'Path to save the script (relative to Assets folder)'
          },
          methods: {
            type: 'array',
            items: {
              type: 'object',
              properties: {
                name: { type: 'string' },
                returnType: { type: 'string', default: 'void' },
                parameters: {
                  type: 'array',
                  items: {
                    type: 'object',
                    properties: {
                      name: { type: 'string' },
                      type: { type: 'string' }
                    },
                    required: ['name', 'type']
                  }
                },
                isPublic: { type: 'boolean', default: true },
                isVirtual: { type: 'boolean', default: false },
                body: { type: 'string', description: 'Method body code' }
              },
              required: ['name']
            },
            description: 'Methods to include in the script'
          },
          properties: {
            type: 'array',
            items: {
              type: 'object',
              properties: {
                name: { type: 'string' },
                type: { type: 'string' },
                isPublic: { type: 'boolean', default: false },
                isSerializeField: { type: 'boolean', default: false },
                hasGetter: { type: 'boolean', default: true },
                hasSetter: { type: 'boolean', default: true },
                defaultValue: { type: 'string' }
              },
              required: ['name', 'type']
            },
            description: 'Properties and fields to include'
          },
          usings: {
            type: 'array',
            items: { type: 'string' },
            description: 'Additional using statements'
          },
          baseClass: {
            type: 'string',
            description: 'Base class to inherit from (if not using template default)'
          },
          interfaces: {
            type: 'array',
            items: { type: 'string' },
            description: 'Interfaces to implement'
          }
        },
        required: ['name'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('code.createScript', args);
        return result;
      }
    },

    {
      name: 'code.analyzeScripts',
      description: 'Analyze existing C# scripts in the project to understand code structure, dependencies, and patterns.',
      inputSchema: {
        type: 'object',
        properties: {
          scriptPath: {
            type: 'string',
            description: 'Path to specific script to analyze'
          },
          directory: {
            type: 'string',
            description: 'Directory to analyze all scripts in'
          },
          analysisType: {
            type: 'string',
            enum: ['dependencies', 'complexity', 'patterns', 'documentation', 'all'],
            description: 'Type of analysis to perform',
            default: 'all'
          },
          includeComments: {
            type: 'boolean',
            description: 'Whether to include comment analysis',
            default: false
          },
          findUnusedCode: {
            type: 'boolean',
            description: 'Whether to identify unused methods and variables',
            default: false
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('code.analyzeScripts', args);
        return result;
      }
    },

    {
      name: 'code.attachScripts',
      description: 'Attach C# scripts to GameObjects in the scene.',
      inputSchema: {
        type: 'object',
        properties: {
          gameObjectName: {
            type: 'string',
            description: 'Name of the target GameObject'
          },
          gameObjectId: {
            type: 'number',
            description: 'Instance ID of the target GameObject'
          },
          scriptPath: {
            type: 'string',
            description: 'Path to the script file'
          },
          scriptName: {
            type: 'string',
            description: 'Name of the script class'
          },
          configureProperties: {
            type: 'object',
            description: 'Properties to set on the script component',
            additionalProperties: true
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('code.attachScript', args);
        return result;
      }
    },

    {
      name: 'code.findReferences',
      description: 'Find references to scripts, components, or specific code elements throughout the project.',
      inputSchema: {
        type: 'object',
        properties: {
          searchType: {
            type: 'string',
            enum: ['script', 'method', 'property', 'variable', 'type'],
            description: 'Type of element to search for'
          },
          searchTerm: {
            type: 'string',
            description: 'Name of the element to find references for'
          },
          scope: {
            type: 'string',
            enum: ['project', 'scene', 'directory'],
            description: 'Scope of the search',
            default: 'project'
          },
          directory: {
            type: 'string',
            description: 'Directory to limit search to (if scope is directory)'
          },
          includeComments: {
            type: 'boolean',
            description: 'Whether to include references in comments',
            default: false
          },
          caseSensitive: {
            type: 'boolean',
            description: 'Whether the search should be case sensitive',
            default: true
          }
        },
        required: ['searchType', 'searchTerm'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('code.findReferences', args);
        return result;
      }
    },

    {
      name: 'code.refactor',
      description: 'Perform code refactoring operations like renaming, extracting methods, or reorganizing code.',
      inputSchema: {
        type: 'object',
        properties: {
          operation: {
            type: 'string',
            enum: ['rename', 'extractMethod', 'inlineMethod', 'moveMethod', 'addInterface'],
            description: 'Refactoring operation to perform'
          },
          scriptPath: {
            type: 'string',
            description: 'Path to the script to refactor'
          },
          oldName: {
            type: 'string',
            description: 'Current name (for rename operations)'
          },
          newName: {
            type: 'string',
            description: 'New name (for rename operations)'
          },
          methodName: {
            type: 'string',
            description: 'Method name for method-related operations'
          },
          startLine: {
            type: 'number',
            description: 'Start line for code selection (for extract operations)'
          },
          endLine: {
            type: 'number',
            description: 'End line for code selection (for extract operations)'
          },
          targetClass: {
            type: 'string',
            description: 'Target class for move operations'
          },
          interfaceName: {
            type: 'string',
            description: 'Interface name for interface operations'
          }
        },
        required: ['operation', 'scriptPath'],
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('code.refactor', args);
        return result;
      }
    },

    {
      name: 'code.generateDocumentation',
      description: 'Generate or update XML documentation comments for C# scripts.',
      inputSchema: {
        type: 'object',
        properties: {
          scriptPath: {
            type: 'string',
            description: 'Path to the script to document'
          },
          directory: {
            type: 'string',
            description: 'Directory to document all scripts in'
          },
          includePrivate: {
            type: 'boolean',
            description: 'Whether to document private members',
            default: false
          },
          updateExisting: {
            type: 'boolean',
            description: 'Whether to update existing documentation',
            default: true
          },
          format: {
            type: 'string',
            enum: ['xml', 'markdown', 'html'],
            description: 'Documentation format',
            default: 'xml'
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('code.generateDocumentation', args);
        return result;
      }
    },

    {
      name: 'code.validate',
      description: 'Validate C# scripts for common issues, style violations, and potential bugs.',
      inputSchema: {
        type: 'object',
        properties: {
          scriptPath: {
            type: 'string',
            description: 'Path to specific script to validate'
          },
          directory: {
            type: 'string',
            description: 'Directory to validate all scripts in'
          },
          checks: {
            type: 'array',
            items: {
              type: 'string',
              enum: ['syntax', 'style', 'performance', 'security', 'unity-specific']
            },
            description: 'Types of validation checks to perform',
            default: ['syntax', 'style', 'unity-specific']
          },
          severity: {
            type: 'string',
            enum: ['all', 'error', 'warning', 'info'],
            description: 'Minimum severity level to report',
            default: 'warning'
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('code.validate', args);
        return result;
      }
    },

    {
      name: 'code.format',
      description: 'Format C# scripts according to coding standards and style guidelines.',
      inputSchema: {
        type: 'object',
        properties: {
          scriptPath: {
            type: 'string',
            description: 'Path to specific script to format'
          },
          directory: {
            type: 'string',
            description: 'Directory to format all scripts in'
          },
          style: {
            type: 'string',
            enum: ['microsoft', 'unity', 'custom'],
            description: 'Coding style to apply',
            default: 'unity'
          },
          indentSize: {
            type: 'number',
            description: 'Number of spaces for indentation',
            default: 4
          },
          useTabs: {
            type: 'boolean',
            description: 'Whether to use tabs instead of spaces',
            default: false
          },
          preserveComments: {
            type: 'boolean',
            description: 'Whether to preserve comment formatting',
            default: true
          }
        },
        additionalProperties: false
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('code.format', args);
        return result;
      }
    }
  ];
} 