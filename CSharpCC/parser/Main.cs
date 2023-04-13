// Copyright 2011 Google Inc. All Rights Reserved.
// Author: sreeni@google.com (Sreeni Viswanadha)

/* Copyright (c) 2006, Sun Microsystems, Inc.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *
 *     * Redistributions of source code must retain the above copyright notice,
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the Sun Microsystems, Inc. nor the names of its
 *       contributors may be used to endorse or promote products derived from
 *       this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
 * THE POSSIBILITY OF SUCH DAMAGE.
 */
using org.javacc.utils;
using System.Security;
using System.Text;

namespace org.javacc.parser;



/**
 * Entry point.
 */
public class MainParser
{
    protected MainParser() { }

    public static LexGen LexGenerator;
    static void HelpMessage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("    javacc option-settings inputfile");
        Console.WriteLine("");
        Console.WriteLine("\"option-settings\" is a sequence of settings separated by spaces.");
        Console.WriteLine("Each option setting must be of one of the following forms:");
        Console.WriteLine("");
        Console.WriteLine("    -optionname=value (e.g., -STATIC=false)");
        Console.WriteLine("    -optionname:value (e.g., -STATIC:false)");
        Console.WriteLine("    -optionname       (equivalent to -optionname=true.  e.g., -STATIC)");
        Console.WriteLine("    -NOoptionname     (equivalent to -optionname=false. e.g., -NOSTATIC)");
        Console.WriteLine("");
        Console.WriteLine("Option settings are not case-sensitive, so one can say \"-nOsTaTiC\" instead");
        Console.WriteLine("of \"-NOSTATIC\".  Option values must be appropriate for the corresponding");
        Console.WriteLine("option, and must be either an integer, a boolean, or a string value.");
        Console.WriteLine("");

        // 2013/07/23 -- Changed this to auto-generate from metadata in Options so that help is always in-sync with codebase
        PrintOptions();

