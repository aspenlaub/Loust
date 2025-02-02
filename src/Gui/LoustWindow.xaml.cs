using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Aspenlaub.Net.GitHub.CSharp.Dvin.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Loust.Core;
using Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Helpers;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Tash;
using Aspenlaub.Net.GitHub.CSharp.TashClient.Components;
using Aspenlaub.Net.GitHub.CSharp.TashClient.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Vishizhukel.Web;
using Aspenlaub.Net.GitHub.CSharp.VishizhukelNet.Helpers;
using Autofac;
using WindowsApplication = System.Windows.Application;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Gui;

/// <summary>
/// Interaction logic for LoustWindow.xaml
/// </summary>
// ReSharper disable once UnusedMember.Global
public partial class LoustWindow : IDisposable {
    private bool IsExecuting { get; set; }
    private bool Abort { get; set; }
    private const string LastScriptFileName = @"c:\temp\LastLoust.txt";
    private ITashAccessor TashAccessor { get; }
    private DispatcherTimer _DispatcherTimer;
    private SynchronizationContext UiSynchronizationContext { get; }
    private DateTime _UiThreadLastActiveAt, _StatusLastConfirmedAt;
    private readonly int _ProcessId;
    private readonly IContainer _Container;

    public LoustWindow() {
        InitializeComponent();
        IsExecuting = false;
        Abort = false;
        _Container = new ContainerBuilder().UseLoust().Build();
        TashAccessor = new TashAccessor(_Container.Resolve<IDvinRepository>(), _Container.Resolve<ISimpleLogger>(),
                                        _Container.Resolve<ILogConfiguration>(), _Container.Resolve<IMethodNamesFromStackFramesExtractor>());
        UiSynchronizationContext = SynchronizationContext.Current;
        _ProcessId = Process.GetCurrentProcess().Id;
        UpdateUiThreadLastActiveAt();
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

        if (File.Exists(LastScriptFileName)) {
            File.Delete(LastScriptFileName);
        }
        await StartOrResumeAsync(false, false, false, IgnoreValidationCheckBox.IsChecked ?? false, IgnoreUnitTestCheckBox.IsChecked ?? false);
    }

    private async void ResumeButtonClickAsync(object sender, RoutedEventArgs e) {
        if (IsExecuting) {
            return;
        }

        await StartOrResumeAsync(false, false, false, IgnoreValidationCheckBox.IsChecked ?? false, IgnoreUnitTestCheckBox.IsChecked ?? false);
    }

    private async void ShowUncoveredButtonClickAsync(object sender, RoutedEventArgs e) {
        if (IsExecuting) {
            return;
        }

        await StartOrResumeAsync(true, false, false, false, false);
    }

