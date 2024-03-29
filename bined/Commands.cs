﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace BinEd
{
    /// <summary>
    /// Handles Commands
    /// </summary>
    public partial class Program
    {
        private static Random RND;

        private static void FindContent(Options OPT, Command C)
        {
            if (FILE == null)
            {
                Status(OPT, "No file open", RESULT.NOFILE);
            }
            else
            {
                var Pos = FILE.Position;
                if (C.Arguments.Length > 0)
                {
                    var Operations = GetByteOperations(C.Arguments);
                    if (Operations == null)
                    {
                        Status(OPT, "Unable to convert arguments to byte instructions", RESULT.INVALID_ARG);
                    }
                    else if (Operations.Any(m => m.Mode != ByteMode.Overwrite))
                    {
                        Status(OPT, "Find does not support Byte modes", RESULT.INVALID_ARG);
                    }
                    else
                    {
                        var Bytes = Operations.SelectMany(m => m.Bytes).ToArray();
                        var Buffer = new byte[Bytes.Length];
                        if (FILE.Read(Buffer, 0, Buffer.Length) == Buffer.Length)
                        {
                            while (FILE.Position < FILE.Length)
                            {
                                //Check if content found by comparing the byte arrays
                                if (NativeMethods.CompareBytes(Buffer, Bytes, (UIntPtr)Buffer.Length) == 0)
                                {
                                    //Set proper file position
                                    FILE.Position -= Buffer.Length;
                                    Status(OPT, $"Content found Position={FILE.Position}", RESULT.OK);
                                    return;
                                }
                                else
                                {
                                    //Shift bytes to the left
                                    for (var i = 1; i < Buffer.Length; i++)
                                    {
                                        Buffer[i - 1] = Buffer[i];
                                    }
                                    //Add new byte
                                    Buffer[Buffer.Length - 1] = (byte)FILE.ReadByte();
                                }
                            }
                        }
                        Status(OPT, "Content not found", RESULT.PART_OK);
                        FILE.Position = Pos;
                    }
                }
                else
                {
                    Status(OPT, "'f' requires at least one argument", RESULT.ARGUMENT_MISMATCH);
                }
            }
        }

        private static void DumpContents(Options OPT, Command C)
        {
            if (FILE == null)
            {
                Status(OPT, "No file open", RESULT.NOFILE);
            }
            else
            {
                var Pos = FILE.Position;
                if (C.Arguments.Length > 0)
                {
                    var L = GetLong(C.Arguments[0], long.MinValue);
                    if (L == long.MinValue)
                    {
                        Status(OPT, $"Unable to parse {C.Arguments[0]} into a number", RESULT.INVALID_NUMBER);
                    }
                    else if (L > 0)
                    {
                        byte[] Buffer = new byte[16];
                        while (L > 0)
                        {
                            int Readed = FILE.Read(Buffer, 0, Buffer.Length);
                            O(
                                //Hexadecimal
                                string.Join(" ", Buffer.Select((m, i) => i < Readed ? m.ToString("X2") : "  ")) +
                                //Spacer
                                "\t" +
                                //ASCII with control chars filtered
                                GetASCII(Buffer.Take(Readed).ToArray()));
                            L -= Readed < 1 ? L : Readed;
                        }
                        if (!C.Arguments[0].Trim().StartsWith("+"))
                        {
                            FILE.Position = Pos;
                        }
                    }
                    else
                    {
                        Status(OPT, $"Number too small. Minimum is 1", RESULT.INVALID_NUMBER);
                    }
                }
                else
                {
                    Status(OPT, "d requires one argument", RESULT.ARGUMENT_MISMATCH);
                }
            }
        }

        private static void Concat(Options OPT, Command C)
        {
            if (FILE == null)
            {
                Status(OPT, "No file open", RESULT.NOFILE);
            }
            else
            {
                if (C.Arguments.Length > 0)
                {
                    try
                    {
                        using (var FS = File.OpenRead(C.Arguments.First()))
                        {
                            FS.CopyTo(FILE);
                            FILE.Flush();
                            Status(OPT, $"Written={FS.Length} Position={FILE.Position}", RESULT.OK);
                        }
                    }
                    catch (Exception ex)
                    {
                        Status(OPT, $"Unable to concat files. {ex.Message}", RESULT.IO_ERROR);
                    }
                }
                else
                {
                    Status(OPT, "cat requires one argument", RESULT.ARGUMENT_MISMATCH);
                }
            }
        }

        private static void RepeatBytes(Options OPT, Command C)
        {
            if (FILE == null)
            {
                Status(OPT, "No file open", RESULT.NOFILE);
            }
            else if (C.Arguments.Length > 1)
            {
                var Count = GetLong(C.Arguments[0], long.MinValue);
                if (Count == long.MinValue)
                {
                    Status(OPT, $"Can't convert {C.Arguments[0]} to a number", RESULT.INVALID_NUMBER);
                }
                else if (Count < 1)
                {
                    Status(OPT, $"First argument must be at least 1", RESULT.INVALID_NUMBER);
                }
                else
                {
                    var Operations = GetByteOperations(C.Arguments.Skip(1));
                    if (Operations == null)
                    {
                        Status(OPT, "Unable to convert arguments to byte instructions", RESULT.INVALID_ARG);
                    }
                    else
                    {
                        for (var i = 0; i < Count; i++)
                        {
                            foreach (var Opt in Operations)
                            {
                                if (!Opt.ProcessBytes(FILE))
                                {
                                    FILE.Flush();
                                    Status(OPT, $"Error writing to file", RESULT.IO_ERROR);
                                    return;
                                }
                            }
                        }
                        FILE.Flush();
                        Status(OPT, $"Written={Operations.Sum(m => m.Bytes.LongLength * Count)} Position={FILE.Position}", RESULT.OK);
                    }
                }
            }
            else
            {
                Status(OPT, "'r' requires at least two arguments", RESULT.ARGUMENT_MISMATCH);
            }
        }

        private static void WriteBytes(Options OPT, Command C)
        {
            if (FILE == null)
            {
                Status(OPT, "No file open", RESULT.NOFILE);
            }
            else if (C.Arguments.Length > 0)
            {
                var Operations = GetByteOperations(C.Arguments);
                if (Operations == null)
                {
                    Status(OPT, "Unable to convert arguments to byte instructions", RESULT.INVALID_ARG);
                }
                else
                {
                    foreach (var Opt in Operations)
                    {
                        if (!Opt.ProcessBytes(FILE))
                        {
                            FILE.Flush();
                            Status(OPT, $"Error writing to file", RESULT.IO_ERROR);
                            return;
                        }
                    }
                    FILE.Flush();
                    Status(OPT, $"Written={Operations.Sum(m => m.Bytes.LongLength)} Position={FILE.Position}", RESULT.OK);
                }
            }
            else
            {
                Status(OPT, "'w' requires at least one argument", RESULT.ARGUMENT_MISMATCH);
            }
        }

        private static void WriteRandom(Options OPT, Command C)
        {
            if (FILE == null)
            {
                Status(OPT, "No file open", RESULT.NOFILE);
            }
            else if (C.Arguments.Length == 1)
            {
                var NumBytes = GetLong(C.Arguments[0], long.MinValue);
                if (NumBytes > long.MinValue)
                {
                    //Negative numbers indicate how many bytes to not overwrite
                    if (NumBytes <= 0)
                    {
                        if (NumBytes == 0)
                        {
                            //Distinguish negative Zero from positive zero
                            if (C.Arguments[0][0] == '-')
                            {
                                NumBytes = FILE.Length - FILE.Position;
                            }
                        }
                        else
                        {
                            //If NumBytes is less than 0, it dictates how many bytes to leave
                            //Need to add the number because it's already negative
                            NumBytes = FILE.Length - FILE.Position + NumBytes;
                        }
                    }
                    if (NumBytes < 0)
                    {
                        Status(OPT, $"Argument {C.Arguments[0]} would write a negative number of bytes", RESULT.INVALID_NUMBER);
                    }
                    else
                    {
                        //Operate in chunks of 1 MB for small data (<100MB) and 100 MB for large data (100MB+)
                        byte[] Buffer = new byte[1000000 * (NumBytes >= 100000000 ? 100 : 1)];
                        if (RND == null)
                        {
                            RND = new Random();
                        }
                        try
                        {
                            for (long i = 0; i < NumBytes; i += Buffer.Length)
                            {
                                RND.NextBytes(Buffer);
                                FILE.Write(Buffer, 0, (int)Math.Min(Buffer.Length, NumBytes - i));
                            }
                            Status(OPT, $"Written={NumBytes} Position={FILE.Position}", RESULT.OK);
                        }
                        catch (Exception ex)
                        {
                            Status(OPT, $"Unable to concat files. {ex.Message}", RESULT.IO_ERROR);
                        }
                    }
                }
                else
                {
                    Status(OPT, $"Unable to parse {C.Arguments[0]} into a number", RESULT.INVALID_NUMBER);
                }
            }
            else
            {
                Status(OPT, $"Require exactly 1 argument, {C.Arguments.Length} given", RESULT.ARGUMENT_MISMATCH);
            }
        }

        private static void SetLength(Options OPT, Command C)
        {
            if (FILE == null)
            {
                Status(OPT, "No file open", RESULT.NOFILE);
            }
            else
            {
                if (C.Arguments.Length == 1)
                {
                    var L = GetLong(C.Arguments[0], long.MinValue);
                    if (L >= 0)
                    {
                        //Implement "Negative Zero"
                        if (L == 0 && C.Arguments[0].Trim().StartsWith("-"))
                        {
                            L = FILE.Position;
                        }
                        //Implement relative to Position
                        else if (C.Arguments[0].Trim().StartsWith("+"))
                        {
                            L += FILE.Position;
                        }

                        FILE.SetLength(L);
                        FILE.Flush();
                        //If new length is less than position, it forces the position inside the new length
                        if (FILE.Position > L)
                        {
                            Status(OPT, $"Length={FILE.Length}. Pos={FILE.Position}", RESULT.PART_OK);
                        }
                        else
                        {
                            Status(OPT, $"Length={FILE.Length}. Pos={FILE.Position}", RESULT.OK);
                        }
                    }
                    else if (L > long.MinValue)
                    {
                        if (L < -FILE.Position)
                        {
                            Status(OPT, $"Number too small. Minimum is {-FILE.Position}", RESULT.INVALID_NUMBER);
                        }
                        else
                        {
                            FILE.SetLength(FILE.Position - L);
                            FILE.Flush();
                            //New length will always be less than the position. Always return OK
                            Status(OPT, $"Length={FILE.Length}. Pos={FILE.Position}", RESULT.OK);
                        }
                    }
                    else
                    {
                        Status(OPT, $"Unable to parse {C.Arguments[0]} into a number", RESULT.INVALID_NUMBER);
                    }
                }
                else
                {
                    Status(OPT, $"Require exactly 1 argument, {C.Arguments.Length} given", RESULT.ARGUMENT_MISMATCH);
                }
            }
        }

        private static Options SetOption(Options OPT, string Option, string Value)
        {
            if (string.IsNullOrEmpty(Option))
            {
                Status(OPT, $"Missing Option", RESULT.INVALID_ARG);
            }
            else
            {
                switch (Option.ToLower())
                {
                    case "pipe":
                        OPT.PipeMode = Value != "0";
                        break;
                    case "fatal":
                        OPT.Fail = Value != "0";
                        break;
                    case "out":
                        OPT.EnableOutput = Value != "0";
                        break;
                    case "share":
                        OPT.Share = Value != "0";
                        break;
                    default:
                        Status(OPT, $"Non-Existing Option {Option}", RESULT.INVALID_ARG);
                        return OPT;
                }
                Status(OPT, $"Option {Option} Set", RESULT.OK);
            }
            return OPT;
        }

        private static void ShowOption(Options OPT, string Option)
        {
            if (string.IsNullOrEmpty(Option))
            {
                Status(OPT, $"Missing Option", RESULT.INVALID_ARG);
            }
            else
            {
                switch (Option.ToLower())
                {
                    case "pipe":
                        Status(OPT, $"pipe={(OPT.PipeMode ? 1 : 0)}", RESULT.OK);
                        return;
                    case "fatal":
                        Status(OPT, $"fatal={(OPT.Fail ? 1 : 0)}", RESULT.OK);
                        return;
                    case "out":
                        Status(OPT, $"out={(OPT.EnableOutput ? 1 : 0)}", RESULT.OK);
                        return;
                    case "share":
                        Status(OPT, $"share={(OPT.Share ? 1 : 0)}", RESULT.OK);
                        return;
                    default:
                        Status(OPT, $"Non-Existing Option {Option}", RESULT.INVALID_ARG);
                        return;
                }
            }
        }

        private static void ShowOptions(Options OPT)
        {
            ShowOption(OPT, "out");
            ShowOption(OPT, "pipe");
            ShowOption(OPT, "fatal");
            ShowOption(OPT, "share");
        }

        private static void SeekStream(Options OPT, Command C)
        {
            if (FILE == null)
            {
                Status(OPT, "No file open", RESULT.NOFILE);
            }
            else
            {
                if (C.Arguments.Length > 0)
                {
                    var P1 = GetLong(C.Arguments[0], long.MinValue);
                    if (P1 > long.MinValue)
                    {
                        if (C.Arguments.Length > 1)
                        {
                            if (C.Arguments.Length > 2)
                            {
                                P1 = long.MinValue;
                                Status(OPT, "Too many arguments", RESULT.ARGUMENT_MISMATCH);
                            }
                            else
                            {
                                switch (C.Arguments[1].ToLower())
                                {
                                    case "b":
                                        break;
                                    case "c":
                                        P1 = FILE.Position + P1;
                                        break;
                                    case "e":
                                        P1 = FILE.Length + P1;
                                        break;
                                    default:
                                        P1 = long.MinValue;
                                        Status(OPT, $"Unable to read {C.Arguments[1]} as seek origin", RESULT.INVALID_ARG);
                                        break;
                                }
                            }
                        }
                        if (P1 > long.MinValue)
                        {
                            //Clamp value
                            var Seek = Math.Min(Math.Max(P1, 0L), FILE.Length);
                            try
                            {
                                FILE.Seek(Seek, SeekOrigin.Begin);
                                Status(OPT, $"Original={P1} Clamped={Seek} Position={FILE.Position}", Seek == P1 ? RESULT.OK : RESULT.PART_OK);
                            }
                            catch (Exception ex)
                            {
                                Status(OPT, $"Unable to seek to Offset {Seek}. {ex.Message}", RESULT.IO_ERROR);
                            }
                        }
                    }
                    else
                    {
                        Status(OPT, $"Unable to read {C.Arguments[0]} as number", RESULT.INVALID_NUMBER);
                    }
                }
                else
                {
                    Status(OPT, "Seek requires at least one parameter", RESULT.ARGUMENT_MISMATCH);
                }
            }
        }

        private static void OpenFile(Options OPT, Command C, bool IsOpen)
        {
            //Attributes under which we won't open the file
            const FileAttributes CRITICAL = FileAttributes.ReadOnly | FileAttributes.System | FileAttributes.Hidden;
            //Fileshare constants
            const FileShare SHARED = FileShare.ReadWrite;
            const FileShare UNSHARED = FileShare.Read;

            if (FILE == null)
            {
                FileName = C.Arguments.FirstOrDefault();
                if (!string.IsNullOrEmpty(FileName))
                {
                    if (FileName != ".")
                    {
                        try
                        {
                            FileName = Path.GetFullPath(FileName);
                        }
                        catch (Exception ex)
                        {
                            FileName = null;
                            Status(OPT, $"Unable to get full file name. {ex.Message}", RESULT.IO_ERROR);
                        }
                    }
                    if (FileName != null)
                    {
                        try
                        {
                            if (FileName == ".")
                            {
                                FILE = new MemoryStream();
                            }
                            else
                            {
                                var Attr = File.Exists(FileName) ? File.GetAttributes(FileName) : FileAttributes.Normal;
                                if ((Attr & CRITICAL) == 0)
                                {
                                    FILE = File.Open(FileName, IsOpen ? FileMode.Open : FileMode.CreateNew, FileAccess.ReadWrite, OPT.Share ? SHARED : UNSHARED);
                                }
                                else
                                {
                                    throw new IOException("File has protective Attributes");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            FileName = null;
                            Status(OPT, $"Unable to open {FileName}: {ex.Message}", RESULT.IO_ERROR);
                        }
                        if (FILE != null)
                        {
                            Status(OPT, $"File opened. Size: {FILE.Length}", RESULT.OK);
                        }
                    }
                }
                else
                {
                    Status(OPT, "Command requires parameter", RESULT.ARGUMENT_MISMATCH);
                }
            }
            else
            {
                Status(OPT, "A File is already open", RESULT.FILE_OPEN);
            }
        }

        private static void DetailHelp(Options OPT)
        {
            Status(OPT, @"More Information

Opening files
-------------
Only one file can be opened at a time. Attempting to open/create a file
before closing the current file will result in an error.
Files opened with 'c' or 'o' are always opened in Read/Write mode.
If the given file has one of these attributes, the operation will fail:
ReadOnly, System, Hidden

Memory file
-----------
If the file name for 'c' or 'o' is a single dot, a file in RAM will be opened.
This file always starts empty and cannot be saved to disk.
Size is purely limited to the amount of free memory.

File names
----------
Commands that accept file names will parse the name identical to the console.
Be sure 'Current Directory' is set properly before using relative paths.

File offset
-----------
Commands that modify the file will not set/reset the offset before writing.
Notably the 'cat' command will simply copy the given file to the current file
without first setting the pointer to the end. This causes the content after
the pointer to be overwritten and some content to be appended if the given
file is large enough.

Trying to set the File offset out of bounds will clamp it into bounds and
return with an error code

Prefixes
--------
Some functions support prefixed byte values.
These prefixes can't be stacked.
This behaviour can be simulated by performing the first operation, then
seeking back and performing the second operation over the same byte range.

Finding content
---------------
When using find there are two possible outcomes:
- Success: The file pointer is set to the position where the given bytes start
- Failure: The file pointer is restored to the previous position

Numerical Arguments
-------------------
Unless otherwise specified in the command help, numerical arguments can be
specified using decimal or hexadecimal notation. Decimal is default and
hexadecimal is triggered by prefixing a number with '0x'

Options
-------
Multiple options are supported and can be modified at any time.
The number in brackets is the default (if static).
Settings that accept 0 or 1 as a value will treat everything that is not 0
as a 1.

out[1]  : If set to 0 it will no longer output any status messages
pipe    : If set to 1, the application will only output codes, no messages.
          Defaults to 1 if the input stream is redirected 
fatal[0]: If set to 1, the application will abort on any error.
share[0]: If set to 1, opened files are not locked for exclusive use.

Pipe Mode
---------
Enabling pipe mode renders some commands less verbose because the output of
commands is limited to a single numerical code.
'd' will always output to console regardless of mode
", RESULT.OK);
        }

        private static void InlineHelp(Options OPT)
        {
            Status(OPT, @"List of Commands:
?    - Command overview

??   - More help

c    - Create a new file with the given name.
       Arg 1: File to create

o    - Open existing file for editing.
       Arg 1: File to open

cl   - Close current file
       No Arguments

del  - Close and delete current file from disk
       No Arguments

cat  - Concat files. Writes the content to the given file to the current file
       Arg 1: File to read data from

w    - Writes the given hex values to the currently open file
       Arg 1-n: a list of hexadecimal values, for example FE EDBE EF
       Spaces are optional. The prefix '0x' is not allowed and bytes must be
       padded with 0 to make them an even length
       A value can be prefixed:
       +  - Add value to current byte
       -  - Subtract value from current byte
       &  - Binary AND with current byte
       |  - Binary OR with current byte
       ^  - Binary XOR with current byte.
       The prefix applies to an entire list of values, +EDBE is
       identical to +ED +BE
       If you try to use prefixes and the pointer is at the file end, 0 is used
       as a base value.
       Mathematical operations that result in the value not being 0<=x<=255 has
       the overflow cut off by applying x&0xFF.

r    - Repeatedly write given bytes.
       Arg 1  : Number of repetitions
       Arg 2-n: Bytes. See 'w' for info on format

rnd  - Writes the given number of random bytes
       Arg 1: Number of random bytes. If negative, stops this many bytes before
              the file end

s    - Seek to the given position
       Arg 1: New Position
       Arg 2: B=Begin, C=Current, E=End

l    - Set absolute stream length. If bigger than current length, the file is
       extended, if smaller than the current length, the file is truncated.
       If the value is negative it will be subtracted from the current file
       position. If the value is prefixed with '+' it will be added to the
       current file position. Using -0 or +0 trims the file to the current
       file pointer position.
       Arg 1: New length with optional prefix

f    - Find values. Seeks to the start of the given hex values.
       If the values are not found, the original file position is restored.
       Search is performed forward only.

d    - Dumps the given number of bytes to the console as a hexadecimal view
       The dump is 16 bytes wide and contains hexadecimal values and ASCII
       renditions. If the number is larger than the remaining bytes, it's
       clamped down. Dumping WILL NOT advance the file pointer unless the
       number is prefixed with '+'
       Arg 1: Number of bytes to dump

stat - Prints status of the current file to the console.
       In case of Pipe mode, the number is the position in the file.
       If it's -1, no file is open.

opt  - Sets an option. Type '??' for details.
       The arguments are optional. Without arguments, this command shows all
       options and their values. With only one argument it shows that options
       value only.
       Arg 1: Name of the option.
       Arg 2: Value to assign to the option.

q    - Quit the application
       This will close an open file as if 'cl' was issued first.

", RESULT.OK);
        }
    }
}
