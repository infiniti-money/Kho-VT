using HisaWarehouse.Services;

var checks = 0;
void Expect(bool actual, bool expected, string description)
{
    if (actual != expected)
        throw new InvalidOperationException(description + ": expected " + expected + ", got " + actual);
    Console.WriteLine("PASS " + description);
    checks++;
}

Expect(WorkspaceNavigationPolicy.CanOpenRootPage(hasActivePage: false),
    true, "first page can be opened");
Expect(WorkspaceNavigationPolicy.CanOpenRootPage(hasActivePage: true),
    false, "sidebar cannot stack a second root page");
Expect(WorkspaceNavigationPolicy.CanOpenRequestEditor(parentIsRequestList: true, parentStackDepth: 0),
    true, "request list may temporarily show its own editor");
Expect(WorkspaceNavigationPolicy.CanOpenRequestEditor(parentIsRequestList: false, parentStackDepth: 0),
    false, "AI and other pages cannot push request editor");
Expect(WorkspaceNavigationPolicy.CanOpenRequestEditor(parentIsRequestList: true, parentStackDepth: 1),
    false, "nested editor cannot stack further pages");
Expect(WorkspaceNavigationPolicy.CanOpenRootPage(hasActivePage: false),
    true, "after closing the active form, a new root page can be opened");
// Form snapshot is taken after defaults are initialized. Empty forms must never
// prompt; exact edits must prompt; reverting must clear the warning.
var note = "";
var lineQuantity = 0d;
var model = new WorkspaceFormSnapshot(() => WorkspaceFormSnapshot.Serialize(note, lineQuantity));
Expect(model.HasChanges, false, "new untouched form navigates without confirmation");
note = "Giao hàng cho đội lắp máy";
Expect(model.HasChanges, true, "newly entered recipient/note prompts on leaving");
note = "";
Expect(model.HasChanges, false, "reverted text no longer triggers warning");
lineQuantity = 12;
Expect(model.HasChanges, true, "added or changed stock line protects unsaved data");
lineQuantity = 0;
Expect(model.HasChanges, false, "removed/reset stock line is clean again");

// The main page must use IWorkspaceDirtyPage instead of checking only the
// editor type. Confirm-on-navigation with no edits was the reported regression.
var mainPath = Path.Combine(Directory.GetCurrentDirectory(), "MainWindow.xaml.cs");
var navCode = File.ReadAllText(mainPath);
Expect(navCode.Contains("editor is IWorkspaceDirtyPage dirty", StringComparison.Ordinal) &&
       navCode.Contains("? dirty.HasUnsavedChanges", StringComparison.Ordinal),
    true, "main navigation checks real user changes before prompting");
foreach (var editorFile in new[] {
    "MovementWindow.xaml.cs", "ItemEditorWindow.xaml.cs",
    "MaterialRequestEditorWindow.xaml.cs", "PrintTemplateWindow.xaml.cs",
    "CatalogsWindow.xaml.cs", "ManageUnitsWindow.xaml.cs", "ReplenishmentWindow.xaml.cs"
})
{
    var source = File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), editorFile));
    Expect(source.Contains("IWorkspaceDirtyPage", StringComparison.Ordinal) &&
           source.Contains("HasUnsavedChanges", StringComparison.Ordinal), true,
        editorFile + " reports actual unsaved changes");
}

Console.WriteLine($"SUCCESS: {checks} navigation+dirty-form regression checks.");
