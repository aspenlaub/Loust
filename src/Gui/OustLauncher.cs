using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Media;
using Aspenlaub.Net.GitHub.CSharp.Loust.Core;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Helpers;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Entities;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Gui;

public class OustLauncher {
    public static async Task LaunchOustIfNecessaryAsync(IFolderResolver folderResolver, Action<Paragraph> addParagraph) {
        if (Process.GetProcessesByName(ControlledApplication.QualifiedName).Length != 0) {
            return;
        }

        var errorsAndInfos = new ErrorsAndInfos();
        IFolder folder = await folderResolver.ResolveAsync(@"$(GitHub)\" + ControlledApplication.Name  + @"Bin\Release", errorsAndInfos);
        if (!folder.Exists() || errorsAndInfos.AnyErrors()) { return; }

        var p = new Paragraph(new Run(Properties.Resources.StartingOustFromDefaultLocation)) {
            Foreground = Brushes.Green
        };
        addParagraph(p);

        StartProcess(folder.FullName + @"\" + ControlledApplication.QualifiedName + ".exe");
        await Wait.UntilAsync(() => Task.FromResult(Process.GetProcessesByName(ControlledApplication.QualifiedName).Length != 0), TimeSpan.FromMinutes(1));
        await Task.Delay(TimeSpan.FromSeconds(30));
    }

    private static void StartProcess(string executableFullName) {
        var process = new Process {
            StartInfo = {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                FileName = executableFullName,
                Arguments = "",
                WorkingDirectory = "",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        process.Start();
    }
}