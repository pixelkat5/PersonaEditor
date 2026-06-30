using AuxiliaryLibraries.WPF.Tools;
using AuxiliaryLibraries.WPF.Wrapper;
using PersonaEditorLib;
using PersonaEditorLib.Other;
using PersonaEditorLib.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace PersonaEditor.Classes
{
    internal static class BatchProcessor
    {
        private const string SettingsFileName = "PersonaEditor.xml";
        private const string DefaultFontName = "P4";

        public static BatchResult ExportImages(string sourceRoot, string outputRoot)
        {
            var result = new BatchResult();
            foreach (string sourcePath in EnumerateSourceFiles(sourceRoot))
            {
                string fileDir = GetOutputDirectory(sourceRoot, sourcePath, outputRoot);
                var file = OpenSourceFile(sourcePath, result);
                if (file != null)
                    ExportGameFileImages(file, fileDir, result);
            }

            return result;
        }

        public static BatchResult ImportImages(string sourceRoot, string imageRoot)
        {
            var result = new BatchResult();
            Dictionary<string, string> imageFiles = BuildImageIndex(imageRoot);

            foreach (string sourcePath in EnumerateSourceFiles(sourceRoot))
            {
                bool changed = false;
                string sourceRelativeDir = GetRelativeDirectory(sourceRoot, sourcePath);
                var file = OpenSourceFile(sourcePath, result);
                if (file == null)
                    continue;

                ImportGameFileImages(file, imageRoot, sourceRelativeDir, imageFiles, result, ref changed);

                if (changed)
                    SaveFile(sourcePath, file);
            }

            return result;
        }

        public static BatchResult ExportText(string sourceRoot, string outputTextPath)
        {
            var result = new BatchResult();
            var settings = LoadTextSettings();
            if (!string.IsNullOrEmpty(outputTextPath))
            {
                EnsureDirectory(outputTextPath);
                File.WriteAllText(outputTextPath, "", Encoding.UTF8);
            }

            foreach (string sourcePath in EnumerateSourceFiles(sourceRoot))
            {
                string fileDir = GetOutputDirectory(sourceRoot, sourcePath, null);
                ProcessFile(sourcePath, result, gameFile =>
                {
                    string path = outputTextPath;
                    if (string.IsNullOrEmpty(path))
                        path = Path.Combine(fileDir, Path.GetFileNameWithoutExtension(gameFile.Name) + ".TXT");

                    IEnumerable<string> lines = ExportTextLines(gameFile, settings.OldEncoding);
                    if (lines != null)
                    {
                        EnsureDirectory(path);
                        File.AppendAllLines(path, lines, Encoding.UTF8);
                        result.Exported++;
                    }
                });
            }

            if (!string.IsNullOrEmpty(outputTextPath))
                RemoveDuplicateTextRows(outputTextPath, Encoding.UTF8);

            return result;
        }

        public static BatchResult ImportText(string sourceRoot, string textPath)
        {
            var result = new BatchResult();
            var settings = LoadTextSettings();
            var rowCache = new Dictionary<string, List<string[]>>(StringComparer.CurrentCultureIgnoreCase);
            List<string[]> sharedRows = File.Exists(textPath) ? ReadTextRows(textPath, Encoding.UTF8, rowCache) : null;

            foreach (string sourcePath in EnumerateSourceFiles(sourceRoot))
            {
                bool changed = false;
                var file = OpenSourceFile(sourcePath, result);
                if (file == null)
                    continue;

                string fileDir = Path.GetDirectoryName(sourcePath);
                ProcessGameFile(file, gameFile =>
                {
                    List<string[]> rows = sharedRows;
                    if (rows == null)
                    {
                        string localPath = Path.Combine(fileDir, Path.GetFileNameWithoutExtension(gameFile.Name) + ".TXT");
                        if (!File.Exists(localPath))
                            return;
                        rows = ReadTextRows(localPath, Encoding.UTF8, rowCache);
                    }

                    if (ImportTextRows(gameFile, rows, settings))
                    {
                        changed = true;
                        result.Imported++;
                    }
                });

                if (changed)
                    SaveFile(sourcePath, file);
            }

            return result;
        }

        private static IEnumerable<string> ExportTextLines(GameFile gameFile, PersonaEncoding oldEncoding)
        {
            if (gameFile.GameData is PTP ptp)
                return ptp.ExportTXT(false, oldEncoding).Select(x => $"{gameFile.Name}\t{x}");
            if (gameFile.GameData is BMD bmd)
                return new PTP(bmd).ExportTXT(false, oldEncoding).Select(x => $"{gameFile.Name}\t{x}");
            if (gameFile.GameData is ATF atf)
                return atf.ExportText(gameFile.Name, false);
            if (gameFile.GameData is StringList list)
                return list.ExportText();

            return null;
        }

        private static bool ImportTextRows(GameFile gameFile, List<string[]> rows, TextSettings settings)
        {
            if (gameFile.GameData is PTP ptp)
                return ImportPTPText(ptp, gameFile.Name, rows);
            if (gameFile.GameData is ATF atf)
                return ImportATFText(atf, gameFile.Name, rows);
            if (gameFile.GameData is BMD bmd)
            {
                var bmdText = new PTP(bmd);
                bmdText.CopyOld2New(settings.OldEncoding);
                if (!ImportPTPText(bmdText, gameFile.Name, rows))
                    return false;

                var temp = new BMD(bmdText, settings.NewEncoding);
                temp.IsLittleEndian = bmd.IsLittleEndian;
                gameFile.GameData = temp;
                return true;
            }
            if (gameFile.GameData is StringList list)
            {
                string[][] imported = rows.Where(x => x.Length > 1 && x[1] != "").ToArray();
                if (imported.Length == 0)
                    return false;
                list.ImportText(imported);
                return true;
            }

            return false;
        }

        private static bool ImportPTPText(PTP ptp, string fileName, List<string[]> rows)
        {
            string[][] imported = rows
                .Select(row => TryGetPTPTranslation(row, fileName, out string[] text) ? text : null)
                .Where(x => x != null)
                .ToArray();

            if (imported.Length == 0)
                return false;

            ptp.ImportText(imported);
            return true;
        }

        private static bool ImportATFText(ATF atf, string fileName, List<string[]> rows)
        {
            var imported = new List<(int Index, string Text)>();
            foreach (string[] row in rows)
                if (TryGetATFTranslation(row, fileName, out int index, out string text))
                    imported.Add((index, text));

            if (imported.Count == 0)
                return false;

            atf.ImportTextByIndex(imported);
            return true;
        }

        private static bool TryGetPTPTranslation(string[] row, string fileName, out string[] text)
        {
            text = null;
            if (row.Length >= 6 && IsMatchingFileName(row[0], fileName) && row[5] != "")
            {
                text = new[] { row[1], row[2], row[5] };
                return true;
            }

            if (row.Length >= 3 && int.TryParse(row[0], out _) && row[2] != "")
            {
                text = new[] { row[0], row[1], row[^1] };
                return true;
            }

            return false;
        }

        private static bool TryGetATFTranslation(string[] row, string fileName, out int index, out string text)
        {
            index = -1;
            text = "";

            if (row.Length >= 4 && IsMatchingFileName(row[0], fileName) && int.TryParse(row[1], out index))
            {
                text = row[3];
                return !string.IsNullOrEmpty(text);
            }

            if (row.Length >= 6 && IsMatchingFileName(row[0], fileName) && int.TryParse(row[2], out index))
            {
                text = row[5];
                return !string.IsNullOrEmpty(text);
            }

            if (row.Length >= 3 && int.TryParse(row[0], out index))
            {
                text = row[^1];
                return !string.IsNullOrEmpty(text);
            }

            return false;
        }

        private static void ProcessFile(string sourcePath, BatchResult result, Action<GameFile> action)
        {
            var file = OpenSourceFile(sourcePath, result);
            if (file != null)
                ProcessGameFile(file, action);
        }

        private static void ProcessGameFile(GameFile gameFile, Action<GameFile> action)
        {
            action(gameFile);
            foreach (GameFile subFile in gameFile.GameData.SubFiles)
                ProcessGameFile(subFile, action);
        }

        private static void ExportGameFileImages(GameFile gameFile, string outputDir, BatchResult result)
        {
            if (gameFile.GameData is IImage image)
            {
                string imageName = GetSafePathPart(Path.GetFileNameWithoutExtension(gameFile.Name)) + ".PNG";
                string path = Path.Combine(outputDir, imageName);
                EnsureDirectory(path);
                var source = image.GetBitmap()?.GetBitmapSource();
                if (source != null)
                {
                    ImageTools.SaveToPNG(source, path);
                    result.Exported++;
                }
            }

            if (gameFile.GameData.SubFiles.Count == 0)
                return;

            string containerDir = Path.Combine(outputDir, GetSafePathPart(gameFile.Name));
            foreach (GameFile subFile in gameFile.GameData.SubFiles)
                ExportGameFileImages(subFile, containerDir, result);
        }

        private static void ImportGameFileImages(
            GameFile gameFile,
            string imageRoot,
            string relativeDir,
            Dictionary<string, string> imageFiles,
            BatchResult result,
            ref bool changed)
        {
            if (gameFile.GameData is IImage image)
            {
                string imageName = GetSafePathPart(Path.GetFileNameWithoutExtension(gameFile.Name)) + ".PNG";
                string imagePath = FindImagePath(imageRoot, relativeDir, imageName, imageFiles);
                if (imagePath != null)
                {
                    try
                    {
                        image.SetBitmap(ImageTools.OpenPNG(imagePath).GetBitmap());
                        changed = true;
                        result.Imported++;
                    }
                    catch
                    {
                        result.Failed++;
                    }
                }
            }

            if (gameFile.GameData.SubFiles.Count == 0)
                return;

            string containerDir = CombineRelativePath(relativeDir, GetSafePathPart(gameFile.Name));
            foreach (GameFile subFile in gameFile.GameData.SubFiles)
                ImportGameFileImages(subFile, imageRoot, containerDir, imageFiles, result, ref changed);
        }

        private static GameFile OpenSourceFile(string sourcePath, BatchResult result)
        {
            try
            {
                return GameFormatHelper.OpenFile(sourcePath);
            }
            catch
            {
                result.Failed++;
                return null;
            }
        }

        private static IEnumerable<string> EnumerateSourceFiles(string sourceRoot)
            => Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories).ToArray();

        private static string GetOutputDirectory(string sourceRoot, string sourcePath, string outputRoot)
        {
            if (string.IsNullOrEmpty(outputRoot))
                return Path.GetDirectoryName(sourcePath);

            string relativeDir = GetRelativeDirectory(sourceRoot, sourcePath);
            return string.IsNullOrEmpty(relativeDir) ? outputRoot : Path.Combine(outputRoot, relativeDir);
        }

        private static string GetRelativeDirectory(string sourceRoot, string sourcePath)
        {
            string relative = Path.GetRelativePath(sourceRoot, sourcePath);
            string dir = Path.GetDirectoryName(relative);
            return dir == "." ? "" : dir;
        }

        private static string CombineRelativePath(string first, string second)
            => string.IsNullOrEmpty(first) ? second : Path.Combine(first, second);

        private static string GetSafePathPart(string name)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                name = name.Replace(invalid, '+');

            return name.Replace('/', '+').Replace('\\', '+');
        }

        private static Dictionary<string, string> BuildImageIndex(string imageRoot)
        {
            return Directory.EnumerateFiles(imageRoot, "*.png", SearchOption.AllDirectories)
                .GroupBy(Path.GetFileName, StringComparer.CurrentCultureIgnoreCase)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.CurrentCultureIgnoreCase);
        }

        private static string FindImagePath(string imageRoot, string relativeDir, string imageName, Dictionary<string, string> imageFiles)
        {
            string mirrored = string.IsNullOrEmpty(relativeDir)
                ? Path.Combine(imageRoot, imageName)
                : Path.Combine(imageRoot, relativeDir, imageName);
            if (File.Exists(mirrored))
                return mirrored;

            return imageFiles.TryGetValue(imageName, out string indexedPath) ? indexedPath : null;
        }

        private static List<string[]> ReadTextRows(string path, Encoding encoding, Dictionary<string, List<string[]>> cache)
        {
            string key = Path.GetFullPath(path);
            if (cache.TryGetValue(key, out List<string[]> rows))
                return rows;

            rows = File.ReadAllLines(path, encoding).Select(x => x.Split('\t')).ToList();
            cache.Add(key, rows);
            return rows;
        }

        private static bool IsMatchingFileName(string value, string fileName)
            => value.Split('|').Any(x => x.Equals(fileName, StringComparison.CurrentCultureIgnoreCase));

        private static void RemoveDuplicateTextRows(string path, Encoding encoding)
        {
            string[] lines = File.ReadAllLines(path, encoding);
            var rows = new Dictionary<string, string[]>();

            foreach (string line in lines)
            {
                string[] columns = line.Split('\t');
                if (columns.Length <= 1)
                {
                    rows.TryAdd(line, new[] { line });
                    continue;
                }

                string key = string.Join('\t', columns.Skip(1));
                if (rows.TryGetValue(key, out string[] existing))
                    existing[0] = MergeFileNames(existing[0], columns[0]);
                else
                    rows.Add(key, columns);
            }

            File.WriteAllLines(path, rows.Values.Select(x => string.Join('\t', x)), encoding);
        }

        private static string MergeFileNames(string first, string second)
        {
            return string.Join("|", first.Split('|')
                .Concat(second.Split('|'))
                .Where(x => x != "")
                .Distinct(StringComparer.CurrentCultureIgnoreCase));
        }

        private static void SaveFile(string sourcePath, GameFile file)
            => File.WriteAllBytes(sourcePath, file.GameData.GetData());

        private static void EnsureDirectory(string filePath)
        {
            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }

        private static TextSettings LoadTextSettings()
        {
            string oldFont = DefaultFontName;
            string newFont = DefaultFontName;
            string settingPath = Path.Combine(Static.Paths.CurrentFolderEXE, SettingsFileName);

            if (File.Exists(settingPath))
            {
                try
                {
                    XDocument doc = XDocument.Load(settingPath, LoadOptions.PreserveWhitespace);
                    XElement settings = doc.Element("Settings");
                    oldFont = settings?.Element("OldFont")?.Value ?? oldFont;
                    newFont = settings?.Element("NewFont")?.Value ?? newFont;
                }
                catch
                {
                }
            }
            else
            {
                CreateDefaultSettings(settingPath);
            }

            return new TextSettings
            {
                OldEncoding = Static.EncodingManager.GetPersonaEncoding(oldFont),
                NewEncoding = Static.EncodingManager.GetPersonaEncoding(newFont)
            };
        }

        private static void CreateDefaultSettings(string path)
        {
            try
            {
                var doc = new XDocument(
                    new XElement("Settings",
                        new XElement("OldFont", DefaultFontName),
                        new XElement("NewFont", DefaultFontName)));
                doc.Save(path);
            }
            catch
            {
            }
        }

        private class TextSettings
        {
            public PersonaEncoding OldEncoding { get; set; }
            public PersonaEncoding NewEncoding { get; set; }
        }
    }

    internal class BatchResult
    {
        public int Exported { get; set; }
        public int Imported { get; set; }
        public int Failed { get; set; }
    }
}
