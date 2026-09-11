using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// Clicking the ribbon indicator reports the full state of the MCP link.
    /// Read-only: it never starts or stops the listener.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class MCPStatus : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                SocketService service = SocketService.Instance;

                string state = !service.IsRunning
                    ? "STOPPED"
                    : (service.ActiveClients > 0 ? "CONNECTED" : "LISTENING");

                string last = service.LastActivityUtc == DateTime.MinValue
                    ? "none"
                    : service.LastActivityUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

                string advice = service.IsRunning
                    ? "CONNECTED means an MCP client is attached right now.\n"
                    + "LISTENING means the port is open and waiting for one.\n\n"
                    + "If it stays on LISTENING while Claude is running, the client is not reaching "
                    + "this port. Check that the MCP server is configured to talk to localhost:" + service.Port + "."
                    : "Press Revit MCP Switch on this panel to start the listener.";

                TaskDialog dialog = new TaskDialog("Revit MCP status");
                dialog.MainInstruction = state;
                dialog.MainContent =
                      "Listener: " + (service.IsRunning ? "running" : "stopped") + "\n"
                    + "Address: 127.0.0.1:" + service.Port + " (loopback only)\n"
                    + "Commands registered: " + service.RegisteredCommandCount + "\n"
                    + "Clients attached: " + service.ActiveClients + "\n"
                    + "Requests handled: " + service.RequestCount + "\n"
                    + "Last request: " + last + "\n\n"
                    + advice;

                dialog.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
