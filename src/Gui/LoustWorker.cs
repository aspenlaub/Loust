using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Aspenlaub.Net.GitHub.CSharp.Loust.Core;
using Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Helpers;
using Aspenlaub.Net.GitHub.CSharp.Tash;
using Aspenlaub.Net.GitHub.CSharp.TashClient.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Vishizhukel.Web;
using Autofac;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Gui;

class LoustWorker(LoustWindow window, IContainer container, ITashAccessor tashAccessor) {
    public async Task StartOrResumeAsync(bool showUncoveredOnly, bool oldestFirst,
            bool broken, bool ignoreValidation,
            bool ignoreUnitTest, bool ignoreBroken) {
        if (window.StatusConfirmedAt.Text == "") {
            MessageBox.Show(Properties.Resources.NotConnectedToTashYet, Properties.Resources.LoustWindowTitle, MessageBoxButton.OK, MessageBoxImage.Hand);
            return;
        }

        Cursor oldCursor = window.AnalysisResultBox.Cursor;
        window.AnalysisResultBox.Cursor = Cursors.Wait;
        window.AnalysisResult.Blocks.Clear();
        window.IsExecuting = true;
        window.Abort = false;
        ICoverageFinder coverageFinder = container.Resolve<ICoverageFinder>();
        IList<string> lastModifiedPhpFilesWithoutCoverage = await coverageFinder.GetLastModifiedPhpFilesWithoutCoverageAsync();
        foreach (Paragraph p2 in lastModifiedPhpFilesWithoutCoverage.Select(fileName => new Paragraph(new Run(string.Format(Properties.Resources.NoCoverageFor, fileName))) { Foreground = Brushes.Red })) {
            window.AnalysisResult.Blocks.Add(p2);
        }

        Paragraph p;
        var gate = new HttpGate();
        bool localHostIsAvailable = await gate.IsLocalHostAvailableAsync();
        if (!localHostIsAvailable) {
            p = new Paragraph(new Run(Properties.Resources.LocalHostNotAvailable)) {
                Foreground = Brushes.Red
            };
            window.AnalysisResult.Blocks.Add(p);
            window.AnalysisResultBox.ScrollToEnd();
        }

        bool sqlServerIsAvailable = Process.GetProcesses().Any(proc => proc.ProcessName.ToUpper().Contains("SQLSERVR"));
        if (!sqlServerIsAvailable) {
            p = new Paragraph(new Run(Properties.Resources.SqlServerNotAvailable)) {
                Foreground = Brushes.Red
            };
            window.AnalysisResult.Blocks.Add(p);
            window.AnalysisResultBox.ScrollToEnd();
        }

        if (localHostIsAvailable && sqlServerIsAvailable && !showUncoveredOnly) {
            IList<string> scriptFileNames = await coverageFinder.GetOrderedScriptFileNamesAsync(oldestFirst, broken, ignoreValidation,
                ignoreUnitTest);
            if (oldestFirst) {
                scriptFileNames = scriptFileNames.Reverse().ToList();
            }
            IScriptRunner runner = container.Resolve<IScriptRunner>();
            bool lastScriptFound = !File.Exists(Constants.LastScriptFileName);
            string lastScriptName = lastScriptFound ? "" : await File.ReadAllTextAsync(Constants.LastScriptFileName);
            IBrokenTestCaseRepository brokenTestCaseRepository = container.Resolve<IBrokenTestCaseRepository>();
            int numberOfBrokenTests = await brokenTestCaseRepository.NumberOfBrokenTestsAsync();
            if (numberOfBrokenTests > 0) {
                p = new Paragraph(new Run($"{numberOfBrokenTests} broken test case/-s registered and awaiting resolution")) {
                    Foreground = Brushes.Red
                };
                window.AnalysisResult.Blocks.Add(p);
                window.AnalysisResultBox.ScrollToEnd();
            }
            await ProcessScriptFileNames(broken, ignoreBroken, scriptFileNames, brokenTestCaseRepository,
                lastScriptFound, lastScriptName, runner);
        }

        p = new Paragraph(new Run(Properties.Resources.Done)) {
            Foreground = Brushes.LawnGreen
        };
        window.AnalysisResult.Blocks.Add(p);
        window.AnalysisResultBox.ScrollToEnd();

        window.StopCheckBox.IsChecked = false;

        window.IsExecuting = false;
        window.AnalysisResultBox.Cursor = oldCursor;
        window.AnalysisResultBox.ScrollToEnd();
    }

    private async Task ProcessScriptFileNames(bool broken, bool ignoreBroken, IList<string> scriptFileNames, IBrokenTestCaseRepository brokenTestCaseRepository, bool lastScriptFound,
                                              string lastScriptName, IScriptRunner runner) {
        bool firstScript = true;
        // ReSharper disable once LoopCanBePartlyConvertedToQuery
        foreach (string scriptFileName in scriptFileNames) {
            if (!firstScript && window.StopCheckBox.IsChecked == true) {
                break;
            }

            if (broken && !await brokenTestCaseRepository.ContainsAsync(scriptFileName)) { continue; }
            if (ignoreBroken && await brokenTestCaseRepository.ContainsAsync(scriptFileName)) { continue; }

            firstScript = false;
            string shortName = scriptFileName.Substring(scriptFileName.LastIndexOf('\\') + 1);
            shortName = shortName.Substring(0, shortName.LastIndexOf('.'));
            if (!lastScriptFound && shortName != lastScriptName) {
                continue;
            }

            lastScriptFound = true;
            await File.WriteAllTextAsync(Constants.LastScriptFileName, shortName);
            await ProcessScriptFile(brokenTestCaseRepository, runner, scriptFileName, shortName);

            if (window.Abort) {
                break;
            }
        }
    }

