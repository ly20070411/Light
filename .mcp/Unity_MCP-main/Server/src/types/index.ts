import type { JSONSchema7 } from 'json-schema';

export interface Tool {
  name: string;
  description: string;
  inputSchema: JSONSchema7;
  execute: (args: Record<string, unknown>) => Promise<unknown>;
}

export interface UnityMessage {
  id: string;
  type: 'request' | 'response' | 'notification';
  method?: string;
  params?: Record<string, unknown>;
  result?: unknown;
  error?: {
    code: number;
    message: string;
    data?: unknown;
  };
}

export interface UnityProjectInfo {
  projectName: string;
  projectPath: string;
  unityVersion: string;
  platform: string;
  scenes: string[];
  packages: UnityPackage[];
}

export interface UnityPackage {
  name: string;
  version: string;
  description?: string;
  isBuiltIn: boolean;
}

export interface UnityGameObject {
  instanceId: number;
  name: string;
  tag: string;
  layer: number;
  active: boolean;
  static: boolean;
  transform: UnityTransform;
  components: UnityComponent[];
  children: UnityGameObject[];
}

export interface UnityTransform {
  position: Vector3;
  rotation: Vector3;
  scale: Vector3;
}

export interface Vector3 {
  x: number;
  y: number;
  z: number;
}

export interface UnityComponent {
  type: string;
  enabled: boolean;
  properties: Record<string, unknown>;
}

export interface UnityScene {
  name: string;
  path: string;
  isLoaded: boolean;
  isDirty: boolean;
  buildIndex: number;
  gameObjects: UnityGameObject[];
}

export interface UnityAsset {
  guid: string;
  name: string;
  path: string;
  type: string;
  size: number;
  lastModified: Date;
}

export interface UnityBuildSettings {
  target: string;
  targetGroup: string;
  scenes: string[];
  playerSettings: Record<string, unknown>;
}

export interface UnityConsoleLog {
  timestamp: Date;
  type: 'Log' | 'Warning' | 'Error';
  message: string;
  stackTrace?: string;
}

export interface UnityTestResult {
  name: string;
  fullName: string;
  result: 'Passed' | 'Failed' | 'Skipped';
  duration: number;
  message?: string;
  stackTrace?: string;
}

export interface UnityMenuItemInfo {
  menuPath: string;
  priority: number;
  validate: boolean;
}

export type LogLevel = 'debug' | 'info' | 'warn' | 'error';

export interface ServerConfig {
  unityPort: number;
  unityHost: string;
  requestTimeout: number;
  logLevel: LogLevel;
  retryAttempts: number;
  retryDelay: number;
} 