using System;
using System.Net;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Dvin.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Tash;
using Aspenlaub.Net.GitHub.CSharp.TashClient.Components;
using Aspenlaub.Net.GitHub.CSharp.TashClient.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Core;

public class ScriptRunner : IScriptRunner {
    private readonly IDvinRepository _DvinRepository;
    private readonly ISimpleLogger _SimpleLogger;
    private readonly ILogConfiguration _LogConfiguration;
    private readonly IMethodNamesFromStackFramesExtractor _MethodNamesFromStackFramesExtractor;

    public ScriptRunner(IDvinRepository dvinRepository, ISimpleLogger simpleLogger, ILogConfiguration logConfiguration, IMethodNamesFromStackFramesExtractor methodNamesFromStackFramesExtractor) {
        _DvinRepository = dvinRepository;
        _SimpleLogger = simpleLogger;
        _LogConfiguration = logConfiguration;
        _MethodNamesFromStackFramesExtractor = methodNamesFromStackFramesExtractor;
    }

    public async Task<IFindIdleProcessResult> RunScriptAsync(string fileName, IErrorsAndInfos errorsAndInfos) {
        var tashAccessor = new TashAccessor(_DvinRepository, _SimpleLogger, _LogConfiguration, _MethodNamesFromStackFramesExtractor);
        var findIdleProcessResult = await tashAccessor.FindIdleProcess(p => p.Title == ControlledApplication.QualifiedName);
        switch (findIdleProcessResult.BestProcessStatus) {
            case ControllableProcessStatus.DoesNotExist:
                errorsAndInfos.Errors.Add(Properties.Resources.NoProcessShookHandsWithTash);
                return findIdleProcessResult;
            case ControllableProcessStatus.Dead:
                errorsAndInfos.Errors.Add(Properties.Resources.AllProcessesAreDead);
                return findIdleProcessResult;
            case ControllableProcessStatus.Busy:
                errorsAndInfos.Errors.Add(Properties.Resources.ProcessIsBusy);
                return findIdleProcessResult;
        }

        var process = findIdleProcessResult.ControllableProcess;
        await RemotelyResetAsync(process, tashAccessor, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return findIdleProcessResult;
        }

        var scriptName = fileName.Substring(0, fileName.Length - 4);
        scriptName = scriptName.Substring(fileName.LastIndexOf('\\') + 1);
        await RemotelySelectScriptAsync(process, tashAccessor, scriptName, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return findIdleProcessResult;
        }

        await RemotelyStartCoverageAsync(process, tashAccessor, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return findIdleProcessResult;
        }

        await RemotelyExecuteScriptAsync(process, tashAccessor, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return findIdleProcessResult;
        }

        await RemotelyStopCoverageAsync(process, tashAccessor, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            return findIdleProcessResult;
        }

        await RemotelyResetAsync(process, tashAccessor, errorsAndInfos);
        return findIdleProcessResult;
    }

    private static async Task RemotelySelectScriptAsync(ControllableProcess process, ITashAccessor tashAccessor, string scriptName, IErrorsAndInfos errorsAndInfos) {
        var task = new ControllableProcessTask {
            Id = Guid.NewGuid(),
            ProcessId = process.ProcessId,
            Type = ControllableProcessTaskType.SelectComboItem,
            ControlName = "SelectedScript",
            Status = ControllableProcessTaskStatus.Requested,
            Text = scriptName
        };
        var status = await tashAccessor.PutControllableProcessTaskAsync(task);
        if (status != HttpStatusCode.Created) {
            errorsAndInfos.Errors.Add($"Could not create script select request ({status})");
            return;
        }

        task = await tashAccessor.AwaitCompletionAsync(task.Id, 30000);
        if (task.Status == ControllableProcessTaskStatus.Completed) { return; }

        errorsAndInfos.Errors.Add(task.Status == ControllableProcessTaskStatus.Failed && !string.IsNullOrWhiteSpace(task.ErrorMessage) ? task.ErrorMessage : $"Script select request failed ({task.Status})");
    }

