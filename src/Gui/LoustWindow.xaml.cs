using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using Aspenlaub.Net.GitHub.CSharp.Dvin.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Loust.Core;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Tash;
using Aspenlaub.Net.GitHub.CSharp.TashClient.Components;
using Aspenlaub.Net.GitHub.CSharp.TashClient.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.VishizhukelNet.Helpers;
using Autofac;
using WindowsApplication = System.Windows.Application;

// ReSharper disable AsyncVoidMethod

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Gui;

/// <summary>
/// Interaction logic for LoustWindow.xaml
/// </summary>
// ReSharper disable once UnusedMember.Global
public partial class LoustWindow : IDisposable {
    public bool IsExecuting { get; set; }
    public bool Abort { get; set; }

    private ITashAccessor TashAccessor { get; }
    private readonly LoustWorker _LoustWorker;
    private DispatcherTimer _DispatcherTimer;
    private SynchronizationContext UiSynchronizationContext { get; }
    private DateTime _UiThreadLastActiveAt, _StatusLastConfirmedAt;
    private readonly int _ProcessId;

    public LoustWindow() {
        InitializeComponent();
        IsExecuting = false;
        Abort = false;
        IContainer container = new ContainerBuilder().UseLoust().Build();
        TashAccessor = new TashAccessor(container.Resolve<IDvinRepository>(), container.Resolve<ISimpleLogger>(),
                                        container.Resolve<ILogConfiguration>(), container.Resolve<IMethodNamesFromStackFramesExtractor>());
        UiSynchronizationContext = SynchronizationContext.Current;
        _ProcessId = Process.GetCurrentProcess().Id;
        UpdateUiThreadLastActiveAt();
        _LoustWorker = new LoustWorker(this, container, TashAccessor);
    }

    private async void OnWindowClosingAsync(object sender, System.ComponentModel.CancelEventArgs e) {
        e.Cancel = true;
        await TashAccessor.ConfirmDeadWhileClosingAsync(_ProcessId);

        await FinishExecutionAsync();
    }

    public void Dispose() {
        _DispatcherTimer?.Stop();
    }