        Console.WriteLine("EXAMPLE:");
        Console.WriteLine("    javacc -STATIC=false -LOOKAHEAD:2 -debug_parser mygrammar.jj");
        Console.WriteLine("");
    }

    private static void PrintOptions()
    {

        HashSet<OptionInfo> options = Options.getUserOptions();

        int maxLengthInt = 0;
        int maxLengthBool = 0;
        int maxLengthString = 0;

        foreach (OptionInfo i in options)
        {
            int length = i.Name.Length;

            if (i.OptionType == OptionType.INTEGER)
            {
                maxLengthInt = length > maxLengthInt ? length : maxLengthInt;
            }
            else if (i.OptionType == OptionType.BOOLEAN)
            {
                maxLengthBool = length > maxLengthBool ? length : maxLengthBool;

            }
            else if (i.OptionType == OptionType.STRING)
            {
                maxLengthString = length > maxLengthString ? length : maxLengthString;

            }
            else
            {
                // Not interested
            }
        }

        if (maxLengthInt > 0)
        {
            Console.WriteLine("The integer valued options are:");
            Console.WriteLine("");
            foreach (OptionInfo i in options)
            {
                PrintOptionInfo(OptionType.INTEGER, i, maxLengthInt);
            }
            Console.WriteLine("");
        }


        if (maxLengthBool > 0)
        {
            Console.WriteLine("The boolean valued options are:");
            Console.WriteLine("");
            foreach (OptionInfo i in options)
            {
                PrintOptionInfo(OptionType.BOOLEAN, i, maxLengthBool);
            }
            Console.WriteLine("");
        }

        if (maxLengthString > 0)
        {
            Console.WriteLine("The string valued options are:");
            Console.WriteLine("");
            foreach (OptionInfo i in options)
            {
                PrintOptionInfo(OptionType.STRING, i, maxLengthString);
            }
            Console.WriteLine("");
        }
    }

    private static void PrintOptionInfo(OptionType filter, OptionInfo optionInfo, int padLength)
    {
        if (optionInfo.OptionType == filter)
        {
            object default1 = optionInfo.Default;
            Console.WriteLine("    " + PadRight(optionInfo.Name, padLength + 1) + (default1 == null ? "" : ("(default : " + (default1.ToString().Length == 0 ? "<<empty>>" : default1) + ")")));
        }
    }

    private static string PadRight(string name, int maxLengthInt)
    {
        int nameLength = name.Length;
        if (nameLength == maxLengthInt)
        {
            return name;
        }
        else
        {
            int charsToPad = maxLengthInt - nameLength;
            var sb = new StringBuilder(charsToPad);
            sb.Append(name);

            for (int i = 0; i < charsToPad; i++)
            {
                sb.Append(' ');
            }

            return sb.ToString();
        }
    }

    /**
       * A main program that exercises the parser.
       */
    public static void Main(string[] args)
    {
        int errorcode = MainProgram(args);
        Environment.Exit(errorcode);
    }

    /**
     * The method to call to exercise the parser from other Java programs.
     * It returns an error code.  See how the main program above uses
     * this method.
     */
    public static int MainProgram(string[] args)
    {

        if (args.Length == 1 && args[^1].Equals("-version", StringComparison.InvariantCultureIgnoreCase))
        {
            Console.WriteLine(Version.VersionNumber);
            return 0;
        }

        // Initialize all static state
        ReInitAll();

        JavaCCGlobals.BannerLine("Parser Generator", "");

        JavaCCParser parser = null;
        if (args.Length == 0)
        {
            Console.WriteLine("");
            HelpMessage();
            return 1;
        }
        else
        {
            Console.WriteLine("(type \"javacc\" with no arguments for help)");
        }

        if (Options.isOption(args[^1]))
        {
            Console.WriteLine("Last argument \"" + args[args.Length - 1] + "\" is not a filename.");
            return 1;
        }
        for (int arg = 0; arg < args.Length - 1; arg++)
        {
            if (!Options.isOption(args[arg]))
            {
                Console.WriteLine("Argument \"" + args[arg] + "\" must be an option setting.");
                return 1;
            }
            Options.setCmdLineOption(args[arg]);
        }




        try
        {
            string fp = (args[^1]);
            if (!File.Exists(fp))
            {
                Console.WriteLine("File " + args[^1] + " not found.");
                return 1;
            }
            if (Directory.Exists(fp))
            {
                Console.WriteLine(args[^1] + " is a directory. Please use a valid file name.");
                return 1;
            }
            parser = new JavaCCParser(new StreamReader(args[^1], Encoding.GetEncoding(Options.getGrammarEncoding())));
        }
        catch (SecurityException se)
        {
            Console.WriteLine("Security violation while trying to open " + args[^1]);
            return 1;
        }
        catch (FileNotFoundException e)
        {
            Console.WriteLine("File " + args[^1] + " not found.");
            return 1;
        }

        try
        {
            Console.WriteLine("Reading from file " + args[^1] + " . . .");
            JavaCCGlobals.FileName = JavaCCGlobals.OrigFileName = args[^1];
            JavaCCGlobals.JjtreeGenerated = JavaCCGlobals.IsGeneratedBy("JJTree", args[args.Length - 1]);
            JavaCCGlobals.ToolNames = JavaCCGlobals.GetToolNames(args[^1]);
            parser.javacc_input();

            // 2012/05/02 - Moved this here as cannot evaluate output language
            // until the cc file has been processed. Was previously setting the 'lg' variable
            // to a lexer before the configuration override in the cc file had been read.
            string outputLanguage = Options.getOutputLanguage();
            // TODO :: CBA --  Require Unification of output language specific processing into a single Enum class
            bool isJavaOutput = Options.isOutputLanguageJava();
            bool isCPPOutput = outputLanguage == (Options.OUTPUT_LANGUAGE__CPP);

            // 2013/07/22 Java Modern is a
            bool isJavaModern = isJavaOutput && Options.getJavaTemplateType() == (Options.JAVA_TEMPLATE_TYPE_MODERN);

            if (isJavaOutput)
            {
                LexGenerator = new LexGen();
            }
            else if (isCPPOutput)
            {
                LexGenerator = new LexGenCPP();
            }
            else
            {
                return UnhandledLanguageExit(outputLanguage);
            }

            JavaCCGlobals.CreateOutputDir(Options.getOutputDirectory());

            if (Options.getUnicodeInput())
            {
                NfaState.unicodeWarningGiven = true;
                Console.WriteLine("Note: UNICODE_INPUT option is specified. " +
                     "Please make sure you create the parser/lexer using a Reader with the correct character encoding.");
            }

            Semanticize.Start();
            bool isBuildParser = Options.getBuildParser();

            // 2012/05/02 -- This is not the best way to add-in GWT support, really the code needs to turn supported languages into enumerations
            // and have the enumerations describe the deltas between the outputs. The current approach means that per-langauge configuration is distributed
            // and small changes between targets does not benefit from inheritance.
            if (isJavaOutput)
            {
                if (isBuildParser)
                {
                    new ParseGen().Start(isJavaModern);
                }

                // Must always create the lexer object even if not building a parser.
                new LexGen().start();

                Options.setStringOption(Options.NONUSER_OPTION__PARSER_NAME, JavaCCGlobals.CuName);
                OtherFilesGen.start(isJavaModern);
            }
            else if (isCPPOutput)
            { // C++ for now
                if (isBuildParser)
                {
                    new ParseGenCPP().start();
                }
                if (isBuildParser)
                {
                    new LexGenCPP().start();
                }
                Options.setStringOption(Options.NONUSER_OPTION__PARSER_NAME, JavaCCGlobals.CuName);
                OtherFilesGenCPP.start();
            }
            else
            {
                UnhandledLanguageExit(outputLanguage);
            }



            if ((JavaCCErrors.GetErrorCount() == 0) && (isBuildParser || Options.getBuildTokenManager()))
            {
                if (JavaCCErrors.GetWarningCount() == 0)
                {
                    if (isBuildParser)
                    {
                        Console.WriteLine("Parser generated successfully.");
                    }
                }
                else
                {
                    Console.WriteLine("Parser generated with 0 errors and "
                                       + JavaCCErrors.GetWarningCount() + " warnings.");
                }
                return 0;
            }
            else
            {
                Console.WriteLine("Detected " + JavaCCErrors.GetErrorCount() + " errors and "
                                   + JavaCCErrors.GetWarningCount() + " warnings.");
                return (JavaCCErrors.GetErrorCount() == 0) ? 0 : 1;
            }
        }
        catch (MetaParseException e)
        {
            Console.WriteLine("Detected " + JavaCCErrors.GetErrorCount() + " errors and "
                               + JavaCCErrors.GetWarningCount() + " warnings.");
            return 1;
        }
        catch (ParseException e)
        {
            Console.WriteLine(e.ToString());
            Console.WriteLine("Detected " + (JavaCCErrors.GetErrorCount() + 1) + " errors and "
                               + JavaCCErrors.GetWarningCount() + " warnings.");
            return 1;
        }
    }

    private static int UnhandledLanguageExit(string outputLanguage)
    {
        Console.WriteLine("Invalid '" + Options.USEROPTION__OUTPUT_LANGUAGE + "' specified : " + outputLanguage);
        return 1;
    }

    public static void ReInitAll()
    {
        Expansion.ReInit();
        JavaCCErrors.ReInit();
        JavaCCGlobals.ReInit();
        Options.Init();
        JavaCCParserInternals.ReInit();
        RStringLiteral.reInit();
        JavaFiles.ReInit();
        NfaState.reInit();
        MatchInfo.ReInit();
        LookaheadWalk.reInit();
        Semanticize.reInit();
        OtherFilesGen.reInit();
        LexGen.reInit();
    }

}
