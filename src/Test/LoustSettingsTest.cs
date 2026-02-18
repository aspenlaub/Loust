using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Loust.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Components;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Seoa.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Entities;
using Autofac;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Test;

[TestClass]
public class LoustSettingsTest {
    [TestMethod]
    public async Task CanGetLoustSettings() {
        IContainer container = new ContainerBuilder().UsePegh(nameof(LoustSettingsTest)).Build();
        var secret = new SecretLoustSettings();
        var errorsAndInfos = new ErrorsAndInfos();
        LoustSettings settings = await container.Resolve<ISecretRepository>().GetAsync(secret, errorsAndInfos);
        Assert.That.ThereWereNoErrors(errorsAndInfos);
        Assert.IsFalse(string.IsNullOrEmpty(settings.LastChangedPhpFilesUrl));
        Assert.IsFalse(string.IsNullOrEmpty(settings.TrivialTest));
    }
}