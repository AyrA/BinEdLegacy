using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BinEd
{
    /// <summary>
    /// Main Functions
    /// </summary>
    public partial class Program
    {
        /// <summary>
        /// Currently processed file
        /// </summary>
        private static Stream FILE;
        /// <summary>
        /// Currently open file name
        /// </summary>
        private static string FileName;
        /// <summary>
        /// Console Title for restoring it later
        /// </summary>
        private static string OldWindowTitle;

        /// <summary>
        /// Main Entry Point
        /// </summary>
        /// <param name="args">Command line Arguments</param>
        /// <returns>Exit Code</returns>
        public static int Main(string[] args)
        {
            OldWindowTitle = Console.Title;
#if DEBUG
            Environment.CurrentDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
#endif
            //Set Defaults
            FILE = null;
            FileName = null;
            var OPT = new Options()
            {
                EnableOutput = true,
                PipeMode = false,
                Fail = false,
                Share = false
            };

            //Provide Help
            if (args.Contains("/?"))
            {
                ShowHelp();
                return Exit(RET.HELP);
            }
            else if (args.Length > 0)
            {
                E("Invalid Command line Argument");
                return Exit(RET.INVALID);
            }
            if (!Console.IsInputRedirected)
            {
                E("Welcome to BinEd. Type '?' to get editor help");
            }
            SetStatus("Ready for File");
            var IN = Console.In;
            //Allows us to exit by setting IN to null.
            while (IN != null)
            {
                var Line = IN.ReadLine();
                //If Line is null, the input stream read a CTRL+Z or EOF
                if (Line == null)
                {
                    return Exit(RET.OK);
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
                                Status(OPT, "No file open", RESULT.NOFILE);
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
                                SetStatus("Ready for File");
                            }
                            break;
                        case CommandType.CloseFile:
                            if (FILE == null)
                            {
                                Status(OPT, "No file open", RESULT.NOFILE);
                            }
                            else
                            {
                                FILE.Close();
                                FILE.Dispose();
                                FILE = null;
                                FileName = null;
                                Status(OPT, "File closed", RESULT.OK);
                                SetStatus("Ready for File");
                            }
                            break;
                        case CommandType.Find:
                            FindContent(OPT, C);
                            break;
                        case CommandType.WriteBytes:
                            WriteBytes(OPT, C);
                            break;
                        case CommandType.RepeatBytes:
                            RepeatBytes(OPT, C);
                            break;
                        case CommandType.Dump:
                            DumpContents(OPT, C);
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
                                Status(OPT, "No File open", -1, true);
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
                                    Status(OPT, "Too many arguments", RESULT.ARGUMENT_MISMATCH, true);
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
                    if (FILE == null)
                    {
                        SetStatus("Ready for File");
                    }
                    else
                    {
                        SetStatus($"{FILE.Position}:{FILE.Length} {FileName}");
                    }
                }
            }
            return Exit(RET.OK);
        }

        public static void SetStatus(string StatusText)
        {
            Console.Title = string.IsNullOrWhiteSpace(StatusText) ? "BinEd" : $"BinEd: {StatusText}";
        }

        /// <summary>
        /// Exits the application and performs any cleanup not done automatically by the system
        /// </summary>
        /// <param name="ExitCode">ExitCode</param>
        /// <returns>Exit Code</returns>
        public static int Exit(int ExitCode)
        {
            Console.Title = OldWindowTitle;
            Environment.Exit(ExitCode);
            return ExitCode;
        }

        /// <summary>
        /// Converts a Line from user input to a Command and arguments
        /// </summary>
        /// <param name="Line">User input</param>
        /// <returns>Command structure</returns>
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
                    case "f":
                        C.CommandType = CommandType.Find;
                        C.Arguments = Segments.Skip(1).ToArray();
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
                    case "d":
                        C.CommandType = CommandType.Dump;
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

        /// <summary>
        /// Command Line Help
        /// </summary>
        private static void ShowHelp()
        {
            E(@"BinEd [/?]
Binary file editor optimized for script processing.
This editor focuses on editing binary files by means of commands that allow
for easy automation.

/?  - Show this Help

This application has no other arguments.
Use the inline help system inside the application to get a command listing.");
        }

        #region Utilities

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
                //Allow "+" Prefixing. Technically allows any number of plusses at the start
                Param = Param.Trim().TrimStart('+');
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

        private static string GetASCII(byte[] Bytes)
        {
            return Bytes == null ? "" : Encoding.ASCII.GetString(Bytes.Select(m => m < 32 || m == 0xFF ? (byte)0x2E : m).ToArray());
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

        #endregion

        #region Console Output

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
                Exit((int)Math.Min(Code, int.MaxValue));
            }
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

        #endregion
    }
}
