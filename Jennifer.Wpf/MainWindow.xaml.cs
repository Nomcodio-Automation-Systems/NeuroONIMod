using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Jennifer.Wpf.Automation;
using Jennifer.Wpf.Config;
using Jennifer.Wpf.Config.ActionInjection;
using Jennifer.Wpf.Contracts;
using Jennifer.Wpf.Parsing;
using Jennifer.Wpf.Services;
using Jennifer.Wpf.Session;
using Microsoft.Win32;

namespace Jennifer.Wpf;

public partial class MainWindow : Window
{
    /// <summary>
    /// Represents a single extracted item from an action result that can be clicked to
    /// pre-fill the JSON data field for a follow-up dispatch.
    /// </summary>
    private sealed class ResultItem
    {
        /// <summary>Human-readable label shown in the result items list.</summary>
        public string Label { get; init; } = string.Empty;

        /// <summary>JSON object string that will be placed into the data field when this item is selected.</summary>
        public string DataJson { get; init; } = string.Empty;

        /// <summary>The action name this item is most naturally a parameter for (used as a hint).</summary>
        public string TargetAction { get; init; } = string.Empty;

        public override string ToString() => Label;
    }

    private const int TcpListenerPort = 8081;

    private JenniferSettings _settings = new();

    private readonly Dictionary<string, JenniferDiscoveredAction> _knownActions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, JenniferIncomingAction> _pendingActions = new(StringComparer.OrdinalIgnoreCase);

    // Action names the game has declared via actions/register — only these are valid to dispatch back.
    private readonly HashSet<string> _gameRegisteredActionNames = new(StringComparer.OrdinalIgnoreCase);

    // The action currently shown in the parameter form (null when the form is hidden).
    private JenniferDiscoveredAction? _paramFormAction;

    // Drag-and-drop origin point — set on MouseLeftButtonDown in the result items list.
    private System.Windows.Point _dragStartPoint;
    private bool _isDragging;

    private TcpListener? _listener;
    private CancellationTokenSource? _listenerCancellationSource;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _webSocketCancellationSource;
    private JenniferAutomationPlan? _automationPlan;

