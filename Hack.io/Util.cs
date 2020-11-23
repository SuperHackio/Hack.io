using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections;

namespace Hack.ConsoleEx
{
    /// <summary>
    /// Extension of the console
    /// </summary>
    public static class ConsoleEx
    {
        /// <summary>
        /// Checks to see if the user presses certain keys
        /// </summary>
        /// <param name="YesKey">The key to indicate "Yes"</param>
        /// <param name="NoKey">The key to indicate "No"</param>
        /// <returns>true if the user presses the YesKey</returns>
        public static bool Confirm(ConsoleKey YesKey = ConsoleKey.Y, ConsoleKey NoKey = ConsoleKey.N)
        {
            ConsoleKey response;
            Console.WriteLine();
            do
            {
                response = Console.ReadKey(false).Key;
            } while (response != YesKey && response != NoKey);

            return (response == YesKey);
        }
        /// <summary>
        /// Writes a coloured message to the console
        /// </summary>
        /// <param name="message">Message to print</param>
        /// <param name="ForeColour">ConsoleColor to use for the text</param>
        /// <param name="BackColour">ConsoleColor to use for the background of the text</param>
        /// <param name="newline">Switch to the next line?</param>
        public static void WriteColoured(string message, ConsoleColor ForeColour = ConsoleColor.White, ConsoleColor BackColour = ConsoleColor.Black, bool newline = false)
        {
            Console.BackgroundColor = BackColour;
            Console.ForegroundColor = ForeColour;
            if (newline)
                Console.WriteLine(message);
            else
                Console.Write(message);
            Console.ResetColor();
        }
        /// <summary>
        /// Quits the Console with a given error message
        /// </summary>
        /// <param name="ExitCode">Code to exit with.</param>
        /// <param name="ErrorMsg">The error message</param>
        public static void Quit(int ExitCode = 0, string ErrorMsg = "An error has occured")
        {
            System.Console.WriteLine();
            WriteColoured($"ERROR: {ErrorMsg}", ConsoleColor.Red, newline: true);
            WriteColoured($"[Error Code 0x{ExitCode.ToString().PadLeft(4, '0')}]", ConsoleColor.DarkRed);
            System.Console.WriteLine();
            System.Console.WriteLine();
            System.Console.WriteLine("Press any key to exit");
            System.Console.ReadKey();
            Environment.Exit(ExitCode);
        }
    }
}

namespace Hack.io.Util
{
    /// <summary>
    /// Extra List functions
    /// </summary>
    public static class ListEx
    {
        /// <summary>
        /// Finds out if a sequence exists in a list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="sequence"></param>
        /// <param name="subsequence"></param>
        /// <returns></returns>
        public static bool ContainsSubsequence<T>(this IList<T> sequence, IList<T> subsequence)
        {
            if (sequence.Count == 0 || subsequence.Count > sequence.Count)
                return false;
            var yee = Enumerable.Range(0, sequence.Count - subsequence.Count + 1).Any(n => sequence.Skip(n).Take(subsequence.Count).SequenceEqual(subsequence));
            return yee;
        }
        /// <summary>
        /// Finds a list inside a list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="start">The index to Start searching from</param>
        /// <param name="sublist">The list to find</param>
        /// <returns></returns>
        public static int SubListIndex<T>(this IList<T> list, int start, IList<T> sublist)
        {
            for (int listIndex = start; listIndex < list.Count - sublist.Count + 1; listIndex++)
            {
                int count = 0;
                while (count < sublist.Count && sublist[count].Equals(list[listIndex + count]))
                    count++;
                if (count == sublist.Count)
                    return listIndex;
            }
            return -1;
        }
        /// <summary>
        /// Moves an item X distance in a list
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="list"></param>
        /// <param name="OldIndex">The original index of the item</param>
        /// <param name="NewIndex">The new index of the item</param>
        /// <returns></returns>
        public static void Move<T>(this IList<T> list,int OldIndex, int NewIndex)
        {
            T item = list[OldIndex];
            list.RemoveAt(OldIndex);
            list.Insert(NewIndex, item);
        }
        /// <summary>
        /// Sort a list of items based on an array of items
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="OriginalList"></param>
        /// <param name="sortref">The list to reference while sorting</param>
        /// <returns></returns>
        public static List<T> SortBy<T>(this List<T> OriginalList, T[] sortref)
        {
            List<T> FinalList = new List<T>();

            for (int i = 0; i < sortref.Length; i++)
                if (OriginalList.Contains(sortref[i]))
                    FinalList.Add(sortref[i]);

            return FinalList;
        }
    }
    /// <summary>
    /// Extra String functions
    /// </summary>
    public static class StringEx
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string WildCardToRegular(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("message", nameof(value));

            return "^" + Regex.Escape(value).Replace("\\?", ".").Replace("\\*", ".*") + "$";
        }
    }
    /// <summary>
    /// Extra FileInfo functions
    /// </summary>
    public static class FileInfoEx
    {
        /// <summary>
        /// Check if a file cannot be opened.
        /// </summary>
        /// <param name="file">File to check for</param>
        /// <returns>If the file is locked, returns true.</returns>
        [DebuggerStepThrough]
        public static bool IsFileLocked(this FileInfo file)
        {
            FileStream stream = null;

            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }
    }
    /// <summary>
    /// Extra BitArray functions
    /// </summary>
    public static class BitArrayEx
    {
        /// <summary>
        /// Converts this BitArray to an Int32
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static int ToInt32(this BitArray array)
        {
            if (array.Length > 32)
                throw new ArgumentException("Argument length shall be at most 32 bits.");

            int[] Finalarray = new int[1];
            array.CopyTo(Finalarray, 0);
            return Finalarray[0];
        }
    }
}