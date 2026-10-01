import type { Tool } from '../types/index.js';
import type { UnityClient } from '../unity-client.js';

interface CaptureResult {
  success: boolean;
  message: string;
  data?: {
    imageBase64: string;
    mimeType: string;
    width: number;
    height: number;
    view: string;
    camera?: string;
    cameraPosition?: { x: number; y: number; z: number };
    cameraRotation?: { x: number; y: number; z: number };
    fieldOfView?: number;
    sceneViewInfo?: {
      pivot: { x: number; y: number; z: number };
      rotation: { x: number; y: number; z: number };
      size: number;
      is2DMode: boolean;
      orthographic: boolean;
    };
    sizeBytes: number;
  };
}

export function createViewportTools(unityClient: UnityClient): Tool[] {
  return [
    {
      name: 'scene.capture',
      description:
        'Capture a screenshot of the Unity Game or Scene view. Returns the image so you can visually inspect the scene, verify object placement, check materials/lighting, and iterate on layout. Use this after making scene changes to confirm they look correct.',
      inputSchema: {
        type: 'object',
        properties: {
          view: {
            type: 'string',
            enum: ['game', 'scene'],
            description:
              'Which view to capture. "game" renders from the active camera (what players see). "scene" captures the editor Scene View (with gizmos/grid).',
            default: 'game',
          },
          width: {
            type: 'number',
            description: 'Image width in pixels (64-3840). Default 960.',
            default: 960,
          },
          height: {
            type: 'number',
            description: 'Image height in pixels (64-2160). Default 540.',
            default: 540,
          },
          cameraName: {
            type: 'string',
            description:
              'Name of a specific camera GameObject to render from. If empty, uses the Main Camera. Only applies to "game" view.',
          },
          includeGizmos: {
            type: 'boolean',
            description:
              'Whether to include gizmos in the Scene view capture. Only applies to "scene" view.',
            default: false,
          },
        },
        additionalProperties: false,
      },
      async execute(args: Record<string, unknown>) {
        const result = (await unityClient.sendRequest(
          'scene.capture',
          args
        )) as CaptureResult;

        // If capture succeeded and we have image data, return it as MCP image content
        if (result?.success && result.data?.imageBase64) {
          const { imageBase64, width, height, view, sizeBytes, ...meta } =
            result.data;

          // Build a text summary with camera/view metadata
          const lines = [
            `Captured ${view} view (${width}x${height}, ${Math.round(sizeBytes / 1024)}KB)`,
          ];

          if (meta.camera) {
            lines.push(`Camera: ${meta.camera}`);
          }
          if (meta.cameraPosition) {
            const p = meta.cameraPosition;
            lines.push(
              `Camera position: (${p.x.toFixed(2)}, ${p.y.toFixed(2)}, ${p.z.toFixed(2)})`
            );
          }
          if (meta.fieldOfView) {
            lines.push(`FOV: ${meta.fieldOfView.toFixed(1)}°`);
          }
          if (meta.sceneViewInfo) {
            const sv = meta.sceneViewInfo;
            lines.push(
              `Scene pivot: (${sv.pivot.x.toFixed(2)}, ${sv.pivot.y.toFixed(2)}, ${sv.pivot.z.toFixed(2)})`
            );
            lines.push(
              `${sv.orthographic ? 'Orthographic' : 'Perspective'} | ${sv.is2DMode ? '2D' : '3D'} mode`
            );
          }

          // Return both the image and metadata as MCP content
          return {
            _mcpContent: [
              {
                type: 'image',
                data: imageBase64,
                mimeType: 'image/png',
              },
              {
                type: 'text',
                text: lines.join('\n'),
              },
            ],
          };
        }

        // Fallback: return raw result if capture failed
        return result;
      },
    },
  ];
}
