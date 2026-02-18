using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Skladasu.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Core;

public class ScriptFinder(IFolderResolver folderResolver) : IScriptFinder {
    public async Task<IEnumerable<string>> FindScriptFileNamesAsync(IErrorsAndInfos errorsAndInfos) {
        string folder = await ScriptFolderAsync(errorsAndInfos);
        string[] potentialFiles = Directory.GetFiles(folder, "*.xml", SearchOption.AllDirectories);
        var potentialFileContents = potentialFiles.Select(File.ReadAllText).ToList();

        return potentialFiles.Where(f => !IsSubScript(f, potentialFileContents)).ToList();
    }

    public async Task<string> ScriptFolderAsync(IErrorsAndInfos errorsAndInfos) {
        string folder = (await folderResolver.ResolveAsync(@"$(MainUserFolder)\" + ControlledApplication.Name + @"\Production\Dump", errorsAndInfos)).FullName + "\\";
        return folder;
    }

    public static bool IsSubScript(string fileName, IList<string> fileContents) {
        string tag = fileName.Substring(fileName.LastIndexOf("\\", StringComparison.Ordinal) + 1);
        tag = tag.Substring(0, tag.Length - 4);
        tag = "subscriptname=\"" + tag + '"';
        return fileContents.Any(fc => fc.Contains(tag));
    }
}