using System;
using System.IO;
using System.Linq;

namespace bined
{
    public struct RET
    {
        public const int OK = 0;
        public const int HELP = 1;

    }

    public struct RESULT
    {
        public const int OK = 0;
        public const int INVALID_COMMAND = 1;
        public const int NOFILE = 2;
        public const int ARGUMENT_MISMATCH = 3;
        public const int FILE_OPEN = 4;
        public const int IO_ERROR = 5;
    }

    public enum CommandType
    {
        Blank,
        Invalid,
        CreateFile,
        OpenFile,
        CloseFile,
        DeleteFile,
        ConcatFile,
        SetOption,
        WriteBytes,
        SeekTo,
        SetLength,
        Find,
        Dump,
        Status,
        Help,
        HelpDetails,
        Quit
    }

    public struct Command
    {
        public CommandType CommandType;
        public string[] Arguments;
    }


    public class Program
    {
        private struct Options
        {
            public bool EnableOutput;
            public bool PipeMode;
            public bool Fail;
        }

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
                E("Welcome to bined. Type '?' to get editor help");
            }
            var IN = Console.In;
            while (IN != null)
            {
                var Line = IN.ReadLine();
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
                                Status(OPT, "File closed", RESULT.OK);
                            }
                            break;
                        case CommandType.Status:
                            //Reports file status. This command never fails
                            if (FILE != null)
                            {
                                Status(OPT, $"Position={FILE.Position} Length={FILE.Length} Name={FileName}", FILE.Position, true);
                            }
                            else
                            {
                                Status(OPT, $"No File open", -1, true);
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
without setting the pointer to the end. This causes the content after the
pointer to be overwritten and some content to be appended if the given file
is large enough.

Trying to set the File offset out of bounds will clamp it into bounds.

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
The number in brackets is the default (if static)

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
       Arg 1: a list of hexadecimal values, for example FE ED BE EF
       Spaces are optional. A value can be prefixed with + or - to add or
       subtract to/from the current value. The prefix applies to an entire
       list of values, +FEED is identical to +FE +ED
       If you try to use prefixes and the pointer is at the file end, 0 is used
       as a base value.

s    - Seek to the given position
       Arg 1: Integer (prefix with '0x' for hexadecimal)
       Arg 2: S=Start, C=Current, E=End

l    - Set stream length. If bigger than current length, the file is extended,
       if smaller than the current length, the file is truncated.
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

        private static Command GetCommand(string Line)
        {
            Command C = new Command()
            {
                Arguments = null,
                CommandType = CommandType.Blank
            };

            if (!string.IsNullOrEmpty(Line))
            {
                var Segments = Line.Split(' ');
                switch (Segments[0].ToLower())
                {
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
                    case "cl":
                        C.CommandType = CommandType.CloseFile;
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
