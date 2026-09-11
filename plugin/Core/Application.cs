using System;
using Autodesk.Revit.UI;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace revit_mcp_plugin.Core
{
    public class Application : IExternalApplication
    {
        private static PushButton _statusButton;
        private static DispatcherTimer _statusTimer;
        private static string _lastState;

        public Result OnStartup(UIControlledApplication application)
        {
            RibbonPanel mcpPanel = application.CreateRibbonPanel("Revit MCP Plugin");

            PushButtonData pushButtonData = new PushButtonData("ID_EXCMD_TOGGLE_REVIT_MCP", "Revit MCP\r\n Switch",
                Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.MCPServiceConnection");
            pushButtonData.ToolTip = "Open / Close mcp server";
            pushButtonData.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/icon-16.png", UriKind.RelativeOrAbsolute));
            pushButtonData.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/icon-32.png", UriKind.RelativeOrAbsolute));
            mcpPanel.AddItem(pushButtonData);

            PushButtonData mcp_settings_pushButtonData = new PushButtonData("ID_EXCMD_MCP_SETTINGS", "Settings",
                Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.Settings");
            mcp_settings_pushButtonData.ToolTip = "MCP Settings";
            mcp_settings_pushButtonData.Image = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/settings-16.png", UriKind.RelativeOrAbsolute));
            mcp_settings_pushButtonData.LargeImage = new BitmapImage(new Uri("/RevitMCPPlugin;component/Core/Ressources/settings-32.png", UriKind.RelativeOrAbsolute));
            mcpPanel.AddItem(mcp_settings_pushButtonData);

            // Live link indicator. Grey = stopped, amber = listening, green = a client is attached.
            PushButtonData statusButtonData = new PushButtonData("ID_EXCMD_MCP_STATUS", "MCP\r\nOff",
                Assembly.GetExecutingAssembly().Location, "revit_mcp_plugin.Core.MCPStatus");
            statusButtonData.ToolTip = "MCP link status";
            _statusButton = mcpPanel.AddItem(statusButtonData) as PushButton;

            _lastState = null;
            UpdateStatusButton();

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _statusTimer.Tick += (s, e) => UpdateStatusButton();
            _statusTimer.Start();

            return Result.Succeeded;
        }

        /// <summary>
        /// Refresh the ribbon indicator from the socket service. Called once a second on the
        /// Revit UI thread. The icon is only redrawn when the state word actually changes.
        /// </summary>
        private static void UpdateStatusButton()
        {
            if (_statusButton == null) return;

            try
            {
                SocketService service = SocketService.Instance;

                string state;
                Color color;

                if (!service.IsRunning)
                {
                    state = "Off";
                    color = Color.FromRgb(0x9A, 0x9A, 0x9A);
                }
                else if (service.ActiveClients > 0)
                {
                    state = "Connected";
                    color = Color.FromRgb(0x2E, 0xA0, 0x43);
                }
                else
                {
                    state = "Listening";
                    color = Color.FromRgb(0xE3, 0x8C, 0x00);
                }

                _statusButton.ToolTip = BuildTooltip(service, state);

                if (state != _lastState)
                {
                    _statusButton.ItemText = "MCP\r\n" + state;
                    _statusButton.Image = MakeDot(16, color);
                    _statusButton.LargeImage = MakeDot(32, color);
                    _lastState = state;
                }
            }
            catch
            {
                // The indicator must never be able to break Revit.
            }
        }

        private static string BuildTooltip(SocketService service, string state)
        {
            if (!service.IsRunning)
            {
                return "MCP server stopped.\nPress Revit MCP Switch to start the listener.";
            }

            string last = service.LastActivityUtc == DateTime.MinValue
                ? "no requests yet"
                : service.LastActivityUtc.ToLocalTime().ToString("HH:mm:ss");

            return state.ToUpperInvariant() + "\n"
                 + "127.0.0.1:" + service.Port + " (loopback only)\n"
                 + "Clients attached: " + service.ActiveClients + "\n"
                 + "Requests handled: " + service.RequestCount + "\n"
                 + "Last request: " + last;
        }

        /// <summary>
        /// Draw a flat status dot at the requested size so no image resources have to ship.
        /// </summary>
        private static BitmapSource MakeDot(int size, Color color)
        {
            DrawingVisual visual = new DrawingVisual();

            using (DrawingContext dc = visual.RenderOpen())
            {
                double radius = size * 0.34;
                System.Windows.Point centre = new System.Windows.Point(size / 2.0, size / 2.0);

                dc.DrawEllipse(new SolidColorBrush(color), null, centre, radius, radius);
                dc.DrawEllipse(
                    null,
                    new Pen(new SolidColorBrush(Color.FromArgb(0x46, 0x00, 0x00, 0x00)), Math.Max(1.0, size / 16.0)),
                    centre, radius, radius);
            }

            RenderTargetBitmap bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();

            return bitmap;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                if (_statusTimer != null)
                {
                    _statusTimer.Stop();
                    _statusTimer = null;
                }
            }
            catch { }

            try
            {
                if (SocketService.Instance.IsRunning)
                {
                    SocketService.Instance.Stop();
                }
            }
            catch { }

            return Result.Succeeded;
        }
    }
}
