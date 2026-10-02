# Local behavior and privacy

Runtime code uses built-in .NET Framework libraries and Windows APIs. There are no network endpoints, cloud models, analytics SDKs or third-party packages.

`Native.IdleSeconds` reads `GetLastInputInfo`. Unsigned 32-bit subtraction of the reported timestamp from `Environment.TickCount`, divided by 1000, handles clock wrap. It reads input age rather than keys, characters, buttons, or application activity. The companion checks it approximately once a second while active. Input-free reading or video watching cannot be distinguished from inactivity.

`GetCursorPos` supplies current coordinates for optional cursor approach and an actively captured pet drag. Head stroking uses mouse messages received on the pet's own silhouette. No global mouse/keyboard hook, screen-wide click tracking, input injection or pointer movement is used. No position history is saved.

Fullscreen avoidance polls about once a second, reading foreground geometry and checking at most eight previously observed fullscreen window identities. Visibility, minimized status, process identity and window style help invalidate or exclude windows; titles, classes, process names, screen content and documents are not read. Framed maximized windows and shell/desktop surfaces are excluded. This heuristic can miss games and can confuse borderless monitor-covering apps with fullscreen use. It never manipulates their windows or processes.

Energy, warmth, approximate active-use counters and observed-window identities live only in RAM. Preference persistence contains only five flags: variation, affection, break reminders, fullscreen avoidance and quiet mode. Size, cursor approach, idle threshold, mood and current animation are not persistent preferences in this preview.

Own-window startup/error metadata and monitor bounds may appear in `diagnostic.txt`; errors may contain local paths. These logs, preference files and rendered diagnostic artwork must not be included when reporting bugs publicly. The release staging contains none of those generated files.

Pause stops roaming; passive fullscreen checks may continue so visibility can recover. Deliberate interaction while paused can still play a short response. Hide, quiet mode, menus, lock and suspend suppress the relevant automatic activity. Session lock/disconnect cancels dragging and hides the pet; resume alone does not count as unlock.

An optional current-user Startup shortcut is created or removed only through explicit startup controls. It is neither an administrator service nor a registry policy. No OS security, network, lock-screen or permission setting is changed.
