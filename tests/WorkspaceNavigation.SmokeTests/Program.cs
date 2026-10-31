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
Console.WriteLine($"SUCCESS: {checks} single-active-workspace checks.");