    /// <summary>
    /// Initializes the Jennifer WPF window and wires up lifecycle handlers.
    /// </summary>
    /// <post>The window is ready to start its listener, action discovery, and UI defaults when loaded.</post>
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
        Closed += MainWindow_Closed;
        // Subscribe to in-process server messages forwarded by TestServerService
        JenniferServer.MessageReceived += OnJenniferServerMessageReceived;
        JenniferServer.StatusChanged += OnJenniferServerStatusChanged;
    }

    /// <summary>
    /// Starts Jennifer services and preloads the local UI state.
    /// </summary>
    /// <param name="sender">The window instance.</param>
    /// <param name="e">The WPF loaded event arguments.</param>
    /// <post>The compatibility listener and source action discovery are active.</post>
    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Load persisted settings and apply to UI before anything else starts.
        _settings = JenniferSettingsStore.Load();
        ApplySettingsToUi(_settings);

        UpdateStatus("Ready for manual testing and automation.");
        ResultSuccessCheck.IsChecked = true;
        AutomationAutoReplyCheck.IsChecked = false;

        StartCompatibilityListener();
        // Action list starts empty and is populated when the game connects and registers its actions.
        TryAutoLoadPlanFromArgs();
        await LoadActionsFromSourceAsync();
        // Provide the TestServerService with payload builders so it can send startup/register on connect
        JenniferServer.BuildStartupPayload = () => JenniferRandyContractPayloadFactory.CreateStartupPayload(NormalizeText(GameNameText.Text));
        JenniferServer.BuildActionsRegisterPayload = () => JenniferRandyContractPayloadFactory.CreateActionsRegisterPayload(NormalizeText(GameNameText.Text), GetRegisteredActions());
        // Try to auto-connect to the configured endpoint (e.g. Randy at ws://localhost:8000)
        _ = AttemptAutoConnectAsync();
    }

    /// <summary>
    /// Applies the given settings values to the corresponding UI controls.
    /// </summary>
    /// <param name="settings">The settings to reflect in the UI.</param>
    private void ApplySettingsToUi(JenniferSettings settings)
    {
        ActionSourceDirectoryText.Text = settings.ActionSourceDirectory;
        EndpointText.Text = settings.Endpoint;
        GameNameText.Text = settings.GameName;
        AutoConnectCheck.IsChecked = settings.AutoConnect;
        TcpListenerPortText.Text = settings.TcpListenerPort.ToString();
        TestServerEnabledCheck.IsChecked = settings.TestServerEnabled;
        TestServerWsPortText.Text = settings.TestServerWsPort.ToString();
        TestServerHttpPortText.Text = settings.TestServerHttpPort.ToString();

        // Apply default priority
        int priorityIndex = settings.DefaultPriority switch
        {
            ForcePriority.Low => 0,
            ForcePriority.Medium => 1,
            ForcePriority.High => 2,
            ForcePriority.Critical => 3,
            _ => 1,
        };
        if (PriorityCombo.Items.Count > priorityIndex)
        {
            PriorityCombo.SelectedIndex = priorityIndex;
        }

        EphemeralCheck.IsChecked = settings.DefaultEphemeral;
    }

    /// <summary>
    /// Reads the current UI state into a <see cref="JenniferSettings"/> instance ready for persistence.
    /// </summary>
    /// <returns>The current settings captured from the UI.</returns>
    private JenniferSettings CollectSettingsFromUi()
    {
        _ = int.TryParse(TcpListenerPortText.Text, out int tcpPort);
        _ = int.TryParse(TestServerWsPortText.Text, out int wsPort);
        _ = int.TryParse(TestServerHttpPortText.Text, out int httpPort);

        ForcePriority priority = PriorityCombo.SelectedIndex switch
        {
            0 => ForcePriority.Low,
            2 => ForcePriority.High,
            3 => ForcePriority.Critical,
            _ => ForcePriority.Medium,
        };

        return new JenniferSettings
        {
            ActionSourceDirectory = NormalizeText(ActionSourceDirectoryText.Text) ?? string.Empty,
            Endpoint = NormalizeText(EndpointText.Text) ?? "ws://localhost:8000",
            GameName = NormalizeText(GameNameText.Text) ?? string.Empty,
            AutoConnect = AutoConnectCheck.IsChecked == true,
            TcpListenerPort = tcpPort > 0 ? tcpPort : 8081,
            TestServerEnabled = TestServerEnabledCheck.IsChecked == true,
            TestServerWsPort = wsPort > 0 ? wsPort : 8000,
            TestServerHttpPort = httpPort > 0 ? httpPort : 1337,
            DefaultPriority = priority,
            DefaultEphemeral = EphemeralCheck.IsChecked == true,
            AutoScrollLog = _settings.AutoScrollLog,
            MaxLogLines = _settings.MaxLogLines,
        };
    }

    private async Task AttemptAutoConnectAsync()
    {
        try
        {
            if (AutoConnectCheck.IsChecked != true)
            {
                AppendResponse("[Auto] Auto-connect is disabled.");
                return;
            }

            await Task.Delay(200);
            string endpoint = await GetPreferredEndpointAsync();
            if (!string.IsNullOrWhiteSpace(endpoint) && endpoint.StartsWith("ws://", StringComparison.OrdinalIgnoreCase))
            {
                AppendResponse($"[Auto] Attempting to connect to {endpoint}...");
                await ConnectAsync();
            }
        }
        catch (Exception ex)
        {
            AppendResponse($"[Auto] Auto-connect failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the preferred WebSocket endpoint honouring UI value and environment variables similar to NeuroMod.
    /// </summary>
    private async Task<string> GetPreferredEndpointAsync()
    {
        // 1) UI override
        string ui = NormalizeText(EndpointText.Text);
        if (!string.IsNullOrWhiteSpace(ui))
        {
            return ui;
        }

        // 2) Try to query a local helper HTTP server for the env variable (similar to NeuroMod's web request approach)
        try
        {
            using var client = new System.Net.Http.HttpClient() { Timeout = System.TimeSpan.FromMilliseconds(500) };
            // Randy exposes HTTP on port 1337; NeuroMod used a $env endpoint to fetch NEURO_SDK_WS_URL
            using var resp = await client.GetAsync("http://localhost:1337/$env/NEURO_SDK_WS_URL").ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(body))
                {
                    return body.Trim();
                }
            }
        }
        catch { }

        // 3) Environment variable fallback
        try
        {
            string? env = Environment.GetEnvironmentVariable("NEURO_SDK_WS_URL");
            if (!string.IsNullOrWhiteSpace(env))
            {
                return env!;
            }
        }
        catch { }

        // Final default
        return "ws://localhost:8000";
    }

    /// <summary>
    /// Shuts down the listener and any active WebSocket connection.
    /// </summary>
    /// <param name="sender">The window instance.</param>
    /// <param name="e">The WPF closed event arguments.</param>
    /// <post>Jennifer releases its network resources and removes its ready marker.</post>
    private async void MainWindow_Closed(object? sender, EventArgs e)
    {
        // Unsubscribe from server events to avoid leaks
        JenniferServer.MessageReceived -= OnJenniferServerMessageReceived;
        JenniferServer.StatusChanged -= OnJenniferServerStatusChanged;

        // Persist UI settings before shutdown.
        _settings = CollectSettingsFromUi();
        await JenniferSettingsStore.SaveAsync(_settings);

        _listenerCancellationSource?.Cancel();

        try
        {
            _listener?.Stop();
        }
        catch
        {
        }

        await DisconnectAsync("Jennifer window closed.");
        DeleteReadyMarker();
    }

    // ── Menu handlers ────────────────────────────────────────────────────────

    /// <summary>Saves the current UI settings to disk immediately.</summary>
    private async void SaveSettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _settings = CollectSettingsFromUi();
        await JenniferSettingsStore.SaveAsync(_settings);
        AppendResponse($"[Settings] Saved to {JenniferSettingsStore.SettingsFilePath}");
    }

    /// <summary>Opens the settings JSON file in the system default text editor.</summary>
    private void OpenSettingsFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string path = JenniferSettingsStore.SettingsFilePath;
        if (!File.Exists(path))
        {
            // Create an empty settings file so the editor has something to open.
            JenniferSettingsStore.Save(CollectSettingsFromUi());
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            AppendResponse($"[Settings] Could not open file: {ex.Message}");
        }
    }

    /// <summary>Exits the application.</summary>
    private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Connects or disconnects Jennifer from the configured WebSocket endpoint.
    /// </summary>
    /// <param name="sender">The connect button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <post>The connection state toggles and the UI reflects the active state.</post>
    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsWebSocketConnected())
        {
            await DisconnectAsync("Disconnected by user.");
            return;
        }

        await ConnectAsync();
    }

    /// <summary>
    /// Sends the currently known action catalog to the connected Neuro endpoint.
    /// </summary>
    /// <param name="sender">The register button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <post>A startup and actions/register message are sent when the WebSocket is open.</post>
    private async void RegisterActionsButton_Click(object sender, RoutedEventArgs e)
    {
        await SendActionsRegisterAsync();
    }

    /// <summary>
    /// Opens a folder picker so the user can choose the action source directory.
    /// </summary>
    /// <param name="sender">The browse button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <post>The selected directory path is written into <see cref="ActionSourceDirectoryText"/>.</post>
    private void BrowseSourceDirButton_Click(object sender, RoutedEventArgs e)
    {
        Microsoft.Win32.OpenFolderDialog dialog = new()
        {
            Title = "Select the Actions directory of the Neuro mod you want to use with Jennifer",
            InitialDirectory = NormalizeText(ActionSourceDirectoryText.Text) ?? string.Empty,
        };

        if (dialog.ShowDialog() == true)
        {
            ActionSourceDirectoryText.Text = dialog.FolderName;
            AppendResponse($"[Settings] Action source directory set to: {dialog.FolderName}");
        }
    }

    /// <summary>
    /// Reloads the action catalog from the configured (or auto-discovered) source directory.
    /// </summary>
    /// <param name="sender">The reload button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <post>The catalog is refreshed with actions from the current source path.</post>
    private async void ReloadSourceActionsButton_Click(object sender, RoutedEventArgs e)
    {
        _settings = CollectSettingsFromUi();
        await LoadActionsFromSourceAsync();
    }

    /// <summary>
    /// Forces the selected actions or the full action list through the connected endpoint.
    /// </summary>
    /// <param name="sender">The force button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <post>A force payload is sent with the current query, state, and priority settings.</post>
    private async void ForceActionsButton_Click(object sender, RoutedEventArgs e)
    {
        await SendSelectedActionsAsync();
    }

    /// <summary>
    /// Sends a single selected action using the current force settings.
    /// </summary>
    /// <param name="sender">The send action button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <post>The selected or typed action is sent immediately when possible.</post>
    private async void SendActionButton_Click(object sender, RoutedEventArgs e)
    {
        string? actionName = ActionCatalogList.SelectedItem is string sel ? StripSourceBadge(sel) : null;
        actionName = string.IsNullOrWhiteSpace(actionName) ? NormalizeText(CustomActionNameBox.Text) : actionName;

        if (string.IsNullOrWhiteSpace(actionName))
        {
            AppendResponse("[Action] Select an action or type a custom action name first.");
            return;
        }

        await SendActionForceAsync(
            actionName,
            NormalizeText(StateTextBox.Text),
            NormalizeText(QueryTextBox.Text),
            GetSelectedPriority(),
            EphemeralCheck.IsChecked == true);
    }

    /// <summary>
    /// Adds a custom action to the Jennifer catalog.
    /// </summary>
    /// <param name="sender">The add action button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <post>The custom action is available for registration and quick sending.</post>
    private void AddActionButton_Click(object sender, RoutedEventArgs e)
    {
        string actionName = NormalizeText(CustomActionNameBox.Text);
        if (string.IsNullOrWhiteSpace(actionName))
        {
            AppendResponse("[Action] Enter a custom action name before adding it.");
            return;
        }

        RegisterKnownAction(new JenniferDiscoveredAction
        {
            Name = actionName,
            Description = actionName,
            HasSchema = false,
            Source = "manual",
        });

        CustomActionNameBox.Clear();
        AppendResponse($"[Action] Added custom action '{actionName}'.");
    }

    /// <summary>
    /// Removes the selected action(s) from the action catalog and quick-action buttons.
    /// </summary>
    /// <param name="sender">The remove action button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <pre>One or more items are selected in <see cref="ActionCatalogList"/>.</pre>
    /// <post>Selected actions are removed from the catalog list and their quick-action buttons are cleared.</post>
    private void RemoveActionButton_Click(object sender, RoutedEventArgs e)
    {
        List<string> toRemove = ActionCatalogList.SelectedItems.Cast<string>().ToList();
        if (toRemove.Count == 0)
        {
            string normalized = NormalizeText(CustomActionNameBox.Text);
            if (!string.IsNullOrWhiteSpace(normalized))
                toRemove.Add(normalized);
        }

        if (toRemove.Count == 0)
        {
            AppendResponse("[Action] Select an action to remove or enter a name in the text box.");
            return;
        }

        foreach (string entry in toRemove)
        {
            string name = StripSourceBadge(entry);
            ActionCatalogList.Items.Remove(entry);
            _knownActions.Remove(name);
            _gameRegisteredActionNames.Remove(name);

            // Remove matching quick-action button
            Button? btn = ActionButtonsPanel.Children.OfType<Button>()
                .FirstOrDefault(b => string.Equals(b.Tag as string, name, StringComparison.OrdinalIgnoreCase));
            if (btn != null)
                ActionButtonsPanel.Children.Remove(btn);

            AppendResponse($"[Action] Removed action '{name}'.");
        }
    }

    /// <summary>
    /// Sends a result for the currently selected incoming action.
    /// </summary>
    /// <param name="sender">The send result button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <post>The selected action result is transmitted and removed from the pending list.</post>
    private async void SendActionResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (IncomingActionsList.SelectedItem is not JenniferIncomingAction incomingAction)
        {
            AppendResponse("[Result] Select an incoming action before sending a result.");
            return;
        }

        await SendActionResultAsync(
            incomingAction,
            ResultSuccessCheck.IsChecked == true,
            NormalizeText(ResultMessageText.Text));
    }

    /// <summary>
    /// Fills the Data JSON field when a result item is clicked, ready for a follow-up dispatch.
    /// Also pre-selects the target action in the action list when it is present.
    /// </summary>
    /// <param name="sender">The result items list.</param>
    /// <param name="e">The selection event arguments.</param>
    private void ResultItemsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultItemsList.SelectedItem is not ResultItem item)
            return;

        ActionDataJsonBox.Text = item.DataJson;

        // Pre-select the natural target action in the catalog list as a convenience hint.
        if (!string.IsNullOrWhiteSpace(item.TargetAction))
        {
            string? match = ActionCatalogList.Items.Cast<string>()
                .FirstOrDefault(n => StripSourceBadge(n).Equals(item.TargetAction, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                ActionCatalogList.SelectedItem = match;
        }
    }

    /// <summary>
    /// Records the mouse-down position to allow drag detection in <see cref="ResultItemsList_PreviewMouseMove"/>.
    /// </summary>
    /// <param name="sender">The result items list.</param>
    /// <param name="e">The mouse-button event arguments.</param>
    private void ResultItemsList_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _isDragging = false;
    }

    /// <summary>
    /// Initiates a drag-and-drop operation when the mouse has moved beyond the system drag threshold.
    /// The dragged data is the <see cref="ResultItem.DataJson"/> string of the selected item.
    /// </summary>
    /// <param name="sender">The result items list.</param>
    /// <param name="e">The mouse-move event arguments.</param>
    private void ResultItemsList_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
            return;

        System.Windows.Point pos = e.GetPosition(null);
        Vector diff = pos - _dragStartPoint;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        if (ResultItemsList.SelectedItem is not ResultItem item)
            return;

        _isDragging = true;
        DataObject dragData = new(typeof(ResultItem), item);
        DragDrop.DoDragDrop(ResultItemsList, dragData, DragDropEffects.Copy);
        _isDragging = false;
    }

    /// <summary>
    /// Updates the incoming action detail panel when the selection changes.
    /// </summary>
    /// <param name="sender">The incoming actions list.</param>
    /// <param name="e">The selection event arguments.</param>
    /// <post>The selected action payload and metadata are shown on the right-hand side.</post>
    private void IncomingActionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IncomingActionsList.SelectedItem is not JenniferIncomingAction incomingAction)
        {
            IncomingActionPayloadBox.Text = string.Empty;
            return;
        }

        StringBuilder builder = new();
        builder.AppendLine($"Id: {incomingAction.Id}");
        builder.AppendLine($"Name: {incomingAction.Name}");
        builder.AppendLine($"Received: {incomingAction.ReceivedAt:O}");
        builder.AppendLine();
        builder.AppendLine("Data:");
        builder.AppendLine(string.IsNullOrWhiteSpace(incomingAction.Data) ? "<none>" : incomingAction.Data);
        builder.AppendLine();
        builder.AppendLine("Raw:");
        builder.AppendLine(incomingAction.Raw);
        IncomingActionPayloadBox.Text = builder.ToString();
    }

    /// <summary>
    /// Loads a Jennifer automation plan from disk.
    /// </summary>
    /// <param name="sender">The load plan button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <post>The loaded automation plan is applied to the current Jennifer session.</post>
    private void LoadAutomationPlanButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new()
        {
            DefaultExt = ".json",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = FindDefaultAutomationDirectory(),
            CheckFileExists = true,
            Title = "Load Jennifer automation plan",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            JenniferAutomationPlan plan = JenniferAutomationPlanLoader.LoadFromFile(dialog.FileName);
            ApplyAutomationPlan(plan, dialog.FileName);
            AppendResponse($"[Automation] Loaded '{plan.Name}' from {dialog.FileName}.");
        }
        catch (Exception ex)
        {
            AppendResponse($"[Automation] Failed to load plan: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs the automation steps currently selected in the UI.
    /// </summary>
    /// <param name="sender">The run selected button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <post>Each selected automation step is executed in order.</post>
    private async void RunSelectedAutomationButton_Click(object sender, RoutedEventArgs e)
    {
        List<JenniferAutomationStep> selectedSteps = AutomationStepsList.SelectedItems.Cast<JenniferAutomationStep>().ToList();
        if (selectedSteps.Count == 0)
        {
            AppendResponse("[Automation] Select one or more automation steps to run.");
            return;
        }

        await RunAutomationStepsAsync(selectedSteps);
    }

    /// <summary>
    /// Runs every loaded automation step from top to bottom.
    /// </summary>
    /// <param name="sender">The run all button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <post>The full loaded automation plan is executed sequentially.</post>
    private async void RunAllAutomationButton_Click(object sender, RoutedEventArgs e)
    {
        List<JenniferAutomationStep> steps = AutomationStepsList.Items.Cast<JenniferAutomationStep>().ToList();
        if (steps.Count == 0)
        {
            AppendResponse("[Automation] Load a plan with at least one step before running all steps.");
            return;
        }

        await RunAutomationStepsAsync(steps);
    }

    /// <summary>
    /// Sends a quick action when the matching action chip is pressed.
    /// If the action has schema parameters the parameter form is shown instead.
    /// </summary>
    /// <param name="sender">The quick action button.</param>
    /// <param name="e">The routed event arguments.</param>
    /// <post>The action is dispatched directly or the parameter form is populated.</post>
    private async void ActionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string actionName)
        {
            if (_knownActions.TryGetValue(actionName, out JenniferDiscoveredAction? discovered) &&
                discovered.Parameters.Count > 0)
            {
                ShowParamForm(discovered);
            }
            else if (_knownActions.TryGetValue(actionName, out JenniferDiscoveredAction? noParamAction))
            {
                // Always show the param form for no-parameter actions so the user can
                // confirm dispatch via the "Dispatch (no params)" button rather than firing immediately.
                ShowParamForm(noParamAction);
            }
            else
            {
                await DispatchActionToGameAsync(actionName);
            }
        }
    }

    /// <summary>
    /// Shows the parameter form when a single action with parameters is selected in the catalog list.
    /// </summary>
    private void ActionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ActionCatalogList.SelectedItems.Count == 1 &&
            ActionCatalogList.SelectedItem is string displayName)
        {
            // Strip source badge prefix ([G] / [S]) to get the plain action name
            string name = StripSourceBadge(displayName);
            if (_knownActions.TryGetValue(name, out JenniferDiscoveredAction? action))
            {
                ShowParamForm(action);
                return;
            }
        }

        ParamFormBorder.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Populates and shows the parameter form for the given action.
    /// </summary>
    /// <param name="action">The action whose schema fields should be rendered.</param>
    private void ShowParamForm(JenniferDiscoveredAction action)
    {
        _paramFormAction = action;
        ParamFormTitle.Text = $"Parameters — {action.Name}";
        ParamFieldsPanel.Children.Clear();

        foreach (JenniferActionParameter param in action.Parameters)
        {
            // Label row
            TextBlock label = new()
            {
                Text = param.IsRequired ? $"{param.Name} *" : param.Name,
                FontWeight = param.IsRequired ? FontWeights.SemiBold : FontWeights.Normal,
                Margin = new Thickness(0, 6, 0, 2),
                FontSize = 12,
                Foreground = (System.Windows.Media.Brush)FindResource("ForegroundBrush"),
            };
            ParamFieldsPanel.Children.Add(label);

            if (param.JsonType == "boolean")
            {
                CheckBox check = new()
                {
                    Tag = param.Name,
                    Content = param.Name,
                    Margin = new Thickness(0, 0, 0, 2),
                    FontSize = 12,
                    AllowDrop = true,
                };
                check.DragOver += ParamField_DragOver;
                check.Drop += ParamField_Drop;
                ParamFieldsPanel.Children.Add(check);
            }
            else
            {
                // Text input for string/integer/number
                string typeHint = param.EnumValues.Count > 0
                    ? $"{param.JsonType}: {string.Join(", ", param.EnumValues)}{(param.IsRequired ? " (required)" : string.Empty)}"
                    : $"{param.JsonType}{(param.IsRequired ? " (required)" : string.Empty)}";
                TextBox box = new()
                {
                    Tag = param.Name,
                    Margin = new Thickness(0, 0, 0, 2),
                    FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                    FontSize = 12,
                    ToolTip = typeHint,
                    AllowDrop = true,
                };
                box.DragOver += ParamField_DragOver;
                box.Drop += ParamField_Drop;
                ParamFieldsPanel.Children.Add(box);
            }
        }

        ParamFormBorder.Visibility = Visibility.Visible;

        bool hasParams = action.Parameters.Count > 0;
        DispatchWithParamsButton.Visibility = hasParams ? Visibility.Visible : Visibility.Collapsed;
        DispatchNoParamsButton.Visibility = action.SupportsParameterlessDispatch
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Builds a JSON object from the current parameter form field values.
    /// </summary>
    /// <returns>A JSON string, or <c>null</c> when no fields have values.</returns>
    private string? BuildParamJson()
    {
        if (_paramFormAction is null)
            return null;

        var doc = new Dictionary<string, object?>();
        foreach (UIElement element in ParamFieldsPanel.Children)
        {
            string? fieldName = null;
            object? value     = null;

            if (element is TextBox tb && tb.Tag is string tbTag)
            {
                string raw = tb.Text.Trim();
                if (string.IsNullOrEmpty(raw))
                    continue;

                fieldName = tbTag;
                JenniferActionParameter? meta = _paramFormAction.Parameters
                    .FirstOrDefault(p => p.Name.Equals(tbTag, StringComparison.OrdinalIgnoreCase));

                if (meta?.JsonType == "integer" && int.TryParse(raw, out int iv))
                    value = iv;
                else if (meta?.JsonType == "number" && double.TryParse(raw,
                             System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out double dv))
                    value = dv;
                else
                    value = raw;
            }
            else if (element is ComboBox cb && cb.Tag is string cbTag && cb.SelectedItem is string selected)
            {
                fieldName = cbTag;
                JenniferActionParameter? cbMeta = _paramFormAction?.Parameters
                    .FirstOrDefault(p => p.Name.Equals(cbTag, StringComparison.OrdinalIgnoreCase));

                if (cbMeta?.JsonType == "integer" && int.TryParse(selected, out int cbInt))
                    value = cbInt;
                else if (cbMeta?.JsonType == "number" && double.TryParse(selected,
                             System.Globalization.NumberStyles.Any,
                             System.Globalization.CultureInfo.InvariantCulture, out double cbDbl))
                    value = cbDbl;
                else
                    value = selected;
            }
            else if (element is CheckBox chk && chk.Tag is string chkTag)
            {
                fieldName = chkTag;
                value     = chk.IsChecked == true;
            }

            if (fieldName != null && value != null)
                doc[fieldName] = value;
        }

        if (doc.Count == 0)
            return null;

        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = false });
    }

    /// <summary>
    /// Dispatches the current parameter form action with the filled-in values.
    /// </summary>
    private async void DispatchWithParamsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_paramFormAction is null)
        {
            AppendResponse("[Params] No action selected in parameter form.");
            return;
        }

        // Validate required fields
        foreach (JenniferActionParameter param in _paramFormAction.Parameters.Where(p => p.IsRequired))
        {
            bool filled = ParamFieldsPanel.Children
                .OfType<FrameworkElement>()
                .Where(el => el.Tag is string t && t.Equals(param.Name, StringComparison.OrdinalIgnoreCase))
                .Any(el => el switch
                {
                    TextBox tb => !string.IsNullOrWhiteSpace(tb.Text),
                    ComboBox cb => cb.SelectedItem != null,
                    CheckBox _ => true,
                    _ => true
                });

            if (!filled)
            {
                AppendResponse($"[Params] Required field '{param.Name}' is empty.");
                return;
            }
        }

        string? json = BuildParamJson();
        ActionDataJsonBox.Text = json ?? string.Empty;
        await DispatchActionToGameAsync(_paramFormAction.Name);
    }

    /// <summary>
    /// Dispatches the current parameter form action without any parameters.
    /// </summary>
    /// <pre>A parameter form action must be selected.</pre>
    /// <post>The action is dispatched without parameter data.</post>
    private async void DispatchNoParamsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_paramFormAction is null)
        {
            AppendResponse("[Params] No action selected in parameter form.");
            return;
        }

        await DispatchActionToGameAsync(_paramFormAction.Name, dataJson: null);
    }

    /// <summary>
    /// Copies the current parameter form values as a JSON string to the clipboard.
    /// </summary>
    private void CopyParamJsonButton_Click(object sender, RoutedEventArgs e)
    {
        string? json = BuildParamJson();
        if (json is null)
        {
            AppendResponse("[Params] Nothing to copy — fill in at least one field.");
            return;
        }
        Clipboard.SetText(json);
        AppendResponse($"[Params] Copied JSON to clipboard: {json}");
    }

    /// <summary>
    /// Establishes a WebSocket connection using the endpoint configured in the UI.
    /// </summary>
    /// <post>A receive loop is started after a successful connection.</post>
    private async Task ConnectAsync()
    {
        string endpoint = NormalizeText(EndpointText.Text);
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = await GetPreferredEndpointAsync();
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri))
        {
            AppendResponse($"[Connect] Invalid endpoint: {endpoint}");
            return;
        }

        await DisconnectAsync(string.Empty);

        try
        {
            _webSocket = new ClientWebSocket();
            _webSocketCancellationSource = new CancellationTokenSource();
            await _webSocket.ConnectAsync(uri, _webSocketCancellationSource.Token);

            ConnectButton.Content = "Disconnect";
            UpdateStatus($"Connected · {endpoint} · TCP :{TcpListenerPort}");
            AppendResponse($"[Connect] Connected to {endpoint}");

            _ = Task.Run(() => ReceiveLoopAsync(_webSocket, _webSocketCancellationSource.Token));
            // Automatically announce startup and register actions (Neuro handshake)
            try
            {
                await SendActionsRegisterAsync();
                AppendResponse("[Connect] Sent startup and actions/register payloads.");
            }
            catch (Exception ex)
            {
                AppendResponse($"[Connect] Failed to send startup/register: {ex.Message}");
            }

            // Persist the successful endpoint so it survives restart.
            _settings = CollectSettingsFromUi();
            _ = JenniferSettingsStore.SaveAsync(_settings);
        }
        catch (Exception ex)
        {
            AppendResponse($"[Connect] Failed: {ex.Message}");
            UpdateStatus("Connection failed.");
        }
    }

    /// <summary>
    /// Tears down any active WebSocket connection.
    /// </summary>
    /// <param name="reason">An optional reason to log after the socket is closed.</param>
    /// <post>The connection button and status area return to the disconnected state.</post>
    private async Task DisconnectAsync(string reason)
    {
        ClientWebSocket? socket = _webSocket;
        _webSocket = null;

        CancellationTokenSource? cancellationSource = _webSocketCancellationSource;
        _webSocketCancellationSource = null;
        cancellationSource?.Cancel();

        if (socket is not null)
        {
            try
            {
                if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing Jennifer connection", CancellationToken.None);
                }
            }
            catch
            {
            }
            finally
            {
                socket.Dispose();
            }
        }

        ConnectButton.Content = "Connect";
        UpdateStatus("Disconnected.");

        // On disconnect only remove actions the game had registered dynamically.
        // Source-loaded and manually added actions survive so they are still available after reconnect.
        foreach (string name in _gameRegisteredActionNames)
        {
            _knownActions.Remove(name);
            ActionCatalogList.Items.Remove(name);

            Button? btn = ActionButtonsPanel.Children
                .OfType<Button>()
                .FirstOrDefault(b => string.Equals(b.Tag as string, name, StringComparison.OrdinalIgnoreCase));
            if (btn is not null)
            {
                ActionButtonsPanel.Children.Remove(btn);
            }
        }
        _gameRegisteredActionNames.Clear();

        if (!string.IsNullOrWhiteSpace(reason))
        {
            AppendResponse($"[Connect] {reason}");
        }
    }

    /// <summary>
    /// Receives WebSocket frames until the remote endpoint closes or Jennifer disconnects.
    /// </summary>
    /// <param name="socket">The active WebSocket connection.</param>
    /// <param name="cancellationToken">The cancellation token for the receive loop.</param>
    /// <post>Each complete message is parsed and routed into the Jennifer UI.</post>
    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];

        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                string? message = await ReceiveMessageAsync(socket, buffer, cancellationToken);
                if (message is null)
                {
                    break;
                }

                await Dispatcher.InvokeAsync(() => ProcessIncomingMessage(message));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppendResponse($"[Receive] Error: {ex.Message}");
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                await Dispatcher.InvokeAsync(async () => await DisconnectAsync("Remote endpoint disconnected."));
            }
        }
    }

    /// <summary>
    /// Reads a full text message from the active WebSocket.
    /// </summary>
    /// <param name="socket">The active WebSocket.</param>
    /// <param name="buffer">The shared receive buffer.</param>
    /// <param name="cancellationToken">The receive cancellation token.</param>
    /// <returns>The received text message, or <c>null</c> if the socket closed.</returns>
    /// <post>A complete text frame payload is assembled before the method returns.</post>
    private static async Task<string?> ReceiveMessageAsync(ClientWebSocket socket, byte[] buffer, CancellationToken cancellationToken)
    {
        StringBuilder builder = new();

        while (true)
        {
            WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            builder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
            if (result.EndOfMessage)
            {
                return builder.ToString();
            }
        }
    }

    /// <summary>
    /// Starts the compatibility TCP listener used by older local tests.
    /// </summary>
    /// <post>Jennifer listens for raw local messages on the compatibility port when possible.</post>
    private void StartCompatibilityListener()
    {
        try
        {
            _listenerCancellationSource = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Loopback, TcpListenerPort);
            _listener.Start();
            WriteReadyMarker();
            AppendResponse($"[TCP] Compatibility listener started on port {TcpListenerPort}.");

            _ = Task.Run(() => ListenForTcpClientsAsync(_listenerCancellationSource.Token));
        }
        catch (Exception ex)
        {
            AppendResponse($"[TCP] Failed to start listener: {ex.Message}");
            UpdateStatus("TCP compatibility listener unavailable.");
        }
    }

    /// <summary>
    /// Accepts compatibility TCP clients until Jennifer shuts down.
    /// </summary>
    /// <param name="cancellationToken">The listener cancellation token.</param>
    /// <post>Each accepted client is handled asynchronously and independently.</post>
    private async Task ListenForTcpClientsAsync(CancellationToken cancellationToken)
    {
        if (_listener is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = Task.Run(() => HandleClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppendResponse($"[TCP] Accept error: {ex.Message}");
                break;
            }
        }
    }

    /// <summary>
    /// Processes a single compatibility TCP client request.
    /// </summary>
    /// <param name="client">The accepted TCP client.</param>
    /// <param name="cancellationToken">The cancellation token for the read operation.</param>
    /// <post>Jennifer echoes a response and routes any JSON payload through the normal message parser.</post>
    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        {
            byte[] buffer = new byte[4096];
            int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                return;
            }

            string received = Encoding.UTF8.GetString(buffer, 0, read).Trim();
            string response = $"Jennifer: {received}{Environment.NewLine}";
            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes.AsMemory(0, responseBytes.Length), cancellationToken);

            AppendResponse($"[TCP] Received: {received}");
            await Dispatcher.InvokeAsync(() => ProcessIncomingMessage(received));
        }
    }

    /// <summary>
    /// Parses and handles incoming WebSocket or TCP messages.
    /// </summary>
    /// <param name="rawMessage">The raw JSON payload or plain text message.</param>
    /// <post>Known action messages are added to the pending list and may trigger automation.</post>
    private void ProcessIncomingMessage(string rawMessage)
    {
        JenniferIncomingMessageResult result = JenniferSessionCoordinator.ProcessIncomingMessage(rawMessage, DateTimeOffset.Now);

        switch (result.Kind)
        {
            case JenniferWsMessageKind.ReRegisterAll:
                AppendResponse(result.LogMessage);
                _ = SendActionsRegisterAsync();
                break;

            case JenniferWsMessageKind.ActionsRegister:
                // Auto-fill game name if the game announced one and the field is currently empty.
                if (!string.IsNullOrWhiteSpace(result.GameName) && string.IsNullOrWhiteSpace(GameNameText.Text))
                {
                    GameNameText.Text = result.GameName;
                    AppendResponse($"[Register] Game name auto-filled: {result.GameName}");
                }
                // Track what the game has registered and surface each action as a quick-action button.
                foreach (string name in result.GameRegisteredActionNames)
                {
                    _gameRegisteredActionNames.Add(name);
                    RegisterKnownAction(new JenniferDiscoveredAction { Name = name, Description = $"Game action: {name}", HasSchema = false, Source = "game" });
                }
                AppendResponse(result.LogMessage);
                break;

            case JenniferWsMessageKind.ActionsUnregister:
                foreach (string name in result.GameUnregisteredActionNames)
                {
                    _gameRegisteredActionNames.Remove(name);
                }
                AppendResponse(result.LogMessage);
                break;

            case JenniferWsMessageKind.ActionsForce:
                AppendResponse(result.LogMessage);
                if (result.ForceCandidateNames.Count > 0)
                {
                    // Pick the first candidate that the game has actually registered.
                    // Fall back to the first candidate if none match (e.g. registry not yet populated).
                    string chosen = result.ForceCandidateNames
                        .FirstOrDefault(n => _gameRegisteredActionNames.Contains(n))
                        ?? result.ForceCandidateNames[0];
                    AppendResponse($"[WS] actions/force — dispatching '{chosen}'.");
                    _ = DispatchActionToGameAsync(chosen);
                }
                break;

            case JenniferWsMessageKind.Action:
                AddIncomingAction(result.IncomingAction!);
                AppendResponse(result.LogMessage);
                // Show a brief UI notification for incoming actions
                try
                {
                    BeginInvokeOnUiThread(() => ShowNotification("Incoming Action", result.IncomingAction!.Name));
                }
                catch { }
                _ = TryRunAutomationReplyAsync(result.IncomingAction!);
                break;

            case JenniferWsMessageKind.ActionResult:
                AppendResponse(result.LogMessage);
                ShowActionResult(result.ActionResultId, result.ActionResultSuccess, result.ActionResultMessage);
                break;

            case JenniferWsMessageKind.Generic:
                AppendResponse(result.LogMessage);
                break;

            default:
                AppendResponse(result.LogMessage);
                break;
        }
    }

    private void OnJenniferServerMessageReceived(string message)
    {
        // If the test server sends lifecycle messages, reflect them in the status banner
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (message.StartsWith("[TestServer]", StringComparison.OrdinalIgnoreCase))
            {
                // Show concise server status in the header and log the full message
                UpdateStatus(message.Replace("[TestServer] ", string.Empty));
                AppendResponse(message);
                return;
            }

            // Forward other messages (Randy-style JSON or compatibility posts) into the normal processing pipeline
            ProcessIncomingMessage(message);
        }));
    }

    private void OnJenniferServerStatusChanged(string status)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            // If this status contains a client count, update the clients badge
            if (status.StartsWith("Clients:", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    ClientsText.Text = status;
                }
                catch { }
            }

            UpdateStatus(status);
            AppendResponse($"[TestServer] {status}");
        }));
    }

    /// <summary>
    /// Shows a transient notification in the top-right overlay.
    /// Notifications auto-dismiss after a short delay.
    /// </summary>
    /// <param name="title">Short title for the notification.</param>
    /// <param name="message">Message body to display.</param>
    private async void ShowNotification(string title, string message)
    {
        try
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 17, 24, 39)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(20, 184, 166)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8),
                Margin = new Thickness(0, 6, 6, 0),
                Opacity = 0,
                IsHitTestVisible = false,
            };

            var stack = new StackPanel { Orientation = Orientation.Vertical };
            var titleText = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
            var bodyText = new TextBlock { Text = message, Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap, MaxWidth = 360 };
            stack.Children.Add(titleText);
            stack.Children.Add(bodyText);
            border.Child = stack;

            NotificationsPanel.Children.Insert(0, border);

            // Simple fade-in
            var fadeInTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
            double t = 0;
            fadeInTimer.Tick += (s, e) =>
            {
                t += 0.08;
                border.Opacity = Math.Min(1.0, t);
                if (border.Opacity >= 1.0)
                {
                    fadeInTimer.Stop();
                }
            };
            fadeInTimer.Start();

            // Wait then fade out and remove
            await Task.Delay(4200).ConfigureAwait(false);

            await Dispatcher.InvokeAsync(() =>
            {
                var fadeOutTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
                double u = 1.0;
                fadeOutTimer.Tick += (s, e) =>
                {
                    u -= 0.08;
                    border.Opacity = Math.Max(0.0, u);
                    if (border.Opacity <= 0.0)
                    {
                        fadeOutTimer.Stop();
                        try { NotificationsPanel.Children.Remove(border); } catch { }
                    }
                };
                fadeOutTimer.Start();
            }).Task.ConfigureAwait(false);
        }
        catch
        {
            // Don't let notification failures affect the app
        }
    }

    /// <summary>
    /// Displays the result of a dispatched action in the Action Result panel and populates
    /// the result items list with any extractable parameters for follow-up actions.
    /// </summary>
    /// <param name="actionId">The action identifier that was resolved.</param>
    /// <param name="success">Whether the game reported success.</param>
    /// <param name="message">The human-readable result message from the game.</param>
    private void ShowActionResult(string? actionId, bool success, string? message)
    {
        InvokeOnUiThread(() =>
        {
            // Find the matching pending action to know which action produced this result.
            string actionName = string.Empty;
            if (!string.IsNullOrWhiteSpace(actionId) && _pendingActions.TryGetValue(actionId, out JenniferIncomingAction? matched))
            {
                IncomingActionsList.SelectedItem = matched;
                actionName = matched.Name;
            }

            string statusLine = success ? "✔ Success" : "✘ Failure";
            string body = string.IsNullOrWhiteSpace(message) ? "(no message)" : message!.Replace("\\r\\n", "\n").Replace("\\n", "\n");
            IncomingActionPayloadBox.Text = $"{statusLine}\r\n\r\n{body}";

            // Populate result items so they can be clicked into follow-up dispatches.
            ResultItemsList.Items.Clear();
            if (success && !string.IsNullOrWhiteSpace(message))
            {
                foreach (ResultItem item in ExtractResultItems(actionName, message!))
                {
                    ResultItemsList.Items.Add(item);
                }
            }
        });
    }

    /// <summary>
    /// Parses a result message into clickable result items using generic line-pattern matching.
    /// Action-specific overrides run first; unmatched lines fall through to generic heuristics
    /// so results from new or unknown actions are still surfaced without code changes.
    /// Each item carries a JSON data snippet ready to be dropped or clicked into a follow-up dispatch.
    /// </summary>
    /// <param name="actionName">The action that produced the result.</param>
    /// <param name="message">The raw result message text.</param>
    /// <returns>Zero or more result items extracted from the message.</returns>
    private static IEnumerable<ResultItem> ExtractResultItems(string actionName, string message)
    {
        string[] lines = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        bool anyYielded = false;

        foreach (string raw in lines)
        {
            string line = raw.TrimStart();

            // ── Indexed errand line: "[N] Description at (X,Y) | ..." ─────────
            if (TryParseIndexedErrandLine(line, out ResultItem? errandItem))
            {
                yield return errandItem!;
                anyYielded = true;
                continue;
            }

            // ── Em-dash item: "Name — rest …" (duplicants, colonies, …) ───────
            if (TryParseEmDashLine(line, actionName, out ResultItem? emDashItem))
            {
                yield return emDashItem!;
                anyYielded = true;
                continue;
            }

            // ── Colon-keyed line: "label: value …" ───────────────────────────
            // Only emit these as generic items when no action-specific match ran
            // (prevents spurious items from header/footer lines).
            if (!anyYielded && TryParseColonLine(line, actionName, out ResultItem? colonItem))
            {
                yield return colonItem!;
                // do NOT set anyYielded — keep scanning for better matches
            }
        }

        // If nothing matched so far, do a second pass emitting colon-keyed items
        // so at minimum something useful is surfaced.
        if (!anyYielded)
        {
            foreach (string raw in lines)
            {
                string line = raw.TrimStart();
                if (TryParseColonLine(line, actionName, out ResultItem? colonItem))
                {
                    yield return colonItem!;
                }
            }
        }
    }

    /// <summary>
    /// Tries to parse an indexed errand line: "[N] Description at (X,Y) | ...".
    /// </summary>
    /// <param name="line">A trimmed text line from the result message.</param>
    /// <param name="item">The resulting item when the pattern matches.</param>
    /// <returns><c>true</c> when the line was parsed successfully.</returns>
    private static bool TryParseIndexedErrandLine(string line, out ResultItem? item)
    {
        item = null;
        if (!line.StartsWith("[", StringComparison.Ordinal)) return false;

        int bracketClose = line.IndexOf(']');
        if (bracketClose < 1) return false;
        if (!int.TryParse(line.Substring(1, bracketClose - 1), out int errandId)) return false;

        string afterId = line.Substring(bracketClose + 1).TrimStart();

        // Strip pipe-separated suffix ("| group:… distance:…") to get "Description at (X,Y)"
        int pipeIdx = afterId.IndexOf('|');
        string atPart = pipeIdx >= 0 ? afterId.Substring(0, pipeIdx).TrimEnd() : afterId;

        int atIdx = atPart.LastIndexOf(" at (", StringComparison.OrdinalIgnoreCase);
        if (atIdx < 0) return false;

        string description = atPart.Substring(0, atIdx).Trim();
        int parenClose = atPart.IndexOf(')', atIdx);
        if (parenClose < 0) return false;

        string coords = atPart.Substring(atIdx + 5, parenClose - atIdx - 5);
        string[] parts = coords.Split(',');
        if (parts.Length != 2) return false;
        if (!int.TryParse(parts[0].Trim(), out int x) || !int.TryParse(parts[1].Trim(), out int y)) return false;

        item = new ResultItem
        {
            Label = $"[{errandId}] {description} ({x},{y})",
            DataJson = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["errand_id"] = errandId,
                ["target_x"]  = x,
                ["target_y"]  = y,
            }),
            TargetAction = "assign_errand",
        };
        return true;
    }

    /// <summary>
    /// Tries to parse an em-dash line: "Name — rest" (typical in duplicant lists).
    /// </summary>
    /// <param name="line">A trimmed text line from the result message.</param>
    /// <param name="actionName">The action name for target-action inference.</param>
    /// <param name="item">The resulting item when the pattern matches.</param>
    /// <returns><c>true</c> when the line was parsed successfully.</returns>
    private static bool TryParseEmDashLine(string line, string actionName, out ResultItem? item)
    {
        item = null;
        int dash = line.IndexOf(" \u2014 ", StringComparison.Ordinal);
        if (dash < 0) return false;

        string name = line.Substring(0, dash).Trim();
        if (string.IsNullOrWhiteSpace(name)) return false;

        // Infer the best follow-up action: if the source action contains "duplicant"
        // prefer get_duplicant_info, otherwise fall back to the action name itself.
        string target = actionName.IndexOf("duplicant", StringComparison.OrdinalIgnoreCase) >= 0
            ? "get_duplicant_info"
            : actionName;

        // Build JSON from the name — key depends on whether the line came from a duplicant list.
        string key = target.Equals("get_duplicant_info", StringComparison.OrdinalIgnoreCase)
            ? "duplicant_name"
            : "name";

        item = new ResultItem
        {
            Label = name,
            DataJson = JsonSerializer.Serialize(new Dictionary<string, string> { [key] = name }),
            TargetAction = target,
        };
        return true;
    }

    /// <summary>
    /// Tries to parse a colon-keyed line: "label: value" into a generic result item.
    /// Skips header/summary lines and very long labels.
    /// </summary>
    /// <param name="line">A trimmed text line from the result message.</param>
    /// <param name="actionName">The source action name (used as fallback target).</param>
    /// <param name="item">The resulting item when the pattern matches.</param>
    /// <returns><c>true</c> when the line was parsed successfully.</returns>
    private static bool TryParseColonLine(string line, string actionName, out ResultItem? item)
    {
        item = null;
        // Skip lines that look like they are sentence-style prose or start with numbers (index lines)
        if (line.Length == 0 || char.IsDigit(line[0])) return false;

        int colon = line.IndexOf(':');
        if (colon < 1) return false;

        string label = line.Substring(0, colon).Trim();
        string value = line.Substring(colon + 1).Trim();

        // Heuristic: labels should be short, non-whitespace-only, and not look like sentences
        if (string.IsNullOrWhiteSpace(label) || label.Length > 40 || label.Contains(' ', StringComparison.Ordinal) && label.Split(' ').Length > 4)
            return false;
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Derive a snake_case JSON key from the label
        string jsonKey = label.Trim().ToLowerInvariant().Replace(' ', '_');

        item = new ResultItem
        {
            Label = $"{label}: {value}",
            DataJson = JsonSerializer.Serialize(new Dictionary<string, string> { [jsonKey] = value }),
            TargetAction = actionName,
        };
        return true;
    }

    /// <summary>
    /// Accepts a <see cref="ResultItem"/> drag onto the Data JSON text box.
    /// </summary>
    /// <param name="sender">The data JSON text box.</param>
    /// <param name="e">The drag-event arguments.</param>
    private void ActionDataJsonBox_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ResultItem)) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Drops the full <see cref="ResultItem.DataJson"/> into the Data JSON text box and
    /// pre-selects the target action so a follow-up dispatch is one click away.
    /// </summary>
    /// <param name="sender">The data JSON text box.</param>
    /// <param name="e">The drop-event arguments.</param>
    private void ActionDataJsonBox_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ResultItem)) is not ResultItem item) return;

        ActionDataJsonBox.Text = item.DataJson;

        if (!string.IsNullOrWhiteSpace(item.TargetAction))
        {
            string? match = ActionCatalogList.Items.Cast<string>()
                .FirstOrDefault(n => StripSourceBadge(n).Equals(item.TargetAction, StringComparison.OrdinalIgnoreCase));
            if (match != null)
                ActionCatalogList.SelectedItem = match;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Accepts a <see cref="ResultItem"/> drag over any parameter form field.
    /// </summary>
    /// <param name="sender">A parameter field (TextBox, ComboBox, or CheckBox).</param>
    /// <param name="e">The drag-event arguments.</param>
    private void ParamField_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(ResultItem)) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Drops a <see cref="ResultItem"/> onto a parameter field.
    /// When the item's JSON contains a key whose name matches the field's parameter name the
    /// matching value is extracted and applied directly; otherwise the raw JSON is placed in the field.
    /// </summary>
    /// <param name="sender">The parameter field that received the drop.</param>
    /// <param name="e">The drop-event arguments.</param>
    private void ParamField_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(typeof(ResultItem)) is not ResultItem item) return;

        if (sender is not FrameworkElement fe || fe.Tag is not string fieldName)
        {
            e.Handled = true;
            return;
        }

        // Try to extract a matching key from the item's JSON.
        string? extracted = TryExtractJsonValue(item.DataJson, fieldName);

        switch (sender)
        {
            case TextBox tb:
                tb.Text = extracted ?? item.DataJson;
                break;

            case ComboBox cb:
                if (extracted != null)
                {
                    // Select matching enum value if present.
                    string? enumMatch = cb.Items.Cast<string>()
                        .FirstOrDefault(v => v.Equals(extracted, StringComparison.OrdinalIgnoreCase));
                    if (enumMatch != null)
                        cb.SelectedItem = enumMatch;
                }
                break;

            case CheckBox chk:
                if (extracted != null && bool.TryParse(extracted, out bool bv))
                    chk.IsChecked = bv;
                break;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Tries to extract a value for <paramref name="key"/> from a JSON object string.
    /// Returns <c>null</c> when the key is absent or the JSON cannot be parsed.
    /// </summary>
    /// <param name="json">The JSON object string.</param>
    /// <param name="key">The property name to look up (case-insensitive).</param>
    /// <returns>The string representation of the value, or <c>null</c>.</returns>
    private static string? TryExtractJsonValue(string? json, string key)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            foreach (JsonProperty prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                    return prop.Value.ToString();
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Adds a pending incoming action to the UI and local lookup map.
    /// </summary>
    /// <param name="incomingAction">The parsed incoming action.</param>
    /// <post>The action appears at the top of the pending incoming actions list.</post>
    private void AddIncomingAction(JenniferIncomingAction incomingAction)
    {
        InvokeOnUiThread(() =>
        {
            IReadOnlyList<JenniferIncomingAction> mergedActions = JenniferSessionCoordinator.UpsertPendingAction(_pendingActions.Values, incomingAction);

            _pendingActions.Clear();
            IncomingActionsList.Items.Clear();
            foreach (JenniferIncomingAction action in mergedActions)
            {
                _pendingActions[action.Id] = action;
                IncomingActionsList.Items.Add(action);
            }

            IncomingActionsList.SelectedItem = incomingAction;
        });
    }

    /// <summary>
    /// Removes a pending incoming action after a result is sent.
    /// </summary>
    /// <param name="incomingActionId">The pending action identifier.</param>
    /// <post>The pending lookup and visible list remain in sync.</post>
    private void RemoveIncomingAction(string incomingActionId)
    {
        InvokeOnUiThread(() =>
        {
            IReadOnlyList<JenniferIncomingAction> remainingActions = JenniferSessionCoordinator.RemovePendingAction(_pendingActions.Values, incomingActionId);

            _pendingActions.Clear();
            IncomingActionsList.Items.Clear();
            foreach (JenniferIncomingAction action in remainingActions)
            {
                _pendingActions[action.Id] = action;
                IncomingActionsList.Items.Add(action);
            }

            if (IncomingActionsList.Items.Count == 0)
            {
                IncomingActionPayloadBox.Clear();
            }
        });
    }

    /// <summary>
    /// Sends the current action catalog to the connected endpoint.
    /// </summary>
    /// <post>The endpoint receives startup and actions/register payloads that reflect the Jennifer catalog.</post>
    private async Task SendActionsRegisterAsync()
    {
        if (!IsWebSocketConnected())
        {
            AppendResponse("[Register] WebSocket not connected.");
            return;
        }

        try
        {
            string gameName = NormalizeText(GameNameText.Text);
            List<JenniferDiscoveredAction> actions = GetRegisteredActions().ToList();

            await SendWebSocketPayloadAsync(JenniferRandyContractPayloadFactory.CreateStartupPayload(gameName));
            await SendWebSocketPayloadAsync(JenniferRandyContractPayloadFactory.CreateActionsRegisterPayload(gameName, actions));
            AppendResponse($"[Register] Registered {actions.Count} actions.");
        }
        catch (Exception ex)
        {
            AppendResponse($"[Register] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Sends the selected actions using the current force-action settings.
    /// </summary>
    /// <post>The currently selected actions are sent in a single force payload.</post>
    private async Task SendSelectedActionsAsync()
    {
        List<string> selectedActions = ActionCatalogList.SelectedItems.Cast<string>().Select(StripSourceBadge).ToList();
        if (selectedActions.Count == 0)
        {
            selectedActions = ActionCatalogList.Items.Cast<string>().Select(StripSourceBadge).ToList();
        }

        if (selectedActions.Count == 0)
        {
            AppendResponse("[Force] Add or discover at least one action first.");
            return;
        }

        await SendActionForceAsync(
            selectedActions,
            NormalizeText(StateTextBox.Text),
            NormalizeText(QueryTextBox.Text),
            GetSelectedPriority(),
            EphemeralCheck.IsChecked == true);
    }

    /// <summary>
    /// Sends a single action through the WebSocket or compatibility TCP path.
    /// </summary>
    /// <param name="actionName">The action name to send.</param>
    /// <param name="state">The optional state payload.</param>
    /// <param name="query">The optional force query.</param>
    /// <param name="priority">The force priority.</param>
    /// <param name="ephemeral">Whether the force request uses ephemeral context.</param>
    /// <post>The action is sent immediately when either transport is available.</post>
    private async Task SendActionForceAsync(string actionName, string state, string query, string priority, bool ephemeral)
    {
        await SendActionForceAsync(new[] { actionName }, state, query, priority, ephemeral);
    }

    /// <summary>
    /// Sends one or more actions through the WebSocket or compatibility TCP path.
    /// </summary>
    /// <param name="actionNames">The action names to send.</param>
    /// <param name="state">The optional state payload.</param>
    /// <param name="query">The optional force query.</param>
    /// <param name="priority">The force priority.</param>
    /// <param name="ephemeral">Whether the force request uses ephemeral context.</param>
    /// <post>The action names are delivered over the best available transport.</post>
    private async Task SendActionForceAsync(IEnumerable<string> actionNames, string state, string query, string priority, bool ephemeral)
    {
        JenniferForceRequestPlan requestPlan = JenniferSessionCoordinator.BuildForceRequestPlan(
            IsWebSocketConnected(),
            NormalizeText(GameNameText.Text),
            actionNames,
            state,
            query,
            priority,
            ephemeral);

        if (requestPlan.Mode == JenniferForceRequestMode.None)
        {
            AppendResponse(requestPlan.LogMessage ?? "[Force] Request could not be prepared.");
            return;
        }

        if (requestPlan.Mode == JenniferForceRequestMode.WebSocket)
        {
            await SendWebSocketPayloadAsync(requestPlan.WebSocketPayload!);
            AppendResponse($"[Force] Sent {requestPlan.ActionNames.Count} action(s) with priority '{requestPlan.Priority}'.");
            return;
        }

        await SendCompatibilityMessageAsync(requestPlan.CompatibilityMessage!);
    }

    /// <summary>
    /// Sends an <c>action</c> command back to the game in response to an <c>actions/force</c> request.
    /// The game processes the action and is expected to reply with <c>action/result</c>.
    /// </summary>
    /// <param name="actionName">The action name Jennifer has selected to dispatch.</param>
    private async Task DispatchActionToGameAsync(string actionName, string? dataJson = null)
    {
        if (!IsWebSocketConnected())
        {
            AppendResponse("[Dispatch] WebSocket not connected — cannot dispatch action to game.");
            return;
        }

        string id = Guid.NewGuid().ToString("N");

        // When no data is provided by the caller, fall back to whatever the user typed in the UI.
        if (dataJson is null)
            Dispatcher.Invoke(() => { dataJson = NormalizeText(ActionDataJsonBox.Text); });

        string payload = JenniferRandyContractPayloadFactory.CreateActionPayload(id, actionName, string.IsNullOrWhiteSpace(dataJson) ? null : dataJson);

        // Register
        JenniferIncomingAction pending = new()
        {
            Id = id,
            Name = actionName,
            ReceivedAt = DateTimeOffset.Now,
            Raw = payload,
        };
        AddIncomingAction(pending);

        await SendWebSocketPayloadAsync(payload);
        AppendResponse($"[Dispatch] Sent action '{actionName}' (id={id}){(string.IsNullOrWhiteSpace(dataJson) ? string.Empty : " with data")} to game.");

        // Clear the data field so it is ready for the next action.
        InvokeOnUiThread(() =>
        {
            ActionDataJsonBox.Clear();
            ResultItemsList.SelectedItem = null;
        });
    }

    /// <summary>
    /// Sends an action result for a pending incoming action.
    /// </summary>
    /// <param name="incomingAction">The pending incoming action.</param>
    /// <param name="success">The result success flag.</param>
    /// <param name="message">An optional result message.</param>
    /// <post>The action/result payload is delivered and the pending action is cleared.</post>
    private async Task SendActionResultAsync(JenniferIncomingAction incomingAction, bool success, string message)
    {
        if (!IsWebSocketConnected())
        {
            AppendResponse("[Result] WebSocket not connected.");
            return;
        }

        await SendWebSocketPayloadAsync(JenniferRandyContractPayloadFactory.CreateActionResultPayload(NormalizeText(GameNameText.Text), incomingAction.Id, success, message));
        RemoveIncomingAction(incomingAction.Id);
        AppendResponse($"[Result] Sent {(success ? "success" : "failure")} for '{incomingAction.Name}'.");
    }

    /// <summary>
    /// Sends a JSON payload over the active WebSocket connection.
    /// </summary>
    /// <param name="payload">The serialized JSON payload to send.</param>
    /// <post>The payload is serialized with Jennifer defaults and written as a text frame.</post>
    private async Task SendWebSocketPayloadAsync(string payload)
    {
        if (_webSocket is null || _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected.");
        }

        byte[] bytes = Encoding.UTF8.GetBytes(payload);
        await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    /// <summary>
    /// Sends a raw message over the compatibility TCP transport.
    /// </summary>
    /// <param name="message">The raw message to send.</param>
    /// <post>The compatibility listener response is logged back into the activity pane.</post>
    private async Task SendCompatibilityMessageAsync(string message)
    {
        try
        {
            using TcpClient client = new();
            await client.ConnectAsync(IPAddress.Loopback, TcpListenerPort);
            using NetworkStream stream = client.GetStream();
            byte[] payload = Encoding.UTF8.GetBytes(message);
            await stream.WriteAsync(payload.AsMemory(0, payload.Length));

            byte[] responseBuffer = new byte[1024];
            int read = await stream.ReadAsync(responseBuffer.AsMemory(0, responseBuffer.Length));
            string response = Encoding.UTF8.GetString(responseBuffer, 0, read).Trim();
            AppendResponse($"[TCP] {response}");
        }
        catch (Exception ex)
        {
            AppendResponse($"[TCP] Send error: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads actions from the configured or auto-discovered source directory.
    /// </summary>
    /// <post>The Jennifer catalog is enriched with source-derived action metadata when available.</post>
    private async Task LoadActionsFromSourceAsync()
    {
        // Prefer the explicitly configured path; fall back to auto-discovery.
        string? configuredDir = NormalizeText(_settings.ActionSourceDirectory);
        string actionsDirectory;

        if (!string.IsNullOrWhiteSpace(configuredDir) && Directory.Exists(configuredDir))
        {
            actionsDirectory = configuredDir;
        }
        else
        {
            string? neuroModRoot = FindNeuroModRootDirectory();
            if (neuroModRoot is null)
            {
                AppendResponse("[Source] Action source directory not configured and NeuroMod root was not found. Set the Actions directory in Settings.");
                return;
            }

            actionsDirectory = Path.Combine(neuroModRoot, "Actions");
        }
        IReadOnlyList<JenniferDiscoveredAction> discoveredActions = await JenniferActionCatalogParser.ParseDirectoryAsync(actionsDirectory);
        foreach (JenniferDiscoveredAction action in discoveredActions)
        {
            action.Source = "source";
            RegisterKnownAction(action);
        }

        AppendResponse($"[Source] Loaded {discoveredActions.Count} action(s) from source.");

        // Merge actions from the JSON injection config (action_injection.json in AppData).
        List<JenniferDiscoveredAction> injectedActions = JenniferActionInjectionService.LoadInjectedActions();
        foreach (JenniferDiscoveredAction action in injectedActions)
        {
            RegisterKnownAction(action);
        }

        if (injectedActions.Count > 0)
            AppendResponse($"[Injection] Merged {injectedActions.Count} action(s) from action_injection.json.");
    }

    /// <summary>
    /// Applies a Jennifer automation plan to the current session.
    /// </summary>
    /// <param name="plan">The loaded automation plan.</param>
    /// <param name="sourcePath">The plan source path for display.</param>
    /// <post>The UI reflects the loaded plan and its actions are added to the catalog.</post>
    private void ApplyAutomationPlan(JenniferAutomationPlan plan, string sourcePath)
    {
        _automationPlan = plan;
        AutomationPlanText.Text = $"{plan.Name} ({plan.Steps.Count} step(s))";
        AutomationDescriptionText.Text = string.IsNullOrWhiteSpace(plan.Description)
            ? sourcePath
            : $"{plan.Description}{Environment.NewLine}{sourcePath}";
        AutomationAutoReplyCheck.IsChecked = plan.AutoRespond;

        if (!string.IsNullOrWhiteSpace(plan.Endpoint))
        {
            EndpointText.Text = plan.Endpoint;
        }

        if (!string.IsNullOrWhiteSpace(plan.GameName))
        {
            GameNameText.Text = plan.GameName;
        }

        AutomationStepsList.Items.Clear();
        foreach (JenniferAutomationStep step in plan.Steps)
        {
            AutomationStepsList.Items.Add(step);
            RegisterKnownAction(new JenniferDiscoveredAction
            {
                Name = step.ActionName,
                Description = string.IsNullOrWhiteSpace(step.Name) ? step.ActionName : step.Name,
                HasSchema = false,
                Source = "source",
            });
        }
    }

    /// <summary>
    /// Runs each automation step in sequence.
    /// </summary>
    /// <param name="steps">The steps to execute.</param>
    /// <post>Each step emits its configured action force request with a small delay between steps.</post>
    private async Task RunAutomationStepsAsync(IEnumerable<JenniferAutomationStep> steps)
    {
        foreach (JenniferAutomationStep step in steps)
        {
            await SendActionForceAsync(step.ActionName, step.State, step.Query, step.Priority, step.Ephemeral);
            AppendResponse($"[Automation] Ran step '{step.DisplayName}'.");
            await Task.Delay(150);
        }
    }

    /// <summary>
    /// Sends an automatic result when a loaded automation step matches an incoming action.
    /// </summary>
    /// <param name="incomingAction">The incoming action to evaluate.</param>
    /// <post>The configured automation result is sent when auto-reply is enabled and a matching step exists.</post>
    private async Task TryRunAutomationReplyAsync(JenniferIncomingAction incomingAction)
    {
        JenniferAutomationReply? reply = JenniferSessionCoordinator.FindAutomationReply(_automationPlan, AutomationAutoReplyCheck.IsChecked == true, incomingAction);
        if (reply is null)
        {
            return;
        }

        await Dispatcher.InvokeAsync(() =>
        {
            ResultSuccessCheck.IsChecked = reply.ResultSuccess;
            ResultMessageText.Text = reply.ResultMessage;
        });

        await SendActionResultAsync(incomingAction, reply.ResultSuccess, reply.ResultMessage);
        AppendResponse($"[Automation] Auto-replied to '{incomingAction.Name}'.");
    }

    /// <summary>
    /// Strips a source badge prefix ([G] or [S]) from a catalog display entry.
    /// </summary>
    /// <param name="displayEntry">The catalog list entry, possibly prefixed with a badge.</param>
    /// <returns>The plain action name without any badge prefix.</returns>
    private static string StripSourceBadge(string displayEntry)
    {
        if (displayEntry.StartsWith("[G] ", StringComparison.Ordinal) ||
            displayEntry.StartsWith("[S] ", StringComparison.Ordinal) ||
            displayEntry.StartsWith("[I] ", StringComparison.Ordinal))
        {
            return displayEntry.Substring(4);
        }

        return displayEntry;
    }

    /// <summary>
    /// Walks the WPF visual tree to find the first child of type <typeparamref name="T"/>.
    /// Used to locate the <see cref="ScrollViewer"/> inside a <see cref="System.Windows.Controls.ListBox"/>
    /// so the scroll offset can be preserved during in-place item updates.
    /// </summary>
    /// <typeparam name="T">The visual element type to locate.</typeparam>
    /// <param name="parent">The root element to search from.</param>
    /// <returns>The first matching child, or <see langword="null"/> when none is found.</returns>
    private static T? FindVisualChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            System.Windows.DependencyObject child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
                return match;
            T? result = FindVisualChild<T>(child);
            if (result != null)
                return result;
        }
        return null;
    }

    /// <summary>
    /// Registers an action in the Jennifer catalog and quick-action UI.
    /// </summary>
    /// <param name="action">The action metadata to register.</param>
    /// <post>The action is available in the list view and quick action strip exactly once.</post>
    private void RegisterKnownAction(JenniferDiscoveredAction action)
    {
        if (string.IsNullOrWhiteSpace(action.Name))
        {
            return;
        }

        _knownActions.TryGetValue(action.Name, out JenniferDiscoveredAction? existing);

        // Prefer source-parsed metadata (which carries schema/parameters) over a bare
        // game registration that has no schema. Only overwrite if the new entry is richer
        // or there is no existing entry.
        bool existingHasSchema = existing?.Parameters.Count > 0;
        bool incomingHasSchema = action.Parameters.Count > 0;
        if (existingHasSchema && !incomingHasSchema)
        {
            // Keep the existing schema but update the source badge so it shows [G] when the game confirms it.
            if (action.Source == "game")
                existing!.Source = "game";
            // Still refresh the catalog badge in the UI below.
            action = existing!;
        }
        else
        {
            _knownActions[action.Name] = action;
        }

        InvokeOnUiThread(() =>
        {
            // Build display string with source badge: [G] game, [S] source, no badge = manual
            string badge = action.Source switch
            {
                "game"      => "[G] ",
                "source"    => "[S] ",
                "injection" => "[I] ",
                _           => string.Empty,
            };
            string displayEntry = badge + action.Name;

            // Remove stale entry (badge may have changed) then add fresh one.
            // Preserve the current selection and scroll position so badge updates
            // (e.g. [S] -> [G] when the game confirms registration) do not jump the list.
            string? catalogEntry = ActionCatalogList.Items.Cast<string>()
                .FirstOrDefault(e => StripSourceBadge(e).Equals(action.Name, StringComparison.OrdinalIgnoreCase));
            if (catalogEntry == null)
            {
                ActionCatalogList.Items.Add(displayEntry);
            }
            else if (catalogEntry != displayEntry)
            {
                // Badge changed: update in place while keeping selection and scroll stable.
                object? selectedBefore = ActionCatalogList.SelectedItem;
                ScrollViewer? sv = FindVisualChild<ScrollViewer>(ActionCatalogList);
                double scrollBefore = sv?.VerticalOffset ?? 0;

                int idx = ActionCatalogList.Items.IndexOf(catalogEntry);
                ActionCatalogList.Items[idx] = displayEntry;

                // Restore selection — if the updated item was selected, point at the new string.
                if (selectedBefore != null)
                    ActionCatalogList.SelectedItem = selectedBefore.Equals(catalogEntry) ? displayEntry : selectedBefore;

                sv?.ScrollToVerticalOffset(scrollBefore);
            }

            // Quick Action buttons are created for game-registered actions, for source/injection
            // actions that have no parameters, and for injection actions explicitly flagged ShowQuickButton.
            // Actions that declare a schema but whose parameters could not be parsed are excluded —
            // they must be dispatched from the catalog list (which will show the param form).
            bool schemaUnparsed = action.HasSchema && action.Parameters.Count == 0;
            bool wantsQuickButton = !schemaUnparsed &&
                (action.Source == "game" ||
                 ((action.Source == "source" || action.Source == "injection") && action.Parameters.Count == 0));

            if (!wantsQuickButton)
                return;

            bool hasQuickActionButton = ActionButtonsPanel.Children
                .OfType<Button>()
                .Any(button => string.Equals(button.Tag as string, action.Name, StringComparison.OrdinalIgnoreCase));

            if (!hasQuickActionButton)
            {
                Button button = new()
                {
                    Content = action.Name,
                    Tag = action.Name,
                    MinWidth = 94,
                    Margin = new Thickness(0, 0, 8, 8),
                };

                button.Click += ActionButton_Click;
                ActionButtonsPanel.Children.Add(button);
            }
        });
    }

    /// <summary>
    /// Returns the currently registered Jennifer actions in a stable order.
    /// </summary>
    /// <returns>The action metadata known to Jennifer.</returns>
    /// <post>The returned action list is sorted alphabetically to reduce registration churn.</post>
    private IReadOnlyList<JenniferDiscoveredAction> GetRegisteredActions()
    {
        return _knownActions.Values
            .OrderBy(action => action.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Finds the workspace root that contains the NeuroMod action source directory.
    /// </summary>
    /// <returns>The NeuroMod project root, or <c>null</c> when it cannot be found.</returns>
    /// <post>The result is suitable for locating the NeuroMod Actions directory.</post>
    private static string? FindNeuroModRootDirectory()
    {
        string[] startingDirectories =
        {
            AppDomain.CurrentDomain.BaseDirectory,
            Environment.CurrentDirectory,
        };

        foreach (string startingDirectory in startingDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            DirectoryInfo? directory = new(startingDirectory);
            while (directory is not null)
            {
                string candidate = Path.Combine(directory.FullName, "NeuroMod");
                if (Directory.Exists(Path.Combine(candidate, "Actions")))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    /// <summary>
    /// Locates the preferred directory for Jennifer automation files.
    /// </summary>
    /// <returns>The automation directory when found, otherwise the current working directory.</returns>
    /// <post>The returned path is a safe default for the file-open dialog.</post>
    private static string FindDefaultAutomationDirectory()
    {
        string[] candidates =
        {
            Path.Combine(Environment.CurrentDirectory, "tests", "jennifer"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tests", "jennifer"),
        };

        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return Environment.CurrentDirectory;
    }

    /// <summary>
    /// Checks the process command line for a --plan=&lt;path&gt; argument and auto-loads that plan.
    /// </summary>
    /// <post>If a valid --plan argument is present the plan is loaded and auto-reply is set per the plan's AutoRespond flag.</post>
    private void TryAutoLoadPlanFromArgs()
    {
        string[] args = Environment.GetCommandLineArgs();
        string? planArg = Array.Find(args, a => a.StartsWith("--plan=", StringComparison.OrdinalIgnoreCase));
        if (planArg is null)
        {
            return;
        }

        string planPath = planArg["--plan=".Length..].Trim('"');
        if (!File.Exists(planPath))
        {
            AppendResponse($"[Startup] --plan path not found: {planPath}");
            return;
        }

        try
        {
            JenniferAutomationPlan plan = JenniferAutomationPlanLoader.LoadFromFile(planPath);
            ApplyAutomationPlan(plan, planPath);
            AppendResponse($"[Startup] Auto-loaded plan '{plan.Name}' from --plan argument.");
        }
        catch (Exception ex)
        {
            AppendResponse($"[Startup] Failed to auto-load plan from --plan argument: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes the Jennifer ready marker for external runners.
    /// </summary>
    /// <post>The ready marker exists alongside the Jennifer executable when the listener starts.</post>
    private static void WriteReadyMarker()
    {
        try
        {
            File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jennifer_ready.txt"), "ready");
        }
        catch
        {
        }
    }

    /// <summary>
    /// Deletes the Jennifer ready marker when the window closes.
    /// </summary>
    /// <post>The temporary ready marker does not remain on disk after shutdown.</post>
    private static void DeleteReadyMarker()
    {
        try
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "jennifer_ready.txt");
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    /// <summary>
    /// Returns the currently selected force priority.
    /// </summary>
    /// <returns>The configured priority string.</returns>
    /// <post>The returned priority always matches one of the supported Neuro values.</post>
    private string GetSelectedPriority()
    {
        if (PriorityCombo.SelectedItem is ComboBoxItem item && item.Content is string priority)
        {
            return priority;
        }

        return "medium";
    }

    /// <summary>
    /// Updates the header status text.
    /// </summary>
    /// <param name="status">The new status text.</param>
    /// <post>The status banner reflects the latest Jennifer state.</post>
    private void UpdateStatus(string status)
    {
        InvokeOnUiThread(() => StatusText.Text = status);
    }

    /// <summary>
    /// Appends a line to the Jennifer activity log.
    /// </summary>
    /// <param name="message">The message to append.</param>
    /// <post>The activity log scrolls to show the most recent line.</post>
    private void AppendResponse(string message)
    {
        InvokeOnUiThread(() =>
        {
            ResponseLog.AppendText($"{DateTime.Now:HH:mm:ss}  {message}{Environment.NewLine}");
            ResponseLog.ScrollToEnd();
        });
    }

    /// <summary>
    /// Executes a UI update immediately when already on the UI thread or schedules it asynchronously otherwise.
    /// </summary>
    /// <param name="action">The UI work to perform.</param>
    /// <pre>The window dispatcher is available.</pre>
    /// <post>The requested UI work is executed without synchronously blocking a background caller.</post>
    private void InvokeOnUiThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Dispatcher.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
    }

    /// <summary>
    /// Schedules UI work asynchronously on the dispatcher.
    /// </summary>
    /// <param name="action">The UI work to schedule.</param>
    /// <pre>The window dispatcher is available.</pre>
    /// <post>The caller returns immediately while the UI work is queued for later execution.</post>
    private void BeginInvokeOnUiThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.BeginInvoke(action, DispatcherPriority.Background);
    }

    /// <summary>
    /// Normalizes UI-entered text by trimming whitespace and collapsing nulls.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>The trimmed string, or an empty string when the input was null.</returns>
    /// <post>The returned string is safe to serialize into Jennifer payloads.</post>
    private static string NormalizeText(string? text)
    {
        return text?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Determines whether Jennifer currently has an open WebSocket connection.
    /// </summary>
    /// <returns><c>true</c> when the WebSocket is open; otherwise, <c>false</c>.</returns>
    /// <post>The result can be used to decide whether WebSocket sends are allowed.</post>
    private bool IsWebSocketConnected()
    {
        return _webSocket is { State: WebSocketState.Open };
    }
}