    private async Task ProcessScriptFile(IBrokenTestCaseRepository brokenTestCaseRepository,
            IScriptRunner runner, string scriptFileName, string shortName) {
        bool tryAgain;
        int attempts = await brokenTestCaseRepository.ContainsAsync(scriptFileName) ? 1 : 3;
        do {
            tryAgain = false;
            Paragraph p = new Paragraph(new Run(string.Format(Properties.Resources.RunningScript, shortName)));
            window.AnalysisResult.Blocks.Add(p);
            var errorsAndInfos = new ErrorsAndInfos();
            IFindIdleProcessResult findIdleProcessResult = await runner.RunScriptAsync(scriptFileName, errorsAndInfos);
            if (!errorsAndInfos.AnyErrors()) {
                await brokenTestCaseRepository.RemoveAsync(scriptFileName);
                p = new Paragraph(new Run(Properties.Resources.ScriptExecutedWithoutErrors)) { Foreground = Brushes.LawnGreen };
                window.AnalysisResult.Blocks.Add(p);
                window.AnalysisResultBox.ScrollToEnd();
                await Task.Delay(TimeSpan.FromSeconds(5));
                continue;
            }

            if (findIdleProcessResult.BestProcessStatus == ControllableProcessStatus.Busy) {
                p = new Paragraph(new Run(string.Join("\r\n", errorsAndInfos.Errors))) {
                    Foreground = Brushes.Yellow
                };
                window.AnalysisResult.Blocks.Add(p);
                window.AnalysisResultBox.ScrollToEnd();
                tryAgain = true;
                window.Abort = false;
                await Task.Delay(TimeSpan.FromSeconds(5));
                continue;
            } 

            bool badRequest = errorsAndInfos.Errors.Any(e => e.Contains("BadRequest"));
            tryAgain = --attempts > 0 && !badRequest && window.StopCheckBox.IsChecked != true;
            IList<string> errors = errorsAndInfos.Errors;
            /*
                if (tryAgain || StopCheckBox.IsChecked != true) {
                    var fileName = ScreenShooter.TakeScreenShot();
                    errors.Add("Screen shot saved in " + fileName);
                }
            */
            p = new Paragraph(new Run(string.Join("\r\n", errors))) {
                Foreground = Brushes.Red
            };
            window.AnalysisResult.Blocks.Add(p);
            window.AnalysisResultBox.ScrollToEnd();
            if (!tryAgain) {
                await brokenTestCaseRepository.RegisterAsync(scriptFileName, errorsAndInfos.Errors);
                p = new Paragraph(new Run(Properties.Resources.StartScriptRecovery)) {
                    Foreground = Brushes.Yellow
                };
                window.AnalysisResult.Blocks.Add(p);
                window.AnalysisResultBox.ScrollToEnd();
                bool success = await runner.RecoverScriptAsync(scriptFileName);
                p = new Paragraph(new Run(success ? Properties.Resources.ScriptRecoveryEnded : Properties.Resources.ScriptRecoveryFailed)) {
                    Foreground = success ? Brushes.LightGreen : Brushes.Red
                };
                window.AnalysisResult.Blocks.Add(p);
                window.AnalysisResultBox.ScrollToEnd();
                if (window.StopCheckBox.IsChecked == true) {
                    return;
                }
            } else if (findIdleProcessResult.BestProcessStatus != ControllableProcessStatus.Dead || window.Abort) {
                continue;
            }

            IList<ControllableProcess> controllableProcesses = await tashAccessor.GetControllableProcessesAsync();
            ControllableProcess controllableProcess = controllableProcesses.FirstOrDefault(pr
               => pr.Title == ControlledApplication.QualifiedName
                    && !pr.LaunchCommand.Contains("Debug")
                    && !pr.LaunchCommand.Contains("Temp"));
            if (controllableProcess == null) {
                continue;
            }

            p = new Paragraph(new Run(Properties.Resources.KillingProcesses)) {
                Foreground = Brushes.Yellow
            };
            window.AnalysisResult.Blocks.Add(p);
            window.AnalysisResultBox.ScrollToEnd();

            foreach (Process process in Process.GetProcessesByName(ControlledApplication.QualifiedName)) {
                process.Kill(true);
            }

            await Wait.UntilAsync(() => Task.FromResult(!Process.GetProcessesByName(ControlledApplication.QualifiedName).Any()), TimeSpan.FromMinutes(1));
            if (Process.GetProcessesByName(ControlledApplication.QualifiedName).Any()) {
                p = new Paragraph(new Run(Properties.Resources.FailedToKillProcesses)) {
                    Foreground = Brushes.Red
                };
                window.AnalysisResult.Blocks.Add(p);
                window.AnalysisResultBox.ScrollToEnd();
                window.Abort = true;
                return;
            }

            p = new Paragraph(new Run(Properties.Resources.Restarting)) {
                Foreground = Brushes.Yellow
            };
            window.AnalysisResult.Blocks.Add(p);
            window.AnalysisResultBox.ScrollToEnd();

            string launchCommand = controllableProcess.LaunchCommand;
            int pos = launchCommand.IndexOf(' ');
            string executable = pos >= 0 ? launchCommand.Substring(0, pos) : launchCommand;
            string arguments = pos >= 0 ? launchCommand.Substring(pos + 1) : "";
            StartProcess(executable, arguments, "");
            await Task.Delay(TimeSpan.FromSeconds(20));
            await Wait.UntilAsync(() => Task.FromResult(Process.GetProcessesByName(ControlledApplication.QualifiedName).Any()), TimeSpan.FromMinutes(1));
        } while (tryAgain && !window.Abort);
    }

    private static void StartProcess(string executableFullName, string arguments, string workingFolder) {
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