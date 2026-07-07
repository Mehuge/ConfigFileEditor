# TODO

* Rename sections
* Rename settings (keys)
* Duplicate sections (e.g. if want to try some different config options but want a backup of the original section)
* If config file changes on the filesystem auto-reload, if currently modified (* in title) then prompt if to discard changes and reload 
* Save as
* Setup verisoning
* ISS setup

---

# Config File Editor — User Manual

## Overview

Config File Editor is a Windows desktop application for viewing and editing INI-style configuration files (`.ini`, `.cfg`, `.conf` and similar formats). It presents the file as a structured tree rather than raw text, making it easy to navigate, search, and modify sections and settings without accidentally corrupting the file's formatting. The original file's whitespace, comments, and blank lines are preserved faithfully on save.

---

## The Interface

The window is divided into two main areas:

- **Left panel** — the tree view showing all sections and their settings
- **Right panel** — the detail area showing the currently selected section or setting

Above the tree is a **search/filter bar**. At the top-right is the **Add Section** button. Below the tree are the **Add Setting** and **Remove Setting** buttons.

A status bar at the bottom shows the time of the last action.

---

## Opening and Saving Files

### Opening a file
- Use **File → Open** to browse for a file.
- Recently opened files appear at the bottom of the **File** menu (up to 5 entries).

### Creating a new file
- Use **File → New File**. If there are unsaved changes you will be prompted to save first.

### Saving
- **File → Save** saves to the current file path. If the file has not been saved before, a Save As dialog appears.
- **Ctrl+S** saves immediately without opening a menu.
- The title bar shows an asterisk (`*`) whenever there are unsaved changes.

---

## The Tree View

Each top-level node in the tree represents a **section** (`[SectionName]`). Child nodes represent individual **settings** (key=value pairs) and **comments** within that section.

Settings that exist before any explicit section header are grouped under a special **[Default]** node.

### Selecting items
- Click any node to select it. The right panel updates to show its details.
- When a **section** is selected, the section name is shown in the Section field.
- When a **setting** is selected, the section name, key, and value are all shown and the value field becomes editable.

### Expanding and collapsing
- Click the expand arrow to show or hide a section's children.

---

## Searching / Filtering

Type in the **Search** box above the tree to filter the view in real time (with a short debounce delay).

- Matches are found in **section names**, **setting keys**, and **setting values**.
- If a section name matches the query, all of its settings are shown.
- Sections with no matching content are hidden automatically.
- Matching sections are expanded so results are immediately visible.
- The first matching item is selected automatically.

### Clearing the filter
- Click the **X** button next to the search box, or simply clear the text.
- The full tree is restored immediately.

> **Note:** Drag-and-drop reordering is disabled while a filter is active.

---

## Adding a Section

1. Click **Add Section** (top-right of the window).
2. Enter a section name in the dialog — you can type it with or without brackets (e.g. `MySection` or `[MySection]`).
3. Click **OK**. The new section is appended to the end of the file and selected in the tree.

Duplicate section names are rejected with an error message.

---

## Adding a Setting

1. Select a **section node** or any **setting node** within the target section. If nothing is selected, the setting is added to the **[Default]** section.
2. Click **Add Setting**.
3. In the dialog, enter the **Name** (key) and **Value**.
4. Click **OK**. The new setting appears at the end of the section.

---

## Editing a Setting Value

1. Select a setting node in the tree.
2. The **Value** field in the right panel becomes active.
3. Type the new value directly — changes are applied immediately as you type and the file is marked as modified.

---

## Commenting Out a Setting

When a setting is selected, a **Commented** checkbox appears in the right panel.

- **Checking** the box prefixes the setting with `; ` in the file and greys out the value field.
- **Unchecking** the box removes the prefix and makes the value editable again.

Commented-out settings are displayed in the tree with a `; ` prefix on their key name.

---

## Removing a Setting

1. Select the setting you want to remove.
2. Click **Remove Setting**.

The setting is removed immediately (no confirmation dialog).

---

## Reordering Sections and Settings (Drag and Drop)

You can reorder items in the tree by dragging and dropping.

- **Sections** (top-level nodes) can be dragged onto other sections to reorder them. A section moves to the position of the target section, along with all of its settings and comments.
- **Settings** within a section can be dragged to a different position within the same section, or dropped directly onto a section header to move them to the top of that section.

> Drag-and-drop is **disabled** while the search filter is active. Clear the filter first.

---

## Section Context Menu

Right-clicking a section node shows a context menu with the following options:

### Copy
Copies the section (header and all its settings) to the clipboard in INI format, ready to paste directly into another `.ini` file.

- The section header line is copied exactly as it appears in the file (preserving any inline comments on the header line).
- Settings are written as `key=value`.
- Commented-out settings are written as `; key=value`.
- Comment lines within the section are included verbatim.

**Keyboard shortcut:** Select the section node and press **Ctrl+C**.

### Delete
Deletes the section and all of its settings after showing a confirmation dialog.

- Hold **Shift** while clicking **Delete** in the menu to skip the confirmation dialog.

**Keyboard shortcut:** Select the section node and press **Delete** — a confirmation dialog is always shown when using the keyboard shortcut.

---

## File Format Notes

- Both `;` and `#` are recognised as comment characters.
- Inline comments on section headers are preserved (e.g. `[Section] ; note` is loaded and saved back unchanged).
- Blank lines between sections are preserved on save.
- Setting values are stored and saved without surrounding quotes.
- The file's original line endings and indentation are preserved.

---

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| **Ctrl+S** | Save the current file |
| **Ctrl+C** | Copy selected section to clipboard (when a section node is focused) |
| **Delete** | Delete selected section with confirmation (when a section node is focused) |