    private async Task StartOrResumeAsync(bool showUncoveredOnly, bool oldestFirst, bool broken, bool ignoreValidation, bool ignoreUnitTest) {
        if (StatusConfirmedAt.Text == "") {
            MessageBox.Show(Properties.Resources.NotConnectedToTashYet, Properties.Resources.LoustWindowTitle, MessageBoxButton.OK, MessageBoxImage.Hand);
            return;
        }

        var oldCursor = AnalysisResultBox.Cursor;
        AnalysisResultBox.Cursor = Cursors.Wait;
        AnalysisResult.Blocks.Clear();
        IsExecuting = true;
        Abort = false;
        var coverageFinder = _Container.Resolve<ICoverageFinder>();
        var lastModifiedPhpFilesWithoutCoverage = await coverageFinder.GetLastModifiedPhpFilesWithoutCoverageAsync();
        foreach (var p2 in lastModifiedPhpFilesWithoutCoverage.Select(fileName => new Paragraph(new Run(string.Format(Properties.Resources.NoCoverageFor, fileName))) {Foreground = Brushes.Red})) {
            AnalysisResult.Blocks.Add(p2);
        }

        Paragraph p;
        var gate = new HttpGate();
        var localHostIsAvailable = await gate.IsLocalHostAvailableAsync();
        if (!localHostIsAvailable) {
            p = new Paragraph(new Run(Properties.Resources.LocalHostNotAvailable)) {
                Foreground = Brushes.Red
            };
            AnalysisResult.Blocks.Add(p);
            AnalysisResultBox.ScrollToEnd();
        }

        var sqlServerIsAvailable = Process.GetProcesses().Any(proc => proc.ProcessName.ToUpper().Contains("SQLSERVR"));
        if (!sqlServerIsAvailable) {
            p = new Paragraph(new Run(Properties.Resources.SqlServerNotAvailable)) {
                Foreground = Brushes.Red
            };
            AnalysisResult.Blocks.Add(p);
            AnalysisResultBox.ScrollToEnd();
        }

        if (localHostIsAvailable && sqlServerIsAvailable && !showUncoveredOnly) {
            var scriptFileNames = await coverageFinder.GetOrderedScriptFileNamesAsync(oldestFirst, broken, ignoreValidation, ignoreUnitTest);
            if (oldestFirst) {
                scriptFileNames = scriptFileNames.Reverse().ToList();
            }
            var runner = _Container.Resolve<IScriptRunner>();
            var lastScriptFound = !File.Exists(LastScriptFileName);
            var lastScriptName = lastScriptFound ? "" : await File.ReadAllTextAsync(LastScriptFileName);
            var brokenTestCaseRepository = _Container.Resolve<IBrokenTestCaseRepository>();
            var numberOfBrokenTests = await brokenTestCaseRepository.NumberOfBrokenTestsAsync();
            if (numberOfBrokenTests > 0) {
                p = new Paragraph(new Run($"{numberOfBrokenTests} broken test case/-s registered and awaiting resolution")) {
                    Foreground = Brushes.Red
                };
                AnalysisResult.Blocks.Add(p);
                AnalysisResultBox.ScrollToEnd();
            }
            var firstScript = true;
            // ReSharper disable once LoopCanBePartlyConvertedToQuery
            foreach (var scriptFileName in scriptFileNames) {
                if (!firstScript && StopCheckBox.IsChecked == true) {
                    break;
                }

                if (broken && !await brokenTestCaseRepository.ContainsAsync(scriptFileName)) { continue; }

                firstScript = false;
                var shortName = scriptFileName.Substring(scriptFileName.LastIndexOf('\\') + 1);
                shortName = shortName.Substring(0, shortName.LastIndexOf('.'));
                if (!lastScriptFound && shortName != lastScriptName) {
                    continue;
                }

                lastScriptFound = true;
                await File.WriteAllTextAsync(LastScriptFileName, shortName);
                bool tryAgain;
                var attempts = await brokenTestCaseRepository.ContainsAsync(scriptFileName) ? 1 : 3;
                do {
                    tryAgain = false;
                    p = new Paragraph(new Run(string.Format(Properties.Resources.RunningScript, shortName)));
                    AnalysisResult.Blocks.Add(p);
                    var errorsAndInfos = new ErrorsAndInfos();
                    var findIdleProcessResult = await runner.RunScriptAsync(scriptFileName, errorsAndInfos);
                    if (!errorsAndInfos.AnyErrors()) {
                        await brokenTestCaseRepository.RemoveAsync(scriptFileName);
                        p = new Paragraph(new Run(Properties.Resources.ScriptExecutedWithoutErrors)) {
                            Foreground = Brushes.LawnGreen
                        };
                        AnalysisResult.Blocks.Add(p);
                        AnalysisResultBox.ScrollToEnd();
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    } else if (findIdleProcessResult.BestProcessStatus == ControllableProcessStatus.Busy) {
                        p = new Paragraph(new Run(string.Join("\r\n", errorsAndInfos.Errors))) {
                            Foreground = Brushes.Yellow
                        };
                        AnalysisResult.Blocks.Add(p);
                        AnalysisResultBox.ScrollToEnd();
                        tryAgain = true;
                        Abort = false;
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    } else {
                        var badRequest = errorsAndInfos.Errors.Any(e => e.Contains("BadRequest"));
                        tryAgain = --attempts > 0 && !badRequest && StopCheckBox.IsChecked != true;
                        var errors = errorsAndInfos.Errors;
                        /*
                        if (tryAgain || StopCheckBox.IsChecked != true) {
                            var fileName = ScreenShooter.TakeScreenShot();
                            errors.Add("Screen shot saved in " + fileName);
                        }
                        */
                        p = new Paragraph(new Run(string.Join("\r\n", errors))) {
                            Foreground = Brushes.Red
                        };
                        AnalysisResult.Blocks.Add(p);
                        AnalysisResultBox.ScrollToEnd();
                        if (!tryAgain) {
                            await brokenTestCaseRepository.RegisterAsync(scriptFileName, errorsAndInfos.Errors);
                            if (StopCheckBox.IsChecked == true) {
                                break;
                            }
                        } else if (findIdleProcessResult.BestProcessStatus != ControllableProcessStatus.Dead || Abort) {
                            continue;
                        }

                        var controllableProcesses = await TashAccessor.GetControllableProcessesAsync();
                        var controllableProcess = controllableProcesses.FirstOrDefault(pr
                                => pr.Title == ControlledApplication.QualifiedName
                                    && !pr.LaunchCommand.Contains("Debug")
                                    && !pr.LaunchCommand.Contains("Temp"));
                        if (controllableProcess == null) {
                            continue;
                        }

                        p = new Paragraph(new Run(Properties.Resources.KillingProcesses)) {
                            Foreground = Brushes.Yellow
                        };
                        AnalysisResult.Blocks.Add(p);
                        AnalysisResultBox.ScrollToEnd();

                        foreach (var process in Process.GetProcessesByName(ControlledApplication.QualifiedName)) {
                            process.Kill(true);
                        }

                        await Wait.UntilAsync(() => Task.FromResult(!Process.GetProcessesByName(ControlledApplication.QualifiedName).Any()), TimeSpan.FromMinutes(1));
                        if (Process.GetProcessesByName(ControlledApplication.QualifiedName).Any()) {
                            p = new Paragraph(new Run(Properties.Resources.FailedToKillProcesses)) {
                                Foreground = Brushes.Red
                            };
                            AnalysisResult.Blocks.Add(p);
                            AnalysisResultBox.ScrollToEnd();
                            Abort = true;
                            break;
                        }

                        p = new Paragraph(new Run(Properties.Resources.Restarting)) {
                            Foreground = Brushes.Yellow
                        };
                        AnalysisResult.Blocks.Add(p);
                        AnalysisResultBox.ScrollToEnd();

                        var launchCommand = controllableProcess.LaunchCommand;
                        var pos = launchCommand.IndexOf(' ');
                        var executable = pos >= 0 ? launchCommand.Substring(0, pos) : launchCommand;
                        var arguments = pos >= 0 ? launchCommand.Substring(pos + 1) : "";
                        StartProcess(executable, arguments, "");
                        await Task.Delay(TimeSpan.FromSeconds(20));
                    }
                } while (tryAgain && !Abort);

                if (Abort) {
                    break;
                }
            }
        }

        p = new Paragraph(new Run(Properties.Resources.Done)) {
            Foreground = Brushes.LawnGreen
        };
        AnalysisResult.Blocks.Add(p);
        AnalysisResultBox.ScrollToEnd();

        StopCheckBox.IsChecked = false;

        IsExecuting = false;
        AnalysisResultBox.Cursor = oldCursor;
        AnalysisResultBox.ScrollToEnd();
    }

