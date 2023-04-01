using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Core;

public class ScriptFinder : IScriptFinder {
    private readonly IFolderResolver _FolderResolver;

    public ScriptFinder(IFolderResolver folderResolver) {
        _FolderResolver = folderResolver;
    }

    public async Task<IEnumerable<string>> FindScriptFileNamesAsync(IErrorsAndInfos errorsAndInfos) {
        var folder = await ScriptFolderAsync(errorsAndInfos);
        var potentialFiles = Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories);
        var potentialFileContents = potentialFiles.Select(f => File.ReadAllText(f)).ToList();

        return potentialFiles.Where(f => !IsSubScript(f, potentialFileContents)).ToList();
    }

    public async Task<string> ScriptFolderAsync(IErrorsAndInfos errorsAndInfos) {
        var folder = (await _FolderResolver.ResolveAsync(@"$(MainUserFolder)\" + ControlledApplication.Name + @"\Production\Dump", errorsAndInfos)).FullName + "\\";
        return folder;
    }

    public static bool IsSubScript(string fileName, IList<string> fileContents) {
        var tag = fileName.Substring(fileName.LastIndexOf("\\", StringComparison.Ordinal) + 1);
        tag = tag.Substring(0, tag.Length - 4);
        tag = "subscriptname=\"" + tag + '"';
        return fileContents.Any(fc => fc.Contains(tag));
    }
}