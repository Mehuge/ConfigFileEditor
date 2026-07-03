namespace ConfigFileEditor
{
    public abstract class IniEntry
    {
        public string RawLine { get; set; } = string.Empty; // Preserves original formatting
    }

    public class SectionEntry : IniEntry
    {
        public string SectionName { get; set; } = string.Empty;
    }

    public class SettingEntry : IniEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsCommentedOut { get; set; } = false;
    }

    public class CommentEntry : IniEntry { }

    public class BlankLineEntry : IniEntry { }
}