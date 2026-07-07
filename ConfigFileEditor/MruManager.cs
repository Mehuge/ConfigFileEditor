using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConfigFileEditor
{
    public class MruManager
    {
        private readonly string _filePath;
        private readonly int _maxItems;
        private List<string> _files = new List<string>();

        public IReadOnlyList<string> Files => _files.AsReadOnly();
        public event EventHandler? Changed;

        public MruManager(string filePath, int maxItems = 5)
        {
            _filePath = filePath;
            _maxItems = maxItems;
            Load();
        }

        public void Add(string filePath)
        {
            _files.Remove(filePath);
            _files.Insert(0, filePath);
            if (_files.Count > _maxItems)
                _files.RemoveAt(_maxItems);
            Save();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void Load()
        {
            if (!File.Exists(_filePath)) return;
            try
            {
                _files = File.ReadAllLines(_filePath)
                             .Where(l => !string.IsNullOrWhiteSpace(l))
                             .ToList();
            }
            catch { }
        }

        private void Save()
        {
            try { File.WriteAllLines(_filePath, _files); }
            catch { }
        }
    }
}