    private static async Task RemotelyExecuteScriptAsync(ControllableProcess process, ITashAccessor tashAccessor, IErrorsAndInfos errorsAndInfos) {
        var task = new ControllableProcessTask {
            Id = Guid.NewGuid(),
            ProcessId = process.ProcessId,
            Type = ControllableProcessTaskType.PressButton,
            ControlName = "Play",
            Status = ControllableProcessTaskStatus.Requested
        };
        var status = await tashAccessor.PutControllableProcessTaskAsync(task);
        if (status != HttpStatusCode.Created) {
            errorsAndInfos.Errors.Add($"Could not create play request ({status})");
            return;
        }

        task = await tashAccessor.AwaitCompletionAsync(task.Id, 600000);
        if (task.Status == ControllableProcessTaskStatus.Completed) { return; }

        errorsAndInfos.Errors.Add(task.Status == ControllableProcessTaskStatus.Failed && !string.IsNullOrWhiteSpace(task.ErrorMessage) ? task.ErrorMessage : $"Play request failed ({task.Status})");
    }

    private static async Task RemotelyStartCoverageAsync(ControllableProcess process, ITashAccessor tashAccessor, IErrorsAndInfos errorsAndInfos) {
        var task = new ControllableProcessTask {
            Id = Guid.NewGuid(),
            ProcessId = process.ProcessId,
            Type = ControllableProcessTaskType.PressButton,
            ControlName = "CodeCoverage",
            Status = ControllableProcessTaskStatus.Requested
        };
        var status = await tashAccessor.PutControllableProcessTaskAsync(task);
        if (status != HttpStatusCode.Created) {
            errorsAndInfos.Errors.Add($"Could not request code coverage start ({status})");
            return;
        }

        task = await tashAccessor.AwaitCompletionAsync(task.Id, 60000);
        if (task.Status == ControllableProcessTaskStatus.Completed) { return; }

        errorsAndInfos.Errors.Add(task.Status == ControllableProcessTaskStatus.Failed && !string.IsNullOrWhiteSpace(task.ErrorMessage) ? task.ErrorMessage : $"Code coverage start request failed ({task.Status})");
    }

    private static async Task RemotelyStopCoverageAsync(ControllableProcess process, ITashAccessor tashAccessor, IErrorsAndInfos errorsAndInfos) {
        var task = new ControllableProcessTask {
            Id = Guid.NewGuid(),
            ProcessId = process.ProcessId,
            Type = ControllableProcessTaskType.PressButton,
            ControlName = "StopCodeCoverage",
            Status = ControllableProcessTaskStatus.Requested
        };
        var status = await tashAccessor.PutControllableProcessTaskAsync(task);
        if (status != HttpStatusCode.Created) {
            errorsAndInfos.Errors.Add($"Could not request code coverage stop ({status})");
            return;
        }

        task = await tashAccessor.AwaitCompletionAsync(task.Id, 60000);
        if (task.Status == ControllableProcessTaskStatus.Completed) { return; }

        errorsAndInfos.Errors.Add(task.Status == ControllableProcessTaskStatus.Failed && !string.IsNullOrWhiteSpace(task.ErrorMessage) ? task.ErrorMessage : $"Code coverage stop request failed ({task.Status})");
    }

    private static async Task RemotelyResetAsync(ControllableProcess process, ITashAccessor tashAccessor, IErrorsAndInfos errorsAndInfos) {
        var task = new ControllableProcessTask {
            Id = Guid.NewGuid(),
            ProcessId = process.ProcessId,
            Type = ControllableProcessTaskType.Reset,
            Status = ControllableProcessTaskStatus.Requested
        };
        var status = await tashAccessor.PutControllableProcessTaskAsync(task);
        if (status != HttpStatusCode.Created) {
            errorsAndInfos.Errors.Add($"Could not request reset ({status})");
            return;
        }

        task = await tashAccessor.AwaitCompletionAsync(task.Id, 60000);
        if (task.Status == ControllableProcessTaskStatus.Completed) { return; }

        errorsAndInfos.Errors.Add(task.Status == ControllableProcessTaskStatus.Failed && !string.IsNullOrWhiteSpace(task.ErrorMessage) ? task.ErrorMessage : $"Reset request failed ({task.Status})");
    }
}