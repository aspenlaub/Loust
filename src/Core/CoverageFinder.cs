using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Aspenlaub.Net.GitHub.CSharp.Loust.Entities;
using Aspenlaub.Net.GitHub.CSharp.Loust.Interfaces;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Entities;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Extensions;
using Aspenlaub.Net.GitHub.CSharp.Pegh.Interfaces;

namespace Aspenlaub.Net.GitHub.CSharp.Loust.Core;

public class CoverageFinder : ICoverageFinder {
    protected string Folder { get; private set; }
    protected Dictionary<string, int> OccurrencesOfCoveredFiles;
    protected IList<string> CoveredFilesToIgnore, OrderedScriptFileNames, LastModifiedPhpFiles, LastModifiedPhpFilesWithoutCoverage;
    protected Dictionary<string, IList<string>> FilesCoveredInCoverageFile;

    private readonly ITestCaseFileNameShortener _TestCaseFileNameShortener;
    private readonly IScriptFinder _ScriptFinder;
    private readonly IFolderResolver _FolderResolver;
    private readonly ISecretRepository _SecretRepository;

    public CoverageFinder(IFolderResolver folderResolver, IScriptFinder scriptFinder, ITestCaseFileNameShortener testCaseFileNameShortener,
                            ISecretRepository secretRepository) {
        _TestCaseFileNameShortener = testCaseFileNameShortener;
        _ScriptFinder = scriptFinder;
        _FolderResolver = folderResolver;
        _SecretRepository = secretRepository;
    }

    public string SortValueForScriptFile(string scriptFileName, bool byLastWriteTime) {
        long maxLastWriteTimeUtc = 0;
        const string format = "000000000000000000000000000000";

        string coverageFile = _TestCaseFileNameShortener.CoverageFileForScriptFile(new Folder(Folder),  scriptFileName);
        if (!File.Exists(coverageFile)) { return maxLastWriteTimeUtc.ToString(format); }

        if (byLastWriteTime) { return File.GetLastWriteTimeUtc(coverageFile).Ticks.ToString(format); }

        IList<string> lines = FilesCoveredInCoverageFile[coverageFile];
        foreach (long lastWriteTimeUtc in lines.Where(line => File.Exists(line) && !CoveredFilesToIgnore.Contains(line)).Select(line => File.GetLastWriteTimeUtc(line).Ticks).Where(lastWriteTimeUtc => lastWriteTimeUtc > maxLastWriteTimeUtc)) {
            maxLastWriteTimeUtc = lastWriteTimeUtc;
        }

        return maxLastWriteTimeUtc.ToString(format);
    }

    private bool NotCoveredScriptFile(string scriptFileName) {
        return !File.Exists(_TestCaseFileNameShortener.CoverageFileForScriptFile(new Folder(Folder), scriptFileName));
    }

    public async Task<IList<string>> GetOrderedScriptFileNamesAsync(bool byLastWriteTime, bool ignoreUncovered, bool ignoreValidation,
            bool ignoreUnitTest) {
        await RefreshAsync(byLastWriteTime, ignoreUncovered);
        return OrderedScriptFileNames
               .Where(x => !x.Contains("Validation") || !ignoreValidation)
               .Where(x => !x.Contains("Unit Test") || !ignoreUnitTest)
               .ToList();
    }

    public async Task<IList<string>> GetLastModifiedPhpFilesWithoutCoverageAsync() {
        await RefreshAsync(false, false);
        return LastModifiedPhpFilesWithoutCoverage;
    }

    protected async Task RefreshAsync(bool byLastWriteTime, bool ignoreUncovered) {
        await SetFolderIfNecessaryAsync();

        FilesCoveredInCoverageFile = new Dictionary<string, IList<string>>();
        foreach (string coverageFile in Directory.GetFiles(Folder, "*.txt")) {
            FilesCoveredInCoverageFile[coverageFile] = (await File.ReadAllLinesAsync(coverageFile)).Where(l => File.Exists(l)).ToList();
        }

        OccurrencesOfCoveredFiles = new Dictionary<string, int>();
        // ReSharper disable once LoopCanBePartlyConvertedToQuery
        foreach (IList<string> lines in FilesCoveredInCoverageFile.Select(f => f.Value)) {
            foreach (string line in lines) {
                if (!OccurrencesOfCoveredFiles.ContainsKey(line)) {
                    OccurrencesOfCoveredFiles[line] = 1;
                    continue;
                }

                OccurrencesOfCoveredFiles[line]++;
            }
        }

        int okayOccurrences = OccurrencesOfCoveredFiles.Any() ? OccurrencesOfCoveredFiles.Max(o => o.Value) / 10 : 10;
        CoveredFilesToIgnore = OccurrencesOfCoveredFiles.Where(o => o.Value > okayOccurrences).Select(o => o.Key).ToList();

        var errorsAndInfos = new ErrorsAndInfos();
        OrderedScriptFileNames = (await _ScriptFinder.FindScriptFileNamesAsync(errorsAndInfos)).OrderByDescending(f => SortValueForScriptFile(f, byLastWriteTime)).ToList();
        if (errorsAndInfos.AnyErrors()) {
            throw new Exception(errorsAndInfos.ErrorsToString());
        }

        if (!ignoreUncovered) {
            var uncoveredScripts = OrderedScriptFileNames.Where(NotCoveredScriptFile).ToList();
            if (uncoveredScripts.Any()) {
                OrderedScriptFileNames = uncoveredScripts;
            }
        }

        LoustSettings loustSettings = await _SecretRepository.GetAsync(new SecretLoustSettings(), errorsAndInfos);
        if (errorsAndInfos.AnyErrors()) {
            throw new Exception(errorsAndInfos.ErrorsToString());
        }

        var request = new HttpRequestMessage(HttpMethod.Get, loustSettings.LastChangedPhpFilesUrl);
        var client = new HttpClient();
        LastModifiedPhpFiles = new List<string>();
        try {
            HttpResponseMessage response = await client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.OK) {
                LastModifiedPhpFiles = (await response.Content.ReadAsStringAsync()).Replace("\r", "").Replace('/', '\\').Split('\n').Where(f => f != "").ToList();
            }
            // ReSharper disable once EmptyGeneralCatchClause
        } catch {
        }

        LastModifiedPhpFilesWithoutCoverage = LastModifiedPhpFiles.Where(f => !OccurrencesOfCoveredFiles.ContainsKey(f)).ToList();
    }

    public int NumberOfResults(string wildcard) {
        return Directory.GetFiles(Folder, wildcard).Length;
    }

    private async Task SetFolderIfNecessaryAsync() {
        if (Folder != null) { return;  }

        var errorsAndInfos = new ErrorsAndInfos();
        Folder = (await _FolderResolver.ResolveAsync(@"$(WampRoot)\temp\coverage\", errorsAndInfos)).FullName + "\\";
        if (errorsAndInfos.AnyErrors()) {
            throw new Exception(errorsAndInfos.ErrorsToString());
        }
    }
}