    private async void OldestFirstButtonClickAsync(object sender, RoutedEventArgs e) {
        if (IsExecuting) {
            return;
        }

        if (File.Exists(LastScriptFileName)) {
            File.Delete(LastScriptFileName);
        }
        await StartOrResumeAsync(false, true, false, IgnoreValidationCheckBox.IsChecked ?? false, IgnoreUnitTestCheckBox.IsChecked ?? false);
    }

    private async void BrokenButtonClickAsync(object sender, RoutedEventArgs e) {
        if (IsExecuting) {
            return;
        }

        if (File.Exists(LastScriptFileName)) {
            File.Delete(LastScriptFileName);
        }
        await StartOrResumeAsync(false, false, true, false, false);
    }

    private async void CrashTestButtonClickAsync(object sender, RoutedEventArgs e) {
        await Task.Run(() => { throw new NotImplementedException(); });
        MessageBox.Show(Properties.Resources.PleaseCloseTheApplication);
    }

    private async Task ConnectAndMakeTashRegistrationAsync() {
        var tashErrorsAndInfos = await TashAccessor.EnsureTashAppIsRunningAsync();
        if (tashErrorsAndInfos.AnyErrors()) {
            MessageBox.Show(string.Join("\r\n", tashErrorsAndInfos.Errors), Properties.Resources.CouldNotConnectToTash, MessageBoxButton.OK, MessageBoxImage.Error);
            Close();
        }

        var statusCode = await TashAccessor.PutControllableProcessAsync(Process.GetCurrentProcess());
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

        var statusCode = await TashAccessor.ConfirmAliveAsync(_ProcessId, _UiThreadLastActiveAt, ControllableProcessStatus.Busy);
        if (statusCode == HttpStatusCode.NoContent) {
            _StatusLastConfirmedAt = _UiThreadLastActiveAt;
            UiSynchronizationContext.Post(_ => ShowLastCommunicatedTimeStamp(), null);
            return;
        }

        UiSynchronizationContext.Post(_ => { CommunicateCouldNotConfirmStatusToTashThenStop(statusCode); }, null);
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

    private void StartProcess(string executableFullName, string arguments, string workingFolder) {
        var process = new Process {
            StartInfo = {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                FileName = executableFullName,
                Arguments = arguments,
                WorkingDirectory = workingFolder,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
    }
}