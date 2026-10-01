using System;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Base class for all MCP tools that can be executed from MCP clients
    /// </summary>
    public abstract class McpToolBase
    {
        /// <summary>
        /// Unique name identifier for this tool
        /// </summary>
        public abstract string ToolName { get; }
        
        /// <summary>
        /// Human-readable description of what this tool does
        /// </summary>
        public abstract string Description { get; }
        
        /// <summary>
        /// Category this tool belongs to (e.g., "project", "scene", "asset")
        /// </summary>
        public abstract string Category { get; }
        
        /// <summary>
        /// Execute the tool with the given parameters
        /// </summary>
        /// <param name="parameters">Parameters from the MCP request</param>
        /// <returns>Result object to send back to the MCP client</returns>
        public abstract object Execute(object parameters);
        
        /// <summary>
        /// Validate if the tool can be executed in the current Unity state
        /// </summary>
        /// <returns>True if the tool can be executed, false otherwise</returns>
        public virtual bool CanExecute()
        {
            return true;
        }
        
        /// <summary>
        /// Get help information for this tool including parameter descriptions
        /// </summary>
        /// <returns>Help information object</returns>
        public virtual object GetHelp()
        {
            return new
            {
                name = ToolName,
                description = Description,
                category = Category,
                canExecute = CanExecute()
            };
        }
        
        /// <summary>
        /// Log a message with the tool name prefix
        /// </summary>
        /// <param name="message">Message to log</param>
        protected void LogMessage(string message)
        {
            McpUnityServer.LogMessage($"[{ToolName}] {message}");
        }
        
        /// <summary>
        /// Log an error with the tool name prefix
        /// </summary>
        /// <param name="message">Error message to log</param>
        protected void LogError(string message)
        {
            McpUnityServer.LogError($"[{ToolName}] {message}");
        }
        
        /// <summary>
        /// Helper method to safely cast parameters to a specific type
        /// </summary>
        /// <typeparam name="T">Type to cast to</typeparam>
        /// <param name="parameters">Parameters object</param>
        /// <returns>Cast parameters or default value</returns>
        protected T GetParameters<T>(object parameters) where T : class, new()
        {
            if (parameters == null)
                return new T();
                
            try
            {
                if (parameters is T directCast)
                    return directCast;
                    
                // Try JSON conversion for complex objects
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(parameters);
                return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(json) ?? new T();
            }
            catch (Exception e)
            {
                LogError($"Failed to parse parameters: {e.Message}");
                return new T();
            }
        }
        
        /// <summary>
        /// Create a standardized success response
        /// </summary>
        /// <param name="data">Data to include in the response</param>
        /// <param name="message">Success message</param>
        /// <returns>Success response object</returns>
        protected object CreateSuccessResponse(object data = null, string message = "Operation completed successfully")
        {
            return new
            {
                success = true,
                message = message,
                data = data,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                tool = ToolName
            };
        }
        
        /// <summary>
        /// Create a standardized error response
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="details">Additional error details</param>
        /// <returns>Error response object</returns>
        protected object CreateErrorResponse(string message, object details = null)
        {
            return new
            {
                success = false,
                message = message,
                details = details,
                timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                tool = ToolName
            };
        }
    }
} 