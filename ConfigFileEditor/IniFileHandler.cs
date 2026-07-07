using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConfigFileEditor
{
    public class IniFileHandler
    {
        public const string DefaultSectionName = "[Default]";

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

            string currentSectionName = DefaultSectionName;

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
            string currentSectionName = DefaultSectionName;

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

        public bool HasSection(string sectionName) => Sections.ContainsKey(sectionName);

        public void AddSection(string sectionName)
        {
            IniStructure.Add(new SectionEntry { SectionName = sectionName, RawLine = $"[{sectionName}]" });
            RebuildSectionsDictionary();
            MarkDirty();
        }

        public SettingEntry AddSetting(string sectionName, string key, string value)
        {
            if (!Sections.ContainsKey(sectionName))
                Sections[sectionName] = new Dictionary<string, string>();
            Sections[sectionName][key] = value;

            var newSetting = new SettingEntry { Key = key, Value = value };
            int insertIndex = FindSettingInsertionIndex(sectionName);
            IniStructure.Insert(insertIndex, newSetting);

            if (!IsLastSection(sectionName) && insertIndex < IniStructure.Count && IniStructure[insertIndex] is CommentEntry)
                IniStructure.Insert(insertIndex + 1, new BlankLineEntry());

            MarkDirty();
            return newSetting;
        }

        public void UpdateSettingValue(string sectionName, string key, string newValue)
        {
            if (Sections.TryGetValue(sectionName, out var section) && section.ContainsKey(key))
                section[key] = newValue;
            MarkDirty();
        }

        public void RemoveSetting(string sectionName, string key, SettingEntry entry)
        {
            if (Sections.TryGetValue(sectionName, out var section))
                section.Remove(key);
            IniStructure.Remove(entry);
            MarkDirty();
        }

        private int FindSettingInsertionIndex(string sectionName)
        {
            int sectionEntryIndex = IniStructure.FindIndex(
                e => e is SectionEntry se && se.SectionName == sectionName);

            if (sectionEntryIndex != -1)
            {
                int endOfSection = sectionEntryIndex + 1;
                while (endOfSection < IniStructure.Count && !(IniStructure[endOfSection] is SectionEntry))
                    endOfSection++;

                int insertIndex = endOfSection;
                for (int i = endOfSection - 1; i > sectionEntryIndex; i--)
                {
                    if (IniStructure[i] is SettingEntry) { insertIndex = i + 1; break; }
                    if (IniStructure[i] is BlankLineEntry) insertIndex = i;
                }
                return insertIndex;
            }
            else if (sectionName == DefaultSectionName)
            {
                int lastDefaultSetting = -1;
                for (int i = 0; i < IniStructure.Count; i++)
                {
                    if (IniStructure[i] is SectionEntry) break;
                    if (IniStructure[i] is SettingEntry) lastDefaultSetting = i;
                }
                return lastDefaultSetting + 1;
            }
            else
            {
                return IniStructure.Count;
            }
        }

        private bool IsLastSection(string sectionName)
        {
            int sectionIdx = IniStructure.FindIndex(e => e is SectionEntry se && se.SectionName == sectionName);
            int start = sectionIdx == -1 ? 0 : sectionIdx + 1;
            for (int i = start; i < IniStructure.Count; i++)
            {
                if (IniStructure[i] is SectionEntry) return false;
            }
            return true;
        }

        public void MarkDirty() => IsDirty = true;
        public void ClearDirty() => IsDirty = false;

        public void DeleteSection(string sectionName)
        {
            bool isDefault = sectionName == DefaultSectionName;

            if (isDefault)
            {
                // Remove all entries that precede the first explicit section header
                int firstSectionIdx = IniStructure.FindIndex(e => e is SectionEntry);
                int removeCount = firstSectionIdx == -1 ? IniStructure.Count : firstSectionIdx;
                if (removeCount > 0)
                    IniStructure.RemoveRange(0, removeCount);
            }
            else
            {
                int startIdx = IniStructure.FindIndex(e => e is SectionEntry se && se.SectionName == sectionName);
                if (startIdx == -1) return;

                int endIdx = startIdx + 1;
                while (endIdx < IniStructure.Count && !(IniStructure[endIdx] is SectionEntry))
                    endIdx++;

                IniStructure.RemoveRange(startIdx, endIdx - startIdx);
            }

            RebuildSectionsDictionary();
            MarkDirty();
        }
    }
}