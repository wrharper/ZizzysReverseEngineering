# Hex Editor & Disassembler Address Synchronization

**Date**: January 21, 2026  
**Feature**: Virtual Address Display Correlation

## Overview

The hex editor and disassembler now display consistent virtual addresses, making it easy to navigate between views and correlate binary locations.

### Before vs After

| Aspect | Before | After |
|--------|--------|-------|
| **Hex Editor Shows** | File offset (0x00001000) | Virtual address (0x0000000140001000) |
| **Disassembler Shows** | Virtual address (0x0000000140001000) | Virtual address (0x0000000140001000) |
| **Correlation** | ❌ Different formats, confusing | ✅ Same format, easy to match |
| **Address Width** | 8 chars (16 hex digits) | 16 chars (32 hex digits) |

## How It Works

### Address Calculation

```
Virtual Address = ImageBase + FileOffset

Example:
- ImageBase:  0x0000000140000000 (standard PE x64)
- FileOffset: 0x0000000000001000 (from binary file)
- Virtual:    0x0000000140001000 (displayed in both views)
```

### Implementation Details

#### 1. HexEditorState - Store ImageBase
```csharp
public class HexEditorState
{
    public ulong ImageBase = 0x140000000;  // PE x64 default
}
```

#### 2. HexEditorRenderer - Calculate Virtual Address
```csharp
private void DrawOffset(int offsetX, int offsetY, int offset)
{
    // NEW: Calculate virtual address instead of file offset
    ulong virtualAddress = _s.ImageBase + (ulong)offset;
    string text = virtualAddress.ToString("X16");  // 16-char hex
    
    // Draw at offsetX, offsetY
    // (same as before, but with virtual address)
}
```

#### 3. HexEditorControl - Propagate ImageBase
```csharp
public void SetImageBase(ulong imageBase)
{
    _state.ImageBase = imageBase;
    Invalidate();  // Redraw with new addresses
}

// Called from MainMenuController after loading binary
```

#### 4. Integration Points (3 locations)

**Location 1**: MainMenuController (~line 310)
```csharp
// After File → Open Binary
_core.LoadFile(path);
_hex.SetBuffer(_core.HexBuffer);
_hex.SetImageBase(_core.ImageBase);  // NEW - set virtual base
```

**Location 2**: MainMenuController (~line 359)
```csharp
// After Project → Restore Project
_core.RestoreProject(projectPath);
_hex.SetBuffer(_core.HexBuffer);
_hex.SetImageBase(_core.ImageBase);  // NEW - set virtual base
```

**Location 3**: DisassemblyController (~line 97)
```csharp
// When disassembly loaded
_hex.SetBuffer(_core.HexBuffer);
_hex.SetImageBase(_core.ImageBase);  // NEW - set virtual base
```

## UI Changes

### Offset Column Width
```
BEFORE: CharWidth * 8 + 10 pixels
        (enough for 8-char hex addresses like 00001000)

AFTER:  CharWidth * 16 + 10 pixels
        (enough for 16-char hex addresses like 0000000140001000)
```

### Display Format
```
Hex Editor Offset Column:

BEFORE:
00000000 | AA BB CC DD EE FF 00 11 | ........
00001000 | 55 8B EC 48 83 EC 20 .. | U..H.... 

AFTER:
0000000140000000 | AA BB CC DD EE FF 00 11 | ........
0000000140001000 | 55 8B EC 48 83 EC 20 .. | U..H....
```

### Disassembly View (Unchanged)
```
Disassembly shows section headers and virtual addresses:

═══ .TEXT SECTION ═══

0000000140001000  55          PUSH RBP
0000000140001001  8B EC       MOV EBP, ESP
0000000140001003  48 83 EC    SUB RSP, 0x20
       20
```

## Usage Example

### Scenario: Correlate Hex and Disassembly

1. **In Disassembler** - Notice instruction at `0x0000000140001000`
   ```
   0000000140001000  55          PUSH RBP
   ```

2. **Click the instruction** - Hex editor automatically scrolls

3. **In Hex Editor** - Confirm same address
   ```
   0000000140001000 | 55 8B EC 48 83 EC 20 | U..H....
   ```

4. **Same address format** - Easy to verify correlation

5. **Edit bytes** - Any hex changes update both views

## ImageBase Configuration

### Default Values by Architecture
```csharp
// PE x64 (64-bit)
ImageBase = 0x0000000140000000

// PE x86 (32-bit)
ImageBase = 0x00400000
```

### Customization
```csharp
// If binary has custom base (rare)
_hexEditor.SetImageBase(0x0000000180000000);
```

### Persistence
- ImageBase is stored in binary PE header
- Automatically read when loading binary
- Propagated to hex editor on load

## Logging & Debugging

### Log Entries
When hex editor is initialized:
```
[HexEditor] SetImageBase: 0x0000000140000000
[HexEditor] Offset column width: 210 pixels (16-char addresses)
[HexEditor] Virtual address display enabled
```

### Verification
To verify addresses are synced:
1. Open binary
2. Note address in disassembler: `0x0000000140002500`
3. Click that instruction
4. Verify hex editor shows same address in offset column

## Performance Considerations

- ✅ **No impact**: Virtual address calculation is O(1) addition
- ✅ **No overhead**: Done during draw cycle (same as before)
- ✅ **Scales**: Works with 135MB+ binaries

## Common Questions

**Q**: Why 0x140000000 for PE x64?
**A**: This is the default image base specified by Microsoft PE format. Real binaries may differ but are uncommon.

**Q**: Can I change the ImageBase?
**A**: Yes, via `SetImageBase()` but normally not needed (read from PE header automatically).

**Q**: What if hex editor shows different address than disassembler?
**A**: Check that `SetImageBase()` was called after loading. Should be called from MainMenuController or DisassemblyController.

**Q**: Why 16-char display?
**A**: PE x64 uses full 64-bit addresses (0x0000000140001000). x86 would be shorter, but we use full width for consistency.

## Related Features

- **Multi-Section Support**: Each section has consistent virtual addresses
- **Cross-Reference Tracking**: All xrefs use virtual addresses
- **Binary Patching**: Patches recorded with virtual addresses
- **Project Serialization**: Projects store addresses as virtual addresses

## See Also

- [HexEditor Documentation](HexEditor/README.md)
- [Disassembler Architecture](ANALYSIS_PIPELINE.md)
- [Binary Format Support](README.md#binary-format-support)
