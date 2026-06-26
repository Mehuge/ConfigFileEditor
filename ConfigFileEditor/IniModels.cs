namespace ConfigFileEditor
{
    // New classes for INI file structure
    public abstract class IniEntry
    {
        // No common properties here, each derived class will handle its own representation
    }

    public class SectionEntry : IniEntry
    {
        public string SectionName { get; set; } = string.Empty;
        public string RawLine { get; set; } = string.Empty; // To preserve original formatting
    }

    public class SettingEntry : IniEntry
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public bool IsCommentedOut { get; set; } = false;
    }

    public class CommentEntry : IniEntry
    {
        public string RawLine { get; set; } = string.Empty; // To preserve original formatting
    }

    public class BlankLineEntry : IniEntry
    {
        public string RawLine { get; set; } = string.Empty; // To preserve original formatting
    }
}