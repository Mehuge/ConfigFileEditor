using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConfigFileEditor
{
    public class IniFileHandler
    {
        public List<IniEntry> IniStructure { get; private set; } = new List<IniEntry>();
        public Dictionary<string, Dictionary<string, string>> Sections { get; private set; } = new Dictionary<string, Dictionary<string, string>>();
        public string? CurrentFilePath { get; set; }
        public bool IsDirty { get; private set; }

        public void NewFile()
        {
            IniStructure.Clear();
            Sections.Clear();
            CurrentFilePath = null;
            ClearDirty();
        }

        public void LoadFile(string path)
        {
            NewFile(); // Start fresh
            CurrentFilePath = path;

            string currentSectionName = "[Default]";

            foreach (string line in File.ReadAllLines(path))
            {
                string trimmedLine = line.Trim();

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    var sectionEntry = new SectionEntry { SectionName = currentSectionName, RawLine = line };
                    IniStructure.Add(sectionEntry);
                }
                else
                {
                    bool isCommented = trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#");
                    string potentialSettingLine = isCommented ? trimmedLine.Substring(1).Trim() : trimmedLine;

                    if (potentialSettingLine.Contains("="))
                    {
                        string[] parts = potentialSettingLine.Split(new[] { '=' }, 2);
                        string key = parts[0].Trim();
                        string val = parts[1].Trim();

                        var settingEntry = new SettingEntry { Key = key, Value = val, IsCommentedOut = isCommented };
                        IniStructure.Add(settingEntry);
                    }
                    else if (isCommented)
                    {
                        var commentEntry = new CommentEntry { RawLine = line };
                        IniStructure.Add(commentEntry);
                    }
                    else
                    {
                        var blankLineEntry = new BlankLineEntry { RawLine = line };
                        IniStructure.Add(blankLineEntry);
                    }
                }
            }

            RebuildSectionsDictionary();
            ClearDirty();
        }

        public void SaveFile()
        {
            if (string.IsNullOrEmpty(CurrentFilePath))
            {
                throw new InvalidOperationException("File path is not set. Use SaveFileAs for new files.");
            }

            var lines = new List<string>();
            for (int i = 0; i < IniStructure.Count; i++)
            {
                var entry = IniStructure[i];

                if (entry is SectionEntry se)
                {
                    // This check is important for implicit sections that have no raw line
                    if (!string.IsNullOrEmpty(se.RawLine))
                    {
                        // If this isn't the first line and the previous entry wasn't a blank line, add one.
                        if (i > 0 && !(IniStructure[i - 1] is BlankLineEntry))
                        {
                            lines.Add("");
                        }
                        lines.Add(se.RawLine);
                    }
                }
                else if (entry is SettingEntry st)
                {
                    string prefix = st.IsCommentedOut ? "; " : "";
                    lines.Add($"{prefix}{st.Key}={st.Value}");
                }
                else if (entry is CommentEntry or BlankLineEntry)
                {
                    // For comments and existing blank lines, just add their raw content.
                    lines.Add(entry.RawLine);
                }
            }

            File.WriteAllLines(CurrentFilePath, lines);
            ClearDirty();
        }

        public void RebuildSectionsDictionary()
        {
            Sections.Clear();
            string currentSectionName = "[Default]";

            foreach (var entry in IniStructure)
            {
                if (entry is SectionEntry sectionEntry)
                {
                    currentSectionName = sectionEntry.SectionName;
                    if (!Sections.ContainsKey(currentSectionName))
                    {
                        Sections[currentSectionName] = new Dictionary<string, string>();
                    }
                }
                else if (entry is SettingEntry settingEntry)
                {
                    if (!Sections.ContainsKey(currentSectionName))
                    {
                        Sections[currentSectionName] = new Dictionary<string, string>();
                    }

                    if (!settingEntry.IsCommentedOut)
                    {
                        Sections[currentSectionName][settingEntry.Key] = settingEntry.Value;
                    }
                }
            }
        }

        public void MoveSectionInDataStructure(SectionEntry draggedSection, SectionEntry targetSection)
        {
            if (draggedSection == null || targetSection == null) return;

            int startIdx = IniStructure.IndexOf(draggedSection);
            if (startIdx == -1) return;

            List<IniEntry> blockToMove = new List<IniEntry>();
            for (int i = startIdx; i < IniStructure.Count; i++)
            {
                if (i > startIdx && IniStructure[i] is SectionEntry)
                    break;
                blockToMove.Add(IniStructure[i]);
            }

            foreach (var entry in blockToMove)
            {
                IniStructure.Remove(entry);
            }

            int targetIdx = IniStructure.IndexOf(targetSection);
            if (targetIdx == -1)
            {
                IniStructure.AddRange(blockToMove); // Failsafe: add to end
            }
            else
            {
                IniStructure.InsertRange(targetIdx, blockToMove);
            }
            MarkDirty();
        }

        public void MarkDirty() => IsDirty = true;
        public void ClearDirty() => IsDirty = false;
    }
}