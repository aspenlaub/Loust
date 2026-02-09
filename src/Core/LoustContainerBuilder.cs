using Aspenlaub.Net.GitHub.CSharp.Dvin.Components;
using Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;
using Autofac;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Core;

public static class LoustContainerBuilder {
    public static ContainerBuilder UseLoust(this ContainerBuilder builder) {
        builder.UseDvinAndPegh("Loust");
        builder.RegisterType<BrokenTestCaseRepository>().As<IBrokenTestCaseRepository>();
        builder.RegisterType<CoverageFinder>().As<ICoverageFinder>();
        builder.RegisterType<ScriptFinder>().As<IScriptFinder>();
        builder.RegisterType<ScriptRunner>().As<IScriptRunner>();
        builder.RegisterType<TestCaseFileNameShortener>().As<ITestCaseFileNameShortener>();
        return builder;
    }
}