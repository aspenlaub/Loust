using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Loust.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Test;

[TestClass]
public class LoustSettingsTest {
    [TestMethod]
    public async Task CanGetLoustSettings() {
        var container = new ContainerBuilder().UsePegh(nameof(LoustSettingsTest), new DummyCsArgumentPrompter()).Build();
        var secret = new SecretLoustSettings();
        var errorsAndInfos = new ErrorsAndInfos();
        var settings = await container.Resolve<ISecretRepository>().GetAsync(secret, errorsAndInfos);
        Assert.IsFalse(errorsAndInfos.AnyErrors(), errorsAndInfos.ErrorsToString());
        Assert.IsTrue(!string.IsNullOrEmpty(settings.LastChangedPhpFilesUrl));
        Assert.IsTrue(!string.IsNullOrEmpty(settings.TrivialTest));
    }
}