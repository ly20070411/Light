import winston from 'winston';
import type { LogLevel } from '../types/index.js';

// Get log level from environment or default to 'info'
const logLevel: LogLevel = (process.env.LOG_LEVEL as LogLevel) || 'info';

// Configure winston logger
export const logger = winston.createLogger({
  level: logLevel,
  format: winston.format.combine(
    winston.format.timestamp({
      format: 'YYYY-MM-DD HH:mm:ss'
    }),
    winston.format.errors({ stack: true }),
    winston.format.colorize({ all: true }),
    winston.format.printf(({ timestamp, level, message, stack }) => {
      const msg = `${timestamp} [${level}]: ${message}`;
      return stack ? `${msg}\n${stack}` : msg;
    })
  ),
  transports: [
    new winston.transports.Console({
      handleExceptions: true,
      handleRejections: true,
      // Send ALL logs to stderr so that stdout remains clean for stdio-based MCP JSON-RPC messages.
      // This prevents log lines from breaking the MCP transport when the server is spawned by clients like Grok.
      stderrLevels: ['error', 'warn', 'info', 'http', 'verbose', 'debug', 'silly']
    })
  ],
  exitOnError: false
});

// Create a stream for Morgan or other HTTP loggers
export const loggerStream = {
  write: (message: string): void => {
    logger.info(message.trim());
  }
};

export default logger; 