using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Entities;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Extensions;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Core;

public class BrokenTestCaseRepository(IFolderResolver folderResolver, ITestCaseFileNameShortener testCaseFileNameShortener)
        : IBrokenTestCaseRepository {
    private IFolder _Folder;

    public async Task RegisterAsync(string scriptFileName, IList<string> errors) {
        await SetFolderIfNecessaryAsync();

        string shortName = testCaseFileNameShortener.CoverageFileForScriptFileShortName(scriptFileName);
        string fileName = _Folder.FullName + '\\' + shortName;
        string contents =
            $"Test case {scriptFileName.Substring(scriptFileName.LastIndexOf('\\'))} failed on {Environment.MachineName} at {DateTime.Now.ToShortDateString()} {DateTime.Now.ToShortTimeString()}";
        if (errors.Any()) {
            contents = contents + "\r\n" + string.Join("\r\n", errors);
        }
        await File.WriteAllTextAsync(fileName, contents);
    }

    public async Task RemoveAsync(string scriptFileName) {
        await SetFolderIfNecessaryAsync();

        string shortName = testCaseFileNameShortener.CoverageFileForScriptFileShortName(scriptFileName);
        string fileName = _Folder.FullName + '\\' + shortName;
        if (!File.Exists(fileName)) { return; }

        File.Delete(fileName);
    }

    public async Task<bool> ContainsAsync(string scriptFileName) {
        await SetFolderIfNecessaryAsync();

        string shortName = testCaseFileNameShortener.CoverageFileForScriptFileShortName(scriptFileName);
        string fileName = _Folder.FullName + '\\' + shortName;
        return File.Exists(fileName);
    }

    public async Task<int> NumberOfBrokenTestsAsync() {
        await SetFolderIfNecessaryAsync();

        return Directory.GetFiles(_Folder.FullName, "*.txt").ToList().Count;
    }

    private async Task SetFolderIfNecessaryAsync() {
        if (_Folder != null) { return; }

        var errorsAndInfos = new ErrorsAndInfos();
        _Folder = await folderResolver.ResolveAsync(@"$(WampRoot)\temp\brokentests\", errorsAndInfos);
        _Folder.CreateIfNecessary();
        if (errorsAndInfos.AnyErrors()) {
            throw new Exception(errorsAndInfos.ErrorsToString());
        }

    }
}