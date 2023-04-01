using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Loust.Core;
using Aspenlaub.Net.GitHub.CSharp.Loust.Entities;
using Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Tash;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Test;

[TestClass]
public class ScriptRunnerTest {
    private readonly IScriptFinder _ScriptFinder;
    private readonly IContainer _Container;
    private readonly ISecretRepository _SecretRepository;

    public ScriptRunnerTest() {
        _Container = new ContainerBuilder().UseLoust().Build();
        _ScriptFinder = _Container.Resolve<IScriptFinder>();
        _SecretRepository = _Container.Resolve<ISecretRepository>();
    }

    [TestMethod]
    public async Task CanRunScript() {
        var sut = _Container.Resolve<IScriptRunner>();
        var errorsAndInfos = new ErrorsAndInfos();
        var folder = await _ScriptFinder.ScriptFolderAsync(errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        var loustSettings = await _SecretRepository.GetAsync(new SecretLoustSettings(), errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        var fileName = Directory.GetFiles(folder, loustSettings.ScriptWildcard, SearchOption.AllDirectories).MinBy(s => s);
        Assert.IsNotNull(fileName);
        var findIdleProcessResult = await sut.RunScriptAsync(fileName, errorsAndInfos);
        if (findIdleProcessResult.BestProcessStatus == ControllableProcessStatus.DoesNotExist || findIdleProcessResult.BestProcessStatus == ControllableProcessStatus.Dead) {
            Assert.Inconclusive(errorsAndInfos.Errors.FirstOrDefault(e => e.Contains("No " + ControlledApplication.Name + " process")));
        }
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
    }

    [TestMethod]
    public async Task CanRunAnotherScript() {
        var sut = _Container.Resolve<IScriptRunner>();
        var errorsAndInfos = new ErrorsAndInfos();
        var folder = await _ScriptFinder.ScriptFolderAsync(errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        var loustSettings = await _SecretRepository.GetAsync(new SecretLoustSettings(), errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        var fileName = Directory.GetFiles(folder, loustSettings.AnotherScriptWildcard, SearchOption.AllDirectories).MinBy(s => s);
        Assert.IsNotNull(fileName);
        var findIdleProcessResult = await sut.RunScriptAsync(fileName, errorsAndInfos);
        if (findIdleProcessResult.BestProcessStatus == ControllableProcessStatus.DoesNotExist || findIdleProcessResult.BestProcessStatus == ControllableProcessStatus.Dead) {
            Assert.Inconclusive(errorsAndInfos.Errors.FirstOrDefault(e => e.Contains("No " + ControlledApplication.Name + " process")));
        }
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
    }

    [TestMethod]
    public async Task CanRunYetAnotherScript() {
        var sut = _Container.Resolve<IScriptRunner>();
        var errorsAndInfos = new ErrorsAndInfos();
        var folder = await _ScriptFinder.ScriptFolderAsync(errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        var loustSettings = await _SecretRepository.GetAsync(new SecretLoustSettings(), errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        var fileName = Directory.GetFiles(folder, loustSettings.YetAnotherScriptWildcard, SearchOption.AllDirectories).MinBy(s => s);
        Assert.IsNotNull(fileName);
        await TryRunningYetAnotherScript(sut, fileName, errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            errorsAndInfos = new ErrorsAndInfos();
            await TryRunningYetAnotherScript(sut, fileName, errorsAndInfos);
        }
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
    }

    private static async Task TryRunningYetAnotherScript(IScriptRunner sut, string fileName, IErrorsAndInfos errorsAndInfos) {
        var findIdleProcessResult = await sut.RunScriptAsync(fileName, errorsAndInfos);
        if (findIdleProcessResult.BestProcessStatus == ControllableProcessStatus.DoesNotExist || findIdleProcessResult.BestProcessStatus == ControllableProcessStatus.Dead) {
            Assert.Inconclusive(errorsAndInfos.Errors.FirstOrDefault(e => e.Contains("No " + ControlledApplication.Name + " process")));
        }
    }

    [TestMethod]
    public async Task HaveEnoughResultsToIgnoreFiles() {
        var shortener = _Container.Resolve<ITestCaseFileNameShortener>();
        var coverageFinder = _Container.Resolve<ICoverageFinder>();
        var errorsAndInfos = new ErrorsAndInfos();
        var folder = await _ScriptFinder.ScriptFolderAsync(errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        var fileNames = Directory.GetFiles(folder, "*unit*test*.xml").OrderBy(s => s).ToList();
        fileNames = fileNames.Where(f => coverageFinder.NumberOfResults(shortener.CoverageFileForScriptFileShortName(f)) == 0).ToList();
        for (var i = 0; i < fileNames.Count && coverageFinder.NumberOfResults("*unit*test*.txt") < 25; i++) {
            var scriptRunner = _Container.Resolve<IScriptRunner>();
            var findIdleProcessResult = await scriptRunner.RunScriptAsync(fileNames[i], errorsAndInfos);
            if (findIdleProcessResult.BestProcessStatus == ControllableProcessStatus.DoesNotExist || findIdleProcessResult.BestProcessStatus == ControllableProcessStatus.Dead) {
                Assert.Inconclusive(errorsAndInfos.Errors.FirstOrDefault(e => e.Contains("No " + ControlledApplication.Name + " process")));
            }
            Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        }
    }
}