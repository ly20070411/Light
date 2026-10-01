import type { Tool } from '../types/index.js';
import type { UnityClient } from '../unity-client.js';

/**
 * Project Memory / Living Intelligence tools.
 * These give the AI persistent, project-specific memory and a "living design document"
 * that improves over time 鈥?the single highest-leverage addition for long-term collaboration.
 */
export function createMemoryTools(unityClient: UnityClient): Tool[] {
  return [
    {
      name: 'memory.status',
      description:
        'Check the current state of the project memory system: whether a living design doc and project model exist, how many insights have been recorded, and when the model was last rebuilt. Use this first when starting work on a project.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false,
      },
      async execute() {
        const result = await unityClient.sendRequest('memory.status', { action: 'status' });
        return result;
      },
    },

    {
      name: 'memory.rebuild',
      description:
        'Re-index the Unity project into the persistent memory layer. Scans scripts for intent comments, finds design documents (any file with "design", "spec", or "doc" in the name), and builds a queryable project model. Run this when first connecting to a new project or after major architectural changes.',
      inputSchema: {
        type: 'object',
        properties: {
          includeScripts: {
            type: 'boolean',
            description: 'Whether to scan C# scripts for design intent and summaries',
            default: true,
          },
          includeDesignDocs: {
            type: 'boolean',
            description: 'Whether to index design documents and specs found in the project',
            default: true,
          },
        },
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('memory.rebuild', {
          action: 'rebuild',
          ...args,
        });
        return result;
      },
    },

    {
      name: 'memory.query',
      description:
        'Ask natural language questions against the accumulated project memory and living design intelligence. Returns relevant excerpts from the living design document, recorded insights from past playtests/observations, and indexed project knowledge. This is how the AI "remembers" what makes this specific game special.',
      inputSchema: {
        type: 'object',
        properties: {
          query: {
            type: 'string',
            description: 'Natural language question about the project (e.g. "What are the current jump parameters and why?", "How does damage calculation work?")',
          },
          maxResults: {
            type: 'number',
            description: 'Maximum number of relevant results to return',
            default: 5,
          },
        },
        required: ['query'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('memory.query', {
          action: 'query',
          ...args,
        });
        return result;
      },
    },

    {
      name: 'memory.recordInsight',
      description:
        'Record a new insight, design decision, measured observation, gotcha, or performance note into the project memory. This is the primary way the AI (and humans) teach the project model over time. Strongly recommended after every significant playmode.observe session or successful tuning pass.',
      inputSchema: {
        type: 'object',
        properties: {
          insightType: {
            type: 'string',
            description: 'Category of insight: "observation", "design_decision", "gotcha", "performance_note", "vision"',
            default: 'observation',
          },
          title: {
            type: 'string',
            description: 'Short title for the insight',
          },
          content: {
            type: 'string',
            description: 'The actual finding, decision, or measured result. Be specific and quantitative when possible.',
          },
          relatedTo: {
            type: 'string',
            description: 'What part of the game this relates to (e.g. "PlayerController", "Jump Mechanics", "Enemy AI")',
          },
        },
        required: ['content'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('memory.record_insight', {
          action: 'record_insight',
          ...args,
        });
        return result;
      },
    },

    {
      name: 'memory.getDesignDoc',
      description:
        'Retrieve the full content of the Living Design Document 鈥?the single source of truth for what this game is trying to be, its core feel, current known-good parameters, open tensions, and recent decisions. The AI should read this frequently and keep it updated.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false,
      },
      async execute() {
        const result = await unityClient.sendRequest('memory.get_design_doc', { action: 'get_design_doc' });
        return result;
      },
    },

    {
      name: 'memory.updateDesignDoc',
      description:
        'Replace the Living Design Document with an improved version. Use this after synthesizing new understanding from playtests, memory.query results, or design discussions. The document is stored as a normal .md file in the project and can also be edited by humans.',
      inputSchema: {
        type: 'object',
        properties: {
          content: {
            type: 'string',
            description: 'The complete new content of the living design document (markdown).',
          },
        },
        required: ['content'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('memory.update_design_doc', {
          action: 'update_design_doc',
          ...args,
        });
        return result;
      },
    },

    {
      name: 'memory.suggest',
      description:
        'The most powerful memory tool. Given a goal (e.g. "Make the double jump feel better"), it synthesizes the living design document, recent recorded insights from playtests, and project knowledge to return highly grounded, context-aware recommendations. This is how the AI develops real taste and long-term memory for this specific game.',
      inputSchema: {
        type: 'object',
        properties: {
          goal: {
            type: 'string',
            description: 'The concrete improvement or design goal you want advice on (be specific).',
          },
          focusArea: {
            type: 'string',
            description: 'Optional focus area (e.g. "movement", "combat", "camera", "progression")',
          },
        },
        required: ['goal'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('memory.suggest', {
          action: 'suggest',
          ...args,
        });
        return result;
      },
    },
  ];
}