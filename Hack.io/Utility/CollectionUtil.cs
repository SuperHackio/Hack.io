using System.Runtime.CompilerServices;

namespace Hack.io.Utility;

/// <summary>
/// A static class for functions relating to collections. (Array, List, etc.)
/// </summary>
public static class CollectionUtil
{
    /// <summary>
    /// Creates a new ICollection (Such as a <see cref="List{T}"/>) with a specified value for every entry
    /// </summary>
    /// <typeparam name="Class"></typeparam>
    /// <typeparam name="Type"></typeparam>
    /// <param name="InitValue"></param>
    /// <param name="Size"></param>
    /// <returns></returns>
    public static Class NewICollection<Class, Type>(Type InitValue, int Size) where Class : ICollection<Type>, new()
    {
        Class output = new();
        for (int i = 0; i < Size; i++)
            output.Add(InitValue);
        return output;
    }

    /// <summary>
    /// Creates a new Array with the provided value and length
    /// </summary>
    /// <typeparam name="T">An array type</typeparam>
    /// <param name="Value">The value to initlize with</param>
    /// <param name="Length">The desired array length</param>
    /// <returns></returns>
    public static T[] InitilizeArray<T>(T Value, int Length)
    {
        T[] arr = new T[Length];
        for (int i = 0; i < arr.Length; ++i)
            arr[i] = Value;
        return arr;
    }

    /// <summary>
    /// Swaps two values.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="Left">Our first contestant</param>
    /// <param name="Right">Our second contestant</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SwapValues<T>(ref T Left, ref T Right) => (Right, Left) = (Left, Right);
    /// <summary>
    /// Swaps two values using a Tuple
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="Values">The tuple to swap values of</param>
    public static void SwapValues<T>(ref Tuple<T, T> Values) => Values = new Tuple<T, T>(Values.Item2, Values.Item1);
    /// <summary>
    /// Cycles an set of objects. Can be cycled backwards
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="Reverse">If true, cycles from left to right instead of right to left</param>
    /// <param name="Values">The values to cycle</param>
    public static void CycleValues<T>(bool Reverse = false, params T[] Values)
    {
        T temp;
        if (Reverse)
        {
            temp = Values[^1];
            for (int i = Values.Length - 1; i >= 1; i--)
                Values[i + 1] = Values[i];
            Values[0] = temp;
        }
        else
        {
            temp = Values[0];
            for (int i = 1; i < Values.Length; i++)
                Values[i - 1] = Values[i];
            Values[^1] = temp;
        }
    }

    /// <summary>
    /// Compares the contents of two dictionaries
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    /// <param name="Left"></param>
    /// <param name="Right"></param>
    /// <returns></returns>
    public static bool Equals<TKey, TValue>(Dictionary<TKey, TValue> Left, Dictionary<TKey, TValue> Right)
        where TKey : notnull
    {
        if (Left is null && Right is null)
            return true;
        if (Left is null || Right is null)
            return false;
        if (Left.Count != Right.Count)
            return false;

        foreach (KeyValuePair<TKey, TValue> pair in Left)
        {
            if (Right.TryGetValue(pair.Key, out TValue? value))
            {
                // Require value be equal.
                if (!Equals(value, pair.Value))
                    return false;
                continue;
            }
            return false;
        }
        return true;
    }

