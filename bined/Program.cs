using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace bined
{

    public partial class Program
    {
        private static Stream FILE;
        private static string FileName;

        public static int Main(string[] args)
        {
            FILE = null;
            FileName = null;
            var OPT = new Options()
            {
                EnableOutput = true,
                PipeMode = false,
                Fail = false
            };

            if (args.Contains("/?"))
            {
                ShowHelp();
                return RET.HELP;
            }
            if (!Console.IsInputRedirected)
            {
                E("Welcome to BinEd. Type '?' to get editor help");
            }
            var IN = Console.In;
            //Allows us to exit by setting IN to null.
            while (IN != null)
            {
                var Line = IN.ReadLine();
                //If Line is null, the input stream read a CTRL+Z
                if (Line == null)
                {
                    return RET.OK;
                }
                else
                {
                    var C = GetCommand(Line);
                    switch (C.CommandType)
                    {
                        case CommandType.CreateFile:
                        case CommandType.OpenFile:
                            OpenFile(OPT, C, C.CommandType == CommandType.OpenFile);
                            break;
                        case CommandType.DeleteFile:
                            if (FILE == null)
                            {
                                Status(OPT, "no file open", RESULT.NOFILE);
                            }
                            else
                            {
                                FILE.Close();
                                FILE.Dispose();
                                FILE = null;
                                try
                                {
                                    File.Delete(FileName);
                                    Status(OPT, $"{FileName} closed and deleted", RESULT.OK);
                                }
                                catch (Exception ex)
                                {
                                    Status(OPT, $"{FileName} closed but unable to delete. {ex.Message}", RESULT.IO_ERROR);
                                }
                                FileName = null;
                            }
                            break;
                        case CommandType.CloseFile:
                            if (FILE == null)
                            {
                                Status(OPT, "no file open", RESULT.NOFILE);
                            }
                            else
                            {
                                FILE.Close();
                                FILE.Dispose();
                                FILE = null;
                                FileName = null;
                                Status(OPT, "File closed", RESULT.OK);
                            }
                            break;
                        case CommandType.WriteBytes:
                            WriteBytes(OPT, C);
                            break;
                        case CommandType.RepeatBytes:
                            RepeatBytes(OPT, C);
                            break;
                        case CommandType.SetLength:
                            SetLength(OPT, C);
                            break;
                        case CommandType.SeekTo:
                            SeekStream(OPT, C);
                            break;
                        case CommandType.ConcatFile:
                            Concat(OPT, C);
                            break;
                        case CommandType.Status:
                            //This command never fails
                            if (FILE != null)
                            {
                                Status(OPT, $"Position={FILE.Position} Length={FILE.Length} Name={FileName}", FILE.Position, true);
                            }
                            else
                            {
                                Status(OPT, $"No File open", -1, true);
                            }
                            break;
                        case CommandType.SetOption:
                            switch (C.Arguments.Length)
                            {
                                case 0:
                                    ShowOptions(OPT);
                                    break;
                                case 1:
                                    ShowOption(OPT, C.Arguments[0]);
                                    break;
                                case 2:
                                    OPT = SetOption(OPT, C.Arguments[0], C.Arguments[1]);
                                    break;
                                default:
                                    Status(OPT, $"Too many arguments", RESULT.ARGUMENT_MISMATCH, true);
                                    break;
                            }
                            break;
                        case CommandType.Help:
                            InlineHelp(OPT);
                            break;
                        case CommandType.HelpDetails:
                            DetailHelp(OPT);
                            break;
                        case CommandType.Quit:
                            Status(OPT, "Application exit", RESULT.OK);
                            IN = null;
                            break;
                        case CommandType.Invalid:
                        case CommandType.Blank:
                            Status(OPT, "Type ? for Help", C.CommandType == CommandType.Invalid ? RESULT.INVALID_COMMAND : RESULT.OK);
                            break;
                        default:
                            throw new NotImplementedException($"The operation {C.CommandType} has not been implemented yet");
                    }
                }
            }
            return RET.OK;
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
                        Status(OPT, "Unable to convert argument to byte instructions", RESULT.INVALID_ARG);
                    }
                    else
                    {
                        for (var i = 0; i < Count; i++)
                        {
                            foreach (var Opt in Operations)
                            {
                                if (!Opt.ProcessBytes(FILE))
                                {
                                    Status(OPT, $"Error writing to file", RESULT.IO_ERROR);
                                    return;
                                }
                            }
                        }
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
                    Status(OPT, "Unable to convert argument to byte instructions", RESULT.INVALID_ARG);
                }
                else
                {
                    foreach (var Opt in Operations)
                    {
                        if (!Opt.ProcessBytes(FILE))
                        {
                            Status(OPT, $"Error writing to file", RESULT.IO_ERROR);
                            return;
                        }
                    }
                    Status(OPT, $"Written={Operations.Sum(m => m.Bytes.LongLength)} Position={FILE.Position}", RESULT.OK);
                }
            }
            else
            {
                Status(OPT, "'w' requires at least one argument", RESULT.ARGUMENT_MISMATCH);
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

                        FILE.SetLength(L);
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
            if (FILE == null)
            {
                FileName = C.Arguments.FirstOrDefault();
                if (!string.IsNullOrEmpty(FileName))
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
                    if (FileName != null)
                    {
                        try
                        {
                            FILE = File.Open(FileName, IsOpen ? FileMode.Open : FileMode.CreateNew, FileAccess.ReadWrite);
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

Opening Files
-------------
Only one file can be opened at a time. Attempting to open/create a file
before closing the current file will result in an error.
Files opened with 'c' or 'o' are always opened in Read/Write mode.
If the given file has one of these attributes, the operation will fail:
ReadOnly, System, Hidden

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
- Success: The file pointer is set to the position where the given bytws start
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

out[1]: If set to 0 it will no longer output any status messages
pipe: If set to 1, the application will only output codes, no messages.
      Defaults to 1 if the input stream is redirected 
fatal[0]: If set to 1, the application will abort on any error.

Pipe Mode
---------
Enabling pipe mode renders some commands less verbose because the output of
commands is limited to a single numerical code.
", RESULT.OK);
        }

        private static void InlineHelp(Options OPT)
        {
            Status(OPT, @"List of Commands:
?    - Command overview

??   - More help

c    - Create the file with the given name. Empties existing files
       Arg 1: File to create/overwrite

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

s    - Seek to the given position
       Arg 1: New Position
       Arg 2: B=Begin, C=Current, E=End

l    - Set stream length. If bigger than current length, the file is extended,
       if smaller than the current length, the file is truncated.
       If the value is negative it will be subtracted from the current file
       position. Using '-0' thus trims the file to the current position.
       Arg 1: New length

f    - Find values. Seeks to the start of the given hex values.
       If the values are not found, the original file position is restored.
       Search is performed forward only.

d    - Dumps the given number of bytes to the console as a hexadecimal view
       The dump is 16 bytes wide and contains hexadecimal values and ASCII
       renditions.
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

        private static void Status(Options OPT, string Line, long Code, bool DontFail = false)
        {
            if (OPT.EnableOutput)
            {
                if (OPT.PipeMode)
                {
                    O("{0}", Code);
                }
                else
                {
                    O(Line);
                }
            }
            //Check failure conditions
            if (OPT.Fail && Code != RESULT.OK && !DontFail)
            {
                E("Aborting because Failure Mode has been turned on. Code={0}", Code);
                Environment.Exit((int)Math.Min(Code, int.MaxValue));
            }
        }

        private static int GetInt(string Param, int Default = 0)
        {
            long Ret = GetLong(Param, Default);
            if (Ret < int.MinValue || Ret > int.MaxValue)
            {
                return Default;
            }
            return (int)Ret;
        }

        private static long GetLong(string Param, long Default = 0)
        {
            long Ret = 0;
            if (!string.IsNullOrEmpty(Param))
            {
                Param = Param.Trim();
                if (Param.ToLower().StartsWith("0x") || Param.ToLower().StartsWith("-0x"))
                {
                    //Need to parse negative hexadecimal manually
                    var Factor = Param.StartsWith("-") ? -1L : 1L;
                    //Cut prefixes
                    Param = Param.Substring(Factor == 1 ? 2 : 3);
                    if (long.TryParse(Param, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out Ret))
                    {
                        return Ret * Factor;
                    }
                }
                else if (long.TryParse(Param, out Ret))
                {
                    return Ret;
                }
            }
            return Default;
        }

        private static byte[] GetBytes(string Param)
        {
            if (string.IsNullOrWhiteSpace(Param))
            {
                return null;
            }
            Param = Param.Trim();
            if (Param.Length % 2 != 0)
            {
                return null;
            }
            if (Param.ToLower().StartsWith("0x"))
            {
                Param = Param.Substring(2);
            }
            int[] Data = new int[Param.Length / 2];
            for (var i = 0; i < Data.Length; i++)
            {
                Data[i] = GetInt("0x" + Param.Substring(i * 2, 2), -1);
            }
            if (Data.Any(m => m < 0))
            {
                return null;
            }
            return Data.Select(m => (byte)m).ToArray();
        }

        private static ByteOperation[] GetByteOperations(IEnumerable<string> Param)
        {
            var Operations = Param.Select(m => new ByteOperation(m)).ToArray();
            if (Operations.Length == 0 || Operations.Any(m => m.Bytes == null || m.LastError != null))
            {
                return null;
            }
            //Consolidate identical consecutive modes to make writes faster
            var OPS = new List<ByteOperation>();
            OPS.Add(Operations[0]);
            for (var i = 1; i < Operations.Length; i++)
            {
                var Current = Operations[i];
                var Last = OPS.Last();
                if (Current.Mode == Last.Mode)
                {
                    //Consolidate identical mode
                    Last.Bytes = Last.Bytes.Concat(Current.Bytes).ToArray();
                    OPS[OPS.Count - 1] = Last;
                }
                else
                {
                    //Append different mode
                    OPS.Add(Current);
                }
            }
            return OPS.ToArray();
        }

        private static Command GetCommand(string Line)
        {
            Command C = new Command()
            {
                Arguments = null,
                CommandType = CommandType.Blank
            };

            if (!string.IsNullOrEmpty(Line))
            {
                var Segments = Line.Trim().Split(' ');
                switch (Segments[0].ToLower())
                {
                    case "cat":
                        C.CommandType = CommandType.ConcatFile;
                        C.Arguments = new string[] { string.Join(" ", Segments.Skip(1)) };
                        break;
                    case "w":
                        C.CommandType = CommandType.WriteBytes;
                        C.Arguments = Segments.Skip(1).ToArray();
                        break;
                    case "r":
                        C.CommandType = CommandType.RepeatBytes;
                        C.Arguments = Segments.Skip(1).ToArray();
                        break;
                    case "s":
                        C.CommandType = CommandType.SeekTo;
                        C.Arguments = Segments.Skip(1).ToArray();
                        break;
                    case "l":
                        C.CommandType = CommandType.SetLength;
                        C.Arguments = Segments.Skip(1).ToArray();
                        break;
                    case "q":
                        C.CommandType = CommandType.Quit;
                        break;
                    case "c":
                        C.CommandType = CommandType.CreateFile;
                        C.Arguments = new string[] { string.Join(" ", Segments.Skip(1)) };
                        break;
                    case "o":
                        C.CommandType = CommandType.OpenFile;
                        C.Arguments = new string[] { string.Join(" ", Segments.Skip(1)) };
                        break;
                    case "del":
                        C.CommandType = CommandType.DeleteFile;
                        break;
                    case "cl":
                        C.CommandType = CommandType.CloseFile;
                        break;
                    case "stat":
                        C.CommandType = CommandType.Status;
                        break;
                    case "opt":
                        C.CommandType = CommandType.SetOption;
                        C.Arguments = Segments.Skip(1).ToArray();
                        break;
                    case "?":
                        C.CommandType = CommandType.Help;
                        break;
                    case "??":
                        C.CommandType = CommandType.HelpDetails;
                        break;
                    default:
                        C.CommandType = CommandType.Invalid;
                        C.Arguments = Segments;
                        break;
                }
            }
            return C;
        }

        private static void ShowHelp()
        {
            E(@"bined [/?]
Binary file editor

/?  - Show this Help");
        }

        private static string Write(TextWriter Output, string Format, object[] Args)
        {
            if (Format == null)
            {
                Output.WriteLine();
                return null;
            }
            else if (Args == null || Args.Length == 0)
            {
                Output.WriteLine(Format);
            }
            else
            {
                Format = string.Format(Format, Args);
                Output.WriteLine(Format);
            }
            return Format;
        }

        public static string O(string Format, params object[] Args)
        {
            return Write(Console.Out, Format, Args);
        }

        public static string E(string Format, params object[] Args)
        {
            return Write(Console.Error, Format, Args);
        }
    }
}
