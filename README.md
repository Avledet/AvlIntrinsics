# AvlIntrinsics

Utility mod to use with Avledet dedicated server for Valheim.

Includes combined features from:
- ValhallaEx
- ValhallaDumper
- ZSocket2Fix

This plugin is intended to replace the above plugins.

The purpose of this unification is simple:
- I created these different plugins over several years when I needed them.
- Every time a new game update came out, or I reinstalled my OS, or did literally anything, the structure would be broken.
- Any change required a change across each individual plugin.
- Unifying them all makes the most sense (why didn't I do this earlier...)

## Features

- `/avldump` - Resource package dumping to use with Avledet server
- `/avl` - Directly access the Avledet server internals in game via the Lua API, using this command as entry!
- Connecting to test server with TCP (for Wireshark)