    /// <summary>
    /// Finds out if a sequence exists in a list
    /// </summary>
    /// <typeparam name="T">List element type</typeparam>
    /// <param name="List">The larger list to look in</param>
    /// <param name="SubSequence">The smaller sequence to look for</param>
    /// <returns>TRUE if the sequence is found, FALSE otherwise</returns>
    public static bool ContainsSubsequence<T>(this IList<T> List, IList<T> SubSequence)
    {
        if (List.Count <= 0 || SubSequence.Count <= 0 || SubSequence.Count > List.Count)
            return false;
        return Enumerable.Range(0, List.Count - SubSequence.Count + 1).Any(n => List.Skip(n).Take(SubSequence.Count).SequenceEqual(SubSequence));
    }
    /// <summary>
    /// Finds a list inside a list
    /// </summary>
    /// <typeparam name="T">List element type</typeparam>
    /// <param name="List">The larger list to look in</param>
    /// <param name="Start">The index to Start searching from</param>
    /// <param name="SubList">The list to find</param>
    /// <returns>The index of the sublist. -1 if not found</returns>
    public static int SubListIndex<T>(this IList<T> List, int Start, IList<T> SubList)
    {
        for (int listIndex = Start; listIndex < List.Count - SubList.Count + 1; listIndex++)
        {
            int count = 0;
            while (count < SubList.Count && Equals(SubList[count], List[listIndex + count]))
                count++;
            if (count == SubList.Count)
                return listIndex;
        }
        return -1;
    }
    /// <summary>
    /// Moves an item to a new index in the list
    /// </summary>
    /// <typeparam name="T">List element type</typeparam>
    /// <param name="List">The list to move an item in</param>
    /// <param name="OldIndex">The original index of the item</param>
    /// <param name="NewIndex">The new index of the item</param>
    public static void Move<T>(this IList<T> List, int OldIndex, int NewIndex)
    {
        T item = List[OldIndex];
        List.RemoveAt(OldIndex);
        List.Insert(NewIndex, item);
    }
    /// <summary>
    /// Sort a list of items based on an array of items
    /// </summary>
    /// <typeparam name="T">List element type</typeparam>
    /// <param name="OriginalList">The original list to sort</param>
    /// <param name="SortRef">The list to reference while sorting</param>
    /// <returns>A new list sorted by "<paramref name="SortRef"/>".<para/>If <paramref name="SortRef"/> does not contain an element from <paramref name="OriginalList"/>, it will NOT be included!</returns>
    public static List<T> SortBy<T>(this IList<T> OriginalList, T[] SortRef)
    {
        List<T> FinalList = [];

        for (int i = 0; i < SortRef.Length; i++)
            if (OriginalList.Contains(SortRef[i]))
                FinalList.Add(SortRef[i]);

        return FinalList;
    }
    /// <summary>
    /// Compares the contents of two spans to see if they match
    /// </summary>
    /// <typeparam name="T">List element type</typeparam>
    /// <param name="Left">The list on the left side of the Equals Sign</param>
    /// <param name="Right">The list on the right side of the Equals Sign</param>
    /// <returns>TRUE if both lists match, FALSE otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Equals<T>(ReadOnlySpan<T> Left, ReadOnlySpan<T> Right) => Left.SequenceEqual(Right);
    /// <summary>
    /// Compares the contents of two nullable ararys to see if they match
    /// </summary>
    /// <typeparam name="T">List element type</typeparam>
    /// <param name="Left">The array on the left side of the Equals Sign</param>
    /// <param name="Right">The array on the right side of the Equals Sign</param>
    /// <returns>TRUE if both arrays match, FALSE otherwise.</returns>
    public static bool Equals<T>(T[]? Left, T[]? Right)
    {
        if (Left is null && Right is null)
            return true;
        if (Left is null || Right is null) //This works because we already checked to see if they're both null, so here it's impossible for these to both be null
            return false;

        return Equals(new ReadOnlySpan<T>(Left), new ReadOnlySpan<T>(Right));
    }
    /// <summary>
    /// Compares the contents of the two lists using a custom function
    /// </summary>
    /// <typeparam name="T">List element type</typeparam>
    /// <param name="Left">The list on the left side of the Equals Sign</param>
    /// <param name="Right">The list on the right side of the Equals Sign</param>
    /// <param name="comparefunc">The function to use as a comparator</param>
    /// <returns>TRUE if both lists match, FALSE otherwise.</returns>
    public static bool Equals<T>(ReadOnlySpan<T> Left, ReadOnlySpan<T> Right, Func<T?, T?, bool> comparefunc)
    {
        if (Left.Length != Right.Length)
            return false;

        for (int i = 0; i < Left.Length; i++)
            if (!comparefunc(Left[i], Right[i]))
                return false;
        return true;
    }

    /// <summary>
    /// Determines the index of a specific item in the collection using ReferenceEquals
    /// </summary>
    /// <param name="Source">The collection to look inparam>
    /// <param name="Item">The object to locate in the collection</param>
    /// <returns>The Index of the item, or -1 if it's not found.</returns>
    public static int IndexOfReference<T>(this IList<T> Source, T Item)
    {
        for (int i = 0; i < Source.Count; i++)
            if (ReferenceEquals(Source[i], Item))
                return i;
        return -1;
    }
}