    private async Task FinishExecutionAsync() {
        Abort = true;
        while (IsExecuting) {
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        WindowsApplication.Current.Shutdown();
    }

    private async void CloseButtonClickAsync(object sender, RoutedEventArgs e) {
        await FinishExecutionAsync();
    }

    private async void StartButtonClickAsync(object sender, RoutedEventArgs e) {
        if (IsExecuting) {
            return;
        }

        if (File.Exists(Constants.LastScriptFileName)) {
            File.Delete(Constants.LastScriptFileName);
        }
        await _LoustWorker.StartOrResumeAsync(false, false, false, IgnoreValidationCheckBox.IsChecked ?? false,
            IgnoreUnitTestCheckBox.IsChecked ?? false, IgnoreBroken.IsChecked ?? false);
    }

    private async void ResumeButtonClickAsync(object sender, RoutedEventArgs e) {
        if (IsExecuting) {
            return;
        }

        await _LoustWorker.StartOrResumeAsync(false, false, false, IgnoreValidationCheckBox.IsChecked ?? false,
            IgnoreUnitTestCheckBox.IsChecked ?? false, IgnoreBroken.IsChecked ?? false);
    }

    private async void ShowUncoveredButtonClickAsync(object sender, RoutedEventArgs e) {
        if (IsExecuting) {
            return;
        }

        await _LoustWorker.StartOrResumeAsync(true, false, false, false, false, false);
    }

    private async void OldestFirstButtonClickAsync(object sender, RoutedEventArgs e) {
        if (IsExecuting) {
            return;
        }

        if (File.Exists(Constants.LastScriptFileName)) {
            File.Delete(Constants.LastScriptFileName);
        }
        await _LoustWorker.StartOrResumeAsync(false, true, false, IgnoreValidationCheckBox.IsChecked ?? false,
            IgnoreUnitTestCheckBox.IsChecked ?? false, IgnoreBroken.IsChecked ?? false);
    }

    private async void BrokenButtonClickAsync(object sender, RoutedEventArgs e) {
        if (IsExecuting) {
            return;
        }

        if (File.Exists(Constants.LastScriptFileName)) {
            File.Delete(Constants.LastScriptFileName);
        }
        await _LoustWorker.StartOrResumeAsync(false, false, true, false, false, false);
    }

    private async void CrashTestButtonClickAsync(object sender, RoutedEventArgs e) {
        await Task.Run(() => throw new NotImplementedException());
        MessageBox.Show(Properties.Resources.PleaseCloseTheApplication);
    }

    private async Task ConnectAndMakeTashRegistrationAsync() {
        IErrorsAndInfos tashErrorsAndInfos = await TashAccessor.EnsureTashAppIsRunningAsync();
        if (tashErrorsAndInfos.AnyErrors()) {
            MessageBox.Show(string.Join("\r\n", tashErrorsAndInfos.Errors), Properties.Resources.CouldNotConnectToTash, MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }

        HttpStatusCode statusCode = await TashAccessor.PutControllableProcessAsync(Process.GetCurrentProcess());
        if (statusCode != HttpStatusCode.Created) {
            MessageBox.Show(string.Format(Properties.Resources.CouldNotMakeTashRegistration, statusCode.ToString()), Properties.Resources.LoustWindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }

        await TashAccessor.AssumeDeath(p => p.Title == ControlledApplication.QualifiedName);
    }

    private async void OnLoustWindowLoadedAsync(object sender, RoutedEventArgs e) {
        await ConnectAndMakeTashRegistrationAsync();
        CreateAndStartTimer();
        await ExceptionHandler.RunAsync(WindowsApplication.Current, TimeSpan.FromSeconds(7));
    }

    private void CreateAndStartTimer() {
        _DispatcherTimer = new DispatcherTimer();
        _DispatcherTimer.Tick += LoustWindow_TickAsync;
        _DispatcherTimer.Interval = TimeSpan.FromSeconds(7);
        _DispatcherTimer.Start();
    }

    private async void LoustWindow_TickAsync(object sender, EventArgs e) {
        UiSynchronizationContext.Send(_ => UpdateUiThreadLastActiveAt(), null);
        if (_StatusLastConfirmedAt == _UiThreadLastActiveAt) { return; }

        HttpStatusCode statusCode = HttpStatusCode.NotFound;
        for (int attempts = 10; attempts > 0; attempts --) {
            try {
                statusCode = await TashAccessor.ConfirmAliveAsync(_ProcessId, _UiThreadLastActiveAt, ControllableProcessStatus.Busy);
                if (statusCode != HttpStatusCode.NoContent) {
                    continue;
                }

                _StatusLastConfirmedAt = _UiThreadLastActiveAt;
                UiSynchronizationContext.Post(_ => ShowLastCommunicatedTimeStamp(), null);
                return;
            } catch {
                statusCode = HttpStatusCode.InternalServerError;
            }

            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        UiSynchronizationContext.Post(_ => CommunicateCouldNotConfirmStatusToTashThenStop(statusCode), null);
    }

    private void CommunicateCouldNotConfirmStatusToTashThenStop(HttpStatusCode statusCode) {
        var p = new Paragraph(new Run(string.Format(Properties.Resources.CouldNotConfirmStatusToTash, statusCode.ToString()))) {
            Foreground = Brushes.Red
        };
        AnalysisResult.Blocks.Add(p);
        AnalysisResultBox.ScrollToEnd();

        StopCheckBox.IsChecked = true;
    }

    private void UpdateUiThreadLastActiveAt() {
        if (Dispatcher?.CheckAccess() != true) {
            MessageBox.Show(Properties.Resources.ConfirmationToTashNotFromUiThread, Properties.Resources.LoustWindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
            return;
        }

        _UiThreadLastActiveAt = DateTime.Now;
    }

    private void ShowLastCommunicatedTimeStamp() {
        StatusConfirmedAt.Text = _StatusLastConfirmedAt.Year > 2000 ? _StatusLastConfirmedAt.ToLongTimeString() : "";
    }
}