using System;
using System.IO;
using System.Globalization;
using System.Diagnostics;

namespace RytheTributary
{
    internal class Logger
    {
        private static string log = "";

        private static Stopwatch timer;

        public static void Init()
        {
            timer = new Stopwatch();
            timer.Start();
        }

        public static void Log(string message, LogLevel logLevel = LogLevel.debug)
        {
            if (CLArgs.logLevel > logLevel)
                return;

            string msg = $"T+ {timer.Elapsed.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)} [{logLevel}] : {message}";

            if (logLevel >= LogLevel.error)
                Console.Error.WriteLine(msg);
            else
                Console.WriteLine(msg);

            log += $"{msg}\n";
        }

        public static void Log<T>(T obj, LogLevel logLevel = LogLevel.debug)
        {
            if (CLArgs.logLevel > logLevel)
                return;

            string msg = $"T+ {timer.Elapsed.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)} [{logLevel}] : {obj}";

            if (logLevel >= LogLevel.error)
                Console.Error.WriteLine(msg);
            else
                Console.WriteLine(msg);

            log += $"{msg}\n";
        }

        public static void PrintLn(string message, LogLevel logLevel = LogLevel.debug)
        {
            if (CLArgs.logLevel > logLevel)
                return;

            string msg = $"T+ {timer.Elapsed.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)} [{logLevel}] : {message}";

            if (logLevel >= LogLevel.error)
                Console.Error.WriteLine(msg);
            else
                Console.WriteLine(msg);
        }

        public static void PrintLn<T>(T obj, LogLevel logLevel = LogLevel.debug)
        {
            if (CLArgs.logLevel > logLevel)
                return;

            string msg = $"T+ {timer.Elapsed.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)} [{logLevel}] : {obj}";

            if (logLevel >= LogLevel.error)
                Console.Error.WriteLine(msg);
            else
                Console.WriteLine(msg);
        }

        public static void LogHidden(string message, LogLevel logLevel = LogLevel.debug)
        {
            if (CLArgs.logLevel > logLevel)
                return;

            log += $"T+ {timer.Elapsed.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)} [{logLevel}] : {message}\n";
        }

        public static void LogHidden<T>(T obj, LogLevel logLevel = LogLevel.debug)
        {
            if (CLArgs.logLevel > logLevel)
                return;

            log += $"T+ {timer.Elapsed.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)} [{logLevel}] : {obj}\n";
        }

        public static bool WriteToFile(string name, string path = "./", bool addTimeStamp = true)
        {
            if (log.Length == 0)
                return false;

            try
            {
                File.WriteAllText(path + name + (addTimeStamp ? DateTime.Now.ToString("dd-MM-yyyy_HH-mm-ss.ff") : "") + ".log", log);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return false;
            }

            if (CLArgs.logLevel <= LogLevel.info)
                Console.WriteLine($"T+ {timer.Elapsed.TotalSeconds.ToString("F6", CultureInfo.InvariantCulture)} [{LogLevel.info}] : Log written to file");

            return true;
        }
    }
}
