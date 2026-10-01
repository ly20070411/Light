import type { Tool } from '../types/index.js';
import type { UnityClient } from '../unity-client.js';

/**
 * UnityMCP Lab 鈥?Autonomous Experimentation & Optimization Tools
 *
 * This is the natural evolution of the memory layer.
 * It gives agents the ability to run rigorous, data-driven experiments
 * on game mechanics at scale, grounded in the project's living design intent.
 */
export function createLabTools(unityClient: UnityClient): Tool[] {
  return [
    {
      name: 'lab.status',
      description:
        'Check the status and capabilities of the UnityMCP Lab 鈥?the autonomous experimentation and optimization system. Use this to understand what data-driven tuning and evolution capabilities are available.',
      inputSchema: {
        type: 'object',
        properties: {},
        additionalProperties: false,
      },
      async execute() {
        const result = await unityClient.sendRequest('lab.status', { action: 'status' });
        return result;
      },
    },

    {
      name: 'lab.create_experiment',
      description:
        'Define a new rigorous experiment. Specify the hypothesis, the parameters you want to vary, their ranges, how many trials to run, and what input behavior to test. The experiment is grounded against your project\'s living design document and past insights.',
      inputSchema: {
        type: 'object',
        properties: {
          experimentName: {
            type: 'string',
            description: 'Human-readable name for the experiment (e.g. "Jump Responsiveness v3")',
          },
          hypothesis: {
            type: 'string',
            description: 'What you are trying to prove or improve (e.g. "Increasing coyote time to 0.2s will improve perceived fairness without hurting precision")',
          },
          parameters: {
            type: 'array',
            items: { type: 'string' },
            description: 'Paths to the parameters being varied (e.g. ["PlayerController.coyoteTime", "PlayerController.jumpForce"])',
          },
          valueRanges: {
            type: 'array',
            items: {
              type: 'array',
              items: { type: 'number' }
            },
            description: 'Parallel array of [min, max] ranges for each parameter',
          },
          trialCount: {
            type: 'number',
            description: 'How many trials / samples to run',
            default: 12,
          },
          trialDuration: {
            type: 'number',
            description: 'Length of each play session in seconds',
            default: 8,
          },
        },
        required: ['experimentName', 'hypothesis', 'parameters'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('lab.create_experiment', {
          action: 'create_experiment',
          ...args,
        });
        return result;
      },
    },

    {
      name: 'lab.run_trials',
      description:
        'Execute the defined experiment by running multiple instrumented play sessions. This leverages the existing playmode.observe and simulateInput systems at scale, collecting rich quantitative data and visual captures for later analysis.',
      inputSchema: {
        type: 'object',
        properties: {
          experimentId: {
            type: 'string',
            description: 'The ID returned from lab.create_experiment',
          },
        },
        required: ['experimentId'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('lab.run_trials', {
          action: 'run_trials',
          ...args,
        });
        return result;
      },
    },

    {
      name: 'lab.analyze_results',
      description:
        'Analyze the results of a completed experiment. Compares outcomes against the living design document goals and past recorded insights. Returns ranked variants, statistical insights, and clear recommendations on what to keep or iterate.',
      inputSchema: {
        type: 'object',
        properties: {
          experimentId: {
            type: 'string',
            description: 'The experiment to analyze',
          },
        },
        required: ['experimentId'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('lab.analyze_results', {
          action: 'analyze_results',
          ...args,
        });
        return result;
      },
    },

    {
      name: 'lab.apply_champion',
      description:
        'Safely apply the winning parameter set from an experiment back into the project. Creates an audit trail, records the decision in project memory, and gives the human a clear diff of what changed and why.',
      inputSchema: {
        type: 'object',
        properties: {
          experimentId: {
            type: 'string',
            description: 'The experiment whose champion should be applied',
          },
        },
        required: ['experimentId'],
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = await unityClient.sendRequest('lab.apply_champion', {
          action: 'apply_champion',
          ...args,
        });
        return result;
      },
    },
  ];
}