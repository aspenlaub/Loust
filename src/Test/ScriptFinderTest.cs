using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Loust.Core;
using Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: DoNotParallelize]
namespace Aspenlaub.Net.GitHub.CSharp.Loust.Test;

[TestClass]
public class ScriptFinderTest {
    [TestMethod]
    public async Task CanFindScriptFiles() {
        IContainer container = new ContainerBuilder().UseLoust().Build();
        IScriptFinder sut = container.Resolve<IScriptFinder>();
        var errorsAndInfos = new ErrorsAndInfos();
        var fileNames = (await sut.FindScriptFileNamesAsync(errorsAndInfos)).ToList();
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        Assert.IsGreaterThan(10, fileNames.Count);
        foreach (string fileName in fileNames) {
            Assert.IsTrue(File.Exists(fileName));
            Assert.EndsWith(".xml", fileName);
        }
    }
}