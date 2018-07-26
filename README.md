BinEd
=====

Scripting optimized binary file editor

This application allows you to edit binary files of almost any size (2^63 bytes).
It's optimized to be used with scripts but is manually usable too.

## Features

- Easy integration into scripts and applications
- Usable by manually entering commands or by piping them into the editor
- Editing of files larger than memory would allow
- Robust against invalid command usage

## Usage

This application has no command line arguments.
Just run it as is from console or double click

Type `?` to get started.

## Help

You don't need to read this documentation further unless you really get stuck.
All commands and their properties are documented in the application itself.
The help can be beought up by typing `?` for command and `??` for details.

## File Buffer

This application does not employs a file buffer beyond that of the framework and the operating system.
While this is detremental to the overall performance,
it grants us the ability to edit files much larger than would fit into memory.

## Commands

This section explains all commands available in the editor.
The documentation format is identical to that of other applications.
If you are not used to this, here's a short rundown of them:

- `arg` This argument is required and must be used literally as documented.
- `<arg>` This argument is required
- `[arg]` This argument is optional
- `{a|b|c}` Pick one variant only
- `...` The previous argument must be repeated
- `[...]` The previous argument can be repeated

Unsupported or invalid commands generally will not exit the application
unless that option is enabled. It's recommended to enable it if the application
is used by automated means, otherwise the application will always exit with code `0`.

Commands will never ask for confirmation and are always executed instantaneously.

### `?`

This command shows the list of supported commands

### `??`

This command shows details and background information about commands and the editor

### `c <FileName>`

This command creates a new empty file.
This command will fail if the file already exists.

### `o <FileName>`

This command opens an existing file for editing.
This command will fail if the file doesn't exists yet.
The editor refuses to open a file that is marked with at least one of these attributes:

- Hidden
- System
- ReadOnly

To open these files in Windows, run the command `attrib -H -R -S <FileName>` first.

### `cl`

Closes an open file

### `del`

Closes an open file and deletes it from disk

### `cat <FileName>`

Reads the specified file and copies all contents into the currently open file.
It will start to write at the current cursor location and extend the file if needed.

### `w <Bytes> [...]`

Writes the given bytes to the current file.
This command uses the "Prefixed bytes" format.

### `r <count> <Bytes> [...]`

Executes `w <Bytes> [...]` as many times as specified.
This command uses the "Prefixed bytes" format.

### `s <Position> [{b|c|e}]`

Sets the cursor to the given position.
The optional second argument specifies the offset base of the first argument:

- B: Begin; This is default if not specified
- C: Current Position
- E: End

The value is clamped to fit into the file region

### `l <Length>`

Sets the new file length.
This is an easy method to shrink a file.
It also helps to prevent file fragmentation
if a file is extended to the final size before anything is written to it.

If this number is bigger than the current length,
the file is extended but the contents left undefined.
Common file systems will fill the new content with `00`.
If the number is smaller than the current length,
the file is truncated to the given length
and the content beyond the new length is immediately lost.

If the Length is prefixed with `+` or `-`,
it's interpreted relative to the current cursor position.
This means using `-0` or `+0` will trim the file size to the current cursor position.

The cursor position is clamped to the new size.

### `f <Bytes> [...]`

Searches for the given bytes in the file.
If found, it sets the cursor to the start of the match.
If not found, it resets the cursor to where it was before the search.
This is a somewhat time consuming operation
and it's recommended to seek close to where the match is expected for large files.

The search is performed forward looking only.
To search the entire file, use `s 0` first.

The arguments are Raw Bytes concatenated into a single byte array

### `d <Number>`

Dumps the given number of bytes to the console as a hexadecimal view.
The dump is 16 bytes wide and contains hexadecimal values and ASCII renditions.
The ASCII part has all control characters stripped.
This will look similar to what Hex Editors display.
If the number is larger than the remaining bytes, it's clamped down.
Dumping WILL NOT advance the file pointer unless the number is prefixed with `+`.

This command will not respect disabled output or pipe mode.

### `stat`

Prints a single line of statistics containing:

- Cursor Position
- File Length
- File Name

This command will not trigger a fail if no file is open.

### `opt [name [value]]`

Displays or sets an option.
If no arguments are given, all options and values are shown.
If one argument is given, that option is shown only.
If two arguments are given, the option is set to the new value.

### `q`

Exits the application. This automatically closes any open file.

## Terms

Below are the description of some of the terms used in this document

### Fail/Failure Code

Error that causes the application to exit immediately if fail mode is enabled

### Cursor and File pointer

Describes the current point in the file from which reads and writes are performed.

### Clamping

Forcing a number into a certain Range.
In essence it's this:

    value = Math.max(MINIMUM, Math.Min(MAXIMUM, value));

### ASCII

American Standard Code for Information Interchange.
A 7-bit character set supporting 128 characters.
It serves as the base of almost all other existing character sets.
The table is thus identical to the first 128 character of most codepages,
it lacks almost all characters necessary to display other languages than english.

### Control Characters

The first 32 Characters and last single character of the ASCII codepage.
They have no graphical representation most of the times.
They historically performed various signal and text control actions.
Apart from line breaks and tabulator, most lost their meaning.

## Type of arguments

The chapters below descript certain argument types of the commands above

### Numbers

Most arguments that accept numbers will accept them in decimal and hexadecimal format.
To signify hexadecimal, the number is to be prefixed with `0x`.
Both, hexadecimal and decimal numbers, can additionally be prefixed with `+` or `-`

### Raw Bytes

Hexadecimal only byte specification without support for any prefixes.

### Prefixed Bytes

Available prefixes :

- `+`: Add value to current byte
- `-`: Subtract value from current byte
- `&`: Binary AND with current byte
- `|`: Binary OR with current byte
- `^`: Binary XOR with current byte.

The prefix applies to an entire list of values, `+EDBE` is
identical to `+ED +BE`
If you try to use prefixes and the pointer is at the file end, `00` is used as a base value.
Mathematical operations that result in the value not being `0<=x<=255`
has the overflow cut off by applying `x&0xFF`.

Prefixes can't be combined.
Using prefixes is slow because the file has to be read first,
seeked back and then written to.

## Opening Files

Only one file can be opened at a time. Attempting to open/create a file
before closing the current file will trigger a failure code.
Files opened with `c` or `o` are always opened in Read/Write mode
regardless if anything is written at all.
This means the application must have read+write permission on the file in any case.

## File Names

Commands that accept file names will parse the name identical to the console in regards to relative paths.
Be sure 'Current Directory' is set properly before using relative paths.
Contrary to the command line,
file names are not enclosed in Quotation marks if they contain spaces in their name.

## Options

Multiple options are supported and can be modified at any time.
The number in brackets is the default (if static).
Settings that accept `0` or `1` as a value will treat everything that is not `0`
as a `1`.

- `out[1]`: If set to `0` it will no longer output any status messages
- `pipe`: If set to `1`, the application will only output codes, no messages. Defaults to `1` if the input stream is redirected
- `fatal[0]`: If set to `1`, the application will abort on any failure code

## Pipe Mode

Enabling pipe mode renders some commands less verbose because the output of
commands is limited to a single numerical code.
`d` will always output to console regardless of mode.

