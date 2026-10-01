using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Reports the Unity Editor console backlog.
    ///
    /// The Node bridge publishes this as the MCP tool "build.getConsoleLogs".
    /// This class therefore registers the exact dispatch name
    /// "build_getConsoleLogs": ProcessMcpRequest tries the exact
    /// "{category}_{action}" match before its category fallback, so the request
    /// lands here instead of being handed to BuildManagerTool, which used to
    /// ignore the injected action and start a full player build.
    ///
    /// Unity 2022.3 has no public API for the console backlog, so it is read
    /// through the internal UnityEditor.LogEntries / UnityEditor.LogEntry types
    /// via reflection. Every reflection step is guarded: if the internal API
    /// moves, the tool degrades into a clear error message rather than
    /// breaking the whole MCP server.
    /// </summary>
    public class ConsoleLogTool : McpToolBase
    {
        public override string ToolName => "build_getConsoleLogs";
        public override string Description => "Retrieve Unity Editor console logs (errors, warnings, messages)";
        public override string Category => "build";

        [Serializable]
        public class LogParameters
        {
            public string[] logTypes = null;
            public int limit = 100;
            public bool clearAfterRetrieve = false;
        }

        // UnityEditor.LogEntry.mode bit flags.
        private const int ModeError = 1 << 0;
        private const int ModeAssert = 1 << 1;
        private const int ModeWarning = 1 << 2;
        private const int ModeException = 1 << 4;
        private const int ModeScriptCompileError = 1 << 7;
        private const int ModeScriptCompileWarning = 1 << 8;

        public override object Execute(object parameters)
        {
            var args = GetParameters<LogParameters>(parameters);
            int limit = args.limit <= 0 ? 100 : args.limit;

            var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (args.logTypes != null && args.logTypes.Length > 0)
            {
                foreach (var t in args.logTypes)
                {
                    if (!string.IsNullOrWhiteSpace(t))
                    {
                        wanted.Add(t.Trim());
                    }
                }
            }

            if (wanted.Count == 0)
            {
                wanted.Add("Log");
                wanted.Add("Warning");
                wanted.Add("Error");
            }

            try
            {
                var entries = ReadBacklog(wanted, limit);

                if (args.clearAfterRetrieve)
                {
                    ClearBacklog();
                }

                var data = new
                {
                    count = entries.Count,
                    limit = limit,
                    logTypes = new List<string>(wanted),
                    entries = entries
                };

                return CreateSuccessResponse(data, $"Retrieved {entries.Count} console log entr{(entries.Count == 1 ? "y" : "ies")}");
            }
            catch (Exception e)
            {
                return CreateErrorResponse(
                    "Unable to read the Unity console backlog on this Editor build: " + e.Message,
                    e.StackTrace);
            }
        }

        private static List<object> ReadBacklog(HashSet<string> wanted, int limit)
        {
            var results = new List<object>();

            Type logEntriesType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
            Type logEntryType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntry");
            if (logEntriesType == null || logEntryType == null)
            {
                throw new InvalidOperationException("UnityEditor.LogEntries / UnityEditor.LogEntry were not found.");
            }

            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

            MethodInfo startGetting = logEntriesType.GetMethod("StartGettingEntries", Flags);
            MethodInfo endGetting = logEntriesType.GetMethod("EndGettingEntries", Flags);
            MethodInfo getEntry = logEntriesType.GetMethod("GetEntryInternal", Flags);
            if (startGetting == null || endGetting == null || getEntry == null)
            {
                throw new InvalidOperationException("UnityEditor.LogEntries no longer exposes StartGettingEntries/GetEntryInternal/EndGettingEntries.");
            }

            FieldInfo fCondition = FindField(logEntryType, "condition", "message", "text", "_condition");
            FieldInfo fFile = FindField(logEntryType, "file", "fileName", "_file");
            FieldInfo fLine = FindField(logEntryType, "line", "_line");
            FieldInfo fMode = FindField(logEntryType, "mode", "severity", "type", "_mode");
            if (fCondition == null || fMode == null)
            {
                // Report what the type actually exposes so the tool can be
                // re-pointed without another round of guesswork.
                var names = new List<string>();
                foreach (var f in logEntryType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    names.Add(f.Name);
                }

                throw new InvalidOperationException(
                    "UnityEditor.LogEntry does not expose the expected 'condition'/'mode' members. Available fields: " +
                    string.Join(", ", names));
            }

            object entry = Activator.CreateInstance(logEntryType);

            startGetting.Invoke(null, null);
            try
            {
                int total = GetCount(logEntriesType, Flags);

                // Walk newest-first so "limit" keeps the most recent entries.
                for (int i = total - 1; i >= 0 && results.Count < limit; i--)
                {
                    getEntry.Invoke(null, new[] { (object)i, entry });

                    int mode = fMode != null ? Convert.ToInt32(fMode.GetValue(entry)) : 0;
                    string type = ClassifyMode(mode);
                    if (!wanted.Contains(type))
                    {
                        continue;
                    }

                    results.Add(new
                    {
                        type = type,
                        message = fCondition.GetValue(entry) as string,
                        file = fFile != null ? fFile.GetValue(entry) as string : null,
                        line = fLine != null ? Convert.ToInt32(fLine.GetValue(entry)) : 0
                    });
                }
            }
            finally
            {
                endGetting.Invoke(null, null);
            }

            // Oldest-first reads more naturally in a report.
            results.Reverse();
            return results;
        }

        /// <summary>
        /// Finds a LogEntry member by any of the supplied names, tolerating the
        /// renames Unity has applied to these internal fields over time.
        /// </summary>
        private static FieldInfo FindField(Type type, params string[] candidates)
        {
            const BindingFlags FieldFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (string candidate in candidates)
            {
                FieldInfo exact = type.GetField(candidate, FieldFlags);
                if (exact != null)
                {
                    return exact;
                }

                foreach (FieldInfo field in type.GetFields(FieldFlags))
                {
                    if (string.Equals(field.Name, candidate, StringComparison.OrdinalIgnoreCase))
                    {
                        return field;
                    }
                }
            }

            return null;
        }

        private static int GetCount(Type logEntriesType, BindingFlags flags)
        {
            // GetCount is a method on most versions but has been a property on
            // some; accept either so a version bump cannot break this tool.
            MethodInfo asMethod = logEntriesType.GetMethod("GetCount", flags);
            if (asMethod != null)
            {
                return Convert.ToInt32(asMethod.Invoke(null, null));
            }

            PropertyInfo asProperty = logEntriesType.GetProperty("GetCount", flags);
            if (asProperty != null)
            {
                return Convert.ToInt32(asProperty.GetValue(null, null));
            }

            throw new InvalidOperationException("UnityEditor.LogEntries no longer exposes GetCount.");
        }

        private static void ClearBacklog()
        {
            Type logEntriesType = typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries");
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            MethodInfo clear = logEntriesType?.GetMethod("Clear", Flags);
            clear?.Invoke(null, null);
        }

        private static string ClassifyMode(int mode)
        {
            // Exceptions set the error bits too, so they are checked first.
            if ((mode & ModeException) != 0) return "Exception";
            if ((mode & ModeError) != 0 || (mode & ModeScriptCompileError) != 0) return "Error";
            if ((mode & ModeWarning) != 0 || (mode & ModeScriptCompileWarning) != 0) return "Warning";
            if ((mode & ModeAssert) != 0) return "Assert";
            return "Log";
        }
    }
}
