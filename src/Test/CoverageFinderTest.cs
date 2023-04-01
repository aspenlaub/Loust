using System;
using System.IO;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Loust.Core;
using Aspenlaub.Net.GitHub.CSharp.Loust.Entities;
using Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Test;

[TestClass]
public class CoverageFinderTest {
    private IContainer _Container;
    private IScriptFinder _ScriptFinder;
    private ISecretRepository _SecretRepository;

    [TestInitialize]
    public void Initialize() {
        _Container = new ContainerBuilder().UseLoust().Build();
        _ScriptFinder = _Container.Resolve<IScriptFinder>();
        _SecretRepository = _Container.Resolve<ISecretRepository>();
    }

    [TestMethod]
    public async Task CanGetCoverageFileForScriptFile() {
        var errorsAndInfos = new ErrorsAndInfos();
        var loustSettings = await _SecretRepository.GetAsync(new SecretLoustSettings(), errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        var trivialTest = loustSettings.TrivialTest;
        var scriptFileName = await _ScriptFinder.ScriptFolderAsync(errorsAndInfos) + trivialTest;
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        Assert.IsTrue(File.Exists(scriptFileName));
        var sut = new TestCaseFileNameShortener();
        var folder = await _Container.Resolve<IFolderResolver>().ResolveAsync(@"$(WampRoot)\temp\coverage", errorsAndInfos);
        var coverageFileName = sut.CoverageFileForScriptFile(folder, scriptFileName);
        var expectedCoverageFileName = folder.FullName + @"\oust_"
            + trivialTest.Substring(trivialTest.LastIndexOf(@"\", StringComparison.InvariantCulture) + 1).ToLower().Replace(' ', '_').Replace(".xml", ".txt");
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        Assert.AreEqual(expectedCoverageFileName, coverageFileName);
    }

    [TestMethod]
    public async Task CanGetSortValue() {
        var sut = _Container.Resolve<ICoverageFinder>();
        var orderedScriptFileNames = await sut.GetOrderedScriptFileNamesAsync(false, true, false, false);
        var sortValue = "";
        foreach (var scriptFileName in orderedScriptFileNames) {
            Assert.IsTrue(File.Exists(scriptFileName));
            var newSortValue = sut.SortValueForScriptFile(scriptFileName, false);
            Assert.IsTrue(sortValue == "" || string.CompareOrdinal(sortValue, newSortValue) >= 0);
            sortValue = newSortValue;
        }
    }
}