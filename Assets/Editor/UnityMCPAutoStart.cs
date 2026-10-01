// UnityMCPAutoStart.cs
//
// Installed into "Emerge" by the DeepSeek Harness Unity MCP setup.
//
// DeepSeek harness auto-configuration helper for the embedded
// com.unity.mcp-server package (isekream/Unity_MCP, community edition).
//
// Behaviour:
//   * On the very first domain load after this file is added, it turns on the
//     package's own "Auto Start Server" preference (UnityMCP_AutoStart).
//   * Whenever that preference is on, after every domain load it opens the
//     Unity MCP Server window (which registers the tool set on the instance)
//     and makes sure the HTTP listener runs on the configured port.
//   * During editor startup the window can be torn down once the layout /
//     modal dialogs settle, so a short self-healing loop re-opens it until it
//     stays alive (checked every ~700 ms for up to ~15 s).
//   * Turning the toggle off inside Tools > Unity MCP > Server Window and
//     reloading the domain (or restarting the Editor) stops the auto behaviour.
//
// The package's tool execution requires the Server Window instance to exist
// (it answers "MCP Server window is not open" otherwise), which is why this
// bootstrap opens the window instead of relying on the bare StartServer().

#if UNITY_EDITOR
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor;

[InitializeOnLoad]
public static class UnityMCPAutoStartBootstrap
{
    private const string PrefAutoStart = "UnityMCP_AutoStart";
    private const string PrefServerPort = "UnityMCP_ServerPort";

    // Self-healing loop state.
    private static double loopStart;
    private static double lastAttempt;
    private static int stableChecks;

    static UnityMCPAutoStartBootstrap()
    {
        EditorApplication.delayCall += () =>
        {
            // First run after installation: enable auto-start by default.
            if (!EditorPrefs.HasKey(PrefAutoStart))
            {
                EditorPrefs.SetBool(PrefAutoStart, true);
            }

            if (!EditorPrefs.GetBool(PrefAutoStart, false))
            {
                return;
            }

            loopStart = EditorApplication.timeSinceStartup;
            lastAttempt = -1.0;
            stableChecks = 0;
            EditorApplication.update += SelfHealUpdate;
            SelfHealUpdate();
        };
    }

    private static void SelfHealUpdate()
    {
        try
        {
            double now = EditorApplication.timeSinceStartup;
            double elapsed = now - loopStart;

            // Give up after ~15 s.
            if (elapsed > 15.0)
            {
                EditorApplication.update -= SelfHealUpdate;
                return;
            }

            // Throttle attempts to every ~700 ms.
            if (now - lastAttempt < 0.7)
            {
                return;
            }
            lastAttempt = now;

            if (!McpUnityServer.IsWindowOpen)
            {
                McpUnityServer.OpenWindow();
                stableChecks = 0;
            }

            int port = EditorPrefs.GetInt(PrefServerPort, 8090);
            if (!IsPortListening(port))
            {
                McpUnityServer.StartServer();
            }

            // Two consecutive passes with the window open count as stable.
            if (McpUnityServer.IsWindowOpen)
            {
                stableChecks++;
                if (stableChecks >= 2 && elapsed > 2.0)
                {
                    EditorApplication.update -= SelfHealUpdate;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[UnityMCP] auto-start bootstrap warning: " + e.Message);
        }
    }

    private static bool IsPortListening(int port)
    {
        // The Unity HttpListener may bind only the IPv6 loopback ([::1]) or
        // only 127.0.0.1, so probe both address families.
        try
        {
            return TryConnect(IPAddress.Loopback, port) || TryConnect(IPAddress.IPv6Loopback, port);
        }
        catch (System.Exception)
        {
            // A failed probe must never abort the bootstrap. Returning false
            // just means "assume the port is down and ask the package to
            // start the listener"; StartServer() is idempotent.
            return false;
        }
    }

    private static bool TryConnect(IPAddress address, int port)
    {
        // Use a Socket whose address family matches the target address:
        // Unity's Mono throws NotSupportedException ("This protocol version
        // is not supported.") when a socket is asked to connect to an address
        // of a different family. Any failure here is reported as "not
        // listening" rather than being allowed to escape.
        try
        {
            using (var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp))
            {
                var result = socket.BeginConnect(address, port, null, null);
                bool connected = result.AsyncWaitHandle.WaitOne(300);
                if (connected)
                {
                    socket.EndConnect(result);
                    return socket.Connected;
                }
            }
        }
        catch (System.Exception)
        {
            // Not listening, or this Editor build cannot run the probe.
        }
        return false;
    }
}
#endif
